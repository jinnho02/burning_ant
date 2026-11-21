using System;
using System.Linq;
using BurningAnt.Loan;
using BurningAnt.Market;
using BurningAnt.News;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BurningAnt.Editor
{
    /// <summary>
    /// 대출·이자·뉴스 로직 자체 점검. 메뉴: BurningAnt > Self Check > Day Loop
    /// </summary>
    public static class DayLoopSelfCheck
    {
        [MenuItem("BurningAnt/Self Check/Day Loop")]
        public static void Run()
        {
            var loanSettings = ScriptableObject.CreateInstance<LoanSettings>();
            var newsSettings = ScriptableObject.CreateInstance<NewsSettings>();
            var market = ScriptableObject.CreateInstance<MarketSettings>();
            var defs = new StockDefinition[8];
            var stocks = new StockState[8];
            for (int i = 0; i < defs.Length; i++)
            {
                defs[i] = ScriptableObject.CreateInstance<StockDefinition>();
                defs[i].displayName = "S" + i;
                defs[i].startPrice = 100f;
                stocks[i] = new StockState(defs[i], i < 2 ? 0.05f : i < 5 ? 0.12f : 0.25f);
            }

            try
            {
                CheckLoan(loanSettings);
                CheckNews(newsSettings, market, stocks);
                Debug.Log("[DayLoopSelfCheck] 모두 통과");
            }
            finally
            {
                Object.DestroyImmediate(loanSettings);
                Object.DestroyImmediate(newsSettings);
                Object.DestroyImmediate(market);
                foreach (var d in defs) Object.DestroyImmediate(d);
            }
        }

        static void CheckLoan(LoanSettings settings)
        {
            var loan = new LoanAccount(settings);
            Near(loan.Debt, 10000f, "시작 빚 $10,000");
            Expect(!loan.IsInterestDay(6) && loan.IsInterestDay(7) && loan.IsInterestDay(14), "이자일 = 7의 배수");
            Expect(loan.NextInterestDay(1) == 7 && loan.NextInterestDay(7) == 7 && loan.NextInterestDay(8) == 14, "다음 이자일");

            Near(loan.ApplyInterest(), 500f, "이자 5% = $500");
            Near(loan.Debt, 10500f, "이자 후 빚");

            Near(loan.TakeExtraLoan(), 5000f, "추가 대출 $5,000");
            Near(loan.InterestRate, 0.07f, "추가 대출 1회 후 이자율 7%");
            loan.TakeExtraLoan();
            loan.TakeExtraLoan();
            Near(loan.InterestRate, 0.11f, "3회 후 이자율 11%");
            Expect(!loan.CanTakeExtraLoan && loan.TakeExtraLoan() == 0f, "4번째 추가 대출 불가");

            Near(loan.Repay(1000f), 1000f, "일부 상환");
            Near(loan.Repay(1e9f), 24500f, "빚보다 많이 갚으면 남은 빚만");
            Near(loan.TotalRepaid, 25500f, "갚은 금액 합계 (상환 게이지)");
            Expect(loan.IsPaidOff, "전액 상환");

            var rescue = new LoanAccount(settings);
            Near(rescue.TakeEmergencyLoan(800f), 800f + settings.emergencyLoanBuffer, "긴급 대출 = 모자란 금액 + 여유분");
            Near(rescue.InterestRate, settings.baseInterestRate + settings.emergencyRateStep, "긴급 대출 후 이자율 크게 오름");
            Expect(rescue.EmergencyUsed && rescue.TakeEmergencyLoan(500f) == 0f, "긴급 대출은 한 판에 한 번");
        }

        static void CheckNews(NewsSettings settings, MarketSettings market, StockState[] stocks)
        {
            var gen = new NewsGenerator(settings, new System.Random(3));
            bool sawChain = false;
            for (int day = 1; day <= 300; day++)
            {
                var chainBefore = gen.ActiveChain;
                var news = gen.GenerateDay(day, stocks);
                Expect(news.Count >= 2 && news.Count <= 3, "하루 뉴스 2~3개");
                Expect(news.Select(n => n.Stock).Distinct().Count() == news.Count, "뉴스마다 다른 종목");

                if (chainBefore != null)
                {
                    sawChain = true;
                    var result = news.FirstOrDefault(n => n.Kind is NewsKind.ChainSuccess or NewsKind.ChainFail);
                    Expect(result != null && result.Stock == chainBefore.Stock, "연쇄 뉴스 결과가 다음 날 발표");
                    Expect((result.Kind == NewsKind.ChainSuccess) == chainBefore.WillSucceed, "발표 결과 = 미리 정해진 결과");
                    Expect(chainBefore.WillSucceed ? result.Effect > 0f : result.Effect < 0f, "성공은 상승, 실패는 하락");
                }

                foreach (var n in news.Where(n => n.Kind == NewsKind.Normal))
                    Expect(Mathf.Abs(n.Effect) >= 0.01f - 1e-4f && Mathf.Abs(n.Effect) <= 0.04f + 1e-4f, "일반 뉴스 폭 1~4%");

                gen.ApplyToPrices(stocks, news, market);
                foreach (var s in stocks)
                {
                    Expect(s.Path.Length == market.ticksPerDay + 1, "경로 길이");
                    foreach (var p in s.Path)
                        Expect(p >= s.LowerLimit - 1e-3f && p <= s.UpperLimit + 1e-3f, "가격이 하루 등락 한도 안");
                    s.CloseDay();
                }
            }
            Expect(sawChain, "300일 동안 연쇄 뉴스가 한 번은 나옴");
        }

        static void Expect(bool condition, string what)
        {
            if (!condition) throw new Exception(what);
        }

        static void Near(float actual, float expected, string what)
        {
            if (Mathf.Abs(actual - expected) > 0.01f) throw new Exception($"{what}: 기대 {expected}, 실제 {actual}");
        }
    }
}
