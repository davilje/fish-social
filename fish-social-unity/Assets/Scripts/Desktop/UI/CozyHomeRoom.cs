using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FishSocial.Desktop.Pet;

namespace FishSocial.Desktop
{
    /// <summary>The approved layered room, in the journal's 1672 x 941 design coordinates.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyHomeRoom : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const string ResourceRoot = "Desktop/Theme/Cozy/home-room/";
        [Serializable] public sealed class Placement
        {
            public string id, asset, fit;
            public float x, y, width, height, rotation;
            public int layer;
        }
        [Serializable] sealed class Manifest { public Placement[] placements; }
        [SerializeField] RectTransform actor;
        [SerializeField] RawImage artwork;
        [SerializeField] Button equipment, aquarium;
        [SerializeField] Text bubble;
        Vector2 feet = new Vector2(835, 742), target = new Vector2(835, 742), dragOffset;
        float inactive, wander, feedback;
        bool dragging, focused = true, pageOpen, sitting;
        int pointer = int.MinValue;
        string skin;
        Sprite mappedRest, mappedSit;
        Texture2D standing, seated;
        public Vector2 Feet => feet;
        public bool IsSitting => sitting;
        public bool PageOpen => pageOpen;
        public static Vector2 ClampFeet(Vector2 value) => new Vector2(Mathf.Clamp(value.x, 580, 1070), Mathf.Clamp(value.y, 690, 775));

        public static CozyHomeRoom Build(Transform main)
        {
            var outdoor = main.Find("Background");
            if (outdoor != null) outdoor.gameObject.SetActive(false);
            var existing = main.Find("HomeRoom")?.GetComponent<CozyHomeRoom>();
            if (existing != null)
            {
                EnsureQuietButton(existing.equipment);
                EnsureQuietButton(existing.aquarium);
                existing.RefreshSkin();
                existing.Present();
                return existing;
            }
            var root = Rect(main, "HomeRoom", 0, 0, 1672, 941);
            root.SetAsFirstSibling();
            var room = root.gameObject.AddComponent<CozyHomeRoom>();
            Picture(root, "RoomBase", "room-base", 0, 0, 1672, 941).preserveAspect = false;
            var props = Rect(root, "Props", 0, 0, 1672, 941);
            var manifest = Resources.Load<TextAsset>(ResourceRoot + "manifest");
            if (manifest == null) throw new InvalidOperationException("Missing home-room manifest");
            var placements = JsonUtility.FromJson<Manifest>(manifest.text).placements;
            // Stable layer ordering, including three independently replaceable rod instances.
            for (var layer = 0; layer <= 21; layer++)
                foreach (var p in placements)
                {
                    if (p.layer != layer) continue;
                    var sprite = Resources.Load<Sprite>(ResourceRoot + p.asset);
                    if (sprite == null) throw new InvalidOperationException("Missing room prop: " + p.asset);
                    var w = p.width; var h = p.height;
                    if (p.fit != "fill") { var scale = Mathf.Min(w / sprite.rect.width, h / sprite.rect.height); w = sprite.rect.width * scale; h = sprite.rect.height * scale; }
                    var image = Picture(props, p.id, p.asset, p.x + (p.width-w)/2, p.y+p.height-h, w, h);
                    image.preserveAspect = false;
                }
            var floor = Rect(root, "Floor", 470, 685, 820, 110);
            floor.gameObject.AddComponent<Image>().color = Color.clear;
            // Events bubble from the floor and actor to this owner.
            room.actor = Rect(root, "Companion", 749, 496, 172, 246);
            room.artwork = room.actor.gameObject.AddComponent<RawImage>();
            room.actor.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = true;
            room.equipment = ActionButton(root, "Equipment", "我的钓具", 500, 683);
            room.aquarium = ActionButton(root, "Aquarium", "鱼缸 · 展示柜", 1200, 683);
            var note = Rect(root, "Feedback", 715, 436, 240, 42);
            var paper = note.gameObject.AddComponent<Image>();
            CozyJournalTheme.ApplySurface(paper, "surfaces/toast-note");paper.raycastTarget = false;
            room.bubble = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            room.bubble.transform.SetParent(note, false);DesktopModalUi.Stretch(room.bubble.gameObject);
            room.bubble.font = CatTheme.Font;room.bubble.fontSize = 19;room.bubble.color = CatTheme.Ink;room.bubble.alignment = TextAnchor.MiddleCenter;room.bubble.raycastTarget = false;
            note.gameObject.SetActive(false);
            room.RefreshSkin();room.Present();
            return room;
        }
        static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = r.pivot = Vector2.up;r.anchoredPosition = new Vector2(x, -y);r.sizeDelta = new Vector2(w,h);return r;
        }
        static Image Picture(Transform parent, string name, string asset, float x, float y, float w, float h)
        {
            var image = Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();
            image.sprite = Resources.Load<Sprite>(ResourceRoot+asset);image.color = Color.white;image.raycastTarget = false;return image;
        }
        static Button ActionButton(Transform parent, string name, string label, float x, float y)
        {
            var button = EnsureQuietButton(Rect(parent, name, x, y, 170, 46).gameObject.AddComponent<Button>());
            var text = Rect(button.transform, "Label", 0, 0, 170, 46).gameObject.AddComponent<Text>();
            text.font = CatTheme.Font; text.fontSize = 20; text.color = CatTheme.Ink;
            text.alignment = TextAnchor.MiddleCenter; text.text = label; text.raycastTarget = false;
            return button;
        }

        /// <summary>Home-room actions must persist the quiet journal plate. A bare Button fails the player build guard.</summary>
        static Button EnsureQuietButton(Button button)
        {
            if (button == null) return null;
            if (button.GetComponent<CozyButtonVisual>() != null && button.transform.Find("CozyVisual")?.GetComponent<Image>() != null)
                return button;
            var image = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
            image.sprite = null; image.color = new Color(1f, 1f, 1f, 0f); image.raycastTarget = true;
            button.targetGraphic = image; button.transition = Selectable.Transition.None;
            if (button.transform.Find("CozyVisual") == null)
            {
                var node = new GameObject("CozyVisual", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                node.transform.SetParent(button.transform, false); node.transform.SetAsFirstSibling();
                var plate = node.GetComponent<Image>(); plate.raycastTarget = false;
                node.GetComponent<LayoutElement>().ignoreLayout = true;
                var rect = plate.rectTransform;
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            var visual = button.GetComponent<CozyButtonVisual>() ?? button.gameObject.AddComponent<CozyButtonVisual>();
            visual.ConfigureSurface("surfaces/button-quiet-default", "surfaces/button-quiet-hover",
                "surfaces/button-quiet-pressed", "surfaces/button-quiet-hover", "surfaces/button-quiet-disabled");
            CatTheme.StyleButton(button);
            return button;
        }
        public void Bind(Action openEquipment, Action openAquarium)
        {
            equipment.onClick.RemoveAllListeners();aquarium.onClick.RemoveAllListeners();
            equipment.onClick.AddListener(()=>openEquipment?.Invoke());aquarium.onClick.AddListener(()=>openAquarium?.Invoke());
        }
        public void SetPageOpen(bool value)
        {
            pageOpen=value;dragging=false;pointer=int.MinValue;target=feet;inactive=wander=feedback=0;
            actor.gameObject.SetActive(!value);equipment.gameObject.SetActive(!value);aquarium.gameObject.SetActive(!value);
            transform.Find("Floor").gameObject.SetActive(!value);bubble.transform.parent.gameObject.SetActive(false);
            Present();
        }
        void RefreshSkin()
        {
            var id=PetArtLoader.CurrentOwnPetId();if(id==skin)return;skin=id;
            standing=Resources.Load<Texture2D>(ResourceRoot+"cat-standing");seated=Resources.Load<Texture2D>(ResourceRoot+"cat-sitting");
            mappedRest=id=="orange"?null:Resources.Load<Sprite>(CozyJournalTheme.ResourceRoot+"characters/"+id+"/rest");
            mappedSit=id=="orange"?null:Resources.Load<Sprite>(CozyJournalTheme.ResourceRoot+"characters/"+id+"/sleep");
        }
        public void MoveTo(Vector2 position, bool immediate=false)
        {
            if(pageOpen)return;target=ClampFeet(position);inactive=wander=0;sitting=false;
            if(immediate||CozyMotion.Reduced)feet=target;Present();
        }
        public void Sit() { target=feet;sitting=true;inactive=wander=0;Present(); }
        public void Respond()
        {
            DesktopAudioService.PlayMeow();
            inactive=wander=0;sitting=false;feedback=1.6f;bubble.text="喵～一起待一会儿";
            bubble.transform.parent.gameObject.SetActive(true);Present();
        }
        public void Advance(float delta)
        {
            if(delta<=0||!isActiveAndEnabled||pageOpen||!focused||CozyMotion.Paused)return;
            RefreshSkin();inactive+=delta;wander+=delta;feedback=Mathf.Max(0,feedback-delta);
            bubble.transform.parent.gameObject.SetActive(feedback>0);
            if(!dragging)
            {
                feet=CozyMotion.Reduced?target:Vector2.MoveTowards(feet,target,180*delta);
                if(inactive>=30) {if(!sitting)Sit();inactive=30;}
                else if(wander>=12&&!CozyMotion.Reduced&&!sitting) {target=ClampFeet(new Vector2(UnityEngine.Random.Range(620,1030),UnityEngine.Random.Range(710,765)));wander=0;}
            }
            Present();
        }
        void Present()
        {
            if(actor==null)return;
            var mapped=sitting?mappedSit:mappedRest;
            var size=sitting?new Vector2(176,204):new Vector2(172,246);
            if(mapped!=null)
            {
                artwork.texture=mapped.texture;var r=mapped.textureRect;
                artwork.uvRect=new Rect(r.x/mapped.texture.width,r.y/mapped.texture.height,r.width/mapped.texture.width,r.height/mapped.texture.height);
                size=new Vector2(228,228*r.height/r.width);
            }
            else
            {
                artwork.texture=sitting?seated:standing;
                artwork.uvRect=sitting?new Rect(181/640f,61/512f,223/640f,259/512f):new Rect(207/640f,61/512f,217/640f,310/512f);
            }
            actor.sizeDelta=size;actor.anchoredPosition=new Vector2(feet.x-size.x/2,-feet.y+size.y);
            actor.localEulerAngles=new Vector3(0,0,feedback>0&&!CozyMotion.Reduced?Mathf.Sin(feedback*16)*3:0);
            ((RectTransform)bubble.transform.parent).anchoredPosition=new Vector2(feet.x-120,-feet.y+size.y+60);
        }
        bool CanInteract => !pageOpen&&focused&&!CozyMotion.Paused;
        Vector2 Position(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out var p);return new Vector2(p.x,-p.y);
        }
        bool ActorHit(PointerEventData e) => RectTransformUtility.RectangleContainsScreenPoint(actor,e.pressPosition,e.pressEventCamera);
        public void OnPointerClick(PointerEventData e)
        { if(!CanInteract||e.button!=PointerEventData.InputButton.Left||e.dragging)return;if(ActorHit(e))Respond();else MoveTo(Position(e)); }
        public void OnBeginDrag(PointerEventData e)
        {if(!CanInteract||e.button!=PointerEventData.InputButton.Left||!ActorHit(e))return;dragging=true;pointer=e.pointerId;dragOffset=feet-Position(e);inactive=wander=0;sitting=false;}
        public void OnDrag(PointerEventData e)
        {if(!CanInteract||!dragging||pointer!=e.pointerId)return;feet=target=ClampFeet(Position(e)+dragOffset);Present();}
        public void OnEndDrag(PointerEventData e) {if(pointer!=e.pointerId)return;dragging=false;pointer=int.MinValue;target=feet;}
        void Update()
        {
            if (CanInteract && !InputFieldFocused())
            {
                var d=new Vector2(Input.GetKey(KeyCode.RightArrow)?1:Input.GetKey(KeyCode.LeftArrow)?-1:0,Input.GetKey(KeyCode.DownArrow)?1:Input.GetKey(KeyCode.UpArrow)?-1:0);
                if(d!=Vector2.zero)MoveTo(feet+d.normalized*180*Time.unscaledDeltaTime,true);
                if(Input.GetKeyDown(KeyCode.Home))MoveTo(new Vector2(835,742));if(Input.GetKeyDown(KeyCode.R))Sit();
            }
            Advance(Time.unscaledDeltaTime);
        }
        static bool InputFieldFocused()
        {
            var events = EventSystem.current;
            if (events == null) return false;
            var selected = events.currentSelectedGameObject;
            return selected != null && selected.GetComponent<InputField>() != null;
        }
        void OnApplicationFocus(bool value){focused=value;if(!value){dragging=false;pointer=int.MinValue;target=feet;}}
        void OnEnable(){if(actor!=null){RefreshSkin();Present();}}
        void OnDisable(){dragging=false;pointer=int.MinValue;target=feet;}
    }
}
