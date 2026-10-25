#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FishSocial.Desktop.Editor
{
    // Asset preparation and isolated sample prefabs only: never rewrites live game pages.
    public static class CozyJournalResourcePreparation
    {
        public const string Root = "Assets/Resources/Desktop/Theme/Cozy/journal/";
        const string Report = "../docs/art/cozy-journal/previews/";

        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void Prepare()
        {
            Directory.CreateDirectory(Report);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImports();
            ExportPonds();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImports();
            Validate();
            BuildComponents();
            RenderSamples();
            WriteThemeManifest();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Report + "unity-result.txt", "PASS: imports, sprites, nine-slice borders, 60 character poses, 21 pond renders and reference sample prefabs rendered.\n");
        }

        public static bool Configure(TextureImporter importer)
        {
            if (!importer.assetPath.StartsWith(Root, StringComparison.Ordinal)) return false;
            var relative = importer.assetPath.Substring(Root.Length);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 4096;
            importer.spritePixelsPerUnit = 200;
            importer.spritePivot = new Vector2(.5f, .5f);
            importer.spriteBorder = Vector4.zero;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            if (relative.StartsWith("characters/")) importer.spritePivot = new Vector2(.5f, .1f);
            if (relative.StartsWith("ponds/")) importer.spritePixelsPerUnit = 100;
            var manifestFile = Root + "ui-manifest.json";
            if (File.Exists(manifestFile))
            {
                var manifest = JObject.Parse(File.ReadAllText(manifestFile));
                foreach (var item in manifest["assets"])
                {
                    if ((string)item["relativePath"] != "journal/" + relative) continue;
                    var b = item["border"].Select(v => (float)v).ToArray();
                    importer.spriteBorder = new Vector4(b[0], b[1], b[2], b[3]);
                    var pivot = item["pivot"].Select(v => (float)v).ToArray();
                    importer.spritePivot = new Vector2(pivot[0], pivot[1]);
                    importer.spritePixelsPerUnit = (float)item["pixelsPerUnit"];
                    importer.wrapMode = (string)item["wrapMode"] == "Repeat" ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                    break;
                }
            }
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = importer.spritePivot;
            importer.SetTextureSettings(settings);
            return true;
        }

        static void ConfigureImports()
        {
            foreach (var file in Directory.GetFiles(Root, "*.png", SearchOption.AllDirectories))
            {
                var path = file.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) throw new InvalidOperationException("Missing importer: " + path);
                Configure(importer);
                importer.SaveAndReimport();
            }
        }

        static void ExportPonds()
        {
            Directory.CreateDirectory(Root + "ponds");
            var rows = new JArray();
            foreach (var file in Directory.GetFiles("Assets/Desktop/OverlayLayouts", "pond-*.prefab").OrderBy(p => p))
            {
                var id = Path.GetFileNameWithoutExtension(file);
                var scene = EditorSceneManager.NewPreviewScene();
                var target = new RenderTexture(1024, 640, 24);
                var previous = RenderTexture.active;
                try
                {
                    var camera = CreateCamera(scene, target, new Color(.92f, .95f, .90f, 1));
                    // A real central 896x560 viewport, no vertical stretching of the 1680x560 scene.
                    camera.orthographicSize = 280;
                    camera.transform.position = new Vector3(840, 280, -100);
                    var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\','/')));
                    SceneManager.MoveGameObjectToScene(root, scene);
                    root.transform.position = Vector3.zero;
                    foreach (var item in root.GetComponentsInChildren<DesktopOverlayLayoutObject>(true))
                    {
                        // Keep physical seats and scene art; remove sample cats, labels and status HUD.
                        if (item.kind == "waiting" || item.kind == "pet-size" ||
                            (item.kind.StartsWith("actor-", StringComparison.Ordinal) && item.kind != "actor-seat"))
                            item.gameObject.SetActive(false);
                    }
                    foreach (var text in root.GetComponentsInChildren<Text>(true)) text.gameObject.SetActive(false);
                    Canvas.ForceUpdateCanvases();
                    camera.Render();
                    SavePixels(target, Root + "ponds/" + id + ".png");
                    rows.Add(new JObject { ["pondId"] = id, ["sourcePrefab"] = file.Replace('\\','/'),
                        ["sourceDependencyHash"] = AssetDatabase.GetAssetDependencyHash(file.Replace('\\','/')).ToString(),
                        ["resourcesPath"] = "Desktop/Theme/Cozy/journal/ponds/" + id,
                        ["width"] = 1024, ["height"] = 640, ["cropCenter"] = new JArray(.5, .5),
                        ["sourceViewport"] = new JArray(392,0,896,560), ["kind"] = "actual-scene-render-no-players" });
                }
                finally { RenderTexture.active = previous; target.Release(); Object.DestroyImmediate(target); EditorSceneManager.ClosePreviewScene(scene); }
            }
            File.WriteAllText(Root + "pond-manifest.json", new JObject { ["schema"] = 1, ["ponds"] = rows }.ToString());
        }

        static Camera CreateCamera(Scene scene, RenderTexture target, Color color)
        {
            var go = new GameObject("JournalArtCamera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(go, scene);
            var camera = go.GetComponent<Camera>();
            camera.scene = scene;
            camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = color;
            camera.orthographic = true;
            camera.orthographicSize = 470.5f;
            camera.transform.position = new Vector3(836,470.5f,-100);
            camera.targetTexture = target;
            return camera;
        }

        static void SavePixels(RenderTexture target, string path)
        {
            RenderTexture.active = target;
            var pixels = new Texture2D(target.width,target.height,TextureFormat.RGBA32,false);
            try { pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0); pixels.Apply(); File.WriteAllBytes(path,pixels.EncodeToPNG()); }
            finally { Object.DestroyImmediate(pixels); }
        }

        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void Validate()
        {
            var items = JObject.Parse(File.ReadAllText(Root+"ui-manifest.json"))["assets"];
            foreach (var item in items)
            {
                var p = "Assets/Resources/" + (string)item["resourcesPath"] + ".png";
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(p);
                if (sprite == null) throw new Exception("Missing sprite: " + p);
                var b = item["border"].Select(v => (float)v).ToArray();
                if (sprite.border != new Vector4(b[0],b[1],b[2],b[3])) throw new Exception("Wrong border: " + p);
                if (Mathf.Abs(sprite.pixelsPerUnit-(float)item["pixelsPerUnit"])>0.01f) throw new Exception("Wrong PPU: " + p);
            }
            foreach (var p in Directory.GetFiles(Root,"*.png",SearchOption.AllDirectories))
                if (AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\','/')) == null) throw new Exception("Not imported as sprite: " + p);
            var ponds = JObject.Parse(File.ReadAllText(Root+"pond-manifest.json"))["ponds"];
            if (ponds.Count()!=21) throw new Exception("Expected 21 scene previews, got " + ponds.Count());
            foreach (var item in ponds)
                if (Resources.Load<Sprite>((string)item["resourcesPath"]) == null) throw new Exception("Unresolved pond: "+item["pondId"]);
            var characters = JObject.Parse(File.ReadAllText(Root+"character-manifest.json"))["assets"];
            if (characters.Count()!=60) throw new Exception("Expected 60 registered character poses.");
            foreach(var character in characters)
            {
                var sprite = Resources.Load<Sprite>((string)character["resourcesPath"]);
                if(sprite==null || sprite.rect.width!=384 || sprite.rect.height!=328)
                    throw new Exception("Character canvas mismatch: "+character["resourcesPath"]);
            }
        }

        static int PngInt(byte[] bytes,int offset)
        {
            return (bytes[offset]<<24)|(bytes[offset+1]<<16)|(bytes[offset+2]<<8)|bytes[offset+3];
        }

        static void WriteThemeManifest()
        {
            var assets = new JObject();
            const string folder = "Assets/Resources/Desktop/Theme/Cozy/";
            foreach(var file in Directory.GetFiles(folder,"*.png",SearchOption.AllDirectories).OrderBy(p=>p))
            {
                var path=file.Replace('\\','/');var relative=path.Substring(folder.Length);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                var bytes=File.ReadAllBytes(path);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                var sprites=new JArray();long mainId=0;
                foreach(var sprite in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>())
                {
                    string spriteGuid;long fileId;AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,out spriteGuid,out fileId);
                    if(importer.spriteImportMode==SpriteImportMode.Single)mainId=fileId;
                    sprites.Add(new JObject { ["name"]=sprite.name,["fileID"]=fileId,
                        ["rect"]=new JArray(sprite.rect.x,sprite.rect.y,sprite.rect.width,sprite.rect.height),
                        ["pivotPixels"]=new JArray(sprite.pivot.x,sprite.pivot.y) });
                }
                var b=importer.spriteBorder;
                assets[relative]=new JObject { ["guid"]=AssetDatabase.AssetPathToGUID(path),["fileID"]=mainId,
                    ["size"]=new JArray(PngInt(bytes,16),PngInt(bytes,20)),
                    ["importedSize"]=new JArray(texture.width,texture.height),
                    ["border"]=new JArray(b.x,b.y,b.z,b.w),["pixelsPerUnit"]=importer.spritePixelsPerUnit,
                    ["spriteMode"]=importer.spriteImportMode.ToString(),["sprites"]=sprites,
                    ["resourcesPath"]="Desktop/Theme/Cozy/"+relative.Substring(0,relative.Length-4) };
            }
            File.WriteAllText(folder+"asset-manifest.json",new JObject { ["schema"]=1,
                ["referenceResolution"]=new JArray(1672,941),["defaultRuntimeFallback"]=false,
                ["generatedBy"]="CozyJournalResourcePreparation from imported AssetDatabase",
                ["spriteAssets"]=assets }.ToString());
        }

        static Image Picture(Transform parent,string name,string asset,float x,float y,float w,float h,Color? tint=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            go.transform.SetParent(parent,false); var rt=(RectTransform)go.transform;
            rt.anchorMin=rt.anchorMax=rt.pivot=Vector2.up;rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);
            var image=go.GetComponent<Image>();image.sprite=asset.StartsWith("@",StringComparison.Ordinal)?CozyItemArt.Get(asset.Substring(1)):Resources.Load<Sprite>("Desktop/Theme/Cozy/"+asset);
            if(image.sprite==null)throw new Exception("Sample missing sprite: "+asset);
            image.type=image.sprite.border==Vector4.zero?Image.Type.Simple:Image.Type.Sliced;
            image.color=tint??Color.white;image.raycastTarget=false;
            if(asset.StartsWith("journal/",StringComparison.Ordinal))
            {
                var spec=JObject.Parse(File.ReadAllText(Root+"ui-manifest.json"))["assets"].FirstOrDefault(a=>(string)a["relativePath"]==asset);
                if(spec!=null && spec["contentInsets"]!=null && spec["contentInsets"].Any(v=>(float)v>0))
                {
                    var insets=spec["contentInsets"].Select(v=>(float)v).ToArray();
                    var visual=new GameObject("Padded artwork",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));visual.transform.SetParent(go.transform,false);
                    var vr=(RectTransform)visual.transform;vr.anchorMin=vr.anchorMax=vr.pivot=Vector2.up;vr.anchoredPosition=new Vector2(-insets[0],insets[3]);vr.sizeDelta=new Vector2(w+insets[0]+insets[2],h+insets[1]+insets[3]);
                    var inner=visual.GetComponent<Image>();inner.sprite=image.sprite;inner.type=image.type;inner.color=image.color;inner.raycastTarget=false;
                    image.sprite=null;image.color=Color.clear;
                }
            }
            return image;
        }
        static Text Label(Transform parent,string name,string text,float x,float y,float w,float h,int size,Color? color=null)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=Vector2.up;rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);
            var t=go.GetComponent<Text>();t.font=CatTheme.Font;t.fontSize=size;t.text=text;t.color=color??CatTheme.Ink;t.alignment=TextAnchor.MiddleLeft;t.raycastTarget=false;return t;
        }
        static void Button(Transform p,string name,string text,float x,float y,float w,bool primary=false)
        {
            var b=Picture(p,name,"journal/surfaces/button-"+(primary?"primary":"quiet")+"-default",x,y,w,58);
            b.gameObject.AddComponent<Button>();b.raycastTarget=true;
            var t=Label(b.transform,"Label",text,12,0,w-24,50,22,primary?Color.white:CatTheme.Ink);t.alignment=TextAnchor.MiddleCenter;
        }
        static void Photo(Transform root,string art,string caption)
        {
            var f=Picture(root,"Polaroid","journal/surfaces/polaroid-frame",1014,170,302,217);
            Picture(f.transform,"Photo",art,12,12,278,140).preserveAspect=true;
            Label(f.transform,"Caption",caption,12,157,278,45,24).alignment=TextAnchor.MiddleCenter;
            Picture(root,"Tape","journal/decor/tape-blue-stripe",1118,156,100,26);
        }

        static void SaveComponent(string name,float width,float height,Action<Transform> build)
        {
            var root=new GameObject(name,typeof(RectTransform));
            try
            {
                ((RectTransform)root.transform).sizeDelta=new Vector2(width,height);
                ((RectTransform)root.transform).pivot=Vector2.up;
                build(root.transform);
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Desktop/JournalSamples/Components/"+name+".prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }

        static void BuildComponents()
        {
            Directory.CreateDirectory("Assets/Desktop/JournalSamples/Components");
            Directory.CreateDirectory(Root+"materials");
            var materialPath=Root+"materials/PaperMultiply.mat";
            var paperMaterial=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(paperMaterial==null)
            {
                var shader=Shader.Find("FishSocial/UI/Journal Paper Multiply");
                if(shader==null)throw new Exception("Missing journal multiply shader");
                paperMaterial=new Material(shader);AssetDatabase.CreateAsset(paperMaterial,materialPath);
            }
            SaveComponent("JournalPage",1370,650,p=>{
                Picture(p,"Shadow","journal/surfaces/paper-shadow",0,0,1370,650);
                Picture(p,"Edge","journal/surfaces/paper-edge",0,0,1370,650);
                var paper=Picture(p,"Surface","journal/surfaces/paper-page",0,0,1370,650);
                paper.gameObject.AddComponent<Mask>().showMaskGraphic=true;
                var grain=Picture(paper.transform,"Grain","journal/textures/paper-grain",0,0,1370,650,new Color(1,1,1,.32f));grain.type=Image.Type.Tiled;grain.material=paperMaterial;
                Picture(paper.transform,"Dots","journal/textures/paper-dots",0,0,1370,650).type=Image.Type.Tiled;
                for(int i=0;i<3;i++)Picture(p,"Ring"+i,"journal/decor/binder-ring",-24,150+160*i,54,24);
            });
            SaveComponent("JournalStickerCard",214,190,p=>{
                Picture(p,"Shadow","journal/surfaces/sticker-shadow",0,0,214,190);
                var visual=Picture(p,"Visual","journal/surfaces/sticker-card",0,0,214,190);
                Picture(visual.transform,"Artwork","journal/decor/fish-placeholder",30,25,154,95).preserveAspect=true;
                Label(visual.transform,"Name","物品名称",20,131,174,35,20).alignment=TextAnchor.MiddleCenter;
                var tape=Picture(p,"SelectedTape","journal/decor/tape-pink-stripe",-14,-8,104,26);tape.transform.localRotation=Quaternion.Euler(0,0,28);tape.gameObject.SetActive(false);
                var hit=p.gameObject.AddComponent<Image>();hit.color=Color.clear;hit.raycastTarget=true;p.gameObject.AddComponent<Button>().targetGraphic=hit;
            });
            SaveComponent("JournalPolaroid",302,228,p=>{
                Picture(p,"Shadow","journal/surfaces/polaroid-shadow",0,0,302,228);
                Picture(p,"Frame","journal/surfaces/polaroid-frame",0,0,302,228);
                Picture(p,"Photo","journal/decor/missing-image",12,12,278,150).preserveAspect=true;
                Label(p,"Caption","照片标题",12,170,278,42,24).alignment=TextAnchor.MiddleCenter;
                Picture(p,"Tape","journal/decor/tape-blue-stripe",104,-14,100,26);
            });
            SaveComponent("JournalPondDetail",362,496,p=>{
                Picture(p,"Paper","journal/surfaces/paper-aside",0,0,362,496);
                Label(p,"Heading","鱼塘详情",24,12,314,36,25);
                Picture(p,"Frame","journal/surfaces/polaroid-frame",30,58,302,206);
                Picture(p,"Photo","journal/decor/missing-image",42,70,278,140).preserveAspect=true;
                Label(p,"PondName","鱼塘名称",42,216,278,36,24).alignment=TextAnchor.MiddleCenter;
                Picture(p,"Tape","journal/decor/tape-blue-stripe",134,44,100,26);
                Label(p,"Facts","水域 · 分类\n鱼种与准入说明",24,278,314,72,16);
                Picture(p,"ModeTrack","journal/surfaces/toggle-track",18,366,326,48,new Color(.95f,.9f,.81f));
                Button(p,"ModeSell","回鱼出售",18,366,161);Button(p,"ModeAuto","自动回鱼",183,366,161);
                Button(p,"Enter","进入鱼塘",18,428,326,true);
            });
            SaveComponent("JournalPawToggle",74,38,p=>{
                var track=Picture(p,"Track","journal/surfaces/toggle-track",0,0,74,38,new Color(.42f,.72f,.42f));
                Picture(p,"Knob","journal/decor/toggle-knob",40,4,30,30);
                var paw=Picture(p,"Paw","journal/decor/paw-push",-30,2,34,34,new Color(1,.95f,.87f));paw.gameObject.SetActive(false);
                // Visual asset assembly. Bind value/state and knob animation in the UI migration phase.
                track.raycastTarget=true;
            });
            SaveComponent("JournalToast",440,100,p=>{
                Picture(p,"Note","journal/surfaces/toast-note",0,5,340,80);
                Label(p,"Message","提示内容",20,18,300,54,20);
                Picture(p,"FishTail","journal/motion/toast-fish-tail",330,15,96,56);
                Picture(p,"FishBody","journal/motion/toast-fish-body",330,15,96,56);
            });
            SaveComponent("JournalEmptyState",260,220,p=>{
                Picture(p,"BasketBack","journal/decor/empty-basket-back",10,90,240,116);
                Picture(p,"Character","journal/characters/orange/sleep",30,10,200,171);
                Picture(p,"BasketFront","journal/decor/empty-basket-front",10,90,240,116);
                Label(p,"SleepText","Z z z",190,8,70,40,24);
            });
            SaveComponent("JournalBobber",40,58,p=>Picture(p,"Sprite","journal/motion/bobber",0,0,40,58));
            SaveComponent("JournalCornerCharacter",192,164,p=>Picture(p,"Sprite","characters/poses/orange-ready",0,0,192,154));
            SaveComponent("JournalLoading",240,150,p=>{
                Picture(p,"Ripple","journal/motion/ripple-ring",72,30,96,96,new Color(.36f,.68f,.89f));
                Picture(p,"Bobber","journal/motion/bobber",100,20,40,58);
                Label(p,"Hint","鱼还没上钩，再等等…",0,110,240,30,16).alignment=TextAnchor.MiddleCenter;
            });
            SaveComponent("JournalStamp",108,108,p=>{
                Picture(p,"Ring","journal/decor/stamp-ring",0,0,108,108,new Color(.8f,.25f,.18f));
                Label(p,"Text","已收录",10,26,88,56,22,new Color(.8f,.25f,.18f)).alignment=TextAnchor.MiddleCenter;
            });
            SaveComponent("JournalMapMarker",100,96,p=>{
                Picture(p,"BobberBase","journal/motion/bobber-base",30,0,40,58);
                Picture(p,"BobberColor","journal/motion/bobber-color",30,0,40,58,new Color(.42f,.72f,.42f));
                Picture(p,"Highlight","journal/motion/bobber-highlight",30,0,40,58);
                Picture(p,"LabelPaper","journal/surfaces/category-chip",0,62,100,30,new Color(1,.97f,.91f));
                Label(p,"Name","鱼塘名称",4,62,92,30,16).alignment=TextAnchor.MiddleCenter;
            });
        }

        [MenuItem("Fish Social/校验/日记美术资源")]
        public static void ValidatePreparedAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            var shader=Shader.Find("FishSocial/UI/Journal Paper Multiply");
            if(shader==null||ShaderUtil.ShaderHasError(shader))throw new Exception("Paper multiply shader failed compilation");
            var material=AssetDatabase.LoadAssetAtPath<Material>(Root+"materials/PaperMultiply.mat");
            if(material==null||!material.HasProperty("_ColorMask"))throw new Exception("Paper multiply material missing UI mask support");
            var layouts=JObject.Parse(File.ReadAllText("Assets/Desktop/JournalSamples/ReferenceAssets/layouts.json"));
            foreach(var page in layouts["pages"])
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Desktop/JournalSamples/JournalSample-"+(string)page["id"]+".prefab");
                if(prefab==null||prefab.GetComponentsInChildren<Text>(true).Any(t=>t.font==null))throw new Exception("Missing sample prefab or font");
            }
            AssetDatabase.SaveAssets();
            File.WriteAllText(Report+"prepared-assets-unity-result.txt","PASS: imported sprites, borders, character and pond manifests, UI multiply shader/material, sample prefab reload and native fonts.\n");
        }

        // Batch/authoring API retained; historical operation is not a daily menu command.
        public static void PrepareReference()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ConfigureImports();
            BuildComponents();
            RenderSamples();
            WriteThemeManifest();
            AssetDatabase.SaveAssets();
            File.WriteAllText(Report+"reference-unity-result.txt", "PASS: Reference samples rendered with separate visual layers and editable Unity Text. Business events are not connected.\n");
        }

        // Read geometry and paint exported from the approved interactive prototype.
        // Text remains native and editable; no full-screen screenshot is used as a UI.
        static void RenderSamples()
        {
            const string folder="Assets/Desktop/JournalSamples/ReferenceAssets/";
            if(!File.Exists(folder+"layouts.json"))throw new Exception("Run scripts/art/export-cozy-journal-reference.cjs first.");
            foreach(var file in Directory.GetFiles(folder,"*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(file);
                importer.textureType=TextureImporterType.Sprite;
                importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true;importer.mipmapEnabled=false;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=4096;
                importer.spritePixelsPerUnit=200;importer.spriteBorder=Vector4.zero;
                var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);
                settings.spriteMeshType=SpriteMeshType.FullRect;importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
            var layouts=JObject.Parse(File.ReadAllText(folder+"layouts.json"));
            foreach(var layout in layouts["pages"])
            {
                var kind=(string)layout["id"];
                var scene=EditorSceneManager.NewPreviewScene();var target=new RenderTexture(3344,1882,24);var previous=RenderTexture.active;
                try
                {
                    var camera=CreateCamera(scene,target,Color.clear);
                    var go=new GameObject("JournalSample-"+kind,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));SceneManager.MoveGameObjectToScene(go,scene);
                    var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=10;canvas.referencePixelsPerUnit=100;
                    go.GetComponent<CanvasScaler>().scaleFactor=2;canvas.scaleFactor=2;
                    foreach(var node in layout["nodes"])
                    {
                        var group=new GameObject((string)node["id"]+" "+(string)node["name"],typeof(RectTransform));group.transform.SetParent(go.transform,false);
                        var rt=(RectTransform)group.transform;rt.anchorMin=rt.anchorMax=rt.pivot=Vector2.up;rt.anchoredPosition=Vector2.zero;rt.sizeDelta=new Vector2(1672,941);
                        if(node["asset"]!=null)
                        {
                            var rect=node["paintRect"];
                            var paint=new GameObject("CSS visual",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));paint.transform.SetParent(group.transform,false);
                            var pr=(RectTransform)paint.transform;pr.anchorMin=pr.anchorMax=pr.pivot=Vector2.up;pr.anchoredPosition=new Vector2((float)rect["x"],-(float)rect["y"]);pr.sizeDelta=new Vector2((float)rect["width"],(float)rect["height"]);
                            var image=paint.GetComponent<Image>();image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>((string)node["asset"]);image.raycastTarget=false;
                            if(image.sprite==null)throw new Exception("Missing reference sprite: "+node["asset"]);
                        }
                        foreach(var line in node["texts"])
                        {
                            var color=CssColor((string)line["color"]);color.a*=(float)line["opacity"];
                            float size=(float)line["size"],width=(float)line["width"],height=(float)line["height"];
                            var parent=group.transform;float offsetX=0,offsetY=0;
                            var clip=line["clip"];
                            if(clip!=null&&((float)clip["x"]>0||(float)clip["y"]>0||(float)clip["right"]<1672||(float)clip["bottom"]<941))
                            {
                                var mask=new GameObject("Text viewport",typeof(RectTransform),typeof(RectMask2D));mask.transform.SetParent(parent,false);parent=mask.transform;
                                offsetX=(float)clip["x"];offsetY=(float)clip["y"];
                                var mr=(RectTransform)parent;mr.anchorMin=mr.anchorMax=mr.pivot=Vector2.up;mr.anchoredPosition=new Vector2(offsetX,-offsetY);mr.sizeDelta=new Vector2((float)clip["right"]-offsetX,(float)clip["bottom"]-offsetY);
                            }
                            var text=Label(parent,"Text",(string)line["text"],(float)line["x"]-offsetX,(float)line["y"]-offsetY,width+8,height,Mathf.RoundToInt(size),color);
                            text.font=Resources.Load<Font>("Desktop/Theme/Cozy/fonts/FishCozyRounded-Medium");
                            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;text.alignment=TextAnchor.MiddleLeft;
                            text.supportRichText=false;
                            int weight;if(int.TryParse((string)line["weight"],out weight)&&weight>=600)text.fontStyle=FontStyle.Bold;
                            // Glyph ranges already include their transformed position. Keep rotation about the text center.
                            float angle=(float)line["angle"];
                            if(Mathf.Abs(angle)>.01f)
                            {
                                var tr=(RectTransform)text.transform;tr.pivot=new Vector2(.5f,.5f);
                                tr.anchoredPosition+=new Vector2((width+8)/2,-height/2);tr.localRotation=Quaternion.Euler(0,0,-angle);
                            }
                        }
                        if((bool)node["interactive"])
                        {
                            var rect=node["rect"];
                            var hit=new GameObject("Interaction target — bind game action",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));hit.transform.SetParent(group.transform,false);
                            var hr=(RectTransform)hit.transform;hr.anchorMin=hr.anchorMax=hr.pivot=Vector2.up;hr.anchoredPosition=new Vector2((float)rect["x"],-(float)rect["y"]);hr.sizeDelta=new Vector2((float)rect["width"],(float)rect["height"]);
                            var image=hit.GetComponent<Image>();image.color=Color.clear;hit.GetComponent<Button>().targetGraphic=image;
                        }
                    }
                    Canvas.ForceUpdateCanvases();camera.Render();SavePixels(target,Report+kind+"-unity-2x.png");
                    canvas.worldCamera=null;canvas.renderMode=RenderMode.ScreenSpaceOverlay;
                    var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1672,941);scaler.matchWidthOrHeight=.5f;
                    PrefabUtility.SaveAsPrefabAsset(go,"Assets/Desktop/JournalSamples/JournalSample-"+kind+".prefab");
                }
                finally{RenderTexture.active=previous;target.Release();Object.DestroyImmediate(target);EditorSceneManager.ClosePreviewScene(scene);}
            }
        }

        static Color CssColor(string value)
        {
            var match=System.Text.RegularExpressions.Regex.Match(value,@"rgba?\(([^)]+)\)");
            if(!match.Success)return CatTheme.Ink;
            var channels=match.Groups[1].Value.Split(',').Select(x=>float.Parse(x.Trim(),System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new Color(channels[0]/255f,channels[1]/255f,channels[2]/255f,channels.Length>3?channels[3]:1);
        }

    }
    public sealed class CozyJournalSpriteImporter : AssetPostprocessor
    {
        void OnPreprocessTexture(){CozyJournalResourcePreparation.Configure((TextureImporter)assetImporter);}
        public override uint GetVersion(){return 1;}
    }
}
#endif
