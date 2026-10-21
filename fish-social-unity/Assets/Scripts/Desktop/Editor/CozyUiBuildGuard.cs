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

        static void Require<T>(GameObject root, string nodePath, string assetPath, List<string> errors) where T : Component
        {
            var node = root.transform.Find(nodePath);
            if (node == null || node.GetComponent<T>() == null)
                errors.Add(assetPath + ": journal UI requires " + nodePath + " (" + typeof(T).Name + ").");
        }

        static void ValidateJournalStructure(GameObject root, string name, string path, List<string> errors)
        {
            if (name == "DesktopShell")
            {
                Require<Button>(root, "MainRoot/Header/WorldMapEntry", path, errors);
                var scaler = root.GetComponent<CanvasScaler>();
                if (scaler == null || scaler.referenceResolution != CozyJournalTheme.DesignResolution ||
                    scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.MatchWidthOrHeight || !Mathf.Approximately(scaler.matchWidthOrHeight, .5f))
                    errors.Add(path + ": journal shell requires the shared 1672×941, MatchWidthOrHeight(0.5) scaling contract.");
            }
            if (name == "PanelWorldMap" || name == "PanelPondSelection")
            {
                var prefix = name == "PanelWorldMap" ? "Details/" : "DesignPondAside/";
                Require<Image>(root, prefix + "Photo", path, errors);
                Require<Text>(root, prefix + "Name", path, errors);
                Require<Text>(root, prefix + "FeeSummary", path, errors);
                Require<Text>(root, prefix + "StatusText", path, errors);
                Require<ScrollRect>(root, prefix + "FactsScroll", path, errors);
                Require<Text>(root, prefix + "FactsScroll/" + (name == "PanelWorldMap" ? "DetailsText" : "Body"), path, errors);
                foreach (var action in new[] { "ModeSell", "ModeAuto", "Retry", name == "PanelWorldMap" ? "EnterPond" : "Enter" })
                    Require<Button>(root, prefix + action, path, errors);
                if (name == "PanelWorldMap") Require<Button>(root, "ListPonds", path, errors);
                else
                {
                    Require<InputField>(root, "Search", path, errors);
                    Require<Text>(root, "ResultCount", path, errors);
                    foreach (var action in new[] { "FilterAll", "FilterAccessible", "FilterFree", "ClearFilters" })
                        Require<Button>(root, action, path, errors);
                }
            }
            if (name == "PondSelectionCard") Require<Image>(root, "Photo", path, errors);
            var grain = root.transform.Find("JournalGrain")?.GetComponent<Image>();
            if (grain != null && (grain.type != Image.Type.Tiled || grain.material == null ||
                grain.material.shader == null || grain.material.shader.name != "FishSocial/UI/Journal Paper Multiply"))
                errors.Add(path + ": journal paper grain requires tiled Multiply rendering.");
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
                ValidateJournalStructure(root, entry.Name, path, errors);
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
