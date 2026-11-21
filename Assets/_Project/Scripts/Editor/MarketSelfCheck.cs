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
