#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using FishSocial.Desktop;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class CozyToggleMotionTests
    {
        GameObject root;
        bool previousReduced,previousPaused;
        [SetUp] public void Setup()
        { previousReduced=CozyMotion.Reduced;previousPaused=CozyMotion.Paused;CozyMotion.SetReduced(false);CozyMotion.SetPaused(false); }
        [TearDown] public void Cleanup()
        { if(root!=null)Object.DestroyImmediate(root);CozyMotion.SetReduced(previousReduced);CozyMotion.SetPaused(previousPaused); }

        // Chromium WAAPI samples of app.js toggleSwitch; the original awaits the knob,
        // then cancels the still-running paw animation at 320 ms.
        [TestCase(.04f,0,-6,.5f)]
        [TestCase(.08f,0,0,1)]
        [TestCase(.10f,0,8.60153f,1)]
        [TestCase(.16f,30.972f,28.0423f,1)]
        [TestCase(.22f,39.4718f,28.434f,.602429f)]
        [TestCase(.30f,36.3804f,21.4968f,.106916f)]
        public void OnFramesMatchBrowserPawAndDelayedKnob(float seconds,float knob,float paw,float alpha)
        {
            var pose=CozyToggleMotion.Sample(true,seconds);
            Assert.AreEqual(knob,pose.KnobX,.003f);
            Assert.AreEqual(paw,pose.PawX,.003f);
            Assert.AreEqual(alpha,pose.PawAlpha,.0001f);
        }

        [Test]
        public void KnobCompletionCancelsPawBeforeItsOwnAnimationWouldFinish()
        {
            Assert.Greater(CozyToggleMotion.Sample(true,.319f).PawAlpha,0);
            var pose=CozyToggleMotion.Sample(true,.32f);
            Assert.AreEqual(36,pose.KnobX,.001f);Assert.AreEqual(0,pose.PawAlpha);
            Assert.AreEqual(1,pose.TrackOn);
        }

        [Test]
        public void OffHasNoPawAndUses220msOutCubic()
        {
            var start=CozyToggleMotion.Sample(false,0);var half=CozyToggleMotion.Sample(false,.11f);var end=CozyToggleMotion.Sample(false,.22f);
            Assert.AreEqual(36,start.KnobX);Assert.AreEqual(4.6008f,half.KnobX,.002f);
            Assert.AreEqual(0,end.KnobX);Assert.AreEqual(0,half.PawAlpha);Assert.AreEqual(0,end.TrackOn);
        }

        CozyJournalToggle CreateToggle(out Toggle toggle,out RectTransform knob,out Image paw)
        {
            root=new GameObject("Toggle",typeof(RectTransform),typeof(Toggle));toggle=root.GetComponent<Toggle>();
            toggle.SetIsOnWithoutNotify(false);
            var track=new GameObject("JournalTrack",typeof(RectTransform),typeof(Image));track.transform.SetParent(root.transform,false);
            var knobObject=new GameObject("JournalKnob",typeof(RectTransform));knobObject.transform.SetParent(track.transform,false);knob=(RectTransform)knobObject.transform;
            var pawObject=new GameObject("JournalPaw",typeof(RectTransform),typeof(Image));pawObject.transform.SetParent(track.transform,false);paw=pawObject.GetComponent<Image>();
            var visual=root.AddComponent<CozyJournalToggle>();visual.Configure(toggle,track.GetComponent<Image>(),knob,paw);
            visual.Configure(toggle,track.GetComponent<Image>(),knob,paw);return visual;
        }

        [Test]
        public void SettingsCommitOnceImmediatelyWhileVisualContinuesIndependently()
        {
            var visual=CreateToggle(out var toggle,out var knob,out var paw);var calls=0;
            toggle.onValueChanged.AddListener(value=>calls++);
            toggle.isOn=true;
            Assert.AreEqual(1,calls);Assert.IsTrue(toggle.isOn);Assert.AreEqual(0,visual.CurrentPose.KnobX);
            visual.Advance(.16f);Assert.AreEqual(30.972f,visual.CurrentPose.KnobX,.003f);Assert.IsTrue(paw.enabled);
            Assert.AreEqual(-6+visual.CurrentPose.KnobX,knob.anchoredPosition.x,.001f);
            visual.Advance(.16f);Assert.AreEqual(1,calls);Assert.IsFalse(paw.enabled);Assert.AreEqual(30,knob.anchoredPosition.x,.001f);
            toggle.SetIsOnWithoutNotify(false);visual.Advance(.22f);
            Assert.AreEqual(1,calls);Assert.AreEqual(0,visual.CurrentPose.KnobX);Assert.IsFalse(paw.enabled);
        }

        [Test]
        public void ReducedMotionSettlesWithoutDelayingOrRepeatingTheSetting()
        {
            var visual=CreateToggle(out var toggle,out var knob,out var paw);var calls=0;
            toggle.onValueChanged.AddListener(value=>calls++);toggle.isOn=true;visual.Advance(.04f);
            CozyMotion.SetReduced(true);
            Assert.AreEqual(1,calls);Assert.AreEqual(36,visual.CurrentPose.KnobX);Assert.IsFalse(paw.enabled);
        }
    }
}
#endif
