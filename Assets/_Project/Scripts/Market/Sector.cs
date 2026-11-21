namespace BurningAnt.Market
{
    /// <summary>업종 5개. 같은 업종의 종목들은 기사 템플릿(SectorNews)을 함께 쓴다.</summary>
    public enum Sector
    {
        Bio,         // 바이오·제약
        Electronics, // 전자·반도체
        Fashion,     // 의류·패션
        Food,        // 식품·외식
        Energy,      // 에너지·화학
    }

    public static class SectorExtensions
    {
        public static string ToKorean(this Sector sector) => sector switch
        {
            Sector.Bio => "바이오·제약",
            Sector.Electronics => "전자·반도체",
            Sector.Fashion => "의류·패션",
            Sector.Food => "식품·외식",
            _ => "에너지·화학",
        };
    }
}
