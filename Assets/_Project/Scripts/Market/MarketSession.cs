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

        void Awake()
        {
            rng = seed == 0 ? new System.Random() : new System.Random(seed);
            foreach (var def in stocks)
                stockStates.Add(new StockState(def, settings.GetDailyLimit(def.type)));
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

        public bool Reserve(OrderRequest order)
        {
            if (Phase != MarketPhase.PreMarket) return false;
            reservations.Add(order);
            ReservationsChanged?.Invoke();
            return true;
        }

        public TradeResult Trade(OrderRequest order)
        {
            if (Phase != MarketPhase.Open) return TradeResult.Fail(order.Stock, order.Side, TradeError.MarketClosed);
            return Portfolio.Execute(order, CurrentPrice(order.Stock));
        }

        public void StartMarket()
        {
            if (Phase != MarketPhase.PreMarket) return;

            Tick = 0;
            tickTimer = 0f;
            SetPhase(MarketPhase.Open);

            var results = new List<TradeResult>();
            foreach (var order in reservations)
                results.Add(Portfolio.Execute(order, order.Stock.OpenPrice));
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
