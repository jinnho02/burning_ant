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
            Won(loan.InterestDue(3), 5_000_000f, "3일차부터 매일 기본 대출 이자 5% = 500만");

            Won(loan.TakeExtraLoan(3), 200_000_000f, "추가 대출은 1번에 2억 고정");
            Expect(loan.CanTakeExtraLoan && loan.ExtraLoanLeft == 1, "추가 대출은 한 판에 2번 (1번 남음)");
            Won(loan.InterestDue(4), 5_000_000f, "추가 대출 다음 날은 추가 대출 이자 없음");
            Won(loan.InterestDue(5), 5_000_000f + 60_000_000f, "다다음 날부터 추가 대출 이자 30%");

            loan.HalveNextRate = true;
            Near(loan.AddLoan("콩대출", 100_000_000f, 0.2f, 4), 0.1f, "신용등급 세탁기: 다음 대출 이자율 절반 (20% → 10%)");
            Expect(!loan.HalveNextRate, "세탁기는 한 번 쓰면 끝");
            Won(loan.InterestDue(5), 5_000_000f + 60_000_000f + 10_000_000f, "대출마다 이자율 따로 (5% + 30% + 10%)");
            Expect(loan.RatesOn(5).SequenceEqual(new[] { 0.3f, 0.1f, 0.05f }), "오늘 이자율 목록 (높은 순)");

            loan.AddUnpaidInterest(5_000_000f);
            Won(loan.MainDebt, 105_000_000f, "안 낸 이자는 기본 대출에 붙음");
            Won(loan.Repay(250_000_000f), 250_000_000f, "일부 상환");
            Expect(loan.Loans.Count == 2 && loan.Loans[1].Name == "콩대출", "이자 높은 추가 대출(30%)부터 갚고, 다 갚은 대출은 지움");
            Won(loan.Debt, 155_000_000f, "남은 빚 = 콩대출 일부 + 기본 대출");
            Won(loan.Repay(1e12f), 155_000_000f, "빚보다 많이 갚으면 남은 빚만");
            Won(loan.TotalRepaid, 405_000_000f, "갚은 금액 합계 (상환 게이지)");
            Expect(loan.IsPaidOff && loan.InterestDue(9) == 0f, "전액 상환하면 이자 없음");

            var adj = new LoanAccount(settings);
            Won(adj.ReduceBasePrincipal(0.01f), 1_000_000f, "채무 조정: 기본 대출 원금 1%");
            adj.AddDebt(30_000_000f);
            Won(adj.MainDebt, 129_000_000f, "사채업자 명함: 기본 대출 원금 증가");

            var rescue = new LoanAccount(settings);
            Won(rescue.EmergencyAmount(settings.emergencyLoanMax + 1_000_000f), settings.emergencyLoanMax, "모자란 돈이 한도보다 크면 1번에 최대 2억");
            Won(rescue.TakeEmergencyLoan(8_000_000f, 4), 8_000_000f + settings.emergencyLoanBuffer, "햇살론 = 모자란 금액 + 여유분");
            Near(rescue.Loans[1].Rate, settings.emergencyRate, "햇살론 이자율 30% (기본 대출과 따로)");
            Expect(rescue.Loans[1].StartDay == 5 && Mathf.Approximately(rescue.InterestRate, settings.baseInterestRate), "햇살론 이자는 다음 날부터, 기본 대출 이자율은 그대로");
            Expect(!rescue.EmergencyUsed && rescue.EmergencyLeft == 1, "햇살론은 한 판에 2번 (1번 남음)");
            Expect(rescue.TakeEmergencyLoan(500f) > 0f && rescue.EmergencyUsed && rescue.TakeEmergencyLoan(500f) == 0f, "햇살론 3번째는 안 됨");
            var big = new LoanAccount(settings);
            Won(big.TakeEmergencyLoan(195_000_000f), settings.emergencyLoanMax, "햇살론은 최대 2억");
        }

        static void CheckNews(NewsSettings settings, MarketSettings market, StockState[] stocks)
        {
            var gen = new NewsGenerator(settings, new System.Random(3));
            int ticks = market.ticksPerDay;
            bool sawIntraday = false, sawSteps = false, sawGradual = false;
            int early = Mathf.Clamp(Mathf.RoundToInt(ticks * settings.earliestReveal), 1, ticks);
            int firstHalf = 0, total = 0;
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
                    Expect(n.RevealTick >= early && n.RevealTick <= ticks, "결과 시점은 장 전체 (첫 틱~마감)");
                    Expect(n.RevealAtClose == (n.RevealTick == ticks), "마지막 틱이면 마감 발표");
                    if (!n.RevealAtClose) sawIntraday = true;
                    total++;
                    if (n.RevealTick <= (early + ticks) / 2) firstHalf++;
                }

                gen.ApplyToPrices(stocks, news, market);
                foreach (var s in stocks)
                {
                    Expect(s.Path.Length == ticks + 1, "경로 길이");
                    var n = news.FirstOrDefault(x => x.Stock == s);
                    int until = n != null ? n.RevealTick - 1 : ticks; // 뉴스 종목은 결과 전까지만 구간 안
                    for (int i = 0; i <= until; i++)
                        Expect(s.Path[i] >= s.LowerLimit - 1e-3f && s.Path[i] <= s.UpperLimit + 1e-3f, "평소엔 기본 구간 안 (전날 종가 기준)");
                    if (n != null)
                    {
                        Expect(n.SettleTick >= n.RevealTick && n.SettleTick <= ticks, "결과 값에 닿는 시점은 속보 뒤, 장 마감 전");
                        Near(s.Path[n.SettleTick] / s.PrevClose - 1f, n.Effect, "결과 값에 다 닿는 틱에 템플릿 값으로 고정");
                        if (n.Steps == 1) Expect(n.SettleTick == n.RevealTick, "한 번에 뛰는 경우는 속보 틱에 바로");
                        else if (n.Steps >= 2) sawSteps = true;
                        else sawGradual = true;
                    }
                    s.CloseDay();
                }
            }
            Expect(sawIntraday, "장 중 특보가 나옴");
            Expect(sawSteps && sawGradual, "한 번에 · 계단식 · 차근차근 움직임이 모두 나옴");
            // 균등 분포: 구간 앞쪽 절반에 대략 절반 (1200개 중 40~60%)
            Expect(firstHalf > total * 0.4f && firstHalf < total * 0.6f, "결과 시점이 구간에 고르게 퍼짐");

            // 예지: 뉴스 없던 종목에 템플릿을 하나 꺼내 경로를 그 값으로 고정
            var day1 = gen.GenerateDay(301, stocks, ticks);
            gen.ApplyToPrices(stocks, day1, market);
            var quiet = stocks.First(s => day1.All(n => n.Stock != s));
            var seen = gen.CreateForesight(quiet, market);
            Expect(seen != null && seen.Foreseen && seen.Kind == NewsKind.Foresight, "예지 템플릿 생성");
            Expect(!day1.Any(n => n.Template == seen.Template), "예지 템플릿도 오늘 뉴스와 겹치지 않음");
            Near(quiet.Path[seen.SettleTick] / quiet.PrevClose - 1f, seen.Effect, "예지한 값으로 경로 고정");
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
