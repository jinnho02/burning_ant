using UnityEngine;

namespace BurningAnt.Market
{
    /// <summary>종목 하나의 고정 정보. Data/Stocks에 에셋으로 만든다.</summary>
    [CreateAssetMenu(menuName = "BurningAnt/Stock", fileName = "Stock")]
    public class StockDefinition : ScriptableObject
    {
        public string displayName = "새 종목";
        public StockType type = StockType.Normal;
        [Tooltip("게임 시작 시 전일 종가")] public float startPrice = 100f;
    }
}
