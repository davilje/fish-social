#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.JournalV2.Editor
{
    [InitializeOnLoad]
    public static class JournalV2Authoring
    {
        public const string Folder="Assets/Resources/Desktop/JournalV2/Prefabs/";
        public static readonly string[] PageIds={"shop","equip","bag","map","ponds","gallery","profile","edit","ach","rank","feed","settings","compose"};
        static double nextCheck;
        static JournalV2Authoring() {EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextCheck||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            nextCheck=EditorApplication.timeSinceStartup+2;
            const string request="Temp/JournalV2.request";
            if(!File.Exists(request))return;
            var action=File.ReadAllText(request).Trim();File.Delete(request);
            try
            {
                if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Journal V2 asset authoring requires Edit Mode.");
                if(PrefabStageUtility.GetCurrentPrefabStage()!=null)throw new InvalidOperationException("Save and close the Prefab Stage before baking.");
                switch(action)
                {
                    case "bake": Bake();break;
                    case "verify": Verify();break;
                    case "preview": JournalV2Preview.RenderAll();break;
                    case "recompile": AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();break;
                    default:throw new InvalidOperationException("Unknown Journal V2 action: "+action);
                }
                File.WriteAllText("Temp/JournalV2.result","OK "+action);
            }
            catch(Exception error){File.WriteAllText("Temp/JournalV2.result",error.ToString());Debug.LogException(error);}
        }
        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            Save(JournalV2Shell.Build(),"shell");
            foreach(var id in PageIds)
            {
                var page=JournalV2Pages.BuildCatalog(null,id)??JournalV2Pages.BuildCommunity(null,id);
                if(page==null)throw new InvalidOperationException("No fresh page builder registered for "+id);
                Save(page.gameObject,id);
            }
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();Verify();
        }
        static void Save(GameObject root,string id)
        {
            try
            {
                root.name=id;root.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(root,Folder+id+".prefab",out var success);
                if(!success)throw new InvalidOperationException("Failed saving "+id);
            }
            finally {UnityEngine.Object.DestroyImmediate(root);}
        }
        [MenuItem("Fish Social/校验/日记页面")]
        public static void Verify()
        {
            var report=new StringBuilder();var count=0;
            foreach(var id in new[]{"shell"})VerifyOne(id,report,ref count);
            foreach(var id in PageIds)VerifyOne(id,report,ref count);
            report.Insert(0,"PASS: "+(PageIds.Length+1)+" independent prefabs; "+count+" components inspected.\n");
            Directory.CreateDirectory("Library/JournalV2");File.WriteAllText("Library/JournalV2/validation.txt",report.ToString());
        }
        static void VerifyOne(string id,StringBuilder report,ref int count)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+id+".prefab");
            if(prefab==null)throw new InvalidOperationException("Missing baked prefab "+id);
            if(PrefabUtility.GetPrefabAssetType(prefab)!=PrefabAssetType.Regular)throw new InvalidOperationException(id+" must be independent, not a legacy prefab variant.");
            var instance=UnityEngine.Object.Instantiate(prefab);
            try
            {
                foreach(var component in instance.GetComponentsInChildren<Component>(true))
                {
                    if(component==null)throw new InvalidOperationException(id+" has a missing script.");
                    count++;
                    var type=component.GetType();var ns=type.Namespace??"";
                    if(component is MonoBehaviour&&ns.StartsWith("FishSocial")&&!ns.StartsWith("FishSocial.Desktop.JournalV2"))
                        throw new InvalidOperationException(id+" retains an old presentation owner: "+type.Name);
                    if(component is LayoutGroup && component.transform.name!="Content")
                        throw new InvalidOperationException(id+" has automatic layout outside a scroll content: "+component.name);
                    if(component is Graphic graphic&&graphic.GetComponent<CanvasRenderer>()==null)
                        throw new InvalidOperationException(id+" has a graphic without CanvasRenderer: "+graphic.name);
                }
                foreach(var button in instance.GetComponentsInChildren<JournalV2Button>(true))
                {
                    var visual=button.transform.Find("Visual") as RectTransform;
                    if(visual==null)throw new InvalidOperationException(id+" button lacks an independent Visual child: "+button.name);
                    var hit=(RectTransform)button.transform;var position=hit.anchoredPosition;
                    button.SetSelected(true);button.Finish();button.SetSelected(false);button.Finish();
                    if(hit.anchoredPosition!=position)throw new InvalidOperationException(id+" animation moved its logical hit area: "+button.name);
                }
                if(id!="shell")
                {
                    var page=instance.GetComponent<JournalV2Page>();
                    if(page==null||page.PageId!=id||instance.GetComponent<JournalV2Controller>()==null)throw new InvalidOperationException(id+" lacks a bound page/controller.");
                    if(page.Body==null||page.CloseButton==null)throw new InvalidOperationException(id+" has missing serialized page references.");
                    var motion=instance.GetComponent<JournalV2PageAnimator>();
                    if(motion==null)throw new InvalidOperationException(id+" lacks a page motion owner.");
                    motion.Enter(true);motion.Sample(.1f);motion.Finish();
                }
                report.AppendLine(id+": native prefab loaded and state sampled after serialization.");
            }
            finally {UnityEngine.Object.DestroyImmediate(instance);}
        }
    }
}
#endif
