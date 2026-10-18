#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace FishSocial.Desktop.Editor
{
    public static class CozyRedesignPreview
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs/";
        static readonly string[] Pages = { "PanelLeaderboard", "PanelProfileHub", "PanelAchievements", "PanelSocial", "PanelCatch", "PanelEquipment", "PanelShop", "PanelSocialFeed", "PanelSettings", "PanelPondSelection", "PanelWorldMap", "PanelGallery" };

        [MenuItem("Fish Social/Cozy Redesign/Render Saved Prefab Previews")]
        public static void RenderAll()
        {
            Directory.CreateDirectory("Library/CozyRedesignPreviews");
            foreach (var name in Pages)
                Render(name);
            Render("PanelProfileEdit");
            foreach (var tab in new[] { "展示柜", "图鉴", "相册" })
                Render("PanelProfileHub", tab);
        }

        static void Render(string name, string tab = null)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(1672, 941, 24);
            var previous = RenderTexture.active;
            Camera camera = null;
            try
            {
                var cameraObject = new GameObject("PreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.white;
                camera.orthographic = true;
                camera.orthographicSize = 470.5f;
                camera.transform.position = new Vector3(836, 470.5f, -100);
                camera.targetTexture = target;
                var shell = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "DesktopShell.prefab"));
                SceneManager.MoveGameObjectToScene(shell, scene);
                var canvas = shell.GetComponent<Canvas>() ?? shell.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                var scaler = shell.GetComponent<CanvasScaler>();
                if (scaler != null) scaler.enabled = false;
                shell.transform.Find("LoginRoot").gameObject.SetActive(false);
                var main = shell.transform.Find("MainRoot");
                main.gameObject.SetActive(true);
                foreach (Transform slot in main.Find("Content")) slot.gameObject.SetActive(false);
                main.Find("Header/Leaderboard").gameObject.SetActive(false);
                main.Find("Header/Home").gameObject.SetActive(false);
                CozyItemArt.Text(main, "Header/ProfileCard/Name", "小橘");
                CozyItemArt.Text(main, "Header/ProfileCard/Level", "Lv.7");
                CozyItemArt.Text(main, "Header/ProfileCard/XpLabel", "320 / 500");
                DesktopModalUi.SetXpFill(DesktopModalUi.FindXpFill(main.Find("Header")), 320f / 500f);
                CozyItemArt.Text(main, "Header/FishCoins/Amount", "1,280");
                CozyItemArt.Text(main, "Header/CatchCount/Amount", "24");
                var page = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab"), main.Find("Content"), false);
                var rect = page.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
                rect.anchoredPosition = new Vector2(160, -150);
                rect.sizeDelta = new Vector2(1370, 650);
                Fill(page, name, tab);
                CatTheme.SkinTree(shell.transform);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                image.Apply();
                var pixels = image.GetPixels32();
                var visible = false;
                foreach (var pixel in pixels)
                    if (pixel.r < 240 || pixel.g < 240 || pixel.b < 240) { visible = true; break; }
                if (!visible)
                    throw new InvalidOperationException("Preview camera rendered no visible UI: " + name);
                File.WriteAllBytes("Library/CozyRedesignPreviews/" + name + (tab == null ? "" : "-" + tab) + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static GameObject Item(string prefab, Transform parent)
        {
            var item = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + prefab + ".prefab"), parent, false);
            item.SetActive(true);
            return item;
        }

        static void Select(GameObject item, bool selected)
        {
            var button = item.GetComponent<Button>();
            if (button == null)
                return;
            DesktopModalUi.StyleButton(button);
            button.GetComponent<CozyButtonVisual>()?.SetSelected(selected);
        }

        static void Art(GameObject item, Sprite sprite, bool silhouette)
        {
            var image = item.transform.Find("DesignItemArt")?.GetComponent<Image>();
            var label = item.transform.Find("Label") as RectTransform;
            if (image == null || label == null)
                return;
            CozyItemArt.Set(item.transform, "DesignItemArt", sprite);
            if (sprite != null && silhouette)
                image.color = new Color(CatTheme.Ink.r, CatTheme.Ink.g, CatTheme.Ink.b, 0.22f);
            label.anchorMax = new Vector2(1f, sprite != null ? 0.42f : 1f);
        }

        static void Fill(GameObject page, string name, string tab = null)
        {
            var root = page.transform;
            var fishIds = new[] { "black_carp", "koi", "largemouth_bass", "grass_carp", "carp", "snakehead" };
            var fishNames = new[] { "青鱼", "锦鲤", "鲈鱼", "草鱼", "鲤鱼", "黑鱼" };
            if (name == "PanelProfileEdit")
            {
                root.Find("Form/Nickname").GetComponent<InputField>().text = "小橘";
                root.Find("Form/Bio").GetComponent<InputField>().text = "一根鱼竿，一个悠闲下午。";
                foreach (var input in root.Find("Form").GetComponentsInChildren<InputField>())
                {
                    input.ForceLabelUpdate();
                    if (input.placeholder != null) input.placeholder.gameObject.SetActive(false);
                    input.textComponent.text = input.name == "Nickname" ? "小橘" : "一根鱼竿，一个悠闲下午。";
                }
                foreach (var avatar in DesktopDefaultAvatars.All)
                {
                    var choice = Item("AvatarChoice", root.Find("Avatars/Viewport/Content"));
                    CozyItemArt.Set(choice.transform, "DesignItemArt", DesktopDefaultAvatars.Portrait(avatar.Path));
                    CozyItemArt.Text(choice.transform, "Label", avatar.Label);
                    Select(choice, avatar.Id == "orange");
                }
                for (var index = 0; index < 8; index++)
                {
                var slot = Item("ShowcaseSlot", root.Find("Showcase/Viewport/Content"));
                    CozyItemArt.Text(slot.transform, "Label", index < 6 ? fishNames[index] : "空位");
                    CozyItemArt.Set(slot.transform, "DesignItemArt", index < 6 ? CozyItemArt.Fish(fishIds[index]) : null);
                }
            }
            if (name == "PanelWorldMap")
            {
                CozyItemArt.Text(root, "Details/DetailsText", "花溪鱼塘\n\n平静淡水 · 适合新手\n等级要求：无\n入场费：免费");
                CozyItemArt.Text(root, "Details/StatusText", "选择回鱼模式，前往垂钓");
                var layer = root.Find("Viewport/MapContent/MarkerLayer");
                for (var index = 0; index < 3; index++)
                {
                    var marker = Item("WorldMapPondMarker", layer);
                    var rect = marker.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
                    rect.anchoredPosition = new Vector2(150 + index * 240, -160 - index % 2 * 160);
                    CozyItemArt.Text(marker.transform, "Label", new[] { "花溪鱼塘", "青石溪流", "远山湖泊" }[index]);
                }
            }
            if (name == "PanelCatch")
            {
                var grid = root.Find("Grid/Viewport/Slots");
                for (var index = 0; index < 6; index++)
                {
                    var item = Item("CatchSlot", grid);
                    CozyItemArt.Set(item.transform, "DesignItemArt", CozyItemArt.Fish(fishIds[index]));
                    CozyItemArt.Text(item.transform, "Label", fishNames[index] + "\n0." + (86 - index * 6) + " m");
                    Select(item, index == 0);
                }
                CozyItemArt.Set(root, "DesignArt", CozyItemArt.Fish("black_carp"));
                CozyItemArt.Text(root, "DesignName", "青鱼");
                CozyItemArt.Rows(root, "DetailRows", "star", "品质", "稀有", "hook", "体长", "0.86 m",
                    "fish", "重量", "7.9 kg（约 15.8 斤）", "coin", "参考售价", "120 金币", "pond", "回鱼", "约 36 金币");
                CozyItemArt.Text(root, "Detail", "");
            }
            if (name == "PanelPondSelection")
            {
                var cards = root.Find("PondCardScroll/Viewport/Cards");
                var ponds = new[] { "樱花池塘", "青石溪流", "远山湖泊" };
                for (var index = 0; index < ponds.Length; index++)
                {
                    var card = Item("PondSelectionCard", cards);
                    CozyItemArt.Text(card.transform, "Name", ponds[index]);
                    CozyItemArt.Text(card.transform, "Description", "淡水 · 温带\n适合新手的平静水域");
                    CozyItemArt.Text(card.transform, "Fee", "入场费：" + (index * 20) + " 金币 / 2 小时");
                    CozyItemArt.Text(card.transform, "Level", index == 0 ? "等级要求：无" : "需要钓鱼等级 " + index * 5);
                    CozyItemArt.Text(card.transform, "Access", index == 2 ? "暂未开放\n前往地图查看开放条件" : "前往地图查看开放条件");
                    CozyItemArt.Set(card.transform, "PondIcon", CatTheme.LoadCozy(index == 2 ? "icons/lock.png" : "icons/pond.png", Vector4.zero));
                    DesktopModalUi.StyleTree(card.transform);
                }
            }
            if (name == "PanelShop" || name == "PanelEquipment")
            {
                var equipment = name == "PanelEquipment";
                var names = equipment ? new[] { "竹制钓竿", "轻巧钓竿", "木柄钓竿", "远投钓竿" } :
                    new[] { "通用鱼饵", "麦香鱼饵", "香甜鱼饵", "鲜虾鱼饵", "混合鱼饵", "颗粒鱼饵" };
                for (var index = 0; index < names.Length; index++)
                {
                    var item = Item("ShopItemCard", root.Find("Items/Viewport/Content"));
                    CozyItemArt.Set(item.transform, "DesignItemArt", CozyItemArt.Product(equipment ? 1 : 0, null, names[index]));
                    CozyItemArt.Text(item.transform, "Label", names[index]);
                    CozyItemArt.Text(item.transform, "Meta", equipment ? "已拥有" : "每次使用扣金");
                    Select(item, index == 0);
                }
                Select(root.Find(equipment ? "Tabs/Tackle" : "Tabs/Bait").gameObject, true);
                CozyItemArt.Set(root, "Details/DesignArt", CozyItemArt.Product(equipment ? 1 : 0, null, names[0]));
                CozyItemArt.Text(root, "Details/DesignName", names[0]);
                CozyItemArt.Text(root, "Details/DescriptionScroll/Description", equipment ? "状态  已拥有\n适合悠闲的日常垂钓。" : "基础鱼饵\n咬钩成功时按规则扣金。\n鱼饵不进货，没有库存。");
                root.Find("Details/Buy").gameObject.SetActive(!equipment);
                root.Find("Details/Equip").gameObject.SetActive(equipment);
            }
            if (name == "PanelProfileHub" || name == "PanelAchievements")
            {
                var content = root.Find("Shell/Content");
                foreach (Transform child in content) child.gameObject.SetActive(false);
                var achievements = name == "PanelAchievements";
                var selected = content.Find(tab != null ? "Tab_" + tab : achievements ? "Tab_成就" : "Tab_资料");
                selected.gameObject.SetActive(true);
                var sidebarTab = root.Find("Shell/Sidebar/" + selected.name);
                if (sidebarTab != null)
                    Select(sidebarTab.gameObject, true);
                if (tab == "展示柜")
                {
                    for (var index = 0; index < 8; index++)
                    {
                        var item = Item("ShowcaseSlot", selected.Find("ShowcaseGrid/Viewport/Content"));
                        CozyItemArt.Text(item.transform, "Label", index < fishNames.Length ? fishNames[index] + "\n稀有  0." + (86 - index * 6) + "m" : "空位 " + (index + 1));
                        Art(item, index < fishIds.Length ? CozyItemArt.Fish(fishIds[index]) : null, false);
                        Select(item, index == 0);
                    }
                    CozyItemArt.Text(selected, "Detail", "青鱼");
                    CozyItemArt.Set(selected, "DesignDetailArt", CozyItemArt.Fish("black_carp"));
                    CozyItemArt.Rows(selected, "ShowcaseDetailRows",
                        "star", "品质", "稀有",
                        "hook", "体长", "0.86 m",
                        "fish", "重量", "7.9 kg",
                        "clock", "捕获时间", "2026-10-15 14:32",
                        "pond", "来源塘", "花溪鱼塘",
                        "leaf", "食性", "杂食",
                        "medal", "脱钩率", "12.0%",
                        "rod", "推荐鱼饵", "蚯蚓");
                }
                else if (tab == "图鉴")
                {
                    var species = DesktopGameData.Species;
                    for (var index = 0; index < species.Length; index++)
                    {
                        var item = Item("ShowcaseSlot", selected.Find("CodexGrid/Viewport/Content"));
                        CozyItemArt.Text(item.transform, "Label", index < 8 ? "✓ " + species[index].name + "  ×" + (index + 2) : "？？？");
                        Art(item, CozyItemArt.Fish(species[index].speciesId), index >= 8);
                    }
                    CozyItemArt.Set(selected, "DesignDetailArt", CozyItemArt.Fish("crucian"));
                    CozyItemArt.Text(selected, "Detail", "鲫鱼\n\n推荐鱼饵：通用鱼饵\n\n累计捕获：2\n最大体长：0.28 m\n已解锁 8 / " + species.Length);
                }
                else if (tab == "相册")
                {
                    CozyItemArt.Text(selected, "AlbumStage/SlotL/Body", "青鱼\n稀有  ·  0.86m\n\n来源塘：花溪鱼塘\n记录：2026-10-15 14:32\n大鱼");
                    CozyItemArt.Text(selected, "AlbumStage/SlotR/Body", "锦鲤\n史诗  ·  0.72m\n\n来源塘：花溪鱼塘\n记录：2026-10-14 09:10\n首次图鉴");
                    CozyItemArt.Text(selected, "Page", "1/5");
                }
                else if (achievements)
                {
                    var titles = new[] { "初出茅庐\n首次将鱼收入背包", "图鉴新芽\n图鉴解锁达到 5 种", "博物渔者\n图鉴解锁达到 20 种", "大鱼临门\n单次捕获尺寸达到 0.8 m" };
                    for (var index = 0; index < titles.Length; index++)
                    {
                        var item = Item("AchievementRow", selected.Find("Achievements/Viewport/Content"));
                        var split = titles[index].IndexOf('\n');
                        CozyItemArt.Text(item.transform, "Label", "<b><size=23>" + titles[index].Substring(0, split) + "</size></b>" + titles[index].Substring(split));
                        CozyItemArt.Text(item.transform, "DesignState", index < 2 ? "已完成" : "未完成");
                        item.transform.Find("DesignState").GetComponent<Text>().color = index < 2 ? CozyItemArt.CompletedText : CatTheme.Ink;
                        item.transform.Find("DesignStateBadge").GetComponent<Image>().color = index < 2 ? CozyItemArt.CompletedBadge : CozyItemArt.PendingBadge;
                        item.transform.Find("Reflow_AchievementSeal").GetComponent<Image>().color = new Color(1f, 1f, 1f, index < 2 ? 1f : 0.45f);
                    }
                    CozyItemArt.Text(root, "DesignAchievementSummary/Value", "8 / 24");
                }
                else
                {
                    CozyItemArt.Set(selected, "Avatar", CatTheme.LoadCozy("characters/portraits/orange.png", Vector4.zero));
                    CozyItemArt.Text(selected, "DesignName", "小橘");
                    CozyItemArt.Text(selected, "DesignBio", "今天也想钓到一条大鱼");
                    CozyItemArt.Text(selected, "DesignId", "ID：102408");
                    CozyItemArt.Rows(selected, "DesignStatCards", "trophy", "钓鱼等级", "Lv.7", "star", "经验", "320 XP", "fish", "图鉴收集", "18 / 50");
                    CozyItemArt.Rows(selected, "DesignInfoRows", "coin", "金币", "1280", "social", "动态可见范围", "公开");
                    CozyItemArt.Set(selected, "DesignShowcase/Art", CozyItemArt.Fish("black_carp"));
                    CozyItemArt.Text(selected, "DesignShowcase/Species", "青鱼");
                    CozyItemArt.Text(selected, "DesignShowcase/Size", "0.86 m");
                }
            }
            if (name == "PanelLeaderboard")
            {
                for (var rank = 1; rank <= 3; rank++)
                {
                    var slot = root.Find("Podium/Slot" + rank);
                    CozyItemArt.Text(slot, "Nickname", rank == 1 ? "小橘" : rank == 2 ? "溪边小白" : "橘子汽水");
                    CozyItemArt.Text(slot, "Value", fishNames[rank - 1] + " · 0." + (92 - rank * 6) + " m");
                    CozyItemArt.Set(slot, "Avatar", CatTheme.LoadCozy("characters/portraits/orange.png", Vector4.zero));
                }
                for (var rank = 4; rank <= 6; rank++)
                {
                    var row = Item("LeaderboardRow", root.Find("Scroll/Viewport/Content"));
                    CozyItemArt.Text(row.transform, "Rank", rank.ToString());
                    CozyItemArt.Text(row.transform, "Nickname", "钓友 " + rank);
                    CozyItemArt.Text(row.transform, "Value", "鲤鱼 · 0.62 m");
                }
                CozyItemArt.Text(root, "MyRank", "我的排名：第 12 名");
            }
            if (name == "PanelSocialFeed")
            {
                var post = Item("SocialPostCard", root.Find("Scroll/Viewport/Content"));
                CozyItemArt.Text(post.transform, "Header/AuthorText", "橘子汽水 · 今天 14:32");
                CozyItemArt.Text(post.transform, "BodyText", "今天的第一条锦鲤，送给悠闲的下午。");
                CozyItemArt.Set(post.transform, "Photo", CatTheme.LoadCozy("scenes/home-sakura.png", Vector4.zero));
                CozyItemArt.Text(post.transform, "FishInfoText", "锦鲤 · 0.72 m · 花溪鱼塘");
            }
            if (name == "PanelSocial")
            {
                root.Find("PondPage").gameObject.SetActive(false);
                root.Find("FriendsPage").gameObject.SetActive(true);
            }
        }
    }
}
#endif
