using UnityEngine;

namespace BurningAnt.Market
{
    /// <summary>종목 하나의 고정 정보. Data/Stocks에 에셋으로 만든다.</summary>
    [CreateAssetMenu(menuName = "BurningAnt/Stock", fileName = "Stock")]
    public class StockDefinition : ScriptableObject
    {
        public string displayName = "새 종목";
        [Tooltip("업종. 같은 업종 종목은 기사 템플릿(SectorNews)을 함께 쓴다")] public Sector sector = Sector.Bio;
        [Tooltip("기업 콘셉트 (화면 표시용)")] public string concept = "";
        [Tooltip("게임 시작 시 전일 종가")] public float startPrice = 100f;
        [Range(0.1f, 0.5f), Tooltip("기본 변동률 구간: 뉴스·예지가 없는 날 전날 종가 ± 이 비율 안에서만 움직인다 (0.2 = ±20%)")]
        public float baseRange = 0.2f;
    }
}
