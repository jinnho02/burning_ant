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
            // 업종 5개 × 4종목, 기본 구간 10~50%, 업종마다 기본 기사 템플릿 10개
            var sectors = (Sector[])Enum.GetValues(typeof(Sector));
            var defs = new StockDefinition[sectors.Length * 4];
            var stocks = new StockState[defs.Length];
            for (int i = 0; i < defs.Length; i++)
            {
                defs[i] = ScriptableObject.CreateInstance<StockDefinition>();
                defs[i].displayName = "S" + i;
                defs[i].startPrice = 100f;
                defs[i].sector = sectors[i / 4];
                defs[i].baseRange = 0.1f + 0.1f * (i % 5);
                stocks[i] = new StockState(defs[i], defs[i].baseRange);
            }
            var library = sectors.Select(sec =>
            {
                var l = ScriptableObject.CreateInstance<SectorNews>();
                l.sector = sec;
                l.templates = DefaultNewsData.Templates[sec].Select(t => new NewsTemplate
                {
                    initialHeadline = t.initial, successHeadline = t.success, successPercent = t.successPercent,
                    failureHeadline = t.failure, failurePercent = t.failurePercent,
                }).ToList();
                return l;
            }).ToList();
            newsSettings.library = library;

            try
            {
                CheckMood();
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
                foreach (var l in library) Object.DestroyImmediate(l);
            }
        }

        static void CheckMood()
        {
            var c = ScriptableObject.CreateInstance<BurningAnt.Character.CharacterSettings>();
            try
            {
                (float r, BurningAnt.Character.Mood m)[] cases =
                {
                    (-0.5f, BurningAnt.Character.Mood.Crash), (-0.30f, BurningAnt.Character.Mood.Crash), (-0.25f, BurningAnt.Character.Mood.Bad),
                    (-0.10f, BurningAnt.Character.Mood.Loss), (-0.09f, BurningAnt.Character.Mood.Flat), (0f, BurningAnt.Character.Mood.Flat),
                    (0.099f, BurningAnt.Character.Mood.Flat), (0.10f, BurningAnt.Character.Mood.Gain), (0.2f, BurningAnt.Character.Mood.Great),
                    (0.3f, BurningAnt.Character.Mood.Jackpot), (1.5f, BurningAnt.Character.Mood.Jackpot),
                };
                foreach (var (r, m) in cases) Expect(c.GetMood(r) == m, $"표정 경계 {r:P0} → {m}");
            }
            finally { Object.DestroyImmediate(c); }
        }

        static void CheckLoan(LoanSettings settings)
        {
            var loan = new LoanAccount(settings);
            // 금액은 만 원 단위로 비교 (float이라 억 단위에서 몇 원 오차)
            void Won(float actual, float expected, string what) => Near(actual / 10000f, expected / 10000f, what);

            Won(loan.Debt, 100_000_000f, "시작 빚 1억");
            Expect(loan.InterestDue(1) == 0f && loan.InterestDue(2) == 0f, "1·2일차는 이자 없음");
            Won(loan.InterestDue(3), 5_000_000f, "3일차부터 매일 이자 5% = 500만");

            Won(loan.TakeExtraLoan(500_000_000f, 3), 200_000_000f, "추가 대출은 최대 2억");
            Expect(!loan.CanTakeExtraLoan && loan.TakeExtraLoan(1f, 3) == 0f, "추가 대출은 한 번만");
            Won(loan.InterestDue(4), 5_000_000f, "추가 대출 다음 날은 추가 대출 이자 없음");
            Won(loan.InterestDue(5), 5_000_000f + 60_000_000f, "다다음 날부터 추가 대출 이자 30%");

            loan.AddUnpaidInterest(5_000_000f);
            Won(loan.MainDebt, 105_000_000f, "안 낸 이자는 빚에 붙음");
            Won(loan.Repay(50_000_000f), 50_000_000f, "일부 상환");
            Won(loan.ExtraDebt, 150_000_000f, "이자 높은 추가 대출부터 갚음");
            Won(loan.Repay(1e12f), 255_000_000f, "빚보다 많이 갚으면 남은 빚만");
            Won(loan.TotalRepaid, 305_000_000f, "갚은 금액 합계 (상환 게이지)");
            Expect(loan.IsPaidOff && loan.InterestDue(9) == 0f, "전액 상환하면 이자 없음");

            var rescue = new LoanAccount(settings);
            Expect(rescue.EmergencyAmount(settings.emergencyLoanMax + 1_000_000f) == 0f, "모자란 돈이 햇살론 한도(2억)를 넘으면 못 받음");
            Won(rescue.TakeEmergencyLoan(8_000_000f), 8_000_000f + settings.emergencyLoanBuffer, "햇살론 = 모자란 금액 + 여유분");
            Near(rescue.InterestRate, settings.baseInterestRate + settings.emergencyRateStep, "햇살론 후 이자율 크게 오름");
            Expect(rescue.EmergencyUsed && rescue.TakeEmergencyLoan(500f) == 0f, "햇살론은 한 판에 한 번");
            var big = new LoanAccount(settings);
            Won(big.TakeEmergencyLoan(195_000_000f), settings.emergencyLoanMax, "햇살론은 최대 2억");
        }

        static void CheckNews(NewsSettings settings, MarketSettings market, StockState[] stocks)
        {
            var gen = new NewsGenerator(settings, new System.Random(3));
            int ticks = market.ticksPerDay;
            bool sawIntraday = false, sawClose = false;
            for (int day = 1; day <= 300; day++)
            {
                var news = gen.GenerateDay(day, stocks, ticks);
                Expect(news.Count == settings.newsPerDay, "하루 뉴스 = 4종목");
                Expect(news.Select(n => n.Stock).Distinct().Count() == news.Count, "뉴스마다 다른 종목");
                Expect(news.Select(n => n.Template).Distinct().Count() == news.Count, "같은 날 같은 기사 두 번 안 씀");

                foreach (var n in news)
                {
                    Expect(settings.library.First(l => l.sector == n.Stock.Definition.sector).templates.Contains(n.Template), "그 종목 업종의 템플릿");
                    float percent = (n.Success ? n.Template.successPercent : n.Template.failurePercent) / 100f;
                    float ratio = Mathf.Abs(n.Effect) / n.Stock.DailyLimit;
                    Expect(n.Effect <= -0.95f + 1e-4f || (ratio >= 1.5f - 1e-3f && ratio <= 3f + 1e-3f), "뉴스 변동 폭 = 기본 구간보다 50~200% 큼");
                    Expect(Mathf.Abs(n.Effect) > n.Stock.DailyLimit, "떡상·떡락은 기본 구간보다 큼");
                    Expect(n.Success ? n.Effect > 0f : n.Effect < 0f, "성공은 상승, 실패는 하락");
                    if (n.RevealAtClose) { sawClose = true; Expect(n.RevealTick == ticks, "마감 발표는 마지막 틱"); }
                    else { sawIntraday = true; Expect(n.RevealTick > 0 && n.RevealTick < ticks, "특보는 장 중"); }
                }

                gen.ApplyToPrices(stocks, news, market);
                foreach (var s in stocks)
                {
                    Expect(s.Path.Length == ticks + 1, "경로 길이");
                    var n = news.FirstOrDefault(x => x.Stock == s);
                    int until = n != null ? n.RevealTick - 1 : ticks; // 뉴스 종목은 결과 전까지만 구간 안
                    for (int i = 0; i <= until; i++)
                        Expect(s.Path[i] >= s.LowerLimit - 1e-3f && s.Path[i] <= s.UpperLimit + 1e-3f, "평소엔 기본 구간 안 (전날 종가 기준)");
                    if (n != null) Near(s.Path[n.RevealTick] / s.PrevClose - 1f, n.Effect, "결과 시점에 템플릿 값으로 고정");
                    s.CloseDay();
                }
            }
            Expect(sawIntraday && sawClose, "특보와 마감 발표가 둘 다 나옴");

            // 예지: 뉴스 없던 종목에 템플릿을 하나 꺼내 경로를 그 값으로 고정
            var day1 = gen.GenerateDay(301, stocks, ticks);
            gen.ApplyToPrices(stocks, day1, market);
            var quiet = stocks.First(s => day1.All(n => n.Stock != s));
            var seen = gen.CreateForesight(quiet, market);
            Expect(seen != null && seen.Foreseen && seen.Kind == NewsKind.Foresight, "예지 템플릿 생성");
            Expect(!day1.Any(n => n.Template == seen.Template), "예지 템플릿도 오늘 뉴스와 겹치지 않음");
            Near(quiet.Path[seen.RevealTick] / quiet.PrevClose - 1f, seen.Effect, "예지한 값으로 경로 고정");
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
