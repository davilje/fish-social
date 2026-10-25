#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop.Editor
{
    public static class CozyHomeAnimationAuthoring
    {
        const string ShellPath = "Assets/Resources/Desktop/Prefabs/DesktopShell.prefab";
        const string Output = "Library/HomeRoomAnimation";

        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void BakeAndVerify()
        {
            var shell = PrefabUtility.LoadPrefabContents(ShellPath);
            try
            {
                CozyHomeRoom.Build(shell.transform.Find("MainRoot"));
                CozyRedesignAuthoring.SaveJournalPrefab(shell, ShellPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(shell); }
            AssetDatabase.SaveAssets();
            VerifyAndRender();
        }

        [MenuItem("Fish Social/预览/小屋动画")]
        public static void VerifyAndRender()
        {
            var scene = EditorSceneManager.NewPreviewScene();
            var target = new RenderTexture(1672, 941, 24);
            var previous = RenderTexture.active;
            var reduced = CozyMotion.Reduced;
            var paused = CozyMotion.Paused;
            var profile = DesktopProfileCache.Latest;
            try
            {
                CozyMotion.SetReduced(false); CozyMotion.SetPaused(false);
                DesktopProfileCache.Latest = new PlayerProfileDto { avatarUrl = "/image/profile/cat_avatar_orange.png" };
                var cameraObject = new GameObject("HomeAnimationCamera", typeof(Camera));
                SceneManager.MoveGameObjectToScene(cameraObject, scene);
                var camera = cameraObject.GetComponent<Camera>();
                camera.scene = scene; camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.white;
                camera.orthographic = true; camera.orthographicSize = 470.5f;
                camera.transform.position = new Vector3(836, 470.5f, -100); camera.targetTexture = target;
                var shell = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ShellPath));
                SceneManager.MoveGameObjectToScene(shell, scene);
                var canvas = shell.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 10;
                shell.GetComponent<CanvasScaler>().enabled = false;
                DesktopShellUi.SetAuthenticatedRoots(shell.transform, true);
                var main = shell.transform.Find("MainRoot"); main.gameObject.SetActive(true);
                foreach (Transform slot in main.Find("Content")) slot.gameObject.SetActive(false);
                var room = CozyHomeRoom.Build(main); room.SetPageOpen(false);
                if (!room.HasFeedingAnimation) throw new InvalidOperationException("Select orange for home animation verification.");
                Directory.CreateDirectory(Output);
                room.MoveTo(new Vector2(835, 742), true);
                Save(camera, target, "00-idle");
                room.Respond(); room.Advance(.58f);
                Save(camera, target, "01-roll");
                room.Advance(1f);
                room.MoveTo(new Vector2(1000, 742)); room.Advance(.58f);
                Save(camera, target, "02-walk");
                room.MoveTo(new Vector2(835, 742), true);
                room.Feed(); room.Advance(.58f);
                Save(camera, target, "03-pour");
                for (var step = 0; step < 300 && room.CurrentActivity != CozyHomeRoom.Activity.Eating; step++) room.Advance(1f / 24f);
                if (room.CurrentActivity != CozyHomeRoom.Activity.Eating) throw new InvalidOperationException("Feed did not reach eating.");
                room.Advance(.4f); Save(camera, target, "04-eat");
                room.Advance(.75f); Save(camera, target, "05-eat-second-loop");
                room.Advance(.75f); Save(camera, target, "06-eat-third-loop");
                room.Advance(6f); Save(camera, target, "07-returned");
                if (room.IsFeeding || room.transform.Find("FoodLayer").gameObject.activeSelf) throw new InvalidOperationException("Feed did not clean up.");
                room.Feed(); room.Advance(.5f); room.SetPageOpen(true);
                if (room.IsFeeding || room.transform.Find("FoodPour").gameObject.activeSelf) throw new InvalidOperationException("Page cancellation failed.");
                File.WriteAllText(Output + "/verification.txt", "PASS: saved food layers; orange roll, walk, pour, approach, three eating loops, return; page cancellation. Contact alignment remains art-review quality.\n");
                Debug.Log("[HomeAnimation] Verification and renders passed: " + Output);
            }
            finally
            {
                CozyMotion.SetReduced(reduced); CozyMotion.SetPaused(paused);
                DesktopProfileCache.Latest = profile;
                RenderTexture.active = previous; target.Release(); UnityEngine.Object.DestroyImmediate(target);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Save(Camera camera, RenderTexture target, string name)
        {
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }
}
#endif
