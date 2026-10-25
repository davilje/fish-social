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
    public static class CozyFishCatalogPreview
    {
        [MenuItem("Fish Social/预览/完整鱼图鉴")]
        public static void RenderAll()
        {
            AssetDatabase.Refresh();
            var species = DesktopGameData.Species;
            for (var start = 0; start < species.Length; start += 10)
                RenderPage(species, start);
        }

        static void RenderPage(DesktopGameData.SpeciesDef[] species, int start)
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(1600, 1000, 24);
            var previous = RenderTexture.active;
            Texture2D pixels = null;
            Camera camera = null;
            try
            {
                var cameraObject = new GameObject("Camera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene;
                camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.97f, .94f, .88f);
                camera.orthographic = true;
                camera.orthographicSize = 500;
                camera.transform.position = new Vector3(800, 500, -100);
                camera.targetTexture = target;
                var root = new GameObject("FishCatalog", typeof(RectTransform), typeof(Canvas));
                SceneManager.MoveGameObjectToScene(root, scene);
                var canvas = root.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10;
                var labelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Desktop/Prefabs/ShowcaseSlot.prefab");
                var font = labelPrefab.transform.Find("Label").GetComponent<Text>().font;
                for (var offset = 0; offset < 10 && start + offset < species.Length; offset++)
                {
                    var entry = species[start + offset];
                    var sprite = CozyItemArt.Fish(entry.speciesId);
                    if (sprite == null) throw new InvalidOperationException("Missing fish illustration: " + entry.speciesId);
                    var left = 24 + offset % 5 * 316;
                    var top = 24 + offset / 5 * 480;
                    var card = Node(root.transform, entry.speciesId, left, top, 292, 456);
                    card.gameObject.AddComponent<Image>().color = new Color(1f, .985f, .95f);
                    var art = Node(card, "Art", 12, 20, 268, 310).gameObject.AddComponent<Image>();
                    art.sprite = sprite;
                    art.preserveAspect = true;
                    var title = Node(card, "Name", 8, 340, 276, 52).gameObject.AddComponent<Text>();
                    title.font = font;
                    title.text = (start + offset + 1).ToString("00") + "  " + entry.name;
                    title.fontSize = 26;
                    title.alignment = TextAnchor.MiddleCenter;
                    title.color = new Color(.38f, .23f, .14f);
                    var id = Node(card, "SpeciesId", 8, 396, 276, 40).gameObject.AddComponent<Text>();
                    id.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    id.text = entry.speciesId;
                    id.fontSize = 20;
                    id.alignment = TextAnchor.MiddleCenter;
                    id.color = title.color;
                }
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory("Library/CozyFishCatalog");
                File.WriteAllBytes("Library/CozyFishCatalog/catalog-" + (start / 10 + 1) + ".png", pixels.EncodeToPNG());
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

        static RectTransform Node(Transform parent, string name, float left, float top, float width, float height)
        {
            var node = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            node.SetParent(parent, false);
            node.anchorMin = node.anchorMax = Vector2.up;
            node.pivot = new Vector2(.5f, .5f);
            node.anchoredPosition = new Vector2(left + width / 2, -top - height / 2);
            node.sizeDelta = new Vector2(width, height);
            return node;
        }
    }
}
#endif
