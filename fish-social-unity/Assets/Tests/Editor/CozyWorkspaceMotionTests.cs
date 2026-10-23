#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Reflection;
using FishSocial.Desktop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyWorkspaceMotionTests
    {
        GameObject root;
        bool reduced;
        [SetUp] public void Setup()
        {
            reduced = CozyMotion.Reduced;
            CozyMotion.SetPaused(false); CozyMotion.SetReduced(false);
            root = new GameObject("WorkspaceMotionTest", typeof(RectTransform));
        }
        [TearDown] public void Cleanup()
        {
            CozyMotion.CancelPageTransition();
            CozyMotion.Cancel(root.transform as RectTransform, true);
            Object.DestroyImmediate(root);
            CozyMotion.SetPaused(false); CozyMotion.SetReduced(reduced);
        }
        RectTransform Rect(string name, Transform parent = null)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            r.SetParent(parent != null ? parent : root.transform, false); return r;
        }

        [TestCase("PanelShop")]
        [TestCase("PanelEquipment")]
        [TestCase("PanelCatch")]
        [TestCase("PanelProfileHub")]
        [TestCase("PanelProfileEdit")]
        [TestCase("PanelLeaderboard")]
        [TestCase("PanelSocialFeed")]
        [TestCase("PanelWorldMap")]
        [TestCase("PanelPondSelection")]
        [TestCase("PanelSettings")]
        [TestCase("PanelAchievements")]
        [TestCase("PanelSocial")]
        [TestCase("PanelGallery")]
        public void ProductionPagesKeepAllContentCoveredUntilPaperHasRisen(string resource)
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/" + resource);
            Assert.IsNotNull(prefab);
            var page = Object.Instantiate(prefab, root.transform);
            var home = Rect("Home");
            var calls = 0;
            CozyMotion.Flip(home.gameObject, page, () => calls++);
            var pose = page.GetComponent<CozyJournalFlipPose>();
            Assert.IsTrue(CozyMotion.EntranceHeld);
            Assert.AreEqual(0, pose.ContentAlpha);
            CozyMotion.Tick(.1f);
            Assert.That(page.GetComponent<CanvasGroup>().alpha, Is.GreaterThan(0).And.LessThan(1));
            Assert.AreEqual(0, pose.ContentAlpha, "Plain text, workspace cards and the rail must not show then disappear.");
            CozyMotion.Tick(.1f);
            Assert.AreEqual(1, pose.ContentAlpha);
            Assert.IsFalse(CozyMotion.EntranceHeld);
            Assert.AreEqual(1, calls);
            CozyMotion.Tick(1);
            Assert.AreEqual(1, calls);
        }

        [TestCase("PanelShop", "", "Details")]
        [TestCase("PanelEquipment", "", "Details")]
        [TestCase("PanelSocialFeed", "", "DesignFeedInfo")]
        [TestCase("PanelLeaderboard", "", "DesignPlayerCard")]
        [TestCase("PanelSettings", "", "DesignSettingsInfo")]
        [TestCase("PanelPondSelection", "", "DesignPondAside")]
        [TestCase("PanelWorldMap", "", "Details")]
        [TestCase("PanelGallery", "", "GalleryAside")]
        [TestCase("PanelProfileHub", "Shell/Content/Tab_资料", "DesignShowcase")]
        public void SavedPrefabActuallyFadesItsVisibleAside(string resource,string ownerPath,string asidePath)
        {
            // Deliberately do not run the runtime repair: loading the saved asset must retain the binding.
            var page = Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/"+resource),root.transform);
            var owner = ownerPath == "" ? page.transform : page.transform.Find(ownerPath);
            var aside = owner.Find(asidePath);
            var motion = owner.GetComponent<CozyJournalPageMotion>();
            Assert.IsNotNull(motion,resource);
            var group = aside.GetComponent<CanvasGroup>();
            Assert.IsNotNull(group,resource+" must save the actual region, not a null reference");
            motion.PlayEntrance(0);
            Assert.AreEqual(0,group.alpha,resource);
            motion.Advance(.1f);
            Assert.AreEqual(.125f,group.alpha,.0001f,resource);
            Assert.AreEqual(group.alpha,motion.AsideAlpha,.0001f);
            Assert.AreEqual(.625f,motion.HeadingAlpha,.0001f,resource+" heading must be bound too");
            motion.Advance(.14f);
            Assert.AreEqual(1,group.alpha,.0001f);
        }

        [TestCase("PanelProfileHub", "Shell/Content/Tab_资料", "DesignShowcase", 0)]
        [TestCase("PanelShop", "", "Details", 8)]
        [TestCase("PanelLeaderboard", "", "DesignPlayerCard", 11)]
        [TestCase("PanelSocialFeed", "", "DesignFeedInfo", 4)]
        [TestCase("PanelPondSelection", "", "DesignPondAside", 20)]
        [TestCase("PanelWorldMap", "", "Details", 0)]
        public void RealRouterOpeningMatchesBrowserSampledAsideClock(string resource,string ownerPath,string asidePath,int cards)
        {
            var home=Rect("Home"); var slot=Rect("Profile");
            var page=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/"+resource),slot);
            page.name=resource;page.SetActive(true);
            var owner=ownerPath==""?page.transform:page.transform.Find(ownerPath);
            for(var node=owner;node!=slot;node=node.parent) node.gameObject.SetActive(true);
            for(var i=0;i<cards;i++)
            {
                var card=Rect("Card"+i,owner);card.gameObject.AddComponent<Image>();
                CozyStickerMotion.Bind(card.gameObject,"timing:"+i,i);
            }
            var router=root.AddComponent<PanelRouter>();
            router.Register(ShellPanelId.Home,home.gameObject);
            router.Register(ShellPanelId.Profile,slot.gameObject);
            router.Show(ShellPanelId.Profile);
            var pose=slot.GetComponent<CozyJournalFlipPose>();
            CozyMotion.Tick(.1f);
            Assert.AreEqual(0,pose.ContentAlpha);
            // Browser #page at 100 ms: opacity .872201 and translateY 2.04478 px.
            Assert.AreEqual(.872201f,slot.GetComponent<CanvasGroup>().alpha,.0001f);
            Assert.AreEqual(-2.04478f,slot.anchoredPosition.y,.001f);
            CozyMotion.Tick(.1f);
            var motion=owner.GetComponent<CozyJournalPageMotion>();
            var group=owner.Find(asidePath).GetComponent<CanvasGroup>();
            Assert.AreEqual(1,pose.ContentAlpha);
            Assert.AreEqual(0,group.alpha);
            motion.Advance(.1f);
            // Web: 0 stickers -> 80 ms delay, 4 -> 160 ms, >=8 -> 240 ms.
            Assert.AreEqual(cards==0?.125f:0,group.alpha,.0001f);
            motion.Advance(.1f);
            Assert.AreEqual(cards==0?.75f:cards==4?.25f:0,group.alpha,.0001f);
            motion.Advance(.2f);
            Assert.AreEqual(1,group.alpha,.0001f);
        }

        [Test]
        public void WorkspaceEditorRegionsStayOutOfLegacyAsideAndUseSectionEntrance()
        {
            var page=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelProfileEdit"),root.transform);
            page.name="PanelProfileEdit";page.SetActive(true);
            var slot=Rect("ProfileEdit");page.transform.SetParent(slot,false);
            var home=Rect("Home");var router=root.AddComponent<PanelRouter>();
            router.Register(ShellPanelId.Home,home.gameObject);router.Register(ShellPanelId.ProfileEdit,slot.gameObject);
            // Bind the actual presenter path too: it used to overwrite the workspace regions with the old aside.
            page.GetComponent<DesktopProfileEditPanel>().Bind(null,null,null);
            router.PanelChanged+=_=>page.GetComponent<DesktopProfileEditPanel>().OnOpened();
            router.Show(ShellPanelId.ProfileEdit);
            CozyMotion.Tick(.2f);
            var motion=page.GetComponent<CozyJournalPageMotion>();
            foreach(var path in new[]{"JournalPreview","WorkspaceEditRight","Form","Avatars","Showcase"})
                Assert.AreEqual(1,page.transform.Find(path).GetComponent<CanvasGroup>().alpha,path);
            Assert.AreEqual(0,motion.HeadingAlpha);
            motion.Advance(.1f);
            Assert.AreEqual(.625f,motion.HeadingAlpha,.0001f);
            Assert.AreEqual(.625f,page.transform.Find("WorkspaceSubtitle").GetComponent<CanvasGroup>().alpha,.0001f);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EditAndCancelFadeOldContentBeforeNewSectionsWithoutMovingPaper(bool returning)
        {
            var home=Rect("Home");var profile=Rect("Profile");var edit=Rect("ProfileEdit");
            var profilePage=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelProfileHub"),profile);
            var editPage=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelProfileEdit"),edit);
            profilePage.name="PanelProfileHub";editPage.name="PanelProfileEdit";
            profilePage.SetActive(true);editPage.SetActive(true);
            var profileTab=profilePage.transform.Find("Shell/Content/Tab_资料");
            for(var node=profileTab;node!=profile;node=node.parent)node.gameObject.SetActive(true);
            var router=root.AddComponent<PanelRouter>();
            router.Register(ShellPanelId.Home,home.gameObject);router.Register(ShellPanelId.Profile,profile.gameObject);
            router.Register(ShellPanelId.ProfileEdit,edit.gameObject);
            router.Show(returning?ShellPanelId.ProfileEdit:ShellPanelId.Profile);CozyMotion.Tick(.2f);
            var target=returning?profile:edit;var old=returning?edit:profile;
            var rest=target.anchoredPosition;
            router.Show(returning?ShellPanelId.Profile:ShellPanelId.ProfileEdit,true);
            Assert.IsTrue(old.gameObject.activeSelf);
            Assert.AreEqual(0,target.GetComponent<CanvasGroup>().alpha);
            CozyMotion.Tick(.05f);
            var oldMotion=returning?editPage.GetComponent<CozyJournalPageMotion>():profileTab.GetComponent<CozyJournalPageMotion>();
            Assert.AreEqual(.5f,oldMotion.HeadingAlpha,.0001f);
            CozyMotion.Tick(.05f);
            Assert.IsFalse(old.gameObject.activeSelf,"100 ms exit completes before the new sections enter");
            Assert.IsFalse(CozyMotion.EntranceHeld);
            Assert.AreEqual(1,target.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(1,target.GetComponent<CozyJournalFlipPose>().ContentAlpha);
            Assert.AreEqual(0,target.GetComponent<CozyJournalFlipPose>().ContentOffset);
            Assert.AreEqual(rest,target.anchoredPosition);
            var motion=returning?profileTab.GetComponent<CozyJournalPageMotion>():editPage.GetComponent<CozyJournalPageMotion>();
            Assert.AreEqual(0,motion.HeadingAlpha);
            motion.Advance(.1f);
            Assert.AreEqual(.625f,motion.HeadingAlpha,.0001f);
            if(returning)Assert.AreEqual(.125f,motion.AsideAlpha,.0001f);
            else Assert.AreEqual(1,editPage.transform.Find("WorkspaceEditRight").GetComponent<CanvasGroup>().alpha);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void InterruptedWorkspaceRefreshRestoresBodyAndInput(bool pause)
        {
            var home=Rect("Home");var profile=Rect("Profile");var edit=Rect("ProfileEdit");
            var a=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelProfileHub"),profile);
            var b=Object.Instantiate(Resources.Load<GameObject>("Desktop/Prefabs/PanelProfileEdit"),edit);
            a.name="PanelProfileHub";b.name="PanelProfileEdit";a.SetActive(true);b.SetActive(true);
            var router=root.AddComponent<PanelRouter>();router.Register(ShellPanelId.Home,home.gameObject);
            router.Register(ShellPanelId.Profile,profile.gameObject);router.Register(ShellPanelId.ProfileEdit,edit.gameObject);
            router.Show(ShellPanelId.Profile);CozyMotion.Tick(.2f);
            router.Show(ShellPanelId.ProfileEdit,true);CozyMotion.Tick(.05f);
            if(pause)CozyMotion.SetPaused(true);else CozyMotion.CancelPageTransition();
            Assert.IsFalse(profile.gameObject.activeSelf);Assert.IsTrue(edit.gameObject.activeSelf);
            Assert.AreEqual(1,edit.GetComponent<CanvasGroup>().alpha);Assert.IsTrue(edit.GetComponent<CanvasGroup>().blocksRaycasts);
            Assert.AreEqual(1,b.transform.Find("Form").GetComponentInChildren<CozyWorkspaceBodyGraphic>().Opacity);
            CozyMotion.SetPaused(false);
        }

        [Test]
        public void AsyncProfileRegionJoinsExistingEntranceInsteadOfAppearingFullyOrRestarting()
        {
            var page=Rect("Page");var pose=page.gameObject.AddComponent<CozyJournalFlipPose>();
            var tab=Rect("Tab",page);var title=Rect("Title",tab);var aside=Rect("Aside",tab);
            var motion=tab.gameObject.AddComponent<CozyJournalPageMotion>();motion.Configure(title,aside,null);
            pose.BeginEntrance(.1f);
            typeof(CozyJournalPageMotion).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(motion,null);
            Assert.AreEqual(.125f,motion.AsideAlpha,.001f);
            pose.BeginEntrance(.65f);
            typeof(CozyJournalPageMotion).GetMethod("OnEnable",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(motion,null);
            Assert.AreEqual(1,motion.AsideAlpha);
        }

        [Test]
        public void ContentArrivingDuringRiseIsCoveredAndCancelledOpenRestoresIt()
        {
            var home = Rect("Home"); var page = Rect("Page");
            Rect("JournalShadow", page).gameObject.AddComponent<Image>();
            CozyMotion.Flip(home.gameObject, page.gameObject, null);
            var late = Rect("LateData", page).gameObject.AddComponent<Image>();
            var pose = page.GetComponent<CozyJournalFlipPose>();
            typeof(CozyJournalFlipPose).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pose, null);
            var effect = late.GetComponent<CozyJournalFlipGraphic>();
            Assert.IsNotNull(effect);
            using (var mesh = new VertexHelper())
            {
                var v = UIVertex.simpleVert; mesh.AddVert(v); effect.ModifyMesh(mesh); mesh.PopulateUIVertex(ref v, 0);
                Assert.AreEqual(0, v.color.a);
            }
            CozyMotion.CancelPageTransition();
            Assert.IsFalse(CozyMotion.EntranceHeld); Assert.AreEqual(1, pose.ContentAlpha);
            Assert.AreEqual(Vector2.zero, page.anchoredPosition);
        }

        [Test]
        public void AsyncFeedOrRankCompletionDoesNotReplayVisibleHeadingAndAside()
        {
            var title = Rect("Title"); var aside = Rect("Aside");
            var motion = root.AddComponent<CozyJournalPageMotion>(); motion.Configure(title, aside, null);
            motion.PlayEntrance(2); motion.Advance(.5f);
            Assert.AreEqual(1, motion.HeadingAlpha); Assert.AreEqual(1, motion.AsideAlpha);
            motion.ContentReady(20);
            Assert.AreEqual(1, motion.HeadingAlpha); Assert.AreEqual(1, motion.AsideAlpha);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DialogUsesWebTimingAndKeepsItsDimStationary(bool reduce)
        {
            CozyMotion.SetReduced(reduce);
            var paper = Rect("Paper"); var dim = Rect("Dim", paper);
            paper.gameObject.AddComponent<Image>(); dim.gameObject.AddComponent<Image>();
            paper.anchoredPosition = new Vector2(30, 40); dim.anchoredPosition = new Vector2(-30, -40);
            var position = dim.position;
            CozyWorkspaceDialogMotion.Play(paper, dim);
            var motion = paper.GetComponent<CozyWorkspaceDialogMotion>();
            Assert.AreEqual(0, motion.Opacity);
            Assert.AreEqual(1, paper.GetComponent<CanvasGroup>().alpha, "The dim stays visible even in the first frame.");
            using (var mesh = new VertexHelper())
            {
                var vertex = UIVertex.simpleVert; mesh.AddVert(vertex);
                paper.GetComponent<CozyDialogFadeGraphic>().ModifyMesh(mesh); mesh.PopulateUIVertex(ref vertex, 0);
                Assert.AreEqual(0, vertex.color.a, "Only the paper is transparent at frame zero.");
            }
            Assert.IsNull(dim.GetComponent<CozyDialogFadeGraphic>());
            Assert.AreEqual(position, dim.position);
            Assert.AreEqual(reduce ? 40 : 24, paper.anchoredPosition.y);
            CozyMotion.Tick(.06f);
            Assert.That(motion.Opacity, Is.GreaterThan(0).And.LessThan(1));
            Assert.AreEqual(position, dim.position);
            CozyMotion.Tick(reduce ? .06f : .14f);
            Assert.AreEqual(1, motion.Opacity);
            Assert.AreEqual(new Vector2(30, 40), paper.anchoredPosition);
            CozyWorkspaceDialogMotion.Play(paper, dim);
            paper.GetComponent<CozyWorkspaceDialogMotion>().Finish();
            Assert.AreEqual(1, paper.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(position, dim.position);
        }

        [Test]
        public void ReducedEntrancesKeepShortFadesWithoutStaggerOrScale()
        {
            CozyMotion.SetReduced(true);
            var index = new CozyIndexMotion(true); index.BeginEntrance(4, true);
            Assert.AreEqual(-16, index.Current.X); Assert.AreEqual(0, index.Current.Alpha);
            index.Advance(.06f); Assert.That(index.Current.Alpha, Is.GreaterThan(0).And.LessThan(1));
            Assert.AreEqual(-16, index.Current.X);
            index.Advance(.06f); Assert.AreEqual(1, index.Current.Alpha);
            var card = Rect("Card"); card.gameObject.AddComponent<Image>();
            var sticker = CozyStickerMotion.Bind(card.gameObject, "reduced-new-card", 5);
            sticker.RestartEntrance(); Assert.IsFalse(sticker.EntranceComplete);
            sticker.Advance(.06f); Assert.IsFalse(sticker.EntranceComplete); Assert.AreEqual(1, sticker.AppliedScale);
            sticker.Advance(.06f); Assert.IsTrue(sticker.EntranceComplete);
            var photo = Rect("Photo"); var image = photo.gameObject.AddComponent<Image>();
            var pose = photo.gameObject.AddComponent<CozyJournalStaticPose>(); pose.Configure(-2, image);
            pose.PlayReveal(new Vector2(24, 0));
            Assert.AreEqual(1, pose.AppliedScale); Assert.AreEqual(Vector2.zero, pose.AppliedOffset);
            Assert.AreEqual(.3f, pose.AppliedOpacity, .001f);
            pose.Advance(.12f); Assert.AreEqual(1, pose.AppliedOpacity);
        }

        [Test]
        public void WorkspaceButtonUsesTwoPixelTravelWithoutPressSquash()
        {
            var rect = Rect("Action"); rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>(); CatTheme.StyleButton(button);
            CozyWorkspaceInteraction.Bind(button);
            var motion = button.GetComponent<CozyWorkspaceInteraction>();
            var visual = button.GetComponent<CozyButtonVisual>();
            var data = new PointerEventData(null) { button = PointerEventData.InputButton.Left };
            visual.OnPointerEnter(data); motion.OnPointerEnter(data); motion.Advance(.16f);
            Assert.AreEqual(2, motion.Progress);
            visual.OnPointerDown(data); motion.OnPointerDown(data); CozyMotion.Tick(.06f); motion.Advance(.16f);
            Assert.AreEqual(-2, motion.Progress); Assert.AreEqual(Vector2.one, visual.MotionScale);
            Assert.AreEqual(Vector2.zero, rect.anchoredPosition);
            motion.OnPointerExit(data); motion.Advance(.16f); Assert.AreEqual(0, motion.Progress);
        }

        [Test]
        public void AvatarHoverRotatesOnlyThePortraitAndReducedMotionSettlesIt()
        {
            var rect = Rect("Avatar"); var button = rect.gameObject.AddComponent<Button>();
            var art = Rect("Art", rect).gameObject.AddComponent<Image>();
            CozyWorkspaceInteraction.Bind(button, CozyWorkspaceInteraction.Kind.Avatar, art);
            var motion = button.GetComponent<CozyWorkspaceInteraction>();
            motion.OnPointerEnter(new PointerEventData(null)); motion.Advance(.2f);
            using (var mesh = new VertexHelper())
            {
                var v = UIVertex.simpleVert; v.position = new Vector3(20, 0, 0); mesh.AddVert(v);
                art.GetComponent<CozyGraphicOffset>().ModifyMesh(mesh); mesh.PopulateUIVertex(ref v, 0);
                var expected = Quaternion.Euler(0, 0, 5) * new Vector3(22, 0, 0);
                Assert.That(Vector3.Distance(expected, v.position), Is.LessThan(.001f));
            }
            Assert.AreEqual(Vector3.one, rect.localScale);
            CozyMotion.SetReduced(true); motion.Advance(0); Assert.AreEqual(0, motion.Progress);
        }
    }
}
#endif
