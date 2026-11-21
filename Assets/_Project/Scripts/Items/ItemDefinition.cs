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
        // ── v0.2.1 아이템 (이미지 이름 기준, 전부 1회성 소모품) ──
        SectorRumor,      // 수상한 전화: 고른 종목의 업종에서 무작위 2종목 예지 (장 전)
        OpenLeak,         // 시초가 유출: 고른 종목의 오늘 시가 공개 (장 전)
        VolatilityRadar,  // 변동성 레이더: 오늘 장 중 특보가 뜨는 종목·시각 공개 (장 전)
        FeeWaiver,        // 은행 VIP 쿠폰: 오늘 매매 수수료 면제 (장 전)
        LossInsurance,    // 손실 보험 증서: 오늘 손실의 일부를 장 마감에 돌려받음 (장 전)
        DevilContract,    // 악마의 계약서 / 더 강한 악마의 계약서: 지금 현금, 대신 빚이 더 크게 (장 전)
        InterestDeferral, // 이자 유예 신청서: 다음 이자 1번 면제 (장 전)
        DebtAdjustment,   // 채무 조정 신청서: 이자율 인하 (장 전)
        SunshineLoan,     // 햇살론(회생): 긴급 대출 기회 1번 더 (장 전)
        Lottery,          // 인생역전 복권: 확률로 현금 (장 전)
        SoulAllIn,        // 영혼의 풀매수: 고른 종목을 현금 전부로 즉시 매수, 수수료 없음 (장 중)
        TrailingStop,     // 추적 손절권: 최고 수익률에서 일정 비율 밀리면 자동 매도 (장 중)
        TargetBuy,        // 목표가 예약권: 고른 종목이 지금보다 일정 비율 내리면 현금 전부로 자동 매수 (장 중)
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
        [Range(0f, 1f), Tooltip("(지금은 안 씀: 정보 아이템은 예지라서 틀리지 않음)")] public float accuracy = 0.75f;

        [Header("장 시간용 아이템")]
        [Tooltip("손절·익절: 평단 대비 이 비율에 닿으면 자동 매도 (0.05 = 5%)")] public float triggerRate = 0.05f;
        [Tooltip("슬로우 모션·시간 정지·망원경: 실제 시간(초)")] public float durationSeconds = 5f;
        [Tooltip("슬로우 모션: 차트 속도 배율")] public float speedMultiplier = 0.5f;

        [Header("돈·대출 아이템")]
        [Tooltip("악마의 계약서: 받는 현금 / 손실 보험: 돌려받는 비율(0.5 = 손실의 50%) / 채무 조정: 내리는 이자율(0.02 = 2%p)")]
        public float amount;
        [Tooltip("악마의 계약서: 늘어나는 빚")] public float debtAmount;

        /// <summary>장 전(2단계)용인지 장 중용인지. 기획 v0.2.1: 아이템은 이 둘로 나뉜다.</summary>
        public ItemTiming Timing => kind switch
        {
            ItemKind.StopLoss or ItemKind.TakeProfit or ItemKind.SlowMotion or ItemKind.TimeStop or ItemKind.Telescope
                or ItemKind.SoulAllIn or ItemKind.TrailingStop or ItemKind.TargetBuy => ItemTiming.Market,
            _ => ItemTiming.Prep,
        };

        /// <summary>사용할 때 종목을 골라야 하는지.</summary>
        public bool NeedsTarget => kind is ItemKind.AnalystReport or ItemKind.InsiderMemo or ItemKind.StopLoss or ItemKind.TakeProfit
            or ItemKind.SectorRumor or ItemKind.OpenLeak or ItemKind.SoulAllIn or ItemKind.TrailingStop or ItemKind.TargetBuy;

        /// <summary>대상 종목을 보유 종목 중에서만 고를 수 있는지.</summary>
        public bool TargetsHeldOnly => kind is ItemKind.StopLoss or ItemKind.TakeProfit or ItemKind.TrailingStop;
    }
}
