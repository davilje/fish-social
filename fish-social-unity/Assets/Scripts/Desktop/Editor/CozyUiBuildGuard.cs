#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public sealed class CozyUiBuildGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            var errors = CollectErrors();
            if (errors.Count > 0)
                throw new BuildFailedException("Cozy UI prefab validation failed before Player build:\n" + string.Join("\n", errors));
        }

        public static List<string> CollectErrors()
        {
            var errors = new List<string>();
            foreach (var entry in DesktopPrefabCatalog.All)
            {
                if (entry.Name == "OverlayPondLayout")
                    continue;
                var path = DesktopPrefabCatalog.AssetPath(entry.Name);
                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null)
                {
                    errors.Add(path + ": required serialized prefab is missing or cannot be imported.");
                    continue;
                }
                foreach (var message in CozyPrefabRepair.ValidateContents(root, entry.Name, entry.ComponentType))
                    errors.Add(path + ": " + message);
                if (!entry.Name.EndsWith("Base", StringComparison.Ordinal)
                    && PrefabUtility.GetPrefabAssetType(root) != PrefabAssetType.Variant)
                    errors.Add(path + ": expected a persistent Prefab Variant, not a detached page copy.");
                foreach (var scroll in root.GetComponentsInChildren<ScrollRect>(true))
                {
                    if (scroll.GetComponents<ScrollRect>().Length != 1)
                        errors.Add(path + ": " + scroll.name + " contains duplicate ScrollRect components.");
                    if (scroll.content == null || scroll.viewport == null || !scroll.content.IsChildOf(scroll.viewport))
                        errors.Add(path + ": " + scroll.name + " requires a viewport and a content RectTransform inside it.");
                }
            }
            return errors;
        }
    }
}
#endif
