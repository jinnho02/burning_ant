using System;
using System.Collections.Generic;
using UnityEngine;

namespace BurningAnt.Market
{
    /// <summary>한 종목의 보유 현황. Cost = 산 금액 합계(수수료 제외).</summary>
    public readonly struct Holding
    {
        public readonly int Shares;
        public readonly float Cost;

        public Holding(int shares, float cost)
        {
            Shares = shares;
            Cost = cost;
        }

        public float AveragePrice => Shares > 0 ? Cost / Shares : 0f;
        public float ProfitLoss(float price) => Shares * price - Cost;
        public float ReturnRate(float price) => Cost > 0f ? ProfitLoss(price) / Cost : 0f;
    }

    /// <summary>현금과 보유 종목. 모든 매매는 여기서만 일어난다.</summary>
    public class Portfolio
    {
        readonly Dictionary<StockState, Holding> holdings = new();
        readonly float feeRate;
        readonly int maxHoldings;

        public float Cash { get; private set; }
        public int HoldingCount => holdings.Count;
        public int MaxHoldings => maxHoldings;

        public event Action Changed;

        public Portfolio(float cash, float feeRate, int maxHoldings)
        {
            Cash = cash;
            this.feeRate = feeRate;
            this.maxHoldings = maxHoldings;
        }

        public Holding GetHolding(StockState stock) =>
            holdings.TryGetValue(stock, out var h) ? h : default;

        public int MaxBuyable(float price) =>
            price > 0f ? Mathf.FloorToInt(Cash / (price * (1f + feeRate))) : 0;

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
            if (h.Shares == 0 && holdings.Count >= maxHoldings)
                return TradeResult.Fail(stock, OrderSide.Buy, TradeError.TooManyHoldings);

            float total = quantity * price * (1f + feeRate);
            if (total > Cash) return TradeResult.Fail(stock, OrderSide.Buy, TradeError.NotEnoughCash);

            Cash -= total;
            holdings[stock] = new Holding(h.Shares + quantity, h.Cost + quantity * price);
            Changed?.Invoke();
            return TradeResult.Ok(stock, OrderSide.Buy, quantity, price);
        }

        public TradeResult Sell(StockState stock, int quantity, float price)
        {
            if (quantity < 1) return TradeResult.Fail(stock, OrderSide.Sell, TradeError.InvalidQuantity);

            var h = GetHolding(stock);
            if (quantity > h.Shares) return TradeResult.Fail(stock, OrderSide.Sell, TradeError.NotEnoughShares);

            float averagePrice = h.AveragePrice;
            Cash += quantity * price * (1f - feeRate);
            int remaining = h.Shares - quantity;
            if (remaining == 0) holdings.Remove(stock);
            else holdings[stock] = new Holding(remaining, h.Cost - averagePrice * quantity);
            Changed?.Invoke();
            return TradeResult.Ok(stock, OrderSide.Sell, quantity, price);
        }
    }
}
