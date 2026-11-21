using System;
using System.Collections.Generic;
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

        class AutoSell
        {
            public StockState Stock;
            public ItemDefinition Item;
            public bool IsStopLoss;
        }

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
            todayNews.AddRange(newsGenerator.GenerateDay(Day, session.Stocks));
            newsGenerator.ApplyToPrices(session.Stocks, todayNews, session.Settings);

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
            }
            else
            {
                line.Sells++;
                line.SoldShares += r.Shares;
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

            CloseReport();

            // 장 마감 정산: 마이너스 종목은 정리되고 그 마이너스만큼 잔고에서 빠진다
            lastClosed.Clear();
            LastSettlementCharge = session.Portfolio.SettleNegatives(session.CurrentPrice, lastClosed);
            LastInterest = Loan.IsInterestDay(Day) ? Loan.ApplyInterest() : 0f;
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
                case ItemKind.Rumor:
                {
                    usedOn = session.Stocks[rng.Next(session.Stocks.Count)];
                    result = DirectionHint(item, usedOn, false, out bool ok);
                    hintCorrect = ok;
                    break;
                }
                case ItemKind.AnalystReport:
                {
                    result = DirectionHint(item, target, false, out bool ok);
                    hintCorrect = ok;
                    break;
                }
                case ItemKind.InsiderMemo:
                {
                    result = DirectionHint(item, target, true, out bool ok);
                    hintCorrect = ok;
                    break;
                }
                case ItemKind.ChainForecast:
                {
                    var chain = newsGenerator.ActiveChain;
                    if (chain == null || chain.ResolveDay != Day + 1)
                    {
                        Say("진행 중인 연쇄 뉴스가 없어요");
                        return;
                    }
                    bool correct = rng.NextDouble() < item.accuracy;
                    bool shownSuccess = correct ? chain.WillSucceed : !chain.WillSucceed;
                    usedOn = chain.Stock;
                    hintCorrect = correct;
                    revealTomorrow = true;
                    result = $"{item.displayName}: {chain.Stock.Name} 연쇄 뉴스는 내일 {(shownSuccess ? "성공" : "실패")}할 것 같다 (정확도 {item.accuracy * 100f:0}%)";
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
                    autoSells.RemoveAll(a => a.Stock == target && a.IsStopLoss == stop);
                    autoSells.Add(new AutoSell { Stock = target, Item = item, IsStopLoss = stop });
                    result = $"{item.displayName}: {target.Name} 평단 대비 {(stop ? "-" : "+")}{item.triggerRate * 100f:0}%에 닿으면 전량 자동 매도";
                    break;
                }
                case ItemKind.SlowMotion:
                    slowRemaining = item.durationSeconds;
                    slowMultiplier = item.speedMultiplier;
                    result = $"{item.displayName}: {item.durationSeconds:0}초간 차트 속도 x{item.speedMultiplier:0.##}";
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

        /// <summary>오늘 방향(전일 종가 → 오늘 종가) 힌트. accuracy 확률로 맞고, 틀리면 방향이 반대로 나온다.</summary>
        string DirectionHint(ItemDefinition item, StockState stock, bool withMagnitude, out bool correct)
        {
            float change = stock.Path[stock.Path.Length - 1] / stock.PrevClose - 1f;
            bool up = change >= 0f;
            correct = rng.NextDouble() < item.accuracy;
            bool shownUp = correct ? up : !up;

            string text = $"{item.displayName}: {stock.Name} 오늘 {(shownUp ? "오를" : "내릴")} 것 같다";
            if (withMagnitude)
            {
                float abs = Mathf.Abs(change);
                text += abs < 0.03f ? " · 소폭(3% 미만)" : abs < 0.08f ? " · 중폭(3~8%)" : " · 대폭(8% 이상)";
            }
            return text + $" (정확도 {item.accuracy * 100f:0}%)";
        }

        void OnTicked()
        {
            if (autoSells.Count == 0 || session.Phase != MarketPhase.Open) return;

            for (int i = autoSells.Count - 1; i >= 0; i--)
            {
                var a = autoSells[i];
                var holding = session.Portfolio.GetHolding(a.Stock);
                if (holding.Shares < 1)
                {
                    autoSells.RemoveAt(i);
                    continue;
                }

                float rate = holding.ReturnRate(session.CurrentPrice(a.Stock));
                bool hit = a.IsStopLoss ? rate <= -a.Item.triggerRate : rate >= a.Item.triggerRate;
                if (!hit) continue;

                var r = session.Trade(new OrderRequest(a.Stock, OrderSide.Sell, 0, true));
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
                yield return $"{a.Stock.Name} {(a.IsStopLoss ? "손절" : "익절")} {(a.IsStopLoss ? "-" : "+")}{a.Item.triggerRate * 100f:0}%";
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
