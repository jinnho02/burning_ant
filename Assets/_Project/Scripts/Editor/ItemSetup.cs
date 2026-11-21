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
    /// v0.2.1 아이템 21종을 만든다/고친다. 이름·그림은 Art/Item의 이미지 파일 이름을 기준으로 한다.
    /// 효과 수치는 기획서에 없는 것은 단순한 값으로 정한 제안값 (에셋에서 고치면 된다).
    /// DayLoop 씬의 GameLoop 상점 목록도 이 21종으로 바꾼다. 메뉴: BurningAnt > Setup > Items (v0.2.1)
    /// </summary>
    public static class ItemSetup
    {
        const string ItemDir = "Assets/_Project/Data/Items";
        const string ArtDir = "Assets/_Project/Art/Item";
        const string ScenePath = "Assets/_Project/Scenes/DayLoop.unity";

        // file, kind, 이름(= 이미지 이름), 가격, 설명, trigger, duration, speed, amount, debt
        static readonly (string file, ItemKind kind, string name, float price, string desc, float trigger, float duration, float speed, float amount, float debt)[] Items =
        {
            // ── 장 전 (2단계) ──
            ("Item_Rumor", ItemKind.Rumor, "증권가 찌라시", 3000000f, "무작위 1종목의 오늘 기사와 결과(성공·실패, 변동률)를 미리 봐요.", 0, 0, 0, 0, 0),
            ("Item_AnalystReport", ItemKind.AnalystReport, "애널리스트 리포트", 6000000f, "고른 1종목의 오늘 기사와 결과를 미리 봐요.", 0, 0, 0, 0, 0),
            ("Item_SectorRumor", ItemKind.SectorRumor, "수상한 전화", 9000000f, "고른 종목과 같은 업종에서 무작위 2종목의 결과와 발표 시점을 미리 봐요.", 0, 0, 0, 0, 0),
            ("Item_OpenLeak", ItemKind.OpenLeak, "시초가 유출", 5000000f, "고른 종목의 오늘 시가(장 시작 가격)를 미리 봐요.", 0, 0, 0, 0, 0),
            ("Item_VolatilityRadar", ItemKind.VolatilityRadar, "변동성 레이더", 6000000f, "오늘 장 중에 특보가 뜨는 종목과 시각을 알려줘요. 결과는 비밀.", 0, 0, 0, 0, 0),
            ("Item_FeeWaiver", ItemKind.FeeWaiver, "은행 VIP 쿠폰", 3000000f, "오늘 하루 매매 수수료 0%. (지금은 수수료가 없어 효과 없음)", 0, 0, 0, 0, 0),
            ("Item_LossInsurance", ItemKind.LossInsurance, "손실 보험 증서", 10000000f, "오늘 매매로 손해를 보면 장 마감에 손실의 50%를 돌려받아요.", 0, 0, 0, 0.5f, 0),
            ("Item_DevilContract", ItemKind.DevilContract, "악마의 계약서", 1000000f, "지금 바로 현금 3,000만원. 대신 빚이 4,500만원 늘어나요.", 0, 0, 0, 30000000f, 45000000f),
            ("Item_StrongDevilContract", ItemKind.DevilContract, "더 강한 악마의 계약서", 2000000f, "지금 바로 현금 8,000만원. 대신 빚이 1억 3,000만원 늘어나요.", 0, 0, 0, 80000000f, 130000000f),
            ("Item_InterestDeferral", ItemKind.InterestDeferral, "이자 유예 신청서", 5000000f, "다음 이자(매일, 3일차부터)를 한 번 면제받아요.", 0, 0, 0, 0, 0),
            ("Item_DebtAdjustment", ItemKind.DebtAdjustment, "채무 조정 신청서", 12000000f, "이자율을 2%p 내려요. 시작 이자율(5%) 밑으로는 안 내려가요.", 0, 0, 0, 0.02f, 0),
            ("Item_SunshineLoan", ItemKind.SunshineLoan, "햇살론(회생)", 8000000f, "이미 쓴 긴급 대출 기회를 한 번 되살려요.", 0, 0, 0, 0, 0),
            ("Item_Lottery", ItemKind.Lottery, "인생역전 복권", 2000000f, "2% 2억원 · 10% 2,000만원 · 30% 300만원 · 나머지는 꽝.", 0, 0, 0, 0, 0),
            // ── 장 중 ──
            ("Item_StopLoss", ItemKind.StopLoss, "손절 예약권", 2000000f, "보유 종목의 손익이 -15%(주가 약 -5%)가 되면 전량 자동 매도.", 0.15f, 0, 0, 0, 0),
            ("Item_TakeProfit", ItemKind.TakeProfit, "익절 예약권", 2000000f, "보유 종목의 손익이 +15%(주가 약 +5%)가 되면 전량 자동 매도.", 0.15f, 0, 0, 0, 0),
            ("Item_TrailingStop", ItemKind.TrailingStop, "추적 손절권", 5000000f, "보유 종목이 최고 손익률에서 15%p 밀리면 전량 자동 매도.", 0.15f, 0, 0, 0, 0),
            ("Item_TargetBuy", ItemKind.TargetBuy, "목표가 예약권", 4000000f, "고른 종목이 지금보다 5% 내리면 현금 전부로 자동 매수.", 0.05f, 0, 0, 0, 0),
            ("Item_SoulAllIn", ItemKind.SoulAllIn, "영혼의 풀매수", 3000000f, "고른 종목을 현금 전부로 즉시 매수. 수수료 없음.", 0, 0, 0, 0, 0),
            ("Item_SlowMotion", ItemKind.SlowMotion, "모래시계(전체 시간 증가)", 7000000f, "남은 장 시간이 1.5배로 늘어나요 (차트가 느려져요).", 0, 600f, 0.67f, 0, 0),
            ("Item_TimeStop", ItemKind.TimeStop, "에너지 드링크", 9000000f, "5초 동안 차트가 멈춰요. 멈춘 동안 매매할 수 있어요.", 0, 5f, 0, 0, 0),
            ("Item_Telescope", ItemKind.Telescope, "미래를 보는 망원경", 10000000f, "앞으로 5초 구간의 차트를 미리 봐요.", 0, 5f, 0, 0, 0),
        };

        // v0.2.1에 없는 예전 아이템
        static readonly string[] Obsolete = { "Item_InsiderMemo", "Item_ChainForecast" };

        [MenuItem("BurningAnt/Setup/Items (v0.2.1)")]
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
                it.triggerRate = d.trigger;
                it.durationSeconds = d.duration;
                it.speedMultiplier = d.speed;
                it.amount = d.amount;
                it.debtAmount = d.debt;
                it.icon = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtDir}/{d.name}.png");
                if (it.icon == null) Debug.LogWarning($"[ItemSetup] 그림 없음: {ArtDir}/{d.name}.png");
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
