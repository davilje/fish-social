using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>Icon-only furniture actions retain accessible names without drawing hover text.</summary>
    public sealed class CozyHomeActionTag : MonoBehaviour
    {
        [SerializeField] CozyHomeRoom room;
        [SerializeField] string textKey, fallback;
        CozyCssGraphic stateDot;
        public string AccessibleName => DesktopLocalization.Text(textKey, fallback);
        public void Configure(CozyHomeRoom owner, string key, string label)
        {
            room = owner; textKey = key; fallback = label;
            if ((name == "Sleep" || name == "Food") && stateDot == null)
            {
                var existing = transform.Find("StateDot");
                var node = existing != null ? existing.gameObject : new GameObject("StateDot", typeof(RectTransform), typeof(CanvasRenderer), typeof(CozyCssGraphic));
                node.transform.SetParent(transform, false);
                var rect = (RectTransform)node.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1,1); rect.pivot = new Vector2(.5f,.5f);
                rect.anchoredPosition = new Vector2(-6,-6); rect.sizeDelta = new Vector2(8,8);
                stateDot = node.GetComponent<CozyCssGraphic>(); stateDot.shape = CozyCssGraphic.Shape.Circle; stateDot.raycastTarget = false;
                stateDot.color = name == "Sleep" ? (Color)new Color32(146,163,231,255) : new Color32(252,146,60,255);
                node.SetActive(false);
            }
        }
        void Update()
        {
            if (stateDot == null || room == null) return;
            var active = name == "Sleep" ? room.IsSleeping || room.CurrentActivity == CozyHomeRoom.Activity.GoingToBed : room.IsFeeding;
            if (stateDot.gameObject.activeSelf != active) stateDot.gameObject.SetActive(active);
        }
    }
}
