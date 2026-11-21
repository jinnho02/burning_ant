using System;
using BurningAnt.Market;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BurningAnt.Editor
{
    /// <summary>
    /// 매매·가격 생성 로직 자체 점검. 메뉴: BurningAnt > Self Check > Market
    /// 배치 실행: Unity -batchmode -executeMethod BurningAnt.Editor.MarketSelfCheck.RunBatch -quit
    /// </summary>
    public static class MarketSelfCheck
    {
        [MenuItem("BurningAnt/Self Check/Market")]
        public static void Run()
        {
            var settings = ScriptableObject.CreateInstance<MarketSettings>();
            var defs = new StockDefinition[5];
            var stocks = new StockState[5];
            for (int i = 0; i < defs.Length; i++)
            {
                defs[i] = ScriptableObject.CreateInstance<StockDefinition>();
                defs[i].displayName = "S" + i;
                stocks[i] = new StockState(defs[i], 0.1f);
            }

            try
            {
                CheckBuySell(stocks[0]);
                CheckLimits(stocks);
                CheckMaxOrders(stocks[0]);
                CheckLeverage(stocks[0], stocks[1]);
                CheckPricePath(settings);
                Debug.Log("[MarketSelfCheck] 모두 통과");
            }
            finally
            {
                Object.DestroyImmediate(settings);
                foreach (var d in defs) Object.DestroyImmediate(d);
            }
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[MarketSelfCheck] 실패: " + e.Message);
                EditorApplication.Exit(1);
            }
        }

        static void CheckBuySell(StockState s)
        {
            var p = new Portfolio(10000f, 0.002f, 4);
            Expect(p.Buy(s, 10, 100f).Success, "매수 성공");
            Near(p.Cash, 10000f - 1000f * 1.002f, "매수 후 현금 = 금액 + 수수료 0.2% 차감");
            Near(p.GetHolding(s).AveragePrice, 100f, "평단");

            Expect(p.Buy(s, 10, 120f).Success, "추가 매수");
            Near(p.GetHolding(s).AveragePrice, 110f, "추가 매수 후 평단");

            float cashBefore = p.Cash;
            Expect(p.Sell(s, 5, 130f).Success, "일부 매도");
            Near(p.Cash, cashBefore + 650f * 0.998f, "매도 후 현금 = 금액 - 수수료");
            Expect(p.GetHolding(s).Shares == 15, "남은 수량 15");
            Near(p.GetHolding(s).AveragePrice, 110f, "매도해도 평단 유지");

            Expect(p.Sell(s, 16, 130f).Error == TradeError.NotEnoughShares, "보유보다 많이 매도 불가");
            Expect(p.Sell(s, 15, 130f).Success && p.HoldingCount == 0, "전량 매도 시 보유 종목 제거");
            Expect(p.Buy(s, 0, 100f).Error == TradeError.InvalidQuantity, "0주 매수 불가");
            Expect(p.Buy(s, 1000, 100f).Error == TradeError.NotEnoughCash, "현금 부족");
        }

        static void CheckLimits(StockState[] s)
        {
            var p = new Portfolio(10000f, 0f, 4);
            for (int i = 0; i < 4; i++) Expect(p.Buy(s[i], 1, 10f).Success, "4종목까지 매수");
            Expect(p.Buy(s[4], 1, 10f).Error == TradeError.TooManyHoldings, "5번째 종목 매수 불가");
            Expect(p.Buy(s[0], 1, 10f).Success, "이미 보유한 종목은 추가 매수 가능");
            p.BonusHoldings = 1;
            Expect(p.MaxHoldings == 5 && p.Buy(s[4], 1, 10f).Success, "듀얼 모니터: 그날 5종목까지");

            // 영혼의 풀매수(다음 매수 10배) · 악마의 계약서(다음 매도 손익 1.5배)
            var q = new Portfolio(10000f, 0f, 4, 3f);
            q.NextBuyLeverage = 10f;
            Expect(q.Buy(s[0], 10, 100f).Success && Mathf.Approximately(q.GetHolding(s[0]).Leverage, 10f) && q.NextBuyLeverage == 0f, "풀매수: 다음 매수 1회만 10배");
            Expect(q.Buy(s[1], 10, 100f).Success && Mathf.Approximately(q.GetHolding(s[1]).Leverage, 3f), "그다음 매수는 다시 3배");
            q.NextSellMultiplier = 1.5f;
            float before = q.Cash;
            Expect(q.Sell(s[0], 10, 110f).Success, "풀매수 종목 매도");
            Near(q.LastRealized, 100f * 10f * 1.5f, "손익 = 가격 차 100 × 10배 × 1.5");
            Near(q.Cash - before, 1000f + 1500f, "받는 돈 = 넣은 돈 + 손익");
            Expect(Mathf.Approximately(q.NextSellMultiplier, 1f), "악마의 계약서는 매도 1회만");
        }

        static void CheckMaxOrders(StockState s)
        {
            var p = new Portfolio(1000f, 0.002f, 4);
            var r = p.Execute(new OrderRequest(s, OrderSide.Buy, 0, true), 100f);
            Expect(r.Success && r.Shares == 9, "최대 매수 = 수수료 포함 살 수 있는 만큼 (9주)");
            r = p.Execute(new OrderRequest(s, OrderSide.Sell, 0, true), 100f);
            Expect(r.Success && r.Shares == 9 && p.HoldingCount == 0, "최대 매도 = 전량");
            r = p.Execute(new OrderRequest(s, OrderSide.Sell, 0, true), 100f);
            Expect(r.Error == TradeError.NotEnoughShares, "보유 없을 때 최대 매도 실패");
        }

        /// <summary>3배: 손해가 넣은 돈보다 크면 마이너스, 넘친 만큼은 잔고에서 빠진다.</summary>
        static void CheckLeverage(StockState a, StockState b)
        {
            var p = new Portfolio(1000f, 0f, 4, 3f);
            Expect(p.Buy(a, 10, 1f).Success, "3배 매수 ($10)");
            Near(p.Cash, 990f, "넣은 돈만큼 현금이 빠짐");
            Near(p.GetHolding(a).Value(1.2f), 16f, "+20% → $10 + $6 = $16");
            Near(p.GetHolding(a).Value(0.5f), -5f, "$10 넣고 −50% → $30어치가 $15 → 평가금 −$5");
            Expect(p.Sell(a, 10, 0.5f).Success, "마이너스 종목 매도");
            Near(p.Cash, 985f, "마이너스 $5만큼 내 현금이 빠짐");

            var broke = new Portfolio(10f, 0f, 4, 3f);
            broke.Buy(a, 10, 1f);
            Expect(broke.Sell(a, 10, 0.5f).Error == TradeError.NotEnoughCash, "현금으로 마이너스를 못 메우면 팔 수 없음");

            // 장 마감 정산: 마이너스 종목만 정리되고 그만큼 잔고에서 빠짐. 플러스 종목은 그대로
            var q = new Portfolio(20f, 0f, 4, 3f);
            q.Buy(a, 10, 1f);
            q.Buy(b, 10, 1f);
            var closed = new System.Collections.Generic.List<StockState>();
            Near(q.SettleNegatives(s => s == a ? 0.5f : 1.5f, closed), 5f, "정산에서 빠진 금액 $5");
            Near(q.Cash, -5f, "잔고가 0 밑으로 (→ 긴급 대출 아니면 파산)");
            Expect(closed.Count == 1 && closed[0] == a && q.GetHolding(a).Shares == 0, "마이너스 종목만 정리");
            Expect(q.GetHolding(b).Shares == 10, "플러스 종목은 다음 날로");
            Expect(q.Sell(b, 10, 1.5f).Success, "플러스 종목을 팔아 잔고 채우기");
            Near(q.Cash, 20f, "−$5 + $25 = $20");
        }

        static void CheckPricePath(MarketSettings settings)
        {
            var rng = new System.Random(1);
            for (int run = 0; run < 200; run++)
            {
                var path = PricePathGenerator.Generate(100f, 0.25f, settings, rng);
                Expect(path.Length == settings.ticksPerDay + 1, "경로 길이 = 틱 수 + 1");
                foreach (var price in path)
                    Expect(price >= 75f - 1e-3f && price <= 125f + 1e-3f, "가격이 하루 등락 한도 안");
            }
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
