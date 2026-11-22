namespace BurningAnt.Market
{
    public enum OrderSide { Buy, Sell }

    /// <summary>매수·매도 요청. UseMax면 Quantity를 무시하고 가능한 최대 수량.</summary>
    public readonly struct OrderRequest
    {
        public readonly StockState Stock;
        public readonly OrderSide Side;
        public readonly int Quantity;
        public readonly bool UseMax;

        public OrderRequest(StockState stock, OrderSide side, int quantity, bool useMax)
        {
            Stock = stock;
            Side = side;
            Quantity = quantity;
            UseMax = useMax;
        }
    }

    public enum TradeError
    {
        None,
        InvalidQuantity,
        NotEnoughCash,
        NotEnoughShares,
        TooManyHoldings,
        MarketClosed,
        GapTooLarge, // 예약: 시가가 기준가(전날 종가)보다 허용 폭 넘게 불리하게 열려 취소
    }

    public readonly struct TradeResult
    {
        public readonly StockState Stock;
        public readonly OrderSide Side;
        public readonly TradeError Error;
        public readonly int Shares;
        public readonly float Price;

        public bool Success => Error == TradeError.None;

        TradeResult(StockState stock, OrderSide side, TradeError error, int shares, float price)
        {
            Stock = stock;
            Side = side;
            Error = error;
            Shares = shares;
            Price = price;
        }

        public static TradeResult Ok(StockState stock, OrderSide side, int shares, float price) =>
            new(stock, side, TradeError.None, shares, price);

        public static TradeResult Fail(StockState stock, OrderSide side, TradeError error) =>
            new(stock, side, error, 0, 0f);

        public static TradeResult Fail(StockState stock, OrderSide side, TradeError error, float price) =>
            new(stock, side, error, 0, price);
    }
}
