#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace FishSocial.Desktop.Editor
{
    /// <summary>
    /// Single registry for complete serialized desktop UI prefabs; repair never creates page shells.
    /// Do not add per-panel Bake MenuItems; extend this catalog instead.
    /// </summary>
    public static class DesktopPrefabCatalog
    {
        public const string Folder = "Assets/Resources/Desktop/Prefabs";
        public const string LibraryFolder = Folder + "/Library";

        public sealed class Entry
        {
            public readonly string Name;
            public readonly string Description;
            public readonly Type ComponentType;
            public readonly Action Ensure;

            public Entry(string name, string description, Type componentType, Action ensure = null)
            {
                Name = name;
                Description = description;
                ComponentType = componentType;
                Ensure = name == "OverlayPondLayout" ? ensure
                    : () => CozyPrefabRepair.Ensure(name, componentType);
            }
        }

        public static readonly Entry[] All =
        {
            new Entry("CozyButtonBase", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyButtonPrimary", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyButtonSecondary", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyButtonQuiet", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyButtonTab", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyButtonIcon", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyButtonNav", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyButtonDanger", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyPanelBase", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyPanelCard", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyPanelDialog", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyPanelSidebar", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyPanelToolbar", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyPanelTooltip", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyPanelHud", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyInputBase", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyInputSearch", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyInputMultiline", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyScrollBase", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyScrollList", "Cozy 可继承基础组件与 Variant。", null),
            new Entry("CozyScrollGrid", "Cozy 可继承基础组件与 Variant。", null),

            new Entry("PanelHome", "首页：猫咪、品牌菜单卡、会话状态和导航动作。", null),
            new Entry("PanelPond", "钓鱼页：猫咪、会话状态和钓鱼动作；背景由主壳承载。", null),
            new Entry("PanelPondSelection", "鱼塘选择：标题和可滚动鱼塘卡片。", typeof(CozyPondSelectionPanel)),
            new Entry("PanelEquipment", "装备搭配（序列化商店视图，绑定装备模式）。", typeof(DesktopShopPanel)),
            new Entry("PanelAchievements", "成就页（序列化个人中心成就视图）。", typeof(DesktopProfileHubPanel)),
            new Entry("PanelSocial", "好友、在线钓友、鱼塘聊天和私聊。", typeof(DesktopSocialModalView)),
            new Entry("PanelCatch", "背包/鱼获网格。", typeof(DesktopCatchBagModalView)),
            new Entry("PanelGallery", "图鉴物种网格。", typeof(DesktopGalleryModalView)),
            new Entry("PanelSettings", "桌面端设置。", typeof(DesktopSettingsModalView)),
            new Entry("PanelWorldMap", "世界地图。", typeof(DesktopWorldMapPanel)),
            new Entry("PanelShop", "商店。", typeof(DesktopShopPanel)),
            new Entry("PanelProfile", "（旧）个人资料。", typeof(DesktopProfilePanel)),
            new Entry("PanelProfileEdit", "资料编辑。", typeof(DesktopProfileEditPanel)),
            new Entry("PanelProfileHub", "个人中心 Hub。", typeof(DesktopProfileHubPanel)),
            new Entry("PanelSocialFeed", "动态墙。", typeof(DesktopSocialFeedPanel)),
            new Entry("PanelLeaderboard", "排行榜。", typeof(DesktopLeaderboardPanel)),
            new Entry("PanelPondSettlement", "离塘结算。", typeof(DesktopPondSettlementModalView)),
            new Entry("DesktopShell", "登录+主壳（顶栏/底栏/内容槽/Toast）。", null),
            new Entry("OverlayHud", "Overlay 960×560 HUD（导出 overlay-hud.json）。", typeof(DesktopOverlayHudView), () => DesktopPrefabValidator.EnsureOverlayHud()),
            new Entry("OverlayPondLayout", "每塘一份 Overlay 960×560 场景布局（导出 layouts/<pondId>.json）。", typeof(DesktopOverlayPondLayoutView), OverlayPondLayoutBaker.EnsureAll),
            new Entry("OverlayPondActor", "钓位角色位：座位/形象/昵称/状态icon/上钩进度环（可调位置尺寸）。", typeof(OverlayPondActorView), OverlayPondActorBaker.Ensure),
            new Entry("ProductContextMenu", "主窗口右键产品菜单。", null),
            new Entry("FeatureModalRoot", "已序列化的功能弹窗容器与完整内嵌页面。", null),
            new Entry("PondSelectionCard", "鱼塘选择卡：说明、费用、等级和地图动作。", null),
            new Entry("WorldMapPondMarker", "世界地图鱼塘标记：按钮、图标与标题。", null),
            new Entry("ShopItemCard", "商店列表商品卡。", null),
            new Entry("AchievementRow", "成就行。", null),
            new Entry("SocialPostCard", "动态卡片。", null),
            new Entry("PostCommentRow", "评论行。", null),
            new Entry("LeaderboardRow", "排行榜行。", null),
            new Entry("FriendRow", "好友行。", null),
            new Entry("FriendRequestRow", "好友申请行。", null),
            new Entry("SteamInviteRow", "Steam 邀请行。", null),
            new Entry("OnlinePlayerRow", "在线钓友行。", null),
            new Entry("PondChatMessageRow", "鱼塘聊天行。", null),
            new Entry("DirectMessageConversationRow", "私聊会话行。", null),
            new Entry("DirectMessageRow", "私聊消息行。", null),
            new Entry("TextStatusRow", "状态文本行。", null),
            new Entry("CatchSlot", "背包格。", null),
            new Entry("ShowcaseSlot", "展示格。", null),
            new Entry("AvatarChoice", "头像格。", null),
            new Entry("GallerySpeciesSlot", "图鉴格。", null),
        };

        public static string AssetPath(string name)
        {
            return (name.StartsWith("Cozy", StringComparison.Ordinal) ? LibraryFolder : Folder) + "/" + name + ".prefab";
        }

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
