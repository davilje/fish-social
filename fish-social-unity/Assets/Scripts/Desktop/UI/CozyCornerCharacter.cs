using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>Optional, explicitly mapped six-pose character. Unknown/default identities keep the approved original cat.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class CozyCornerCharacter : MonoBehaviour
    {
        public enum Pose { Rest, LookUp, Jump, Sleep, Error, Nod }
        [Serializable] sealed class Manifest { public Asset[] assets; }
        [Serializable] sealed class Asset { public string skin; public string pose; public string resourcesPath; }
        static readonly string[] PoseNames = { "rest", "look-up", "jump", "sleep", "error", "nod" };
        static Dictionary<string, Dictionary<string, string>> paths;
        readonly Sprite[] poses = new Sprite[6];
        Image image;
        RectTransform rect;
        Sprite original;
        Vector2 originalSize;
        Vector2 restPosition;
        Quaternion restRotation;
        float remaining;
        float elapsed;
        float idleElapsed;
        bool journalOpen;
        bool leaning;
        bool waking;
        float leanFrom;
        float leanTo;
        float leanElapsed = .22f;
        float leanAngle;
        Text zzz;
        public void SetPageOpen(bool open) { journalOpen = open; if (open) RestoreTransform(); }
        public void NotifyActivity()
        {
            Initialize();
            idleElapsed = 0;
            if (CurrentPose != Pose.Sleep) return;
            SetPose(Pose.Rest);
            waking = true;
            elapsed = 0;
        }
        public void Lean(bool on)
        {
            NotifyActivity();
            leaning = on;
            leanFrom = leanAngle;
            leanTo = on ? 5 : 0;
            leanElapsed = 0;
        }
        bool initialized;
        public Pose CurrentPose { get; private set; }
        public string SkinId { get; private set; }
        public bool HasMappedPoses => !string.IsNullOrEmpty(SkinId);

        void Initialize()
        {
            if (initialized) return;
            image = GetComponent<Image>();
            rect = image.rectTransform;
            CozyMotion.StopFloat(rect);
            original = Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/illustrations/cat-fishing")
                ?? Resources.Load<Sprite>("Desktop/Theme/Cozy/characters/poses/orange-ready") ?? image.sprite;
            originalSize = rect.sizeDelta;
            restPosition = rect.anchoredPosition;
            restRotation = rect.localRotation;
            initialized = true;
        }

        static void LoadManifest()
        {
            if (paths != null) return;
            paths = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
            var json = Resources.Load<TextAsset>("Desktop/Theme/Cozy/journal/character-manifest");
            var manifest = json != null ? JsonUtility.FromJson<Manifest>(json.text) : null;
            if (manifest?.assets == null) return;
            foreach (var asset in manifest.assets)
            {
                if (string.IsNullOrEmpty(asset.skin) || string.IsNullOrEmpty(asset.pose) || string.IsNullOrEmpty(asset.resourcesPath)) continue;
                if (!paths.TryGetValue(asset.skin, out var skin))
                    paths[asset.skin] = skin = new Dictionary<string, string>(StringComparer.Ordinal);
                skin[asset.pose] = asset.resourcesPath;
            }
        }

        public bool SetSkin(string explicitSkinId)
        {
            Initialize();
            if (HasMappedPoses && SkinId == explicitSkinId) return true;
            LoadManifest();
            SkinId = null;
            if (!string.IsNullOrEmpty(explicitSkinId) && paths.TryGetValue(explicitSkinId, out var skin))
            {
                var complete = true;
                for (var i = 0; i < PoseNames.Length; i++)
                {
                    poses[i] = skin.TryGetValue(PoseNames[i], out var path) ? Resources.Load<Sprite>(path) : null;
                    complete &= poses[i] != null;
                }
                if (complete) SkinId = explicitSkinId;
            }
            rect.sizeDelta = HasMappedPoses ? new Vector2(originalSize.x, originalSize.x * 328f / 384f) : originalSize;
            SetPose(CozyMotion.Paused ? Pose.Sleep : Pose.Rest);
            return HasMappedPoses;
        }

        public void SetPose(Pose pose, float holdSeconds = 0f)
        {
            Initialize();
            RestoreTransform();
            CurrentPose = pose;
            waking = false;
            elapsed = 0f;
            remaining = holdSeconds > 0 ? holdSeconds
                : pose == Pose.Jump ? .64f : pose == Pose.Nod ? .6f
                : pose == Pose.LookUp || pose == Pose.Error ? 1.2f : 0f;
            idleElapsed = 0f;
            image.sprite = HasMappedPoses ? poses[(int)pose] : original;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        public void Advance(float delta)
        {
            Initialize();
            if (delta <= 0 || CozyMotion.Paused || !isActiveAndEnabled) return;
            idleElapsed += delta;
            if (idleElapsed >= 30 && CurrentPose != Pose.Sleep) SetPose(Pose.Sleep);
            elapsed += delta;
            leanElapsed = Mathf.Min(.22f, leanElapsed + delta);
            leanAngle = Mathf.LerpUnclamped(leanFrom, leanTo,
                CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutBack, leanElapsed / .22f));
            var offset = Vector2.zero;
            var scale = Vector2.one;
            var rotation = leanAngle;
            if (!CozyMotion.Reduced)
            {
                if (CurrentPose == Pose.Sleep)
                {
                    var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.InOutSine, elapsed / .6f);
                    offset = new Vector2(6, -10) * t;
                    rotation = -7 * t;
                    RenderSleepLetters();
                }
                else if (waking)
                {
                    var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutCubic, elapsed / .32f);
                    offset.y = t < .4f ? Mathf.Lerp(0, 18, t / .4f) : Mathf.Lerp(18, 0, (t - .4f) / .6f);
                    if (elapsed >= .32f) waking = false;
                }
                else if (CurrentPose == Pose.Jump)
                {
                    var t = CozyWebMotion.Evaluate(CozyWebMotion.Ease.OutQuad, elapsed / .64f);
                    if (t < .12f) scale = Vector2.Lerp(Vector2.one, new Vector2(1.08f, .9f), t / .12f);
                    else if (t < .5f)
                    {
                        var p = (t - .12f) / .38f;
                        offset.y = 64 * p;
                        scale = Vector2.Lerp(new Vector2(1.08f, .9f), new Vector2(.94f, 1.08f), p);
                    }
                    else if (t < .85f)
                    {
                        var p = CozyWebMotion.Evaluate(CozyWebMotion.Ease.InQuad, (t - .5f) / .35f);
                        offset.y = 64 * (1 - p);
                        scale = Vector2.Lerp(new Vector2(.94f, 1.08f), new Vector2(1.1f, .88f), p);
                    }
                    else scale = Vector2.Lerp(new Vector2(1.1f, .88f), Vector2.one, (t - .85f) / .15f);
                }
                else if (CurrentPose == Pose.Nod)
                    rotation = CozyWebMotion.Frames(CozyWebMotion.Evaluate(CozyWebMotion.Ease.InOutSine, elapsed / .6f), 0, 6, 0, 6, 0);
                else if (CurrentPose == Pose.Rest && !journalOpen && CozyMotion.ActiveLoopOwner == null)
                {
                    var phase = Mathf.PingPong(elapsed / 1.5f, 1);
                    scale.y = Mathf.Lerp(1, 1.025f, CozyWebMotion.Evaluate(CozyWebMotion.Ease.InOutSine, phase));
                }
            }
            if (CurrentPose != Pose.Sleep || CozyMotion.Reduced)
                if (zzz != null) zzz.gameObject.SetActive(false);
            ApplyTransform(offset, rotation, scale);
            if (remaining > 0)
            {
                remaining -= delta;
                if (remaining <= 0) SetPose(Pose.Rest);
            }
        }

        void RenderSleepLetters()
        {
            if (CozyMotion.ActiveLoopOwner != null) { if (zzz != null) zzz.gameObject.SetActive(false); return; }
            if (zzz == null)
            {
                var go = new GameObject("SleepLetters", typeof(RectTransform), typeof(Text));
                go.transform.SetParent(transform, false);
                zzz = go.GetComponent<Text>();
                zzz.font = CatTheme.Font;
                zzz.fontSize = 26;
                DesktopLocalization.SetText(zzz, "Zzz");
                zzz.raycastTarget = false;
                zzz.rectTransform.anchorMin = zzz.rectTransform.anchorMax = new Vector2(0, 1);
                zzz.rectTransform.pivot = new Vector2(0, 1);
                zzz.rectTransform.sizeDelta = new Vector2(90, 42);
            }
            zzz.gameObject.SetActive(true);
            var t = Mathf.Repeat(elapsed / 2, 1);
            var a = t < .4f ? t / .4f : (1 - t) / .6f;
            var x = t < .4f ? Mathf.Lerp(0, 6, t / .4f) : Mathf.Lerp(6, 14, (t - .4f) / .6f);
            var y = t < .4f ? Mathf.Lerp(-10, 6, t / .4f) : Mathf.Lerp(6, 30, (t - .4f) / .6f);
            zzz.rectTransform.anchoredPosition = new Vector2(70 + x, 10 + y);
            zzz.color = new Color(117f / 255, 71f / 255, 46f / 255, .68f * a);
        }

        void ApplyTransform(Vector2 offset, float rotation, Vector2 scale)
        {
            if (CozyMotion.Reduced) { offset = Vector2.zero; rotation = 0; scale = Vector2.one; }
            var origin = new Vector3((.3f - rect.pivot.x) * rect.rect.width, -rect.pivot.y * rect.rect.height);
            var q = Quaternion.Euler(0, 0, rotation);
            rect.localScale = new Vector3(scale.x, scale.y, 1);
            rect.localRotation = restRotation * q;
            rect.anchoredPosition = restPosition + offset + (Vector2)(origin - q * Vector3.Scale(origin, rect.localScale));
        }

        void RestoreTransform()
        {
            if (rect == null) return;
            rect.anchoredPosition = restPosition;
            rect.localRotation = restRotation;
            rect.localScale = Vector3.one;
            if (zzz != null) zzz.gameObject.SetActive(false);
        }

        void OnEnable()
        {
            CozyMotion.ReducedChanged -= OnReduced;
            CozyMotion.PausedChanged -= OnPaused;
            CozyMotion.ReducedChanged += OnReduced;
            CozyMotion.PausedChanged += OnPaused;
        }
        void Start() { if (!initialized) { Initialize(); SetPose(CozyMotion.Paused ? Pose.Sleep : Pose.Rest); } }
        void Update() { if (Input.anyKeyDown) NotifyActivity(); Advance(Time.unscaledDeltaTime); }
        void OnDisable()
        {
            CozyMotion.ReducedChanged -= OnReduced;
            CozyMotion.PausedChanged -= OnPaused;
            RestoreTransform();
        }
        void OnReduced(bool reduced) { if (reduced) RestoreTransform(); }
        void OnPaused(bool paused) { SetPose(paused ? Pose.Sleep : Pose.Rest); }
    }
}
