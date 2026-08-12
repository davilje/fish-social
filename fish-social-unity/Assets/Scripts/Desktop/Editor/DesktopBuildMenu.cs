#if UNITY_EDITOR
using System;
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
        const string SteamAppIdFile = "steam_appid.txt";

        [MenuItem("Fish Social/Build Windows Development Player")]
        public static void BuildWindowsDevelopment()
        {
            try
            {
                if (!File.Exists(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath))))
                {
                    FailBuild("找不到桌面主场景：\n" + ScenePath);
                    return;
                }

                EnsureSceneInBuildSettings();
                var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                var outputDirectory = Path.Combine(projectRoot, OutputDir);
                Directory.CreateDirectory(outputDirectory);
                var output = Path.Combine(outputDirectory, "FishSocialDesktop.exe");

                if (IsFileLocked(output))
                {
                    FailBuild(
                        "构建输出文件正在被占用：\n" + output +
                        "\n\n请先退出正在运行的 FishSocialDesktop.exe，再重新构建。");
                    return;
                }

                var options = new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.Development | BuildOptions.AllowDebugging,
                };

                var report = BuildPipeline.BuildPlayer(options);
                if (report == null || report.summary.result != BuildResult.Succeeded)
                {
                    var result = report == null ? "未知错误" : report.summary.result.ToString();
                    var details = report == null
                        ? string.Empty
                        : $"\n错误数：{report.summary.totalErrors}，警告数：{report.summary.totalWarnings}";
                    FailBuild("Windows Development Build 失败：" + result + details +
                              "\n\n请查看 Unity Console 获取具体错误。");
                    return;
                }

                var steamAppIdSource = Path.Combine(projectRoot, SteamAppIdFile);
                if (File.Exists(steamAppIdSource))
                {
                    File.Copy(steamAppIdSource,
                        Path.Combine(outputDirectory, SteamAppIdFile), true);
                    Debug.Log("[Build] Copied Steam AppID file to the Windows build output.");
                }
                else
                {
                    Debug.LogWarning("[Build] Missing " + SteamAppIdFile +
                                     "; direct Steamworks launch may not initialize.");
                }

                Debug.Log("[Build] Windows Development Build OK → " + output);
                var smoke = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "STEAM-DESKTOP-04-smoke.md"));
                Debug.Log("[Build] Smoke checklist: " + smoke);
                if (!Application.isBatchMode)
                {
                    EditorUtility.DisplayDialog("Fish Social 构建完成",
                        "Windows Development Build 已生成：\n" + output, "确定");
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                FailBuild("构建过程中发生异常：\n" + error.Message +
                          "\n\nUnity 编辑器已保留，请查看 Console 和 Editor.log。");
            }
        }

        static bool IsFileLocked(string path)
        {
            if (!File.Exists(path))
                return false;

            try
            {
                using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    return false;
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return true;
            }
        }

        static void FailBuild(string message)
        {
            Debug.LogError("[Build] " + message);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Fish Social 构建失败", message, "确定");
            }
            else
            {
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Fish Social/Open Desktop Main Scene")]
        public static void OpenMainScene()
        {
            var scenePath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ScenePath));
            if (!File.Exists(scenePath))
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
