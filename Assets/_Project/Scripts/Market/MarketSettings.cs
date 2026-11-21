using UnityEngine;

namespace BurningAnt.Market
{
    /// <summary>장 · 거래 · 가격 생성 밸런스 값. 숫자는 코드가 아니라 이 에셋에서 고친다.</summary>
    [CreateAssetMenu(menuName = "BurningAnt/Market Settings", fileName = "MarketSettings")]
    public class MarketSettings : ScriptableObject
    {
        [Header("장 시간")]
        [Tooltip("하루 장의 틱 수")] public int ticksPerDay = 120;
        [Tooltip("틱 하나의 실제 시간(초)")] public float secondsPerTick = 0.5f;

        [Header("거래")]
        public float startingCash = 10000f;
        [Tooltip("거래금액 대비 수수료 (0.002 = 0.2%)")] public float feeRate = 0.002f;
        [Tooltip("동시에 보유할 수 있는 최대 종목 수")] public int maxHoldings = 4;
        [Tooltip("손익 배수. 3 = $10 넣으면 $30어치처럼 움직임. 손해가 넣은 돈보다 크면 넘친 만큼 장 마감 때 잔고에서 빠짐")]
        public float leverage = 3f;

        [Header("하루 등락 한도 (전일 종가 대비)")]
        public float stableLimit = 0.05f;
        public float normalLimit = 0.12f;
        public float highRiskLimit = 0.25f;

        [Header("가격 생성 (한도 대비 비율)")]
        [Range(0f, 1f), Tooltip("시가 갭 최대폭")] public float maxGapRatio = 0.4f;
        [Range(0f, 1f), Tooltip("하루 추세 최대폭")] public float maxTrendRatio = 0.6f;
        [Range(0f, 1f), Tooltip("틱마다 흔들림 크기")] public float noiseRatio = 0.1f;
        [Range(0f, 1f), Tooltip("추세선으로 당기는 힘")] public float pullStrength = 0.08f;

        public float GetDailyLimit(StockType type) => type switch
        {
            StockType.Stable => stableLimit,
            StockType.Normal => normalLimit,
            _ => highRiskLimit,
        };
    }
}
