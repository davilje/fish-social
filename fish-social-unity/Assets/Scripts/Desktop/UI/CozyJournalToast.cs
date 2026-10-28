using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Original app.js toast queue: 420ms entrance, 2500ms hold, 320ms exit.</summary>
    [DisallowMultipleComponent]
    public sealed class CozyJournalToast : MonoBehaviour
    {
        struct Notice { public string Text; public bool Error; }
        readonly Queue<Notice> queue = new Queue<Notice>();
        RectTransform root;
        RectTransform mascot;
        CanvasGroup alpha;
        float elapsed;
        bool error;
        public const float Duration = 3.24f;
        public bool IsPlaying => root != null;
        public int QueuedCount => queue.Count;
        public static CozyJournalToast Show(Transform host, string text, bool error = false)
        {
            if (host == null || string.IsNullOrEmpty(text)) return null;
            var presenter = host.GetComponent<CozyJournalToast>();
            if (presenter == null) presenter = host.gameObject.AddComponent<CozyJournalToast>();
            presenter.queue.Enqueue(new Notice { Text = DesktopLocalization.Message(text), Error = error });
            presenter.Next();
            return presenter;
        }
        void Next()
        {
            if (root != null || queue.Count == 0 || CozyMotion.Paused || !isActiveAndEnabled) return;
            var notice = queue.Dequeue(); error = notice.Error; elapsed = 0;
            root = Node("JournalToast", transform, new Vector2(474, 84));
            root.anchorMin = root.anchorMax = root.pivot = Vector2.one;
            root.anchoredPosition = new Vector2(-40, -112);
            alpha = root.gameObject.AddComponent<CanvasGroup>(); alpha.blocksRaycasts = false;
            var note = Node("Note", root, new Vector2(360, 60));
            // The authored toast slice has 100px borders, so it disappears inside a short note.
            // The preview note is a plain paper card, drawn here at the text size.
            var plateRect = Node("Plate", note, Vector2.zero);
            plateRect.anchorMin = Vector2.zero;
            plateRect.anchorMax = Vector2.one;
            plateRect.offsetMin = Vector2.zero;
            plateRect.offsetMax = Vector2.zero;
            var plate = plateRect.gameObject.AddComponent<CozyCssGraphic>();
            plate.shape = CozyCssGraphic.Shape.Paper;
            plate.radius = 16f;
            plate.border = 2f;
            plate.color = new Color(1f, 253f / 255f, 247f / 255f, 1f);
            plate.borderColor = error ? new Color(163f / 255f, 57f / 255f, 43f / 255f, 1f) : new Color(0.91f, 0.82f, 0.67f, 1f);
            plate.raycastTarget = false;
            var label = Node("Text", note, new Vector2(324, 42)).gameObject.AddComponent<Text>();
            label.font = CatTheme.Font; label.fontSize = 20; DesktopLocalization.SetText(label, notice.Text);
            label.supportRichText = false; label.raycastTarget = false;
            label.color = error ? new Color(163f / 255, 57f / 255, 43f / 255) : CatTheme.Ink;
            label.alignment = TextAnchor.MiddleLeft; label.horizontalOverflow = HorizontalWrapMode.Wrap;
            var width = Mathf.Clamp(label.preferredWidth + 36, 50, 360);
            label.rectTransform.sizeDelta = new Vector2(width - 36, 1000);
            var height = Mathf.Max(44, label.preferredHeight + 20);
            label.rectTransform.sizeDelta = new Vector2(width - 36, height - 20);
            note.sizeDelta = new Vector2(width, height);
            var mascotWidth = error ? 84 : 120;
            root.sizeDelta = new Vector2(width + mascotWidth - 6, Mathf.Max(height, 84));
            note.anchorMin = note.anchorMax = new Vector2(0, .5f);
            note.anchoredPosition = new Vector2(width / 2, 0);
            mascot = Node(error ? "Cat" : "Fish", root, new Vector2(mascotWidth, error ? 84 : 76));
            mascot.anchorMin = mascot.anchorMax = new Vector2(1, .5f);
            mascot.pivot = new Vector2(error ? .5f : .8f, .5f);
            mascot.anchoredPosition = new Vector2(-mascotWidth * (1 - mascot.pivot.x), 0);
            var imageHost = mascot;
            if (error)
            {
                var backing = mascot.gameObject.AddComponent<CozyCssGraphic>();
                backing.shape = CozyCssGraphic.Shape.Paper; backing.radius = 26; backing.border = 0;
                backing.color = new Color(1, 243f / 255, 223f / 255); backing.raycastTarget = false;
                mascot.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                imageHost = Node("Portrait", mascot, new Vector2(84, 84));
            }
            var image = imageHost.gameObject.AddComponent<Image>(); image.raycastTarget = false;
            image.sprite = Resources.Load<Sprite>(error ? "Desktop/Theme/Cozy/characters/avatars/orange" : "Desktop/Theme/Cozy/items/fish/crucian");
            image.preserveAspect = true;
            if (!error && image.sprite != null)
                mascot.sizeDelta = new Vector2(120, 120 * image.sprite.rect.height / image.sprite.rect.width);
            Render();
        }
        static RectTransform Node(string name, Transform parent, Vector2 size)
        {
            var node = new GameObject(name, typeof(RectTransform)); node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform; rect.sizeDelta = size; return rect;
        }
        public void Advance(float delta)
        {
            if (CozyMotion.Paused || delta <= 0) return;
            Next(); if (root == null) return;
            elapsed += delta;
            if (elapsed >= Duration) { Remove(); Next(); return; }
            Render();
        }
        void Render()
        {
            if (root == null) return;
            var entrance = CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic, elapsed / .42f);
            var exit = CozyWebMotion.Evaluate(CozyWebMotion.Ease.InQuad, (elapsed - 2.92f) / .32f);
            alpha.alpha = Mathf.Min(entrance, 1 - exit);
            root.anchoredPosition = new Vector2(-40, -112) + (CozyMotion.Reduced ? Vector2.zero
                : new Vector2(460 * (1 - entrance) - 140 * exit, 8 * exit));
            mascot.localRotation = Quaternion.identity;
            if (CozyMotion.Reduced) return;
            var angle = error ? -14 * CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, (elapsed - .3f) / .3f)
                : Mathf.Lerp(5, -5, CozyWebMotion.Evaluate(CozyWebMotion.Ease.InOutSine, Mathf.PingPong(Mathf.Min(elapsed, 2.88f) / .24f, 1)));
            mascot.localRotation = Quaternion.Euler(0, 0, angle);
        }
        void Remove() { if (root == null) return; if (Application.isPlaying) Destroy(root.gameObject); else DestroyImmediate(root.gameObject); root = null; }
        public void Clear() { queue.Clear(); Remove(); }
        void Update() { Advance(Time.unscaledDeltaTime); }
        void OnEnable() { CozyMotion.ReducedChanged += Reduced; CozyMotion.PausedChanged += Paused; }
        void OnDisable() { CozyMotion.ReducedChanged -= Reduced; CozyMotion.PausedChanged -= Paused; Clear(); }
        void Reduced(bool value) { Render(); }
        void Paused(bool value) { if (root != null) root.gameObject.SetActive(!value); if (!value) Next(); }
    }
}
