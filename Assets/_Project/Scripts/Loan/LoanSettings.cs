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

        [Header("추가 대출")]
        public float extraLoanAmount = 5000f;
        public int maxExtraLoans = 3;
        [Tooltip("추가 대출 1회마다 오르는 이자율 (0.02 = +2%p)")] public float extraLoanRateStep = 0.02f;

        [Header("패배")]
        [Tooltip("총자산(현금 + 주식)이 이 값 미만이고 추가 대출을 다 쓰면 파산")] public float bankruptcyThreshold = 100f;
    }
}
