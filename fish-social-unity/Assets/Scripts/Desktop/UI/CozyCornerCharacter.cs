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
        int idleAction;
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
            original = Resources.Load<Sprite>("Desktop/Theme/Cozy/characters/poses/orange-ready") ?? image.sprite;
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
            elapsed = 0f;
            remaining = holdSeconds > 0 ? holdSeconds
                : pose == Pose.Jump ? .9f : pose == Pose.Nod ? .7f
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
            if (CurrentPose == Pose.Rest)
            {
                if (!HasMappedPoses || CozyMotion.Reduced) return;
                idleElapsed += delta;
                // Occasional poses, never another continuous floating owner.
                if (idleElapsed >= 15f)
                    SetPose((idleAction++ & 1) == 0 ? Pose.LookUp : Pose.Nod);
                return;
            }
            if (remaining <= 0f) return;
            elapsed += delta;
            remaining -= delta;
            if (!CozyMotion.Reduced && HasMappedPoses)
            {
                if (CurrentPose == Pose.Jump)
                    rect.anchoredPosition = restPosition + Vector2.up * (Mathf.Sin(Mathf.Clamp01(elapsed / CozyMotion.Hop) * Mathf.PI) * 14f);
                else if (CurrentPose == Pose.Nod)
                    rect.localRotation = restRotation * Quaternion.Euler(0, 0, Mathf.Sin(Mathf.Clamp01(elapsed / .7f) * Mathf.PI * 2f) * 3f);
            }
            if (remaining <= 0f) SetPose(Pose.Rest);
        }

        void RestoreTransform()
        {
            if (rect == null) return;
            rect.anchoredPosition = restPosition;
            rect.localRotation = restRotation;
        }

        void OnEnable()
        {
            CozyMotion.ReducedChanged -= OnReduced;
            CozyMotion.PausedChanged -= OnPaused;
            CozyMotion.ReducedChanged += OnReduced;
            CozyMotion.PausedChanged += OnPaused;
        }
        void Start() { if (!initialized) { Initialize(); SetPose(CozyMotion.Paused ? Pose.Sleep : Pose.Rest); } }
        void Update() { Advance(Time.unscaledDeltaTime); }
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
