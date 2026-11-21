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
        [SerializeField, Tooltip("상점에 진열할 아이템. 변경점: 랜덤 진열 대신 전부 진열하고 플레이어가 골라 산다.")]
        List<ItemDefinition> shopItems = new();
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
        public LoanAccount Loan { get; private set; }
        public Inventory Inventory { get; private set; }
        public GamePhase Phase { get; private set; } = GamePhase.News;
        public int Day => session.Day;
        public IReadOnlyList<NewsItem> TodayNews => todayNews;
        public IReadOnlyList<string> Hints => hints;
        public float DayStartAssets { get; private set; }
        public float LastInterest { get; private set; }
        public bool Won { get; private set; }

        public float SlowRemaining => slowRemaining;
        public float StopRemaining => stopRemaining;
        /// <summary>망원경: 차트에 미리 보여줄 미래 틱 수.</summary>
        public int PreviewTicks => Phase == GamePhase.Market && telescopeUntilTick > session.Tick ? telescopeUntilTick - session.Tick : 0;

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
            BeginDay();
        }

        void OnDestroy()
        {
            Time.timeScale = 1f;
            if (session == null) return;
            session.PhaseChanged -= OnMarketPhaseChanged;
            session.Ticked -= OnTicked;
        }

        void Update()
        {
            if (Phase != GamePhase.Market) return;

            float dt = Time.unscaledDeltaTime;
            if (stopRemaining > 0f) stopRemaining = Mathf.Max(0f, stopRemaining - dt);
            if (slowRemaining > 0f) slowRemaining = Mathf.Max(0f, slowRemaining - dt);
            Time.timeScale = stopRemaining > 0f ? 0f : slowRemaining > 0f ? slowMultiplier : 1f;
        }

        // ───────── 자산 ─────────

        public float StockValue()
        {
            float sum = 0f;
            foreach (var s in session.Stocks)
                sum += session.Portfolio.GetHolding(s).Shares * session.CurrentPrice(s);
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
            SetPhase(GamePhase.News);
        }

        /// <summary>뉴스 확인 → 아이템 사용·예약 단계</summary>
        public void ConfirmNews()
        {
            if (Phase == GamePhase.News) SetPhase(GamePhase.Prep);
        }

        void OnMarketPhaseChanged()
        {
            switch (session.Phase)
            {
                case MarketPhase.Open:
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

            LastInterest = Loan.IsInterestDay(Day) ? Loan.ApplyInterest() : 0f;
            SetPhase(GamePhase.Settlement);
            CheckBankruptcy();
        }

        public void GoToShop()
        {
            if (Phase != GamePhase.Settlement) return;
            SetPhase(GamePhase.Shop);
        }

        public void StartNextDay()
        {
            if (Phase != GamePhase.Shop) return;
            if (CheckBankruptcy()) return;
            session.NextDay(); // → PreMarket 이벤트 → BeginDay
        }

        bool CheckBankruptcy()
        {
            if (TotalAssets() >= loanSettings.bankruptcyThreshold || Loan.CanTakeExtraLoan) return false;
            Won = false;
            SetPhase(GamePhase.GameOver);
            return true;
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
            Say($"${pay:N2} 갚았어요");

            if (Loan.IsPaidOff)
            {
                Won = true;
                SetPhase(GamePhase.GameOver);
                return;
            }
            Changed?.Invoke();
        }

        public void TakeExtraLoan()
        {
            if (Phase != GamePhase.Settlement) return;
            float amount = Loan.TakeExtraLoan();
            if (amount <= 0f)
            {
                Say("추가 대출을 모두 썼어요");
                return;
            }
            session.Portfolio.Deposit(amount);
            Say($"추가 대출 ${amount:N0} (이자율 {Loan.InterestRate * 100f:0}%)");
            Changed?.Invoke();
        }

        // ───────── 상점 · 아이템 ─────────

        public void Buy(ItemDefinition item)
        {
            if (Phase != GamePhase.Shop) return;
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
            switch (item.kind)
            {
                case ItemKind.Rumor:
                    result = DirectionHint(item, session.Stocks[rng.Next(session.Stocks.Count)], false);
                    break;
                case ItemKind.AnalystReport:
                    result = DirectionHint(item, target, false);
                    break;
                case ItemKind.InsiderMemo:
                    result = DirectionHint(item, target, true);
                    break;
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
            Say(result);
            Changed?.Invoke();
        }

        /// <summary>오늘 방향(전일 종가 → 오늘 종가) 힌트. accuracy 확률로 맞고, 틀리면 방향이 반대로 나온다.</summary>
        string DirectionHint(ItemDefinition item, StockState stock, bool withMagnitude)
        {
            float change = stock.Path[stock.Path.Length - 1] / stock.PrevClose - 1f;
            bool up = change >= 0f;
            bool correct = rng.NextDouble() < item.accuracy;
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
