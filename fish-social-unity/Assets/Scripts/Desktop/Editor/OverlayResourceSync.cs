#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    /// <summary>Exports saved pond layouts and installs their resources beside the local Overlay.</summary>
    public static class OverlayResourceSync
    {
        [MenuItem("Fish Social/Overlay/更新钓鱼场景到运行目录", false, 0)]
        public static void UpdateFishingScene()
        {
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene.isDirty)
                throw new InvalidOperationException("请先保存当前 Prefab，再更新 Overlay 场景。");
            var error = OverlayLayoutExporter.ExportAll(out var count, false);
            if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            SyncExportedResources();
            Debug.Log("[OverlayResourceSync] Exported " + count + " saved pond layouts; restart Overlay to reload the installed resources.");
        }

        // Public for batch use when source resources have already been exported.
        public static void SyncExportedResources()
        {
            var project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var source = Path.GetFullPath(Path.Combine(project, "..", "desktop-overlay", "OverlayResources"));
            var destination = Path.Combine(project, "FishSocialOverlay", "OverlayResources");
            if (!Directory.Exists(source) || !File.Exists(Path.Combine(source, "layouts", "pond-novice.json")))
                throw new DirectoryNotFoundException("Overlay source resources are incomplete: " + source);
            var files = Directory.GetFiles(source, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)
                    && !f.EndsWith(".DS_Store", StringComparison.OrdinalIgnoreCase)).ToArray();
            var changed = 0;
            foreach (var file in files)
            {
                var relative = file.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var target = Path.Combine(destination, relative);
                if (File.Exists(target) && SameBytes(file, target)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
                if (!SameBytes(file, target)) throw new IOException("Resource copy verification failed: " + relative);
                changed++;
            }
            Debug.Log("[OverlayResourceSync] Verified " + files.Length + " resources; updated " + changed + " → " + destination);
            if (!Application.isBatchMode)
                EditorUtility.DisplayDialog("Overlay 场景已更新", "已同步布局与配套资源：\n" + destination
                    + "\n\n请重新启动 Overlay，进入对应鱼塘查看已更新的场景。", "确定");
        }

        static bool SameBytes(string left, string right)
        {
            if (new FileInfo(left).Length != new FileInfo(right).Length) return false;
            using (var a = File.OpenRead(left))
            using (var b = File.OpenRead(right))
            {
                var aa = new byte[65536];
                var bb = new byte[65536];
                int read;
                while ((read = a.Read(aa, 0, aa.Length)) != 0)
                {
                    var offset = 0;
                    while (offset < read)
                    {
                        var n = b.Read(bb, offset, read - offset);
                        if (n == 0) return false;
                        offset += n;
                    }
                    for (var i = 0; i < read; i++) if (aa[i] != bb[i]) return false;
                }
                return true;
            }
        }
    }
}
#endif
