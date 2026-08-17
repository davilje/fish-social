#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static class DesktopPrefabValidator
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs";

        static void Generate()
        {
            var generated = 0;
            var errors = string.Empty;

            generated += GenerateChildPrefab(
                "PanelOnlinePlayers",
                "PanelSocial",
                "OnlinePage",
                ref errors);
            generated += GenerateRowPrefab(
                "FriendRow",
                "Name",
                new[] { "Dm", "Remove" },
                ref errors);
            generated += GenerateRowPrefab(
                "FriendRequestRow",
                "Name",
                new[] { "Accept", "Reject" },
                ref errors);
            generated += GenerateRowPrefab(
                "SteamInviteRow",
                "Name",
                new[] { "Invite" },
                ref errors);
            generated += GenerateRowPrefab(
                "OnlinePlayerRow",
                "Name",
                new string[0],
                ref errors);
            generated += GenerateTextRowPrefab(
                "PondChatMessageRow",
                "Message",
                ref errors);
            generated += GenerateConversationPrefab(
                "DirectMessageConversationRow",
                ref errors);
            generated += GenerateTextRowPrefab(
                "DirectMessageRow",
                "Message",
                ref errors);
            generated += GenerateTextRowPrefab(
                "TextStatusRow",
                "Message",
                ref errors);
            generated += GenerateSlotPrefab("CatchSlot", ref errors);
            generated += GenerateSlotPrefab("GallerySpeciesSlot", ref errors);
            generated += GenerateWorldMapPrefab(ref errors);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            var message = generated + " 个独立 UI Prefab 已生成。";
            if (!string.IsNullOrEmpty(errors))
                message += "\n\n失败项目：" + errors;
            message += "\n\n生成目录：\n" + Folder;
            EditorUtility.DisplayDialog("Desktop UI Prefabs", message, "确定");
        }

        static void Validate()
        {
            var entries = new[]
            {
                new PrefabEntry("PanelSocial", typeof(DesktopSocialModalView)),
                new PrefabEntry("PanelCatch", typeof(DesktopCatchBagModalView)),
                new PrefabEntry("PanelGallery", typeof(DesktopGalleryModalView)),
                new PrefabEntry("PanelSettings", typeof(DesktopSettingsModalView)),
                new PrefabEntry("PanelWorldMap", typeof(DesktopWorldMapPanel)),
            };

            var errors = string.Empty;
            foreach (var entry in entries)
            {
                var path = Folder + "/" + entry.Name + ".prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                {
                    errors += "\n缺少 " + path;
                    continue;
                }
                if (prefab.GetComponent(entry.ComponentType) == null)
                    errors += "\n" + entry.Name + " 缺少 " + entry.ComponentType.Name;
            }

            var itemPrefabs = new[]
            {
                "PanelOnlinePlayers",
                "FriendRow",
                "FriendRequestRow",
                "SteamInviteRow",
                "OnlinePlayerRow",
                "PondChatMessageRow",
                "DirectMessageConversationRow",
                "DirectMessageRow",
                "TextStatusRow",
                "CatchSlot",
                "GallerySpeciesSlot",
            };
            for (var i = 0; i < itemPrefabs.Length; i++)
            {
                var path = Folder + "/" + itemPrefabs[i] + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    errors += "\n缺少 " + path;
            }
            var socialPath = Folder + "/PanelSocial.prefab";
            var social = AssetDatabase.LoadAssetAtPath<GameObject>(socialPath);
            if (social != null)
            {
                var root = social.transform;
                var friendsChatPrefix = root.Find("FriendsPage/FriendsChatPage") != null
                    ? "FriendsPage/FriendsChatPage/"
                    : "FriendsPage/";
                var isTwoPage = root.Find("PondPage") != null &&
                                root.Find("Tabs/T0") != null &&
                                root.Find("Tabs/T1") != null &&
                                root.Find("PondPage/OnlinePage/Scroll/Content") != null &&
                                root.Find("PondPage/ChatPage/Scroll/Content") != null &&
                                root.Find("PondPage/ChatPage/ChatInput") != null &&
                                root.Find("PondPage/ChatPage/SendChat") != null &&
                                root.Find("FriendsPage/Scroll/Content") != null &&
                                root.Find(friendsChatPrefix + "Messages/Content") != null &&
                                root.Find(friendsChatPrefix + "DmTitle") != null &&
                                root.Find(friendsChatPrefix + "DmInput") != null &&
                                root.Find(friendsChatPrefix + "SendDm") != null;
                var responsiveColumns = IsFullStretch(root) &&
                                        IsResponsiveColumn(root.Find("PondPage/OnlinePage"), false) &&
                                        IsResponsiveColumn(root.Find("PondPage/ChatPage"), true) &&
                                        IsResponsiveColumn(root.Find("FriendsPage/Scroll"), false) &&
                                        IsResponsiveColumn(root.Find(friendsChatPrefix.TrimEnd('/')), true);
                var isLegacy = root.Find("OnlinePage") != null &&
                               root.Find("ChatPage/Scroll/Content") != null &&
                               root.Find("FriendsPage/Scroll/Content") != null &&
                               root.Find("DmPage/Messages/Content") != null;
                if (!isTwoPage && !isLegacy)
                    errors += "\nPanelSocial 层级不符合双页规范，也不是可兼容的旧层级";
                else if (isTwoPage && !responsiveColumns)
                    errors += "\nPanelSocial 双页左右栏仍使用固定像素，请执行 Normalize PanelSocial Responsive Layout";
            }

            EditorUtility.DisplayDialog(
                "Desktop Prefabs",
                string.IsNullOrEmpty(errors)
                    ? "桌面功能 Prefab 均可用。\n\n" +
                      "修改方式：打开 Prefab → 修改 UI → Ctrl+S 保存 → 重新打包。"
                    : "Prefab 检查失败：" + errors,
                "确定");
        }

        static int GenerateWorldMapPrefab(ref string errors)
        {
            try
            {
                var root = new GameObject(
                    "PanelWorldMap",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(DesktopWorldMapPanel));
                var rootRect = root.GetComponent<RectTransform>();
                Stretch(rootRect);
                root.GetComponent<Image>().color = new Color(0.07f, 0.1f, 0.14f, 1f);

                var viewport = new GameObject(
                    "Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
                viewport.transform.SetParent(root.transform, false);
                var viewportRect = viewport.GetComponent<RectTransform>();
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = new Vector2(0.68f, 1f);
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = Vector2.zero;
                viewport.GetComponent<Image>().color = new Color(0.12f, 0.2f, 0.18f, 1f);

                var content = new GameObject("MapContent", typeof(RectTransform));
                content.transform.SetParent(viewport.transform, false);
                var contentRect = content.GetComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0.5f, 0.5f);
                contentRect.anchorMax = new Vector2(0.5f, 0.5f);
                contentRect.pivot = new Vector2(0.5f, 0.5f);
                contentRect.sizeDelta = new Vector2(2200f, 1300f);

                var map = new GameObject("MapImage", typeof(RectTransform), typeof(Image));
                map.transform.SetParent(content.transform, false);
                var mapRect = map.GetComponent<RectTransform>();
                Stretch(mapRect);
                map.GetComponent<Image>().color = new Color(0.16f, 0.3f, 0.24f, 1f);

                var markers = new GameObject("MarkerLayer", typeof(RectTransform));
                markers.transform.SetParent(content.transform, false);
                var markerRect = markers.GetComponent<RectTransform>();
                Stretch(markerRect);

                var details = new GameObject("Details", typeof(RectTransform), typeof(Image));
                details.transform.SetParent(root.transform, false);
                var detailsRect = details.GetComponent<RectTransform>();
                detailsRect.anchorMin = new Vector2(0.68f, 0f);
                detailsRect.anchorMax = Vector2.one;
                detailsRect.offsetMin = Vector2.zero;
                detailsRect.offsetMax = Vector2.zero;
                details.GetComponent<Image>().color = new Color(0.08f, 0.12f, 0.16f, 0.96f);
                var detailsText = CreateLabel(details.transform, "DetailsText");
                detailsText.text = "请选择地图上的鱼塘。";
                detailsText.fontSize = 18;
                detailsText.rectTransform.anchorMin = new Vector2(0.08f, 0.55f);
                detailsText.rectTransform.anchorMax = new Vector2(0.92f, 0.95f);
                detailsText.rectTransform.offsetMin = Vector2.zero;
                detailsText.rectTransform.offsetMax = Vector2.zero;
                var statusText = CreateLabel(details.transform, "StatusText");
                statusText.rectTransform.anchorMin = new Vector2(0.08f, 0.28f);
                statusText.rectTransform.anchorMax = new Vector2(0.92f, 0.5f);
                statusText.rectTransform.offsetMin = Vector2.zero;
                statusText.rectTransform.offsetMax = Vector2.zero;
                var enter = CreateButton(details.transform, "EnterPond", "进入鱼塘");
                var enterRect = enter.GetComponent<RectTransform>();
                enterRect.anchorMin = new Vector2(0.08f, 0.13f);
                enterRect.anchorMax = new Vector2(0.92f, 0.22f);
                enterRect.offsetMin = Vector2.zero;
                enterRect.offsetMax = Vector2.zero;
                var reset = CreateButton(details.transform, "ResetView", "重置地图");
                var resetRect = reset.GetComponent<RectTransform>();
                resetRect.anchorMin = new Vector2(0.08f, 0.04f);
                resetRect.anchorMax = new Vector2(0.92f, 0.11f);
                resetRect.offsetMin = Vector2.zero;
                resetRect.offsetMax = Vector2.zero;

                return SaveGeneratedPrefab(root, "PanelWorldMap", ref errors);
            }
            catch (System.Exception error)
            {
                errors += "\nPanelWorldMap：" + error.Message;
                return 0;
            }
        }

        static void NormalizePanelSocialResponsiveLayout()
        {
            const string path = Folder + "/PanelSocial.prefab";
            if (!EditorUtility.DisplayDialog(
                    "Normalize PanelSocial",
                    "此工具只会调整 PanelSocial 根页面和左右栏的 Anchor/Offset，" +
                    "不修改内容 Prefab，也不会运行时覆盖布局。\n\n" +
                    "当前 Prefab 的手动双栏比例将统一为左右各 50%，是否继续？",
                    "继续并保存",
                    "取消"))
                return;

            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                EditorUtility.DisplayDialog("Normalize PanelSocial", "找不到 " + path, "确定");
                return;
            }

            try
            {
                var social = root.transform;
                var pond = social.Find("PondPage");
                var friends = social.Find("FriendsPage");
                var online = social.Find("PondPage/OnlinePage");
                var pondChat = social.Find("PondPage/ChatPage");
                var friendList = social.Find("FriendsPage/Scroll");
                var friendChat = social.Find("FriendsPage/FriendsChatPage");
                if (pond == null || friends == null || online == null || pondChat == null ||
                    friendList == null || friendChat == null)
                    throw new System.InvalidOperationException(
                        "需要双页层级 PondPage/OnlinePage、PondPage/ChatPage、" +
                        "FriendsPage/Scroll、FriendsPage/FriendsChatPage。");

                Undo.RegisterFullObjectHierarchyUndo(root, "Normalize PanelSocial Responsive Layout");
                StretchRoot(social);
                SetHorizontalStretch(pond);
                SetHorizontalStretch(friends);
                SetColumn(online, false);
                SetColumn(pondChat, true);
                SetColumn(friendList, false);
                SetColumn(friendChat, true);
                NormalizeChatControls(pondChat);
                NormalizeChatControls(friendChat);

                EditorUtility.SetDirty(root);
                bool success;
                PrefabUtility.SaveAsPrefabAsset(root, path, out success);
                if (!success)
                    throw new System.InvalidOperationException("PrefabUtility 保存失败。");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "Normalize PanelSocial",
                    "已保存响应式双栏布局。可使用 Undo 撤销本次 Prefab 修改，" +
                    "然后在 Prefab Mode 检查手动视觉细节。",
                    "确定");
            }
            catch (System.Exception error)
            {
                Debug.LogError("[DesktopPrefabBaker] PanelSocial responsive normalization failed: " + error);
                EditorUtility.DisplayDialog("Normalize PanelSocial", error.Message, "确定");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static bool IsResponsiveColumn(Transform node, bool right)
        {
            var rect = node != null ? node.GetComponent<RectTransform>() : null;
            if (rect == null)
                return false;
            var expectedMin = right ? 0.5f : 0f;
            var expectedMax = right ? 1f : 0.5f;
            return Approximately(rect.anchorMin.x, expectedMin) &&
                   Approximately(rect.anchorMax.x, expectedMax) &&
                   Approximately(rect.offsetMin.x, 0f) &&
                   Approximately(rect.offsetMax.x, 0f);
        }

        static bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) < 0.001f;
        }

        static bool IsFullStretch(Transform node)
        {
            var rect = node != null ? node.GetComponent<RectTransform>() : null;
            return rect != null &&
                   Approximately(rect.anchorMin.x, 0f) &&
                   Approximately(rect.anchorMin.y, 0f) &&
                   Approximately(rect.anchorMax.x, 1f) &&
                   Approximately(rect.anchorMax.y, 1f) &&
                   Approximately(rect.offsetMin.x, 0f) &&
                   Approximately(rect.offsetMin.y, 0f) &&
                   Approximately(rect.offsetMax.x, 0f) &&
                   Approximately(rect.offsetMax.y, 0f);
        }

        static void StretchRoot(Transform node)
        {
            var rect = node.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void SetHorizontalStretch(Transform node)
        {
            var rect = node.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
            rect.offsetMin = new Vector2(0f, rect.offsetMin.y);
            rect.offsetMax = new Vector2(0f, rect.offsetMax.y);
        }

        static void SetColumn(Transform node, bool right)
        {
            var rect = node.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(right ? 0.5f : 0f, 0f);
            rect.anchorMax = new Vector2(right ? 1f : 0.5f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void NormalizeChatControls(Transform chatPage)
        {
            var input = chatPage.Find("ChatInput") ?? chatPage.Find("DmInput");
            var send = chatPage.Find("SendChat") ?? chatPage.Find("SendDm");
            if (input != null)
            {
                var rect = input.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.offsetMin = new Vector2(8f, 0f);
                rect.offsetMax = new Vector2(-88f, 40f);
            }
            if (send != null)
            {
                var rect = send.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 0f);
                rect.anchorMax = new Vector2(1f, 0f);
                rect.offsetMin = new Vector2(-80f, 0f);
                rect.offsetMax = new Vector2(0f, 40f);
            }
        }

        static void MigratePanelSocial()
        {
            const string path = Folder + "/PanelSocial.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                EditorUtility.DisplayDialog("PanelSocial Migration", "找不到 " + path, "确定");
                return;
            }

            try
            {
                if (root.transform.Find("PondPage") != null)
                {
                    EditorUtility.DisplayDialog(
                        "PanelSocial Migration",
                        "PanelSocial 已经是双栏页面，无需重复迁移。",
                        "确定");
                    return;
                }

                var online = root.transform.Find("OnlinePage");
                var chat = root.transform.Find("ChatPage");
                var friends = root.transform.Find("FriendsPage");
                var dm = root.transform.Find("DmPage");
                if (online == null || chat == null || friends == null || dm == null)
                    throw new System.InvalidOperationException(
                        "需要 OnlinePage、ChatPage、FriendsPage、DmPage 四个旧节点。");

                // Only change ownership of existing nodes. Preserve world-space UI geometry
                // while changing ownership so manual Prefab layout work is not overwritten.
                var pond = new GameObject("PondPage", typeof(RectTransform));
                pond.transform.SetParent(root.transform, false);
                ReparentPreserveLayout(online, pond.transform);
                ReparentPreserveLayout(chat, pond.transform);

                MoveChildPreserveLayout(dm, friends, "Messages");
                MoveChildPreserveLayout(dm, friends, "DmTitle");
                MoveChildPreserveLayout(dm, friends, "DmInput");
                MoveChildPreserveLayout(dm, friends, "SendDm");

                Object.DestroyImmediate(dm.gameObject);
                SetTabLabel(root.transform, "T0", "在线钓友/鱼塘聊天");
                SetTabLabel(root.transform, "T1", "好友/私聊");
                Object.DestroyImmediate(root.transform.Find("Tabs/T2")?.gameObject);
                Object.DestroyImmediate(root.transform.Find("Tabs/T3")?.gameObject);

                EditorUtility.SetDirty(root);
                bool success;
                PrefabUtility.SaveAsPrefabAsset(root, path, out success);
                if (!success)
                    throw new System.InvalidOperationException("PrefabUtility 保存失败。");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorUtility.DisplayDialog(
                    "PanelSocial Migration",
                    "已迁移为 PondPage 与 FriendsPage 两个页面。\n" +
                    "原有 RectTransform 布局未被脚本覆盖，请在 Prefab Mode 检查并按需微调双栏比例。",
                    "确定");
            }
            catch (System.Exception error)
            {
                Debug.LogError("[DesktopPrefabBaker] PanelSocial migration failed: " + error);
                EditorUtility.DisplayDialog("PanelSocial Migration", error.Message, "确定");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void MoveChildPreserveLayout(Transform source, Transform target, string name)
        {
            var child = source.Find(name);
            if (child == null)
                throw new System.InvalidOperationException("DmPage 缺少节点：" + name);
            ReparentPreserveLayout(child, target);
        }

        static void ReparentPreserveLayout(Transform child, Transform target)
        {
            if (child == null || target == null)
                throw new System.ArgumentNullException(child == null ? "child" : "target");
            child.SetParent(target, true);
        }

        static void SetTabLabel(Transform root, string tabName, string text)
        {
            var tab = root.Find("Tabs/" + tabName + "/Label");
            var label = tab != null ? tab.GetComponent<Text>() : null;
            if (label != null)
                label.text = text;
        }

        static int GenerateChildPrefab(
            string outputName,
            string sourcePrefabName,
            string childPath,
            ref string errors)
        {
            var sourcePath = Folder + "/" + sourcePrefabName + ".prefab";
            var outputPath = Folder + "/" + outputName + ".prefab";
            var sourceRoot = PrefabUtility.LoadPrefabContents(sourcePath);
            if (sourceRoot == null)
            {
                errors += "\n找不到源 Prefab：" + sourcePath;
                return 0;
            }

            try
            {
                var source = sourceRoot.transform.Find(childPath);
                if (source == null)
                {
                    errors += "\n" + sourcePrefabName + " 缺少节点：" + childPath;
                    return 0;
                }

                var root = new GameObject(outputName, typeof(RectTransform));
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.anchorMin = Vector2.zero;
                rootRect.anchorMax = Vector2.one;
                rootRect.offsetMin = Vector2.zero;
                rootRect.offsetMax = Vector2.zero;

                var copy = Object.Instantiate(source.gameObject);
                copy.name = source.name;
                copy.transform.SetParent(root.transform, false);
                var copyRect = copy.GetComponent<RectTransform>();
                if (copyRect != null)
                {
                    copyRect.anchorMin = Vector2.zero;
                    copyRect.anchorMax = Vector2.one;
                    copyRect.offsetMin = Vector2.zero;
                    copyRect.offsetMax = Vector2.zero;
                }

                bool success;
                PrefabUtility.SaveAsPrefabAsset(root, outputPath, out success);
                Object.DestroyImmediate(root);
                if (!success)
                {
                    errors += "\n保存失败：" + outputPath;
                    return 0;
                }

                Debug.Log("[DesktopPrefabBaker] Generated " + outputPath);
                return 1;
            }
            catch (System.Exception error)
            {
                errors += "\n" + outputName + "：" + error.Message;
                return 0;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(sourceRoot);
            }
        }

        static int GenerateRowPrefab(
            string outputName,
            string labelName,
            string[] buttons,
            ref string errors)
        {
            try
            {
                var root = CreateRowRoot(outputName);
                CreateLabel(root.transform, labelName);
                for (var i = 0; i < buttons.Length; i++)
                    CreateButton(root.transform, buttons[i], buttons[i]);
                return SaveGeneratedPrefab(root, outputName, ref errors);
            }
            catch (System.Exception error)
            {
                errors += "\n" + outputName + "：" + error.Message;
                return 0;
            }
        }

        static int GenerateTextRowPrefab(string outputName, string labelName, ref string errors)
        {
            try
            {
                var root = CreateRowRoot(outputName);
                CreateLabel(root.transform, labelName);
                return SaveGeneratedPrefab(root, outputName, ref errors);
            }
            catch (System.Exception error)
            {
                errors += "\n" + outputName + "：" + error.Message;
                return 0;
            }
        }

        static int GenerateConversationPrefab(string outputName, ref string errors)
        {
            try
            {
                var root = new GameObject(
                    outputName,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(HorizontalLayoutGroup),
                    typeof(LayoutElement));
                var element = root.GetComponent<LayoutElement>();
                element.minHeight = 44f;
                element.preferredHeight = 44f;
                var layout = root.GetComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(8, 8, 4, 4);
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                root.GetComponent<Image>().color = new Color(0.18f, 0.28f, 0.36f, 1f);
                CreateLabel(root.transform, "Name");
                return SaveGeneratedPrefab(root, outputName, ref errors);
            }
            catch (System.Exception error)
            {
                errors += "\n" + outputName + "：" + error.Message;
                return 0;
            }
        }

        static int GenerateSlotPrefab(string outputName, ref string errors)
        {
            try
            {
                var root = new GameObject(
                    outputName,
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(Button),
                    typeof(LayoutElement));
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(92f, 64f);
                var element = root.GetComponent<LayoutElement>();
                element.minWidth = 72f;
                element.preferredWidth = 92f;
                element.minHeight = 56f;
                element.preferredHeight = 64f;
                root.GetComponent<Image>().color = new Color(0.16f, 0.22f, 0.28f, 1f);
                CreateLabel(root.transform, "Label");
                return SaveGeneratedPrefab(root, outputName, ref errors);
            }
            catch (System.Exception error)
            {
                errors += "\n" + outputName + "：" + error.Message;
                return 0;
            }
        }

        static GameObject CreateRowRoot(string name)
        {
            var root = new GameObject(
                name,
                typeof(RectTransform),
                typeof(HorizontalLayoutGroup),
                typeof(LayoutElement));
            var element = root.GetComponent<LayoutElement>();
            element.minHeight = 44f;
            element.preferredHeight = 44f;
            var layout = root.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.padding = new RectOffset(8, 8, 4, 4);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return root;
        }

        static Text CreateLabel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var label = go.GetComponent<Text>();
            label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            label.text = "文本";
            label.fontSize = 15;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            var element = go.GetComponent<LayoutElement>();
            element.minHeight = 34f;
            element.preferredHeight = 34f;
            element.flexibleWidth = 1f;
            return label;
        }

        static Button CreateButton(Transform parent, string name, string labelText)
        {
            var go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.minWidth = 88f;
            element.preferredWidth = 88f;
            element.minHeight = 34f;
            element.preferredHeight = 34f;
            go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.55f, 1f);
            var label = CreateLabel(go.transform, "Label");
            label.text = labelText;
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform);
            return go.GetComponent<Button>();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static int SaveGeneratedPrefab(GameObject root, string outputName, ref string errors)
        {
            var outputPath = Folder + "/" + outputName + ".prefab";
            bool success;
            PrefabUtility.SaveAsPrefabAsset(root, outputPath, out success);
            Object.DestroyImmediate(root);
            if (!success)
            {
                errors += "\n保存失败：" + outputPath;
                return 0;
            }
            Debug.Log("[DesktopPrefabBaker] Generated " + outputPath);
            return 1;
        }

        sealed class PrefabEntry
        {
            public readonly string Name;
            public readonly System.Type ComponentType;

            public PrefabEntry(string name, System.Type componentType)
            {
                Name = name;
                ComponentType = componentType;
            }
        }
    }
}
#endif
