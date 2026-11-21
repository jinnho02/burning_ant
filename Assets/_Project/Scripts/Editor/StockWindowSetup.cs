using BurningAnt.Market;
using BurningAnt.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.Editor
{
    /// <summary>
    /// 주식 창 씬과 필요한 에셋(장 설정, 종목 20개, PanelSettings)을 만든다.
    /// 이미 있는 에셋은 그대로 두고 없는 것만 만든다. 메뉴: BurningAnt > Setup > Stock Window Scene
    /// </summary>
    public static class StockWindowSetup
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/StockWindow.unity";

        // 기본 종목은 DefaultStockData (업종 5개 × 20종목)
        static readonly (string file, string name, Sector sector, string concept, float range, float price)[] DefaultStocks = DefaultStockData.Stocks;

        [MenuItem("BurningAnt/Setup/Stock Window Scene")]
        public static void Run()
        {
            // 새 씬을 먼저 연다: NewScene이 아직 저장 안 된 새 에셋을 언로드해서 참조가 비는 것을 막는다.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var settings = LoadOrCreate<MarketSettings>(Root + "/Data/MarketSettings.asset", _ => { });

            var stocks = new StockDefinition[DefaultStocks.Length];
            for (int i = 0; i < DefaultStocks.Length; i++)
            {
                var d = DefaultStocks[i];
                stocks[i] = LoadOrCreate<StockDefinition>($"{Root}/Data/Stocks/{d.file}.asset", s =>
                {
                    s.displayName = d.name;
                    s.sector = d.sector;
                    s.concept = d.concept;
                    s.baseRange = d.range;
                    s.startPrice = d.price;
                });
            }

            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(Root + "/UI/Settings/DefaultRuntimeTheme.tss");
            var panel = LoadOrCreate<PanelSettings>(Root + "/UI/Settings/PanelSettings.asset", p =>
            {
                p.themeStyleSheet = theme;
                p.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                p.referenceResolution = new Vector2Int(1920, 1080);
                p.match = 0.5f;
            });
            var layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/StockWindow/StockWindow.uxml");

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.white;
            camera.orthographic = true;

            var window = new GameObject("StockWindow");
            var document = window.AddComponent<UIDocument>();
            document.panelSettings = panel;
            document.visualTreeAsset = layout;

            var session = window.AddComponent<MarketSession>();
            var sessionData = new SerializedObject(session);
            sessionData.FindProperty("settings").objectReferenceValue = settings;
            var stockList = sessionData.FindProperty("stocks");
            stockList.arraySize = stocks.Length;
            for (int i = 0; i < stocks.Length; i++)
                stockList.GetArrayElementAtIndex(i).objectReferenceValue = stocks[i];
            sessionData.ApplyModifiedPropertiesWithoutUndo();

            var controller = window.AddComponent<StockWindowController>();
            var controllerData = new SerializedObject(controller);
            controllerData.FindProperty("session").objectReferenceValue = session;
            controllerData.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log($"[StockWindowSetup] 완료: {ScenePath}");
        }

        static T LoadOrCreate<T>(string path, System.Action<T> init) where T : ScriptableObject
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
