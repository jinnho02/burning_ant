using System;
using System.Linq;
using BurningAnt.Character;
using BurningAnt.Core;
using BurningAnt.Items;
using BurningAnt.Loan;
using BurningAnt.Market;
using BurningAnt.News;
using BurningAnt.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.Editor
{
    /// <summary>
    /// 하루 루프 데모 씬(DayLoop)과 필요한 에셋(대출·뉴스·캐릭터 설정, 아이템 9종)을 만든다.
    /// 이미 있는 에셋은 그대로 두고 없는 것만 만든다. 메뉴: BurningAnt > Setup > Day Loop Scene
    /// 종목·장 설정·PanelSettings는 주식 창 셋업이 만든 것을 그대로 쓴다.
    /// </summary>
    public static class DayLoopSetup
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/DayLoop.unity";

        static readonly (string file, ItemKind kind, string name, float price, string desc, float accuracy, float trigger, float duration)[] DefaultItems =
        {
            ("Item_Rumor", ItemKind.Rumor, "찌라시", 300f, "무작위 1종목의 오늘 기사와 결과(성공·실패, 변동률)를 미리 본다. 틀리지 않음", 0.65f, 0f, 0f),
            ("Item_AnalystReport", ItemKind.AnalystReport, "애널리스트 리포트", 600f, "고른 1종목의 오늘 기사와 결과를 미리 본다. 틀리지 않음", 0.75f, 0f, 0f),
            ("Item_ChainForecast", ItemKind.ChainForecast, "뉴스 결과 예측", 800f, "오늘 뉴스 하나의 결과와 발표 시점을 미리 본다", 0.80f, 0f, 0f),
            ("Item_InsiderMemo", ItemKind.InsiderMemo, "내부자 메모", 1200f, "고른 1종목의 오늘 기사·결과와 발표 시점(특보 몇 초 뒤 / 장 마감)까지 미리 본다", 0.85f, 0f, 0f),
            ("Item_StopLoss", ItemKind.StopLoss, "손절 예약", 200f, "보유 종목이 평단 대비 -5%가 되면 전량 자동 매도", 0f, 0.05f, 0f),
            ("Item_TakeProfit", ItemKind.TakeProfit, "익절 예약", 200f, "보유 종목이 평단 대비 +5%가 되면 전량 자동 매도", 0f, 0.05f, 0f),
            ("Item_SlowMotion", ItemKind.SlowMotion, "슬로우 모션", 500f, "10초간 차트 속도 절반", 0f, 0f, 10f),
            ("Item_TimeStop", ItemKind.TimeStop, "시간 정지", 900f, "5초간 차트 멈춤 (멈춘 동안 매매 가능)", 0f, 0f, 5f),
            ("Item_Telescope", ItemKind.Telescope, "망원경", 1000f, "다음 5초 구간 차트 미리보기", 0f, 0f, 5f),
        };

        [MenuItem("BurningAnt/Setup/Day Loop Scene")]
        public static void Run()
        {
            if (!AssetDatabase.IsValidFolder(Root + "/Scenes")) AssetDatabase.CreateFolder(Root, "Scenes");

            // 주식 창 에셋(종목 20개, 장 설정, PanelSettings)이 없으면 먼저 만든다.
            if (AssetDatabase.LoadAssetAtPath<MarketSettings>(Root + "/Data/MarketSettings.asset") == null)
                StockWindowSetup.Run();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var market = AssetDatabase.LoadAssetAtPath<MarketSettings>(Root + "/Data/MarketSettings.asset");
            var stocks = AssetDatabase.FindAssets("t:StockDefinition", new[] { Root + "/Data/Stocks" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .OrderBy(p => p)
                .Select(AssetDatabase.LoadAssetAtPath<StockDefinition>)
                .OrderBy(s => s.sector).ThenBy(s => s.startPrice)
                .ToArray();

            var loan = LoadOrCreate<LoanSettings>(Root + "/Data/LoanSettings.asset", _ => { });
            var news = LoadOrCreate<NewsSettings>(Root + "/Data/NewsSettings.asset", _ => { });
            var character = LoadOrCreate<CharacterSettings>(Root + "/Data/CharacterSettings.asset", _ => { });
            AssignCharacterArt(character);

            var items = DefaultItems.Select(d => LoadOrCreate<ItemDefinition>($"{Root}/Data/Items/{d.file}.asset", it =>
            {
                it.kind = d.kind;
                it.displayName = d.name;
                it.price = d.price;
                it.description = d.desc;
                it.accuracy = d.accuracy;
                it.triggerRate = d.trigger;
                it.durationSeconds = d.duration;
                it.speedMultiplier = 0.5f;
            })).ToArray();

            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(Root + "/UI/Settings/PanelSettings.asset");
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/GameScreen/GameScreen.uxml");

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;

            var gameObject = new GameObject("Game");
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;

            var session = gameObject.AddComponent<MarketSession>();
            var sessionData = new SerializedObject(session);
            sessionData.FindProperty("settings").objectReferenceValue = market;
            SetList(sessionData.FindProperty("stocks"), stocks);
            sessionData.ApplyModifiedPropertiesWithoutUndo();

            var stockWindow = gameObject.AddComponent<StockWindowController>();
            var stockWindowData = new SerializedObject(stockWindow);
            stockWindowData.FindProperty("session").objectReferenceValue = session;
            stockWindowData.ApplyModifiedPropertiesWithoutUndo();

            var loop = gameObject.AddComponent<GameLoop>();
            var loopData = new SerializedObject(loop);
            loopData.FindProperty("session").objectReferenceValue = session;
            loopData.FindProperty("loanSettings").objectReferenceValue = loan;
            loopData.FindProperty("newsSettings").objectReferenceValue = news;
            loopData.FindProperty("characterSettings").objectReferenceValue = character;
            SetList(loopData.FindProperty("shopItems"), items);
            loopData.ApplyModifiedPropertiesWithoutUndo();

            var screen = gameObject.AddComponent<GameScreenController>();
            var screenData = new SerializedObject(screen);
            screenData.FindProperty("game").objectReferenceValue = loop;
            screenData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            // 다시 하기(씬 다시 불러오기)를 위해 빌드 목록 맨 앞에 넣는다. 기존 씬은 유지.
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();

            AssetDatabase.SaveAssets();
            Debug.Log($"[DayLoopSetup] 완료: {ScenePath}");
        }

        /// <summary>
        /// Art/Character의 good·bad·flex 그림을 표정에 연결한다 (비어 있는 칸만 채움).
        /// 폭락 = bad3(패닉), 손해 = bad1(고민), 보합 = good1(미소), 이득 = good3(주먹), 대박 = flex,
        /// 장 전 = bad2(기도), 승리 = good2(활짝 웃음).
        /// </summary>
        static void AssignCharacterArt(CharacterSettings c)
        {
            const string art = Root + "/Art/Character/";
            Texture2D T(string path) => AssetDatabase.LoadAssetAtPath<Texture2D>(art + path);

            var moods = new[] { "bad/bad3.png", "bad/bad1.png", "good/good1.png", "good/good3.png", "flex/flex.png" };
            if (c.moodImages == null || c.moodImages.Length != moods.Length) c.moodImages = new Texture2D[moods.Length];
            for (int i = 0; i < moods.Length; i++)
                if (c.moodImages[i] == null) c.moodImages[i] = T(moods[i]);
            if (c.prepImage == null) c.prepImage = T("bad/bad2.png");
            if (c.winImage == null) c.winImage = T("good/good2.png");
            EditorUtility.SetDirty(c);
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
                Debug.LogError("[DayLoopSetup] 실패: " + e);
                EditorApplication.Exit(1);
            }
        }

        static void SetList(SerializedProperty list, UnityEngine.Object[] values)
        {
            list.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        static T LoadOrCreate<T>(string path, Action<T> init) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            init(asset);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
