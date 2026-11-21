using System;
using System.Linq;
using BurningAnt.Loan;
using BurningAnt.Prologue;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace BurningAnt.Editor
{
    /// <summary>
    /// 타이틀·프롤로그 씬(Title.unity)과 대사 에셋(Data/PrologueScript.asset)을 만들고 빌드 첫 씬으로 둔다.
    /// 대사 에셋이 이미 있으면 그대로 둔다 (고친 대사를 덮어쓰지 않음). 메뉴: BurningAnt > Setup > Title + Prologue Scene
    /// </summary>
    public static class PrologueSetup
    {
        const string Root = "Assets/_Project";
        const string ScenePath = Root + "/Scenes/Title.unity";
        const string CoverPath = Root + "/Art/UI/cover.png";

        // (말하는 사람, 대사, 그림, 소품, 작은 글씨) — 기본 프롤로그 "풀대출 받았습니다"
        static readonly (string speaker, string text, string portrait, PrologueProp prop, string footnote)[] DefaultLines =
        {
            ("", "희망 대출 금액을 입력해 주세요.", "good/good1.png", PrologueProp.LoanApp, ""),
            ("{hero}", "한도 끝까지요. 네, 풀로요!", "good/good3.png", PrologueProp.LoanApp, ""),
            ("개미 대출", "정말 괜찮으시겠어요? 이자는 {period}마다 {rate}씩 붙어요.", "good/good1.png", PrologueProp.LoanApp, ""),
            ("{hero}", "괜찮아요! 금방 갚을 거거든요!", "good/good2.png", PrologueProp.Approved, ""),
            ("{hero}", "영상에서 봤어. 주식은 오를 때 사서, 더 오를 때 팔면 된대!", "flex/flex.png", PrologueProp.Dream, ""),
            ("{hero}", "이 돈이면 한 달… 아니, 일주일이면 두 배야! 후후…", "flex/flex.png", PrologueProp.Dream, "※ 그런 영상은 대부분 무리입니다"),
            ("{hero}", "…어? 순자산 $0? 내 돈은 하나도 없는 거야?", "bad/bad1.png", PrologueProp.Account, ""),
            ("{hero}", "무, 무리무리무리…!", "bad/bad3.png", PrologueProp.Account, ""),
            ("{hero}", "아, 아니야! 지금부터 벌면 되잖아!", "bad/bad2.png", PrologueProp.Account, ""),
            ("{hero}", "빚을 다 갚는 날까지… 가즈아~!", "good/good3.png", PrologueProp.Login, "{debt} · {period}마다 이자 {rate} · 다 갚으면 승리"),
        };

        [MenuItem("BurningAnt/Setup/Title + Prologue Scene")]
        public static void Run()
        {
            // 새 씬을 먼저 연다: NewScene이 아직 저장 안 된 새 에셋을 언로드해서 참조가 비는 것을 막는다.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var importer = (TextureImporter)AssetImporter.GetAtPath(CoverPath);
            if (importer != null && importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite; // 2의 제곱 크기로 늘리지 않게
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
            var cover = AssetDatabase.LoadAssetAtPath<Texture2D>(CoverPath);

            var script = AssetDatabase.LoadAssetAtPath<PrologueScript>(Root + "/Data/PrologueScript.asset");
            if (script == null)
            {
                script = ScriptableObject.CreateInstance<PrologueScript>();
                script.loanSettings = AssetDatabase.LoadAssetAtPath<LoanSettings>(Root + "/Data/LoanSettings.asset");
                foreach (var d in DefaultLines)
                {
                    script.lines.Add(new PrologueLine
                    {
                        speaker = d.speaker,
                        text = d.text,
                        portrait = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Root}/Art/Character/{d.portrait}"),
                        prop = d.prop,
                        footnote = d.footnote,
                    });
                }
                AssetDatabase.CreateAsset(script, Root + "/Data/PrologueScript.asset");
            }

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;

            var go = new GameObject("Prologue");
            var document = go.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Root + "/UI/Settings/PanelSettings.asset");
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/Prologue/Prologue.uxml");
            var controller = go.AddComponent<PrologueController>();
            var data = new SerializedObject(controller);
            data.FindProperty("script").objectReferenceValue = script;
            data.FindProperty("cover").objectReferenceValue = cover;
            data.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);

            // 타이틀을 빌드 첫 씬으로, 나머지는 기존 순서 그대로
            var others = EditorBuildSettings.scenes.Where(s => s.path != ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }.Concat(others).ToArray();
            AssetDatabase.SaveAssets();

            Check(script, cover);
            Debug.Log($"[PrologueSetup] 완료: {ScenePath} (대사 {script.lines.Count}줄)");
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
                Debug.LogError("[PrologueSetup] 실패: " + e);
                EditorApplication.Exit(1);
            }
        }

        static void Check(PrologueScript script, Texture2D cover)
        {
            if (cover == null) throw new Exception("표지 그림이 없어요: " + CoverPath);
            if (script.lines.Count == 0) throw new Exception("대사가 비었어요");
            if (script.lines.Any(l => l.portrait == null)) throw new Exception("그림이 빠진 대사가 있어요");
            if (EditorBuildSettings.scenes[0].path != ScenePath) throw new Exception("타이틀이 첫 씬이 아니에요");
            if (script.loanSettings != null && !script.Fill("{debt} {rate} {period}").StartsWith("$"))
                throw new Exception("숫자 치환이 안 돼요");
            if (script.Fill("{hero}") != script.heroName) throw new Exception("이름 치환이 안 돼요");
        }
    }
}
