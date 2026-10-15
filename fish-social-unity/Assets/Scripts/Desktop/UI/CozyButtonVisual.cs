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
        bool generatedIcon;
        Vector2 plateRestPosition;
        bool interactable;
        Coroutine submitRoutine;

        public bool Selected => selected;

        public void SetSelected(bool value)
        {
            selected = value;
            RefreshState();
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
                plateRestPosition = plate.rectTransform.anchoredPosition;
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
            var highlighted = interactable && (hovered || selected || (!flat && focused));
            var sprite = depressed ? pressedSprite != null ? pressedSprite : normalSprite
                : highlighted ? hoverSprite != null ? hoverSprite : normalSprite : normalSprite;
            var baseTint = selected ? Color.white : tint;
            var color = sprite != null ? baseTint
                : selected ? SelectedColor : baseTint == Color.white ? CatTheme.Cream : baseTint;
            if (depressed && sprite == null)
                color = new Color(color.r * 0.86f, color.g * 0.86f, color.b * 0.86f, color.a);
            if (!interactable)
            {
                color = Color.Lerp(color, CatTheme.Cream, 0.6f);
                color.a = 0.55f;
            }
            if (flat && !depressed && !highlighted)
                color.a = 0f;
            CatTheme.SetPlate(plate, sprite, color);
            plate.rectTransform.anchoredPosition = plateRestPosition + (depressed ? new Vector2(0f, -3f) : Vector2.zero);
            foreach (var offset in offsets)
            {
                if (offset != null)
                    offset.SetVisual(transform, depressed ? -3f : 0f, interactable ? 1f : 0.55f);
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
            focused = EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject;
            RefreshState();
        }

        void OnDisable()
        {
            Canvas.willRenderCanvases -= HideHitImage;
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
            RefreshState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!initialized || eventData.button != PointerEventData.InputButton.Left || !CanInteract())
                return;
            hovered = true;
            pointerPressed = true;
            RefreshState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;
            pointerPressed = false;
            RefreshState();
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
            RefreshState();
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!initialized || !CanInteract() || !isActiveAndEnabled)
                return;
            if (submitRoutine != null)
                StopCoroutine(submitRoutine);
            submitPressed = true;
            RefreshState();
            submitRoutine = StartCoroutine(ReleaseSubmit());
        }

        IEnumerator ReleaseSubmit()
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, button.colors.fadeDuration));
            submitPressed = false;
            submitRoutine = null;
            RefreshState();
        }
    }

}
