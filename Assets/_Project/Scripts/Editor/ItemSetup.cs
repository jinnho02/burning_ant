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
    /// 아이템 21종 (그림이 있는 것만)을 만든다/고친다. 가격은 밸런싱 2차: 전부 10배, 엠바고만 1억 고정.
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
            ("Item_Rumor", ItemKind.Rumor, "엠바고", 100_000_000f, "[장 전] 오늘 뉴스 하나를 골라, 아직 안 풀린 후속 기사(성공·실패, 변동률, 발표 시점)를 미리 봐요.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_AnalystReport", ItemKind.AnalystReport, "애널리스트 리포트", 3_500_000f, "[장 전] 고른 종목의 오늘 상승·하락 방향을 알려줘요. 75% 확률로 맞아요.", 0.75f, 0, 0, 0, 0, 0, 0),
            ("Item_SectorRumor", ItemKind.ChainForecast, "수상한 전화", 5_000_000f, "[장 전] 오늘 뉴스 하나가 성공할지 실패할지 알려줘요. 80% 확률로 맞아요.", 0.80f, 0, 0, 0, 0, 0, 0),
            ("Item_OpenLeak", ItemKind.OpenLeak, "시초가 유출", 4_000_000f, "[장 전] 고른 종목의 오늘 시가(장 시작 가격)를 미리 봐요.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_VolatilityRadar", ItemKind.VolatilityRadar, "변동성 레이더", 5_500_000f, "[장 전] 오늘 가장 크게 움직일 종목 2개를 알려줘요. 오를지 내릴지는 몰라요.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_Telescope", ItemKind.Telescope, "미래를 보는 망원경", 15_000_000f, "[장 중] 고른 종목의 앞으로 5초 가격을 따로 창에서 미리 봐요. 실제 차트는 그대로예요.", 1f, 0, 5f, 0, 0, 0, 0),
            // ── 매매 보조형 ──
            ("Item_StopLoss", ItemKind.StopLoss, "손절 예약권", 1_500_000f, "[장 전·장 중] 보유 종목의 손익이 -15%가 되면 전량 자동 매도.", 1f, 0.15f, 0, 0, 0, 0, 0),
            ("Item_TakeProfit", ItemKind.TakeProfit, "익절 예약권", 1_500_000f, "[장 전·장 중] 보유 종목의 손익이 +15%가 되면 전량 자동 매도.", 1f, 0.15f, 0, 0, 0, 0, 0),
            ("Item_TrailingStop", ItemKind.TrailingStop, "추적 손절권", 4_500_000f, "[장 중] 보유 종목이 최고 손익률에서 15%p 밀리면 전량 자동 매도.", 1f, 0.15f, 0, 0, 0, 0, 0),
            ("Item_TargetBuy", ItemKind.TargetBuy, "목표가 예약권", 3_000_000f, "[장 중] 고른 종목이 지금보다 5% 내리면 현금 전부로 자동 매수.", 1f, 0.05f, 0, 0, 0, 0, 0),
            // ── 대출·부채 관리형 ──
            ("Item_FeeWaiver", ItemKind.InterestCoupon, "은행 VIP 쿠폰", 9_000_000f, "[장 전] VIP 우대 금리: 다음에 낼 이자 50% 할인.", 1f, 0, 0, 0, 0.5f, 0, 0),
            ("Item_InterestDeferral", ItemKind.InterestDeferral, "이자 유예 신청서", 4_000_000f, "[장 전] 다음 이자를 하루 미뤄요 (다음 날 이자에 더해져요). 게임당 1번.", 1f, 0, 0, 0, 0, 0, 0),
            ("Item_DebtAdjustment", ItemKind.DebtAdjustment, "채무 조정 신청서", 6_000_000f, "[장 전] 기본 대출 원금 10% 감면. 게임당 1번.", 1f, 0, 0, 0, 0.1f, 0, 0),
            // ── 고위험·도박형 ──
            ("Item_DevilContract", ItemKind.DevilTrade, "악마의 계약서", 5_000_000f, "[장 전·장 중] 다음 매도 1회의 손익을 1.5배로 정산해요. 이익도 손해도.", 1f, 0, 0, 1.5f, 0, 0, 0),
            ("Item_LossInsurance", ItemKind.LossInsurance, "손실 보험 증서", 7_500_000f, "[장 전·장 중] 다음 매도 1회가 손해면 손실의 30%를 보상해요 (최대 5,000만원).", 1f, 0, 0, 0, 0.3f, 50_000_000f, 0),
            ("Item_Lottery", ItemKind.Lottery, "인생역전 복권", 2_000_000f, "[장 전] 오늘 장 결과 때 5% 확률로 5,000만원! 실패하면 0원.", 1f, 0, 0, 0, 50_000_000f, 0.05f, 0),
            // ── 기존 아이템 ──
            ("Item_LoanRadish", ItemKind.CashLoan, "러시앤러시", 2_000_000f, "[장 전·장 중] 즉시 5,000만원 대출. 하루 이자 20% (다음 날부터).", 1f, 0, 0, 0, 50_000_000f, 50_000_000f, 0.2f),
            ("Item_LoanKidney", ItemKind.CashLoan, "사나머니", 3_000_000f, "[장 전·장 중] 즉시 1억원 대출. 하루 이자 20% (다음 날부터).", 1f, 0, 0, 0, 100_000_000f, 100_000_000f, 0.2f),
            ("Item_StrongDevilContract", ItemKind.CashLoan, "더 강한 악마의 계약서", 2_000_000f, "[장 전·장 중] 즉시 8,000만원. 대신 기본 대출 원금이 1억원 늘어나요 (이자는 기본 대출 5%).", 1f, 0, 0, 0, 80_000_000f, 100_000_000f, 0),
            ("Item_SlowMotion", ItemKind.SlowMotion, "모래시계(전체 시간 증가)", 7_000_000f, "[장 중] 남은 장 시간이 1.5배로 늘어나요 (차트가 느려져요).", 1f, 0, 600f, 0.67f, 0, 0, 0),
            ("Item_TimeStop", ItemKind.SlowMotion, "에너지 드링크", 9_000_000f, "[장 중] 10초 동안 주가가 0.5배속으로 천천히 움직여요.", 1f, 0, 10f, 0.5f, 0, 0, 0),
        };

        // 이름이 바뀌어 그림 파일 이름과 다른 아이템 (아이템 이름 → 그림 이름)
        static readonly System.Collections.Generic.Dictionary<string, string> IconAlias = new()
        {
            { "엠바고", "증권가 찌라시" },
            { "러시앤러시", "무대출" },
            { "사나머니", "콩대출" },
        };

        // 더는 안 쓰는 아이템 (햇살론은 아이템이 아니라 파산 직전에만)
        static readonly string[] Obsolete = { "Item_SunshineLoan", "Item_ChainForecast",
            // 그림이 없어 뺀 아이템 (22종으로 정리)
            "Item_InsiderMemo", "Item_BankruptAlarm", "Item_EmergencyExit", "Item_DualMonitor", "Item_CreditLaunder", "Item_LoanShark",
            // 밸런싱: 레버리지 10배 아이템 삭제 (21종)
            "Item_SoulAllIn" };

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
                string iconName = IconAlias.TryGetValue(d.name, out var alias) ? alias : d.name;
                it.icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{iconName}.png");
                if (it.icon == null) Debug.LogWarning($"[ItemSetup] 그림 없음: {ArtDir}/{iconName}.png");
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
