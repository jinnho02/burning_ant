using System;
using System.Linq;
using BurningAnt.Items;
using BurningAnt.Market;
using BurningAnt.News;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BurningAnt.Editor
{
    /// <summary>
    /// 업종 5개 × 20종목과 업종별 기사 템플릿(50개)을 에셋으로 만들고, 뉴스 설정과 씬(DayLoop, StockWindow)의 종목 목록에 연결한다.
    /// 예전 임시 8종목 에셋은 지운다. 이미 있는 종목·기사 에셋은 덮어쓰지 않는다.
    /// 메뉴: BurningAnt > Setup > Stocks + News (5 sectors)
    /// </summary>
    public static class StockNewsSetup
    {
        const string Root = "Assets/_Project";
        static readonly string[] Scenes = { Root + "/Scenes/DayLoop.unity", Root + "/Scenes/StockWindow.unity" };

        // 예지 규칙(틀리지 않음)에 맞춘 정보 아이템 설명
        static readonly (ItemKind kind, string name, string desc)[] InfoItems =
        {
            (ItemKind.Rumor, "찌라시", "무작위 1종목의 오늘 기사와 결과(성공·실패, 변동률)를 미리 본다. 틀리지 않음"),
            (ItemKind.AnalystReport, "애널리스트 리포트", "고른 1종목의 오늘 기사와 결과를 미리 본다. 틀리지 않음"),
            (ItemKind.InsiderMemo, "내부자 메모", "고른 1종목의 오늘 기사·결과와 발표 시점(특보 몇 초 뒤 / 장 마감)까지 미리 본다"),
            (ItemKind.ChainForecast, "뉴스 결과 예측", "오늘 뉴스 하나의 결과와 발표 시점을 미리 본다"),
        };

        [MenuItem("BurningAnt/Setup/Stocks + News (5 sectors)")]
        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EnsureFolder(Root + "/Data", "Stocks");
            EnsureFolder(Root + "/Data", "News");

            // 예전 임시 종목 지우기
            var keep = DefaultStockData.Stocks.Select(d => $"{Root}/Data/Stocks/{d.file}.asset").ToHashSet();
            foreach (var path in AssetDatabase.FindAssets("t:StockDefinition", new[] { Root + "/Data/Stocks" }).Select(AssetDatabase.GUIDToAssetPath))
                if (!keep.Contains(path)) AssetDatabase.DeleteAsset(path);

            var stocks = DefaultStockData.Stocks.Select(d => LoadOrCreate<StockDefinition>($"{Root}/Data/Stocks/{d.file}.asset", s =>
            {
                s.displayName = d.name;
                s.sector = d.sector;
                s.concept = d.concept;
                s.baseRange = d.range;
                s.startPrice = d.price;
            })).ToArray();

            var library = DefaultNewsData.Templates.Select(kv => LoadOrCreate<SectorNews>($"{Root}/Data/News/News_{kv.Key}.asset", l =>
            {
                l.sector = kv.Key;
                l.templates = kv.Value.Select(t => new NewsTemplate
                {
                    initialHeadline = t.initial, successHeadline = t.success, successPercent = t.successPercent,
                    failureHeadline = t.failure, failurePercent = t.failurePercent,
                }).ToList();
            })).ToList();

            var news = AssetDatabase.LoadAssetAtPath<NewsSettings>(Root + "/Data/NewsSettings.asset");
            if (news != null)
            {
                news.library = library;
                EditorUtility.SetDirty(news);
            }

            foreach (var item in AssetDatabase.FindAssets("t:ItemDefinition", new[] { Root + "/Data/Items" })
                         .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<ItemDefinition>))
            {
                var info = InfoItems.FirstOrDefault(i => i.kind == item.kind);
                if (info.name == null) continue;
                item.displayName = info.name;
                item.description = info.desc;
                EditorUtility.SetDirty(item);
            }
            AssetDatabase.SaveAssets();

            foreach (var scenePath in Scenes)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) continue;
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                // 씬을 열면 메모리의 에셋이 내려갈 수 있어 경로로 다시 읽는다
                stocks = DefaultStockData.Stocks.Select(d => AssetDatabase.LoadAssetAtPath<StockDefinition>($"{Root}/Data/Stocks/{d.file}.asset")).ToArray();
                foreach (var session in UnityEngine.Object.FindObjectsByType<MarketSession>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    var data = new SerializedObject(session);
                    var list = data.FindProperty("stocks");
                    list.arraySize = stocks.Length;
                    for (int i = 0; i < stocks.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = stocks[i];
                    data.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorSceneManager.SaveScene(scene);
            }

            // 씬을 연 뒤라 경로로 다시 읽어 점검
            library = DefaultNewsData.Templates.Keys.Select(k => AssetDatabase.LoadAssetAtPath<SectorNews>($"{Root}/Data/News/News_{k}.asset")).ToList();
            news = AssetDatabase.LoadAssetAtPath<NewsSettings>(Root + "/Data/NewsSettings.asset");
            Check(stocks, library, news);
            Debug.Log($"[StockNewsSetup] 완료: 종목 {stocks.Length}개, 기사 {library.Sum(l => l.templates.Count)}개");
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
                Debug.LogError("[StockNewsSetup] 실패: " + e);
                EditorApplication.Exit(1);
            }
        }

        static void Check(StockDefinition[] stocks, System.Collections.Generic.List<SectorNews> library, NewsSettings news)
        {
            if (stocks.Length != 20) throw new Exception("종목이 20개가 아니에요");
            foreach (Sector sec in Enum.GetValues(typeof(Sector)))
            {
                if (stocks.Count(s => s.sector == sec) != 4) throw new Exception($"{sec} 업종 종목이 4개가 아니에요");
                if (library.FirstOrDefault(l => l.sector == sec)?.templates.Count < 10) throw new Exception($"{sec} 기사가 10개 미만이에요");
            }
            if (stocks.Any(s => s.baseRange < 0.1f - 1e-4f || s.baseRange > 0.5f + 1e-4f)) throw new Exception("기본 구간이 10~50% 밖이에요");
            if (news == null || news.library.Count != 5) throw new Exception("뉴스 설정에 기사 템플릿이 연결되지 않았어요");
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}")) AssetDatabase.CreateFolder(parent, name);
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
