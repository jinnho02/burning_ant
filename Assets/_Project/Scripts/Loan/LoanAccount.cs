using UnityEngine;

namespace BurningAnt.Loan
{
    /// <summary>남은 빚, 이자율, 추가 대출 횟수. 현금 이동은 호출하는 쪽(Portfolio)이 한다.</summary>
    public class LoanAccount
    {
        readonly LoanSettings settings;

        public float Debt { get; private set; }
        public float InterestRate { get; private set; }
        public int ExtraLoansTaken { get; private set; }
        /// <summary>지금까지 갚은 금액 합계 (빚 상환 게이지용).</summary>
        public float TotalRepaid { get; private set; }

        public bool IsPaidOff => Debt <= 0.005f;
        public bool CanTakeExtraLoan => ExtraLoansTaken < settings.maxExtraLoans;
        public int ExtraLoansLeft => settings.maxExtraLoans - ExtraLoansTaken;

        public LoanAccount(LoanSettings settings)
        {
            this.settings = settings;
            Debt = settings.startingDebt;
            InterestRate = settings.baseInterestRate;
        }

        public bool IsInterestDay(int day) => settings.interestPeriodDays > 0 && day % settings.interestPeriodDays == 0;

        /// <summary>다음 이자가 붙는 날 (오늘 포함).</summary>
        public int NextInterestDay(int day)
        {
            int period = Mathf.Max(1, settings.interestPeriodDays);
            return ((day + period - 1) / period) * period;
        }

        /// <summary>남은 빚에 이자를 더하고 더한 금액을 돌려준다.</summary>
        public float ApplyInterest()
        {
            float interest = Debt * InterestRate;
            Debt += interest;
            return interest;
        }

        /// <summary>추가 대출. 성공하면 빌린 금액, 실패하면 0.</summary>
        public float TakeExtraLoan()
        {
            if (!CanTakeExtraLoan) return 0f;
            ExtraLoansTaken++;
            Debt += settings.extraLoanAmount;
            InterestRate += settings.extraLoanRateStep;
            return settings.extraLoanAmount;
        }

        public bool EmergencyUsed { get; private set; }

        /// <summary>긴급 대출 (한 판에 1번). 모자란 금액 + 여유분을 빌려주고 이자율이 크게 오른다. 실패하면 0.</summary>
        public float TakeEmergencyLoan(float shortfall)
        {
            if (EmergencyUsed || shortfall <= 0f) return 0f;
            EmergencyUsed = true;
            float amount = shortfall + settings.emergencyLoanBuffer;
            Debt += amount;
            InterestRate += settings.emergencyRateStep;
            return amount;
        }

        /// <summary>최대 amount만큼 갚고 실제로 갚은 금액을 돌려준다.</summary>
        public float Repay(float amount)
        {
            float paid = Mathf.Clamp(amount, 0f, Debt);
            Debt -= paid;
            TotalRepaid += paid;
            if (Debt < 0.005f) Debt = 0f;
            return paid;
        }
    }
}
