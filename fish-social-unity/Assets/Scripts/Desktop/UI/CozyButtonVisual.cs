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
        public Vector2 MotionScale => motionScale;
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

        public bool Selected => selected;

        public void SetSelected(bool value)
        {
            selected = value;
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

        internal void SetMotionScale(Vector2 scale)
        {
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
                icon = icon != null ? icon : transform.Find("Icon")?.GetComponent<Image>()
                    ?? transform.Find("CozyIcon")?.GetComponent<Image>();
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
                if (graphic == hitImage || graphic == plate || graphic.GetComponentInParent<Selectable>(true) != button
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
            if (flat && !depressed && !highlighted)
                color.a = 0f;
            var state = !interactable ? "disabled" : depressed ? "pressed" : selected ? "selected"
                : highlighted ? "hover" : "default";
            CozyJournalTheme.ApplySurface(plate, SurfacePath(state), expandSurfaceInsets);
            plate.color = color;
            if (plateOffset == null)
                plateOffset = plate.GetComponent<CozyGraphicOffset>() ?? plate.gameObject.AddComponent<CozyGraphicOffset>();
            var displacement = depressed && !CozyMotion.Reduced ? -3f : 0f;
            plateOffset.SetVisual(transform, displacement, 1f, motionScale);
            foreach (var offset in offsets)
            {
                if (offset != null)
                    offset.SetVisual(transform, displacement, dimmed ? 0.55f : 1f, motionScale);
            }
            HideHitImage();
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
            if (initialized && button != null && interactable != CanInteract())
            {
                pointerPressed = false;
                submitPressed = false;
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
            if (reduced) motionScale = Vector2.one;
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
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            if (pointerPressed) CozyMotion.CancelPress(transform as RectTransform);
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
            CozyMotion.PressDown(transform as RectTransform, true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            var wasPressed = pointerPressed;
            pointerPressed = false;
            RefreshState();
            if (wasPressed)
                CozyMotion.PressUp(transform as RectTransform);
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
            CozyMotion.PressUp(transform as RectTransform);
            RefreshState();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!initialized || !CanInteract() || !isActiveAndEnabled)
                return;
            if (submitRoutine != null)
                StopCoroutine(submitRoutine);
            submitPressed = true;
            CozyMotion.PressDown(transform as RectTransform, true);
            RefreshState();
            submitRoutine = StartCoroutine(ReleaseSubmit());
        }

        IEnumerator ReleaseSubmit()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, button.colors.fadeDuration));
            submitPressed = false;
            submitRoutine = null;
            CozyMotion.PressUp(transform as RectTransform);
            RefreshState();
        }
    }

}
