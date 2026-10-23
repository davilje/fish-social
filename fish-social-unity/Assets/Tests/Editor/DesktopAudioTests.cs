#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using FishSocial.Desktop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class DesktopAudioTests
    {
        bool hadMusic, hadEffects;
        float oldMusic, oldEffects;
        GameObject root;
        [SetUp]
        public void SetUp()
        {
            hadMusic = PlayerPrefs.HasKey(DesktopAudioService.MusicPreference);
            hadEffects = PlayerPrefs.HasKey(DesktopAudioService.EffectsPreference);
            oldMusic = DesktopAudioService.MusicVolume; oldEffects = DesktopAudioService.EffectsVolume;
            PlayerPrefs.DeleteKey(DesktopAudioService.MusicPreference);
            PlayerPrefs.DeleteKey(DesktopAudioService.EffectsPreference);
        }
        [TearDown]
        public void TearDown()
        {
            if (root != null) UnityEngine.Object.DestroyImmediate(root);
            if (hadMusic) PlayerPrefs.SetFloat(DesktopAudioService.MusicPreference, oldMusic);
            else PlayerPrefs.DeleteKey(DesktopAudioService.MusicPreference);
            if (hadEffects) PlayerPrefs.SetFloat(DesktopAudioService.EffectsPreference, oldEffects);
            else PlayerPrefs.DeleteKey(DesktopAudioService.EffectsPreference);
            PlayerPrefs.Save();
        }
        [Test]
        public void MusicAndEffectsPersistIndependentlyAndCanBothBeMuted()
        {
            Assert.AreEqual(.35f, DesktopAudioService.MusicVolume);
            Assert.AreEqual(.7f, DesktopAudioService.EffectsVolume);
            DesktopAudioService.SetMusicVolume(.21f);
            DesktopAudioService.SetEffectsVolume(.82f);
            Assert.AreEqual(.21f, PlayerPrefs.GetFloat(DesktopAudioService.MusicPreference));
            Assert.AreEqual(.82f, PlayerPrefs.GetFloat(DesktopAudioService.EffectsPreference));
            DesktopAudioService.SetMusicVolume(0);
            Assert.AreEqual(.82f, DesktopAudioService.EffectsVolume);
            DesktopAudioService.SetEffectsVolume(0);
            Assert.AreEqual(0, DesktopAudioService.MusicVolume);
            Assert.AreEqual(0, DesktopAudioService.EffectsVolume);
        }
        [Test]
        public void InvalidSavedVolumesCannotPropagateNanOrOutOfRangeToMixer()
        {
            DesktopAudioService.SetMusicVolume(-2);
            DesktopAudioService.SetEffectsVolume(3);
            Assert.AreEqual(0, DesktopAudioService.MusicVolume);
            Assert.AreEqual(1, DesktopAudioService.EffectsVolume);
            DesktopAudioService.SetMusicVolume(float.NaN);
            DesktopAudioService.SetEffectsVolume(float.PositiveInfinity);
            Assert.AreEqual(DesktopAudioService.DefaultMusicVolume, DesktopAudioService.MusicVolume);
            Assert.AreEqual(DesktopAudioService.DefaultEffectsVolume, DesktopAudioService.EffectsVolume);
        }
        [Test]
        public void MusicLoopsAndEffectsAndCatVoicesUseIndependentNonSpatialSources()
        {
            root = new GameObject("AudioTest");
            var service = root.AddComponent<DesktopAudioService>();
            if (DesktopAudioService.Instance != service)
                typeof(DesktopAudioService).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(service, null);
            var sources = root.GetComponentsInChildren<AudioSource>();
            Assert.AreEqual(3, sources.Length);
            var music = sources.Single(source => source.name == "BackgroundMusic");
            Assert.IsTrue(music.loop);
            Assert.IsFalse(sources.Single(source => source.name == "CatVoice").loop);
            Assert.IsFalse(sources.Single(source => source.name == "InterfaceAndFishingSounds").loop);
            foreach (var source in sources) { Assert.AreEqual(0, source.spatialBlend); Assert.IsFalse(source.playOnAwake); }
            DesktopAudioService.SetMusicVolume(.12f); DesktopAudioService.SetEffectsVolume(.63f);
            Assert.AreEqual(.12f, music.volume);
            Assert.IsTrue(sources.Where(source => source != music).All(source => Mathf.Approximately(.63f, source.volume)));
        }
        [Test]
        public void SettingsRowsFitLegacyScrollContentAndRestoreSavedValuesWhenReopened()
        {
            root = new GameObject("SettingsContent", typeof(RectTransform));
            var content = (RectTransform)root.transform; content.sizeDelta = new Vector2(710, 556);
            DesktopAudioSettingsView.Ensure(content, 374);
            DesktopAudioSettingsView.Ensure(content, 374);
            Assert.AreEqual(1, root.GetComponentsInChildren<DesktopAudioSettingsView>().Length);
            var audio = (RectTransform)content.Find("AudioSettings");
            Assert.LessOrEqual(-audio.anchoredPosition.y + audio.rect.height, content.rect.height);
            var sliders = root.GetComponentsInChildren<Slider>(); Assert.AreEqual(2, sliders.Length);
            var music = sliders.Single(slider => slider.transform.parent.name == "MusicVolume");
            var effects = sliders.Single(slider => slider.transform.parent.name == "EffectsVolume");
            music.value = .25f; effects.value = .75f;
            Assert.AreEqual(.25f, DesktopAudioService.MusicVolume);
            Assert.AreEqual(.75f, DesktopAudioService.EffectsVolume);
            root.SetActive(false); DesktopAudioService.SetMusicVolume(.48f); root.SetActive(true);
            // EditMode does not dispatch ordinary MonoBehaviour enable callbacks.
            typeof(DesktopAudioSettingsView).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(root.GetComponentInChildren<DesktopAudioSettingsView>(), null);
            Assert.AreEqual(.48f, music.value);
            Assert.AreEqual(.75f, effects.value);
            foreach (var slider in sliders)
            {
                Assert.LessOrEqual(slider.fillRect.rect.height, 12.01f);
                Assert.LessOrEqual(slider.handleRect.rect.height, 32.01f);
                Assert.Less(slider.fillRect.rect.width, 674);
            }
        }
        [TestCase("Close", false, false, "ui_close")]
        [TestCase("Cancel", false, false, "ui_close")]
        [TestCase("Backdrop", false, false, "ui_close")]
        [TestCase("Notify0", true, false, "ui_toggle")]
        [TestCase("bag", false, true, "ui_open")]
        [TestCase("Buy", false, false, "ui_click")]
        public void InteractionUsesOneSemanticCue(string name, bool toggle, bool navigation, string expected)
        { Assert.AreEqual(expected, DesktopUiAudioFeedback.CueFor(name, toggle, navigation)); }

        [UnityTest]
        public IEnumerator AllBundledWavsDecodeWithTheRuntimePlaybackOptions()
        {
            var audioRoot = Path.Combine(Application.streamingAssetsPath, "Audio");
            var paths = Directory.GetFiles(audioRoot, "*.wav", SearchOption.AllDirectories);
            Assert.AreEqual(14, paths.Length);
            foreach (var path in paths)
            {
                var streaming = Path.GetFileName(path) == "pond_daylight.wav";
                using (var request = UnityWebRequestMultimedia.GetAudioClip(new Uri(path).AbsoluteUri, AudioType.WAV))
                {
                    ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio = streaming;
                    var operation = request.SendWebRequest();
                    var deadline = UnityEditor.EditorApplication.timeSinceStartup + 20;
                    while (!operation.isDone && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                    Assert.IsTrue(operation.isDone, path + " timed out");
                    Assert.AreEqual(UnityWebRequest.Result.Success, request.result, path + ": " + request.error);
                    var clip = DownloadHandlerAudioClip.GetContent(request);
                    Assert.IsNotNull(clip, path); Assert.Greater(clip.samples, 0, path);
                    Assert.AreEqual(44100, clip.frequency, path);
                    if (streaming) { Assert.AreEqual(2, clip.channels); Assert.AreEqual(80, clip.length, .01); }
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }
        }
    }
}
#endif
