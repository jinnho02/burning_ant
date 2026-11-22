using UnityEngine;

namespace BurningAnt.Items
{
    /// <summary>아이템 효과 종류. 효과 동작은 코드(GameLoop)에, 숫자는 ItemDefinition 에셋에 있다.
    /// 순서를 바꾸면 에셋의 kind 번호가 어긋나므로 새 종류는 맨 뒤에만 붙인다.</summary>
    public enum ItemKind
    {
        Rumor,          // 증권가 찌라시: 랜덤 1종목 오늘 방향 (accuracy 확률로 맞음)
        AnalystReport,  // 애널리스트 리포트: 고른 1종목 오늘 방향
        InsiderMemo,    // 내부자 메모: 고른 1종목 방향 + 예상 등락폭
        ChainForecast,  // 수상한 전화: 오늘 뉴스 하나의 성공·실패
        StopLoss,       // 손절 예약권
        TakeProfit,     // 익절 예약권
        SlowMotion,     // 모래시계: 차트 느리게
        TimeStop,       // 에너지 드링크: 차트 멈춤
        Telescope,      // 미래를 보는 망원경: 고른 종목의 앞으로 몇 초를 따로 창에
        SectorRumor,    // (안 씀)
        OpenLeak,       // 시초가 유출: 고른 종목 오늘 시가
        VolatilityRadar, // 변동성 레이더: 오늘 가장 크게 움직일 2종목 (방향은 비밀)
        InterestCoupon, // 은행 VIP 쿠폰: 다음 이자 30% 감면
        LossInsurance,  // 손실 보험 증서: 다음 매도 1회 손실의 일부 보상
        CashLoan,       // 무대출·콩대출·사채업자 명함·더 강한 악마의 계약서: 지금 현금, 대신 빚
        InterestDeferral, // 이자 유예 신청서: 다음 이자 하루 연기 (게임당 1회)
        DebtAdjustment, // 채무 조정 신청서: 기본 대출 원금 1% 감면 (게임당 1회)
        SunshineLoan,   // (안 씀: 햇살론은 파산 직전에만)
        Lottery,        // 인생역전 복권: 장 결과 때 확률로 현금
        SoulAllIn,      // 영혼의 풀매수: 다음 매수 1회 레버리지 10배
        TrailingStop,   // 추적 손절권
        TargetBuy,      // 목표가 예약권
        DevilTrade,     // 악마의 계약서: 다음 매도 1회 손익 1.5배 (이익·손해 모두)
        BankruptAlarm,  // 파산 경보기: 현금+주식이 위험 금액 밑으로 가면 경고
        EmergencyExit,  // 비상 탈출 버튼: 보유 종목 전부 지금 가격에 매도
        DualMonitor,    // 듀얼 모니터: 오늘 보유 가능 종목 수 +1
        CreditLaunder,  // 신용등급 세탁기: 다음 대출 1회(기본 대출 제외) 이자율 절반
    }

    public enum ItemTiming
    {
        Prep,    // 아이템 시간용 (2단계)
        Market,  // 장 시간용 (3단계)
        Anytime, // 2단계·3단계 둘 다
    }

    /// <summary>아이템 하나. Data/Items에 에셋으로 만든다 (BurningAnt > Setup > Items).</summary>
    [CreateAssetMenu(menuName = "BurningAnt/Item", fileName = "Item")]
    public class ItemDefinition : ScriptableObject
    {
        public ItemKind kind;
        public string displayName = "새 아이템";
        public float price = 100f;
        [TextArea] public string description;
        [Tooltip("상점 칸에 보일 그림. 비우면 빈 칸으로 보인다.")] public Texture2D icon;

        [Header("정보 아이템")]
        [Range(0f, 1f), Tooltip("정보가 맞을 확률 (0.65 = 65%)")] public float accuracy = 0.75f;

        [Header("장 시간용 아이템")]
        [Tooltip("손절·익절·추적: 손익률 기준 (0.15 = 15%) / 목표가: 지금 가격에서 내려갈 비율")] public float triggerRate = 0.05f;
        [Tooltip("모래시계·에너지 드링크·망원경: 실제 시간(초)")] public float durationSeconds = 5f;
        [Tooltip("모래시계: 차트 속도 배율 / 풀매수: 레버리지 / 악마의 계약서: 손익 배수")] public float speedMultiplier = 0.5f;

        [Header("돈·대출 아이템")]
        [Tooltip("대출 아이템: 받는 현금 / 손실 보험: 돌려받는 비율 / 복권: 당첨금 / 파산 경보기: 위험 금액 / 채무 조정: 감면 비율")]
        public float amount;
        [Tooltip("대출 아이템: 늘어나는 빚 / 손실 보험: 최대 보상 / 복권: 당첨 확률")] public float debtAmount;
        [Tooltip("대출 아이템: 하루 이자율 (0이면 기본 대출 원금에 더함)")] public float loanRate;

        /// <summary>2단계(장 전)용 · 3단계(장 중)용 · 둘 다.</summary>
        public ItemTiming Timing => kind switch
        {
            ItemKind.TrailingStop or ItemKind.TargetBuy
                or ItemKind.SlowMotion or ItemKind.TimeStop or ItemKind.Telescope or ItemKind.EmergencyExit => ItemTiming.Market,
            ItemKind.CashLoan or ItemKind.SoulAllIn or ItemKind.DevilTrade or ItemKind.LossInsurance or ItemKind.BankruptAlarm
                or ItemKind.StopLoss or ItemKind.TakeProfit => ItemTiming.Anytime, // 손절·익절 예약권: 2단계에서도 미리 걸 수 있음
            _ => ItemTiming.Prep,
        };

        /// <summary>사용할 때 종목을 골라야 하는지.</summary>
        public bool NeedsTarget => kind is ItemKind.Rumor or ItemKind.AnalystReport or ItemKind.InsiderMemo or ItemKind.StopLoss or ItemKind.TakeProfit
            or ItemKind.OpenLeak or ItemKind.TrailingStop or ItemKind.TargetBuy or ItemKind.Telescope;

        /// <summary>엠바고: 대상 종목을 오늘 뉴스가 뜬 종목 중에서만 고른다.</summary>
        public bool TargetsNewsOnly => kind is ItemKind.Rumor;

        /// <summary>대상 종목을 보유 종목 중에서만 고를 수 있는지.</summary>
        public bool TargetsHeldOnly => kind is ItemKind.StopLoss or ItemKind.TakeProfit or ItemKind.TrailingStop;
    }
}
