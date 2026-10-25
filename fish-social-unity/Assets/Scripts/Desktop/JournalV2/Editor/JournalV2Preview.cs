#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2.Editor
{
    public static class JournalV2Preview
    {
        public const string Output="Library/JournalV2/Previews/";
        [Serializable] public sealed class Node
        {
            public string path,text,sprite;public bool active;public float x,y,width,height,angle;public int fontSize;
        }
        [Serializable] sealed class Geometry {public string page;public int tab;public Node[] nodes;}
        [MenuItem("Fish Social/预览/日记页面")]
        public static void RenderAll()
        {
            Directory.CreateDirectory(Output);Render("home",0);
            foreach(var id in JournalV2Authoring.PageIds)
            {
                var tabs=id=="profile"?4:id=="shop"||id=="equip"||id=="rank"||id=="feed"||id=="settings"?3:id=="bag"?4:1;
                for(var tab=0;tab<tabs;tab++)Render(id,tab);
            }
        }
        public static void Render(string id,int tab)
        {
            var scene=EditorSceneManager.NewPreviewScene();var target=new RenderTexture(1672,941,24);var previous=RenderTexture.active;Camera camera=null;
            try
            {
                var cameraObject=new GameObject("JournalV2PreviewCamera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraObject,scene);
                camera=cameraObject.GetComponent<Camera>();camera.scene=scene;camera.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.white;camera.orthographic=true;camera.orthographicSize=470.5f;camera.transform.position=new Vector3(836,470.5f,-100);camera.targetTexture=target;
                var shell=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(JournalV2Authoring.Folder+"shell.prefab"));SceneManager.MoveGameObjectToScene(shell,scene);
                var canvas=shell.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;shell.GetComponent<CanvasScaler>().enabled=false;
                var app=shell.GetComponent<JournalV2App>();app.Bind(null,null,true);app.PresentHeaderSnapshot("小橘",7,320,500,8600,24,"characters/avatars/orange");
                if(id!="home")
                {
                    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(JournalV2Authoring.Folder+id+".prefab");
                    if(prefab==null)throw new InvalidOperationException("Missing production prefab: "+id);
                    var page=UnityEngine.Object.Instantiate(prefab,app.Stage.Find("PageHost"),false).GetComponent<JournalV2Page>();
                    if(page.Modal){page.transform.SetParent(app.Stage.Find("ModalHost"),false);app.Stage.Find("ModalHost").gameObject.SetActive(true);}
                    var controller=page.GetComponent<JournalV2Controller>();controller.Bind(app,page);
                    var fixtureName=(id=="shop"||id=="equip"||id=="bag"||id=="map"||id=="ponds"||id=="gallery")?"JournalV2CatalogFixtures":"JournalV2CommunityFixtures";
                    var fixture=typeof(JournalV2Preview).Assembly.GetType("FishSocial.Desktop.JournalV2.Editor."+fixtureName);
                    var fill=fixture?.GetMethod("Fill",BindingFlags.Public|BindingFlags.Static);
                    if(fill==null)throw new InvalidOperationException("Missing real-controller fixture "+fixtureName);
                    fill.Invoke(null,new object[]{app,page,tab});
                    page.GetComponent<JournalV2PageAnimator>().Finish();
                }
                foreach(Transform nav in app.Stage.Find("Nav"))nav.GetComponent<JournalV2Button>()?.SetSelected(nav.name==id||(id=="edit"&&nav.name=="profile"));
                Canvas.ForceUpdateCanvases();
                foreach(var layout in shell.GetComponentsInChildren<LayoutGroup>(true))LayoutRebuilder.ForceRebuildLayoutImmediate(layout.GetComponent<RectTransform>());
                Canvas.ForceUpdateCanvases();
                foreach(var button in shell.GetComponentsInChildren<JournalV2Button>(true))button.Finish();
                foreach(var photo in shell.GetComponentsInChildren<JournalV2Polaroid>(true))photo.Finish();
                var name=id+"-"+tab;
                WriteGeometry(shell,camera,name,id,tab);camera.Render();RenderTexture.active=target;
                var image=new Texture2D(1672,941,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1672,941),0,0);image.Apply();
                File.WriteAllBytes(Output+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            finally
            {
                RenderTexture.active=previous;if(camera!=null)camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);
            }
        }
        static void WriteGeometry(GameObject shell,Camera camera,string name,string page,int tab)
        {
            var nodes=new List<Node>();var corners=new Vector3[4];
            foreach(var rect in shell.GetComponentsInChildren<RectTransform>(true))
            {
                rect.GetWorldCorners(corners);var a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);var b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);var text=rect.GetComponent<Text>();var image=rect.GetComponent<Image>();
                nodes.Add(new Node{path=AnimationUtility.CalculateTransformPath(rect,shell.transform),active=rect.gameObject.activeInHierarchy,x=a.x,y=941-b.y,width=b.x-a.x,height=b.y-a.y,angle=-Mathf.DeltaAngle(0,rect.localEulerAngles.z),text=text?.text,fontSize=text!=null?text.fontSize:0,sprite=image?.sprite!=null?AssetDatabase.GetAssetPath(image.sprite):null});
            }
            File.WriteAllText(Output+name+".geometry.json",JsonUtility.ToJson(new Geometry{page=page,tab=tab,nodes=nodes.ToArray()},true));
        }
    }
}
#endif
