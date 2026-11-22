using UnityEngine;

namespace BurningAnt.Loan
{
    /// <summary>대출 · 이자 · 승패 밸런스 값. 돈 단위는 원.</summary>
    [CreateAssetMenu(menuName = "BurningAnt/Loan Settings", fileName = "LoanSettings")]
    public class LoanSettings : ScriptableObject
    {
        [Tooltip("시작 대출금 (현금도 같은 금액으로 시작)")] public float startingDebt = 100_000_000f;

        [Header("이자 (매일, 장 결과에서 '이자 갚기'로 냄. 안 내면 빚에 붙음)")]
        [Tooltip("하루 이자율 (0.05 = 남은 빚의 5%)")] public float baseInterestRate = 0.05f;
        [Tooltip("이 일차부터 이자가 붙음 (3 = 1·2일차는 없음)")] public int interestStartDay = 3;

        [Header("추가 대출 (2·3단계에서, 한 판에 1번)")]
        [Tooltip("한 번에 빌릴 수 있는 최대 금액")] public float extraLoanMax = 200_000_000f;
        [Tooltip("추가 대출 하루 이자율 (일반 대출과 따로)")] public float extraLoanRate = 0.30f;
        [Tooltip("빌린 날로부터 며칠 뒤부터 이자 (2 = 다다음 날)")] public int extraLoanGraceDays = 2;

        // shortcut: 금액·이자율은 팀 결정 전 제안값.
        [Header("햇살론 = 긴급 대출 (한 판에 1번, 장 마감 정산 후 현금이 0 밑이면 제안. 거절하면 파산)")]
        [Tooltip("모자란 금액에 더 얹어 주는 돈")] public float emergencyLoanBuffer = 10_000_000f;
        [Tooltip("햇살론 최대 금액 (파산 직전에만 1회). 모자란 돈이 이보다 크면 파산")] public float emergencyLoanMax = 200_000_000f;
        [Tooltip("햇살론 하루 이자율 (기본 대출과 따로, 받은 다음 날부터)")] public float emergencyRate = 0.30f;

        [Header("승리")]
        [Tooltip("빚(원금+이자)을 다 갚고, 자산(현금+주식)이 이만큼 있으면 승리")] public float winAssets = 1_000_000_000f;
    }
}
