#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop;
using FishSocial.Desktop.Editor;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyLoginSceneTests
    {
        const string ShellPath = "Assets/Resources/Desktop/Prefabs/DesktopShell.prefab";

        [Test]
        public void AuthTransitionsNeverShowTheHomeBackdropBehindLogin()
        {
            var shell = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath));
            try
            {
                Assert.IsFalse(shell.transform.Find("Backdrop").gameObject.activeSelf, "Serialized startup must be safe before Build() runs.");
                foreach (var authenticated in new[] { false, true, false, true })
                {
                    DesktopShellUi.SetAuthenticatedRoots(shell.transform, authenticated);
                    Assert.AreEqual(authenticated, shell.transform.Find("Backdrop").gameObject.activeSelf);
                    Assert.AreEqual(authenticated, shell.transform.Find("MainRoot").gameObject.activeSelf);
                    Assert.AreEqual(!authenticated, shell.transform.Find("LoginRoot").gameObject.activeSelf);
                    Assert.NotNull(shell.transform.Find("LoginRoot/SteamLogin").GetComponent<Button>());
                    Assert.NotNull(shell.transform.Find("LoginRoot/LoginHint").GetComponent<Text>());
                }
            }
            finally { Object.DestroyImmediate(shell); }
        }

        [TestCase(1280, 720)]
        [TestCase(1600, 900)]
        [TestCase(1920, 1080)]
        [TestCase(2560, 1440)]
        [TestCase(1280, 800)]
        [TestCase(1024, 768)]
        [TestCase(2560, 1080)]
        public void FullWindowSceneCoversViewportAndKeepsControlsAboveCats(int width, int height)
        {
            var cameraObject = new GameObject("LoginTestCamera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            var target = new RenderTexture(width, height, 24);
            camera.targetTexture = target;
            var shell = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath));
            try
            {
                var canvas = shell.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                var scaler = shell.GetComponent<CanvasScaler>();
                Assert.AreEqual(new Vector2(1672, 941), scaler.referenceResolution);
                Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
                DesktopShellUi.SetAuthenticatedRoots(shell.transform, false);
                CatTheme.SkinTree(shell.transform);
                foreach (var visual in shell.GetComponentsInChildren<CozyButtonVisual>(true)) visual.ApplyFinalPose();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)shell.transform);
                Canvas.ForceUpdateCanvases();
                Assert.That(canvas.scaleFactor, Is.EqualTo(Mathf.Sqrt(width / 1672f * height / 941f)).Within(.01f));
                var login = (RectTransform)shell.transform.Find("LoginRoot");
                var canvasRect = ((RectTransform)shell.transform).rect;
                Assert.That(login.rect.width, Is.EqualTo(canvasRect.width).Within(.1f));
                Assert.That(login.rect.height, Is.EqualTo(canvasRect.height).Within(.1f));
                Assert.IsNull(login.GetComponent<Image>(), "No small rectangular login plate for SkinTree to restore.");
                var scene = login.Find("SceneBackground").GetComponent<Image>();
                Assert.AreEqual(CozyLoginSceneAuthoring.ArtPath, AssetDatabase.GetAssetPath(scene.sprite));
                Assert.IsFalse(scene.raycastTarget);
                var bounds = BoundsIn(login, scene.rectTransform);
                Assert.LessOrEqual(bounds.xMin, login.rect.xMin + .1f);
                Assert.GreaterOrEqual(bounds.xMax, login.rect.xMax - .1f);
                Assert.LessOrEqual(bounds.yMin, login.rect.yMin + .1f);
                Assert.GreaterOrEqual(bounds.yMax, login.rect.yMax - .1f);
                Assert.That(bounds.width / bounds.height, Is.EqualTo(scene.sprite.rect.width / scene.sprite.rect.height).Within(.001f));
                foreach (var graphic in login.GetComponentsInChildren<Graphic>(false))
                {
                    if (!graphic.enabled || graphic == scene) continue;
                    var r = BoundsIn(login, graphic.rectTransform);
                    Assert.GreaterOrEqual(r.xMin, login.rect.xMin - .5f, graphic.name);
                    Assert.LessOrEqual(r.xMax, login.rect.xMax + .5f, graphic.name);
                    Assert.GreaterOrEqual(r.yMin, login.rect.yMin - .5f, graphic.name);
                    Assert.LessOrEqual(r.yMax, login.rect.yMax + .5f, graphic.name);
                }
                // Art's topmost ear is at y~397/941. Keep the button clear of it,
                // including when cover crops the upper/lower edges on a wide monitor.
                var button = BoundsIn(login, (RectTransform)login.Find("SteamLogin"));
                var catTop = bounds.yMax - bounds.height * (397f / 941f);
                Assert.Greater(button.yMin, catTop + 16, "Login button intrudes into the five-cat gathering.");
                var catLeft = bounds.xMin + bounds.width * .25f;
                var catRight = bounds.xMin + bounds.width * .72f;
                Assert.GreaterOrEqual(catLeft, login.rect.xMin);
                Assert.LessOrEqual(catRight, login.rect.xMax);
            }
            finally
            {
                Object.DestroyImmediate(shell);
                camera.targetTexture = null;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        static Rect BoundsIn(RectTransform parent, RectTransform child)
        {
            var corners = new Vector3[4]; child.GetWorldCorners(corners);
            var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (var world in corners)
            {
                var local = (Vector2)parent.InverseTransformPoint(world);
                min = Vector2.Min(min, local); max = Vector2.Max(max, local);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
#endif
