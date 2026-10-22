using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Preserves the sliced round caps; Image.Filled stretches a 96px sprite across the entire track.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Image))]
    public sealed class CozyJournalProgressFill : MonoBehaviour
    {
        [SerializeField] Image fill;
        public void SetValue(float value)
        {
            if (fill == null) fill = GetComponent<Image>();
            fill.fillAmount = Mathf.Clamp01(value);
            Refresh();
        }
        public void Refresh()
        {
            if (fill == null) fill = GetComponent<Image>();
            var rect = fill.rectTransform;
            var parent = rect.parent as RectTransform;
            if (parent == null) return;
            fill.type = Image.Type.Sliced; fill.preserveAspect = false;
            rect.anchorMin = rect.anchorMax = new Vector2(0, .5f);
            rect.pivot = new Vector2(0, .5f); rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(parent.rect.width * fill.fillAmount, parent.rect.height);
            fill.enabled = fill.fillAmount > 0;
        }
        void OnEnable() { Refresh(); }
        void LateUpdate() { Refresh(); }
    }
}
