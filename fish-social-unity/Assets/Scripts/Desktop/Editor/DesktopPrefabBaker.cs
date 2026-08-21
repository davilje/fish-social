#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    public static class DesktopPrefabValidator
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs";

        public static void GeneratePanelProfilePrefabs()
        {
            GenerateNamedPanel(
                "PanelProfile",
                typeof(DesktopProfilePanel),
                "已创建 PanelProfile.prefab。运行时只绑定服务端资料，不覆盖 Prefab 布局。");
            GenerateNamedPanel(
                "PanelProfileEdit",
                typeof(DesktopProfileEditPanel),
                "已创建 PanelProfileEdit.prefab。运行时只绑定编辑状态，不覆盖 Prefab 布局。");
            var errors = string.Empty;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/ShowcaseSlot.prefab") == null)
                GenerateSlotPrefab("ShowcaseSlot", ref errors);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/AvatarChoice.prefab") == null)
                GenerateSlotPrefab("AvatarChoice", ref errors);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!string.IsNullOrEmpty(errors))
                EditorUtility.DisplayDialog("PanelProfile", "格子 Prefab：" + errors, "确定");
        }

        public static void PopulatePanelProfilePrefabs()
        {
            PopulateNamedPanel("PanelProfile", (root) =>
            {
                var panel = root.GetComponent<DesktopProfilePanel>();
                if (panel != null)
                    panel.BuildEditorLayout();
            });
            PopulateNamedPanel("PanelProfileEdit", (root) =>
            {
                var panel = root.GetComponent<DesktopProfileEditPanel>();
                if (panel != null)
                    panel.BuildEditorLayout();
            });
        }

        public static void GeneratePanelSocialFeedPrefab()
        {
            GenerateNamedPanel(
                "PanelSocialFeed",
                typeof(DesktopSocialFeedPanel),
                "已创建 PanelSocialFeed.prefab。运行时只绑定服务端动态数据，不覆盖 Prefab 布局。");
            var errors = string.Empty;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    Folder + "/SocialPostCard.prefab") == null)
                GenerateSocialPostCardPrefab(ref errors);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    Folder + "/PostCommentRow.prefab") == null)
                GeneratePostCommentRowPrefab(ref errors);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!string.IsNullOrEmpty(errors))
                Debug.LogError("[DesktopPrefabBaker] Social feed prefab: " + errors);
        }

        public static void GeneratePanelLeaderboardPrefab()
        {
            GenerateNamedPanel(
                "PanelLeaderboard",
                typeof(DesktopLeaderboardPanel),
                "已创建 PanelLeaderboard.prefab。运行时只绑定服务端排行榜数据，不覆盖 Prefab 布局。");
            var errors = string.Empty;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(
                    Folder + "/LeaderboardRow.prefab") == null)
                GenerateLeaderboardRowPrefab(ref errors);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (!string.IsNullOrEmpty(errors))
                Debug.LogError("[DesktopPrefabBaker] Leaderboard prefab: " + errors);
        }

        public static void PopulatePanelLeaderboardPrefab()
        {
            PopulateNamedPanel("PanelLeaderboard", root =>
            {
                var panel = root.GetComponent<DesktopLeaderboardPanel>();
                if (panel != null)
                    panel.BuildEditorLayout();
            });
            if (!HasLeaderboardRowStructure())
            {
                var errors = string.Empty;
                GenerateLeaderboardRowPrefab(ref errors);
                if (!string.IsNullOrEmpty(errors))
                    Debug.LogError("[DesktopPrefabBaker] Leaderboard row rebuild: " + errors);
            }
        }

        static bool HasLeaderboardRowStructure()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "/LeaderboardRow.prefab");
            return prefab != null &&
                   prefab.transform.Find("Rank") != null &&
                   prefab.transform.Find("Nickname") != null &&
                   prefab.transform.Find("Value") != null;
        }

        public static void PopulatePanelSocialFeedPrefab()
        {
            PopulateNamedPanel("PanelSocialFeed", root =>
            {
                var panel = root.GetComponent<DesktopSocialFeedPanel>();
                if (panel != null)
                    panel.BuildEditorLayout();
            });
            if (!HasSocialPostCardStructure())
            {
                var errors = string.Empty;
                GenerateSocialPostCardPrefab(ref errors);
                GeneratePostCommentRowPrefab(ref errors);
                if (!string.IsNullOrEmpty(errors))
                    Debug.LogError("[DesktopPrefabBaker] Social card rebuild: " + errors);
            }
        }

        static bool HasSocialPostCardStructure()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "/SocialPostCard.prefab");
            return prefab != null &&
                   prefab.GetComponent<DesktopSocialPostCard>() != null &&
                   prefab.transform.Find("Header/AuthorText") != null &&
                   prefab.transform.Find("Photo") != null &&
                   prefab.transform.Find("BodyText") != null &&
                   prefab.transform.Find("FishInfoText") != null &&
                   prefab.transform.Find("Actions/LikeButton") != null &&
                   prefab.transform.Find("Actions/CommentsButton") != null &&
                   prefab.transform.Find("CommentsPanel/CommentsContent") != null &&
                   prefab.transform.Find("CommentsPanel/CommentInput") != null &&
                   prefab.transform.Find("CommentsPanel/SendButton") != null;
        }

        public static void GeneratePanelShopPrefab()
        {
            const string outputName = "PanelShop";
            var path = Folder + "/" + outputName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                EditorUtility.DisplayDialog(
                    "PanelShop",
                    "PanelShop.prefab 已存在，未覆盖手动布局。",
                    "确定");
                return;
            }

            var root = new GameObject(
                outputName,
                typeof(RectTransform),
                typeof(Image),
                typeof(DesktopShopPanel));
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0.09f, 0.12f, 0.16f, 1f);
            var errors = string.Empty;
            var created = SaveGeneratedPrefab(root, outputName, ref errors);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "PanelShop",
                created == 1
                    ? "已创建 PanelShop.prefab。运行时只绑定服务端商店数据，不覆盖 Prefab 布局。"
                    : "创建失败：" + errors,
                "确定");
        }

        public static void PopulatePanelShopPrefab()
        {
            const string path = Folder + "/PanelShop.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError("[DesktopUI] PanelShop.prefab 不存在，请先创建。");
                return;
            }

            var panel = root.GetComponent<DesktopShopPanel>();
            if (panel != null)
                panel.BuildEditorLayout();
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        public static void PopulatePanelSettingsPrefab()
        {
            PopulateNamedPanel("PanelSettings", EnsurePanelSettingsControls);
        }

        static void EnsurePanelSettingsControls(GameObject root)
        {
            var content = DesktopModalUi.FindDescendant(root.transform, "SettingsContent");
            if (content == null)
            {
                Debug.LogError("[DesktopUI] PanelSettings.prefab 缺少 SettingsContent。");
                return;
            }

            const float shift = 220f;
            var windowFound = DesktopModalUi.FindDescendant(content, "窗口模式");
            var windowRt = windowFound != null ? windowFound.GetComponent<RectTransform>() : null;

            if (windowRt != null && windowRt.anchoredPosition.y > -200f)
            {
                for (var i = 0; i < content.childCount; i++)
                {
                    var child = content.GetChild(i) as RectTransform;
                    if (child == null)
                        continue;
                    var p = child.anchoredPosition;
                    child.anchoredPosition = new Vector2(p.x, p.y - shift);
                }
            }

            var contentRt = content as RectTransform ?? content.GetComponent<RectTransform>();
            if (contentRt != null && contentRt.sizeDelta.y < 980f)
                contentRt.sizeDelta = new Vector2(contentRt.sizeDelta.x, 980f);

            EnsureSettingsLabel(content, "服务器", "服务器", 20f, -16f, 400f, 28f, 20, Color.white, false);
            EnsureSettingsLabel(
                content, "当前服务器", "当前服务器：", 29f, -52f, -58f, 24f, 15,
                new Color(0.85f, 0.9f, 0.93f, 1f), true);
            EnsureSettingsInput(content, "服务器地址输入", "http://公网或局域网IP:3001", -84f);
            EnsureSettingsButton(content, "保存服务器地址", 29f, -128f, 200f, DesktopModalUi.Button);
            EnsureSettingsButton(content, "测试服务器连接", 241f, -128f, 200f, DesktopModalUi.Button);
            var police = DesktopModalUi.FindDescendant(content, "一键出警（Debug）");
            if (police != null)
                UnityEngine.Object.DestroyImmediate(police.gameObject);
            EnsureSettingsLabel(
                content, "服务器连接状态",
                "保存后请重启客户端；可用「测试服务器连接」检查 /health。",
                29f, -172f, -58f, 40f, 14, new Color(0.75f, 0.82f, 0.88f, 1f), true);
            EnsureSettingsButton(content, "重置新手引导", 29f, -220f, 280f, DesktopModalUi.Button);
        }

        static void EnsureSettingsLabel(
            Transform content,
            string name,
            string text,
            float x,
            float y,
            float width,
            float height,
            int fontSize,
            Color color,
            bool stretch)
        {
            var existing = DesktopModalUi.FindDescendant(content, name);
            Text label;
            RectTransform rt;
            if (existing == null)
            {
                label = DesktopModalUi.Label(content, name, text, fontSize, TextAnchor.MiddleLeft);
                rt = label.rectTransform;
            }
            else
            {
                label = existing.GetComponent<Text>();
                rt = existing.GetComponent<RectTransform>();
            }

            if (label != null)
            {
                label.text = text;
                label.fontSize = fontSize;
                label.color = color;
                label.alignment = TextAnchor.MiddleLeft;
            }

            PlaceSettingsRect(rt, x, y, width, height, stretch);
        }

        static void EnsureSettingsButton(
            Transform content, string name, float x, float y, float width, Color color)
        {
            var existing = DesktopModalUi.FindDescendant(content, name);
            RectTransform rt;
            if (existing == null)
            {
                var button = DesktopModalUi.MakeButton(content, name, name, () => { });
                button.onClick.RemoveAllListeners();
                rt = button.GetComponent<RectTransform>();
                var image = button.GetComponent<Image>();
                if (image != null)
                    image.color = color;
            }
            else
            {
                rt = existing.GetComponent<RectTransform>();
                var image = existing.GetComponent<Image>();
                if (image != null)
                    image.color = color;
            }

            PlaceSettingsRect(rt, x, y, width, 36f, false);
        }

        static void EnsureSettingsInput(Transform content, string name, string placeholder, float y)
        {
            var existing = DesktopModalUi.FindDescendant(content, name);
            RectTransform rt;
            if (existing == null)
            {
                var input = DesktopModalUi.MakeInput(content, name, placeholder, 256);
                rt = input.GetComponent<RectTransform>();
            }
            else
            {
                rt = existing.GetComponent<RectTransform>();
            }

            PlaceSettingsRect(rt, 29f, y, -58f, 36f, true);
        }

        static void PlaceSettingsRect(
            RectTransform rt, float x, float y, float width, float height, bool stretch)
        {
            if (rt == null)
                return;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = stretch ? new Vector2(1f, 1f) : new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(width, height);
        }

        static void GenerateNamedPanel(string outputName, System.Type componentType, string createdMessage)
        {
            var path = Folder + "/" + outputName + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
            {
                EditorUtility.DisplayDialog(outputName, outputName + ".prefab 已存在，未覆盖手动布局。", "确定");
                return;
            }

            var root = new GameObject(
                outputName,
                typeof(RectTransform),
                typeof(Image),
                componentType);
            Stretch(root.GetComponent<RectTransform>());
            root.GetComponent<Image>().color = new Color(0.09f, 0.12f, 0.16f, 1f);
            var errors = string.Empty;
            var created = SaveGeneratedPrefab(root, outputName, ref errors);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                outputName,
                created == 1 ? createdMessage : "创建失败：" + errors,
                "确定");
        }

        static void PopulateNamedPanel(string outputName, System.Action<GameObject> populate)
        {
            var path = Folder + "/" + outputName + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            if (root == null)
            {
                Debug.LogError("[DesktopUI] " + outputName + ".prefab 不存在，请先创建。");
                return;
            }

            if (populate != null)
                populate(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

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
            generated += GenerateSlotPrefab("ShowcaseSlot", ref errors);
            generated += GenerateSlotPrefab("AvatarChoice", ref errors);
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
                new PrefabEntry("PanelShop", typeof(DesktopShopPanel)),
                new PrefabEntry("PanelProfile", typeof(DesktopProfilePanel)),
                new PrefabEntry("PanelProfileEdit", typeof(DesktopProfileEditPanel)),
                new PrefabEntry("PanelSocialFeed", typeof(DesktopSocialFeedPanel)),
                new PrefabEntry("PanelLeaderboard", typeof(DesktopLeaderboardPanel)),
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
                "ShowcaseSlot",
                "AvatarChoice",
                "GallerySpeciesSlot",
                "SocialPostCard",
                "PostCommentRow",
                "LeaderboardRow",
            };
            for (var i = 0; i < itemPrefabs.Length; i++)
            {
                var path = Folder + "/" + itemPrefabs[i] + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    errors += "\n缺少 " + path;
            }
            if (!HasSocialPostCardStructure())
                errors += "\nSocialPostCard 不是完整数据绑定结构，请执行“初始化”";
            if (!HasLeaderboardRowStructure())
                errors += "\nLeaderboardRow 不是完整数据绑定结构，请执行“初始化”";
            var commentRow = AssetDatabase.LoadAssetAtPath<GameObject>(
                Folder + "/PostCommentRow.prefab");
            if (commentRow != null &&
                (commentRow.transform.Find("Text") == null ||
                 commentRow.transform.Find("Delete") == null))
                errors += "\nPostCommentRow 缺少 Text 或 Delete 节点";
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

        static int GenerateSocialPostCardPrefab(ref string errors)
        {
            const string path = Folder + "/SocialPostCard.prefab";
            try
            {
                AssetDatabase.DeleteAsset(path);
                var root = new GameObject(
                    "SocialPostCard",
                    typeof(RectTransform),
                    typeof(Image),
                    typeof(VerticalLayoutGroup),
                    typeof(LayoutElement),
                    typeof(DesktopSocialPostCard));
                root.GetComponent<Image>().color = new Color(0.13f, 0.18f, 0.23f, 1f);
                var rootLayout = root.GetComponent<VerticalLayoutGroup>();
                rootLayout.padding = new RectOffset(16, 16, 12, 12);
                rootLayout.spacing = 6f;
                rootLayout.childControlWidth = true;
                rootLayout.childControlHeight = true;
                rootLayout.childForceExpandWidth = true;

                var header = NewContainer(root.transform, "Header",
                    typeof(HorizontalLayoutGroup));
                var headerLayout = header.GetComponent<HorizontalLayoutGroup>();
                headerLayout.childControlWidth = true;
                headerLayout.childControlHeight = true;
                headerLayout.childForceExpandWidth = true;
                var author = CreateLabel(header.transform, "AuthorText");
                author.fontSize = 18;

                var photo = new GameObject("Photo", typeof(RectTransform), typeof(Image),
                    typeof(LayoutElement));
                photo.transform.SetParent(root.transform, false);
                photo.GetComponent<Image>().color = new Color(0.08f, 0.11f, 0.14f, 1f);
                var photoLayout = photo.GetComponent<LayoutElement>();
                photoLayout.minHeight = 150f;
                photoLayout.preferredHeight = 180f;

                var body = CreateLabel(root.transform, "BodyText");
                body.fontSize = 16;
                body.GetComponent<LayoutElement>().minHeight = 48f;
                var fishInfo = CreateLabel(root.transform, "FishInfoText");
                fishInfo.fontSize = 14;

                var actions = NewContainer(root.transform, "Actions",
                    typeof(HorizontalLayoutGroup));
                var actionLayout = actions.GetComponent<HorizontalLayoutGroup>();
                actionLayout.spacing = 12f;
                actionLayout.childControlWidth = false;
                actionLayout.childControlHeight = true;
                CreateButton(actions.transform, "LikeButton", "点赞 0");
                CreateButton(actions.transform, "CommentsButton", "评论 0");

                var comments = NewContainer(root.transform, "CommentsPanel",
                    typeof(Image), typeof(VerticalLayoutGroup));
                comments.GetComponent<Image>().color = new Color(0.09f, 0.13f, 0.17f, 1f);
                var commentsLayout = comments.GetComponent<VerticalLayoutGroup>();
                commentsLayout.padding = new RectOffset(8, 8, 8, 8);
                commentsLayout.spacing = 4f;
                commentsLayout.childControlWidth = true;
                commentsLayout.childControlHeight = true;
                var content = NewContainer(comments.transform, "CommentsContent",
                    typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                var contentLayout = content.GetComponent<VerticalLayoutGroup>();
                contentLayout.spacing = 4f;
                contentLayout.childControlWidth = true;
                contentLayout.childControlHeight = true;
                content.GetComponent<ContentSizeFitter>().verticalFit =
                    ContentSizeFitter.FitMode.PreferredSize;
                var input = CreateInput(comments.transform, "CommentInput",
                    "写评论（最多 200 字）");
                var send = CreateButton(comments.transform, "SendButton", "发送评论");
                comments.SetActive(false);

                var component = root.GetComponent<DesktopSocialPostCard>();
                var serialized = new SerializedObject(component);
                SetReference(serialized, "_authorText", author);
                SetReference(serialized, "_photo", photo.GetComponent<Image>());
                SetReference(serialized, "_bodyText", body);
                SetReference(serialized, "_fishInfoText", fishInfo);
                SetReference(serialized, "_likeButton", actions.transform.Find("LikeButton")
                    .GetComponent<Button>());
                SetReference(serialized, "_commentsButton", actions.transform.Find("CommentsButton")
                    .GetComponent<Button>());
                SetReference(serialized, "_commentsPanel", comments.gameObject);
                SetReference(serialized, "_commentsContent", content.GetComponent<RectTransform>());
                SetReference(serialized, "_commentInput", input);
                SetReference(serialized, "_sendCommentButton", send);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return SaveGeneratedPrefab(root, "SocialPostCard", ref errors);
            }
            catch (System.Exception error)
            {
                errors += "\nSocialPostCard：" + error.Message;
                return 0;
            }
        }

        static int GeneratePostCommentRowPrefab(ref string errors)
        {
            try
            {
                const string name = "PostCommentRow";
                AssetDatabase.DeleteAsset(Folder + "/" + name + ".prefab");
                var root = NewContainer(null, name, typeof(HorizontalLayoutGroup),
                    typeof(LayoutElement));
                var layout = root.GetComponent<HorizontalLayoutGroup>();
                layout.spacing = 8f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                var element = root.GetComponent<LayoutElement>();
                element.minHeight = 32f;
                element.preferredHeight = 32f;
                var text = CreateLabel(root.transform, "Text");
                text.GetComponent<LayoutElement>().flexibleWidth = 1f;
                CreateButton(root.transform, "Delete", "删除");
                return SaveGeneratedPrefab(root, name, ref errors);
            }
            catch (System.Exception error)
            {
                errors += "\nPostCommentRow：" + error.Message;
                return 0;
            }
        }

        static int GenerateLeaderboardRowPrefab(ref string errors)
        {
            try
            {
                const string name = "LeaderboardRow";
                AssetDatabase.DeleteAsset(Folder + "/" + name + ".prefab");
                var root = NewContainer(null, name, typeof(Image),
                    typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                root.GetComponent<Image>().color = new Color(0.13f, 0.18f, 0.23f, 1f);
                var layout = root.GetComponent<HorizontalLayoutGroup>();
                layout.padding = new RectOffset(10, 10, 6, 6);
                layout.spacing = 10f;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                var element = root.GetComponent<LayoutElement>();
                element.minHeight = 40f;
                element.preferredHeight = 40f;
                CreateLabel(root.transform, "Rank").text = "#4";
                CreateLabel(root.transform, "Nickname").text = "钓友";
                CreateLabel(root.transform, "Value").text = "成绩";
                return SaveGeneratedPrefab(root, name, ref errors);
            }
            catch (System.Exception error)
            {
                errors += "\nLeaderboardRow：" + error.Message;
                return 0;
            }
        }

        static GameObject NewContainer(
            Transform parent, string name, params System.Type[] extraComponents)
        {
            var types = new System.Type[1 + extraComponents.Length];
            types[0] = typeof(RectTransform);
            for (var i = 0; i < extraComponents.Length; i++)
                types[i + 1] = extraComponents[i];
            var root = new GameObject(name, types);
            if (parent != null)
                root.transform.SetParent(parent, false);
            return root;
        }

        static InputField CreateInput(Transform parent, string name, string placeholder)
        {
            var root = new GameObject(name, typeof(RectTransform), typeof(Image),
                typeof(InputField), typeof(LayoutElement));
            root.transform.SetParent(parent, false);
            root.GetComponent<Image>().color = new Color(0.16f, 0.21f, 0.26f, 1f);
            var element = root.GetComponent<LayoutElement>();
            element.minHeight = 34f;
            element.preferredHeight = 34f;
            var input = root.GetComponent<InputField>();
            var text = CreateLabel(root.transform, "Text");
            var hint = CreateLabel(root.transform, "Placeholder");
            hint.text = placeholder;
            hint.color = new Color(0.65f, 0.7f, 0.74f, 1f);
            Stretch(text.rectTransform);
            Stretch(hint.rectTransform);
            input.textComponent = text;
            input.placeholder = hint;
            return input;
        }

        static void SetReference(SerializedObject serialized, string name, UnityEngine.Object value)
        {
            var property = serialized.FindProperty(name);
            if (property != null)
                property.objectReferenceValue = value;
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
