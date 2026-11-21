using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BurningAnt.Editor
{
    /// <summary>
    /// Windows 실행 파일 빌드. 에디터를 열지 않으려면 프로젝트 폴더의 build.bat을 실행한다.
    /// 결과: Builds/Windows/(제품 이름).exe — 빌드 설정(Build Profiles)에 켜진 씬을 순서대로 넣는다.
    /// </summary>
    public static class BuildScript
    {
        const string OutputDir = "Builds/Windows";

        [MenuItem("BurningAnt/Build/Windows")]
        public static void BuildFromMenu() => Build(exitWhenDone: false);

        /// <summary>build.bat이 배치 모드로 부르는 진입점. 실패하면 종료 코드 1.</summary>
        public static void BuildWindows() => Build(exitWhenDone: true);

        static void Build(bool exitWhenDone)
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            bool ok = scenes.Length > 0;
            if (!ok) Debug.LogError("[BuildScript] 빌드 설정에 켜진 씬이 없어요.");

            if (ok)
            {
                string exe = Path.Combine(OutputDir, PlayerSettings.productName + ".exe");
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = exe,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                ok = report.summary.result == BuildResult.Succeeded;
                Debug.Log($"[BuildScript] {(ok ? "성공" : "실패")}: {exe} ({report.summary.totalErrors} errors, 씬 {string.Join(", ", scenes)})");
            }

            if (exitWhenDone) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
