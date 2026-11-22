using System;
using System.Collections.Generic;
using UnityEngine;

namespace BurningAnt.Market
{
    /// <summary>
    /// 한 종목의 보유 현황. Cost = 넣은 돈(산 금액 합계, 수수료 제외).
    /// 손익은 넣은 돈의 Leverage배로 계산된다 (3배: $10 넣으면 $30어치처럼 움직임). 손해가 넣은 돈보다 크면 평가금이 마이너스.
    /// </summary>
    public readonly struct Holding
    {
        public readonly int Shares;
        public readonly float Cost;
        public readonly float Leverage;

        public Holding(int shares, float cost, float leverage)
        {
            Shares = shares;
            Cost = cost;
            Leverage = leverage;
        }

        public float AveragePrice => Shares > 0 ? Cost / Shares : 0f;
        public float ProfitLoss(float price) => (Shares * price - Cost) * Leverage;
        public float ReturnRate(float price) => Cost > 0f ? ProfitLoss(price) / Cost : 0f;
        /// <summary>평가금 = 넣은 돈 + 손익. 마이너스가 될 수 있다.</summary>
        public float Value(float price) => Cost + ProfitLoss(price);
    }

    /// <summary>현금과 보유 종목. 모든 매매는 여기서만 일어난다.</summary>
    public class Portfolio
    {
        readonly Dictionary<StockState, Holding> holdings = new();
        readonly float feeRate;
        readonly int maxHoldings;
        readonly float leverage;

        /// <summary>장 마감 정산에서 마이너스가 빠지면 0 밑이 될 수 있다 (그러면 긴급 대출 아니면 파산).</summary>
        public float Cash { get; private set; }
        public int HoldingCount => holdings.Count;
        /// <summary>듀얼 모니터: 그날만 보유 종목 수 +1.</summary>
        public int BonusHoldings { get; set; }
        public int MaxHoldings => maxHoldings + BonusHoldings;
        /// <summary>영혼의 풀매수: 다음 매수 1회의 레버리지 (0이면 기본).</summary>
        public float NextBuyLeverage { get; set; }
        /// <summary>악마의 계약서: 다음 매도 1회의 손익 배수 (1이면 그대로).</summary>
        public float NextSellMultiplier { get; set; } = 1f;
        /// <summary>마지막 매도의 실현 손익 (손실 보험 계산용).</summary>
        public float LastRealized { get; private set; }
        public float Leverage => leverage;
        /// <summary>은행 VIP 쿠폰·영혼의 풀매수: true인 동안 매매 수수료 0.</summary>
        public bool FeeWaived { get; set; }
        float Fee => FeeWaived ? 0f : feeRate;
        /// <summary>지금 적용되는 매매 수수료율 (표시·예상용).</summary>
        public float FeeRateNow => Fee;

        public event Action Changed;

        public Portfolio(float cash, float feeRate, int maxHoldings, float leverage = 1f)
        {
            Cash = cash;
            this.feeRate = feeRate;
            this.maxHoldings = maxHoldings;
            this.leverage = Mathf.Max(1f, leverage);
        }

        /// <summary>대출금 입금, 상환·아이템 구매 출금. 출금은 현금이 모자라면 실패한다.</summary>
        public void Deposit(float amount)
        {
            if (amount <= 0f) return;
            Cash += amount;
            Changed?.Invoke();
        }

        public bool Withdraw(float amount)
        {
            if (amount <= 0f || amount > Cash + 0.001f) return false;
            Cash = Mathf.Max(0f, Cash - amount);
            Changed?.Invoke();
            return true;
        }

        public Holding GetHolding(StockState stock) =>
            holdings.TryGetValue(stock, out var h) ? h : default;

        public IEnumerable<StockState> HeldStocks => holdings.Keys;

        public int MaxBuyable(float price) =>
            price > 0f && Cash > 0f ? Mathf.FloorToInt(Cash / (price * (1f + Fee))) : 0;

        public TradeResult Execute(OrderRequest order, float price)
        {
            if (order.Side == OrderSide.Buy)
            {
                int qty = order.UseMax ? MaxBuyable(price) : order.Quantity;
                if (order.UseMax && qty < 1) return TradeResult.Fail(order.Stock, OrderSide.Buy, TradeError.NotEnoughCash);
                return Buy(order.Stock, qty, price);
            }

            int shares = order.UseMax ? GetHolding(order.Stock).Shares : order.Quantity;
            if (order.UseMax && shares < 1) return TradeResult.Fail(order.Stock, OrderSide.Sell, TradeError.NotEnoughShares);
            return Sell(order.Stock, shares, price);
        }

        public TradeResult Buy(StockState stock, int quantity, float price)
        {
            if (quantity < 1) return TradeResult.Fail(stock, OrderSide.Buy, TradeError.InvalidQuantity);

            var h = GetHolding(stock);
            if (h.Shares == 0 && holdings.Count >= MaxHoldings)
                return TradeResult.Fail(stock, OrderSide.Buy, TradeError.TooManyHoldings);

            float total = quantity * price * (1f + Fee);
            if (total > Cash) return TradeResult.Fail(stock, OrderSide.Buy, TradeError.NotEnoughCash);

            Cash -= total;
            // shortcut: 풀매수로 다른 레버리지를 섞어 사면 넣은 돈 비율로 평균낸다 (산 가격이 크게 다르면 손익이 조금 어긋남), 정확히 하려면 매수분마다 따로 보관
            float buyLev = NextBuyLeverage > 0f ? NextBuyLeverage : leverage;
            NextBuyLeverage = 0f;
            float addCost = quantity * price;
            float lev = h.Shares > 0 ? (h.Cost * h.Leverage + addCost * buyLev) / (h.Cost + addCost) : buyLev;
            holdings[stock] = new Holding(h.Shares + quantity, h.Cost + addCost, lev);
            Changed?.Invoke();
            return TradeResult.Ok(stock, OrderSide.Buy, quantity, price);
        }

        /// <summary>
        /// 판 만큼의 평가금(넣은 돈 + 손익)을 받는다. 마이너스 종목을 팔면 그만큼 현금이 빠져나가고,
        /// 현금으로 메울 수 없으면 팔 수 없다 (NotEnoughCash).
        /// </summary>
        public TradeResult Sell(StockState stock, int quantity, float price)
        {
            if (quantity < 1) return TradeResult.Fail(stock, OrderSide.Sell, TradeError.InvalidQuantity);

            var h = GetHolding(stock);
            if (quantity > h.Shares) return TradeResult.Fail(stock, OrderSide.Sell, TradeError.NotEnoughShares);

            float soldCost = h.AveragePrice * quantity;
            float realized = (quantity * price - soldCost) * h.Leverage * NextSellMultiplier;
            float proceeds = soldCost + realized - quantity * price * Fee;
            if (Cash + proceeds < 0f) return TradeResult.Fail(stock, OrderSide.Sell, TradeError.NotEnoughCash);

            Cash += proceeds;
            LastRealized = realized;
            NextSellMultiplier = 1f;
            int remaining = h.Shares - quantity;
            if (remaining == 0) holdings.Remove(stock);
            else holdings[stock] = new Holding(remaining, h.Cost - soldCost, h.Leverage);
            Changed?.Invoke();
            return TradeResult.Ok(stock, OrderSide.Sell, quantity, price);
        }

        /// <summary>
        /// 장 마감 정산: 평가금이 마이너스인 종목을 정리하고 그 마이너스만큼 현금에서 뺀다 (현금이 0 밑이 될 수 있음).
        /// 플러스 종목은 그대로 다음 날로. 돌려주는 값 = 뺀 금액(양수).
        /// </summary>
        public float SettleNegatives(Func<StockState, float> priceOf, List<StockState> closed)
        {
            float charged = 0f;
            foreach (var (stock, h) in new List<KeyValuePair<StockState, Holding>>(holdings))
            {
                float value = h.Value(priceOf(stock));
                if (value >= 0f) continue;
                charged -= value;
                holdings.Remove(stock);
                closed?.Add(stock);
            }
            if (charged > 0f)
            {
                Cash -= charged;
                Changed?.Invoke();
            }
            return charged;
        }
    }
}
