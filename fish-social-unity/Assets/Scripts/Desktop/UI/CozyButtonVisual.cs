using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class CozyButtonVisual : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        static readonly Color SelectedColor = new Color(0xFF / 255f, 0xF0 / 255f, 0xDD / 255f, 1f);
        readonly List<CozyGraphicOffset> offsets = new List<CozyGraphicOffset>();
        Button button;
        Image hitImage;
        [SerializeField] Image plate;
        [SerializeField] Image icon;
        [SerializeField] Sprite normalSprite;
        [SerializeField] Sprite hoverSprite;
        [SerializeField] Sprite pressedSprite;
        [SerializeField] Sprite selectedSprite;
        [SerializeField] string surfaceNormal;
        [SerializeField] string surfaceHover;
        [SerializeField] string surfacePressed;
        [SerializeField] string surfaceSelected;
        [SerializeField] string surfaceDisabled;
        [SerializeField] bool primarySurface;
        Sprite disabledSprite;
        CozyGraphicOffset plateOffset;
        Vector2 motionScale = Vector2.one;
        public Vector2 MotionScale => IsIndex ? EnsureIndexMotion().Current.Scale : motionScale;
        Vector2 motionOffset;
        public bool LightPress => GetComponent<CozyStickerMotion>() != null
            || (!string.IsNullOrEmpty(surfaceNormal) && (surfaceNormal.Contains("sticker") || surfaceNormal.Contains("bobber")));
        [SerializeField] Color tint = Color.white;
        [SerializeField] bool explicitTint;
        bool initialized;
        bool hovered;
        bool focused;
        bool pointerPressed;
        bool submitPressed;
        [SerializeField] bool selected;
        [SerializeField] bool stackedIcon;
        [SerializeField] bool flat;
        bool generatedPlate;
        bool expandSurfaceInsets;
        bool generatedIcon;
        bool interactable;
        Coroutine submitRoutine;

        CozyIndexMotion indexMotion;
        CozyIndexGraphic indexGraphic;
        CozyGraphicOffset indexGraphicOffset;
        public CozyIndexMotion.Pose IndexPose => EnsureIndexMotion().Current;
        CozyIndexMotion EnsureIndexMotion() => indexMotion ?? (indexMotion = new CozyIndexMotion(selected));
        public void AdvanceIndex(float seconds)
        {
            if (!IsIndex || CozyMotion.Paused) return;
            if (CozyMotion.Reduced) EnsureIndexMotion().Settle(); else EnsureIndexMotion().Advance(seconds);
            RefreshState();
        }
        public void BeginIndexEntrance(int ordinal)
        {
            if (!IsIndex) return;
            EnsureIndexMotion().BeginEntrance(ordinal);
            if (CozyMotion.Reduced) indexMotion.Settle();
            RefreshState();
        }
        public static void BeginRailEntrance(Transform root)
        {
            var tabs = new List<CozyButtonVisual>();
            foreach (var visual in root.GetComponentsInChildren<CozyButtonVisual>(true))
                if (visual.IsIndex && visual.gameObject.activeInHierarchy) tabs.Add(visual);
            tabs.Sort((a,b) => b.transform.position.y.CompareTo(a.transform.position.y));
            for(var i=0;i<tabs.Count;i++) tabs[i].BeginIndexEntrance(i);
        }
        bool IsNavigation => transform.parent != null && transform.parent.name == "Nav";
        bool IsPrimary => primarySurface || (!string.IsNullOrEmpty(surfaceNormal) && surfaceNormal.Contains("button-primary"));
        bool IsIndex => !string.IsNullOrEmpty(surfaceNormal) && surfaceNormal.Contains("tab-index");
        public bool Selected => selected;

        /// <summary>Settle the visual pose synchronously for Editor screenshots, retaining fixed hit geometry.</summary>
        public void ApplyFinalPose()
        {
            if (!initialized) CatTheme.StyleButton(GetComponent<Button>());
            if (IsIndex) EnsureIndexMotion().Settle();
            motionScale = Vector2.one;
            motionOffset = Vector2.zero;
            RefreshState();
            GetComponent<CozyStickerMotion>()?.ApplyFinalPose();
        }

        public void SetSelected(bool value)
        {
            var motion = IsIndex ? EnsureIndexMotion() : null;
            if (selected == value) { RefreshState(); return; }
            selected = value;
            if (motion != null)
            {
                motion.Select(value);
                if (CozyMotion.Reduced) motion.Settle();
            }
            RefreshState();
            GetComponent<CozyJournalCardVisual>()?.RefreshNow();
        }

        public void SetFlat(bool value)
        {
            flat = value;
            RefreshState();
        }

        public void SetTint(Color value)
        {
            tint = value;
            explicitTint = true;
            RefreshState();
        }

        /// <summary>Persist a card/tab/button family; later theme refreshes preserve this contract.</summary>
        public void ConfigureSurface(string normalPath, string hoverPath = null, string pressedPath = null,
            string selectedPath = null, string disabledPath = null)
        {
            surfaceNormal = normalPath;
            surfaceHover = hoverPath;
            surfacePressed = pressedPath;
            surfaceSelected = selectedPath;
            surfaceDisabled = disabledPath;
            ApplyPlateFamily();
            RefreshState();
        }

        internal void SetMotionOffset(Vector2 offset)
        {
            if (!initialized) CatTheme.StyleButton(GetComponent<Button>());
            motionOffset = CozyMotion.Reduced ? Vector2.zero : offset;
            RefreshState();
        }

        internal void SetMotionScale(Vector2 scale)
        {
            if (!initialized) CatTheme.StyleButton(GetComponent<Button>());
            motionScale = CozyMotion.Reduced ? Vector2.one : scale;
            RefreshState();
        }

        string SurfacePath(string state)
        {
            if (!string.IsNullOrEmpty(surfaceNormal))
            {
                var configured = state == "hover" ? surfaceHover : state == "pressed" ? surfacePressed
                    : state == "selected" ? surfaceSelected : state == "disabled" ? surfaceDisabled : surfaceNormal;
                return string.IsNullOrEmpty(configured) ? surfaceNormal : configured;
            }
            return "surfaces/button-" + (primarySurface ? "primary-" : "quiet-")
                + (state == "selected" ? "hover" : state);
        }

        static Sprite LoadSurface(string path)
        {
            return CozyJournalTheme.Sprite(path);
        }

        void ApplyPlateFamily()
        {
            normalSprite = LoadSurface(SurfacePath("default"));
            hoverSprite = LoadSurface(SurfacePath("hover"));
            pressedSprite = LoadSurface(SurfacePath("pressed"));
            selectedSprite = LoadSurface(SurfacePath("selected"));
            disabledSprite = LoadSurface(SurfacePath("disabled"));
            if (button == null) return;
            button.transition = Selectable.Transition.None;
            var states = button.spriteState;
            states.highlightedSprite = hoverSprite;
            states.selectedSprite = selectedSprite;
            states.pressedSprite = pressedSprite;
            states.disabledSprite = disabledSprite;
            button.spriteState = states;
        }

        public void SetStackedIcon(bool value)
        {
            stackedIcon = value;
            RefreshContent();
        }

        internal bool Owns(Transform node)
        {
            return (plate != null && node == plate.transform) || (icon != null && node == icon.transform);
        }

        internal void Configure(Button owner, string iconName, bool primary)
        {
            button = owner;
            primarySurface = primarySurface || primary;
            if (!initialized)
            {
                plate = plate != null ? plate : transform.Find("CozyVisual")?.GetComponent<Image>();
                if (plate == null && transform.Find("CozyVisual") != null)
                {
                    Debug.LogError("[DesktopUI] CozyVisual must have an Image: " + name);
                    return;
                }
                if (icon == null) icon = transform.Find("Icon")?.GetComponent<Image>();
                if (icon == null) icon = transform.Find("CozyIcon")?.GetComponent<Image>();
                if (!explicitTint)
                {
                    tint = plate != null ? plate.color : primary ? CatTheme.Coral : Color.white;
                    if (flat && tint.a == 0f)
                        tint.a = 1f;
                }
                normalSprite = normalSprite != null ? normalSprite : plate != null && plate.sprite != null
                    ? plate.sprite : CatTheme.LoadCozy("buttons/plate-default.png", CatTheme.PlateBorder);
                hoverSprite = hoverSprite != null ? hoverSprite
                    : CatTheme.LoadCozy("buttons/plate-hover.png", CatTheme.PlateBorder);
                pressedSprite = pressedSprite != null ? pressedSprite
                    : CatTheme.LoadCozy("buttons/plate-pressed.png", CatTheme.PlateBorder);
                hitImage = button.GetComponent<Image>();
                if (hitImage == null && button.targetGraphic is Image targetImage
                    && targetImage != plate && targetImage != icon && targetImage.transform.IsChildOf(transform))
                    hitImage = targetImage;
                var originalIcon = hitImage != null && hitImage.preserveAspect && hitImage.type == Image.Type.Simple
                    ? hitImage.sprite : null;
                if (plate == null)
                {
                    plate = CreateImage("CozyVisual");
                    generatedPlate = true;
                    plate.transform.SetAsFirstSibling();
                    plate.rectTransform.anchorMin = Vector2.zero;
                    plate.rectTransform.anchorMax = Vector2.one;
                    plate.rectTransform.offsetMin = Vector2.zero;
                    plate.rectTransform.offsetMax = Vector2.zero;
                    if (hitImage != null)
                        plate.maskable = hitImage.maskable;
                }
                expandSurfaceInsets = generatedPlate || (plate.rectTransform.anchorMin == Vector2.zero
                    && plate.rectTransform.anchorMax == Vector2.one);
                plate.raycastTarget = false;
                foreach (var shadow in plate.GetComponents<Shadow>())
                    shadow.enabled = false;
                if (originalIcon != null && icon == null)
                    SetIcon(originalIcon);
                if (hitImage != null)
                {
                    foreach (var shadow in hitImage.GetComponents<Shadow>())
                        shadow.enabled = false;
                }
                button.transition = Selectable.Transition.None;
                initialized = true;
                interactable = CanInteract();
                focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            }
            else if (!explicitTint && primary && generatedPlate)
                tint = CatTheme.Coral;
            ApplyPlateFamily();
            if (icon == null || generatedIcon)
                SetIconName(iconName);
            if (icon != null && icon.sprite != null)
            {
                if (generatedIcon)
                    icon.color = Color.white;
                icon.type = Image.Type.Simple;
                icon.preserveAspect = true;
                icon.raycastTarget = false;
            }
            RefreshContent();
            RefreshState();
            if (generatedPlate && GetComponent<CozyPanelSkin>() == null)
                gameObject.AddComponent<CozyPanelSkin>();
        }

        internal void SetIconName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return;
            if (!initialized)
            {
                CatTheme.StyleButton(GetComponent<Button>(), name);
                return;
            }
            var sprite = CatTheme.LoadCozy(CatTheme.IconPath(name), Vector4.zero);
            if (sprite != null)
                SetIcon(sprite);
        }

        internal void RefreshContent()
        {
            if (!initialized)
                return;
            offsets.Clear();
            var hasLabel = false;
            foreach (var graphic in GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is CozyIndexGraphic || graphic == hitImage || graphic == plate || graphic.GetComponentInParent<Selectable>(true) != button
                    || graphic.GetComponentInParent<InputField>(true) != null || graphic.GetComponent<Mask>() != null
                    || graphic.GetComponent<RectMask2D>() != null
                    || (graphic is Image contentImage && contentImage.type == Image.Type.Filled))
                    continue;
                if (graphic is Text label)
                {
                    hasLabel = true;
                    CatTheme.ApplyTextFont(label);
                    if (label.transform.parent == transform && (label.name == "Label" || label.name == "Text")
                        && !label.text.Contains("\n") && GetComponentsInChildren<Text>(true).Length == 1)
                    {
                        label.horizontalOverflow = HorizontalWrapMode.Overflow;
                        label.verticalOverflow = VerticalWrapMode.Truncate;
                        label.resizeTextForBestFit = true;
                        label.resizeTextMinSize = Mathf.Min(12, label.fontSize);
                        label.resizeTextMaxSize = label.fontSize;
                    }
                    if (generatedPlate && label.GetComponent<CozyGraphicOffset>() == null)
                        CatTheme.SkinText(label);
                }
                var offset = graphic.GetComponent<CozyGraphicOffset>();
                if (offset == null)
                    offset = graphic.gameObject.AddComponent<CozyGraphicOffset>();
                if (graphic is Text && generatedPlate)
                    offset.SetStackedLabel(stackedIcon);
                offsets.Add(offset);
                foreach (var shadow in graphic.GetComponents<Shadow>())
                    shadow.enabled = false;
            }
            if (icon != null && generatedIcon)
            {
                icon.rectTransform.anchorMin = stackedIcon || !hasLabel
                    ? new Vector2(0.5f, 0.5f) : new Vector2(0f, 0.5f);
                icon.rectTransform.anchorMax = icon.rectTransform.anchorMin;
                icon.rectTransform.sizeDelta = stackedIcon ? new Vector2(34f, 34f) : new Vector2(24f, 24f);
                icon.rectTransform.anchoredPosition = stackedIcon ? new Vector2(0f, 12f)
                    : hasLabel ? new Vector2(24f, 0f) : Vector2.zero;
            }
            ReserveInlineIconSpace();
            RefreshState();
        }

        Image CreateImage(string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            child.transform.SetParent(transform, false);
            child.GetComponent<LayoutElement>().ignoreLayout = true;
            var image = child.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        void SetIcon(Sprite sprite)
        {
            if (sprite == null)
                return;
            if (icon == null)
            {
                if (transform.Find("Icon") != null || transform.Find("CozyIcon") != null)
                {
                    Debug.LogError("[DesktopUI] Icon must have an Image: " + name);
                    return;
                }
                icon = CreateImage("CozyIcon");
                generatedIcon = true;
                icon.rectTransform.sizeDelta = new Vector2(24f, 24f);
            }
            icon.sprite = sprite;
            icon.enabled = true;
            icon.type = Image.Type.Simple;
            icon.preserveAspect = true;
            icon.color = Color.white;
        }

        void ReserveInlineIconSpace()
        {
            if (icon != null && icon.gameObject.activeSelf && icon.enabled && icon.sprite != null && !stackedIcon
                && GetComponentsInChildren<Text>(true).Length == 1)
            {
                var label = transform.Find("Label")?.GetComponent<Text>();
                if (label != null && label.rectTransform.anchorMin.x == 0f && label.rectTransform.anchorMax.x == 1f)
                {
                    var inset = icon.rectTransform.anchoredPosition.x
                        + icon.rectTransform.sizeDelta.x * (1f - icon.rectTransform.pivot.x) + 8f;
                    var minimum = label.rectTransform.offsetMin;
                    minimum.x = Mathf.Max(minimum.x, inset);
                    label.rectTransform.offsetMin = minimum;
                }
            }
        }

        void RefreshState()
        {
            if (!initialized || plate == null || button == null)
                return;
            interactable = CanInteract();
            var depressed = interactable && ((pointerPressed && hovered) || submitPressed);
            var highlighted = selected || interactable && (hovered || (!flat && focused));
            var sprite = !interactable && disabledSprite != null ? disabledSprite : depressed ? pressedSprite != null ? pressedSprite : normalSprite
                : selected && selectedSprite != null ? selectedSprite
                : highlighted ? hoverSprite != null ? hoverSprite : normalSprite : normalSprite;
            var baseTint = sprite != null ? Color.white : selected ? Color.white : tint;
            var color = sprite != null ? baseTint
                : selected ? SelectedColor : baseTint == Color.white ? CatTheme.Cream : baseTint;
            if (depressed && sprite == null)
                color = new Color(color.r * 0.86f, color.g * 0.86f, color.b * 0.86f, color.a);
            var dimmed = !interactable && !selected;
            if (dimmed)
            {
                color = Color.Lerp(color, CatTheme.Cream, 0.6f);
                color.a = 0.55f;
            }
            // Original .navtab has no painted background in any state; only text changes color.
            if (IsNavigation || (flat && !depressed && !highlighted))
                color.a = 0f;
            var state = !interactable ? "disabled" : IsIndex ? selected ? "selected" : "default"
                : depressed ? "pressed" : selected ? "selected" : highlighted ? "hover" : "default";
            CozyJournalTheme.ApplySurface(plate, SurfacePath(state), expandSurfaceInsets);
            plate.color = color;
            if (plateOffset == null) plateOffset = plate.GetComponent<CozyGraphicOffset>();
            if (plateOffset == null) plateOffset = plate.gameObject.AddComponent<CozyGraphicOffset>();
            var pose = IsIndex ? EnsureIndexMotion().Current : new CozyIndexMotion.Pose { Scale = motionScale, Alpha = 1 };
            var displacement = (CozyMotion.Reduced ? Vector2.zero : motionOffset) + Vector2.right * pose.X;
            var scale = IsIndex ? pose.Scale : motionScale;
            if (IsIndex) RefreshIndexGraphic(pose, interactable);
            if (IsIndex || IsPrimary || IsNavigation)
            {
                var lightIcon = !IsNavigation && (IsPrimary || selected);
                var foreground = lightIcon ? Color.white : IsNavigation && selected
                    ? new Color(212f / 255, 111f / 255, 34f / 255) : new Color(117f / 255, 71f / 255, 46f / 255);
                if (IsIndex) foreground = Color.Lerp(new Color(117f/255,71f/255,46f/255),Color.white,pose.Selection);
                foreach (var label in GetComponentsInChildren<Text>(true))
                    if (label.GetComponentInParent<Selectable>(true) == button) label.color = foreground;
                if (icon != null)
                {
                    // Source icons contain ink pixels: tinting them white cannot create the original white glyph.
                    var source = icon.sprite;
                    var alternate = source != null ? Resources.Load<Sprite>("Desktop/Theme/Cozy/" + (lightIcon ? "icons-light/" : "icons/") + source.name) : null;
                    if (alternate != null) icon.sprite = alternate;
                    icon.color = Color.white;
                }
            }
            plateOffset.SetVisual(transform, displacement, pose.Alpha, scale);
            foreach (var offset in offsets)
            {
                if (offset != null)
                    offset.SetVisual(transform, displacement, pose.Alpha * (dimmed ? 0.55f : 1f), scale);
            }
            if (indexGraphicOffset != null && IsIndex)
                indexGraphicOffset.SetVisual(transform,displacement,pose.Alpha,scale);
            HideHitImage();
        }

        void RefreshIndexGraphic(CozyIndexMotion.Pose pose, bool active)
        {
            if (indexGraphic == null)
            {
                var existing = transform.Find("JournalIndexPaint");
                var node = existing != null ? existing.gameObject : new GameObject("JournalIndexPaint",typeof(RectTransform),typeof(CanvasRenderer),typeof(CozyIndexGraphic));
                node.transform.SetParent(transform,false);
                if(node.GetComponent<CanvasRenderer>()==null)node.AddComponent<CanvasRenderer>();
                indexGraphic=node.GetComponent<CozyIndexGraphic>();
                if(indexGraphic==null) indexGraphic=node.AddComponent<CozyIndexGraphic>();
                var rect=(RectTransform)node.transform;
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
                rect.SetSiblingIndex(plate.transform.GetSiblingIndex()+1);
                indexGraphic.raycastTarget=false;
                indexGraphicOffset=node.GetComponent<CozyGraphicOffset>() ?? node.AddComponent<CozyGraphicOffset>();
            }
            indexGraphic.enabled=active;
            indexGraphic.SetState(selected,pose.Selection);
            if(active) plate.color=Color.clear;
        }

        bool CanInteract()
        {
            return button != null && button.isActiveAndEnabled && button.IsInteractable();
        }

        void Start()
        {
            if (!initialized)
                CatTheme.StyleButton(GetComponent<Button>());
        }

        void Update()
        {
            if (IsIndex) AdvanceIndex(Time.unscaledDeltaTime);
            if (initialized && button != null && interactable != CanInteract())
            {
                pointerPressed = false;
                submitPressed = false;
                if (IsIndex) EnsureIndexMotion().CancelPress();
                CozyMotion.CancelPress(transform as RectTransform);
                RefreshState();
            }
        }

        void LateUpdate()
        {
            if (hitImage != null)
                hitImage.canvasRenderer.SetAlpha(0f);
        }

        void OnEnable()
        {
            Canvas.willRenderCanvases += HideHitImage;
            CozyMotion.ReducedChanged += OnReducedChanged;
            focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            RefreshState();
        }

        void OnDisable()
        {
            Canvas.willRenderCanvases -= HideHitImage;
            CozyMotion.ReducedChanged -= OnReducedChanged;
            CozyMotion.CancelPress(transform as RectTransform);
            motionScale = Vector2.one;
            if (IsIndex) EnsureIndexMotion().Settle();
            hovered = false;
            focused = false;
            pointerPressed = false;
            submitPressed = false;
            if (submitRoutine != null)
            {
                StopCoroutine(submitRoutine);
                submitRoutine = null;
            }
            RefreshState();
        }

        void OnReducedChanged(bool reduced)
        {
            if (reduced) { motionScale = Vector2.one; motionOffset = Vector2.zero; if (IsIndex) EnsureIndexMotion().Settle(); }
            RefreshState();
        }

        void HideHitImage()
        {
            if (hitImage != null)
                hitImage.canvasRenderer.SetAlpha(0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            RefreshState();
            if (primarySurface && CanInteract())
            {
                GetComponentInParent<Canvas>()?.GetComponentInChildren<CozyCornerCharacter>(true)?.Lean(true);
                if (icon != null)
                {
                    var item = icon.rectTransform;
                    var originalRotation = item.localRotation;
                    CozyMotion.Animate(item, .42f, t => { if (item != null) item.localRotation = originalRotation
                        * Quaternion.Euler(0, 0, CozyWebMotion.Frames(t, 0, 14, -10, 0)); },
                        easing: CozyWebMotion.Ease.InOutSine);
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (primarySurface) GetComponentInParent<Canvas>()?.GetComponentInChildren<CozyCornerCharacter>(true)?.Lean(false);
            if (pointerPressed && !IsIndex) CozyMotion.CancelPress(transform as RectTransform);
            RefreshState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!initialized || eventData.button != PointerEventData.InputButton.Left)
                return;
            if (!CanInteract())
            {
                CozyMotion.PressDown(transform as RectTransform, false);
                return;
            }
            hovered = true;
            pointerPressed = true;
            RefreshState();
            if (IsIndex) { EnsureIndexMotion().PointerDown(); if (CozyMotion.Reduced) indexMotion.Settle(); RefreshState(); }
            else CozyMotion.PressDown(transform as RectTransform, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            var wasPressed = pointerPressed;
            pointerPressed = false;
            RefreshState();
            if (wasPressed)
            {
                if (IsIndex) { EnsureIndexMotion().PointerUp(); RefreshState(); }
                else CozyMotion.PressUp(transform as RectTransform);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            focused = true;
            RefreshState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            focused = false;
            pointerPressed = false;
            if (IsIndex) EnsureIndexMotion().PointerUp();
            else CozyMotion.PressUp(transform as RectTransform);
            RefreshState();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!initialized || !CanInteract() || !isActiveAndEnabled)
                return;
            if (submitRoutine != null)
                StopCoroutine(submitRoutine);
            submitPressed = true;
            if (IsIndex) { EnsureIndexMotion().PointerDown(); if (CozyMotion.Reduced) indexMotion.Settle(); RefreshState(); }
            else CozyMotion.PressDown(transform as RectTransform, true);
            RefreshState();
            submitRoutine = StartCoroutine(ReleaseSubmit());
        }

        IEnumerator ReleaseSubmit()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, button.colors.fadeDuration));
            submitPressed = false;
            submitRoutine = null;
            if (IsIndex) EnsureIndexMotion().PointerUp();
            else CozyMotion.PressUp(transform as RectTransform);
            RefreshState();
        }
    }

}
