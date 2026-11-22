using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BurningAnt.Loan
{
    /// <summary>
    /// 빚 = 대출 여러 개. 대출마다 원금 · 하루 이자율 · 이자가 붙기 시작하는 날이 따로 있다.
    /// 0번 = 기본 대출(5%, 3일차부터). 안 낸 이자 · 사채업자 명함 · 더 강한 악마의 계약서는 기본 대출에 더한다.
    /// 그 밖: 추가 대출(30%, 다다음 날부터), 러시앤러시·사나머니(20%), 햇살론(30%) (각각 다음 날부터).
    /// 현금 이동은 호출하는 쪽(Portfolio)이 한다.
    /// </summary>
    public class LoanAccount
    {
        public class Loan
        {
            public string Name;
            public float Principal;
            public float Rate;
            public int StartDay;
        }

        readonly LoanSettings settings;
        readonly List<Loan> loans = new();

        public IReadOnlyList<Loan> Loans => loans;
        Loan Base => loans[0];

        public float MainDebt => Base.Principal;
        public float Debt => loans.Sum(l => l.Principal);
        /// <summary>기본 대출 이자율.</summary>
        public float InterestRate => Base.Rate;
        public bool ExtraLoanUsed { get; private set; }
        /// <summary>지금까지 받은 햇살론 횟수 (한 판에 emergencyMaxCount번까지).</summary>
        public int EmergencyCount { get; private set; }
        public bool EmergencyUsed => EmergencyCount >= settings.emergencyMaxCount;
        public int EmergencyLeft => Mathf.Max(0, settings.emergencyMaxCount - EmergencyCount);
        /// <summary>신용등급 세탁기: 다음 대출 1회(기본 대출 제외)의 이자율 절반.</summary>
        public bool HalveNextRate { get; set; }
        /// <summary>지금까지 갚은 금액 합계 (빚 상환 게이지용, 이자 포함).</summary>
        public float TotalRepaid { get; private set; }

        public bool IsPaidOff => Debt <= 0.5f;
        public bool CanTakeExtraLoan => !ExtraLoanUsed;

        public LoanAccount(LoanSettings settings)
        {
            this.settings = settings;
            loans.Add(new Loan { Name = "기본 대출", Principal = settings.startingDebt, Rate = settings.baseInterestRate, StartDay = settings.interestStartDay });
        }

        IEnumerable<Loan> ChargedOn(int day) => loans.Where(l => l.Principal > 0.5f && day >= l.StartDay);

        /// <summary>그날 장 결과에서 낼 이자 = 이자가 붙기 시작한 대출들의 원금 × 이자율 합.</summary>
        public float InterestDue(int day) => ChargedOn(day).Sum(l => l.Principal * l.Rate);

        /// <summary>그날 이자가 붙는 이자율들 (화면 표시용, 높은 순).</summary>
        public IEnumerable<float> RatesOn(int day) => ChargedOn(day).Select(l => l.Rate).Distinct().OrderByDescending(r => r);

        /// <summary>안 낸 이자는 기본 대출에 붙는다.</summary>
        public void AddUnpaidInterest(float amount)
        {
            if (amount > 0f) Base.Principal += amount;
        }

        /// <summary>이자를 냈다 (상환 게이지에만 더함).</summary>
        public void RecordInterestPaid(float amount)
        {
            if (amount > 0f) TotalRepaid += amount;
        }

        /// <summary>새 대출 하나. 세탁기가 걸려 있으면 이자율 절반. 실제로 붙은 이자율을 돌려준다.</summary>
        public float AddLoan(string name, float amount, float rate, int startDay)
        {
            if (amount <= 0f) return 0f;
            if (HalveNextRate)
            {
                rate *= 0.5f;
                HalveNextRate = false;
            }
            loans.Add(new Loan { Name = name, Principal = amount, Rate = rate, StartDay = startDay });
            return rate;
        }

        /// <summary>추가 대출 (한 판에 1번, 최대 extraLoanMax, 다다음 날부터 이자). 성공하면 빌린 금액, 실패하면 0.</summary>
        public float TakeExtraLoan(float amount, int day)
        {
            amount = Mathf.Min(amount, settings.extraLoanMax);
            if (ExtraLoanUsed || amount <= 0f) return 0f;
            ExtraLoanUsed = true;
            AddLoan("추가 대출", amount, settings.extraLoanRate, day + settings.extraLoanGraceDays);
            return amount;
        }

        /// <summary>햇살론 1번 금액 (모자란 금액 + 여유분, 최대 emergencyLoanMax). 남은 횟수가 없으면 0. 모자란 돈이 한도보다 크면 한도만큼.</summary>
        public float EmergencyAmount(float shortfall)
        {
            if (EmergencyUsed || shortfall <= 0f) return 0f;
            return Mathf.Min(shortfall + settings.emergencyLoanBuffer, settings.emergencyLoanMax);
        }

        /// <summary>햇살론 (한 판에 emergencyMaxCount번, 파산 직전에만). 다음 날부터 이자. 실패하면 0.</summary>
        public float TakeEmergencyLoan(float shortfall, int day = 0)
        {
            float amount = EmergencyAmount(shortfall);
            if (amount <= 0f) return 0f;
            EmergencyCount++;
            AddLoan("햇살론", amount, settings.emergencyRate, day + 1);
            return amount;
        }

        /// <summary>기본 대출 원금을 늘린다 (사채업자 명함, 더 강한 악마의 계약서).</summary>
        public void AddDebt(float amount)
        {
            if (amount > 0f) Base.Principal += amount;
        }

        /// <summary>채무 조정: 기본 대출 원금을 ratio만큼 줄인다. 줄인 금액을 돌려준다.</summary>
        public float ReduceBasePrincipal(float ratio)
        {
            float cut = Base.Principal * Mathf.Clamp01(ratio);
            Base.Principal -= cut;
            return cut;
        }

        /// <summary>최대 amount만큼 갚고 실제로 갚은 금액을 돌려준다. 이자율 높은 대출부터 갚는다.</summary>
        public float Repay(float amount)
        {
            float left = Mathf.Clamp(amount, 0f, Debt);
            float paid = left;
            foreach (var l in loans.OrderByDescending(x => x.Rate))
            {
                float p = Mathf.Min(left, l.Principal);
                l.Principal -= p;
                left -= p;
                if (l.Principal < 0.5f) l.Principal = 0f;
            }
            var basis = Base;
            loans.RemoveAll(l => l != basis && l.Principal <= 0f); // 다 갚은 대출은 지운다 (기본 대출은 0원으로 남김)
            TotalRepaid += paid;
            return paid;
        }
    }
}
