#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    public static class DesktopBuildMenu
    {
        const string ScenePath = "Assets/Scenes/DesktopMain.unity";
        const string OutputDir = "Builds/Windows64";

        [MenuItem("Fish Social/Build Windows Development Player")]
        public static void BuildWindowsDevelopment()
        {
            EnsureSceneInBuildSettings();
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "..", OutputDir));
            var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", OutputDir, "FishSocialDesktop.exe"));

            var options = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging,
            };

            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError("[Build] Windows Development Build failed: " + report.summary.result);
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log("[Build] Windows Development Build OK → " + output);
            var smoke = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "STEAM-DESKTOP-04-smoke.md"));
            Debug.Log("[Build] Smoke checklist: " + smoke);
        }

        [MenuItem("Fish Social/Open Desktop Main Scene")]
        public static void OpenMainScene()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError("Missing " + ScenePath);
                return;
            }

            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ScenePath);
        }

        static void EnsureSceneInBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            EditorBuildSettings.scenes = scenes;
        }
    }
}
#endif
