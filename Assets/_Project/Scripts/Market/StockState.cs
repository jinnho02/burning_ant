using UnityEngine;

namespace BurningAnt.Market
{
    /// <summary>게임 중 종목 하나의 상태 (전일 종가, 오늘 가격 경로).</summary>
    public class StockState
    {
        public StockDefinition Definition { get; }
        public float DailyLimit { get; }
        public float PrevClose { get; private set; }
        public float[] Path { get; private set; }

        public string Name => Definition.displayName;
        public float UpperLimit => PrevClose * (1f + DailyLimit);
        public float LowerLimit => PrevClose * (1f - DailyLimit);
        public float OpenPrice => Path[0];

        public StockState(StockDefinition definition, float dailyLimit)
        {
            Definition = definition;
            DailyLimit = dailyLimit;
            PrevClose = definition.startPrice;
        }

        public float PriceAt(int tick) => Path[Mathf.Clamp(tick, 0, Path.Length - 1)];

        /// <summary>오늘 가격 경로를 정한다. 뉴스 단계에서 확정되고 이후 바뀌지 않는다.</summary>
        public void PrepareDay(float[] path) => Path = path;

        /// <summary>장 마감가를 다음 날의 전일 종가로 넘긴다.</summary>
        public void CloseDay() => PrevClose = Path[Path.Length - 1];
    }
}
