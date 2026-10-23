using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>One audio owner for the main window and native pond overlay.</summary>
    public sealed class DesktopAudioService : MonoBehaviour
    {
        public const string MusicPreference = "FishSocial.Audio.MusicVolume";
        public const string EffectsPreference = "FishSocial.Audio.EffectsVolume";
        public const float DefaultMusicVolume = .35f;
        public const float DefaultEffectsVolume = .7f;
        static readonly string[] Effects = {
            "ui_click", "ui_open", "ui_close", "ui_success", "ui_error", "ui_toggle", "ui_tick",
            "cast", "splash", "catch", "meow_01", "meow_02", "meow_03"
        };
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>(StringComparer.Ordinal);
        AudioSource music, effects, voice;
        float scanAt, saveAt = -1, nextMeow;
        int lastMeow = -1;
        string fishingPhase;
        public static DesktopAudioService Instance { get; private set; }
        public static float MusicVolume => ReadVolume(MusicPreference, DefaultMusicVolume);
        public static float EffectsVolume => ReadVolume(EffectsPreference, DefaultEffectsVolume);

        public static float NormalizeVolume(float value, float fallback)
        { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value); }
        static float ReadVolume(string key, float fallback)
        { return NormalizeVolume(PlayerPrefs.GetFloat(key, fallback), fallback); }
        public static void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(MusicPreference, NormalizeVolume(value, DefaultMusicVolume));
            if (Instance != null) { Instance.music.volume = MusicVolume; Instance.SaveSoon(); }
            else PlayerPrefs.Save();
        }
        public static void SetEffectsVolume(float value)
        {
            PlayerPrefs.SetFloat(EffectsPreference, NormalizeVolume(value, DefaultEffectsVolume));
            if (Instance != null) {
                Instance.effects.volume = Instance.voice.volume = EffectsVolume;
                Instance.SaveSoon();
            } else PlayerPrefs.Save();
        }
        void SaveSoon() { saveAt = Time.realtimeSinceStartup + .5f; }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            music = Source("BackgroundMusic", MusicVolume);
            effects = Source("InterfaceAndFishingSounds", EffectsVolume);
            voice = Source("CatVoice", EffectsVolume);
            music.loop = true;
        }
        AudioSource Source(string label, float volume)
        {
            var node = new GameObject(label); node.transform.SetParent(transform, false);
            var source = node.AddComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0; source.volume = volume;
            source.ignoreListenerPause = true;
            return source;
        }
        void Start()
        {
            // The desktop scene supplies its camera listener. Preview scenes may not.
            if (FindObjectOfType<AudioListener>() == null) gameObject.AddComponent<AudioListener>();
            StartCoroutine(LoadMusic());
            StartCoroutine(LoadEffects());
            BindControls();
        }
        IEnumerator LoadMusic()
        {
            yield return Load("music/pond_daylight.wav", "music", true);
            if (!clips.TryGetValue("music", out var clip)) yield break;
            music.clip = clip; music.Play();
        }
        IEnumerator LoadEffects()
        {
            foreach (var cue in Effects) yield return Load("sfx/" + cue + ".wav", cue, false);
        }
        IEnumerator Load(string relativePath, string key, bool streaming)
        {
            var path = Path.Combine(Application.streamingAssetsPath, "Audio", relativePath);
            var url = new Uri(Path.GetFullPath(path)).AbsoluteUri;
            using (var request = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
            {
                ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = streaming;
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success) {
                    Debug.LogWarning("[DesktopAudio] Could not load " + relativePath + ": " + request.error);
                    yield break;
                }
                var clip = DownloadHandlerAudioClip.GetContent(request);
                if (clip != null) { clip.name = key; clips[key] = clip; }
            }
        }
        public static void Play(string cue) { Instance?.PlayCue(cue); }
        void PlayCue(string cue)
        {
            if (cue == "meow") { Meow(); return; }
            if (string.IsNullOrEmpty(cue) || EffectsVolume <= 0 || !clips.TryGetValue(cue, out var clip)) return;
            var now = Time.realtimeSinceStartup;
            var cooldown = cue.StartsWith("ui_", StringComparison.Ordinal) ? .055f : .25f;
            if (lastPlayed.TryGetValue(cue, out var previous) && now - previous < cooldown) return;
            lastPlayed[cue] = now;
            effects.PlayOneShot(clip);
        }
        public static void PlayMeow() { Instance?.Meow(); }
        void Meow()
        {
            if (EffectsVolume <= 0 || Time.realtimeSinceStartup < nextMeow) return;
            var index = (lastMeow + UnityEngine.Random.Range(1, 3)) % 3;
            if (!clips.TryGetValue("meow_0" + (index + 1), out var clip)) return;
            nextMeow = Time.realtimeSinceStartup + 2.5f;
            lastMeow = index; voice.clip = clip; voice.Play();
        }
        public void ObserveFishingPhase(string phase)
        {
            if (phase == fishingPhase) return;
            var previous = fishingPhase; fishingPhase = phase;
            // A snapshot received after joining a pond must not replay old actions.
            if (previous == null) return;
            if (phase == "casting") PlayCue("cast");
            else if (phase == "waiting" && previous == "casting") PlayCue("splash");
        }
        public static void CelebrateCatch() { Play("catch"); PlayMeow(); }
        void BindControls()
        {
            foreach (var selectable in FindObjectsOfType<Selectable>(true))
                if (selectable is Button || selectable is Toggle) DesktopUiAudioFeedback.Ensure(selectable.gameObject);
            foreach (var cat in FindObjectsOfType<CozyCornerCharacter>(true)) DesktopCatAudioFeedback.Bind(cat.transform);
            foreach (var cat in FindObjectsOfType<Pet.SpriteFramePetRenderer>(true)) DesktopCatAudioFeedback.Bind(cat.transform);
        }
        void Update()
        {
            var now = Time.realtimeSinceStartup;
            if (now >= scanAt) { scanAt = now + 1; BindControls(); }
            if (saveAt >= 0 && now >= saveAt) { saveAt = -1; PlayerPrefs.Save(); }
        }
        void OnApplicationQuit() { PlayerPrefs.Save(); }
        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (saveAt >= 0) PlayerPrefs.Save();
            foreach (var clip in clips.Values) if (clip != null) Destroy(clip);
        }
    }
}
