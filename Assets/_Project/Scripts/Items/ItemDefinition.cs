using UnityEngine;

namespace BurningAnt.Items
{
    /// <summary>아이템 효과 종류. 효과 동작은 코드(GameLoop)에, 숫자는 ItemDefinition 에셋에 있다.</summary>
    public enum ItemKind
    {
        Rumor,          // 찌라시: 랜덤 1종목 오늘 방향
        AnalystReport,  // 애널리스트 리포트: 고른 1종목 오늘 방향
        InsiderMemo,    // 내부자 메모: 고른 1종목 오늘 방향 + 대략적 폭
        ChainForecast,  // 연쇄 예측: 진행 중인 연쇄 뉴스 성공 여부
        StopLoss,       // 손절 예약
        TakeProfit,     // 익절 예약
        SlowMotion,     // 슬로우 모션
        TimeStop,       // 시간 정지
        Telescope,      // 망원경
    }

    public enum ItemTiming
    {
        Prep,   // 아이템 시간용 (장 전)
        Market, // 장 시간용
    }

    /// <summary>아이템 하나. Data/Items에 에셋으로 만든다.</summary>
    [CreateAssetMenu(menuName = "BurningAnt/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public ItemKind kind;
        public string displayName = "새 아이템";
        public float price = 100f;
        [TextArea] public string description;
        [Tooltip("상점 칸에 보일 그림. 비우면 빈 칸으로 보인다.")] public Texture2D icon;

        [Header("정보 아이템")]
        [Range(0f, 1f), Tooltip("힌트가 맞을 확률")] public float accuracy = 0.75f;

        [Header("장 시간용 아이템")]
        [Tooltip("손절·익절: 평단 대비 이 비율에 닿으면 자동 매도 (0.05 = 5%)")] public float triggerRate = 0.05f;
        [Tooltip("슬로우 모션·시간 정지·망원경: 실제 시간(초)")] public float durationSeconds = 5f;
        [Tooltip("슬로우 모션: 차트 속도 배율")] public float speedMultiplier = 0.5f;

        public ItemTiming Timing => kind switch
        {
            ItemKind.Rumor or ItemKind.AnalystReport or ItemKind.InsiderMemo or ItemKind.ChainForecast => ItemTiming.Prep,
            _ => ItemTiming.Market,
        };

        /// <summary>사용할 때 종목을 골라야 하는지.</summary>
        public bool NeedsTarget => kind is ItemKind.AnalystReport or ItemKind.InsiderMemo or ItemKind.StopLoss or ItemKind.TakeProfit;

        /// <summary>대상 종목을 보유 종목 중에서만 고를 수 있는지.</summary>
        public bool TargetsHeldOnly => kind is ItemKind.StopLoss or ItemKind.TakeProfit;
    }
}
