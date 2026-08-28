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
        const string ReleaseOutputDir = "Builds/Windows64";
        const string DebugOutputDir = "Builds/Windows64-Debug";
        const string SteamAppIdFile = "steam_appid.txt";
        const string ServerJsonExampleFile = "server.json.example";
        const string OverlayProjectPath = "../desktop-overlay/FishSocialOverlay.csproj";
        const string OverlayOutputDir = "FishSocialOverlay";

        // Menu layout:
        // Fish Social/
        //   打包/ Debug · Release
        //   UI Prefab 管理

        [MenuItem("Fish Social/打包/Debug 包（Development + Overlay）", false, 10)]
        public static void BuildWindowsDevelopment()
        {
            BuildWindowsPlayer(true, true);
        }

        [MenuItem("Fish Social/打包/Release 包（+ Native Overlay）", false, 11)]
        public static void BuildWindowsWithNativeOverlay()
        {
            BuildWindowsPlayer(true, false);
        }

        static void BuildWindowsPlayer(bool includeNativeOverlay, bool development)
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
                var outputDirectory = Path.Combine(
                    projectRoot, development ? DebugOutputDir : ReleaseOutputDir);
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
                    options = development
                        ? BuildOptions.Development | BuildOptions.AllowDebugging
                        : BuildOptions.None,
                };

                var report = BuildPipeline.BuildPlayer(options);
                if (report == null || report.summary.result != BuildResult.Succeeded)
                {
                    var result = report == null ? "未知错误" : report.summary.result.ToString();
                    var details = report == null
                        ? string.Empty
                        : $"\n错误数：{report.summary.totalErrors}，警告数：{report.summary.totalWarnings}";
                    FailBuild("Windows Build 失败：" + result + details +
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

                var serverJsonExampleSource = Path.Combine(projectRoot, ServerJsonExampleFile);
                if (File.Exists(serverJsonExampleSource))
                {
                    File.Copy(serverJsonExampleSource,
                        Path.Combine(outputDirectory, ServerJsonExampleFile), true);
                    Debug.Log("[Build] Copied " + ServerJsonExampleFile +
                              " beside the Windows build output.");
                }
                else
                {
                    Debug.LogWarning("[Build] Missing " + ServerJsonExampleFile +
                                     "; copy manually for LAN/public server overrides.");
                }

                if (includeNativeOverlay &&
                    !PublishNativeOverlay(projectRoot, outputDirectory))
                    return;

                Debug.Log("[Build] Windows " + (development ? "Debug" : "Release") +
                          " Build OK → " + output);
                var smoke = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs", "STEAM-DESKTOP-04-smoke.md"));
                Debug.Log("[Build] Smoke checklist: " + smoke);
                if (!Application.isBatchMode)
                {
                    var kind = development
                        ? "Debug 包（Development + Overlay，含玩法 Debug 菜单）"
                        : "Release 包（不含玩法 Debug 入口）";
                    EditorUtility.DisplayDialog("Fish Social 构建完成",
                        kind + " 已生成：\n" + output, "确定");
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                FailBuild("构建过程中发生异常：\n" + error.Message +
                          "\n\nUnity 编辑器已保留，请查看 Console 和 Editor.log。");
            }
        }

        static bool PublishNativeOverlay(string projectRoot, string outputDirectory)
        {
            var projectPath = Path.GetFullPath(Path.Combine(projectRoot, OverlayProjectPath));
            var overlayOutput = Path.Combine(outputDirectory, OverlayOutputDir);
            if (!File.Exists(projectPath))
            {
                FailBuild("找不到 Native Overlay 项目：\n" + projectPath);
                return false;
            }

            try
            {
                if (Directory.Exists(overlayOutput))
                    Directory.Delete(overlayOutput, true);
                Directory.CreateDirectory(overlayOutput);

                var arguments =
                    "publish " + QuoteArgument(projectPath) +
                    " -c Release -r win-x64 --self-contained true -o " +
                    QuoteArgument(overlayOutput);
                using (var process = new System.Diagnostics.Process())
                {
                    process.StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = arguments,
                        WorkingDirectory = projectRoot,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                    };
                    process.Start();
                    var standardOutput = process.StandardOutput.ReadToEnd();
                    var standardError = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (process.ExitCode != 0)
                    {
                        FailBuild(
                            "Native Overlay 发布失败，退出码：" + process.ExitCode +
                            "\n\n" + standardOutput + "\n" + standardError);
                        return false;
                    }
                }

                var overlayExecutable = Path.Combine(overlayOutput, "FishSocialOverlay.exe");
                if (!File.Exists(overlayExecutable))
                {
                    FailBuild("Native Overlay 发布完成但找不到：\n" + overlayExecutable);
                    return false;
                }

                Debug.Log("[Build] Native Overlay OK → " + overlayExecutable);
                CopyOverlayResources(projectRoot, overlayOutput);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                FailBuild("Native Overlay 发布异常：\n" + error.Message);
                return false;
            }
        }

        static void CopyOverlayResources(string unityProjectRoot, string overlayOutput)
        {
            var repoRoot = Path.GetFullPath(Path.Combine(unityProjectRoot, ".."));
            var source = Path.Combine(repoRoot, "desktop-overlay", "OverlayResources");
            if (!Directory.Exists(source))
                return;

            CopyDirectory(source, Path.Combine(overlayOutput, "OverlayResources"));
            var pets = Path.Combine(source, "pets");
            if (Directory.Exists(pets))
            {
                CopyDirectory(pets, Path.Combine(unityProjectRoot, "Assets", "StreamingAssets", "Pet"));
            }
        }

        static void CopyDirectory(string source, string dest)
        {
            foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta"))
                    continue;
                var rel = file.Substring(source.Length).TrimStart(
                    Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var target = Path.Combine(dest, rel);
                var dir = Path.GetDirectoryName(target);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.Copy(file, target, true);
            }
        }

        static string QuoteArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
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
