using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FishSocial.Desktop.Pet;

namespace FishSocial.Desktop
{
    /// <summary>The approved layered room, in the journal's 1672 x 941 design coordinates.</summary>
    [DisallowMultipleComponent]
    public sealed partial class CozyHomeRoom : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const string ResourceRoot = "Desktop/Theme/Cozy/home-room/";
        [Serializable] public sealed class Placement
        {
            public string id, asset, fit;
            public float x, y, width, height, rotation;
            public int layer;
        }
        [Serializable] sealed class Manifest { public Placement[] placements; }
        [Serializable] sealed class SavedFurniture { public string id; public Vector2 bottomCenter; }
        [Serializable] sealed class SavedLayout { public int version = 1; public SavedFurniture[] items; }
        public enum Activity { Free, GoingToBed, Sleeping, Rolling, Pouring, GoingToFood, Eating }
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
        Sprite sleepingPose;
        [SerializeField] RawImage bedFront;
        CozyRoomProp[] propTargets;
        bool furnitureDragging;
        string layoutOwner;
        Activity activity;
        public Vector2 Feet => feet;
        public bool IsSitting => sitting;
        public Activity CurrentActivity => activity;
        public bool IsSleeping => activity == Activity.Sleeping;
        public bool PageOpen => pageOpen;
        public bool CanInteract => isActiveAndEnabled && !pageOpen && focused && !CozyMotion.Paused;
        public static Vector2 ClampFeet(Vector2 value) => new Vector2(Mathf.Clamp(value.x, 580, 1070), Mathf.Clamp(value.y, 690, 775));
        public static Rect FurnitureBounds(string id)
        {
            // Bottom-center coordinates keep each item's entire footprint clear of the dock.
            switch (id)
            {
                case "cat-bed": return UnityEngine.Rect.MinMaxRect(170, 675, 370, 785);
                case "cat-climber": return UnityEngine.Rect.MinMaxRect(180, 600, 350, 665);
                case "food-bowls": return UnityEngine.Rect.MinMaxRect(1430, 660, 1580, 785);
                default: return new Rect(0, 0, 0, 0);
            }
        }
        public static Vector2 ClampFurniture(string id, Vector2 value)
        {
            var b = FurnitureBounds(id);
            return new Vector2(Mathf.Clamp(value.x, b.xMin, b.xMax), Mathf.Clamp(value.y, b.yMin, b.yMax));
        }

        public static CozyHomeRoom Build(Transform main)
        {
            var outdoor = main.Find("Background");
            if (outdoor != null) outdoor.gameObject.SetActive(false);
            var existing = main.Find("HomeRoom")?.GetComponent<CozyHomeRoom>();
            if (existing != null)
            {
                EnsureQuietButton(existing.equipment);
                EnsureQuietButton(existing.aquarium);
                existing.EnsurePropInteractions();
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
            room.equipment = ActionButton(root, "Equipment", DesktopLocalization.Text("ui.home.room.button.equipment", "我的钓具"), 500, 683);
            room.aquarium = ActionButton(root, "Aquarium", DesktopLocalization.Text("ui.home.room.button.showcase", "鱼缸 · 展示柜"), 1200, 683);
            var note = Rect(root, "Feedback", 715, 436, 240, 42);
            var paper = note.gameObject.AddComponent<Image>();
            CozyJournalTheme.ApplySurface(paper, "surfaces/toast-note");paper.raycastTarget = false;
            room.bubble = new GameObject("Label", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            room.bubble.transform.SetParent(note, false);DesktopModalUi.Stretch(room.bubble.gameObject);
            room.bubble.font = CatTheme.Font;room.bubble.fontSize = 19;room.bubble.color = CatTheme.Ink;room.bubble.alignment = TextAnchor.MiddleCenter;room.bubble.raycastTarget = false;
            note.gameObject.SetActive(false);
            room.RefreshSkin();room.Present();
            room.EnsurePropInteractions();
            return room;
        }
        void EnsurePropInteractions()
        {
            var props = transform.Find("Props");
            foreach (var id in new[] { "rod-1", "rod-2", "rod-3", "cat-bed", "cat-climber", "food-bowls" })
            {
                var node = props.Find(id);
                if (node == null) continue;
                var hit = node.GetComponent<CozyRoomProp>();
                if (hit == null) hit = node.gameObject.AddComponent<CozyRoomProp>();
                hit.Configure(this, id);
            }
            propTargets = props.GetComponentsInChildren<CozyRoomProp>(true);
            var group = props.GetComponent<CanvasGroup>();
            if (group == null) group = props.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = !pageOpen;
            transform.Find("Floor").SetSiblingIndex(1);
            if (bedFront == null)
            {
                var rect = Rect(transform, "BedFront", 0, 0, 1, 1);
                rect.SetSiblingIndex(actor.GetSiblingIndex() + 1);
                bedFront = rect.gameObject.AddComponent<RawImage>();
                bedFront.raycastTarget = false;
            }
            bedFront.texture = Resources.Load<Texture2D>(ResourceRoot + "cat-bed");
            bedFront.gameObject.SetActive(IsSleeping && !pageOpen);
            EnsureAnimationLayers();
        }
        public CozyRoomProp Prop(string id)
        { return transform.Find("Props/" + id)?.GetComponent<CozyRoomProp>(); }
        public void ActivateProp(string id)
        {
            if (!CanInteract) return;
            if (id == "rod-1" || id == "rod-2" || id == "rod-3") equipment.onClick.Invoke();
            else if (id == "cat-bed") SleepInBed();
            else if (id == "food-bowls") Feed();
        }
        Vector2 BedDestination()
        {
            var bed = (RectTransform)Prop("cat-bed").transform;
            return Prop("cat-bed").BottomCenter - new Vector2(0, bed.rect.height * .29f);
        }
        public void SleepInBed()
        {
            if (!CanInteract || sleepingPose == null) return;
            CancelAnimation();
            dragging = false; pointer = int.MinValue; sitting = false; inactive = wander = feedback = 0;
            activity = Activity.GoingToBed; target = BedDestination();
            bubble.transform.parent.gameObject.SetActive(false);
            if (CozyMotion.Reduced) { feet = target; activity = Activity.Sleeping; }
            Present();
        }
        public void BeginFurnitureDrag() { CancelFeeding(); furnitureDragging = true; dragging = false; pointer = int.MinValue; inactive = wander = 0; }
        public void EndFurnitureDrag() { furnitureDragging = false; inactive = wander = 0; }
        public void FurnitureMoved(string id)
        {
            if (id == "food-bowls") PresentFood();
            if (id == "cat-bed" && (activity == Activity.GoingToBed || IsSleeping))
            {
                target = BedDestination();
                if (IsSleeping) feet = target;
                Present();
            }
        }
        public void SetLayoutOwner(string owner)
        {
            if (layoutOwner == owner) return;
            CancelAnimation();
            CancelPropDrags(); layoutOwner = owner; activity = Activity.Free; feet = target = new Vector2(835, 742);
            var data = Resources.Load<TextAsset>(ResourceRoot + "manifest");
            foreach (var p in JsonUtility.FromJson<Manifest>(data.text).placements)
            {
                var prop = Prop(p.id); if (prop == null || !prop.CanDrag) continue;
                prop.SetBottomCenter(ClampFurniture(p.id, new Vector2(p.x + p.width / 2, p.y + p.height)));
            }
            if (!string.IsNullOrEmpty(owner))
            {
                try
                {
                    var saved = JsonUtility.FromJson<SavedLayout>(PlayerPrefs.GetString(LayoutKey(owner), "{}"));
                    if (saved?.version == 1 && saved.items != null)
                        foreach (var item in saved.items)
                        {
                            var prop = Prop(item.id);
                            if (prop != null && prop.CanDrag && Finite(item.bottomCenter)) prop.SetBottomCenter(ClampFurniture(item.id, item.bottomCenter));
                        }
                }
                catch (ArgumentException) { /* Damaged local layout falls back to the authored arrangement. */ }
            }
            Present();
        }
        static bool Finite(Vector2 value) => !float.IsNaN(value.x) && !float.IsNaN(value.y) && !float.IsInfinity(value.x) && !float.IsInfinity(value.y);
        public static string LayoutKey(string owner) => "FishSocial.HomeRoom.Layout.v1." + Uri.EscapeDataString(owner ?? "");
        public void SaveLayout()
        {
            if (string.IsNullOrEmpty(layoutOwner)) return;
            var items = new System.Collections.Generic.List<SavedFurniture>();
            foreach (var prop in propTargets)
                if (prop.CanDrag) items.Add(new SavedFurniture { id = prop.PropId, bottomCenter = prop.BottomCenter });
            PlayerPrefs.SetString(LayoutKey(layoutOwner), JsonUtility.ToJson(new SavedLayout { items = items.ToArray() }));
            PlayerPrefs.Save();
        }
        void CancelPropDrags()
        { if (propTargets != null) foreach (var prop in propTargets) prop.CancelDrag(); furnitureDragging = false; }
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
            if (value) CancelAnimation();
            CancelPropDrags();
            pageOpen=value;dragging=false;pointer=int.MinValue;target=feet;inactive=wander=feedback=0;
            actor.gameObject.SetActive(!value);equipment.gameObject.SetActive(!value);aquarium.gameObject.SetActive(!value);
            transform.Find("Floor").gameObject.SetActive(!value);bubble.transform.parent.gameObject.SetActive(false);
            var group = transform.Find("Props").GetComponent<CanvasGroup>();
            if (group != null) group.blocksRaycasts = !value;
            if (activity == Activity.GoingToBed) target = BedDestination();
            Present();
        }
        void RefreshSkin()
        {
            var id=PetArtLoader.CurrentOwnPetId();if(id==skin)return;CancelAnimation();skin=id;
            sleepingPose = Resources.Load<Sprite>(ResourceRoot + "poses/" + id + "-sleep");
            standing=Resources.Load<Texture2D>(ResourceRoot+"cat-standing");seated=Resources.Load<Texture2D>(ResourceRoot+"cat-sitting");
            mappedRest=id=="orange"?null:Resources.Load<Sprite>(CozyJournalTheme.ResourceRoot+"characters/"+id+"/rest");
            mappedSit=id=="orange"?null:Resources.Load<Sprite>(CozyJournalTheme.ResourceRoot+"characters/"+id+"/sleep");
        }
        public void MoveTo(Vector2 position, bool immediate=false)
        {
            if(pageOpen)return;
            if (activity != Activity.Free || sitting || immediate) CancelAnimation();
            activity=Activity.Free;target=ClampFeet(position);inactive=wander=0;sitting=false;
            if(immediate||CozyMotion.Reduced)feet=target;Present();
        }
        public void Sit() { CancelAnimation(); activity=Activity.Free;target=feet;sitting=true;inactive=wander=0;Present(); }
        public void Respond()
        {
            if (!CanInteract) return;
            CancelAnimation();
            DesktopAudioService.PlayMeow();
            if (activity != Activity.Free) { activity=Activity.Free; target=ClampFeet(feet); }
            inactive=wander=0;sitting=false;feedback=1.6f;bubble.text=DesktopLocalization.Text("ui.home.room.message.companion", "喵～一起待一会儿");
            var roll = CozyHomeAnimation.Find(skin, "pet-roll");
            if (!CozyMotion.Reduced && roll != null) { activity = Activity.Rolling; target = feet; PlayAnimation(roll); }
            bubble.transform.parent.gameObject.SetActive(true);Present();
        }
        public void Advance(float delta)
        {
            if(delta<=0||float.IsNaN(delta)||float.IsInfinity(delta)||!isActiveAndEnabled||pageOpen||!focused)return;
            if (CozyMotion.Paused) { CancelAnimation(); Present(); return; }
            RefreshSkin();
            while (delta > 0)
            {
                var step = Mathf.Min(delta, 1f / 24f);
                AdvanceRoom(step); delta -= step;
            }
            Present();
        }
        void AdvanceRoom(float delta)
        {
            inactive+=delta;wander+=delta;feedback=Mathf.Max(0,feedback-delta);
            bubble.transform.parent.gameObject.SetActive(feedback>0);
            if (AdvanceInteraction(delta)) return;
            if (activity == Activity.GoingToBed || IsSleeping)
            {
                target = BedDestination();
                if (!furnitureDragging && !IsSleeping) AdvanceLocomotion(delta, 210);
                if ((feet-target).sqrMagnitude < .01f) { activity = Activity.Sleeping; animationClip = null; }
            }
            else if(!dragging && !furnitureDragging)
            {
                AdvanceLocomotion(delta, 180);
                if(inactive>=29.999f) {if(!sitting)Sit();inactive=30;}
                else if(wander>=12&&!CozyMotion.Reduced&&!sitting) {target=ClampFeet(new Vector2(UnityEngine.Random.Range(620,1030),UnityEngine.Random.Range(710,765)));wander=0;}
            }
        }
        void Present()
        {
            if(actor==null)return;
            var mapped=sitting?mappedSit:mappedRest;
            var size=sitting?new Vector2(176,204):new Vector2(172,246);
            if (IsSleeping && sleepingPose != null)
            {
                artwork.texture = sleepingPose.texture; artwork.uvRect = new Rect(0,0,1,1);
                var bed = (RectTransform)Prop("cat-bed").transform;
                var width = bed.rect.width * .64f;
                size = new Vector2(width, width * sleepingPose.rect.height / sleepingPose.rect.width);
            }
            else if(mapped!=null)
            {
                artwork.texture=mapped.texture;var r=mapped.textureRect;
                artwork.uvRect=new Rect(r.x/mapped.texture.width,r.y/mapped.texture.height,r.width/mapped.texture.width,r.height/mapped.texture.height);
                size=new Vector2(228,228*r.height/r.width);
            }
            else
            {
                artwork.texture=sitting?seated:standing;
                artwork.uvRect = new Rect(0,0,1,1);
            }
            actor.sizeDelta=size;actor.anchoredPosition=new Vector2(feet.x-size.x/2,-feet.y+size.y);
            if (animationClip != null && !IsSleeping && !sitting) PresentAnimation();
            actor.localEulerAngles=new Vector3(0,0,animationClip==null&&feedback>0&&!CozyMotion.Reduced?Mathf.Sin(feedback*16)*3:0);
            ((RectTransform)bubble.transform.parent).anchoredPosition=new Vector2(feet.x-120,-feet.y+size.y+60);
            if (bedFront != null)
            {
                bedFront.gameObject.SetActive(IsSleeping && !pageOpen);
                if (IsSleeping)
                {
                    var bed = (RectTransform)Prop("cat-bed").transform;
                    var frontHeight = bed.rect.height * .35f;
                    bedFront.rectTransform.sizeDelta = new Vector2(bed.rect.width,frontHeight);
                    bedFront.rectTransform.anchoredPosition = bed.anchoredPosition + new Vector2(0,-bed.rect.height+frontHeight);
                    bedFront.uvRect = new Rect(0,0,1,.35f);
                }
            }
            PresentFood();
        }
        public Vector2 PointerPosition(PointerEventData e)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform,e.position,e.pressEventCamera,out var p);return new Vector2(p.x,-p.y);
        }
        bool ActorHit(PointerEventData e) => RectTransformUtility.RectangleContainsScreenPoint(actor,e.pressPosition,e.pressEventCamera);
        public void OnPointerClick(PointerEventData e)
        { if(!CanInteract||e.button!=PointerEventData.InputButton.Left||e.dragging)return;if(ActorHit(e))Respond();else MoveTo(PointerPosition(e)); }
        public void OnBeginDrag(PointerEventData e)
        {if(!CanInteract||e.button!=PointerEventData.InputButton.Left||!ActorHit(e))return;CancelAnimation();activity=Activity.Free;dragging=true;pointer=e.pointerId;dragOffset=feet-PointerPosition(e);inactive=wander=0;sitting=false;}
        public void OnDrag(PointerEventData e)
        {if(!CanInteract||!dragging||pointer!=e.pointerId)return;feet=target=ClampFeet(PointerPosition(e)+dragOffset);Present();}
        public void OnEndDrag(PointerEventData e) {if(pointer!=e.pointerId)return;dragging=false;pointer=int.MinValue;target=feet;}
        void Update()
        {
            if (CanInteract && !InputFieldFocused())
            {
                var d=new Vector2(Input.GetKey(KeyCode.RightArrow)?1:Input.GetKey(KeyCode.LeftArrow)?-1:0,Input.GetKey(KeyCode.DownArrow)?1:Input.GetKey(KeyCode.UpArrow)?-1:0);
                if(d!=Vector2.zero)MoveTo(feet+d.normalized*60);
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
        void OnApplicationFocus(bool value){focused=value;if(!value){CancelAnimation();CancelPropDrags();dragging=false;pointer=int.MinValue;target=feet;Present();}}
        void OnApplicationPause(bool value){if(value){CancelAnimation();CancelPropDrags();dragging=false;pointer=int.MinValue;target=feet;Present();}}
        void OnEnable(){if(actor!=null){RefreshSkin();Present();}}
        void OnDisable(){CancelAnimation();CancelPropDrags();dragging=false;pointer=int.MinValue;target=feet;}
    }
}
