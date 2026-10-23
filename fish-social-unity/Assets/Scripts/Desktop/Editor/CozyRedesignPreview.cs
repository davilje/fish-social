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
            Render("PanelProfileEdit", "展示鱼获");
            Render("PanelProfileEdit", "选择鱼获");
            Render("PanelPondSettlement");
            Render("PanelSocialFeed", "发布");
            foreach (var tab in new[] { "展示柜", "图鉴", "相册" })
                Render("PanelProfileHub", tab);
            foreach (var section in new[] { "好友申请", "私信", "鱼塘聊天", "消息通知" })
                Render("PanelSocial", section);
        }

        public static void RenderWorkspace()
        {
            Directory.CreateDirectory("Library/CozyRedesignPreviews");
            Render("PanelProfileHub");
            Render("PanelProfileEdit");
            Render("PanelProfileEdit", "展示鱼获");
            Render("PanelProfileEdit", "选择鱼获");
            Render("PanelSocialFeed", "发布");
            Render("PanelPondSettlement");
        }

        public static void RenderMotionAll()
        {
            Directory.CreateDirectory("Library/CozyRedesignPreviews");
            foreach(var seconds in new[]{0f,.03f,.06f,.1f,.16f,.2f}) Render("PanelShop",null,"index",seconds);
            foreach(var seconds in new[]{0f,.06f,.11f,.18f,.22f}) Render("PanelLeaderboard",null,"rank",seconds);
        }

        public static void RenderWorkspaceMotion()
        {
            Directory.CreateDirectory("Library/CozyRedesignPreviews");
            var reduced = CozyMotion.Reduced;
            var paused = CozyMotion.Paused;
            try
            {
                foreach (var seconds in new[] { 0f, .1f, .2f, .3f, .4f, .65f })
                {
                    Render("PanelProfileHub", null, "workspace-profile", seconds);
                    Render("PanelProfileEdit", null, "workspace-edit", seconds);
                    Render("PanelProfileEdit", null, "workspace-edit-refresh", seconds);
                    Render("PanelProfileHub", null, "workspace-profile-refresh", seconds);
                    Render("PanelProfileHub", null, "workspace-save-refresh", seconds);
                    Render("PanelProfileEdit", "选择鱼获", "workspace-picker", seconds);
                    Render("PanelSocialFeed", "发布", "workspace-compose", seconds);
                    Render("PanelPondSettlement", null, "workspace-settlement", seconds);
                }
            }
            finally { CozyMotion.SetReduced(reduced); CozyMotion.SetPaused(paused); }
        }

        static void Render(string name, string tab = null, string motionCase = null, float seconds = 0)
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
                CozyItemArt.Text(main, "Header/ProfileCard/XpLabel", "320 / 650");
                DesktopModalUi.SetXpFill(DesktopModalUi.FindXpFill(main.Find("Header")), 320f / 650f);
                CozyItemArt.Text(main, "Header/FishCoins/Amount", "8,600");
                CozyItemArt.Text(main, "Header/CatchCount/Amount", "24");
                var page = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab"), main.Find("Content"), false);
                var rect = page.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
                rect.anchoredPosition = new Vector2(160, -150);
                rect.sizeDelta = new Vector2(1370, 650);
                if (name == "PanelPondSettlement")
                {
                    page.transform.SetParent(shell.transform, false);
                    DesktopModalUi.Stretch(page);
                    page.transform.SetAsLastSibling();
                }
                Fill(page, name, tab);
                if (name == "PanelProfileEdit") CozyProfileRefinementPreview.FillEdit(page,tab=="展示鱼获"?"showcase":tab=="选择鱼获"?"picker":"avatars");
                if (name == "PanelPondSettlement") CozyWorkspaceSettlementAuthoring.Fill(page);
                CozyProfileRefinementPreview.Fill(page, tab);
                CozySocialRefinementPreview.Fill(page);
                if(name=="PanelSocial")
                    page.GetComponent<DesktopSocialModalView>().PreviewSection(tab=="好友申请"?SocialSection.Requests:tab=="私信"?SocialSection.DirectMessages:tab=="鱼塘聊天"?SocialSection.PondChat:tab=="消息通知"?SocialSection.Notifications:SocialSection.Friends);
                var navName=name=="PanelCatch"?"CatchBag":name=="PanelShop"?"Shop":name=="PanelEquipment"?"Equipment":name=="PanelAchievements"?"Achievements":name=="PanelLeaderboard"?"Leaderboard":name=="PanelSocialFeed"?"SocialFeed":name=="PanelProfileHub"||name=="PanelProfileEdit"?"Profile":null;
                var nav=main.Find("Nav");
                foreach(var b in nav.GetComponentsInChildren<Button>(true)) b.GetComponent<CozyButtonVisual>()?.SetSelected(b.name==navName);
                CozyMotion.MoveBobber(nav,navName!=null?nav.Find(navName) as RectTransform:null);
                nav.Find("CozyBobber")?.GetComponent<CozyBobberNavigation>()?.Advance(1);

                if (tab == "发布")
                {
                    var composer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "JournalComposeModal.prefab"), shell.transform, false);
                    composer.SetActive(true);
                    var ids=new[]{"koi","mandarin","grass_carp","golden_line","carp","crucian","bream","silver_carp"};
                    var sizes=new[]{.62f,.38f,.84f,.31f,.45f,.23f,.34f,.51f};
                    var inventory=new FishSocial.Desktop.Auth.FishInventoryItemDto[ids.Length];
                    for(var i=0;i<ids.Length;i++) inventory[i]=new FishSocial.Desktop.Auth.FishInventoryItemDto {id="workspace-fish-"+i,speciesId=ids[i],sizeM=sizes[i],quality=i==0?"red":"purple",caughtAt=1792654938000-i*660000,pondId="pond-calm"};
                    composer.GetComponent<DesktopSocialComposeModal>().PresentPreview(inventory,"小橘","/image/profile/cat_avatar_orange.png","锦鲤真的来了！这份好运，分给今天在湖边的每一位钓友。");
                }
                CatTheme.SkinTree(shell.transform);
                var bindings = new System.Text.StringBuilder();
                foreach (var button in page.GetComponentsInChildren<Button>(true))
                {
                    var visual = button.GetComponent<CozyButtonVisual>();
                    if (visual == null) continue;
                    var so = new SerializedObject(visual);
                    var plate = so.FindProperty("plate").objectReferenceValue as Image;
                    bindings.AppendLine(AnimationUtility.CalculateTransformPath(button.transform, page.transform) + " | " + so.FindProperty("surfaceNormal").stringValue + " | " + (plate != null ? AssetDatabase.GetAssetPath(plate.sprite) : "null"));
                }
                File.WriteAllText("Library/CozyRedesignPreviews/" + name + ".bindings.txt", bindings.ToString());
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
                CozyJournalInlineContent.ReflowTree(shell.transform);
                foreach (var pose in shell.GetComponentsInChildren<CozyJournalStaticPose>(true)) pose.ApplyFinalPose();
                foreach (var visual in shell.GetComponentsInChildren<CozyButtonVisual>(true)) visual.ApplyFinalPose();
                foreach (var motion in shell.GetComponentsInChildren<CozyStickerMotion>(true)) motion.ApplyFinalPose();
                if(motionCase=="index")
                {
                    var old=page.transform.Find("Tabs/Tackle").GetComponent<CozyButtonVisual>();
                    var next=page.transform.Find("Tabs/Bait").GetComponent<CozyButtonVisual>();
                    old.SetSelected(true); next.SetSelected(false); old.ApplyFinalPose(); next.ApplyFinalPose();
                    old.SetSelected(false); next.SetSelected(true);
                    old.AdvanceIndex(seconds); next.AdvanceIndex(seconds);
                }
                if(motionCase=="rank")
                {
                    var panel=page.GetComponent<DesktopLeaderboardPanel>();
                    panel.SelectPlayer(new FishSocial.Desktop.Auth.LeaderboardEntryDto {playerId="cat-snow",nickname="小白",rank=2,value=1.16f,avatarUrl="/image/profile/cat_avatar_white.png"});
                    panel.SampleMotion(seconds);
                }
                if (motionCase != null && motionCase.StartsWith("workspace-"))
                {
                    CozyMotion.SetPaused(false); CozyMotion.SetReduced(false);
                    if (motionCase == "workspace-profile" || motionCase == "workspace-edit" ||
                        motionCase == "workspace-edit-refresh" || motionCase == "workspace-profile-refresh" || motionCase == "workspace-save-refresh")
                    {
                        // Use the same saved slots and routing entry as DesktopShellUi.
                        // Keep the rendered design coordinates while moving under its real slot.
                        var id = motionCase.StartsWith("workspace-edit") ? ShellPanelId.ProfileEdit : ShellPanelId.Profile;
                        var slot = main.Find("Content/" + id);
                        page.transform.SetParent(slot, true);
                        page.SetActive(true);
                        var router = shell.GetComponent<PanelRouter>() ?? shell.AddComponent<PanelRouter>();
                        router.Register(ShellPanelId.Home, main.Find("Content/Home").gameObject);
                        router.Register(id, slot.gameObject);
                        if (motionCase.EndsWith("-refresh"))
                        {
                            var previousId = id == ShellPanelId.Profile ? ShellPanelId.ProfileEdit : ShellPanelId.Profile;
                            var previousSlot = main.Find("Content/" + previousId);
                            var previousName = previousId == ShellPanelId.Profile ? "PanelProfileHub" : "PanelProfileEdit";
                            var previousPage = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + previousName + ".prefab"), previousSlot, false);
                            previousPage.SetActive(true);
                            Fill(previousPage,previousName);
                            if(previousName=="PanelProfileEdit")CozyProfileRefinementPreview.FillEdit(previousPage,"avatars");
                            CozyProfileRefinementPreview.Fill(previousPage,null);
                            CatTheme.SkinTree(previousPage.transform);
                            var previousRect=(RectTransform)previousPage.transform;
                            previousRect.anchorMin=previousRect.anchorMax=previousRect.pivot=Vector2.up;
                            previousRect.sizeDelta=new Vector2(1370,650);
                            previousRect.position=rect.position;
                            router.Register(previousId, previousSlot.gameObject);
                            router.Show(previousId);
                            CozyMotion.Tick(.2f);
                            CozyMotion.Tick(1f);
                            foreach(var body in previousPage.GetComponentsInChildren<CozyWorkspaceBodyMotion>())body.Finish();
                            foreach(var region in previousPage.GetComponentsInChildren<CozyJournalPageMotion>())region.Finish();
                            foreach(var visual in previousPage.GetComponentsInChildren<CozyButtonVisual>())visual.ApplyFinalPose();
                            if (motionCase == "workspace-save-refresh")
                                CozyProfileSaveFeedback.Play(previousPage.transform.Find("Actions/Save").GetComponent<Button>(), () => router.Show(id,true));
                            else router.Show(id, true);
                        }
                        else router.Show(id);
                    }
                    else if (motionCase == "workspace-compose")
                        shell.GetComponentInChildren<DesktopSocialComposeModal>(true).PlayOpenMotion();
                    else if (motionCase == "workspace-settlement")
                        page.GetComponent<DesktopPondSettlementModalView>().PlayOpenMotion();
                    else
                    {
                        var picker = page.transform.Find("Picker") as RectTransform;
                        CozyWorkspaceDialogMotion.Play(picker, picker.Find("WorkspaceDim") as RectTransform);
                    }
                    var time = 0f;
                    while (time < seconds - .00001f)
                    {
                        var delta = Mathf.Min(.01f, seconds - time);
                        foreach (var motion in shell.GetComponentsInChildren<CozyStickerMotion>()) motion.Advance(delta);
                        foreach (var motion in shell.GetComponentsInChildren<CozyJournalPageMotion>()) motion.Advance(delta);
                        foreach (var motion in shell.GetComponentsInChildren<CozyWorkspaceBodyMotion>()) motion.Advance(delta);
                        foreach (var visual in shell.GetComponentsInChildren<CozyButtonVisual>()) visual.AdvanceIndex(delta);
                        CozyMotion.Tick(delta);
                        time += delta;
                    }
                }
                Canvas.ForceUpdateCanvases();
                var outputId=motionCase!=null?"motion-"+motionCase+"-"+Mathf.RoundToInt(seconds*1000).ToString("000"):name+(tab==null?"":"-"+tab);
                WriteGeometry(shell,camera,outputId);
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
                File.WriteAllBytes("Library/CozyRedesignPreviews/" + outputId + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                CozyMotion.CancelPageTransition();
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Serializable] sealed class GeometryNode
        {
            public string path, text, sprite, layout;
            public bool active;
            public float x,y,width,height,cssAngle,visualX,visualY,visualScale=1,visualAlpha=1,tapeReveal=1;
            public int fontSize;
        }
        [Serializable] sealed class GeometryReport { public GeometryNode[] nodes; }
        static void WriteGeometry(GameObject shell, Camera camera, string id)
        {
            var nodes=new System.Collections.Generic.List<GeometryNode>();
            var corners=new Vector3[4];
            foreach(var rect in shell.GetComponentsInChildren<RectTransform>(true))
            {
                rect.GetWorldCorners(corners);
                var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
                var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
                var text=rect.GetComponent<Text>();var image=rect.GetComponent<Image>();
                var motion=rect.GetComponent<CozyStickerMotion>();var pose=rect.GetComponent<CozyJournalStaticPose>();
                var button=rect.GetComponent<CozyButtonVisual>();
                nodes.Add(new GeometryNode {path=AnimationUtility.CalculateTransformPath(rect,shell.transform),active=rect.gameObject.activeInHierarchy,
                    x=a.x,y=941-b.y,width=b.x-a.x,height=b.y-a.y,text=text!=null?text.text:null,fontSize=text!=null?text.fontSize:0,
                    sprite=image!=null && image.sprite!=null?AssetDatabase.GetAssetPath(image.sprite):null,
                    layout=rect.GetComponent<LayoutGroup>()?.GetType().Name,
                    visualX=button!=null && rect.parent!=null && (rect.parent.name=="Tabs" || rect.parent.name=="SettingsTabs")?button.IndexPose.X:pose!=null?pose.AppliedOffset.x:0,
                    visualY=motion!=null?motion.CurrentVisualOffset.y:pose!=null?pose.AppliedOffset.y:0,
                    visualScale=motion!=null?motion.AppliedScale:pose!=null?pose.AppliedScale:1,
                    visualAlpha=pose!=null?pose.AppliedOpacity:1,tapeReveal=motion!=null?motion.TapeReveal:1,
                    cssAngle=motion!=null?motion.CssAngle:pose!=null?pose.CssAngle:-Mathf.DeltaAngle(0,rect.localEulerAngles.z)});
            }
            File.WriteAllText("Library/CozyRedesignPreviews/"+id+".geometry.json",JsonUtility.ToJson(new GeometryReport{nodes=nodes.ToArray()},true));
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
            if(name=="PanelGallery")
            {
                var controller=page.GetComponent<DesktopGalleryModalView>();
                controller.Bind(null,null);
                var entries=new FishSocial.Desktop.Auth.FishCodexEntryDto[fishIds.Length];
                for(var i=0;i<entries.Length;i++) entries[i]=new FishSocial.Desktop.Auth.FishCodexEntryDto {speciesId=fishIds[i],totalCaught=12-i,maxSizeM=.86f-i*.06f};
                controller.PresentEntries(entries);
                controller.SelectSpecies(fishIds[0]);
            }
            if (name == "PanelProfileEdit")
            {
                // Original pages.js J/AVATARS fixture, kept in the Editor preview only.
                const string bio = "喜欢晴天和湖水，也喜欢等一条鱼上钩。";
                root.Find("Form/Nickname").GetComponent<InputField>().text = "小橘";
                root.Find("Form/Bio").GetComponent<InputField>().text = bio;
                foreach (var input in root.Find("Form").GetComponentsInChildren<InputField>())
                {
                    input.ForceLabelUpdate();
                    if (input.placeholder != null) input.placeholder.gameObject.SetActive(false);
                    input.textComponent.text = input.name == "Nickname" ? "小橘" : bio;
                }
                CozyItemArt.Text(root, "JournalEditHeading", "编辑资料");
                CozyItemArt.Text(root, "JournalPreview/Name", "小橘");
                CozyItemArt.Set(root, "JournalPreview/Avatar", DesktopDefaultAvatars.Portrait("/image/profile/cat_avatar_orange.png"));
                foreach (var avatar in DesktopDefaultAvatars.All)
                {
                    var choice = Item("AvatarChoice", root.Find("Avatars/Viewport/Content"));
                    CozyItemArt.Set(choice.transform, "DesignItemArt", DesktopDefaultAvatars.Portrait(avatar.Path));
                    CozyItemArt.Text(choice.transform, "Label", avatar.Label);
                    Select(choice, avatar.Id == "orange");
                }
                var showcaseIds = new[] { "koi", "golden_line", "mandarin" };
                var showcaseNames = new[] { "锦鲤", "金线鲃", "鳜鱼" };
                for (var index = 0; index < 8; index++)
                {
                    var slot = Item("ShowcaseSlot", root.Find("Showcase/Viewport/Content"));
                    CozyItemArt.Text(slot.transform, "Label", index < 3 ? showcaseNames[index] : "空位 " + (index + 1));
                    CozyItemArt.Set(slot.transform, "DesignItemArt", index < 3 ? CozyItemArt.Fish(showcaseIds[index]) : null);
                }
                CozyItemArt.Text(root, "Status", "把喜欢的名字、头像和一句话介绍，留在自己的手账里。");
            }
            if (name == "PanelWorldMap")
            {
                var layer = root.Find("Viewport/MapContent/MarkerLayer");
                var ponds = WorldMapPondCatalog.LoadVisible();
                var selected = Array.Find(ponds, PondSelectionState.HasDualFee);
                foreach (var pond in ponds)
                {
                    var marker = Item("WorldMapPondMarker", layer);
                    var rect = marker.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(pond.x, 1 - pond.y);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    var view = marker.GetComponent<CozyPondMarkerView>() ?? marker.AddComponent<CozyPondMarkerView>();
                    view.Bind(pond.displayName);
                    var locked = !pond.isOpen || pond.minPlayerLevel > 30;
                    view.SetState(pond == selected, locked, DesktopGameData.CategoryColor(pond.pondCategory, locked));
                }
            }
            if (name == "PanelCatch")
            {
                var grid = root.Find("Grid/Viewport/Slots");
                for (var index = 0; index < 6; index++)
                {
                    var item = Item("CatchSlot", grid);
                    CozyItemArt.Set(item.transform, "DesignItemArt", CozyItemArt.Fish(fishIds[index]));
                    CozyItemArt.Text(item.transform, "Label", fishNames[index]);
                    CozyItemArt.Text(item.transform, "Meta", "0." + (86 - index * 6) + " m · ★★★");
                    CozyStickerMotion.Bind(item, "preview:"+fishIds[index], index, sourceId:fishIds[index]);
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
            if (name == "PanelPondSelection" || name == "PanelWorldMap")
            {
                var state = new PondSelectionState();
                state.ApplyProgress(new FishSocial.Desktop.Auth.FishingProgressDto { level = 30, onboardingCompleted = true, todayFeeCharges = 1 });
                var ponds = WorldMapPondCatalog.LoadVisible();
                var selected = Array.Find(ponds, PondSelectionState.HasDualFee) ?? (ponds.Length > 0 ? ponds[0] : null);
                state.Select(selected);
                PondDetailView.Refresh(root.Find(name == "PanelWorldMap" ? "Details" : "DesignPondAside"), state,
                    name == "PanelWorldMap" ? "DetailsText" : "Body");
                if (name == "PanelPondSelection")
                {
                    var cards = root.Find("PondCardScroll/Viewport/Cards");
                    for (var i = 0; i < cards.childCount && i < ponds.Length; i++)
                    {
                        cards.GetChild(i).GetComponent<CozyPondCardContent>()?.Bind(ponds[i], DesktopGameData.GetPond(ponds[i].pondId), state);
                        Select(cards.GetChild(i).gameObject, ponds[i] == selected);
                    }
                    CozyItemArt.Text(root, "ResultCount", "全部鱼塘 · " + ponds.Length);
                }
            }
            if (name == "PanelShop" || name == "PanelEquipment")
            {
                var equipment = name == "PanelEquipment";
                var names = new[] { "竹制手竿", "台钓碳素竿", "溪流竿", "矶钓竿", "海竿", "路亚竿", "重型路亚竿", "巨物竿" };
                var ids = new[] { "rod-bamboo", "rod-tai", "rod-stream", "rod-iso", "rod-surf", "rod-lure", "rod-heavy", "rod-giant" };
                var prices = new[] { "已拥有", "2,500", "3,200", "5,500", "4,800", "9,000", "22,000", "45,000" };
                for (var index = 0; index < names.Length; index++)
                {
                    var item = Item("ShopItemCard", root.Find("Items/Viewport/Content"));
                    CozyItemArt.Set(item.transform, "DesignItemArt", CozyItemArt.Product(1, ids[index], names[index]));
                    CozyItemArt.Text(item.transform, "Label", names[index]);
                    CozyItemArt.Text(item.transform, "Meta", equipment ? index==0 ? "手竿入门" : "商店购买" : prices[index]);
                    Select(item, index == (equipment ? 0 : 1));
                    CozyStickerMotion.Bind(item, "preview:"+ids[index], index, sourceId:ids[index]).ApplyFinalPose();
                }
                Select(root.Find("Tabs/Tackle").gameObject, true);
                var selectedIndex = equipment ? 0 : 1;
                CozyItemArt.Set(root, "Details/DesignArt", CozyItemArt.Product(1, ids[selectedIndex], names[selectedIndex]));
                CozyItemArt.Text(root, "Details/DesignName", names[selectedIndex]);
                CozyItemArt.Text(root, "Header/Equipped", equipment ? "当前装备\n竹制手竿" : "");
                CozyItemArt.Text(root, "DesignItemsHeading", equipment ? "钓竿    已拥有 1 / 8" : "顺手的竿    8 件");
                CozyItemArt.Text(root, "Status", equipment ? "装备时会在卡片上盖一个「使用中」的章。" : "金币不够时购买按钮会摇一摇；买到的钓竿会飞进「装备」。");
                CozyItemArt.Text(root, "Details/DescriptionScroll/Description", equipment ? "手竿入门 · 咬钩 +0% · 脱钩 -0%\n断线 0.45 m" : "台钓 · 咬钩 +3% · 脱钩 -4%\n断线 0.8 m       2,500");
                root.Find("Details/Buy").gameObject.SetActive(!equipment);
                root.Find("Details/Equip").gameObject.SetActive(equipment);
                CozyItemArt.Text(root, "Details/Buy/Label", "2,500 购买");
                CozyItemArt.Text(root, "Details/Equip/Label", "使用中");
            }
            if (name == "PanelProfileHub" || name == "PanelAchievements")
            {
                // Original app.js FISH/ACH and pages.js profile fixtures; never written to player state.
                var ids = new[] { "koi", "golden_line", "crucian", "carp", "grass_carp", "black_bass", "mandarin", "silver_carp", "bream", "catfish", "snakehead", "yellow_croaker", "taimen", "chinese_sturgeon", "black_carp" };
                var names = new[] { "锦鲤", "金线鲃", "鲫鱼", "鲤鱼", "草鱼", "大口黑鲈", "鳜鱼", "鲢鱼", "鳊鱼", "鲶鱼", "乌鳢", "大黄鱼", "哲罗鲑", "中华鲟", "青鱼" };
                var sizes = new[] { .62f, .31f, .21f, .58f, .86f, .44f, .38f, .72f, .29f, .66f, .53f, .35f };
                var stars = new[] { 5, 4, 2, 3, 3, 3, 4, 2, 2, 3, 3, 3 };
                var content = root.Find("Shell/Content");
                foreach (Transform child in content) child.gameObject.SetActive(false);
                var achievements = name == "PanelAchievements";
                var chosen = tab ?? (achievements ? "成就" : "资料");
                var selected = content.Find("Tab_" + chosen);
                selected.gameObject.SetActive(true);
                CozyItemArt.Text(root, "DesignHeader/Title", achievements ? "成就" : "个人中心");
                CozyItemArt.Text(root, "DesignHeader/Subtitle", achievements ? "每一枚奖章都是一个故事" : "记录属于你的钓鱼时光");
                root.Find("Shell/Header/Edit").gameObject.SetActive(!achievements && chosen == "资料");
                foreach (var label in new[] { "资料", "展示柜", "图鉴", "相册", "成就" })
                {
                    var button = root.Find("Shell/Sidebar/Tab_" + label);
                    if (button != null) Select(button.gameObject, label == chosen);
                }
                if (chosen == "展示柜")
                {
                    CozyItemArt.Text(selected, "DesignGridHeading", "我的展示柜");
                    var indexes = new[] { 0, 1, 6 };
                    for (var index = 0; index < indexes.Length; index++)
                    {
                        var f = indexes[index];
                        var item = UnityEngine.Object.Instantiate(selected.Find("JournalCardTemplate").gameObject,
                            selected.Find("ShowcaseGrid/Viewport/Content"), false);
                        item.name = "PreviewShowcase" + index;
                        item.SetActive(true);
                        CozyItemArt.Text(item.transform, "Label", names[f] + "\n" + sizes[f].ToString("0.00") + " m · " + new string('★', stars[f]));
                        CozyItemArt.Set(item.transform, "DesignItemArt", CozyItemArt.Fish(ids[f]));
                        Select(item, index == 0);
                        CozyStickerMotion.Bind(item, "preview-showcase:" + ids[f], index, sourceId: "slot-" + index);
                    }
                    CozyItemArt.Text(selected, "DesignDetailHeading", "展示位 1");
                    CozyItemArt.Text(selected, "Detail", "锦鲤");
                    CozyItemArt.Text(selected, "JournalPhotoCaption", "锦鲤");
                    CozyItemArt.Set(selected, "DesignDetailArt", CozyItemArt.Fish("koi"));
                    CozyItemArt.Rows(selected, "ShowcaseDetailRows", "star", "品质", "★★★★★", "hook", "体长", "0.62 m", "pond", "来自", "千岛湖", "heart", "收藏", "最喜欢的收藏");
                    CozyItemArt.Text(root, "Shell/Status", "选中展示格，再从背包挑一条鱼。清空展示格不会出售鱼获。");
                }
                else if (chosen == "图鉴")
                {
                    CozyItemArt.Text(selected, "DesignGridHeading", "鱼类图鉴");
                    for (var index = 0; index < ids.Length; index++)
                    {
                        var item = UnityEngine.Object.Instantiate(selected.Find("JournalCardTemplate").gameObject,
                            selected.Find("CodexGrid/Viewport/Content"), false);
                        item.name = "PreviewCodex" + ids[index];
                        item.SetActive(true);
                        CozyItemArt.Text(item.transform, "Label", index < 12 ? names[index] + "\n" + new string('★', stars[index]) : "尚未发现\n捕获后解锁");
                        CozyItemArt.Set(item.transform, "DesignItemArt", CozyItemArt.Fish(ids[index]));
                        if (index >= 12) item.transform.Find("DesignItemArt").GetComponent<Image>().color = new Color(.46f, .28f, .18f, .22f);
                        item.transform.Find("JournalStamp").gameObject.SetActive(index < 12);
                        Select(item, index == 0);
                        CozyStickerMotion.Bind(item, "preview-codex:" + ids[index], index, sourceId: ids[index]);
                    }
                    CozyItemArt.Text(selected, "DesignDetailHeading", "锦鲤 · 图鉴");
                    CozyItemArt.Set(selected, "DesignDetailArt", CozyItemArt.Fish("koi"));
                    CozyItemArt.Text(selected, "JournalPhotoCaption", "锦鲤");
                    CozyItemArt.Text(selected, "Detail", "杂食 / 肉食 · 腥香饵推荐\n最大体长                    0.62 m\n累计捕获                    7 次\n首次相遇    10 月 14 日 · 千岛湖\n最近相遇                    今天 · 湖畔");
                    CozyItemArt.Text(root, "Shell/Status", "灰色剪影是尚未遇见的朋友；每一种新鱼，都会在图鉴里留下印记。");
                }
                else if (chosen == "相册")
                {
                    CozyItemArt.Text(selected, "JournalHeading", "湖畔相册");
                    CozyItemArt.Set(selected, "AlbumStage/SlotL/Photo", CozyItemArt.Fish("koi"));
                    CozyItemArt.Set(selected, "AlbumStage/SlotR/Photo", CozyItemArt.Fish("grass_carp"));
                    CozyItemArt.Text(selected, "AlbumStage/SlotL/Body", "锦鲤 · 0.62 m\n难忘的稀有鱼获\n10 月 21 日 · 千岛湖");
                    CozyItemArt.Text(selected, "AlbumStage/SlotR/Body", "草鱼 · 0.86 m\n值得记下的一次收获\n10 月 20 日 · 千岛湖");
                    CozyItemArt.Text(selected, "Page", "1 / 3");
                    CozyItemArt.Set(selected, "JournalAlbumAside/Art", CozyItemArt.Fish("koi"));
                    CozyItemArt.Text(selected, "JournalAlbumAside/Caption", "锦鲤");
                    CozyItemArt.Text(selected, "JournalAlbumAside/Detail", "在千岛湖，遇见 0.62 m 的锦鲤。\n这一天的风和水光，也一起留在了照片里。");
                    CozyItemArt.Text(root, "Shell/Status", "一页两张拍立得，记下稀有鱼、大鱼，以及每一个值得留住的时刻。");
                }
                else if (chosen == "成就")
                {
                    CozyItemArt.Text(selected, "DesignHeading", "全部成就");
                    var titles = new[] { "初次上钩", "锦鲤附体", "百鱼斩", "湖泊旅人", "早起的猫", "满载而归" };
                    var descriptions = new[] { "钓到第一条鱼", "钓到一条锦鲤", "累计钓到 100 条鱼", "去过 5 个湖泊", "清晨钓到 10 条鱼", "一次卖出 1000 金币" };
                    var icons = new[] { "hook", "star", "fish", "map", "sun", "coin" };
                    for (var index = 0; index < titles.Length; index++)
                    {
                        var item = Item("AchievementRow", selected.Find("Achievements/Viewport/Content"));
                        CozyItemArt.Text(item.transform, "Label", titles[index]);
                        CozyItemArt.Text(item.transform, "JournalDescription", descriptions[index]);
                        CozyItemArt.Text(item.transform, "DesignState", index == 0 ? "已完成" : "");
                        item.transform.Find("DesignStateBadge").gameObject.SetActive(index == 0);
                        item.transform.Find("DesignState").GetComponent<Text>().color = new Color(.8f, .25f, .18f);
                        CozyItemArt.Set(item.transform, "Reflow_AchievementSeal", CatTheme.LoadCozy("icons/" + icons[index] + ".png", Vector4.zero));
                        item.transform.Find("Reflow_AchievementSeal").GetComponent<Image>().color = new Color(1, 1, 1, index == 0 || index == 1 || index == 3 ? 1 : .45f);
                        CozyStickerMotion.Bind(item, "preview-achievement:a" + (index + 1), index, sourceId: "a" + (index + 1));
                    }
                    CozyItemArt.Text(root, "DesignAchievementSummary/Value", "1 / 6");
                    CozyItemArt.Text(selected, "JournalAchievementSummary/Value", "1 / 6");
                    var category = root.Find("DesignAchievementCategories/All");
                    if (category != null) Select(category.gameObject, true);
                    Select(selected.Find("DesignFilterAll").gameObject, true);
                    CozyItemArt.Text(root, "Shell/Status", "每一枚奖章都是一个故事。");
                }
                else
                {
                    CozyItemArt.Text(selected, "JournalHeading", "我的钓鱼时光");
                    CozyItemArt.Set(selected, "Avatar", DesktopDefaultAvatars.Portrait("/image/profile/cat_avatar_orange.png"));
                    CozyItemArt.Text(selected, "DesignName", "小橘");
                    CozyItemArt.Text(selected, "DesignBio", "喜欢晴天和湖水，也喜欢等一条鱼上钩。");
                    CozyItemArt.Rows(selected, "DesignStatCards", "fish", "累计鱼获", "64", "map", "探索水域", "5", "star", "图鉴收集", "12 / 15");
                    CozyItemArt.Text(selected, "JournalProgress/Level", "Lv.7 → Lv.8");
                    CozyItemArt.Text(selected, "JournalProgress/Caption", "320 / 500 经验 · 去收获下一段故事吧。");
                    selected.Find("JournalProgress/Track/Fill").GetComponent<Image>().fillAmount = .64f;
                    CozyItemArt.Set(selected, "DesignShowcase/Art", CozyItemArt.Fish("koi"));
                    CozyItemArt.Text(selected, "DesignShowcase/Species", "锦鲤");
                    CozyItemArt.Text(selected, "DesignShowcase/Size", "0.62 m · ★★★★★");
                    CozyItemArt.Text(root, "Shell/Status", "头像、昵称、简介和展示鱼获，都可以在这里整理。");
                }
            }
            if (name == "PanelLeaderboard")
            {
                var players = new[] { "阿花", "小白", "煤球", "蓝莓", "小橘", "银杏", "奶糖", "小墨" };
                var avatars = new[] { "calico", "white", "black", "blue", "orange", "silver", "cream", "cow" };
                var records = new[] { "1.28", "1.16", "1.05", "0.94", "0.86", "0.78", "0.62", "0.51" };
                CozyItemArt.Text(root, "JournalBoardHeading", "今日大鱼");
                var snapshot = new FishSocial.Desktop.Auth.LeaderboardEntryDto[players.Length];
                for (var index = 0; index < players.Length; index++)
                    snapshot[index] = new FishSocial.Desktop.Auth.LeaderboardEntryDto
                    {
                        rank = index + 1, playerId = new[] { "cat-flower", "cat-snow", "cat-black", "cat-blue", "self", "cat-silver", "cat-cream", "cat-cow" }[index],
                        nickname = players[index], avatarUrl = "/image/profile/cat_avatar_" + avatars[index] + ".png",
                        value = float.Parse(records[index], System.Globalization.CultureInfo.InvariantCulture),
                    };
                root.GetComponent<DesktopLeaderboardPanel>().PresentRows(snapshot);
                root.GetComponent<DesktopLeaderboardPanel>().SelectPlayer(snapshot[0]);
                root.GetComponent<DesktopLeaderboardPanel>().SampleMotion(.22f);
                Select(root.Find("Tabs/Daily").gameObject, true);
                CozyItemArt.Set(root, "DesignPlayerCard/Avatar", DesktopDefaultAvatars.Portrait("/image/profile/cat_avatar_calico.png"));
                CozyItemArt.Text(root, "DesignPlayerCard/Heading", "钓友名片  #1");
                CozyItemArt.Text(root, "DesignPlayerCard/Name", "阿花");
                CozyItemArt.Text(root, "DesignPlayerCard/Bio", "Lv.12 · 今天也想遇到一条漂亮的鱼。");
                CozyItemArt.Text(root, "DesignPlayerCard/Value", "今日大鱼 1.28 m · 代表鱼获 锦鲤");
                CozyItemArt.Text(root, "MyRank", "我的排名 #5 · 今日最大体长 0.86 m · 每一次抛竿，都在刷新自己的纪录。");
                CozyItemArt.Text(root, "Status", "");
            }
            if (name == "PanelSocialFeed")
            {
                var authors = new[] { "阿花", "小白", "煤球", "蓝莓" };
                var avatars = new[] { "calico", "white", "black", "blue" };
                var species = new[] { "koi", "grass_carp", "black_bass", "mandarin" };
                var stories = new[] { "锦鲤真的来了！这份好运，分给今天在湖边的每一位钓友。", "草鱼又刷新纪录了。今天的谷物饵，果然很受欢迎。", "夕阳快落下的时候，等到了这一口。慢慢等，总会有惊喜。", "今天第一次钓到鳜鱼，已经贴进我的图鉴啦。" };
                var likes = new[] { 28, 16, 11, 9 };
                var levels = new[] { 12, 10, 11, 9 };
                var sizes = new[] { .62f, .86f, .44f, .38f };
                var stars = new[] { 5, 3, 3, 4 };
                var minutes = new[] { 10, 35, 60, 120 };
                var ponds = new[] { "千岛湖", "太湖", "洪泽湖", "鄱阳湖" };
                CozyItemArt.Text(root, "JournalFeedHeading", "公开动态");
                Select(root.Find("Header/Public").gameObject, true);
                for (var index = 0; index < authors.Length; index++)
                {
                    var post = Item("SocialPostCard", root.Find("Scroll/Viewport/Content"));
                    var data = new FishSocial.Desktop.Auth.SocialPostDto
                    {
                        id = "preview-post-" + index, playerId = "preview-cat-" + index, nickname = authors[index],
                        authorAvatarUrl = "characters/avatars/" + avatars[index] + ".png", text = stories[index], visibility = "public",
                        createdAt = DateTimeOffset.UtcNow.AddMinutes(-minutes[index]).ToUnixTimeMilliseconds(), likeCount = likes[index], commentCount = index < 2 ? 1 : 0,
                        fish = new FishSocial.Desktop.Auth.FishInventoryItemDto { id = "preview-fish-" + index, speciesId = species[index], sizeM = sizes[index], quality = "rare", pondId = ponds[index] },
                    };
                    var binder = post.GetComponent<DesktopSocialPostCard>();
                    binder.Bind(data, null);
                    CozyItemArt.Set(post.transform, "Header/JournalAvatar", DesktopDefaultAvatars.Portrait("/image/profile/cat_avatar_" + avatars[index] + ".png"));
                    CozyItemArt.Text(post.transform, "Header/JournalTime", "Lv." + levels[index] + " · " + (minutes[index] < 60 ? minutes[index] + " 分钟前" : minutes[index] / 60 + " 小时前"));
                    CozyItemArt.Text(post.transform, "FishInfoText", sizes[index].ToString("0.00") + " m · " + new string('★', stars[index]));
                    if (index == 0)
                    {
                        var host = root.Find("DesignFeedInfo/CommentsHost") as RectTransform;
                        binder.ConfigureCommentsHost(host, null);
                        var comments = post.transform.Find("CommentsPanel") as RectTransform;
                        comments.SetParent(host, false);
                        comments.anchorMin = Vector2.zero; comments.anchorMax = Vector2.one;
                        comments.offsetMin = comments.offsetMax = Vector2.zero;
                        comments.gameObject.SetActive(true);
                        var comment = Item("PostCommentRow", comments.Find("CommentsViewport/CommentsContent"));
                        CozyItemArt.Text(comment.transform, "Text", "小白\n好漂亮的花纹，接住好运！");
                        CozyItemArt.Text(comment.transform, "CommentText", "小白\n好漂亮的花纹，接住好运！");
                        var delete = comment.transform.Find("Delete"); if (delete != null) delete.gameObject.SetActive(false);
                        var input = comments.Find("CommentInput").GetComponent<InputField>();
                        if (input.placeholder is Text placeholder) placeholder.text = "写一句温暖的话…";
                        CozyItemArt.Text(comments, "SendButton/Label", "留下鼓励");
                        CozyItemArt.Text(root, "DesignFeedInfo/Heading", "给 阿花 的留言");
                        CozyItemArt.Set(root, "DesignFeedInfo/JournalSummaryArt", CozyItemArt.Fish("koi"));
                        CozyItemArt.Text(root, "DesignFeedInfo/DetailTitle", "锦鲤\n千岛湖 · 1 条评论");
                    }
                }
                CozyItemArt.Text(root, "Status", "把收获分享给钓友，也把一句鼓励留在他们的手账里。");
            }
            if (name == "PanelSettings")
            {
                var selectedTab = tab == "通知偏好" || tab == "提醒开关" ? 1 : tab == "托盘与其他" || tab == "系统托盘" ? 3 : 0;
                for (var group = 0; group < DesktopSettingsModalView.TabNodes.Length; group++)
                    foreach (var nodeName in DesktopSettingsModalView.TabNodes[group])
                        DesktopModalUi.FindDescendant(root, nodeName)?.gameObject.SetActive(group == selectedTab);
                var content = root.Find("SettingsViewport/SettingsContent");
                foreach (var nodeName in new[] { "JournalWindowed", "JournalDisplayTrack" })
                    content.Find(nodeName)?.gameObject.SetActive(selectedTab == 0);
                for (var i = 0; i < DesktopSettingsModalView.TabNames.Length; i++)
                {
                    var node = root.Find("SettingsTabs/" + DesktopSettingsModalView.TabNames[i]);
                    if (node != null) { node.gameObject.SetActive(i != 2); Select(node.gameObject, i == selectedTab); }
                }
                foreach (var preset in new[] { "1280×720", "1600×900", "1920×1080", "2560×1440" })
                    Select(content.Find(preset).gameObject, preset == "1600×900");
                Select(content.Find("JournalWindowed").gameObject, true);
                Select(content.Find("无边框").gameObject, false);
                Select(content.Find("全屏").gameObject, false);
                var notifyNames = new[] { "启用通知", "免打扰", "鱼咬钩通知", "好友邀请通知", "连接错误通知" };
                foreach (var nodeName in notifyNames)
                {
                    var toggle = DesktopModalUi.FindDescendantComponent<Toggle>(root, nodeName);
                    if (toggle != null) toggle.SetIsOnWithoutNotify(nodeName != "免打扰");
                }
                CozyItemArt.Text(root, "DesignSettingsHeading", selectedTab == 0 ? "窗口尺寸" : selectedTab == 1 ? "提醒开关" : "系统托盘");
                CozyItemArt.Text(root, "DesignSettingsInfo/Body", "1600×900\n\n窗口 · 提醒 4 项\n\n所有改动都会自动保存。\n开关由猫爪拨动，选中的尺寸会盖一个小勾章。");
                CozyItemArt.Text(root, "DesignSettingsNote", "当前 1600×900\n切换后立即保存");
                foreach (var nodeName in new[] { "保存窗口设置", "模拟连接错误", "窗口模式", "通知" })
                    DesktopModalUi.FindDescendant(root, nodeName)?.gameObject.SetActive(false);
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
