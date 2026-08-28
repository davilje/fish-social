#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    /// <summary>
    /// Single registry for desktop UI prefabs. Create = generate shell + populate layout.
    /// Do not add per-panel Bake MenuItems; extend this catalog instead.
    /// </summary>
    public static class DesktopPrefabCatalog
    {
        public const string Folder = "Assets/Resources/Desktop/Prefabs";

        public sealed class Entry
        {
            public readonly string Name;
            public readonly string Description;
            public readonly Type ComponentType;
            public readonly Action Ensure; // create if missing + populate structure

            public Entry(string name, string description, Type componentType, Action ensure)
            {
                Name = name;
                Description = description;
                ComponentType = componentType;
                Ensure = ensure;
            }
        }

        public static readonly Entry[] All =
        {
            new Entry("PanelSocial", "好友、在线钓友、鱼塘聊天和私聊。", typeof(DesktopSocialModalView),
                () => DesktopPrefabValidator.EnsurePanelSocial()),
            new Entry("PanelCatch", "背包/鱼获网格。", typeof(DesktopCatchBagModalView),
                () => DesktopPrefabValidator.EnsureNamedPanelExists("PanelCatch", typeof(DesktopCatchBagModalView))),
            new Entry("PanelGallery", "图鉴物种网格。", typeof(DesktopGalleryModalView),
                () => DesktopPrefabValidator.EnsureNamedPanelExists("PanelGallery", typeof(DesktopGalleryModalView))),
            new Entry("PanelSettings", "桌面端设置。", typeof(DesktopSettingsModalView),
                () =>
                {
                    DesktopPrefabValidator.EnsureNamedPanelExists("PanelSettings", typeof(DesktopSettingsModalView));
                    DesktopPrefabValidator.PopulatePanelSettingsPrefab();
                }),
            new Entry("PanelWorldMap", "世界地图。", typeof(DesktopWorldMapPanel),
                () => DesktopPrefabValidator.EnsurePanelWorldMap()),
            new Entry("PanelShop", "商店。", typeof(DesktopShopPanel),
                () =>
                {
                    DesktopPrefabValidator.GeneratePanelShopPrefab();
                    DesktopPrefabValidator.PopulatePanelShopPrefab();
                    DesktopPrefabValidator.EnsureShopItemCard();
                }),
            new Entry("PanelProfile", "（旧）个人资料。", typeof(DesktopProfilePanel),
                () =>
                {
                    DesktopPrefabValidator.GeneratePanelProfilePrefabs();
                    DesktopPrefabValidator.PopulatePanelProfilePrefabs();
                }),
            new Entry("PanelProfileEdit", "资料编辑。", typeof(DesktopProfileEditPanel),
                () =>
                {
                    DesktopPrefabValidator.GeneratePanelProfilePrefabs();
                    DesktopPrefabValidator.PopulatePanelProfilePrefabs();
                }),
            new Entry("PanelProfileHub", "个人中心 Hub。", typeof(DesktopProfileHubPanel),
                () => DesktopPrefabValidator.BakePanelProfileHub()),
            new Entry("PanelSocialFeed", "动态墙。", typeof(DesktopSocialFeedPanel),
                () =>
                {
                    DesktopPrefabValidator.GeneratePanelSocialFeedPrefab();
                    DesktopPrefabValidator.PopulatePanelSocialFeedPrefab();
                }),
            new Entry("PanelLeaderboard", "排行榜。", typeof(DesktopLeaderboardPanel),
                () =>
                {
                    DesktopPrefabValidator.GeneratePanelLeaderboardPrefab();
                    DesktopPrefabValidator.PopulatePanelLeaderboardPrefab();
                }),
            new Entry("PanelPondSettlement", "离塘结算。", typeof(DesktopPondSettlementModalView),
                () => DesktopPrefabValidator.BakePanelPondSettlement()),
            new Entry("DesktopShell", "登录+主壳（顶栏/底栏/内容槽/Toast）。", null,
                () => DesktopPrefabValidator.BakeDesktopShell()),
            new Entry("ProductContextMenu", "主窗口右键产品菜单。", null,
                () => DesktopPrefabValidator.EnsureProductContextMenu()),
            new Entry("ShopItemCard", "商店列表商品卡。", null,
                () => DesktopPrefabValidator.EnsureShopItemCard()),
            new Entry("AchievementRow", "成就行。", null,
                () => DesktopPrefabValidator.BakePanelProfileHub()),
            new Entry("SocialPostCard", "动态卡片。", null,
                () =>
                {
                    DesktopPrefabValidator.GeneratePanelSocialFeedPrefab();
                    DesktopPrefabValidator.PopulatePanelSocialFeedPrefab();
                }),
            new Entry("PostCommentRow", "评论行。", null,
                () =>
                {
                    DesktopPrefabValidator.GeneratePanelSocialFeedPrefab();
                    DesktopPrefabValidator.PopulatePanelSocialFeedPrefab();
                }),
            new Entry("LeaderboardRow", "排行榜行。", null,
                () =>
                {
                    DesktopPrefabValidator.GeneratePanelLeaderboardPrefab();
                    DesktopPrefabValidator.PopulatePanelLeaderboardPrefab();
                }),
            new Entry("FriendRow", "好友行。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("FriendRequestRow", "好友申请行。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("SteamInviteRow", "Steam 邀请行。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("OnlinePlayerRow", "在线钓友行。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("PondChatMessageRow", "鱼塘聊天行。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("DirectMessageConversationRow", "私聊会话行。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("DirectMessageRow", "私聊消息行。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("TextStatusRow", "状态文本行。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("CatchSlot", "背包格。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("ShowcaseSlot", "展示格。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("AvatarChoice", "头像格。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
            new Entry("GallerySpeciesSlot", "图鉴格。", null, () => DesktopPrefabValidator.EnsureSocialRows()),
        };

        public static Entry Find(string name)
        {
            for (var i = 0; i < All.Length; i++)
            {
                if (string.Equals(All[i].Name, name, StringComparison.Ordinal))
                    return All[i];
            }
            return null;
        }

        public static void EnsureAll()
        {
            for (var i = 0; i < All.Length; i++)
            {
                try
                {
                    All[i].Ensure?.Invoke();
                }
                catch (Exception ex)
                {
                    Debug.LogError("[DesktopPrefabCatalog] Ensure failed: " + All[i].Name + " — " + ex);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
#endif
