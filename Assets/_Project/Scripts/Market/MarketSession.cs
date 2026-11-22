using System;
using System.Collections.Generic;
using UnityEngine;

namespace BurningAnt.Market
{
    public enum MarketPhase
    {
        PreMarket, // 아이템 단계: 시가를 모른 채 예약 주문만 가능
        Open,      // 장 시간: 실시간 매매, 멈출 수 없음
        Closed,    // 장 마감
    }

    /// <summary>
    /// 하루 장을 진행한다: 예약 주문 → 장 시작(시가에 예약 체결) → 틱 진행 → 마감 → 다음 날.
    /// UI는 이 클래스의 이벤트만 구독하고, 매매는 Reserve/Trade로만 요청한다.
    /// </summary>
    public class MarketSession : MonoBehaviour
    {
        [SerializeField] MarketSettings settings;
        [SerializeField] List<StockDefinition> stocks = new();
        [SerializeField, Tooltip("0이면 매번 무작위")] int seed;

        readonly List<StockState> stockStates = new();
        readonly List<OrderRequest> reservations = new();
        System.Random rng;
        float tickTimer;

        public MarketSettings Settings => settings;
        public IReadOnlyList<StockState> Stocks => stockStates;
        public IReadOnlyList<OrderRequest> Reservations => reservations;
        public Portfolio Portfolio { get; private set; }
        public MarketPhase Phase { get; private set; } = MarketPhase.PreMarket;
        public int Tick { get; private set; }
        public int Day { get; private set; } = 1;
        public float RemainingSeconds => (settings.ticksPerDay - Tick) * settings.secondsPerTick;

        public event Action PhaseChanged;
        public event Action Ticked;
        public event Action ReservationsChanged;
        public event Action<IReadOnlyList<TradeResult>> ReservationsExecuted;
        /// <summary>체결된 매매 하나와 그로 인한 현금 변화(매수는 음수, 매도는 양수, 수수료 포함). 하루 기록용.</summary>
        public event Action<TradeResult, float> Traded;

        void Awake()
        {
            rng = seed == 0 ? new System.Random() : new System.Random(seed);
            foreach (var def in stocks)
                stockStates.Add(new StockState(def, def.baseRange));
            Portfolio = new Portfolio(settings.startingCash, settings.feeRate, settings.maxHoldings, settings.leverage);
            PrepareDay();
        }

        void Update()
        {
            if (Phase != MarketPhase.Open) return;

            tickTimer += Time.deltaTime;
            while (tickTimer >= settings.secondsPerTick && Phase == MarketPhase.Open)
            {
                tickTimer -= settings.secondsPerTick;
                Tick++;
                if (Tick >= settings.ticksPerDay)
                {
                    Tick = settings.ticksPerDay;
                    SetPhase(MarketPhase.Closed);
                }
                Ticked?.Invoke();
            }
        }

        /// <summary>장 전에는 전일 종가, 장 중·마감 후에는 현재 틱 가격.</summary>
        public float CurrentPrice(StockState stock) =>
            Phase == MarketPhase.PreMarket ? stock.PrevClose : stock.PriceAt(Tick);

        /// <summary>
        /// 장 전 예약. 같은 종목·같은 방향 예약이 이미 있으면 줄을 늘리지 않고 수량을 더한다 (둘 중 하나라도 '최대'면 최대).
        /// </summary>
        public bool Reserve(OrderRequest order)
        {
            if (Phase != MarketPhase.PreMarket) return false;
            int i = reservations.FindIndex(o => o.Stock == order.Stock && o.Side == order.Side);
            if (i >= 0)
            {
                var old = reservations[i];
                bool max = old.UseMax || order.UseMax;
                long sum = (long)old.Quantity + order.Quantity;
                reservations[i] = new OrderRequest(order.Stock, order.Side, (int)System.Math.Min(int.MaxValue, sum), max);
            }
            else reservations.Add(order);
            ReservationsChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 화면 표시용 예상 체결: 예약 순서대로 전날 종가로 체결된다고 보고, 각 예약이 몇 주 체결될지 (0 = 현금·보유가 없어 체결 안 됨).
        /// 실제 체결(StartMarket)은 바꾸지 않는다. 시가가 전날 종가와 다르면 실제 수량은 조금 다를 수 있다.
        /// </summary>
        public List<(OrderRequest order, int shares)> EstimateReservationFills()
        {
            var list = new List<(OrderRequest, int)>();
            float cash = Portfolio.Cash;
            float fee = Portfolio.FeeRateNow;
            var held = new Dictionary<StockState, int>();
            foreach (var o in reservations)
            {
                float price = Mathf.Max(0.0001f, o.Stock.PrevClose);
                if (!held.TryGetValue(o.Stock, out int have)) have = Portfolio.GetHolding(o.Stock).Shares;
                int n;
                if (o.Side == OrderSide.Buy)
                {
                    int can = cash > 0f ? Mathf.FloorToInt(cash / (price * (1f + fee))) : 0;
                    n = o.UseMax ? can : Mathf.Min(o.Quantity, can);
                    cash -= n * price * (1f + fee);
                    held[o.Stock] = have + n;
                }
                else
                {
                    n = o.UseMax ? have : Mathf.Min(o.Quantity, have);
                    var h = Portfolio.GetHolding(o.Stock);
                    if (n > 0 && h.Shares > 0) cash += h.Value(price) * n / h.Shares;
                    held[o.Stock] = have - n;
                }
                list.Add((o, n));
            }
            return list;
        }

        /// <summary>그 종목의 장 전 예약(매수·매도 모두)을 취소한다. 취소한 게 있으면 true.</summary>
        public bool CancelReservations(StockState stock)
        {
            if (Phase != MarketPhase.PreMarket) return false;
            int removed = reservations.RemoveAll(o => o.Stock == stock);
            if (removed > 0) ReservationsChanged?.Invoke();
            return removed > 0;
        }

        public TradeResult Trade(OrderRequest order)
        {
            if (Phase != MarketPhase.Open) return TradeResult.Fail(order.Stock, order.Side, TradeError.MarketClosed);
            return ExecuteAndReport(order, CurrentPrice(order.Stock));
        }

        TradeResult ExecuteAndReport(OrderRequest order, float price)
        {
            float cashBefore = Portfolio.Cash;
            var result = Portfolio.Execute(order, price);
            if (result.Success) Traded?.Invoke(result, Portfolio.Cash - cashBefore);
            return result;
        }

        public void StartMarket()
        {
            if (Phase != MarketPhase.PreMarket) return;

            Tick = 0;
            tickTimer = 0f;
            SetPhase(MarketPhase.Open);

            var results = new List<TradeResult>();
            foreach (var order in reservations)
            {
                // 예약 수량이 시가 기준으로 살 수 있는 수(매수) · 가진 수(매도)를 넘으면 되는 만큼만 체결한다
                // (예전엔 통째로 실패). 0주면 실패로 알린다.
                var o = order;
                if (!o.UseMax)
                {
                    int cap = o.Side == OrderSide.Buy ? Portfolio.MaxBuyable(o.Stock.OpenPrice) : Portfolio.GetHolding(o.Stock).Shares;
                    if (o.Quantity > cap && cap >= 1) o = new OrderRequest(o.Stock, o.Side, cap, false);
                }
                results.Add(ExecuteAndReport(o, o.Stock.OpenPrice));
            }
            reservations.Clear();
            ReservationsChanged?.Invoke();
            if (results.Count > 0) ReservationsExecuted?.Invoke(results);
        }

        public void NextDay()
        {
            if (Phase != MarketPhase.Closed) return;

            foreach (var s in stockStates) s.CloseDay();
            Day++;
            PrepareDay();
            SetPhase(MarketPhase.PreMarket);
        }

        void PrepareDay()
        {
            foreach (var s in stockStates)
                s.PrepareDay(PricePathGenerator.Generate(s.PrevClose, s.DailyLimit, settings, rng));
        }

        void SetPhase(MarketPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke();
        }
    }
}
