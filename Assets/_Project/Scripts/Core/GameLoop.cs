using System;
using System.Collections.Generic;
using System.Linq;
using BurningAnt.Character;
using BurningAnt.Items;
using BurningAnt.Loan;
using BurningAnt.Market;
using BurningAnt.News;
using UnityEngine;

namespace BurningAnt.Core
{
    /// <summary>하루 루프 단계 (기획서 2장). Prep = 아이템 사용 + 장 전 예약 주문.</summary>
    public enum GamePhase
    {
        News,       // 1. 뉴스 탐색
        Prep,       // 2. 아이템 사용 (장 전 예약 주문 포함)
        Market,     // 3. 장 시간 (60초)
        Settlement, // 4. 장 결과: 정산, 이자, 상환, 추가 대출
        Shop,       // 5. 아이템 구매
        GameOver,   // 승리 또는 파산
    }

    /// <summary>
    /// 하루 루프 전체를 진행한다: 뉴스 → 아이템 → 장 → 정산 → 상점 → 다음 날.
    /// 장 자체는 MarketSession이 맡고, 여기서는 단계 전환·뉴스·대출·아이템·승패만 다룬다.
    /// UI는 이벤트를 구독하고 public 메서드로만 요청한다.
    /// </summary>
    public class GameLoop : MonoBehaviour
    {
        [SerializeField] MarketSession session;
        [SerializeField] LoanSettings loanSettings;
        [SerializeField] NewsSettings newsSettings;
        [SerializeField] CharacterSettings characterSettings;
        [SerializeField, Tooltip("상점 아이템 전체 목록(예정 20종). 매일 이 중에서 Shop Slots 개를 무작위로 진열한다.")]
        List<ItemDefinition> shopItems = new();
        [SerializeField, Tooltip("하루에 진열하는 아이템 수 (상점 화면 칸 수, 2×4 = 8)")] int shopSlots = 8;
        [SerializeField, Tooltip("0이면 매번 무작위")] int seed;

        enum AutoMode { StopLoss, TakeProfit, Trailing, TargetBuy }

        class AutoSell
        {
            public StockState Stock;
            public ItemDefinition Item;
            public AutoMode Mode;
            public float Peak;        // 추적 손절: 지금까지 최고 수익률
            public float BuyPrice;    // 목표가 예약: 이 가격 이하가 되면 매수
            public bool IsStopLoss => Mode == AutoMode.StopLoss;
        }

        int interestDeferrals;      // 이자 유예 신청서: 남은 면제 횟수
        ItemDefinition insurance;   // 손실 보험 증서: 오늘 걸려 있으면

        readonly List<NewsItem> todayNews = new();
        readonly List<string> hints = new();
        readonly List<AutoSell> autoSells = new();
        System.Random rng;
        NewsGenerator newsGenerator;
        float slowRemaining, slowMultiplier = 1f, stopRemaining;
        int telescopeUntilTick = -1;

        public MarketSession Session => session;
        public CharacterSettings Character => characterSettings;
        public IReadOnlyList<ItemDefinition> ShopItems => shopItems;
        readonly List<ItemDefinition> todayShop = new();
        /// <summary>오늘 상점에 진열된 아이템 (상점 단계에 들어갈 때 새로 뽑는다).</summary>
        public IReadOnlyList<ItemDefinition> TodayShop => todayShop;
        public int ShopSlots => shopSlots;
        public LoanAccount Loan { get; private set; }
        public LoanSettings LoanSettings => loanSettings;
        public Inventory Inventory { get; private set; }
        public GamePhase Phase { get; private set; } = GamePhase.News;
        public int Day => session.Day;
        public IReadOnlyList<NewsItem> TodayNews => todayNews;
        /// <summary>뉴스 없던 종목을 예지해서 생긴 템플릿 (플레이어만 앎, 결과 시점에 속보로 공개).</summary>
        public IReadOnlyList<NewsItem> Foresights => foresights;
        readonly List<NewsItem> foresights = new();
        public IReadOnlyList<string> Hints => hints;
        public float DayStartAssets { get; private set; }
        public float LastInterest { get; private set; }
        public bool Won { get; private set; }
        /// <summary>오늘 장 마감 정산에서 마이너스 종목 때문에 잔고에서 빠진 금액과 정리된 종목.</summary>
        public float LastSettlementCharge { get; private set; }
        public IReadOnlyList<StockState> LastClosedStocks => lastClosed;
        /// <summary>정산 후 현금이 0 밑이고 긴급 대출을 아직 안 썼다 → 받을지 고르기 전까지 4단계로 못 감.</summary>
        public bool NeedsEmergency { get; private set; }
        readonly List<StockState> lastClosed = new();

        public float SlowRemaining => slowRemaining;
        public float StopRemaining => stopRemaining;
        /// <summary>망원경: 차트에 미리 보여줄 미래 틱 수.</summary>
        public int PreviewTicks => Phase == GamePhase.Market && telescopeUntilTick > session.Tick ? telescopeUntilTick - session.Tick : 0;

        /// <summary>오늘 하루 기록 (2단계 흐름 표시, 4단계 결과창). 지난 날들은 History.</summary>
        public DailyReport Today { get; private set; }
        readonly List<DailyReport> history = new();
        public IReadOnlyList<DailyReport> History => history;

        /// <summary>튜토리얼 등 바깥에서 장을 잠시 멈출 때 true (장 시간과 아이템 효과 시간이 모두 멈춘다).</summary>
        public bool ExternalPause { get; set; }

        public event Action Changed;
        public event Action PhaseChanged;
        public event Action<string> Message;

        void Awake()
        {
            rng = seed == 0 ? new System.Random() : new System.Random(seed);
            newsGenerator = new NewsGenerator(newsSettings, rng);
            Loan = new LoanAccount(loanSettings);
            Inventory = new Inventory();
        }

        void Start()
        {
            session.PhaseChanged += OnMarketPhaseChanged;
            session.Ticked += OnTicked;
            session.Traded += OnTraded;
            session.ReservationsExecuted += OnReservationsExecuted;
            BeginDay();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (session == null) return;
            session.PhaseChanged -= OnMarketPhaseChanged;
            session.Ticked -= OnTicked;
            session.Traded -= OnTraded;
            session.ReservationsExecuted -= OnReservationsExecuted;
        }

        void Update()
        {
            if (Phase != GamePhase.Market) return;
            if (ExternalPause)
            {
                Time.timeScale = 0f;
                return;
            }

            float dt = Time.unscaledDeltaTime;
            if (stopRemaining > 0f) stopRemaining = Mathf.Max(0f, stopRemaining - dt);
            if (slowRemaining > 0f) slowRemaining = Mathf.Max(0f, slowRemaining - dt);
            Time.timeScale = stopRemaining > 0f ? 0f : slowRemaining > 0f ? slowMultiplier : 1f;
        }

        // ───────── 자산 ─────────

        public float StockValue()
        {
            // 3배 손익이 들어간 평가금 합계. 마이너스 종목이 있으면 줄어들고, 전체가 마이너스일 수도 있다.
            float sum = 0f;
            foreach (var s in session.Stocks)
                sum += session.Portfolio.GetHolding(s).Value(session.CurrentPrice(s));
            return sum;
        }

        /// <summary>현금 + 주식 평가액</summary>
        public float TotalAssets() => session.Portfolio.Cash + StockValue();

        /// <summary>순자산 = 현금 + 주식 − 빚 (옷 단계 기준)</summary>
        public float NetWorth() => TotalAssets() - Loan.Debt;

        /// <summary>그날 수익률 (표정 기준). 뉴스 단계 시점 총자산 대비.</summary>
        public float TodayReturn() => DayStartAssets > 0f ? TotalAssets() / DayStartAssets - 1f : 0f;

        // ───────── 단계 전환 ─────────

        void BeginDay()
        {
            hints.Clear();
            autoSells.Clear();
            ResetMarketEffects();

            todayNews.Clear();
            foresights.Clear();
            todayNews.AddRange(newsGenerator.GenerateDay(Day, session.Stocks, session.Settings.ticksPerDay));
            newsGenerator.ApplyToPrices(session.Stocks, todayNews, session.Settings);

            session.Portfolio.FeeWaived = false; // 은행 VIP 쿠폰은 그날만
            insurance = null;                    // 손실 보험도 그날만
            DayStartAssets = TotalAssets();
            StartReport();
            SetPhase(GamePhase.News);
        }

        /// <summary>뉴스 확인 → 아이템 사용·예약 단계</summary>
        public void ConfirmNews()
        {
            if (Phase != GamePhase.News) return;
            Today.NewsChecked = true;
            SetPhase(GamePhase.Prep);
        }

        // ───────── 하루 기록 ─────────

        void StartReport()
        {
            Today = new DailyReport { Day = Day, StartAssets = DayStartAssets };
            Today.News.AddRange(todayNews);
            foreach (var s in session.Portfolio.HeldStocks)
                Today.Line(s).StartValue = session.Portfolio.GetHolding(s).Value(session.CurrentPrice(s));
        }

        void OnTraded(TradeResult r, float cashDelta)
        {
            if (Today == null) return;
            var line = Today.Line(r.Stock);
            line.NetCash += cashDelta;
            line.Fees += r.Shares * r.Price * session.Settings.feeRate;
            if (r.Side == OrderSide.Buy)
            {
                line.Buys++;
                line.BoughtShares += r.Shares;
                line.BuyAmount += r.Shares * r.Price;
            }
            else
            {
                line.Sells++;
                line.SoldShares += r.Shares;
                line.SellAmount += r.Shares * r.Price;
            }
        }

        void OnReservationsExecuted(IReadOnlyList<TradeResult> results)
        {
            if (Today == null) return;
            foreach (var r in results)
                if (r.Success) Today.ReservationsFilled++;
        }

        /// <summary>장 마감 직후(마이너스 정산 전) 종목별 평가금과 총자산을 기록한다.</summary>
        void CloseReport()
        {
            foreach (var s in session.Stocks)
            {
                var h = session.Portfolio.GetHolding(s);
                if (h.Shares > 0 || Today.Stocks.ContainsKey(s))
                    Today.Line(s).EndValue = h.Shares > 0 ? h.Value(session.CurrentPrice(s)) : 0f;
            }
            Today.EndAssets = TotalAssets();
            Today.Closed = true;
            history.Add(Today);
        }

        void OnMarketPhaseChanged()
        {
            switch (session.Phase)
            {
                case MarketPhase.Open:
                    Today.ReservationsPlaced = session.Reservations.Count; // 이 시점엔 아직 체결 전
                    SetPhase(GamePhase.Market);
                    break;
                case MarketPhase.Closed:
                    EnterSettlement();
                    break;
                case MarketPhase.PreMarket:
                    BeginDay();
                    break;
            }
        }

        void EnterSettlement()
        {
            ResetMarketEffects();
            autoSells.Clear();
            RevealDueNews(); // 장 마감에 뜨는 결과 기사

            CloseReport();

            // 장 마감 정산: 마이너스 종목은 정리되고 그 마이너스만큼 잔고에서 빠진다
            lastClosed.Clear();
            LastSettlementCharge = session.Portfolio.SettleNegatives(session.CurrentPrice, lastClosed);
            ApplyInsurance();
            LastInterest = 0f;
            if (Loan.IsInterestDay(Day))
            {
                if (interestDeferrals > 0)
                {
                    interestDeferrals--;
                    Today.InterestDeferred = true;
                }
                else LastInterest = Loan.ApplyInterest();
            }
            Today.SettlementCharge = LastSettlementCharge;
            Today.ClosedStocks.AddRange(lastClosed);
            Today.Interest = LastInterest;

            if (session.Portfolio.Cash < 0f)
            {
                if (Loan.EmergencyUsed)
                {
                    GoBankrupt();
                    return;
                }
                NeedsEmergency = true; // 4단계 직전: 긴급 대출을 받을지 고른다
            }
            SetPhase(GamePhase.Settlement);
        }

        /// <summary>긴급 대출 수락: 모자란 금액 + 여유분을 빌려 잔고를 0 이상으로 만든다 (한 판에 1번).</summary>
        public void AcceptEmergencyLoan()
        {
            if (!NeedsEmergency) return;
            float amount = Loan.TakeEmergencyLoan(-session.Portfolio.Cash);
            NeedsEmergency = false;
            if (amount <= 0f)
            {
                GoBankrupt();
                return;
            }
            session.Portfolio.Deposit(amount);
            Today.EmergencyLoan += amount;
            Say($"긴급 대출 ${amount:N0} (이자율 {Loan.InterestRate * 100f:0}%)");
            Changed?.Invoke();
        }

        public void DeclineEmergencyLoan()
        {
            if (!NeedsEmergency) return;
            NeedsEmergency = false;
            GoBankrupt();
        }

        void GoBankrupt()
        {
            Won = false;
            SetPhase(GamePhase.GameOver);
        }

        public void GoToShop()
        {
            if (Phase != GamePhase.Settlement || NeedsEmergency) return;
            RollShop();
            SetPhase(GamePhase.Shop);
        }

        public void StartNextDay()
        {
            if (Phase != GamePhase.Shop) return;
            session.NextDay(); // → PreMarket 이벤트 → BeginDay
        }

        void SetPhase(GamePhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke();
            Changed?.Invoke();
        }

        // ───────── 대출 ─────────

        public void Repay(float amount)
        {
            if (Phase != GamePhase.Settlement) return;
            float pay = Mathf.Min(amount, session.Portfolio.Cash, Loan.Debt);
            if (pay <= 0f)
            {
                Say("갚을 현금이 없어요");
                return;
            }

            session.Portfolio.Withdraw(pay);
            Loan.Repay(pay);
            Today.Repaid += pay;
            Say($"${pay:N2} 갚았어요");

            if (Loan.IsPaidOff)
            {
                Won = true;
                SetPhase(GamePhase.GameOver);
                return;
            }
            Changed?.Invoke();
        }

        /// <summary>추가 대출은 2단계(아이템·예약)에서만.</summary>
        public void TakeExtraLoan()
        {
            if (Phase != GamePhase.Prep)
            {
                Say("추가 대출은 아이템 단계(2단계)에서만 받을 수 있어요");
                return;
            }
            float amount = Loan.TakeExtraLoan();
            if (amount <= 0f)
            {
                Say("추가 대출을 모두 썼어요");
                return;
            }
            session.Portfolio.Deposit(amount);
            Today.ExtraLoan += amount;
            Say($"추가 대출 ${amount:N0} (이자율 {Loan.InterestRate * 100f:0}%)");
            Changed?.Invoke();
        }

        // ───────── 상점 · 아이템 ─────────

        /// <summary>전체 목록에서 겹치지 않게 shopSlots개를 무작위로 고른다. 목록이 더 적으면 전부 진열.</summary>
        void RollShop()
        {
            todayShop.Clear();
            var pool = new List<ItemDefinition>();
            foreach (var item in shopItems)
                if (item != null) pool.Add(item);
            int count = Mathf.Min(shopSlots, pool.Count);
            for (int i = 0; i < count; i++)
            {
                int pick = rng.Next(i, pool.Count);
                (pool[i], pool[pick]) = (pool[pick], pool[i]);
                todayShop.Add(pool[i]);
            }
        }

        public void Buy(ItemDefinition item)
        {
            if (Phase != GamePhase.Shop || !todayShop.Contains(item)) return;
            if (!session.Portfolio.Withdraw(item.price))
            {
                Say("현금이 부족해요");
                return;
            }
            Inventory.Add(item);
            Say($"{item.displayName} 구매");
            Changed?.Invoke();
        }

        public bool CanUseNow(ItemDefinition item) =>
            Inventory.Count(item) > 0 &&
            (item.Timing == ItemTiming.Prep ? Phase == GamePhase.Prep : Phase == GamePhase.Market && session.Phase == MarketPhase.Open);

        /// <summary>아이템 사용. 대상이 필요 없는 아이템은 target = null.</summary>
        public void UseItem(ItemDefinition item, StockState target)
        {
            if (!CanUseNow(item))
            {
                Say(item.Timing == ItemTiming.Prep ? "장 전(아이템 시간)에만 쓸 수 있어요" : "장 시간에만 쓸 수 있어요");
                return;
            }
            if (item.NeedsTarget && target == null) return;

            string result;
            bool? hintCorrect = null;
            bool revealTomorrow = false;
            var usedOn = target;
            switch (item.kind)
            {
                // 예지: 틀리지 않는다. 뉴스 종목이면 숨겨진 결과를, 아니면 템플릿을 하나 꺼내 고정하고 보여준다.
                case ItemKind.Rumor:
                {
                    usedOn = session.Stocks[rng.Next(session.Stocks.Count)];
                    if (!Foresee(item, usedOn, false, out result)) return;
                    hintCorrect = true;
                    break;
                }
                case ItemKind.AnalystReport:
                {
                    if (!Foresee(item, target, false, out result)) return;
                    hintCorrect = true;
                    break;
                }
                case ItemKind.InsiderMemo:
                {
                    if (!Foresee(item, target, true, out result)) return;
                    hintCorrect = true;
                    break;
                }
                case ItemKind.ChainForecast: // 뉴스 결과 예측: 오늘 뉴스 하나의 결과를 미리 본다
                {
                    var pending = todayNews.Where(n => !n.Foreseen).ToList();
                    if (pending.Count == 0)
                    {
                        Say("미리 볼 뉴스가 없어요");
                        return;
                    }
                    usedOn = pending[rng.Next(pending.Count)].Stock;
                    if (!Foresee(item, usedOn, true, out result)) return;
                    hintCorrect = true;
                    break;
                }
                case ItemKind.StopLoss:
                case ItemKind.TakeProfit:
                {
                    if (session.Portfolio.GetHolding(target).Shares < 1)
                    {
                        Say("보유한 종목에만 걸 수 있어요");
                        return;
                    }
                    bool stop = item.kind == ItemKind.StopLoss;
                    var mode = stop ? AutoMode.StopLoss : AutoMode.TakeProfit;
                    autoSells.RemoveAll(a => a.Stock == target && a.Mode == mode);
                    autoSells.Add(new AutoSell { Stock = target, Item = item, Mode = mode });
                    result = $"{item.displayName}: {target.Name} 평단 대비 {(stop ? "-" : "+")}{item.triggerRate * 100f:0}%(손익 기준)에 닿으면 전량 자동 매도";
                    break;
                }
                case ItemKind.TrailingStop:
                {
                    var h = session.Portfolio.GetHolding(target);
                    if (h.Shares < 1)
                    {
                        Say("보유한 종목에만 걸 수 있어요");
                        return;
                    }
                    autoSells.RemoveAll(a => a.Stock == target && a.Mode == AutoMode.Trailing);
                    autoSells.Add(new AutoSell { Stock = target, Item = item, Mode = AutoMode.Trailing, Peak = h.ReturnRate(session.CurrentPrice(target)) });
                    result = $"{item.displayName}: {target.Name} 최고 수익률에서 {item.triggerRate * 100f:0}%p 밀리면 전량 자동 매도";
                    break;
                }
                case ItemKind.TargetBuy:
                {
                    float buyAt = session.CurrentPrice(target) * (1f - item.triggerRate);
                    autoSells.RemoveAll(a => a.Stock == target && a.Mode == AutoMode.TargetBuy);
                    autoSells.Add(new AutoSell { Stock = target, Item = item, Mode = AutoMode.TargetBuy, BuyPrice = buyAt });
                    result = $"{item.displayName}: {target.Name}이 ${buyAt:N2}(-{item.triggerRate * 100f:0}%) 이하가 되면 현금 전부로 자동 매수";
                    break;
                }
                case ItemKind.SoulAllIn:
                {
                    bool before = session.Portfolio.FeeWaived;
                    session.Portfolio.FeeWaived = true;
                    var r = session.Trade(new OrderRequest(target, OrderSide.Buy, 0, true));
                    session.Portfolio.FeeWaived = before;
                    if (!r.Success)
                    {
                        Say(r.Error == TradeError.TooManyHoldings ? "보유 종목이 꽉 찼어요" : "살 현금이 없어요");
                        return;
                    }
                    result = $"{item.displayName}: {target.Name} {r.Shares}주 풀매수 @ ${r.Price:N2} (수수료 없음)";
                    break;
                }
                case ItemKind.SectorRumor:
                {
                    var sector = target.Definition.sector;
                    var pool = session.Stocks.Where(s => s.Definition.sector == sector).OrderBy(_ => rng.Next()).Take(2).ToList();
                    var lines = new List<string>();
                    foreach (var s in pool)
                        if (Foresee(item, s, true, out string line)) lines.Add(line);
                    if (lines.Count == 0) return;
                    result = string.Join("\n", lines);
                    hintCorrect = true;
                    break;
                }
                case ItemKind.OpenLeak:
                    result = $"{item.displayName}: {target.Name} 오늘 시가 ${target.OpenPrice:N2} (전일 대비 {Pct(target.OpenPrice / target.PrevClose - 1f)})";
                    break;
                case ItemKind.VolatilityRadar:
                {
                    var intraday = todayNews.Concat(foresights).Where(n => !n.RevealAtClose && !n.Revealed).OrderBy(n => n.RevealTick).ToList();
                    result = intraday.Count == 0
                        ? $"{item.displayName}: 오늘 장 중 특보는 없어요 (결과는 모두 장 마감에)"
                        : $"{item.displayName}: " + string.Join(", ", intraday.Select(n => $"{n.Stock.Name} {n.RevealTick * session.Settings.secondsPerTick:0}초"));
                    break;
                }
                case ItemKind.FeeWaiver:
                    if (session.Portfolio.FeeWaived)
                    {
                        Say("오늘은 이미 수수료가 면제예요");
                        return;
                    }
                    session.Portfolio.FeeWaived = true;
                    result = $"{item.displayName}: 오늘 매매 수수료 0%";
                    break;
                case ItemKind.LossInsurance:
                    if (insurance != null)
                    {
                        Say("오늘은 이미 보험에 들었어요");
                        return;
                    }
                    insurance = item;
                    result = $"{item.displayName}: 오늘 손실이 나면 장 마감에 {item.amount * 100f:0}% 돌려받아요";
                    break;
                case ItemKind.DevilContract:
                    session.Portfolio.Deposit(item.amount);
                    Loan.AddDebt(item.debtAmount);
                    Today.ItemCash += item.amount;
                    Today.ItemDebt += item.debtAmount;
                    result = $"{item.displayName}: 현금 +${item.amount:N0}, 빚 +${item.debtAmount:N0}";
                    break;
                case ItemKind.InterestDeferral:
                    interestDeferrals++;
                    result = $"{item.displayName}: 다음 이자일({Loan.NextInterestDay(Day)}일차) 이자 면제";
                    break;
                case ItemKind.DebtAdjustment:
                {
                    float cut = Loan.ReduceRate(item.amount);
                    if (cut <= 0f)
                    {
                        Say("이자율이 이미 가장 낮아요");
                        return;
                    }
                    result = $"{item.displayName}: 이자율 -{cut * 100f:0.#}%p → {Loan.InterestRate * 100f:0.#}%";
                    break;
                }
                case ItemKind.SunshineLoan:
                    if (!Loan.RestoreEmergency())
                    {
                        Say("아직 긴급 대출 기회가 남아 있어요");
                        return;
                    }
                    result = $"{item.displayName}: 긴급 대출 기회가 다시 생겼어요";
                    break;
                case ItemKind.Lottery:
                {
                    double roll = rng.NextDouble();
                    float prize = roll < 0.02 ? 20000f : roll < 0.12 ? 2000f : roll < 0.42 ? 300f : 0f;
                    if (prize > 0f)
                    {
                        session.Portfolio.Deposit(prize);
                        Today.ItemCash += prize;
                    }
                    result = prize > 0f ? $"{item.displayName}: 당첨! +${prize:N0}" : $"{item.displayName}: 꽝…";
                    break;
                }
                case ItemKind.SlowMotion:
                    slowRemaining = item.durationSeconds;
                    slowMultiplier = item.speedMultiplier;
                    result = item.durationSeconds >= session.RemainingSeconds
                        ? $"{item.displayName}: 장 마감까지 차트 속도 x{item.speedMultiplier:0.##} (남은 시간 {1f / Mathf.Max(0.1f, item.speedMultiplier):0.#}배)"
                        : $"{item.displayName}: {item.durationSeconds:0}초간 차트 속도 x{item.speedMultiplier:0.##}";
                    break;
                case ItemKind.TimeStop:
                    stopRemaining = item.durationSeconds;
                    result = $"{item.displayName}: {item.durationSeconds:0}초간 차트 멈춤 (매매 가능)";
                    break;
                case ItemKind.Telescope:
                {
                    int ticks = Mathf.RoundToInt(item.durationSeconds / session.Settings.secondsPerTick);
                    telescopeUntilTick = Mathf.Min(session.Tick + ticks, session.Settings.ticksPerDay);
                    result = $"{item.displayName}: 다음 {item.durationSeconds:0}초 구간 차트 미리보기";
                    break;
                }
                default:
                    return;
            }

            Inventory.Consume(item);
            hints.Add(result);
            Today.ItemUses.Add(new DailyReport.ItemUse
            {
                Item = item,
                Phase = Phase,
                Target = usedOn,
                Text = result,
                HintCorrect = hintCorrect,
                HintRevealedTomorrow = revealTomorrow,
            });
            Say(result);
            Changed?.Invoke();
        }

        /// <summary>
        /// 예지: 그 종목의 오늘 템플릿과 결과를 보여준다 (틀리지 않음). 뉴스가 있으면 숨겨진 결과를, 없으면 템플릿을 새로 꺼내
        /// 오늘 경로를 그 값으로 고정한다. withTiming이면 결과가 뜨는 시점도 알려준다.
        /// </summary>
        bool Foresee(ItemDefinition item, StockState stock, bool withTiming, out string text)
        {
            text = null;
            var n = todayNews.FirstOrDefault(x => x.Stock == stock) ?? foresights.FirstOrDefault(x => x.Stock == stock);
            if (n == null)
            {
                n = newsGenerator.CreateForesight(stock, session.Settings);
                if (n == null)
                {
                    Say($"{stock.Name}: 예지할 기사가 없어요");
                    return false;
                }
                foresights.Add(n);
            }
            n.Foreseen = true;

            text = $"{item.displayName}: {stock.Name} — \"{n.Template.initialHeadline}\" → {(n.Success ? "성공" : "실패")} {Pct(n.Effect)}";
            if (withTiming)
                text += n.RevealAtClose ? " · 장 마감에 발표" : $" · 장 시작 {n.RevealTick * session.Settings.secondsPerTick:0}초 뒤 특보";
            return true;
        }

        static string Pct(float change) => (change >= 0f ? "+" : "") + (change * 100f).ToString("0") + "%";

        /// <summary>결과 시점이 된 템플릿의 결과 기사를 띄운다 (장 중 = 특보, 장 마감 = 마감 발표).</summary>
        void RevealDueNews()
        {
            bool any = false;
            foreach (var n in todayNews.Concat(foresights))
            {
                if (n.Revealed || session.Tick < n.RevealTick) continue;
                n.Revealed = true;
                any = true;
                string line = $"{(n.RevealAtClose ? "[마감]" : "[속보]")} {n.ResultText} ({Pct(n.Effect)})";
                hints.Add(line);
                Say(line);
            }
            if (any) Changed?.Invoke();
        }

        void OnTicked()
        {
            RevealDueNews();
            if (autoSells.Count == 0 || session.Phase != MarketPhase.Open) return;

            for (int i = autoSells.Count - 1; i >= 0; i--)
            {
                var a = autoSells[i];
                if (a.Mode == AutoMode.TargetBuy)
                {
                    if (session.CurrentPrice(a.Stock) > a.BuyPrice) continue;
                    var b = session.Trade(new OrderRequest(a.Stock, OrderSide.Buy, 0, true));
                    autoSells.RemoveAt(i);
                    Say(b.Success
                        ? $"{a.Item.displayName} 발동: {a.Stock.Name} {b.Shares}주 매수 @ ${b.Price:N2}"
                        : $"{a.Item.displayName}: {a.Stock.Name} 목표가에 닿았지만 살 수 없었어요");
                    continue;
                }
                var holding = session.Portfolio.GetHolding(a.Stock);
                if (holding.Shares < 1)
                {
                    autoSells.RemoveAt(i);
                    continue;
                }

                float rate = holding.ReturnRate(session.CurrentPrice(a.Stock));
                if (a.Mode == AutoMode.Trailing) a.Peak = Mathf.Max(a.Peak, rate);
                bool hit = a.Mode switch
                {
                    AutoMode.StopLoss => rate <= -a.Item.triggerRate,
                    AutoMode.TakeProfit => rate >= a.Item.triggerRate,
                    AutoMode.Trailing => rate <= a.Peak - a.Item.triggerRate,
                    _ => false,
                };
                if (!hit) continue;

                var r = session.Trade(new OrderRequest(a.Stock, OrderSide.Sell, 0, true));
                if (!r.Success && r.Error == TradeError.NotEnoughCash) continue; // 마이너스를 메울 현금이 없으면 다음 틱에 다시
                autoSells.RemoveAt(i);
                if (r.Success) Today.AutoSellsFired++;
                if (r.Success)
                    Say($"{a.Item.displayName} 발동: {a.Stock.Name} {r.Shares}주 매도 @ ${r.Price:N2}");
            }
        }

        /// <summary>현재 걸려 있는 손절·익절 예약 설명 (화면 표시용).</summary>
        public IEnumerable<string> ActiveAutoSells()
        {
            foreach (var a in autoSells)
                yield return a.Mode switch
                {
                    AutoMode.StopLoss => $"{a.Stock.Name} 손절 -{a.Item.triggerRate * 100f:0}%",
                    AutoMode.TakeProfit => $"{a.Stock.Name} 익절 +{a.Item.triggerRate * 100f:0}%",
                    AutoMode.Trailing => $"{a.Stock.Name} 추적 손절 (최고 {a.Peak * 100f:+0;-0}%에서 -{a.Item.triggerRate * 100f:0}%p)",
                    _ => $"{a.Stock.Name} 목표가 매수 ${a.BuyPrice:N2}",
                };
        }

        /// <summary>손실 보험 증서: 오늘 매매 손실이 있으면 그 일부를 현금으로 돌려준다.</summary>
        void ApplyInsurance()
        {
            if (insurance == null) return;
            float loss = -Today.Stocks.Values.Sum(l => l.ProfitLoss); // 매매 손익만 (아이템 현금은 빼고)
            if (loss > 0f)
            {
                float refund = loss * Mathf.Clamp01(insurance.amount);
                session.Portfolio.Deposit(refund);
                Today.InsuranceRefund = refund;
                Today.ItemCash += refund;
                Say($"{insurance.displayName}: 손실 보상 +${refund:N2}");
            }
            insurance = null;
        }

        void ResetMarketEffects()
        {
            slowRemaining = 0f;
            stopRemaining = 0f;
            slowMultiplier = 1f;
            telescopeUntilTick = -1;
            Time.timeScale = 1f;
        }

        void Say(string text) => Message?.Invoke(text);
    }
}
