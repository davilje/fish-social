#if UNITY_INCLUDE_TESTS
using System.Reflection;
using FishSocial.Desktop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyJournalFlipParityTests
    {
        GameObject root;
        int preference;

        [SetUp]
        public void SetUp()
        {
            preference = PlayerPrefs.GetInt(CozyMotion.ReducePrefsKey, 0);
            CozyMotion.CancelPageTransition();
            CozyMotion.SetPaused(false);
            CozyMotion.SetReduced(false);
            root = new GameObject("FlipParityTests", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            CozyMotion.CancelPageTransition();
            CozyMotion.Cancel(root.transform as RectTransform, true);
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Namespace == "FishSocial.Desktop")
                    behaviour.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.Invoke(behaviour, null);
            Object.DestroyImmediate(root);
            CozyMotion.SetPaused(false);
            CozyMotion.SetReduced(preference == 1);
        }

        RectTransform Node(string name, Transform parent)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            return (RectTransform)node.transform;
        }

        Image Art(string name, Transform parent)
        {
            var rect = Node(name, parent);
            rect.sizeDelta = new Vector2(120, 60);
            return rect.gameObject.AddComponent<Image>();
        }

        GameObject Page(string name, bool entrance = false, Transform parent = null)
        {
            var page = Node(name, parent != null ? parent : root.transform);
            page.sizeDelta = new Vector2(1380, 680);
            page.anchoredPosition = new Vector2(51, 73);
            page.gameObject.AddComponent<Image>();
            Art("JournalShadow", page); Art("JournalEdge", page); Art("JournalDots", page);
            Art("JournalGrain", page); Art("JournalStitch", page); Art("JournalRing0", page);
            var header = Node("DesignHeader", page);
            Art("Title", header); Art("Subtitle", header); Art("Icon", header);
            Art("Content", page); Art("Aside", page); Art("Heading", page);
            if (entrance)
            {
                var motion = page.gameObject.AddComponent<CozyJournalPageMotion>();
                motion.Configure(page.Find("Heading"), page.Find("Aside"), null, page.Find("Content"));
                // Runtime-only MonoBehaviours do not receive OnEnable in the deterministic EditMode runner.
                typeof(CozyJournalPageMotion).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)?.Invoke(motion, null);
            }
            return page.gameObject;
        }

        static UIVertex Sample(Image image)
        {
            using (var helper = new VertexHelper())
            {
                var vertex = UIVertex.simpleVert;
                vertex.position = new Vector3(7, 11, 0);
                vertex.color = new Color32(20, 40, 60, 200);
                helper.AddVert(vertex);
                var effect = image.GetComponent<CozyJournalFlipGraphic>();
                if (effect != null) effect.ModifyMesh(helper);
                helper.PopulateUIVertex(ref vertex, 0);
                return vertex;
            }
        }

        static Image Find(GameObject page, string path) => page.transform.Find(path).GetComponent<Image>();
        static void Rest(GameObject page)
        {
            Assert.AreEqual(new Vector2(51, 73), ((RectTransform)page.transform).anchoredPosition);
            Assert.AreEqual(Vector3.one, page.transform.localScale);
            Assert.AreEqual(Quaternion.identity, page.transform.localRotation);
            var group = page.GetComponent<CanvasGroup>();
            if (group != null) { Assert.AreEqual(1, group.alpha); Assert.IsTrue(group.blocksRaycasts); }
            var pose = page.GetComponent<CozyJournalFlipPose>();
            if (pose != null) { Assert.AreEqual(0, pose.ContentOffset); Assert.AreEqual(1, pose.ContentAlpha); }
        }

        [Test]
        public void ContentAndTitleUseIndependentMeshOffsetsWhilePaperHeaderAndHitGeometryStayFixed()
        {
            var page = Page("Shop");
            var pose = page.AddComponent<CozyJournalFlipPose>();
            var hit = Find(page, "Content").gameObject.AddComponent<Button>();
            var hitRect = (RectTransform)hit.transform;
            var before = hitRect.anchoredPosition;
            pose.Sample(24, .25f, 10, .5f);
            var content = Sample(Find(page, "Content")); var title = Sample(Find(page, "DesignHeader/Title"));
            Assert.AreEqual(new Vector3(31, 11, 0), content.position); Assert.AreEqual(50, content.color.a);
            Assert.AreEqual(new Vector3(17, 11, 0), title.position); Assert.AreEqual(100, title.color.a);
            foreach (var path in new[] { "JournalShadow", "JournalEdge", "JournalDots", "JournalGrain", "JournalStitch", "JournalRing0", "DesignHeader/Icon", "DesignHeader/Subtitle" })
            {
                var vertex = Sample(Find(page, path));
                Assert.AreEqual(new Vector3(7, 11, 0), vertex.position, path); Assert.AreEqual(200, vertex.color.a, path);
            }
            Assert.AreEqual(before, hitRect.anchoredPosition); Assert.AreEqual(Vector3.one, hitRect.localScale);
            Assert.AreEqual(Quaternion.identity, hitRect.localRotation);
            pose.ResetPose();
            Assert.AreEqual(new Vector3(7, 11, 0), Sample(Find(page, "Content")).position);
            Assert.AreEqual(200, Sample(Find(page, "Content")).color.a);
        }

        [Test]
        public void RouterSlotKeepsThePaperStillWhileTheWrappedPageSlides()
        {
            var oldSlot = Node("Shop", root.transform);
            var newSlot = Node("Bag", root.transform);
            var oldPage = Page("PanelShop", false, oldSlot);
            var newPage = Page("PanelBag", false, newSlot);
            newSlot.gameObject.SetActive(false);
            CozyMotion.Flip(oldSlot.gameObject, newSlot.gameObject, null);
            CozyMotion.Tick(.06f);
            Assert.AreEqual(new Vector3(7, 11, 0), Sample(Find(oldPage, "JournalEdge")).position);
            Assert.Less(Sample(Find(oldPage, "Content")).position.x, 7f);
            Assert.AreEqual(200, Sample(Find(oldPage, "DesignHeader/Icon")).color.a);
            Assert.AreEqual(new Vector3(7, 11, 0), Sample(Find(oldPage, "DesignHeader/Title")).position);
            CozyMotion.Tick(.061f);
            Assert.AreEqual(1, newSlot.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(new Vector3(7, 11, 0), Sample(Find(newPage, "JournalEdge")).position);
            Assert.AreEqual(new Vector3(7, 11, 0), Sample(Find(newPage, "JournalRing0")).position);
            Assert.AreEqual(new Vector3(31, 11, 0), Sample(Find(newPage, "Content")).position);
            Assert.AreEqual(0, Sample(Find(newPage, "Content")).color.a);
            Assert.AreEqual(new Vector3(17, 11, 0), Sample(Find(newPage, "DesignHeader/Title")).position);
            Assert.AreEqual(0, Sample(Find(newPage, "DesignHeader/Title")).color.a);
            Assert.AreEqual(200, Sample(Find(newPage, "DesignHeader/Icon")).color.a);
        }

        [Test]
        public void OldContentExitsBeforeIncomingPaperAndItsTitleSlideBecomeVisible()
        {
            var oldPage = Page("Shop"); var newPage = Page("Bag"); newPage.SetActive(false);
            CozyMotion.Flip(oldPage, newPage, null);
            CozyMotion.Tick(.06f);
            var oldPose = oldPage.GetComponent<CozyJournalFlipPose>();
            Assert.That(oldPose.ContentOffset, Is.LessThan(0).And.GreaterThan(-24));
            Assert.AreEqual(0, newPage.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(200, Sample(Find(oldPage, "DesignHeader/Title")).color.a);
            Assert.AreEqual(new Vector3(7, 11, 0), Sample(Find(oldPage, "JournalEdge")).position);
            CozyMotion.Tick(.061f);
            Assert.IsFalse(oldPage.activeSelf); Assert.AreEqual(1, newPage.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(24, newPage.GetComponent<CozyJournalFlipPose>().ContentOffset);
            Assert.AreEqual(new Vector3(17, 11, 0), Sample(Find(newPage, "DesignHeader/Title")).position);
            Assert.AreEqual(0, Sample(Find(newPage, "DesignHeader/Title")).color.a);
            CozyMotion.Tick(.2f);
            Rest(oldPage); Rest(newPage);
        }

        [Test]
        public void FadingSheetHoldsThePolaroidUntilThePageIsOpaque()
        {
            var page = Page("Shop");
            var group = page.AddComponent<CanvasGroup>();
            var photo = Art("JournalPolaroid", page.transform);
            var pose = photo.gameObject.AddComponent<CozyJournalStaticPose>();
            pose.Configure(-2f, photo);
            var visible = typeof(CozyJournalStaticPose).GetMethod(
                "HierarchyVisible",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(visible);
            group.alpha = 0f;
            Assert.IsFalse((bool)visible.Invoke(pose, null), "A concealed sheet must not spend the polaroid reveal.");
            group.alpha = .2f;
            Assert.IsFalse((bool)visible.Invoke(pose, null), "The rise must finish before the polaroid starts.");
            group.alpha = .98f;
            Assert.IsTrue((bool)visible.Invoke(pose, null), "The polaroid plays once the sheet is on screen.");
        }

        [Test]
        public void OpeningFromHomeRaisesTheSheetBeforeTheMenuAndCards()
        {
            var home = Page("Home");
            var shop = Page("Shop");
            var card = Art("Card", shop.transform);
            var motion = CozyStickerMotion.Bind(card.gameObject, "home-door", 0);
            motion.ApplyFinalPose();
            motion.RestartEntrance();
            Assert.IsFalse(motion.EntranceComplete);
            var nested = Art("JournalRing1", Node("PanelShop", shop.transform));
            nested.rectTransform.pivot = new Vector2(0f, 1f);
            nested.rectTransform.sizeDelta = new Vector2(74f, 44f);
            nested.rectTransform.anchoredPosition = new Vector2(-34f, -300f);
            var ringRest = nested.rectTransform.anchoredPosition;

            CozyMotion.Flip(home, shop, null);
            Assert.IsFalse(motion.EntranceComplete, "The finished cards are not drawn while the sheet rises.");
            Assert.AreEqual(1.12f, motion.AppliedScale);
            Assert.AreEqual(0f, shop.GetComponent<CanvasGroup>().alpha);
            var ring = shop.transform.Find("JournalRing0") as RectTransform;
            Assert.AreEqual(Vector3.one, ring.localScale);
            Assert.AreEqual(Vector3.one, nested.rectTransform.localScale);

            CozyMotion.Tick(.1f);
            Assert.IsFalse(motion.EntranceComplete, "The 200ms rise does not play the cards.");
            Assert.AreEqual(1.12f, motion.AppliedScale);
            Assert.Greater(shop.GetComponent<CanvasGroup>().alpha, 0f);
            Assert.Less(shop.GetComponent<CanvasGroup>().alpha, 1f);

            CozyMotion.Tick(.11f);
            Assert.IsFalse(motion.EntranceComplete, "Index, rings and cards start after the sheet has arrived.");
            Assert.AreEqual(1.12f, motion.AppliedScale);
            Assert.AreEqual(1f, shop.GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(.6f, ring.localScale.y, .001f);
            Assert.AreEqual(.6f, nested.rectTransform.localScale.y, .001f);
            Assert.AreEqual(ringRest.y - 8.8f, nested.rectTransform.anchoredPosition.y, .05f);
        }

        [Test]
        public void CardEntranceRestartsOnlyAfterOutgoingContentFinishes()
        {
            var oldPage = Page("Shop"); var newPage = Page("Bag");
            var card = Art("Card", newPage.transform);
            var motion = CozyStickerMotion.Bind(card.gameObject, "flip-entrance", 0);
            motion.ApplyFinalPose();
            CozyMotion.Flip(oldPage, newPage, null);
            CozyMotion.Tick(.06f); Assert.IsTrue(motion.EntranceComplete);
            CozyMotion.Tick(.061f); Assert.IsFalse(motion.EntranceComplete); Assert.AreEqual(1.12f, motion.AppliedScale);
            Assert.AreEqual(Vector2.zero, card.rectTransform.anchoredPosition);
        }

        [Test]
        public void RapidNavigationDuringIncomingSlideRestoresOldPagesAndCallsOnlyLatestCompletion()
        {
            var first = Page("Shop"); var second = Page("Bag"); var third = Page("Equipment");
            var stale = 0; var latest = 0;
            CozyMotion.Flip(first, second, () => stale++); CozyMotion.Tick(.121f); CozyMotion.Tick(.05f);
            CozyMotion.Flip(second, third, () => latest++); CozyMotion.Tick(.121f); CozyMotion.Tick(.2f);
            Assert.AreEqual(0, stale); Assert.AreEqual(1, latest);
            Assert.IsFalse(first.activeSelf); Assert.IsFalse(second.activeSelf); Assert.IsTrue(third.activeSelf);
            Rest(first); Rest(second); Rest(third);
            CozyMotion.Tick(1); Assert.AreEqual(1, latest);
        }

        [Test]
        public void CloseDuringIncomingSlideRestoresPoseAndDoesNotLeaveHiddenInputBlockers()
        {
            var first = Page("Shop"); var second = Page("Bag"); var home = Page("Home");
            var stale = 0; var closed = 0;
            CozyMotion.Flip(first, second, () => stale++); CozyMotion.Tick(.121f); CozyMotion.Tick(.03f);
            CozyMotion.Flip(second, home, () => closed++); CozyMotion.Tick(.14f);
            Assert.AreEqual(0, stale); Assert.AreEqual(1, closed);
            Assert.IsFalse(first.activeSelf); Assert.IsFalse(second.activeSelf); Assert.IsTrue(home.activeSelf);
            Rest(first); Rest(second); Rest(home);
            Assert.AreEqual(new Vector3(7, 11, 0), Sample(Find(home, "Content")).position);
        }

        [Test]
        public void ReducedDuringIncomingSlideRestoresNestedEntranceGroupsImmediately()
        {
            var first = Page("Shop"); var second = Page("Bag", true); var completed = 0;
            CozyMotion.Flip(first, second, () => completed++); CozyMotion.Tick(.121f);
            CozyMotion.SetReduced(true);
            Assert.AreEqual(1, completed); Rest(first); Rest(second);
            Assert.AreEqual(1, second.transform.Find("Heading").GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(1, second.transform.Find("Aside").GetComponent<CanvasGroup>().alpha);
            Assert.IsTrue(second.transform.Find("Aside").GetComponent<CanvasGroup>().blocksRaycasts);
            CozyMotion.Tick(1); Assert.AreEqual(1, completed);
        }

        [Test]
        public void PauseDuringIncomingSlideLeavesNoInvisibleNestedContentOnRestore()
        {
            var first = Page("Shop"); var second = Page("Bag", true);
            CozyMotion.Flip(first, second, null); CozyMotion.Tick(.121f);
            CozyMotion.SetPaused(true); Rest(second);
            Assert.AreEqual(1, second.transform.Find("Heading").GetComponent<CanvasGroup>().alpha);
            Assert.AreEqual(1, second.transform.Find("Aside").GetComponent<CanvasGroup>().alpha);
            CozyMotion.SetPaused(false);
            Assert.AreEqual(200, Sample(Find(second, "Content")).color.a);
        }

        [Test]
        public void ExplicitCancellationRestoresBothPagesWithoutCallingStaleCompletion()
        {
            var first = Page("Shop"); var second = Page("Bag"); var callbacks = 0;
            CozyMotion.Flip(first, second, () => callbacks++); CozyMotion.Tick(.121f); CozyMotion.Tick(.05f);
            CozyMotion.CancelPageTransition(); CozyMotion.Tick(1);
            Assert.AreEqual(0, callbacks); Rest(first); Rest(second);
            Assert.IsFalse(first.activeSelf); Assert.IsTrue(second.activeSelf);
            Assert.AreEqual(200, Sample(Find(second, "DesignHeader/Title")).color.a);
        }
    }
}
#endif
