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

        static void Fill(GameObject page, string name, string tab = null)
        {
            var root = page.transform;
            var fishIds = new[] { "black_carp", "koi", "largemouth_bass", "grass_carp", "carp", "snakehead" };
            var fishNames = new[] { "青鱼", "锦鲤", "鲈鱼", "草鱼", "鲤鱼", "黑鱼" };
            if (name == "PanelCatch")
            {
                var grid = root.Find("Grid/Viewport/Slots");
                for (var index = 0; index < 6; index++)
                {
                    var item = Item("CatchSlot", grid);
                    CozyItemArt.Set(item.transform, "DesignItemArt", CozyItemArt.Fish(fishIds[index]));
                    CozyItemArt.Text(item.transform, "Label", fishNames[index] + "\n0." + (86 - index * 6) + " m");
                }
                CozyItemArt.Set(root, "DesignArt", CozyItemArt.Fish("black_carp"));
                CozyItemArt.Text(root, "Detail", "青鱼\n尺寸  0.86 m\n品质  稀有\n来源  花溪鱼塘\n售价  120 金币");
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
                }
                CozyItemArt.Set(root, "Details/DesignArt", CozyItemArt.Product(equipment ? 1 : 0, null, names[0]));
                CozyItemArt.Text(root, "Details/DescriptionScroll/Description", names[0] + (equipment ? "\n状态  已拥有\n适合悠闲的日常垂钓。" : "\n基础鱼饵\n咬钩成功时按规则扣金。\n鱼饵不进货，没有库存。"));
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
                if (tab == "展示柜")
                {
                    for (var index = 0; index < 8; index++)
                    {
                        var item = Item("ShowcaseSlot", selected.Find("ShowcaseGrid/Viewport/Content"));
                        CozyItemArt.Text(item.transform, "Label", index < fishNames.Length ? fishNames[index] + "\n稀有\n0." + (86 - index * 6) + "m" : "空位 " + (index + 1));
                    }
                    CozyItemArt.Text(selected, "Detail", "点击左侧格子查看鱼获资料。");
                }
                else if (tab == "图鉴")
                {
                    for (var index = 0; index < 12; index++)
                    {
                        var item = Item("ShowcaseSlot", selected.Find("CodexGrid/Viewport/Content"));
                        CozyItemArt.Text(item.transform, "Label", index < fishNames.Length ? "✓ " + fishNames[index] + "\n×" + (index + 2) : "？？？");
                    }
                    CozyItemArt.Text(selected, "Detail", "已解锁 6 / 50\n\n点击左侧格子查看物种资料与捕获记录。");
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
                        CozyItemArt.Text(item.transform, "Label", titles[index]);
                        CozyItemArt.Text(item.transform, "DesignState", index < 2 ? "已完成" : "未完成");
                    }
                    CozyItemArt.Text(root, "DesignAchievementSummary/Value", "8 / 24");
                }
                else
                {
                    CozyItemArt.Set(selected, "Avatar", CatTheme.LoadCozy("characters/portraits/orange.png", Vector4.zero));
                    CozyItemArt.Text(selected, "DesignName", "小橘");
                    CozyItemArt.Text(selected, "DesignBio", "今天也想钓到一条大鱼");
                    CozyItemArt.Text(selected, "DesignId", "ID：102408");
                    CozyItemArt.Text(selected, "DesignStats", "钓鱼等级  7    XP  320\n图鉴收集  18 / 50");
                    CozyItemArt.Text(selected, "DesignPrivacy", "金币  1280\n动态可见范围  公开");
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
