#if UNITY_INCLUDE_TESTS
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyGalleryParityTests
    {
        GameObject page;
        DesktopGalleryModalView view;
        [SetUp]
        public void SetUp()
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/PanelGallery");
            Assert.IsNotNull(prefab);
            page = Object.Instantiate(prefab); page.name = "PanelGallery";
            view = page.GetComponent<DesktopGalleryModalView>();
            Assert.IsNotNull(view); view.Bind(null, null);
        }
        [TearDown]
        public void TearDown()
        {
            if (page == null) return;
            foreach (var behaviour in page.GetComponentsInChildren<MonoBehaviour>(true))
                if (behaviour != null && behaviour.GetType().Namespace == "FishSocial.Desktop")
                    behaviour.GetType().GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)?.Invoke(behaviour, null);
            Object.DestroyImmediate(page);
        }
        [Test]
        public void GalleryRetainsScrollContractAndFourColumnCardsWithNativeFishArt()
        {
            var scroll = page.transform.Find("Grid").GetComponent<ScrollRect>();
            Assert.AreSame(page.transform.Find("Grid/Viewport"), scroll.viewport);
            Assert.AreSame(page.transform.Find("Grid/Viewport/Species"), scroll.content);
            var layout = scroll.content.GetComponent<GridLayoutGroup>();
            Assert.AreEqual(4, layout.constraintCount);
            Assert.AreEqual(new Vector2(210, 190), layout.cellSize);
            var card = Resources.Load<GameObject>("Desktop/Prefabs/GallerySpeciesSlot");
            Assert.IsNotNull(card.GetComponent<Button>());
            Assert.IsNotNull(card.transform.Find("FishArt").GetComponent<Image>());
            Assert.IsNotNull(card.transform.Find("Label").GetComponent<Text>());
            Assert.IsNotNull(card.transform.Find("Rarity").GetComponent<Text>());
            Assert.IsNotNull(page.transform.Find("Detail/Viewport/Body").GetComponent<Text>());
            Assert.IsNotNull(page.transform.Find("GalleryAside/DesignArt").GetComponent<Image>());
            Assert.IsTrue(page.transform.Find("Retry").gameObject.activeSelf);
        }
        [Test]
        public void ReceivedCodexSnapshotDisplaysUnlockedArtAndClickSelectsLockedSilhouette()
        {
            var catalog = DesktopGameData.Species;
            Assert.Greater(catalog.Length, 1);
            var unlockedId = catalog[0].speciesId; var lockedId = catalog[1].speciesId;
            var snapshot = new[] { new FishCodexEntryDto { speciesId = unlockedId, totalCaught = 7, maxSizeM = .58f } };
            view.PresentEntries(snapshot);
            var grid = page.transform.Find("Grid/Viewport/Species");
            Assert.AreEqual(catalog.Length, grid.childCount);
            var unlocked = grid.Find(unlockedId); var locked = grid.Find(lockedId);
            Assert.AreEqual(DesktopGameData.SpeciesName(unlockedId), unlocked.Find("Label").GetComponent<Text>().text);
            Assert.AreEqual(Color.white, unlocked.Find("FishArt").GetComponent<Image>().color);
            Assert.IsNotNull(unlocked.Find("FishArt").GetComponent<Image>().sprite);
            Assert.AreEqual("？？？", locked.Find("Label").GetComponent<Text>().text);
            Assert.AreEqual(new Color(0, 0, 0, .26f), locked.Find("FishArt").GetComponent<Image>().color);
            Assert.That(page.transform.Find("Detail/Viewport/Body").GetComponent<Text>().text, Does.Contain("累计捕获：7"));
            locked.GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(lockedId, view.SelectedSpeciesId);
            Assert.AreSame(locked, grid.Find(lockedId), "Selecting must not rebuild rows and replay every entrance.");
            Assert.IsTrue(locked.GetComponent<CozyButtonVisual>().Selected);
            Assert.IsFalse(unlocked.GetComponent<CozyButtonVisual>().Selected);
            Assert.IsTrue(page.transform.Find("GalleryAside/LockedHint").gameObject.activeSelf);
            Assert.AreEqual("？？？", page.transform.Find("GalleryAside/DesignName").GetComponent<Text>().text);
            Assert.That(page.transform.Find("Detail/Viewport/Body").GetComponent<Text>().text, Does.Contain("尚未解锁"));
            view.PresentEntries(snapshot);
            Assert.AreEqual(catalog.Length, grid.childCount, "Refresh must detach retired rows before Unity's deferred Destroy.");
            Assert.AreEqual(lockedId, view.SelectedSpeciesId);
        }
    }
}
#endif
