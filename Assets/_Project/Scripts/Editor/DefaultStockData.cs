using BurningAnt.Market;

namespace BurningAnt.Editor
{
    /// <summary>
    /// 기본 20종목 (팀 문서 "업종 및 기사 정리"). 기본 변동률은 기획서 5장 초안(10~50%). 시작가는 임시 (원).
    /// 셋업 메뉴가 Data/Stocks 에셋을 만들 때 쓴다. 에셋이 생긴 뒤에는 에셋을 고친다.
    /// </summary>
    public static class DefaultStockData
    {
        public static readonly (string file, string name, Sector sector, string concept, float range, float price)[] Stocks =
        {
            ("Bio_Bulsa", "불사제약", Sector.Bio, "불로장생 신약 개발", 0.26f, 48000f),
            ("Bio_CellRevive", "셀부활바이오", Sector.Bio, "줄기세포·재생의학", 0.34f, 32000f),
            ("Bio_Panacea", "만병통치", Sector.Bio, "신약 및 백신 개발", 0.22f, 75000f),
            ("Bio_HumanClone", "휴먼클론", Sector.Bio, "유전자 복제·생명공학", 0.42f, 21000f),
            ("Elec_Genius", "천재전자", Sector.Electronics, "스마트폰·가전제품", 0.14f, 120000f),
            ("Elec_Supercon", "초전도테크", Sector.Electronics, "초전도체 연구개발", 0.42f, 18000f),
            ("Elec_NanoChips", "나노칩스", Sector.Electronics, "차세대 반도체 제조", 0.22f, 88000f),
            ("Elec_Quantum", "퀀텀컴퓨팅", Sector.Electronics, "양자컴퓨터·AI 기술", 0.34f, 54000f),
            ("Fashion_Trend", "유행패션", Sector.Fashion, "트렌디한 패스트패션", 0.18f, 26000f),
            ("Fashion_FakeLuxury", "명품인척", Sector.Fashion, "고급 패션 브랜드", 0.22f, 140000f),
            ("Fashion_FitIsGood", "핏이즈굿", Sector.Fashion, "스포츠웨어·애슬레저", 0.14f, 44000f),
            ("Fashion_Thread", "실밥홀딩스", Sector.Fashion, "의류 제조·유통", 0.10f, 15000f),
            ("Food_NyamNyam", "냠냠푸드", Sector.Food, "종합 식품기업", 0.10f, 62000f),
            ("Food_ChickenMoon", "치킨투더문", Sector.Food, "글로벌 치킨 프랜차이즈", 0.22f, 37000f),
            ("Food_Masitdang", "맛있당", Sector.Food, "간편식·밀키트", 0.14f, 23000f),
            ("Food_ZeroCal", "제로칼로리", Sector.Food, "다이어트 식품·음료", 0.18f, 29000f),
            ("Energy_Power", "파워에너지", Sector.Energy, "석유·천연가스", 0.14f, 96000f),
            ("Energy_Solar", "태양만세", Sector.Energy, "태양광 발전", 0.22f, 34000f),
            ("Energy_Fusion", "핵융합의꿈", Sector.Energy, "핵융합 발전 연구", 0.50f, 12000f),
            ("Energy_Battery", "배터리풀충", Sector.Energy, "이차전지·배터리 제조", 0.26f, 71000f),
        };
    }
}
