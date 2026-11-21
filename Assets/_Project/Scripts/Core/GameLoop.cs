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

        bool deferNext, deferralUsed; // 이자 유예 신청서: 다음 이자 하루 연기 (게임당 1회)
        float deferredCarry;           // 연기된 이자 (다음 날 이자에 더해짐)
        bool vipCoupon;                // 은행 VIP 쿠폰: 다음 이자 30% 감면
        bool debtAdjustUsed;           // 채무 조정 신청서: 게임당 1회
        ItemDefinition insurance;      // 손실 보험 증서: 다음 매도 1회 (날이 바뀌어도 유지)
        ItemDefinition lottery;        // 인생역전 복권: 오늘 장 결과에서 추첨
        ItemDefinition alarm;          // 파산 경보기: 오늘 장 중
        bool alarmOn;                  // 지금 경보 중 (위험 금액 위로 올라오면 다시 꺼짐)

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
        /// <summary>망원경으로 보는 종목과 미리보기 시작 틱 (PreviewTicks > 0일 때만 의미 있음).</summary>
        public StockState TelescopeStock { get; private set; }
        public int TelescopeFromTick { get; private set; }
        public int TelescopeUntilTick => telescopeUntilTick;

        /// <summary>오늘 하루 기록 (2단계 흐름 표시, 4단계 결과창). 지난 날들은 History.</summary>
        public DailyReport Today { get; private set; }
        readonly List<DailyReport> history = new();
        public IReadOnlyList<DailyReport> History => history;

        /// <summary>튜토리얼 등 바깥에서 장을 잠시 멈출 때 true (장 시간과 아이템 효과 시간이 모두 멈춘다).</summary>
        public bool ExternalPause { get; set; }

        public event Action Changed;
        public event Action PhaseChanged;
        public event Action<string> Message;
        /// <summary>파산 경보기가 울린 순간 (화면 깜빡임·경고음).</summary>
        public event Action AlarmFired;
        /// <summary>상점에서 아이템을 산 순간 (골드 효과음).</summary>
        public event Action<ItemDefinition> ItemBought;

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

            session.Portfolio.BonusHoldings = 0; // 듀얼 모니터는 그날만
            alarm = null;                        // 파산 경보기도 그날만
            alarmOn = false;
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
                // 손실 보험 증서: 걸어 둔 뒤 첫 매도가 손해면 손실의 amount 비율을 돌려줌 (최대 debtAmount)
                if (insurance != null)
                {
                    float loss = -session.Portfolio.LastRealized;
                    if (loss > 0f)
                    {
                        float refund = Mathf.Min(loss * insurance.amount, insurance.debtAmount);
                        session.Portfolio.Deposit(refund);
                        Today.InsuranceRefund += refund;
                        Today.ItemCash += refund;
                        Say($"{insurance.displayName}: 손실 보상 +{Money(refund)}");
                    }
                    insurance = null;
                }
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
            DrawLottery();
            // 오늘 이자: 3일차부터 매일 (대출마다 이자율 따로). 장 결과에서 '이자 갚기'로 내고, 안 내면 상점으로 갈 때 빚에 붙는다
            LastInterest = Loan.InterestDue(Day) + deferredCarry;
            deferredCarry = 0f;
            if (LastInterest > 0f && deferNext)
            {
                deferNext = false;
                deferredCarry = LastInterest; // 하루 연기: 내일 이자에 더해짐
                Today.InterestDeferred = true;
                LastInterest = 0f;
            }
            if (LastInterest > 0f && vipCoupon)
            {
                vipCoupon = false;
                float cut = LastInterest * 0.3f;
                LastInterest -= cut;
                Say($"은행 VIP 쿠폰: 오늘 이자 {Money(cut)} 감면");
            }
            Today.SettlementCharge = LastSettlementCharge;
            Today.ClosedStocks.AddRange(lastClosed);
            Today.Interest = LastInterest;

            if (session.Portfolio.Cash < 0f)
            {
                // 햇살론은 한 판에 1번, 모자란 돈이 한도(1억)를 넘으면 못 받는다 → 파산
                if (Loan.EmergencyAmount(-session.Portfolio.Cash) <= 0f)
                {
                    GoBankrupt();
                    return;
                }
                NeedsEmergency = true; // 4단계 직전: 햇살론을 받을지 고른다
            }
            SetPhase(GamePhase.Settlement);
        }

        /// <summary>오늘 아직 안 낸 이자.</summary>
        public float InterestLeft => Today == null ? 0f : Mathf.Max(0f, Today.Interest - Today.InterestPaid);

        /// <summary>장 결과에서 오늘 이자를 낸다 (현금이 모자라면 있는 만큼).</summary>
        public void PayInterest()
        {
            if (Phase != GamePhase.Settlement || NeedsEmergency) return;
            float pay = Mathf.Min(InterestLeft, session.Portfolio.Cash);
            if (InterestLeft <= 0f)
            {
                Say(Today.Interest > 0f ? "오늘 이자는 다 냈어요" : "오늘은 낼 이자가 없어요");
                return;
            }
            if (pay <= 0f)
            {
                Say("이자를 낼 현금이 없어요");
                return;
            }
            session.Portfolio.Withdraw(pay);
            Loan.RecordInterestPaid(pay);
            Today.InterestPaid += pay;
            Say($"이자 {Money(pay)} 냈어요");
            if (CheckWin()) return;
            Changed?.Invoke();
        }

        /// <summary>승리: 빚(원금 + 이자)을 다 갚고 자산이 목표(5억) 이상.</summary>
        bool CheckWin()
        {
            if (!Loan.IsPaidOff || InterestLeft > 0f || TotalAssets() < loanSettings.winAssets) return false;
            Won = true;
            SetPhase(GamePhase.GameOver);
            return true;
        }

        static string Money(float v) => BurningAnt.UI.Format.Money(v);

        /// <summary>햇살론 수락: 모자란 금액 + 여유분을 빌려 잔고를 0 이상으로 만든다 (한 판에 1번).</summary>
        public void AcceptEmergencyLoan()
        {
            if (!NeedsEmergency) return;
            float amount = Loan.TakeEmergencyLoan(-session.Portfolio.Cash, Day);
            NeedsEmergency = false;
            if (amount <= 0f)
            {
                GoBankrupt();
                return;
            }
            session.Portfolio.Deposit(amount);
            Today.EmergencyLoan += amount;
            Say($"햇살론 {Money(amount)} (하루 이자 {Loan.Loans[Loan.Loans.Count - 1].Rate * 100f:0}%, 내일부터)");
            Changed?.Invoke();
        }

        public void DeclineEmergencyLoan()
        {
            if (!NeedsEmergency) return;
            NeedsEmergency = false;
            GoBankrupt();
        }

        /// <summary>시연·테스트용 (-autoplay-win / -autoplay-bankrupt): 바로 엔딩으로.</summary>
        public void EndForDemo(bool won)
        {
            Won = won;
            SetPhase(GamePhase.GameOver);
        }

        void GoBankrupt()
        {
            Won = false;
            SetPhase(GamePhase.GameOver);
        }

        public void GoToShop()
        {
            if (Phase != GamePhase.Settlement || NeedsEmergency) return;
            float unpaid = InterestLeft;
            if (unpaid > 0f)
            {
                Loan.AddUnpaidInterest(unpaid);
                Today.InterestUnpaid = unpaid;
                Today.InterestPaid = Today.Interest; // 빚에 붙었으니 오늘 이자는 끝
            }
            if (CheckWin()) return;
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
            Say(Loan.IsPaidOff && TotalAssets() < loanSettings.winAssets
                ? $"{Money(pay)} 갚아서 빚 0원! 자산 {Money(loanSettings.winAssets)}을 모으면 성공"
                : $"{Money(pay)} 갚았어요");
            if (CheckWin()) return;
            Changed?.Invoke();
        }

        /// <summary>추가 대출은 2단계(아이템·예약)·3단계(장 중)에서, 한 판에 1번, 최대 2억. 이자는 다다음 날부터.</summary>
        public bool CanTakeExtraLoanNow => Loan.CanTakeExtraLoan && (Phase == GamePhase.Prep || (Phase == GamePhase.Market && session.Phase == MarketPhase.Open));

        public void TakeExtraLoan(float amount)
        {
            if (Phase != GamePhase.Prep && !(Phase == GamePhase.Market && session.Phase == MarketPhase.Open))
            {
                Say("추가 대출은 아이템 시간(2단계)이나 장 시간(3단계)에만 받을 수 있어요");
                return;
            }
            float got = Loan.TakeExtraLoan(amount, Day);
            if (got <= 0f)
            {
                Say(Loan.ExtraLoanUsed ? "추가 대출은 한 번만 받을 수 있어요" : "빌릴 금액을 입력하세요");
                return;
            }
            session.Portfolio.Deposit(got);
            Today.ExtraLoan += got;
            Say($"추가 대출 {Money(got)} (하루 이자 {Loan.Loans[Loan.Loans.Count - 1].Rate * 100f:0}%, {Day + loanSettings.extraLoanGraceDays}일차부터)");
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
            if (item.price > 0f && !session.Portfolio.Withdraw(item.price)) // 사채업자 명함은 무료
            {
                Say("현금이 부족해요");
                return;
            }
            Inventory.Add(item);
            ItemBought?.Invoke(item);
            Say($"{item.displayName} 구매");
            Changed?.Invoke();
        }

        public bool CanUseNow(ItemDefinition item) =>
            Inventory.Count(item) > 0 &&
            item.Timing switch
            {
                ItemTiming.Prep => Phase == GamePhase.Prep,
                ItemTiming.Market => Phase == GamePhase.Market && session.Phase == MarketPhase.Open,
                _ => Phase == GamePhase.Prep || (Phase == GamePhase.Market && session.Phase == MarketPhase.Open),
            };

        /// <summary>아이템 사용. 대상이 필요 없는 아이템은 target = null.</summary>
        public void UseItem(ItemDefinition item, StockState target)
        {
            if (!CanUseNow(item))
            {
                Say(item.Timing switch
                {
                    ItemTiming.Prep => "장 전(아이템 시간)에만 쓸 수 있어요",
                    ItemTiming.Market => "장 시간에만 쓸 수 있어요",
                    _ => "아이템 시간이나 장 시간에만 쓸 수 있어요",
                });
                return;
            }
            if (item.NeedsTarget && target == null) return;

            string result;
            bool? hintCorrect = null;
            bool revealTomorrow = false;
            var usedOn = target;
            switch (item.kind)
            {
                // ── 정보 (2단계): 오늘 종가 방향을 item.accuracy 확률로 맞게 알려준다. 틀릴 수 있다. ──
                case ItemKind.Rumor:
                    usedOn = session.Stocks[rng.Next(session.Stocks.Count)];
                    result = Predict(item, usedOn, false, out bool rumorOk);
                    hintCorrect = rumorOk;
                    break;
                case ItemKind.AnalystReport:
                    result = Predict(item, target, false, out bool analystOk);
                    hintCorrect = analystOk;
                    break;
                case ItemKind.InsiderMemo:
                    result = Predict(item, target, true, out bool insiderOk);
                    hintCorrect = insiderOk;
                    break;
                case ItemKind.ChainForecast: // 수상한 전화: 아직 결과가 안 나온 오늘 뉴스 하나의 성공·실패
                {
                    var pending = todayNews.Where(n => !n.Revealed).ToList();
                    if (pending.Count == 0)
                    {
                        Say("결과를 기다리는 뉴스가 없어요");
                        return;
                    }
                    var news = pending[rng.Next(pending.Count)];
                    bool ok = rng.NextDouble() < item.accuracy;
                    bool saySuccess = ok ? news.Success : !news.Success;
                    usedOn = news.Stock;
                    result = $"{item.displayName}: {news.Stock.Name} \"{news.Template.initialHeadline}\" → {(saySuccess ? "성공" : "실패")}할 것 같대요 (적중률 {item.accuracy * 100f:0}%)";
                    hintCorrect = ok;
                    break;
                }
                case ItemKind.OpenLeak:
                    result = $"{item.displayName}: {target.Name} 오늘 시가 {Money(target.OpenPrice)} (전일 대비 {Pct(target.OpenPrice / target.PrevClose - 1f)})";
                    break;
                case ItemKind.VolatilityRadar: // 오늘 하루 중 전일 종가에서 가장 멀리 가는 2종목 (방향은 비밀)
                {
                    var top = session.Stocks.OrderByDescending(s => s.Path.Max(p => Mathf.Abs(p / s.PrevClose - 1f))).Take(2).ToList();
                    result = $"{item.displayName}: 오늘 크게 움직일 종목 → {string.Join(", ", top.Select(s => s.Name))} (방향은 몰라요)";
                    break;
                }

                // ── 매매 보조 ──
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
                    result = $"{item.displayName}: {target.Name} 손익 {(stop ? "-" : "+")}{item.triggerRate * 100f:0}%에 닿으면 전량 자동 매도";
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
                    result = $"{item.displayName}: {target.Name} 최고 손익률에서 {item.triggerRate * 100f:0}%p 밀리면 전량 자동 매도";
                    break;
                }
                case ItemKind.TargetBuy:
                {
                    float buyAt = session.CurrentPrice(target) * (1f - item.triggerRate);
                    autoSells.RemoveAll(a => a.Stock == target && a.Mode == AutoMode.TargetBuy);
                    autoSells.Add(new AutoSell { Stock = target, Item = item, Mode = AutoMode.TargetBuy, BuyPrice = buyAt });
                    result = $"{item.displayName}: {target.Name}이 {Money(buyAt)}(-{item.triggerRate * 100f:0}%) 이하가 되면 현금 전부로 자동 매수";
                    break;
                }
                case ItemKind.BankruptAlarm:
                    if (alarm != null)
                    {
                        Say("오늘은 이미 경보기를 켰어요");
                        return;
                    }
                    alarm = item;
                    alarmOn = false;
                    result = $"{item.displayName}: 오늘 현금+주식이 {Money(item.amount)} 밑으로 내려가면 경보가 울려요";
                    break;
                case ItemKind.EmergencyExit:
                {
                    var held = session.Portfolio.HeldStocks.ToList();
                    if (held.Count == 0)
                    {
                        Say("보유한 종목이 없어요");
                        return;
                    }
                    int sold = held.Count(s => session.Trade(new OrderRequest(s, OrderSide.Sell, 0, true)).Success);
                    result = $"{item.displayName}: 보유 {held.Count}종목 중 {sold}종목 전량 매도" + (sold < held.Count ? " (마이너스를 메울 현금이 없어 못 판 종목 있음)" : "");
                    break;
                }
                case ItemKind.DualMonitor:
                    if (session.Portfolio.BonusHoldings > 0)
                    {
                        Say("오늘은 이미 듀얼 모니터를 켰어요");
                        return;
                    }
                    session.Portfolio.BonusHoldings = 1;
                    result = $"{item.displayName}: 오늘 보유 가능 종목 {session.Portfolio.MaxHoldings - 1}개 → {session.Portfolio.MaxHoldings}개";
                    break;
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
                case ItemKind.Telescope: // 고른 종목의 앞으로 몇 초를 따로 창에 보여준다 (실제 차트는 그대로)
                {
                    int ticks = Mathf.RoundToInt(item.durationSeconds / session.Settings.secondsPerTick);
                    TelescopeStock = target;
                    TelescopeFromTick = session.Tick;
                    telescopeUntilTick = Mathf.Min(session.Tick + ticks, session.Settings.ticksPerDay);
                    result = $"{item.displayName}: {target.Name} 앞으로 {item.durationSeconds:0}초 가격을 미리 봐요";
                    break;
                }

                // ── 대출·빚 관리 (2단계) ──
                case ItemKind.InterestCoupon:
                    if (vipCoupon)
                    {
                        Say("이미 쿠폰을 걸어 뒀어요");
                        return;
                    }
                    vipCoupon = true;
                    result = $"{item.displayName}: 다음 이자 30% 감면";
                    break;
                case ItemKind.InterestDeferral:
                    if (deferralUsed)
                    {
                        Say("이자 유예는 게임당 한 번만 쓸 수 있어요");
                        return;
                    }
                    deferralUsed = deferNext = true;
                    result = $"{item.displayName}: 다음 이자를 하루 미뤄요 (그다음 날 이자에 더해짐)";
                    break;
                case ItemKind.CreditLaunder:
                    if (Loan.HalveNextRate)
                    {
                        Say("이미 세탁기를 돌려 뒀어요");
                        return;
                    }
                    Loan.HalveNextRate = true;
                    result = $"{item.displayName}: 다음 대출 1회 이자율 절반 (기본 대출 5%에는 안 됨)";
                    break;
                case ItemKind.DebtAdjustment:
                {
                    if (debtAdjustUsed)
                    {
                        Say("채무 조정은 게임당 한 번만 쓸 수 있어요");
                        return;
                    }
                    debtAdjustUsed = true;
                    float cut = Loan.ReduceBasePrincipal(item.amount);
                    result = $"{item.displayName}: 기본 대출 원금 {Money(cut)} 감면";
                    break;
                }

                // ── 고위험 · 대출 아이템 ──
                case ItemKind.SoulAllIn:
                    if (session.Portfolio.NextBuyLeverage > 0f)
                    {
                        Say("이미 다음 매수에 걸려 있어요");
                        return;
                    }
                    session.Portfolio.NextBuyLeverage = item.speedMultiplier;
                    result = $"{item.displayName}: 다음 매수 1회 레버리지 {item.speedMultiplier:0}배";
                    break;
                case ItemKind.DevilTrade:
                    if (!Mathf.Approximately(session.Portfolio.NextSellMultiplier, 1f))
                    {
                        Say("이미 다음 매도에 걸려 있어요");
                        return;
                    }
                    session.Portfolio.NextSellMultiplier = item.speedMultiplier;
                    result = $"{item.displayName}: 다음 매도 1회 손익 {item.speedMultiplier:0.#}배 (이익도 손해도)";
                    break;
                case ItemKind.LossInsurance:
                    if (insurance != null)
                    {
                        Say("이미 보험에 들어 있어요");
                        return;
                    }
                    insurance = item;
                    result = $"{item.displayName}: 다음 매도 1회가 손해면 손실의 {item.amount * 100f:0}% 보상 (최대 {Money(item.debtAmount)})";
                    break;
                case ItemKind.CashLoan:
                {
                    session.Portfolio.Deposit(item.amount);
                    Today.ItemCash += item.amount;
                    Today.ItemDebt += item.debtAmount;
                    if (item.loanRate > 0f)
                    {
                        float rate = Loan.AddLoan(item.displayName, item.debtAmount, item.loanRate, Day + 1);
                        result = $"{item.displayName}: 현금 +{Money(item.amount)}, 빚 +{Money(item.debtAmount)} (하루 이자 {rate * 100f:0.#}%, 내일부터)";
                    }
                    else
                    {
                        Loan.AddDebt(item.debtAmount);
                        result = $"{item.displayName}: 현금 +{Money(item.amount)}, 기본 대출 원금 +{Money(item.debtAmount)}";
                    }
                    break;
                }
                case ItemKind.Lottery:
                    if (lottery != null)
                    {
                        Say("오늘은 이미 복권을 샀어요");
                        return;
                    }
                    lottery = item;
                    result = $"{item.displayName}: 오늘 장 결과 때 추첨 ({item.debtAmount * 100f:0}% 확률로 {Money(item.amount)})";
                    break;
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

        /// <summary>정보 아이템: 그 종목 오늘 종가 방향을 accuracy 확률로 맞게 알려준다. withSize면 예상 등락폭도.</summary>
        string Predict(ItemDefinition item, StockState s, bool withSize, out bool correct)
        {
            float change = s.Path[s.Path.Length - 1] / s.PrevClose - 1f;
            correct = rng.NextDouble() < item.accuracy;
            bool up = (change >= 0f) == correct;
            string text = $"{item.displayName}: {s.Name} 오늘 {(up ? "오를" : "내릴")} 것 같아요";
            if (withSize)
            {
                // 맞으면 실제 폭 근처, 틀리면 엉뚱한 폭 (둘 다 ±20% 흔들기)
                float size = correct ? Mathf.Abs(change) : s.DailyLimit * (0.3f + 0.7f * (float)rng.NextDouble());
                size *= 0.8f + 0.4f * (float)rng.NextDouble();
                text += $" (약 {(up ? "+" : "-")}{size * 100f:0}%)";
            }
            return text + $" · 적중률 {item.accuracy * 100f:0}%";
        }

        /// <summary>인생역전 복권: 장 결과 때 debtAmount 확률로 amount.</summary>
        void DrawLottery()
        {
            if (lottery == null) return;
            bool win = rng.NextDouble() < lottery.debtAmount;
            if (win)
            {
                session.Portfolio.Deposit(lottery.amount);
                Today.ItemCash += lottery.amount;
            }
            string line = win ? $"{lottery.displayName}: 당첨! +{Money(lottery.amount)}" : $"{lottery.displayName}: 꽝…";
            hints.Add(line);
            Say(line);
            lottery = null;
        }

        /// <summary>파산 경보기: 현금+주식이 위험 금액 밑으로 내려가면 한 번 울리고, 다시 올라오면 다음에 또 울린다.</summary>
        void CheckAlarm()
        {
            if (alarm == null) return;
            float total = TotalAssets();
            bool danger = total < alarm.amount;
            if (danger && !alarmOn)
            {
                alarmOn = true;
                Say($"[파산 경보] 현금+주식 {Money(total)} — {Money(alarm.amount)} 밑으로 떨어졌어요!");
                AlarmFired?.Invoke();
            }
            else if (!danger) alarmOn = false;
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
            CheckAlarm();
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
                        ? $"{a.Item.displayName} 발동: {a.Stock.Name} {b.Shares}주 매수 @ {Money(b.Price)}"
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
                    Say($"{a.Item.displayName} 발동: {a.Stock.Name} {r.Shares}주 매도 @ {Money(r.Price)}");
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
                    _ => $"{a.Stock.Name} 목표가 매수 {Money(a.BuyPrice)}",
                };
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
