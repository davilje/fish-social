#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting.Proxies;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyBagCommentRegressionTests
    {
        const string Folder = "Assets/Resources/Desktop/Prefabs/";
        GameObject root;
        static object Invoke(object target, string method, params object[] args) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        static void Drain(IEnumerator routine) { while (routine.MoveNext()) if (routine.Current is IEnumerator nested) Drain(nested); }
        GameObject Load(string name)
        {
            root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab"));
            root.name = name; root.SetActive(true); return root;
        }
        [TearDown] public void Cleanup()
        {
            if (root != null) Object.DestroyImmediate(root);
            CozyMotion.CancelPageTransition();
        }
        sealed class BagApi : RealProxy
        {
            public int Requests;
            public BagApi() : base(typeof(IAuthenticatedApiClient)) { }
            public IAuthenticatedApiClient Client => (IAuthenticatedApiClient)GetTransparentProxy();
            public override IMessage Invoke(IMessage message)
            {
                var call = (IMethodCallMessage)message;
                object result = null;
                if (call.MethodName == "get_CanUse") result = true;
                else if (call.MethodName == "get_PlayerId") result = "bag-test";
                else if (call.MethodName == "get_BaseUrl") result = "https://example.invalid";
                else { Requests++; result = Empty(); }
                return new ReturnMessage(result, null, 0, call.LogicalCallContext, call);
            }
            static IEnumerator Empty() { yield break; }
        }
        DesktopCatchBagModalView Bag(BagApi api)
        {
            var view = Load("PanelCatch").GetComponent<DesktopCatchBagModalView>();
            view.Bind(api.Client, null);
            Invoke(view, "OnInventory", (object)new[] { new FishInventoryItemDto {
                id = "catch-one", speciesId = "koi", quality = "rare", sizeM = .86f } });
            return view;
        }
        [Test]
        public void ItemsTabNeverLoadsEquipmentAndReturningRestoresSelectedCatchWithoutRebuildingCards()
        {
            var api = new BagApi(); var view = Bag(api);
            var grid = root.transform.Find("Grid/Viewport/Slots");
            var card = grid.GetChild(0);
            var name = root.transform.Find("DesignName").GetComponent<Text>().text;
            for (var i = 0; i < 3; i++)
            {
                root.transform.Find("DesignCategoryItems").GetComponent<Button>().onClick.Invoke();
                Drain((IEnumerator)Invoke(view, "Load"));
                Assert.IsTrue(root.transform.Find("BagItemsEmpty").gameObject.activeSelf);
                Assert.IsFalse(grid.gameObject.activeSelf);
                foreach (var path in new[] { "Reflow_CatchDetails", "Sell", "Return", "Share", "Detail" })
                    Assert.IsFalse(root.transform.Find(path).gameObject.activeSelf, path);
                Assert.IsNull(root.transform.Find("EquipItem"));
                view.OnClosed(); view.OnOpened();
                root.transform.Find("DesignCategoryFish").GetComponent<Button>().onClick.Invoke();
                Assert.IsTrue(grid.gameObject.activeSelf);
                Assert.AreSame(card, grid.GetChild(0), "Category changes must retain fish cards and their selection.");
                Assert.AreEqual(name, root.transform.Find("DesignName").GetComponent<Text>().text);
                Assert.IsTrue(root.transform.Find("DetailRows/Row0").gameObject.activeSelf);
                Assert.IsTrue(root.transform.Find("Sell").GetComponent<Button>().interactable);
            }
            Assert.AreEqual(0, api.Requests, "Items must not fetch the shop catalog or equip anything.");
        }
        [Test]
        public void EmptyFishBagKeepsActionsDisabledAfterVisitingItems()
        {
            var view = Bag(new BagApi());
            Invoke(view, "OnInventory", (object)Array.Empty<FishInventoryItemDto>());
            root.transform.Find("DesignCategoryItems").GetComponent<Button>().onClick.Invoke();
            root.transform.Find("DesignCategoryFish").GetComponent<Button>().onClick.Invoke();
            StringAssert.Contains("背包是空的", root.transform.Find("Detail").GetComponent<Text>().text);
            foreach (var path in new[] { "Sell", "Return", "Share" })
                Assert.IsFalse(root.transform.Find(path).GetComponent<Button>().interactable, path);
            Assert.IsEmpty(root.transform.Find("DesignName").GetComponent<Text>().text);
        }

        [TestCase(.75f)] [TestCase(1f)] [TestCase(1.5f)]
        public void DetailAndActionsHaveSeparateBoundsAndPrimaryMatchesWebSize(float scale)
        {
            Bag(new BagApi()); root.transform.localScale = Vector3.one * scale;
            CozyJournalInlineContent.ReflowTree(root.transform);
            Canvas.ForceUpdateCanvases();
            var detail = (RectTransform)root.transform.Find("DetailRows");
            var sell = (RectTransform)root.transform.Find("Sell");
            Assert.AreEqual(new Vector2(204, 58), sell.rect.size);
            var bounds = Bounds(detail);
            Rect? previous = null;
            foreach (var path in new[] { "Sell", "Return", "Share" })
            {
                var button = (RectTransform)root.transform.Find(path);
                var action = Bounds(button);
                Assert.GreaterOrEqual(bounds.yMin - action.yMax, 12 * scale, path + " overlaps details");
                if (previous.HasValue) Assert.GreaterOrEqual(action.xMin - previous.Value.xMax, 9 * scale);
                var label = Bounds((RectTransform)button.Find("Label"));
                Assert.GreaterOrEqual(label.xMin, action.xMin - .01f);
                Assert.LessOrEqual(label.xMax, action.xMax + .01f);
                Assert.AreEqual(action.center.y, label.center.y, .01f);
                previous = action;
            }
        }
        static Rect Bounds(RectTransform rect)
        {
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }
        [TestCase("你好，钓友！")] [TestCase("第一行\n第二行")] [TestCase(" ")]
        public void CommentsHideHintWhileTypingAndRestoreItAfterClearAndReopen(string message)
        {
            var feed = Load("PanelSocialFeed");
            var post = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "SocialPostCard.prefab"),
                feed.transform.Find("Scroll/Viewport/Content"));
            var card = post.GetComponent<DesktopSocialPostCard>();
            card.Bind(new SocialPostDto { id = "post", nickname = "阿花", text = "今天钓到鱼啦" }, null);
            feed.GetComponent<DesktopSocialFeedPanel>().PresentSelectedComments(card, new PostCommentDto[0], 0);
            var panel = feed.transform.Find("DesignFeedInfo/CommentsHost/CommentsPanel");
            var input = panel.Find("CommentInput").GetComponent<InputField>();
            Assert.AreSame(input.transform.Find("Placeholder").GetComponent<Text>(), input.placeholder);
            Assert.AreNotSame(input.textComponent, input.placeholder);
            Assert.IsTrue(input.placeholder.enabled);
            input.text = message;
            Assert.IsFalse(input.placeholder.enabled);
            Assert.AreEqual(message, input.text);
            Assert.IsNotEmpty(input.textComponent.text); // Multi-line fields render only the visible lines.
            panel.gameObject.SetActive(false); panel.gameObject.SetActive(true);
            DesktopModalUi.StyleTree(panel);
            Assert.IsFalse(input.placeholder.enabled, "Restyling or reopening must not reveal the hint over text.");
            input.text = "";
            Assert.IsTrue(input.placeholder.enabled);
            Assert.IsEmpty(input.textComponent.text);
        }
        [Test]
        public void SharedInputPrefabRetainsHintBindingInNestedInstances()
        {
            var input = Load("Library/CozyInputBase").GetComponent<InputField>();
            Assert.NotNull(input.placeholder);
            input.text = new string('鱼', 200);
            Assert.IsFalse(input.placeholder.enabled);
            input.SetTextWithoutNotify("");
            Assert.IsTrue(input.placeholder.enabled);
        }
    }
}
#endif
