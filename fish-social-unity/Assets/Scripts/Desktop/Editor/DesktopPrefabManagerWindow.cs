#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public sealed class DesktopPrefabManagerWindow : EditorWindow
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs";
        const string MenuPath = "Fish Social/UI Prefab 管理";

        static readonly HashSet<string> KnownNames = BuildKnownNames();
        Vector2 _scroll;
        bool _showCreate;
        string _newName = string.Empty;
        string _newDescription = string.Empty;
        int _newTemplate;
        string _notice =
            "Cozy 修复只保存已提供的完整 Prefab；缺失页面不会生成空壳。" +
            "保留用户节点和布局，只修复持久化 Sprite、Sliced 和按钮状态。";

        [MenuItem(MenuPath, false, 20)]
        static void Open()
        {
            var window = GetWindow<DesktopPrefabManagerWindow>();
            window.titleContent = new GUIContent("UI Prefab 管理");
            window.minSize = new Vector2(760f, 420f);
            window.Show();
        }

        void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.HelpBox(
                "桌面端 UI Prefab 唯一管理入口（DesktopPrefabCatalog）。" +
                "普通页面只修复持久化与校验，不重建布局；缺失时请导入完整序列化 Prefab。基础按钮、面板、输入框与各页 Variant 同目录管理；Overlay HUD 同步主题。",
                MessageType.Info);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (var i = 0; i < DesktopPrefabCatalog.All.Length; i++)
            {
                var entry = DesktopPrefabCatalog.All[i];
                DrawDefinition(new PrefabDefinition(entry.Name, entry.Description, entry.ComponentType));
            }
            DrawUnknownPrefabs();
            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(_notice))
                EditorGUILayout.HelpBox(_notice, MessageType.None);
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("Fish Social / UI Prefab 管理", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("刷新", EditorStyles.toolbarButton, GUILayout.Width(60f)))
                {
                    AssetDatabase.Refresh();
                    Repaint();
                }
                if (GUILayout.Button("新增 Prefab", EditorStyles.toolbarButton, GUILayout.Width(86f)))
                    _showCreate = !_showCreate;
                if (GUILayout.Button("检查/修复全部", EditorStyles.toolbarButton, GUILayout.Width(100f)))
                {
                    DesktopPrefabCatalog.EnsureAll();
                    _notice = "已按 Catalog 检查/修复完整 Prefab；缺失或不完整项请查看 Console。";
                    Repaint();
                }
                if (GUILayout.Button("一键更新", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    UpdateAll();
            }

            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("修复/烘焙 Cozy Sprite（保留布局）", EditorStyles.toolbarButton))
                {
                    try
                    {
                        CozyPrefabRepair.RepairAll();
                        _notice = "修复完成；拒绝保存的 Prefab 详见 Console。请继续执行持久化校验。";
                    }
                    catch (Exception exception)
                    {
                        _notice = "修复失败：" + exception.Message;
                        Debug.LogException(exception);
                    }
                }
                if (GUILayout.Button("校验 Cozy 持久化", EditorStyles.toolbarButton))
                {
                    CozyThemeValidationMenu.ValidateAssets();
                    var errors = CozyPrefabRepair.ValidateAll();
                    _notice = "Prefab 校验问题数：" + errors + "；素材问题请查看 Console。";
                }
            }

            if (_showCreate)
                DrawCreatePanel();
        }

        void DrawCreatePanel()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("新增 UI Prefab", EditorStyles.boldLabel);
                _newName = EditorGUILayout.TextField("Prefab 名称", _newName);
                _newDescription = EditorGUILayout.TextField("用途描述", _newDescription);
                _newTemplate = EditorGUILayout.Popup(
                    "基础模板",
                    _newTemplate,
                    new[] { "空 UI 根节点", "普通面板", "列表行", "按钮格子" });

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("取消", GUILayout.Width(70f)))
                        _showCreate = false;
                    if (GUILayout.Button("创建", GUILayout.Width(70f)))
                        CreatePrefab();
                }
            }
        }

        void DrawDefinition(PrefabDefinition definition)
        {
            var path = ResolvePrefabPath(definition.Name);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var status = GetStatus(prefab, definition);
            var statusText = status == PrefabStatus.Valid ? "有效" :
                status == PrefabStatus.Missing ? "缺失" : "无效";
            var statusColor = status == PrefabStatus.Valid ? Color.green :
                status == PrefabStatus.Missing ? Color.yellow : Color.red;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    var oldColor = GUI.color;
                    GUI.color = statusColor;
                    GUILayout.Label(statusText, GUILayout.Width(42f));
                    GUI.color = oldColor;
                    GUILayout.Label(definition.Name, EditorStyles.boldLabel, GUILayout.Width(220f));
                    GUILayout.Label(definition.Description, EditorStyles.wordWrappedLabel);

                    if (status == PrefabStatus.Valid)
                    {
                        if (GUILayout.Button("修复", GUILayout.Width(56f)))
                        {
                            PopulateDefinition(definition.Name);
                            AssetDatabase.Refresh();
                            Repaint();
                        }
                        var pending = HasPendingChanges(path);
                        var action = pending ? "更新" : "查看";
                        var tooltip = pending
                            ? "保存当前未进包的 Prefab 修改，不重置手动布局。"
                            : "打开 Prefab 查看当前布局。";
                        var oldBackground = GUI.backgroundColor;
                        if (pending)
                            GUI.backgroundColor = new Color(1f, 0.65f, 0.15f);
                        if (GUILayout.Button(new GUIContent(action, tooltip), GUILayout.Width(56f)))
                        {
                            if (pending)
                                UpdatePrefab(path);
                            else
                                ViewPrefab(path);
                        }
                        GUI.backgroundColor = oldBackground;
                    }
                    if (status == PrefabStatus.Invalid &&
                        GUILayout.Button("删除", GUILayout.Width(56f)))
                        DeletePrefab(definition.Name, path);
                }
                if (prefab == null)
                {
                    EditorGUILayout.LabelField("路径", path, EditorStyles.miniLabel);
                    EditorGUILayout.LabelField("请提供完整 Prefab；不会回退到旧 builder 或生成空壳。", EditorStyles.miniLabel);
                    if (definition.Name.StartsWith("Overlay", StringComparison.Ordinal) &&
                        GUILayout.Button("创建 Overlay", GUILayout.Width(110f)))
                    {
                        CreateDefinition(definition.Name);
                        AssetDatabase.Refresh();
                        Repaint();
                    }
                }
            }
        }

        static void CreateDefinition(string name)
        {
            var entry = DesktopPrefabCatalog.Find(name);
            if (entry == null)
            {
                Debug.LogWarning("[DesktopUI] Catalog 未登记：" + name + "。请先加入 DesktopPrefabCatalog。");
                return;
            }
            try
            {
                entry.Ensure?.Invoke();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError("[DesktopUI] Ensure " + name + " failed: " + ex);
                EditorUtility.DisplayDialog("检查/修复失败", name + "\n" + ex.Message, "确定");
            }
        }

        static void PopulateDefinition(string name)
        {
            CreateDefinition(name);
        }

        void DrawUnknownPrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { Folder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]).Replace("\\", "/");
                var name = Path.GetFileNameWithoutExtension(path);
                if (KnownNames.Contains(name))
                    continue;

                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    var oldColor = GUI.color;
                    GUI.color = Color.red;
                    GUILayout.Label("未登记", GUILayout.Width(42f));
                    GUI.color = oldColor;
                    GUILayout.Label(name, EditorStyles.boldLabel, GUILayout.Width(220f));
                    GUILayout.Label("不在当前 Steam 桌面 Prefab 清单中，建议确认后删除。",
                        EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("删除", GUILayout.Width(56f)))
                        DeletePrefab(name, path);
                }
            }
        }

        void UpdateAll()
        {
            var updated = 0;
            var skipped = 0;
            for (var i = 0; i < DesktopPrefabCatalog.All.Length; i++)
            {
                var path = DesktopPrefabCatalog.AssetPath(DesktopPrefabCatalog.All[i].Name);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    skipped++;
                    continue;
                }
                if (HasPendingChanges(path) && UpdatePrefab(path))
                    updated++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _notice = "一键更新完成：已更新 " + updated + " 个，跳过 " + skipped +
                      " 个缺失 Prefab。";
            Repaint();
        }

        static bool UpdatePrefab(string path)
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null &&
                string.Equals(stage.assetPath.Replace("\\", "/"), path,
                    StringComparison.OrdinalIgnoreCase))
            {
                PrepareForSave(stage.prefabContentsRoot, path);
                bool success;
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, path, out success);
                if (success)
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return success;
            }

            var selected = Selection.activeGameObject;
            var instanceRoot = selected != null
                ? PrefabUtility.GetNearestPrefabInstanceRoot(selected)
                : null;
            if (instanceRoot != null &&
                string.Equals(GetPrefabAssetPath(instanceRoot), path,
                    StringComparison.OrdinalIgnoreCase))
            {
                PrepareForSave(instanceRoot, path);
                PrefabUtility.ApplyPrefabInstance(instanceRoot, InteractionMode.UserAction);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                return true;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return false;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return true;
        }

        static void PrepareForSave(GameObject root, string path)
        {
            var entry = DesktopPrefabCatalog.Find(Path.GetFileNameWithoutExtension(path));
            CozyPrefabRepair.PrepareForSave(root, Path.GetFileNameWithoutExtension(path),
                entry != null ? entry.ComponentType : null);
        }

        static void ViewPrefab(string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null)
                AssetDatabase.OpenAsset(prefab);
        }

        static bool HasPendingChanges(string path)
        {
            var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null &&
                string.Equals(stage.assetPath.Replace("\\", "/"), path,
                    StringComparison.OrdinalIgnoreCase))
                return EditorUtility.IsDirty(stage.prefabContentsRoot);

            var selected = Selection.activeGameObject;
            var instanceRoot = selected != null
                ? PrefabUtility.GetNearestPrefabInstanceRoot(selected)
                : null;
            return instanceRoot != null &&
                   string.Equals(GetPrefabAssetPath(instanceRoot), path,
                       StringComparison.OrdinalIgnoreCase) &&
                   PrefabUtility.HasPrefabInstanceAnyOverrides(instanceRoot, false);
        }

        static string GetPrefabAssetPath(GameObject instanceRoot)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
            return source == null
                ? string.Empty
                : AssetDatabase.GetAssetPath(source).Replace("\\", "/");
        }

        void DeletePrefab(string name, string path)
        {
            if (!EditorUtility.DisplayDialog(
                    "删除无效 Prefab",
                    "确定删除 " + name + "？\n\n路径：" + path +
                    "\n\n此操作不会删除运行时代码，但可能导致引用该 Prefab 的页面缺失。",
                    "删除",
                    "取消"))
                return;

            if (!AssetDatabase.DeleteAsset(path))
            {
                _notice = "删除失败：" + path;
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _notice = "已删除无效 Prefab：" + name;
            Repaint();
        }

        void CreatePrefab()
        {
            var name = (_newName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(name))
            {
                _notice = "请输入 Prefab 名称。";
                return;
            }
            if (name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                _notice = "Prefab 名称包含非法字符或扩展名，请只填写名称。";
                return;
            }

            var path = DesktopPrefabCatalog.AssetPath(name);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                _notice = "Prefab 已存在：" + path;
                return;
            }

            var root = new GameObject(name, typeof(RectTransform));
            try
            {
                switch (_newTemplate)
                {
                    case 1:
                        root.AddComponent<Image>();
                        break;
                    case 2:
                        root.AddComponent<HorizontalLayoutGroup>();
                        root.AddComponent<LayoutElement>();
                        break;
                    case 3:
                        root.AddComponent<Image>();
                        root.AddComponent<Button>();
                        root.AddComponent<LayoutElement>();
                        break;
                }

                bool success;
                PrefabUtility.SaveAsPrefabAsset(root, path, out success);
                if (!success)
                {
                    _notice = "创建失败：" + path;
                    return;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _notice = "已创建 " + name + "。用途描述：" +
                          (string.IsNullOrEmpty(_newDescription) ? "未填写" : _newDescription);
                _newName = string.Empty;
                _newDescription = string.Empty;
                _showCreate = false;
            }
            finally
            {
                DestroyImmediate(root);
            }
        }

        static string ResolvePrefabPath(string name)
        {
            if (string.Equals(name, "OverlayPondLayout", StringComparison.Ordinal))
                return OverlayPondLayoutBaker.PrefabPath("pond-calm");
            return DesktopPrefabCatalog.AssetPath(name);
        }

        static PrefabStatus GetStatus(GameObject prefab, PrefabDefinition definition)
        {
            if (prefab == null)
                return PrefabStatus.Missing;
            if (prefab.GetComponent<RectTransform>() == null)
                return PrefabStatus.Invalid;
            if (definition.RequiredComponent != null &&
                prefab.GetComponent(definition.RequiredComponent) == null)
                return PrefabStatus.Invalid;
            if (definition.RequiredComponent == null &&
                prefab.GetComponentInChildren<Graphic>(true) == null)
                return PrefabStatus.Invalid;
            return PrefabStatus.Valid;
        }

        static HashSet<string> BuildKnownNames()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < DesktopPrefabCatalog.All.Length; i++)
                names.Add(DesktopPrefabCatalog.All[i].Name);
            return names;
        }

        enum PrefabStatus
        {
            Missing,
            Valid,
            Invalid,
        }

        sealed class PrefabDefinition
        {
            public readonly string Name;
            public readonly string Description;
            public readonly Type RequiredComponent;

            public PrefabDefinition(string name, string description, Type requiredComponent)
            {
                Name = name;
                Description = description;
                RequiredComponent = requiredComponent;
            }
        }
    }
}
#endif
