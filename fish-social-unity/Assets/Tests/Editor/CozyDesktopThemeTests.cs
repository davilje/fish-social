#if UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FishSocial.Desktop;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyDesktopThemeTests
    {
        GameObject _root;
        GameObject _events;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ThemeTest", typeof(RectTransform));
            _events = new GameObject("ThemeTestEvents", typeof(EventSystem));
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_root);
            UnityEngine.Object.DestroyImmediate(_events);
        }

        [Test]
        public void ExistingRouteValuesRemainStableAndNewRoutesAreUnique()
        {
            Assert.AreEqual(0, (int)ShellPanelId.Home);
            Assert.AreEqual(1, (int)ShellPanelId.Pond);
            Assert.AreEqual(11, (int)ShellPanelId.Leaderboard);
            Assert.AreEqual(12, (int)ShellPanelId.PondSelection);
            Assert.AreEqual(13, (int)ShellPanelId.Equipment);
            Assert.AreEqual(14, (int)ShellPanelId.Achievements);
            var values = Enum.GetValues(typeof(ShellPanelId));
            Assert.AreEqual(15, values.Length);
        }

        [Test]
        public void ApprovedRasterAssetsArePackagedAndLoadable()
        {
            foreach (var state in new[] { "default", "hover", "pressed" })
                Assert.NotNull(CatTheme.LoadCozy("buttons/plate-" + state + ".png", CatTheme.PlateBorder));
            foreach (var icon in new[] { "home", "map", "pond", "hook", "bag", "rod", "shop", "medal", "trophy", "user", "social" })
                Assert.NotNull(CatTheme.LoadCozy("icons/" + icon + ".png", Vector4.zero), icon);
            Assert.NotNull(CatTheme.LoadCozy("scenes/home-sakura.png", Vector4.zero));
            Assert.NotNull(CatTheme.LoadCozy("scenes/world-map.png", Vector4.zero));
            Assert.NotNull(CatTheme.LoadCozy("brand/meow-fish.png", Vector4.zero));
            Assert.NotNull(CatTheme.LoadCozy("characters/cat-portrait.png", Vector4.zero));
            Assert.AreSame(Resources.Load<Sprite>("Desktop/Theme/Cozy/panels/paper"),
                CatTheme.LoadCozy("panels/paper.png", CatTheme.PaperBorder));
            Assert.NotNull(CatTheme.LoadCozy("panels/paper.png", CatTheme.PaperBorder));
        }

        [Test]
        public void PressUsesApprovedPlateWithoutMovingHitTargetOrReplacingClick()
        {
            var clicks = 0;
            var button = DesktopModalUi.MakeButton(_root.transform, "Bag", "背包", () => clicks++);
            CatTheme.StyleButton(button, "bag");
            var rect = button.GetComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(30f, 70f);
            var visual = button.GetComponent<CozyButtonVisual>();
            var plate = button.transform.Find("CozyVisual").GetComponent<Image>();
            var pointer = new PointerEventData(_events.GetComponent<EventSystem>())
            {
                button = PointerEventData.InputButton.Left
            };
            visual.OnPointerEnter(pointer);
            Assert.AreSame(CatTheme.LoadCozy("buttons/plate-hover.png", CatTheme.PlateBorder), plate.sprite);
            visual.OnPointerDown(pointer);
            Assert.AreSame(CatTheme.LoadCozy("buttons/plate-pressed.png", CatTheme.PlateBorder), plate.sprite);
            Assert.AreEqual(new Vector2(0f, -3f), plate.rectTransform.anchoredPosition);
            Assert.AreEqual(new Vector2(30f, 70f), rect.anchoredPosition);
            Assert.IsEmpty(plate.GetComponents<Shadow>());
            visual.OnPointerUp(pointer);
            Assert.AreEqual(Vector2.zero, plate.rectTransform.anchoredPosition);
            button.onClick.Invoke();
            Assert.AreEqual(1, clicks);
        }

        [Test]
        public void PrimaryPressKeepsItsTintAndRasterIconsAreNotDoubleTinted()
        {
            var button = DesktopModalUi.MakeButton(_root.transform, "Start", "开始钓鱼", () => { });
            CatTheme.StyleButton(button, "hook", true);
            var plate = button.transform.Find("CozyVisual").GetComponent<Image>();
            var normalTint = plate.color;
            var pointer = new PointerEventData(_events.GetComponent<EventSystem>())
            {
                button = PointerEventData.InputButton.Left
            };
            button.GetComponent<CozyButtonVisual>().OnPointerDown(pointer);
            Assert.AreEqual(normalTint, plate.color);
            Assert.AreEqual(Color.white, button.transform.Find("CozyIcon").GetComponent<Image>().color);
        }

        [Test]
        public void RepeatedStylingRetainsSelectionAndDoesNotDuplicateVisuals()
        {
            var button = DesktopModalUi.MakeButton(_root.transform, "Map", "地图", () => { });
            CatTheme.StyleButton(button, "map");
            var visual = button.GetComponent<CozyButtonVisual>();
            visual.SetSelected(true);
            visual.SetStackedIcon(true);
            CatTheme.SkinTree(_root.transform);
            CatTheme.SkinTree(_root.transform);
            Assert.IsTrue(visual.Selected);
            Assert.AreEqual(1, button.GetComponents<CozyButtonVisual>().Length);
            Assert.NotNull(button.transform.Find("CozyIcon"));
        }

        [Test]
        public void FilledImagesKeepTheirFillAndBusinessColor()
        {
            var node = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            node.transform.SetParent(_root.transform, false);
            var image = node.GetComponent<Image>();
            image.type = Image.Type.Filled;
            image.fillAmount = 0.35f;
            image.color = Color.green;
            CatTheme.SkinTree(_root.transform);
            Assert.AreEqual(Image.Type.Filled, image.type);
            Assert.AreEqual(0.35f, image.fillAmount);
            Assert.AreEqual(Color.green, image.color);
        }

        [Test]
        public void ThemeLoaderRejectsPathsOutsideTheAssetPack()
        {
            Assert.IsNull(CatTheme.LoadCozy("../btn-coral.png", Vector4.zero));
            Assert.IsNull(CatTheme.LoadCozy("/tmp/other.png", Vector4.zero));
            Assert.IsNull(CatTheme.LoadCozy("C:/other.png", Vector4.zero));
        }
    }
}
#endif
