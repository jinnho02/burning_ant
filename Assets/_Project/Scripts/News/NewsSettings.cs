using UnityEngine;

namespace BurningAnt.News
{
    /// <summary>뉴스 개수·영향 폭·연쇄 뉴스 확률과 문구 템플릿. 문구의 {0}은 종목 이름으로 바뀐다.</summary>
    [CreateAssetMenu(menuName = "BurningAnt/News Settings", fileName = "NewsSettings")]
    public class NewsSettings : ScriptableObject
    {
        [Header("하루 뉴스")]
        public int minNewsPerDay = 2;
        public int maxNewsPerDay = 3;
        [Tooltip("일반 뉴스가 그날 결과에 더하는 폭 (0.01 = 1%)")] public float effectMin = 0.01f;
        public float effectMax = 0.04f;

        [Header("연쇄 뉴스")]
        [Tooltip("진행 중인 연쇄 뉴스가 없을 때, 그날 뉴스 하나가 연쇄 1단계일 확률")] public float chainStartChance = 0.3f;
        public float chainStartEffectMin = 0.03f;
        public float chainStartEffectMax = 0.10f;
        [Range(0f, 1f)] public float chainSuccessChance = 0.6f;
        public float chainSuccessEffectMin = 0.05f;
        public float chainSuccessEffectMax = 0.15f;
        public float chainFailEffectMin = 0.08f;
        public float chainFailEffectMax = 0.20f;

        [Header("문구 ({0} = 종목 이름)")]
        public string[] goodNews =
        {
            "{0}, 분기 실적 예상치 소폭 상회",
            "{0} 대표 \"올해는 다르다\" 인터뷰",
            "{0}, 신제품 사전 예약 순조",
            "외국인, {0} 조용히 순매수",
        };

        public string[] badNews =
        {
            "{0}, 분기 실적 기대에 살짝 못 미쳐",
            "{0} 공장에 작은 화재… 피해는 경미",
            "{0} 임원, 보유 주식 일부 매도",
            "증권가, {0} 목표가 소폭 하향",
        };

        public string[] chainStartNews =
        {
            "[속보] {0}, 대형 계약 성사 임박?",
            "[단독] {0}, 해외 기업 인수 협상 중",
        };

        public string[] chainSuccessNews =
        {
            "[확정] {0}, 계약 최종 성사! 시장 환호",
            "[확정] {0}, 협상 타결 발표",
        };

        public string[] chainFailNews =
        {
            "[확정] {0}, 계약 결국 무산… 실망 매물",
            "[확정] {0}, 협상 결렬 공식 발표",
        };
    }
}
