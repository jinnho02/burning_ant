using System;
using System.Linq;
using System.Reflection;
using BurningAnt.Core;
using BurningAnt.Items;
using BurningAnt.Market;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BurningAnt.Editor
{
    /// <summary>
    /// 화면 없이 DayLoop 씬의 MarketSession + GameLoop를 그대로 돌려 하루 루프를 여러 날 반복해 본다.
    /// 뉴스 → 아이템(장 전 전부 써 봄) → 예약 → 장(중간에 장 중 아이템) → 정산(긴급 대출) → 상환 → 상점 구매 → 다음 날.
    /// 예외가 나거나 단계가 막히면 실패. 메뉴: BurningAnt > Self Check > Full Loop
    /// </summary>
    public static class FullLoopSelfCheck
    {
        const BindingFlags Any = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        [MenuItem("BurningAnt/Self Check/Full Loop")]
        public static void Run() => Run(30, 1, false);

        public static void RunBatch()
        {
            try
            {
                for (int seed = 1; seed <= 5; seed++) Run(40, seed, false);
                Run(5, 7, true);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[FullLoopSelfCheck] 실패: " + e);
                EditorApplication.Exit(1);
            }
        }

        static void Run(int maxDays, int seed, bool forceWin)
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/DayLoop.unity", OpenSceneMode.Single);
            var sceneSession = Object.FindFirstObjectByType<MarketSession>();
            var sceneLoop = Object.FindFirstObjectByType<GameLoop>();
            Expect(sceneSession != null && sceneLoop != null, "DayLoop 씬에 MarketSession, GameLoop");

            // 씬 오브젝트를 복제해 설정(에셋 참조)을 그대로 쓴다
            var go = new GameObject("FullLoopTest");
            try
            {
                var session = go.AddComponent<MarketSession>();
                EditorUtility.CopySerialized(sceneSession, session);
                SetField(session, "seed", seed);
                var loop = go.AddComponent<GameLoop>();
                EditorUtility.CopySerialized(sceneLoop, loop);
                SetField(loop, "seed", seed);
                SetField(loop, "session", session);

                Call(session, "Awake");
                Call(loop, "Awake");
                Call(loop, "Start");

                var items = loop.ShopItems.Where(i => i != null).ToList();
                Expect(items.Count >= 8, "상점 아이템 8종 이상");
                int days = 0, itemUses = 0;
                var rng = new System.Random(seed);

                while (loop.Phase != GamePhase.GameOver && days < maxDays)
                {
                    days++;
                    Expect(loop.Phase == GamePhase.News, $"{days}일차 시작은 뉴스 단계");
                    Expect(loop.TodayNews.Count > 0, "뉴스가 나온다");
                    loop.ConfirmNews();
                    Expect(loop.Phase == GamePhase.Prep, "뉴스 확인 → 2단계");

                    // 장 전 아이템: 가진 것 하나씩 써 본다
                    foreach (var it in loop.Inventory.Owned.Select(o => o.item).Where(i => i.Timing != ItemTiming.Market).ToList())
                    {
                        loop.UseItem(it, session.Stocks[rng.Next(session.Stocks.Count)]);
                        itemUses++;
                    }
                    if (days == 2 && loop.Loan.CanTakeExtraLoan) loop.TakeExtraLoan();

                    // 예약: 무작위 2종목 매수
                    foreach (var s in session.Stocks.OrderBy(_ => rng.Next()).Take(2))
                        session.Reserve(new OrderRequest(s, OrderSide.Buy, Math.Max(1, (int)(session.Portfolio.Cash / 4f / Math.Max(1f, s.PrevClose))), false));
                    session.StartMarket();
                    Expect(loop.Phase == GamePhase.Market, "장 시작 → 3단계");

                    // 장 절반 진행 → 장 중 아이템 → 일부 매매 → 나머지
                    Advance(session, session.Settings.ticksPerDay / 2);
                    foreach (var it in loop.Inventory.Owned.Select(o => o.item).Where(i => i.Timing == ItemTiming.Market).ToList())
                    {
                        var held = session.Portfolio.HeldStocks.FirstOrDefault() ?? session.Stocks[0];
                        loop.UseItem(it, held);
                        itemUses++;
                    }
                    var sellOne = session.Portfolio.HeldStocks.FirstOrDefault();
                    if (sellOne != null) session.Trade(new OrderRequest(sellOne, OrderSide.Sell, 0, true));
                    Advance(session, session.Settings.ticksPerDay);
                    Expect(session.Phase == MarketPhase.Closed, "장 마감");

                    if (loop.Phase == GamePhase.GameOver) break;
                    Expect(loop.Phase == GamePhase.Settlement, "마감 → 4단계");
                    Expect(loop.Today.Closed, "하루 기록 마감");
                    for (int k = 0; k < 3 && loop.NeedsEmergency; k++) loop.AcceptEmergencyLoan(); // 햇살론은 2억씩 2번까지
                    if (loop.Phase == GamePhase.GameOver) break;
                    Expect(session.Portfolio.Cash >= loop.LoanSettings.bankruptLine, "정산 후 현금이 파산선 위 (햇살론 뒤)");

                    if (forceWin && days >= 3)
                    {
                        // 승리 경로 (이자일): 이자를 따로 내지 않고 '빚 갚기'만 해도 오늘 이자부터 갚아져 빚 0원이 된다.
                        // 예전엔 원금만 갚히고 상점으로 갈 때 이자가 빚에 다시 붙어 성공 엔딩이 안 나왔다.
                        Expect(loop.InterestLeft > 0f, "3일차엔 낼 이자가 있다");
                        float owe = loop.Loan.Debt + loop.InterestLeft;
                        session.Portfolio.Deposit(owe + 1f);
                        loop.Repay(owe);
                        Expect(loop.Loan.IsPaidOff && loop.InterestLeft <= 0f, "빚 갚기로 이자까지 다 갚아진다");
                        Expect(loop.Phase != GamePhase.GameOver || loop.TotalAssets() >= loop.LoanSettings.winAssets, "자산이 목표 전이면 계속");
                        if (loop.Phase != GamePhase.GameOver)
                        {
                            session.Portfolio.Deposit(loop.LoanSettings.winAssets);
                            loop.GoToShop();
                        }
                        Expect(loop.Phase == GamePhase.GameOver && loop.Won, "빚 0원 + 자산 목표 이상이면 승리 엔딩");
                        break;
                    }
                    if (loop.Today.Interest > 0f)
                    {
                        float cash0 = session.Portfolio.Cash, debt0 = loop.Loan.Debt, net0 = loop.NetWorth(), left0 = loop.InterestLeft;
                        loop.PayInterest();
                        float paid = left0 - loop.InterestLeft;
                        Expect(loop.InterestLeft <= 0.5f || session.Portfolio.Cash <= 0.5f, "이자 갚기");
                        Expect(Mathf.Abs((cash0 - session.Portfolio.Cash) - paid) < 1000f, $"이자 갚기: 낸 만큼 현금이 준다 (낸 {paid}, 현금 {cash0}→{session.Portfolio.Cash})");
                        Expect(Mathf.Abs(loop.Loan.Debt - debt0) < 1000f, "이자 갚기: 원금(빚)은 그대로");
                        Expect(Mathf.Abs((net0 - loop.NetWorth()) - paid) < 1000f, "이자 갚기: 순자산은 낸 만큼 준다 (현금이 줄었으니)");
                    }
                    else Expect(days < 3 || loop.Today.InterestDeferred || loop.Loan.InterestDue(loop.Day) <= 0.5f, "3일차부터 매일 이자 (이자가 붙는 대출이 있으면)"); // 기본 대출을 다 갚고 남은 대출이 아직 유예 기간이면 0원일 수 있다
                    if (session.Portfolio.Cash > 30_000_000f) loop.Repay(session.Portfolio.Cash * 0.3f);
                    if (loop.Phase == GamePhase.GameOver) break;

                    loop.GoToShop();
                    Expect(loop.Phase == GamePhase.Shop, "4단계 → 상점");
                    Expect(loop.TodayShop.Count == Math.Min(loop.ShopSlots, items.Count), "상점 진열 수");
                    Expect(loop.TodayShop.Distinct().Count() == loop.TodayShop.Count, "진열 중복 없음");
                    foreach (var it in loop.TodayShop.OrderBy(_ => rng.Next()).Take(3))
                        if (session.Portfolio.Cash > it.price + 5_000_000f) loop.Buy(it);

                    loop.StartNextDay();
                }

                Time.timeScale = 1f;
                Debug.Log($"[FullLoopSelfCheck] seed {seed}: {days}일 진행, 아이템 {itemUses}번 사용, 결과 {(loop.Phase == GamePhase.GameOver ? (loop.Won ? "승리" : "파산") : "진행 중")}, 빚 {loop.Loan.Debt:N0}원");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        static void Advance(MarketSession session, int untilTick)
        {
            int guard = 0;
            while (session.Phase == MarketPhase.Open && session.Tick < untilTick && guard++ < 1000)
            {
                SetField(session, "tickTimer", session.Settings.secondsPerTick);
                Call(session, "Update");
            }
        }

        static void Call(object target, string method) =>
            target.GetType().GetMethod(method, Any)?.Invoke(target, null);

        static void SetField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, Any);
            if (f == null) throw new Exception($"필드 없음: {target.GetType().Name}.{field}");
            f.SetValue(target, value);
        }

        static void Expect(bool condition, string what)
        {
            if (!condition) throw new Exception(what);
        }
    }
}
