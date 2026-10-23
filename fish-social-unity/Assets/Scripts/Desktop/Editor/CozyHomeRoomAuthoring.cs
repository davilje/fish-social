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
    public sealed class CozyHomeRoomImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if(!assetPath.StartsWith("Assets/Resources/Desktop/Theme/Cozy/home-room/",StringComparison.Ordinal))return;
            var importer=(TextureImporter)assetImporter;
            importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
            importer.maxTextureSize=2048;importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.wrapMode=TextureWrapMode.Clamp;
            importer.filterMode=FilterMode.Bilinear;
            importer.npotScale=TextureImporterNPOTScale.None;
            var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
        }
        public override uint GetVersion() => 2;
    }
    public static class CozyHomeRoomAuthoring
    {
        const string Folder="Assets/Resources/Desktop/Prefabs/";
        [MenuItem("Fish Social/Home Room/Apply approved layered room")]
        public static void Bake()
        {
            var shell=PrefabUtility.LoadPrefabContents(Folder+"DesktopShell.prefab");
            try
            {
                var main=shell.transform.Find("MainRoot");
                var corner=main.Find("CozyCornerCat");if(corner!=null)UnityEngine.Object.DestroyImmediate(corner.gameObject);
                CozyHomeRoom.Build(main);
                CozyRedesignAuthoring.SaveJournalPrefab(shell,Folder+"DesktopShell.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(shell);}
            var home=PrefabUtility.LoadPrefabContents(Folder+"PanelHome.prefab");
            try
            {
                foreach(Transform child in home.transform)child.gameObject.SetActive(false);
                var graphic=home.GetComponent<Graphic>();if(graphic!=null)graphic.enabled=false;
                CozyPrefabRepair.RepairContents(home);
                CozyRedesignAuthoring.SaveJournalPrefab(home,Folder+"PanelHome.prefab");
            }
            finally{PrefabUtility.UnloadPrefabContents(home);}
            AssetDatabase.SaveAssets();VerifyAndRender();
        }
        [MenuItem("Fish Social/Home Room/Verify and render saved room")]
        public static void VerifyAndRender()
        {
            var scene=EditorSceneManager.NewPreviewScene();var target=new RenderTexture(1672,941,24);var previous=RenderTexture.active;
            var reduced=CozyMotion.Reduced;var paused=CozyMotion.Paused;
            try
            {
                var cameraObject=new GameObject("RoomCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
                var camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.white;camera.orthographic=true;camera.orthographicSize=470.5f;
                camera.transform.position=new Vector3(836,470.5f,-100);camera.targetTexture=target;
                var shell=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"DesktopShell.prefab"));SceneManager.MoveGameObjectToScene(shell,scene);
                var canvas=shell.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;shell.GetComponent<CanvasScaler>().enabled=false;
                shell.transform.Find("LoginRoot").gameObject.SetActive(false);var main=shell.transform.Find("MainRoot");main.gameObject.SetActive(true);
                foreach(Transform slot in main.Find("Content"))slot.gameObject.SetActive(false);
                var room=CozyHomeRoom.Build(main);room.SetPageOpen(false);
                CatTheme.SkinTree(shell.transform);
                Require(main.Find("CozyCornerCat")==null,"Duplicate corner cat remains");
                Require(!main.Find("Background").gameObject.activeSelf,"Outdoor background covers room");
                Require(room.transform.Find("Props").childCount==17,"Expected 17 independent prop instances");
                Require(room.Prop("rod-1") != null && room.Prop("cat-bed") != null && room.Prop("food-bowls") != null && room.Prop("cat-climber") != null,"Prop input bindings missing");
                Require(room.transform.Find("RoomBase").GetComponent<Image>().sprite!=null,"Missing room background");
                foreach(var image in room.transform.Find("Props").GetComponentsInChildren<Image>(true))Require(image.sprite!=null,"Missing sprite: "+image.name);
                var clicks=0;room.Bind(()=>clicks++,()=>clicks+=10);
                room.transform.Find("Equipment").GetComponent<Button>().onClick.Invoke();room.transform.Find("Aquarium").GetComponent<Button>().onClick.Invoke();Require(clicks==11,"Furniture routes not bound");
                room.MoveTo(new Vector2(-100,2000),true);Require(room.Feet==new Vector2(580,775),"Movement escapes floor");
                room.Sit();Require(room.IsSitting,"Sit pose unavailable");room.Respond();Require(!room.IsSitting,"Tap did not wake companion");
                room.SetPageOpen(true);var position=room.Feet;room.MoveTo(new Vector2(900,700),true);Require(room.Feet==position&&!room.transform.Find("Companion").gameObject.activeSelf,"Page failed to suspend interaction");
                room.SetPageOpen(false);CozyMotion.SetReduced(true);room.MoveTo(new Vector2(835,742));Require(room.Feet==new Vector2(835,742),"Reduced motion did not settle immediately");
                CozyMotion.SetPaused(true);room.Advance(30);Require(!room.IsSitting,"Paused room advanced");CozyMotion.SetPaused(false);CozyMotion.SetReduced(false);
                room.Advance(30);Require(room.IsSitting,"Idle companion did not sit");room.MoveTo(new Vector2(835,742),true);
                CozyItemArt.Text(main,"Header/ProfileCard/Name","小橘");CozyItemArt.Text(main,"Header/ProfileCard/Level","Lv.7");
                CozyItemArt.Text(main,"Header/FishCoins/Amount","8,600");CozyItemArt.Text(main,"Header/CatchCount/Amount","24");
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;
                Directory.CreateDirectory("Library/HomeRoom");
                SaveImage("unity-home-room.png");
                room.SleepInBed();room.Advance(10);Require(room.IsSleeping,"Bed action failed to settle into sleep");
                Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;SaveImage("unity-home-room-sleep.png");
                room.MoveTo(new Vector2(835,742),true);
                File.WriteAllText("Library/HomeRoom/verification.txt","PASS: saved shell, 17 independent props, no duplicate corner cat, both native routes, movement bounds, tap wake, sit, page suspension, reduced motion, background pause, idle sit.\n");
                Debug.Log("[HomeRoom] Verification passed; Library/HomeRoom/unity-home-room.png");
            }
            finally {CozyMotion.SetReduced(reduced);CozyMotion.SetPaused(paused);RenderTexture.active=previous;target.Release();UnityEngine.Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException("Home room verification: "+message);}
        static void SaveImage(string name)
        {
            var output=new Texture2D(1672,941,TextureFormat.RGBA32,false);output.ReadPixels(new Rect(0,0,1672,941),0,0);output.Apply();
            File.WriteAllBytes("Library/HomeRoom/"+name,output.EncodeToPNG());UnityEngine.Object.DestroyImmediate(output);
        }
    }
}
#endif
