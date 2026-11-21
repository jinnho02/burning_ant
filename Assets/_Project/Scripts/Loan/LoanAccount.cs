using UnityEngine;

namespace BurningAnt.Loan
{
    /// <summary>
    /// 빚 = 일반 대출(시작 1억 + 햇살론 + 안 낸 이자 + 악마의 계약서) + 추가 대출(30%, 다다음 날부터 이자).
    /// 현금 이동은 호출하는 쪽(Portfolio)이 한다.
    /// </summary>
    public class LoanAccount
    {
        readonly LoanSettings settings;

        public float MainDebt { get; private set; }
        public float ExtraDebt { get; private set; }
        public float Debt => MainDebt + ExtraDebt;
        public float InterestRate { get; private set; }
        public float ExtraRate => settings.extraLoanRate;
        public bool ExtraLoanUsed { get; private set; }
        public int ExtraLoanDay { get; private set; }
        /// <summary>지금까지 갚은 금액 합계 (빚 상환 게이지용, 이자 포함).</summary>
        public float TotalRepaid { get; private set; }
        public bool EmergencyUsed { get; private set; }

        public bool IsPaidOff => Debt <= 0.5f;
        public bool CanTakeExtraLoan => !ExtraLoanUsed;

        public LoanAccount(LoanSettings settings)
        {
            this.settings = settings;
            MainDebt = settings.startingDebt;
            InterestRate = settings.baseInterestRate;
        }

        public bool IsInterestDay(int day) => day >= settings.interestStartDay;

        /// <summary>그날 장 결과에서 낼 이자. 추가 대출은 빌린 날 + 2일부터.</summary>
        public float InterestDue(int day)
        {
            if (!IsInterestDay(day)) return 0f;
            float due = MainDebt * InterestRate;
            if (ExtraLoanUsed && day >= ExtraLoanDay + settings.extraLoanGraceDays) due += ExtraDebt * settings.extraLoanRate;
            return due;
        }

        /// <summary>안 낸 이자는 일반 빚에 붙는다.</summary>
        public void AddUnpaidInterest(float amount)
        {
            if (amount > 0f) MainDebt += amount;
        }

        /// <summary>이자를 냈다 (상환 게이지에만 더함).</summary>
        public void RecordInterestPaid(float amount)
        {
            if (amount > 0f) TotalRepaid += amount;
        }

        /// <summary>추가 대출 (한 판에 1번, 최대 extraLoanMax). 성공하면 빌린 금액, 실패하면 0.</summary>
        public float TakeExtraLoan(float amount, int day)
        {
            amount = Mathf.Min(amount, settings.extraLoanMax);
            if (ExtraLoanUsed || amount <= 0f) return 0f;
            ExtraLoanUsed = true;
            ExtraLoanDay = day;
            ExtraDebt += amount;
            return amount;
        }

        /// <summary>햇살론에 필요한 금액 (모자란 금액 + 여유분, 최대 emergencyLoanMax). 모자란 금액이 한도를 넘으면 0.</summary>
        public float EmergencyAmount(float shortfall)
        {
            if (EmergencyUsed || shortfall <= 0f || shortfall > settings.emergencyLoanMax) return 0f;
            return Mathf.Min(shortfall + settings.emergencyLoanBuffer, settings.emergencyLoanMax);
        }

        /// <summary>햇살론 (한 판에 1번). 실패하면 0.</summary>
        public float TakeEmergencyLoan(float shortfall)
        {
            float amount = EmergencyAmount(shortfall);
            if (amount <= 0f) return 0f;
            EmergencyUsed = true;
            MainDebt += amount;
            InterestRate += settings.emergencyRateStep;
            return amount;
        }

        /// <summary>악마의 계약서: 빚을 늘린다.</summary>
        public void AddDebt(float amount)
        {
            if (amount > 0f) MainDebt += amount;
        }

        /// <summary>채무 조정: 이자율을 내린다 (시작 이자율 밑으로는 안 내려감). 실제로 내린 폭을 돌려준다.</summary>
        public float ReduceRate(float step)
        {
            float before = InterestRate;
            InterestRate = Mathf.Max(settings.baseInterestRate, InterestRate - step);
            return before - InterestRate;
        }

        /// <summary>햇살론(회생): 이미 쓴 긴급 대출 기회를 되살린다. 아직 안 썼으면 false.</summary>
        public bool RestoreEmergency()
        {
            if (!EmergencyUsed) return false;
            EmergencyUsed = false;
            return true;
        }

        /// <summary>최대 amount만큼 갚고 실제로 갚은 금액을 돌려준다. 이자 높은 추가 대출부터 갚는다.</summary>
        public float Repay(float amount)
        {
            float paid = Mathf.Clamp(amount, 0f, Debt);
            float toExtra = Mathf.Min(paid, ExtraDebt);
            ExtraDebt -= toExtra;
            MainDebt -= paid - toExtra;
            TotalRepaid += paid;
            if (ExtraDebt < 0.5f) ExtraDebt = 0f;
            if (MainDebt < 0.5f) MainDebt = 0f;
            return paid;
        }
    }
}
