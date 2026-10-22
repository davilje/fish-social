#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyWorkspaceComposeTests
    {
        GameObject canvas;
        DesktopSocialComposeModal modal;
        Transform paper;

        [SetUp]
        public void SetUp()
        {
            canvas = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas));
            modal = DesktopSocialComposeModal.Create(canvas.transform);
            Assert.IsNotNull(modal);
            Assert.IsTrue(modal.BindControls());
            paper = modal.transform.Find("ModalPanel");
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(canvas); }

        [Test]
        public void InventoryCardsKeepEveryCatchAndSelectionUpdatesArtAndFacts()
        {
            var items = new FishInventoryItemDto[12];
            for (var i = 0; i < items.Length; i++) items[i] = new FishInventoryItemDto
            {
                id = "catch-" + i, speciesId = i == 11 ? "crucian" : "koi", sizeM = .20f + i * .02f,
                pondId = "qiandao", caughtAt = 1792650000000L + i * 1000,
            };
            modal.PresentInventory(items, items[8].id);
            var content = paper.Find("FishCards/Content");
            Assert.AreEqual(13, content.childCount, "12 real catches plus the inactive template; no 8-item cap.");
            Assert.IsFalse(content.Find("FishCardTemplate").gameObject.activeSelf);
            Assert.AreEqual("catch-8", Field("_fishId"));
            content.Find("FishCard_11").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("catch-11", Field("_fishId"));
            Assert.AreEqual("0.42 m", paper.Find("CatchPreview/Size").GetComponent<Text>().text);
            Assert.AreEqual(DesktopGameData.CalcWeightKg(.42f).ToString("0.00") + " kg", paper.Find("CatchPreview/Weight").GetComponent<Text>().text);
            Assert.AreEqual(DesktopFishCatalog.SpeciesName("crucian"), paper.Find("CatchPreview/FishName").GetComponent<Text>().text);
            Assert.AreEqual(AssetDatabase.GetAssetPath(CozyItemArt.Fish("crucian")), AssetDatabase.GetAssetPath(paper.Find("CatchPreview/FishArt").GetComponent<Image>().sprite));
            Assert.IsTrue(paper.Find("FishCards").GetComponent<ScrollRect>().vertical);
            modal.PresentInventory(new FishInventoryItemDto[0]);
            Assert.IsFalse(paper.Find("Publish").GetComponent<Button>().interactable);
            Assert.IsFalse(paper.Find("CatchPreview/FishArt").GetComponent<Image>().enabled);
        }

        [Test]
        public void VisibleVisibilityAndFooterControlsReplaceDropdownsAndLockDuringPublication()
        {
            modal.PresentPreview(new[] { new FishInventoryItemDto { id = "a", speciesId = "koi", sizeM = .62f } }, "测试钓友");
            Assert.IsFalse(paper.Find("FishChoice").gameObject.activeSelf);
            Assert.IsFalse(paper.Find("Visibility").gameObject.activeSelf);
            paper.Find("VisibilityFriends").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("friends", Field("_visibility"));
            StringAssert.Contains("好友动态", paper.Find("VisibilityHint").GetComponent<Text>().text);
            paper.Find("VisibilityPublic").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("public", Field("_visibility"));
            Assert.AreEqual("", paper.Find("Story").GetComponent<InputField>().text);
            Assert.IsTrue(paper.Find("Publish").GetComponent<Button>().interactable, "Sharing an optional empty story must remain available.");
            modal.gameObject.SetActive(true);
            typeof(DesktopSocialComposeModal).GetField("_busy", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(modal, true);
            typeof(DesktopSocialComposeModal).GetMethod("RefreshControls", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(modal, null);
            foreach (var name in new[] { "Cancel", "Close", "Publish", "VisibilityPublic", "VisibilityFriends" })
                Assert.IsFalse(paper.Find(name).GetComponent<Button>().interactable, name);
            paper.Find("Cancel").GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(modal.gameObject.activeSelf);
            modal.Close(true);
            Assert.IsFalse(modal.gameObject.activeSelf);
        }

        [Test]
        public void SelectedFishAndVisibilityKeepFlatOrangeBorderAndFillAfterReskin()
        {
            modal.PresentPreview(new[] {
                new FishInventoryItemDto { id = "a", speciesId = "koi" },
                new FishInventoryItemDto { id = "b", speciesId = "crucian" }
            }, "测试钓友");
            var first = paper.Find("FishCards/Content/FishCard_0");
            var second = paper.Find("FishCards/Content/FishCard_1");
            second.GetComponent<Button>().onClick.Invoke();
            paper.Find("VisibilityFriends").GetComponent<Button>().onClick.Invoke();
            CatTheme.SkinTree(modal.transform);
            foreach (var choice in new[] { second, paper.Find("VisibilityFriends") })
            {
                choice.GetComponent<CozyJournalCardVisual>().RefreshNow();
                var wash = choice.Find("SelectionWash").GetComponent<CozyCssGraphic>();
                Assert.IsTrue(wash.gameObject.activeSelf);
                Assert.AreEqual((Color)new Color32(255, 240, 216, 255), wash.color);
                Assert.AreEqual((Color)new Color32(252, 146, 60, 255), wash.borderColor);
                Assert.AreEqual(2, wash.border);
                Assert.AreEqual("Assets/Resources/Desktop/Theme/Cozy/journal/surfaces/input-default.png",
                    AssetDatabase.GetAssetPath(choice.Find("CozyVisual").GetComponent<Image>().sprite));
            }
            Assert.IsFalse(first.Find("SelectionWash").gameObject.activeSelf);
            Assert.IsFalse(paper.Find("VisibilityPublic/SelectionWash").gameObject.activeSelf);
        }

        [Test]
        public void MissingRequestedCatchRequiresExplicitSelectionRatherThanSharingAnotherFish()
        {
            modal.PresentInventory(new[] { new FishInventoryItemDto { id = "other", speciesId = "koi" } }, "already-sold");
            Assert.IsNull(Field("_fishId"));
            Assert.IsFalse(paper.Find("Publish").GetComponent<Button>().interactable);
            StringAssert.Contains("已不在背包", paper.Find("Status").GetComponent<Text>().text);
            paper.Find("FishCards/Content/FishCard_0").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual("other", Field("_fishId"));
            Assert.IsTrue(paper.Find("Publish").GetComponent<Button>().interactable);
            Assert.IsEmpty(paper.Find("Status").GetComponent<Text>().text);
        }

        [Test]
        public void TwoColumnComposerKeepsSelectionAndActionsOutOfTheWritingArea()
        {
            Assert.AreEqual(new Vector2(1120, 752), ((RectTransform)paper).sizeDelta);
            var writing = (RectTransform)paper.Find("Story");
            var choices = (RectTransform)paper.Find("FishCards");
            Assert.GreaterOrEqual(writing.anchoredPosition.x, choices.anchoredPosition.x + choices.sizeDelta.x + 20);
            var publish = (RectTransform)paper.Find("Publish");
            Assert.Greater(-publish.anchoredPosition.y, -choices.anchoredPosition.y + choices.sizeDelta.y);
            Assert.Greater(publish.anchoredPosition.x, ((RectTransform)paper.Find("Cancel")).anchoredPosition.x);
            Assert.AreEqual(210, writing.sizeDelta.y);
        }

        object Field(string name) { return typeof(DesktopSocialComposeModal).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(modal); }
    }
}
#endif
