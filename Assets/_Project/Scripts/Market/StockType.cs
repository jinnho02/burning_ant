namespace BurningAnt.Market
{
    /// <summary>종목 성향. 하루 등락 한도는 MarketSettings에서 성향별로 정한다.</summary>
    public enum StockType
    {
        Stable,   // 안정형
        Normal,   // 보통형
        HighRisk, // 고위험형
    }

    public static class StockTypeExtensions
    {
        public static string ToKorean(this StockType type) => type switch
        {
            StockType.Stable => "안정형",
            StockType.Normal => "보통형",
            _ => "고위험형",
        };
    }
}
