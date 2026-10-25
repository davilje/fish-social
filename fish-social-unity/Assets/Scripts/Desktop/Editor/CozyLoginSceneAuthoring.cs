#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FishSocial.Desktop.Editor
{
    /// <summary>Applies the full-window login art and previews the complete shell at desktop sizes.</summary>
    public static class CozyLoginSceneAuthoring
    {
        public const string ArtPath = "Assets/Resources/Desktop/Theme/Cozy/scenes/login-background-cats-v4.png";
        const string ShellPath = "Assets/Resources/Desktop/Prefabs/DesktopShell.prefab";
        const string Output = "../docs/art/login-pastel-2026-10-26/cats-v4/";

        public static void ApplyAndRender()
        {
            AssetDatabase.ImportAsset(ArtPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = Vector4.zero;
            importer.spritePixelsPerUnit = 100;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtPath);
            if (sprite == null) throw new InvalidOperationException("Missing login scene art");
            var shell = PrefabUtility.LoadPrefabContents(ShellPath);
            try
            {
                var login = (RectTransform)shell.transform.Find("LoginRoot");
                login.anchorMin = Vector2.zero;
                login.anchorMax = Vector2.one;
                login.pivot = new Vector2(.5f, .5f);
                login.anchoredPosition = login.sizeDelta = Vector2.zero;
                login.localScale = Vector3.one;
                var oldPlate = login.GetComponent<Image>();
                // Remove the obsolete card graphic; SkinTree may re-enable disabled chrome images.
                if (oldPlate != null) UnityEngine.Object.DestroyImmediate(oldPlate);
                // The render viewport clips the cover image; a full-canvas mask can leave a subpixel seam.
                var obsoleteMask = login.GetComponent<RectMask2D>();
                if (obsoleteMask != null) UnityEngine.Object.DestroyImmediate(obsoleteMask);
                var background = login.Find("SceneBackground") as RectTransform;
                if (background == null)
                {
                    background = new GameObject("SceneBackground", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter)).GetComponent<RectTransform>();
                    background.SetParent(login, false);
                }
                background.gameObject.layer = login.gameObject.layer;
                background.SetAsFirstSibling();
                var image = background.GetComponent<Image>();
                image.sprite = sprite;
                image.color = Color.white;
                image.raycastTarget = false;
                image.type = Image.Type.Simple;
                image.preserveAspect = false; // The fitter sizes the rect; do not letterbox inside it.
                var fitter = background.GetComponent<AspectRatioFitter>();
                // Set the mode first: in the editor, None recomputes and overwrites the ratio.
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
                Place(login.Find("Brand"), 20, 260, 95);
                PlaceText(login.Find("Reflow_BrandCaption"), 118, 680, 28, 22);
                foreach (var text in login.GetComponentsInChildren<Text>(true))
                    if (text.text == "欢迎回来") PlaceText(text.transform, 154, 680, 34, 32);
                PlaceText(login.Find("LoginHint"), 194, 680, 44, 20);
                var button = login.Find("SteamLogin");
                Place(button, 246, 352, 54);
                button.localScale = Vector3.one;
                var note = login.Find("Reflow_AccountNote");
                PlaceText(note, 0, 680, 28, 16);
                var noteRect = (RectTransform)note;
                noteRect.anchorMin = noteRect.anchorMax = new Vector2(.5f, 0);
                noteRect.pivot = new Vector2(.5f, 0);
                noteRect.anchoredPosition = new Vector2(0, 18);
                DesktopShellUi.SetAuthenticatedRoots(shell.transform, false);
                CozyPrefabRepair.RequireValid(shell, "DesktopShell");
                PrefabUtility.SaveAsPrefabAsset(shell, ShellPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(shell); }
            AssetDatabase.SaveAssets();
            RenderAll();
        }

        static void Place(Transform transform, float top, float width, float height)
        {
            var rect = (RectTransform)transform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.anchoredPosition = new Vector2(0, -top);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void PlaceText(Transform transform, float top, float width, float height, int fontSize)
        {
            Place(transform, top, width, height);
            var text = transform.GetComponent<Text>();
            text.fontSize = fontSize;
            text.resizeTextForBestFit = false;
        }

        public static void RenderAll()
        {
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080),
                new Vector2Int(2560, 1440), new Vector2Int(1280, 800), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
                Render(size.x, size.y);
        }

        static void Render(int width, int height)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(width, height, 24);
            var previous = RenderTexture.active;
            Camera camera = null;
            Texture2D pixels = null;
            try
            {
                var go = new GameObject("LoginScenePreviewCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(go, scene);
                camera = go.GetComponent<Camera>();
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.magenta;
                camera.orthographic = true;
                camera.orthographicSize = height / 2f;
                camera.transform.position = new Vector3(width / 2f, height / 2f, -100);
                camera.targetTexture = target;
                var shell = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath));
                SceneManager.MoveGameObjectToScene(shell, scene);
                var canvas = shell.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                // Keep the production 1672x941 CanvasScaler and all authored shell layers.
                // Do not isolate LoginRoot or substitute a 480x320 reference canvas.
                DesktopShellUi.SetAuthenticatedRoots(shell.transform, false);
                CatTheme.SkinTree(shell.transform);
                foreach (var visual in shell.GetComponentsInChildren<CozyButtonVisual>(true)) visual.ApplyFinalPose();
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)shell.transform);
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(Output);
                File.WriteAllBytes(Output + "unity-login-" + width + "x" + height + ".png", pixels.EncodeToPNG());
                Debug.Log("[LoginScene] Rendered " + width + "x" + height + "; canvas=" + ((RectTransform)shell.transform).rect.size + "; scale=" + canvas.scaleFactor);
            }
            finally
            {
                RenderTexture.active = previous;
                if (camera != null) camera.targetTexture = null;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
