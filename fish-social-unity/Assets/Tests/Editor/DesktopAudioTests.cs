#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FishSocial.Desktop;
using FishSocial.Desktop.Auth;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FishSocial.Tests.Editor
{
    public sealed class DesktopAudioTests
    {
        bool hadMusic, hadEffects, hadBackground;
        float oldMusic, oldEffects;
        int oldBackground;
        GameObject root;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Invoke(object target,string method,params object[] args) => target.GetType().GetMethod(method,Private).Invoke(target,args);
        static object Field(object target,string name) => target.GetType().GetField(name,Private).GetValue(target);
        [SetUp] public void SetUp()
        {
            hadMusic=PlayerPrefs.HasKey(DesktopAudioService.MusicPreference);hadEffects=PlayerPrefs.HasKey(DesktopAudioService.EffectsPreference);
            hadBackground=PlayerPrefs.HasKey(DesktopAudioService.BackgroundBitePreference);
            oldMusic=DesktopAudioService.MusicVolume;oldEffects=DesktopAudioService.EffectsVolume;
            oldBackground=PlayerPrefs.GetInt(DesktopAudioService.BackgroundBitePreference,1);
            PlayerPrefs.DeleteKey(DesktopAudioService.MusicPreference);PlayerPrefs.DeleteKey(DesktopAudioService.EffectsPreference);
            PlayerPrefs.DeleteKey(DesktopAudioService.BackgroundBitePreference);
        }
        [TearDown] public void TearDown()
        {
            if(root!=null)UnityEngine.Object.DestroyImmediate(root);
            if(hadMusic)PlayerPrefs.SetFloat(DesktopAudioService.MusicPreference,oldMusic);else PlayerPrefs.DeleteKey(DesktopAudioService.MusicPreference);
            if(hadEffects)PlayerPrefs.SetFloat(DesktopAudioService.EffectsPreference,oldEffects);else PlayerPrefs.DeleteKey(DesktopAudioService.EffectsPreference);
            if(hadBackground)PlayerPrefs.SetInt(DesktopAudioService.BackgroundBitePreference,oldBackground);else PlayerPrefs.DeleteKey(DesktopAudioService.BackgroundBitePreference);
            BackgroundRenderGate.SetAudioWork(false);BackgroundRenderGate.SetHidden(false);
            PlayerPrefs.Save();
        }
        DesktopAudioService Service()
        {
            root=new GameObject("AudioTest");var service=root.AddComponent<DesktopAudioService>();
            if(DesktopAudioService.Instance!=service)Invoke(service,"Awake");return service;
        }
        static AudioPresentationDto Context(string kind="bite",string id="fish-A") => new AudioPresentationDto{
            enabled=true,presentationSessionId="session",actorId="own",pondId="pond",spotId="spot",owner="overlay",ownerEpoch=3,
            actionId="action-A",eventId=id,eventKind=kind,source="authoritative",eventDeadlineMs=10000,waterSizeTier="M"};
        static NativeOverlayCommandDto Marker(AudioPresentationDto c,string marker,int occurrence=0) => new NativeOverlayCommandDto{
            protocolVersion=2,presentationSessionId=c.presentationSessionId,actorId=c.actorId,pondId=c.pondId,spotId=c.spotId,
            ownerEpoch=c.ownerEpoch,actionId=c.actionId,eventId=c.eventId,marker=marker,occurrence=occurrence,actorGeneration=1,
            source=c.source,waterSizeTier=c.waterSizeTier};
        static DesktopAudioPolicy Policy(AudioPresentationDto c) {var p=new DesktopAudioPolicy(DesktopAudioService.DefaultConfiguration());p.Reset(c);return p;}
        [Test] public void IndependentPersistedVolumesAndDefaultOnBackgroundBite()
        {
            Assert.AreEqual(.35f,DesktopAudioService.MusicVolume);Assert.AreEqual(.7f,DesktopAudioService.EffectsVolume);
            Assert.IsTrue(DesktopAudioService.BackgroundBiteEnabled);
            DesktopAudioService.SetMusicVolume(.21f);DesktopAudioService.SetEffectsVolume(.82f);DesktopAudioService.SetBackgroundBite(false);
            Assert.AreEqual(.21f,PlayerPrefs.GetFloat(DesktopAudioService.MusicPreference));Assert.AreEqual(.82f,DesktopAudioService.EffectsVolume);
            Assert.IsFalse(DesktopAudioService.BackgroundBiteEnabled);
            DesktopAudioService.SetMusicVolume(0);Assert.AreEqual(.82f,DesktopAudioService.EffectsVolume);
            DesktopAudioService.SetEffectsVolume(0);Assert.AreEqual(0,DesktopAudioService.MusicVolume);Assert.AreEqual(0,DesktopAudioService.EffectsVolume);
        }
        [Test] public void InvalidSavedVolumesCannotReachMixer()
        {
            DesktopAudioService.SetMusicVolume(-2);DesktopAudioService.SetEffectsVolume(3);
            Assert.AreEqual(0,DesktopAudioService.MusicVolume);Assert.AreEqual(1,DesktopAudioService.EffectsVolume);
            DesktopAudioService.SetMusicVolume(float.NaN);DesktopAudioService.SetEffectsVolume(float.PositiveInfinity);
            Assert.AreEqual(.35f,DesktopAudioService.MusicVolume);Assert.AreEqual(.7f,DesktopAudioService.EffectsVolume);
        }
        [Test] public void ExactlyFiveIndependentNonSpatialSourcesAndMusicLoops()
        {
            var service=Service();var sources=root.GetComponentsInChildren<AudioSource>();Assert.AreEqual(5,sources.Length);
            Assert.AreEqual(1,sources.Count(s=>s.loop));Assert.IsTrue(sources.Single(s=>s.name=="BackgroundMusic").loop);
            CollectionAssert.AreEquivalent(new[]{"BackgroundMusic","InterfaceSounds","FishingSoundsA","FishingSoundsB","CatVoice"},sources.Select(s=>s.name));
            foreach(var source in sources){Assert.AreEqual(0,source.spatialBlend);Assert.IsFalse(source.playOnAwake);Assert.IsTrue(source.ignoreListenerPause);}
            DesktopAudioService.SetMusicVolume(.12f);Assert.AreEqual(.12f,sources.Single(s=>s.loop).volume);
            Assert.AreEqual(0,service.ActiveShortVoices);
        }
        [Test] public void SettingsFitLegacyContentAndRefreshWithoutWritingPreferences()
        {
            root=new GameObject("SettingsContent",typeof(RectTransform));var content=(RectTransform)root.transform;content.sizeDelta=new Vector2(710,556);
            DesktopAudioSettingsView.Ensure(content,374);DesktopAudioSettingsView.Ensure(content,374);
            Assert.AreEqual(1,root.GetComponentsInChildren<DesktopAudioSettingsView>().Length);
            var audio=(RectTransform)content.Find("AudioSettings");Assert.LessOrEqual(-audio.anchoredPosition.y+audio.rect.height,content.rect.height);
            var music=root.GetComponentsInChildren<Slider>().Single(s=>s.transform.parent.name=="MusicVolume");
            var effects=root.GetComponentsInChildren<Slider>().Single(s=>s.transform.parent.name=="EffectsVolume");
            var background=root.GetComponentInChildren<Toggle>();Assert.IsTrue(background.isOn);
            music.value=.25f;effects.value=.75f;background.isOn=false;
            Assert.AreEqual(.25f,DesktopAudioService.MusicVolume);Assert.AreEqual(.75f,DesktopAudioService.EffectsVolume);Assert.IsFalse(DesktopAudioService.BackgroundBiteEnabled);
            DesktopAudioService.SetMusicVolume(.48f);DesktopAudioService.SetBackgroundBite(true);
            Invoke(root.GetComponentInChildren<DesktopAudioSettingsView>(),"OnEnable");Assert.AreEqual(.48f,music.value);Assert.IsTrue(background.isOn);
            foreach(RectTransform row in audio)Assert.LessOrEqual(-row.anchoredPosition.y+row.rect.height,audio.rect.height);
        }
        [TestCase("Close",false,false,"ui_close")][TestCase("Notify0",true,false,"ui_toggle")]
        [TestCase("bag",false,true,"ui_open")][TestCase("Buy",false,false,"ui_click")]
        public void SemanticInteraction(string name,bool toggle,bool navigation,string expected) => Assert.AreEqual(expected,DesktopUiAudioFeedback.CueFor(name,toggle,navigation));
        [Test] public void UiAllowsOnlySemanticCuesAndConsumesMutedOperation()
        {
            var p=Policy(Context());Assert.IsFalse(p.TryUi("catch","evil",0,true));Assert.IsFalse(p.TryUi("../sfx/catch.wav","evil",1,true));
            Assert.IsFalse(p.TryUi("ui_success","buy-A",2,false));Assert.IsFalse(p.TryUi("ui_success","buy-A",3,true));
            Assert.IsTrue(p.TryUi("ui_click",null,4,true));Assert.IsFalse(p.TryUi("ui_click",null,4.05,true));
            Assert.IsTrue(p.TryUi("ui_tick",null,4.06,true));Assert.IsFalse(p.TryUi("ui_tick",null,4.20,true));
            Assert.IsTrue(p.TryUi("ui_tick",null,4.25,true));
        }
        [Test] public void SnapshotPredictionAndStaleOrForeignMarkersCannotPlayAuthority()
        {
            var c=Context();var p=Policy(c);var m=Marker(c,"bite.onset");string cue;
            c.source="snapshot";Assert.IsFalse(p.TryMarker(m,"overlay","hooked",0,100,true,out cue));
            c.source="prediction";m.source="prediction";Assert.IsFalse(p.TryMarker(m,"overlay","hooked",0,100,true,out cue));
            c.source=m.source="authoritative";m.elapsedMs=251;Assert.IsFalse(p.TryMarker(m,"overlay","hooked",0,100,true,out cue));
            m.elapsedMs=0;m.actorId="other";Assert.IsFalse(p.TryMarker(m,"overlay","hooked",0,100,true,out cue));m.actorId=c.actorId;
            m.ownerEpoch--;Assert.IsFalse(p.TryMarker(m,"overlay","hooked",0,100,true,out cue));m.ownerEpoch=c.ownerEpoch;
            Assert.IsFalse(p.TryMarker(m,"unity","hooked",0,100,true,out cue));
            Assert.IsFalse(p.TryMarker(m,"overlay","hooked",0,10001,true,out cue));
            Assert.IsTrue(p.TryMarker(m,"overlay","hooked",0,100,true,out cue));Assert.AreEqual("bite",cue);
            Assert.IsFalse(p.TryMarker(m,"overlay","hooked",1,100,true,out cue));
        }
        [Test] public void DistinctAdjacentResultsSurviveNoGlobalCooldownAndDuplicatesDoNot()
        {
            var c=Context("catch");var p=Policy(c);string cue;
            Assert.IsTrue(p.TryMarker(Marker(c,"result.presented"),"overlay","seated",0,100,true,out cue));
            Assert.IsFalse(p.TryMarker(Marker(c,"result.presented",5),"overlay","seated",.001,100,true,out cue));
            c.eventId="fish-B";p.SetContext(c);Assert.IsTrue(p.TryMarker(Marker(c,"result.presented"),"overlay","seated",.002,100,true,out cue));
            Assert.IsFalse(p.TryMarker(Marker(c,"escape.contact"),"overlay","resolving",.003,100,true,out cue));
        }
        [Test] public void ActiveAuthorityConsumptionPinnedDespiteBoundedHistoryEviction()
        {
            var c=Context("catch");var p=Policy(c);string cue;
            Assert.IsTrue(p.TryMarker(Marker(c,"result.presented"),"overlay","seated",0,100,true,out cue));
            for(var i=0;i<1100;i++)p.TryUi("ui_success","operation-"+i,i+1,true);
            Assert.LessOrEqual(p.ConsumedCount,512);
            Assert.IsFalse(p.TryMarker(Marker(c,"result.presented"),"overlay","seated",1200,100,true,out cue));
        }
        [Test] public void HiddenBiteOffAndMutedConsumeAndNeverReplayOnRestore()
        {
            var c=Context();var p=Policy(c);Assert.IsFalse(p.ConsumeHiddenBite(c.eventId,100,false));
            Assert.IsFalse(p.ConsumeHiddenBite(c.eventId,100,true));string cue;
            Assert.IsFalse(p.TryMarker(Marker(c,"bite.onset"),"overlay","hooked",1,100,true,out cue));
            c.eventId="new-bite";p.SetContext(c);Assert.IsTrue(p.ConsumeHiddenBite(c.eventId,100,true));
            c.eventId="expired";c.eventDeadlineMs=90;p.SetContext(c);Assert.IsFalse(p.ConsumeHiddenBite(c.eventId,100,true));
        }
        [Test] public void WaterSharedBudgetStrongSupersedesWeakAndGroundbaitEveryThree()
        {
            var c=Context();var p=Policy(c);string cue;
            Assert.IsTrue(p.TryMarker(Marker(c,"fish.surface",0),"overlay","hooked",0,100,true,out cue));
            Assert.IsTrue(p.TryMarker(Marker(c,"fish.slap",0),"overlay","hooked",.1,100,true,out cue));
            Assert.IsFalse(p.TryMarker(Marker(c,"fish.surface",1),"overlay","hooked",1,100,true,out cue));
            Assert.IsFalse(p.TryMarker(Marker(c,"fish.slap",1),"overlay","hooked",.2,100,true,out cue));
            Assert.IsTrue(p.TryMarker(Marker(c,"fish.surface",3),"overlay","hooked",1.1,100,true,out cue));
            Assert.IsTrue(p.TryMarker(Marker(c,"groundbait.contact",0),"overlay","groundbaiting",3,100,true,out cue));
            Assert.IsFalse(p.TryMarker(Marker(c,"groundbait.contact",3),"overlay","groundbaiting",5,100,true,out cue));
            Assert.IsTrue(p.TryMarker(Marker(c,"groundbait.contact",6),"overlay","groundbaiting",5.17,100,true,out cue));
        }
        [Test] public void VoiceCooldownAndDuckingUseGlobalMinimumLatestEnd()
        {
            var p=Policy(Context());Assert.IsTrue(p.TryVoice(0,false,true));Assert.IsFalse(p.TryVoice(2.4,true,true));
            Assert.IsTrue(p.TryVoice(2.5,true,true));Assert.IsFalse(p.TryVoice(10,true,true));Assert.IsTrue(p.TryVoice(14.6,true,true));
            p.Duck(.5f,.7,0);p.Duck(.7f,.7,.3);Assert.AreEqual(.5f,p.DuckTarget(.8));Assert.AreEqual(1,p.DuckTarget(1.01));
            p.Duck(.7f,.2,1.1);Assert.AreEqual(.7f,p.DuckTarget(1.2));
            Assert.AreEqual(.35f,DesktopAudioService.MusicVolume);
        }
        [Test] public void PoolRejectsLowPriorityAndReplacesOldestCriticalWithoutGrowing()
        {
            var voices=new[]{new AudioVoiceCandidate{group="ui",active=true,priority=1,started=0},
                new AudioVoiceCandidate{group="fishing",active=true,priority=3,started=1,cue="bite_s",identity="bite"},
                new AudioVoiceCandidate{group="fishing",active=true,priority=2,started=2,cue="catch",identity="old"},
                new AudioVoiceCandidate{group="voice",active=true,priority=1,started=0}};
            Assert.AreEqual(-1,DesktopAudioPolicy.ChooseVoice(voices,"fishing","groundbait",0,"new"));
            Assert.AreEqual(2,DesktopAudioPolicy.ChooseVoice(voices,"fishing","catch",2,"new"));
            voices[1].priority=2;voices[1].started=0;Assert.AreEqual(1,DesktopAudioPolicy.ChooseVoice(voices,"fishing","catch",2,"new"));
            voices[2].priority=1;voices[2].cue="water_struggle_m";voices[2].identity="same";
            Assert.AreEqual(2,DesktopAudioPolicy.ChooseVoice(voices,"fishing","water_slap_m",1,"same"));Assert.AreEqual(4,voices.Length);
        }
        [Test] public void VariantsNeverRepeatAdjacentAndPathsStayInAudioBank()
        {
            var p=Policy(Context());var prior=-1;for(var i=0;i<30;i++){var n=p.Variant("meow","event"+i,3);Assert.AreNotEqual(prior,n);prior=n;}
            foreach(var path in new[]{"/tmp/a.wav","../a.wav","C:/a.wav","sfx/a.mp3"})Assert.IsFalse(DesktopAudioService.SafeAudioPath(path));
            Assert.IsTrue(DesktopAudioService.SafeAudioPath("water/bite_s_01.wav"));
        }
        [TestCase(.29f,"S")][TestCase(.3f,"M")][TestCase(.8f,"L")][TestCase(1.5f,"XL")][TestCase(float.NaN,"S")]
        public void WaterTierBoundaries(float size,string tier) => Assert.AreEqual(tier,DesktopAudioPolicy.Tier(size));
        [Test] public void PeriodicAndReconnectSnapshotNeverArmNewBiteAndSharedIdentityIsCanonical()
        {
            var s=Service();var user=new PondUserDto{id="own",spotId="spot",fishingPhase="waiting",phaseContext=new PondUserPhaseContextDto()};
            var shared=new FishingPresentationEnvelope{presentationSessionId="canonical",actorId="own",pondId="pond",spotId="spot",owner="overlay",ownerEpoch=8,actionId="canonical-action",source="snapshot"};
            s.UpdateContext("pond",user,true,"overlay",true,true,null,shared);
            user.fishingPhase="hooked";user.phaseContext.catchId="snapshot-fish";user.phaseEndsAt=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+5000;
            s.UpdateContext("pond",user,true,"overlay",true,true,null,shared);s.UpdateContext("pond",user,true,"overlay",true,false,null,shared);
            Assert.AreEqual("snapshot",s.Presentation.source);Assert.AreEqual("",s.Presentation.eventKind);
            Assert.AreEqual("canonical",s.Presentation.presentationSessionId);Assert.AreEqual("canonical-action",s.Presentation.actionId);Assert.AreEqual(8,s.Presentation.ownerEpoch);
            s.UpdateContext("pond",user,false,"overlay",true);s.UpdateContext("pond",user,true,"overlay",true,true,null,shared);
            Assert.AreEqual("",s.Presentation.eventKind);Assert.AreEqual(0,s.ActiveShortVoices);
        }
        [Test] public void MissingConfirmationClipConsumesResultWithoutAutomaticMeow()
        {
            var s=Service();var user=new PondUserDto{id="own",spotId="spot",fishingPhase="seated"};
            s.UpdateContext("pond",user,true,"unity",false,true);s.ArmEvent("catch","result",.5f,0);
            LogAssert.Expect(LogType.Warning,"[DesktopAudio] sfx/catch.wav: audio unavailable; event consumed");
            s.UnityMarker("result.presented",0);Assert.AreEqual(-1d,Field(s,"automaticVoiceAt"));
            s.UnityMarker("result.presented",0);Assert.AreEqual(0,s.ActiveShortVoices);
        }
        void AddClip(DesktopAudioService service,string path,float seconds=1)
        {
            var clips=(Dictionary<string,AudioClip>)Field(service,"clips");
            clips[path]=AudioClip.Create(path,(int)(44100*seconds),1,44100,false);
        }
        [Test] public void VisibleTierIsValidatedAndHiddenFutureSizeNeverLeaks()
        {
            Assert.AreEqual("S",DesktopAudioPolicy.VisibleTier(null));Assert.AreEqual("S",DesktopAudioPolicy.VisibleTier("xxl"));
            Assert.AreEqual("XL",DesktopAudioPolicy.VisibleTier("XL"));
            var s=Service();var user=new PondUserDto{id="own",spotId="spot",fishingPhase="hooked"};
            s.UpdateContext("pond",user,true,"overlay",false,true);s.ArmEvent("bite","tier-fish",10,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+5000);
            Assert.AreEqual("S",s.Presentation.waterSizeTier);AddClip(s,"water/bite_m_01.wav");AddClip(s,"water/bite_m_02.wav");
            var marker=Marker(s.Presentation,"bite.onset");marker.waterSizeTier="M";s.HandleMarker(marker);
            Assert.IsTrue(root.GetComponentsInChildren<AudioSource>().Any(source=>source.clip!=null&&source.clip.name.Contains("bite_m")));
        }
        [Test] public void MutingStopsExistingVoicesAndPendingConfirmationWithoutReplay()
        {
            var s=Service();AddClip(s,"sfx/catch.wav");var user=new PondUserDto{id="own",spotId="spot",fishingPhase="seated"};
            s.UpdateContext("pond",user,true,"unity",false,true);s.ArmEvent("catch","paper-A",.5f,0);s.UnityMarker("result.presented",0);
            Assert.Greater((double)Field(s,"automaticVoiceAt"),Time.realtimeSinceStartupAsDouble+.9);
            DesktopAudioService.SetEffectsVolume(0);Assert.AreEqual(-1d,Field(s,"automaticVoiceAt"));Assert.AreEqual(0,s.ActiveShortVoices);
            DesktopAudioService.SetEffectsVolume(1);s.UnityMarker("result.presented",0);Assert.AreEqual(-1d,Field(s,"automaticVoiceAt"));
            s.ArmEvent("bite","new-bite",.5f,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+5000);Assert.AreEqual(-1d,Field(s,"automaticVoiceAt"));
        }
        [Test] public void HiddenWaitingRestoresFiveFpsButCriticalAndTransitionLeaseAreBounded()
        {
            var s=Service();var user=new PondUserDto{id="own",spotId="spot",fishingPhase="waiting"};
            BackgroundRenderGate.SetOverlayActive(false);BackgroundRenderGate.SetHidden(true);
            s.UpdateContext("pond",user,true,"unity",true,true);Invoke(s,"Update");Assert.AreEqual(5,Application.targetFrameRate);
            user.fishingPhase="hooked";s.UpdateContext("pond",user,true,"unity",true);
            DesktopAudioService.SetEffectsVolume(0);
            s.ArmEvent("bite","muted-critical",.5f,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()+5000);
            Invoke(s,"Update");Assert.AreEqual(60,Application.targetFrameRate);
            s.Presentation.eventDeadlineMs=1;Invoke(s,"Update");Assert.AreEqual(5,Application.targetFrameRate);
        }
        [UnityTest] public IEnumerator ActualSourceReplacementUsesAnchoredFortyMsFadeAndBoundedPool()
        {
            var s=Service();AddClip(s,"sfx/catch.wav");AddClip(s,"water/bite_s_01.wav");AddClip(s,"water/bite_s_02.wav");
            var user=new PondUserDto{id="own",spotId="spot",fishingPhase="seated"};s.UpdateContext("pond",user,true,"unity",false,true);
            AddClip(s,"sfx/ui_click_01.wav");AddClip(s,"sfx/ui_click_02.wav");AddClip(s,"sfx/meow_01.wav");AddClip(s,"sfx/meow_02.wav");AddClip(s,"sfx/meow_03.wav");
            Invoke(s,"Request","bite_s","bite");Invoke(s,"Request","catch","result-A");
            Invoke(s,"Request","ui_click","ui");Invoke(s,"Request","meow","cat");
            Assert.AreEqual(4,s.ActiveShortVoices);
            Invoke(s,"Request","catch","result-B");var slots=(Array)Field(s,"slots");var replacing=slots.GetValue(2);
            var anchored=(double)replacing.GetType().GetField("fadingAt").GetValue(replacing);
            Assert.GreaterOrEqual(anchored,0,"runtime audio source must really be active before replacement");
            Invoke(s,"Request","catch","result-C");Assert.AreEqual(anchored,replacing.GetType().GetField("fadingAt").GetValue(replacing));
            var until=UnityEditor.EditorApplication.timeSinceStartup+.06;while(UnityEditor.EditorApplication.timeSinceStartup<until)yield return null;
            Invoke(s,"Update");Assert.AreEqual("result-C",replacing.GetType().GetField("identity").GetValue(replacing));
            Assert.AreEqual(5,root.GetComponentsInChildren<AudioSource>().Length);
            s.UpdateContext("pond",user,true,"unity",true);Assert.AreEqual(-1d,Field(s,"automaticVoiceAt"));
            until=UnityEditor.EditorApplication.timeSinceStartup+.06;while(UnityEditor.EditorApplication.timeSinceStartup<until)yield return null;
            Invoke(s,"Update");Assert.AreEqual(0,s.ActiveShortVoices);
        }
        [Test] public void FallbackWaitsForCompleteVisibleSourceFrameAndNeverInventsWaterContact()
        {
            var s=Service();AddClip(s,"sfx/cast.wav");var user=new PondUserDto{id="own",spotId="spot",fishingPhase="casting"};
            s.UpdateContext("pond",user,true,"unity",false,true);s.BeginAction("start_fishing",1);
            var markers=new DesktopAudioFrameMarkers();markers.Published("cast",1,true);markers.Published("cast",2,false);
            Assert.AreEqual(0,s.ActiveShortVoices);markers.Published("cast",2,true);Assert.AreEqual(1,s.ActiveShortVoices);
            // A portrait has no authoritative shoreline/contact provider; splash must not be requested.
            markers.Published("cast",3,true);Assert.AreEqual(1,s.ActiveShortVoices);
        }
        [UnityTest] public IEnumerator ConfiguredWavsDecodeWithRuntimeOptionsWithinShortMemoryBudget()
        {
            var dir=Path.Combine(Application.streamingAssetsPath,"Audio");var config=JsonUtility.FromJson<DesktopAudioConfiguration>(File.ReadAllText(Path.Combine(dir,"cue-config.json")));
            Assert.AreEqual(2,config.version);var paths=config.cues.SelectMany(c=>c.paths).Distinct().ToList();Assert.Greater(paths.Count,40);
            paths.Add("music/pond_daylight.wav");long shortBytes=0;
            foreach(var relative in paths)
            {
                Assert.IsTrue(DesktopAudioService.SafeAudioPath(relative));var streaming=relative.StartsWith("music/",StringComparison.Ordinal);
                using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(dir,relative)).AbsoluteUri,AudioType.WAV))
                {
                    ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio=streaming;var op=request.SendWebRequest();var deadline=UnityEditor.EditorApplication.timeSinceStartup+20;
                    while(!op.isDone&&UnityEditor.EditorApplication.timeSinceStartup<deadline)yield return null;
                    Assert.IsTrue(op.isDone,relative+" timed out");Assert.AreEqual(UnityWebRequest.Result.Success,request.result,relative+": "+request.error);
                    var clip=DownloadHandlerAudioClip.GetContent(request);Assert.IsNotNull(clip,relative);Assert.Greater(clip.samples,0);Assert.AreEqual(44100,clip.frequency);
                    if(streaming){Assert.AreEqual(2,clip.channels);Assert.That(clip.length,Is.InRange(60f,90f));}else shortBytes+=(long)clip.samples*clip.channels*sizeof(float);
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }
            Assert.LessOrEqual(shortBytes,4*1024*1024,"decoded PCM short-sound cache");
        }
    }
}
#endif
