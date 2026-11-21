using UnityEngine;

namespace BurningAnt.Loan
{
    /// <summary>대출 · 이자 · 승패 밸런스 값 (기획서 5장 대출·이자).</summary>
    [CreateAssetMenu(menuName = "BurningAnt/Loan Settings", fileName = "LoanSettings")]
    public class LoanSettings : ScriptableObject
    {
        [Tooltip("시작 대출금 (본인 돈 $0)")] public float startingDebt = 10000f;

        [Header("이자")]
        [Tooltip("이자율 (0.05 = 남은 빚의 5%)")] public float baseInterestRate = 0.05f;
        [Tooltip("며칠마다 이자가 붙는지 (7 = 7·14·21일차 정산 때)")] public int interestPeriodDays = 7;

        [Header("추가 대출 (2단계에서만)")]
        public float extraLoanAmount = 5000f;
        public int maxExtraLoans = 3;
        [Tooltip("추가 대출 1회마다 오르는 이자율 (0.02 = +2%p)")] public float extraLoanRateStep = 0.02f;

        // shortcut: 금액·이자율은 팀 결정 전 제안값.
        [Header("긴급 대출 (한 판에 1번, 장 마감 정산 후 현금이 0 밑이면 제안. 거절하면 파산)")]
        [Tooltip("모자란 금액에 더 얹어 주는 돈")] public float emergencyLoanBuffer = 1000f;
        [Tooltip("긴급 대출을 받으면 오르는 이자율 (0.10 = +10%p)")] public float emergencyRateStep = 0.10f;
    }
}
