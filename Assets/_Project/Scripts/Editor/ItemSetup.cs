using System;
using System.Linq;
using BurningAnt.Core;
using BurningAnt.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BurningAnt.Editor
{
    /// <summary>
    /// 아이템 28종 (팀 아이템표 23종 + 무대출·콩대출·더 강한 악마의 계약서·모래시계·에너지 드링크)을 만든다/고친다.
    /// 이름 = Art/Item 그림 파일 이름 (그림이 없으면 경고만 하고 그림 없이 만든다).
    /// 설명 맨 앞 [장 전] = 2단계, [장 중] = 3단계, [장 전·장 중] = 둘 다 (ItemDefinition.Timing과 같아야 한다).
    /// DayLoop 씬의 GameLoop 상점 목록도 이 목록으로 바꾼다. 메뉴: BurningAnt > Setup > Items
    /// </summary>
    public static class ItemSetup
    {
        const string ItemDir = "Assets/_Project/Data/Items";
        const string ArtDir = "Assets/_Project/Art/Item";
        const string ScenePath = "Assets/_Project/Scenes/DayLoop.unity";

        // file, kind, 이름, 가격(원), 설명, 정확도, trigger, duration, speed, amount, debt, 대출 이자율
        static readonly (string file, ItemKind kind, string name, float price, string desc, float acc, float trigger, float duration, float speed, float amount, float debt, float rate)[] Items =
        {
            // ── 정보 획득형 (틀릴 수 있음) ──
            ("Item_Rumor", ItemKind.Rumor, "증권가 찌라시", 150_000f, "[장 전] 무작위 종목 1개의 오늘 상승·하락 방향을 알려줘요. 65% 확률로 맞아요.", 0.65f, 0, 0, 0, 0, 0, 0),
            ("Item_AnalystReport", ItemKind.AnalystReport, "애널리스트 리포트", 350_000f, "[장 전] 고른 종목의 오늘 상승·하락 방향을 알려줘요. 75% 확률로 맞아요.", 0.75f, 0, 0, 0, 0, 0, 0),
            ("Item_InsiderMemo", ItemKind.InsiderMemo, "내부자 메모", 800_000f, "[장 전] 고른 종목의 오늘 방향과 예상 등락폭을 알려줘요. 85% 확률로 맞아요.", 0.85f, 0, 0, 0, 0, 0, 0),
            ("Item_SectorRumor", ItemKind.ChainForecast, "수상한 전화", 500_000f, "[장 전] 오늘 뉴스 하나가 성공할지 실패할지 알려줘요. 80% 확률로 맞아요.", 0.80f, 0, 0, 0, 0, 0, 0),
            ("Item_OpenLeak", ItemKind.OpenLeak, "시초가 유출", 400_000f, "[장 전] 고른 종목의 오늘 시가(장 시작 가격)를 미리 봐요.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_VolatilityRadar", ItemKind.VolatilityRadar, "변동성 레이더", 550_000f, "[장 전] 오늘 가장 크게 움직일 종목 2개를 알려줘요. 오를지 내릴지는 몰라요.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_Telescope", ItemKind.Telescope, "미래를 보는 망원경", 1_500_000f, "[장 중] 고른 종목의 앞으로 5초 가격을 따로 창에서 미리 봐요. 실제 차트는 그대로예요.", 1f, 0, 5f, 0, 0, 0, 0),
            // ── 매매 보조형 ──
            ("Item_StopLoss", ItemKind.StopLoss, "손절 예약권", 150_000f, "[장 중] 보유 종목의 손익이 -15%가 되면 전량 자동 매도.", 1f, 0.15f, 0, 0, 0, 0, 0),
            ("Item_TakeProfit", ItemKind.TakeProfit, "익절 예약권", 150_000f, "[장 중] 보유 종목의 손익이 +15%가 되면 전량 자동 매도.", 1f, 0.15f, 0, 0, 0, 0, 0),
            ("Item_TrailingStop", ItemKind.TrailingStop, "추적 손절권", 450_000f, "[장 중] 보유 종목이 최고 손익률에서 15%p 밀리면 전량 자동 매도.", 1f, 0.15f, 0, 0, 0, 0, 0),
            ("Item_TargetBuy", ItemKind.TargetBuy, "목표가 예약권", 300_000f, "[장 중] 고른 종목이 지금보다 5% 내리면 현금 전부로 자동 매수.", 1f, 0.05f, 0, 0, 0, 0, 0),
            ("Item_BankruptAlarm", ItemKind.BankruptAlarm, "파산 경보기", 300_000f, "[장 전·장 중] 오늘 현금+주식이 2,000만원 밑으로 내려가면 화면과 소리로 강하게 경고해요.", 1f, 0, 0, 0, 20_000_000f, 0, 0),
            ("Item_EmergencyExit", ItemKind.EmergencyExit, "비상 탈출 버튼", 500_000f, "[장 중] 쓰는 즉시 보유 종목 전부를 지금 가격에 매도해요.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_DualMonitor", ItemKind.DualMonitor, "듀얼 모니터", 600_000f, "[장 전] 오늘 하루 동시에 보유할 수 있는 종목 수 4개 → 5개.", 1f, 0, 0, 0, 0, 0, 0),
            // ── 대출·부채 관리형 ──
            ("Item_FeeWaiver", ItemKind.InterestCoupon, "은행 VIP 쿠폰", 900_000f, "[장 전] 다음에 낼 이자를 30% 깎아줘요.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_InterestDeferral", ItemKind.InterestDeferral, "이자 유예 신청서", 400_000f, "[장 전] 다음 이자를 하루 미뤄요 (다음 날 이자에 더해져요). 게임당 1번.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_CreditLaunder", ItemKind.CreditLaunder, "신용등급 세탁기", 750_000f, "[장 전] 다음 대출 1회(추가 대출·무대출·콩대출·햇살론)의 이자율을 절반으로. 기본 대출 5%에는 안 돼요.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_DebtAdjustment", ItemKind.DebtAdjustment, "채무 조정 신청서", 600_000f, "[장 전] 기본 대출 원금 1% 감면. 게임당 1번.", 1f, 0, 0, 0, 0.01f, 0, 0),
            // ── 고위험·도박형 ──
            ("Item_SoulAllIn", ItemKind.SoulAllIn, "영혼의 풀매수", 1_000_000f, "[장 전·장 중] 다음 매수 1회의 레버리지 3배 → 10배. 이익도 손해도 10배예요.", 1f, 0, 0, 10f, 0, 0, 0),
            ("Item_DevilContract", ItemKind.DevilTrade, "악마의 계약서", 500_000f, "[장 전·장 중] 다음 매도 1회의 손익을 1.5배로 정산해요. 이익도 손해도.", 1f, 0, 0, 1.5f, 0, 0, 0),
            ("Item_LossInsurance", ItemKind.LossInsurance, "손실 보험 증서", 750_000f, "[장 전·장 중] 다음 매도 1회가 손해면 손실의 20%를 보상해요 (최대 400만원).", 1f, 0, 0, 0, 0.2f, 4_000_000f, 0),
            ("Item_LoanShark", ItemKind.CashLoan, "사채업자 명함", 0f, "[장 전·장 중] 무료. 즉시 2,000만원을 받고, 대신 기본 대출 원금이 3,000만원 늘어나요.", 1f, 0, 0, 0, 20_000_000f, 30_000_000f, 0),
            ("Item_Lottery", ItemKind.Lottery, "인생역전 복권", 200_000f, "[장 전] 오늘 장 결과 때 10% 확률로 100만원. 실패하면 0원.", 1f, 0, 0, 0, 1_000_000f, 0.1f, 0),
            // ── 기존 아이템 ──
            ("Item_LoanRadish", ItemKind.CashLoan, "무대출", 200_000f, "[장 전·장 중] 즉시 5,000만원 대출. 하루 이자 20% (다음 날부터).", 1f, 0, 0, 0, 50_000_000f, 50_000_000f, 0.2f),
            ("Item_LoanKidney", ItemKind.CashLoan, "콩대출", 300_000f, "[장 전·장 중] 즉시 1억원 대출. 하루 이자 20% (다음 날부터).", 1f, 0, 0, 0, 100_000_000f, 100_000_000f, 0.2f),
            ("Item_StrongDevilContract", ItemKind.CashLoan, "더 강한 악마의 계약서", 200_000f, "[장 전·장 중] 즉시 8,000만원. 대신 기본 대출 원금이 1억 3,000만원 늘어나요.", 1f, 0, 0, 0, 80_000_000f, 130_000_000f, 0),
            ("Item_SlowMotion", ItemKind.SlowMotion, "모래시계(전체 시간 증가)", 700_000f, "[장 중] 남은 장 시간이 1.5배로 늘어나요 (차트가 느려져요).", 1f, 0, 600f, 0.67f, 0, 0, 0),
            ("Item_TimeStop", ItemKind.TimeStop, "에너지 드링크", 900_000f, "[장 중] 5초 동안 차트가 멈춰요. 멈춘 동안 매매할 수 있어요.", 1f, 0, 5f, 0, 0, 0, 0),
        };

        // 더는 안 쓰는 아이템 (햇살론은 아이템이 아니라 파산 직전에만)
        static readonly string[] Obsolete = { "Item_SunshineLoan", "Item_ChainForecast" };

        [MenuItem("BurningAnt/Setup/Items")]
        public static void Run()
        {
            foreach (var file in Obsolete)
            {
                string path = $"{ItemDir}/{file}.asset";
                if (AssetDatabase.LoadAssetAtPath<ItemDefinition>(path) != null) AssetDatabase.DeleteAsset(path);
            }

            var assets = Items.Select(d =>
            {
                string path = $"{ItemDir}/{d.file}.asset";
                var it = AssetDatabase.LoadAssetAtPath<ItemDefinition>(path);
                bool create = it == null;
                if (create) it = ScriptableObject.CreateInstance<ItemDefinition>();
                it.kind = d.kind;
                it.displayName = d.name;
                it.price = d.price;
                it.description = d.desc;
                it.accuracy = d.acc;
                it.triggerRate = d.trigger;
                it.durationSeconds = d.duration;
                it.speedMultiplier = d.speed;
                it.amount = d.amount;
                it.debtAmount = d.debt;
                it.loanRate = d.rate;
                it.icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{d.name}.png");
                if (it.icon == null) Debug.LogWarning($"[ItemSetup] 그림 없음: {ArtDir}/{d.name}.png");
                string tag = it.Timing switch { ItemTiming.Prep => "[장 전]", ItemTiming.Market => "[장 중]", _ => "[장 전·장 중]" };
                if (!d.desc.StartsWith(tag)) throw new Exception($"{d.name}: 설명의 사용 시점이 코드({tag})와 달라요");
                if (create) AssetDatabase.CreateAsset(it, path);
                else EditorUtility.SetDirty(it);
                return it;
            }).ToArray();
            AssetDatabase.SaveAssets();

            // DayLoop 씬 상점 목록
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // 씬을 열면 방금 만든 에셋 참조가 풀릴 수 있어 경로로 다시 읽는다
            assets = Items.Select(d => AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemDir}/{d.file}.asset")).ToArray();
            var loop = UnityEngine.Object.FindFirstObjectByType<GameLoop>();
            if (loop != null)
            {
                var data = new SerializedObject(loop);
                var list = data.FindProperty("shopItems");
                list.arraySize = assets.Length;
                for (int i = 0; i < assets.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = assets[i];
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[ItemSetup] 완료: 아이템 {assets.Length}종");
        }

        public static void RunBatch()
        {
            try
            {
                Run();
                DayLoopSelfCheck.Run();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[ItemSetup] 실패: " + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
