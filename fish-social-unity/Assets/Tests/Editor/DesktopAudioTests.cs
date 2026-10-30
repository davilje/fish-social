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
        readonly Dictionary<string,float?> savedAmbience=new Dictionary<string,float?>();
        static readonly string[] AmbienceIds={"water","wind","leaves"};
        static readonly string[] AmbienceKeys={DesktopAudioService.WaterPreference,DesktopAudioService.WindPreference,DesktopAudioService.LeavesPreference};
        GameObject root;
        static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static void Invoke(object target,string method,params object[] args) => target.GetType().GetMethod(method,Private).Invoke(target,args);
        static object Field(object target,string name) => target.GetType().GetField(name,Private).GetValue(target);
        [SetUp] public void SetUp()
        {
            savedAmbience.Clear();foreach(var key in AmbienceKeys){savedAmbience[key]=PlayerPrefs.HasKey(key)?PlayerPrefs.GetFloat(key):(float?)null;PlayerPrefs.DeleteKey(key);}
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
            foreach(var pair in savedAmbience){if(pair.Value.HasValue)PlayerPrefs.SetFloat(pair.Key,pair.Value.Value);else PlayerPrefs.DeleteKey(pair.Key);}
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
        [Test] public void NineFixedSourcesSeparateWaterCrossfadeWeatherLoopsAndFourShortVoices()
        {
            var service=Service();var sources=root.GetComponentsInChildren<AudioSource>();Assert.AreEqual(9,sources.Length);
            Assert.AreEqual(5,sources.Count(s=>s.loop));Assert.IsTrue(sources.Single(s=>s.name=="BackgroundMusic").loop);
            CollectionAssert.AreEquivalent(new[]{"BackgroundMusic","PondWaterA","PondWaterB","PondWind","PondLeaves","InterfaceSounds","FishingSoundsA","FishingSoundsB","CatVoice"},sources.Select(s=>s.name));
            foreach(var source in sources){Assert.AreEqual(0,source.spatialBlend);Assert.IsFalse(source.playOnAwake);Assert.IsTrue(source.ignoreListenerPause);}
            DesktopAudioService.SetMusicVolume(.12f);Assert.AreEqual(.12f,sources.Single(s=>s.name=="BackgroundMusic").volume);
            Assert.AreEqual(0,sources.Single(s=>s.name=="PondWaterA").volume);
            Assert.AreEqual(0,service.ActiveShortVoices);
        }
        [Test] public void PondMembershipMutesMusicBeforeFishingStartsAndKeepsUserPreference()
        {
            var s=Service();var music=(AudioSource)Field(s,"music");
            AddClip(s,"music/pond_daylight.wav");music.clip=((Dictionary<string,AudioClip>)Field(s,"clips"))["music/pond_daylight.wav"];
            Invoke(s,"SyncSceneLoops");Assert.IsTrue(music.isPlaying);
            var user=new PondUserDto{id="own",fishingPhase="idle"};
            s.UpdateContext("pond",user,true,"overlay",false,true);
            Assert.IsTrue(s.IsFishingScene);Assert.IsFalse(music.isPlaying);Assert.AreEqual(0,music.volume);
            DesktopAudioService.SetMusicVolume(.8f);Invoke(s,"Update");
            Assert.IsFalse(music.isPlaying);Assert.AreEqual(0,music.volume);Assert.AreEqual(.8f,DesktopAudioService.MusicVolume);
            s.UpdateContext("pond",user,false,"overlay",false,true);
            Assert.IsTrue(s.IsFishingScene,"Reconnect must not leak music back into the pond");
            s.UpdateContext("pond",null,false,"unity",false);
            Assert.IsFalse(s.IsFishingScene);Assert.IsTrue(music.isPlaying);Assert.AreEqual(.8f,music.volume);
            Assert.AreEqual(.7f,DesktopAudioService.EffectsVolume);
        }
        [Test] public void ColdLoadedMusicCannotAutoplayInsidePondAndDefaultPondIdIsNotMembership()
        {
            var s=Service();s.UpdateContext("pond-calm",null,true,"unity",false,true);
            Assert.IsFalse(s.IsFishingScene);
            s.UpdateContext("pond-calm",new PondUserDto{id="own",fishingPhase="seated"},true,"unity",false,true);
            AddClip(s,"music/pond_daylight.wav");var music=(AudioSource)Field(s,"music");
            music.clip=((Dictionary<string,AudioClip>)Field(s,"clips"))["music/pond_daylight.wav"];
            Invoke(s,"SyncSceneLoops");Assert.IsFalse(music.isPlaying);Assert.AreEqual(0,music.volume);
        }
        [Test] public void PondSilencesUnityAndNativeButtonsClearsPendingUiAndRetainsActionAndCatSounds()
        {
            var s=Service();foreach(var path in DesktopAudioService.DefaultConfiguration().cues.SelectMany(c=>c.paths))AddClip(s,path);
            Invoke(s,"Request","ui_click","before-entry");
            var ui=root.GetComponentsInChildren<AudioSource>().Single(x=>x.name=="InterfaceSounds");Assert.IsTrue(ui.isPlaying);
            // Queue a replacement just before entry: neither old nor pending UI may leak in.
            Invoke(s,"Request","ui_open","pending-entry");
            s.UpdateContext("pond",new PondUserDto{id="own",fishingPhase="seated"},true,"overlay",false,true);
            Assert.IsFalse(ui.isPlaying);var slot=((Array)Field(s,"slots")).GetValue(0);Assert.IsNull(slot.GetType().GetField("nextClip").GetValue(slot));
            foreach(var cue in new[]{"ui_click","ui_open","ui_close","ui_toggle","ui_tick","ui_success","ui_error"})
            {
                DesktopAudioService.PlayResult(cue,"unity-"+cue);
                s.HandleNativeUi(new NativeOverlayCommandDto{text=cue,actionId="native-"+cue});
                Invoke(s,"Request",cue,"direct-"+cue);
                Assert.IsFalse(ui.isPlaying,cue);
            }
            Invoke(s,"Request","cast","cast");DesktopAudioService.PlayMeow();
            Assert.AreEqual(2,s.ActiveShortVoices,"Only buttons and BGM are suppressed");
        }
        AudioSource EnvironmentSource(string id) => root.GetComponentsInChildren<AudioSource>().Single(x=>x.name==id);
        DesktopAudioEnvelope Envelope(string id)=>EnvironmentSource(id).GetComponent<DesktopAmbienceEnvelopeFilter>().envelope;
        double audioNow;
        void At(DesktopAudioService service,double time){audioNow=time;Invoke(service,"SyncSceneLoops");}
        void AddEnvironmentClips(DesktopAudioService service)
        {
            audioNow=AudioSettings.dspTime;
            service.GetType().GetField("ambienceClock",Private).SetValue(service,new Func<double>(()=>audioNow));
            foreach(var name in new[]{"lake_calm","lake_ripples","shore","wind_open_loop","leaves"})AddClip(service,"ambience/pond_"+name+".wav",name=="wind_open_loop"?12:48);
        }
        DesktopAmbienceConfiguration EnvironmentConfig()=>new DesktopAmbienceConfiguration{
            version=4,defaultWaterProfile="calm",
            loops=new[]{new DesktopAmbienceLoop{id="water",path="ambience/pond_lake_calm.wav",gain=.6f},new DesktopAmbienceLoop{id="wind",path="ambience/pond_wind_open_loop.wav",gain=.18f},new DesktopAmbienceLoop{id="leaves",path="ambience/pond_leaves.wav",gain=.65f}},
            waterProfiles=new[]{new DesktopAmbienceLoop{id="calm",path="ambience/pond_lake_calm.wav",gain=.6f},new DesktopAmbienceLoop{id="ripples",path="ambience/pond_lake_ripples.wav",gain=.65f},new DesktopAmbienceLoop{id="shore",path="ambience/pond_shore.wav",gain=.45f}},
            scenes=new[]{new DesktopAmbienceScene{pondId="lake-a",waterProfile="calm"},new DesktopAmbienceScene{pondId="lake-b",waterProfile="ripples"},new DesktopAmbienceScene{pondId="river",waterProfile="shore"}}
        };
        void ConfigureEnvironment(DesktopAudioService service,DesktopAmbienceConfiguration config)
        {Assert.AreEqual(true,service.GetType().GetMethod("ConfigureAmbience",Private).Invoke(service,new object[]{config}));}
        DesktopWeatherPlayback WeatherPlayback(DesktopAudioService service)=>(DesktopWeatherPlayback)Field(service,"weatherPlayback");
        [Test] public void IndependentAmbienceLevelsPersistClampAndDoNotChangeOtherBuses()
        {
            foreach(var id in AmbienceIds)Assert.AreEqual(1,DesktopAudioService.GetAmbienceLevel(id));
            DesktopAudioService.SetAmbienceLevel("water",.2f);DesktopAudioService.SetAmbienceLevel("wind",.35f);DesktopAudioService.SetAmbienceLevel("leaves",0);
            PlayerPrefs.Save();
            Assert.AreEqual(.2f,PlayerPrefs.GetFloat(DesktopAudioService.WaterPreference));
            Assert.AreEqual(.35f,PlayerPrefs.GetFloat(DesktopAudioService.WindPreference));Assert.AreEqual(0,PlayerPrefs.GetFloat(DesktopAudioService.LeavesPreference));
            var service=Service();UnityEngine.Object.DestroyImmediate(root);service=Service();
            Assert.AreEqual(.2f,DesktopAudioService.GetAmbienceLevel("water"));Assert.AreEqual(.35f,DesktopAudioService.GetAmbienceLevel("wind"));Assert.AreEqual(0,DesktopAudioService.GetAmbienceLevel("leaves"));
            DesktopAudioService.SetAmbienceLevel("wind",-1);DesktopAudioService.SetAmbienceLevel("leaves",3);
            Assert.AreEqual(0,DesktopAudioService.GetAmbienceLevel("wind"));Assert.AreEqual(1,DesktopAudioService.GetAmbienceLevel("leaves"));
            DesktopAudioService.SetAmbienceLevel("wind",float.NaN);PlayerPrefs.SetFloat(DesktopAudioService.LeavesPreference,float.PositiveInfinity);
            Assert.AreEqual(1,DesktopAudioService.GetAmbienceLevel("wind"));Assert.AreEqual(1,DesktopAudioService.GetAmbienceLevel("leaves"));
            Assert.Throws<ArgumentException>(()=>DesktopAudioService.SetAmbienceLevel("unknown",.5f));
            Assert.AreEqual(.35f,DesktopAudioService.MusicVolume);Assert.AreEqual(.7f,DesktopAudioService.EffectsVolume);
        }
        [Test] public void WaterProfilesFollowPondWithCalmFallbackAndBoundedRapidCrossfades()
        {
            var s=Service();ConfigureEnvironment(s,EnvironmentConfig());AddEnvironmentClips(s);
            Assert.AreEqual("calm",s.WaterProfileForPond("unmapped"));Assert.AreEqual("calm",s.WaterProfileForPond(null));Assert.AreEqual("ripples",s.WaterProfileForPond("lake-b"));
            var user=new PondUserDto{id="own",fishingPhase="waiting"};s.UpdateContext("lake-a",user,true,"unity",false,true);
            var entry=audioNow;At(s,entry+2);
            Assert.AreEqual("ambience/pond_lake_calm.wav",EnvironmentSource("PondWaterA").clip.name);
            Assert.AreEqual(1,Envelope("PondWaterA").Evaluate(audioNow));Assert.AreEqual(.7f*.6f,EnvironmentSource("PondWaterA").volume,.001);
            s.UpdateContext("lake-b",user,true,"unity",false,true);At(s,entry+3);
            Assert.AreEqual(.5f,Envelope("PondWaterA").Evaluate(audioNow),.001);Assert.AreEqual(.5f,Envelope("PondWaterB").Evaluate(audioNow),.001);
            Assert.AreEqual("ambience/pond_lake_ripples.wav",EnvironmentSource("PondWaterB").clip.name);
            s.UpdateContext("river",user,true,"unity",false,true);At(s,entry+3.1);s.UpdateContext("lake-a",user,true,"unity",false,true);At(s,entry+6);
            var water=root.GetComponentsInChildren<AudioSource>().Where(x=>x.name.StartsWith("PondWater",StringComparison.Ordinal)).ToArray();
            Assert.AreEqual(2,water.Length);Assert.AreEqual(1,water.Count(x=>x.isPlaying));Assert.IsTrue(water.Single(x=>x.isPlaying).clip.name.EndsWith("lake_calm.wav"));
            Assert.AreEqual(9,root.GetComponentsInChildren<AudioSource>().Length);Assert.AreEqual(0,s.ActiveShortVoices);
            Assert.IsFalse(((AudioSource)Field(s,"music")).isPlaying);Assert.IsFalse(EnvironmentSource("InterfaceSounds").isPlaying);
        }
        [Serializable] sealed class WeatherGoldenSample {public double time,wind,leaves;}
        [Serializable] sealed class WeatherGolden {public int seed;public DesktopWeatherConfiguration config;public WeatherGoldenSample[] samples;}
        [Test] public void WeatherMatchesAdoptedJavaScriptGoldenAcrossThirtyMinuteTimeline()
        {
            var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../../docs/工程/声音/flowing-adoption-2026-10-30/weather-golden.json"));
            var golden=JsonUtility.FromJson<WeatherGolden>(File.ReadAllText(path));Assert.AreEqual(2201030,golden.seed);Assert.GreaterOrEqual(golden.samples.Length,100);
            var playback=new DesktopWeatherPlayback(golden.config,0);
            foreach(var point in golden.samples)
            {
                playback.Prepare(point.time);
                Assert.AreEqual(point.wind,playback.Evaluate(point.time,false),1e-9,"wind at "+point.time);
                Assert.AreEqual(point.leaves,playback.Evaluate(point.time,true),1e-9,"leaves at "+point.time);
            }
        }
        [Test] public void SharedWeatherIsContinuousAcrossBlocksAndBoundedForSixHours()
        {
            var config=new DesktopWeatherConfiguration();var model=new DesktopWeather(config);var first=model.NextBlock();var next=model.NextBlock();
            Assert.AreEqual(first.wind.Last(),next.wind.First(),1e-12);Assert.AreEqual(first.leaves.Last(),next.leaves.First(),1e-12);
            var playback=new DesktopWeatherPlayback(config,0);var min=1d;var max=0d;
            for(var time=0d;time<=6*3600;time+=15)
            {
                playback.Prepare(time);Assert.LessOrEqual(playback.window.BlockCount,4);Assert.LessOrEqual(playback.RetainedWindPoints,8);
                Assert.GreaterOrEqual(playback.window.End-time,60);
                foreach(var leaf in new[]{false,true}){var gain=playback.Evaluate(time,leaf);Assert.That(gain,Is.InRange(0d,1d));}
                var wind=playback.Evaluate(time,false);min=Math.Min(min,wind);max=Math.Max(max,wind);
            }
            Assert.Less(min,.04);Assert.Greater(max,.4,"Long running weather must keep varying instead of freezing");
        }
        [Test] public void DspProcessesSharedWeatherWithoutFramesAndCancellationSilencesNextBlock()
        {
            var service=Service();var wind=EnvironmentSource("PondWind").GetComponent<DesktopAmbienceEnvelopeFilter>();var leaves=EnvironmentSource("PondLeaves").GetComponent<DesktopAmbienceEnvelopeFilter>();
            var playback=new DesktopWeatherPlayback(new DesktopWeatherConfiguration(),0);
            wind.weather=leaves.weather=playback;wind.envelope=leaves.envelope=new DesktopAudioEnvelope(-2,-1,1,1);
            var a=Enumerable.Repeat(1f,1024).ToArray();var b=(float[])a.Clone();var c=(float[])a.Clone();
            wind.Process(a,2,5,44100);wind.Process(b,2,45,44100);leaves.Process(c,2,45,44100);
            Assert.AreEqual(playback.Evaluate(5,false),a[0],1e-6);Assert.AreEqual(playback.Evaluate(45,false),b[0],1e-6);Assert.AreEqual(playback.Evaluate(45,true),c[0],1e-6);
            Assert.Greater(Math.Abs(a[0]-b[0]),.01);Assert.Greater(Math.Abs(b[0]-c[0]),.01);Assert.AreEqual(b[0],b[1]);Assert.AreNotEqual(b[0],b[1022]);
            wind.envelope=null;wind.Process(a,2,46,44100);Assert.IsTrue(a.All(x=>x==0));
            BackgroundRenderGate.SetOverlayActive(false);BackgroundRenderGate.SetHidden(true);Invoke(service,"RefreshAudioClock");Assert.AreEqual(5,Application.targetFrameRate);
        }
        [Test] public void ContinuousWindLeavesShareTimelineAndIndependentMuteRestoresCurrentShape()
        {
            var s=Service();ConfigureEnvironment(s,EnvironmentConfig());AddEnvironmentClips(s);
            s.UpdateContext("lake-a",new PondUserDto{id="own",fishingPhase="waiting"},true,"unity",false,true);
            var playback=WeatherPlayback(s);var wind=EnvironmentSource("PondWind");var leaves=EnvironmentSource("PondLeaves");
            Assert.IsTrue(wind.loop);Assert.IsTrue(leaves.loop);Assert.IsTrue(wind.isPlaying);Assert.IsTrue(leaves.isPlaying);
            Assert.AreSame(playback,wind.GetComponent<DesktopAmbienceEnvelopeFilter>().weather);Assert.AreSame(playback,leaves.GetComponent<DesktopAmbienceEnvelopeFilter>().weather);
            Assert.AreEqual(0,Envelope("PondWind").Evaluate(audioNow));At(s,audioNow+2);Assert.AreEqual(1,Envelope("PondWind").Evaluate(audioNow));
            Assert.AreEqual(.7f*.18f,wind.volume,.001);Assert.AreEqual(.7f*.65f,leaves.volume,.001,"Production must not apply the 5x monitoring gain");
            DesktopAudioService.SetAmbienceLevel("wind",0);Assert.IsFalse(wind.isPlaying);Assert.IsNull(Envelope("PondWind"));Assert.IsTrue(leaves.isPlaying);
            At(s,audioNow+10);DesktopAudioService.SetAmbienceLevel("wind",.3f);Assert.IsTrue(wind.isPlaying);Assert.AreSame(playback,WeatherPlayback(s));
            Assert.AreEqual(0,Envelope("PondWind").Evaluate(audioNow));Assert.AreEqual((3.2+audioNow-playback.origin)%12,wind.time,.02,"Resume from current shared timeline, not a stale gust offset");
            At(s,audioNow+2);Assert.AreEqual(1,Envelope("PondWind").Evaluate(audioNow));
            DesktopAudioService.SetAmbienceLevel("leaves",0);Assert.IsFalse(leaves.isPlaying);At(s,audioNow+5);DesktopAudioService.SetAmbienceLevel("leaves",.4f);
            Assert.IsTrue(leaves.isPlaying);Assert.AreSame(playback,WeatherPlayback(s));Assert.AreEqual(.7f*.18f*.3f,wind.volume,.001);Assert.AreEqual(.7f*.65f*.4f,leaves.volume,.001);
            Assert.AreEqual(0,s.ActiveShortVoices);Assert.AreEqual(9,root.GetComponentsInChildren<AudioSource>().Length);
            BackgroundRenderGate.SetOverlayActive(false);BackgroundRenderGate.SetHidden(true);Invoke(s,"RefreshAudioClock");Assert.AreEqual(5,Application.targetFrameRate);
        }
        [Test] public void WeatherSceneChangesFadeButHiddenDisableAndMasterMuteImmediatelyClearModels()
        {
            var s=Service();ConfigureEnvironment(s,EnvironmentConfig());AddEnvironmentClips(s);var user=new PondUserDto{id="own",fishingPhase="waiting"};
            s.UpdateContext("lake-a",user,true,"unity",false,true);At(s,audioNow+3);var first=WeatherPlayback(s);
            s.UpdateContext("lake-b",user,true,"unity",false,true);Assert.AreSame(first,WeatherPlayback(s));var exit=audioNow;
            At(s,exit+.6);Assert.AreEqual(.5f,Envelope("PondWind").Evaluate(audioNow),.001);
            At(s,exit+1.3);Assert.AreNotSame(first,WeatherPlayback(s));Assert.AreEqual(0,Envelope("PondWind").Evaluate(audioNow));
            At(s,audioNow+2);s.UpdateContext("lake-b",user,true,"unity",true);Assert.IsNull(WeatherPlayback(s));
            foreach(var name in new[]{"PondWaterA","PondWaterB","PondWind","PondLeaves"}){Assert.IsFalse(EnvironmentSource(name).isPlaying);Assert.IsNull(Envelope(name));}
            At(s,audioNow+100);s.UpdateContext("lake-b",user,true,"unity",false);Assert.IsNotNull(WeatherPlayback(s));Assert.AreEqual(audioNow,WeatherPlayback(s).origin);Assert.AreEqual(0,Envelope("PondWind").Evaluate(audioNow));
            DesktopAudioService.SetEffectsVolume(0);Assert.IsNull(WeatherPlayback(s));At(s,audioNow+100);DesktopAudioService.SetEffectsVolume(.5f);Assert.AreEqual(audioNow,WeatherPlayback(s).origin);
            s.enabled=false;Invoke(s,"OnDisable");Assert.IsNull(WeatherPlayback(s));At(s,audioNow+100);s.enabled=true;Invoke(s,"OnEnable");Assert.AreEqual(audioNow,WeatherPlayback(s).origin);
            DesktopAudioService.SetAmbienceLevel("water",0);Assert.IsFalse(EnvironmentSource("PondWaterA").isPlaying);Assert.IsFalse(EnvironmentSource("PondWaterB").isPlaying);Assert.IsNotNull(WeatherPlayback(s));
            DesktopAudioService.SetAmbienceLevel("water",.8f);At(s,audioNow+2);var water=root.GetComponentsInChildren<AudioSource>().Single(x=>x.name.StartsWith("PondWater",StringComparison.Ordinal)&&x.isPlaying);Assert.AreEqual(.5f*.65f*.8f,water.volume,.001);
            DesktopAudioService.SetMusicVolume(0);Assert.IsNotNull(WeatherPlayback(s));
            s.UpdateContext("lake-b",null,false,"unity",false);Assert.IsNotNull(WeatherPlayback(s));At(s,audioNow+1.3);Assert.IsNull(WeatherPlayback(s));Assert.IsFalse(EnvironmentSource("PondWind").isPlaying);At(s,audioNow+1);Assert.IsFalse(water.isPlaying);
        }
        [Test] public void AmbienceManifestRejectsInvalidWeatherAndRetiredVersionsAtomically()
        {
            var s=Service();var config=EnvironmentConfig();ConfigureEnvironment(s,config);
            config.scenes[0].waterProfile="missing";Assert.AreEqual(false,s.GetType().GetMethod("ConfigureAmbience",Private).Invoke(s,new object[]{config}));Assert.AreEqual("calm",s.WaterProfileForPond("lake-a"));
            config=EnvironmentConfig();config.weather.sampleIntervalSeconds=0;Assert.AreEqual(false,s.GetType().GetMethod("ConfigureAmbience",Private).Invoke(s,new object[]{config}));
            config=EnvironmentConfig();config.weather.leavesFallSeconds=double.NaN;Assert.AreEqual(false,s.GetType().GetMethod("ConfigureAmbience",Private).Invoke(s,new object[]{config}));
            config=EnvironmentConfig();config.version=3;Assert.AreEqual(false,s.GetType().GetMethod("ConfigureAmbience",Private).Invoke(s,new object[]{config}));
            Assert.AreEqual("ripples",s.WaterProfileForPond("lake-b"));
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
            var user=new PondUserDto{id="own",spotId="spot",fishingPhase="seated"};
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
            Assert.AreEqual(9,root.GetComponentsInChildren<AudioSource>().Length);
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
            paths.Add("music/pond_daylight.wav");
            var ambience=JsonUtility.FromJson<DesktopAmbienceConfiguration>(File.ReadAllText(Path.Combine(dir,"ambience-manifest.json")));
            Assert.AreEqual(4,ambience.version);CollectionAssert.AreEquivalent(AmbienceIds,ambience.loops.Select(x=>x.id));
            Assert.AreEqual(.18f,ambience.loops.Single(x=>x.id=="wind").gain);Assert.AreEqual(.65f,ambience.loops.Single(x=>x.id=="leaves").gain);
            Assert.AreEqual(2201030,ambience.weather.seed);Assert.IsTrue(ambience.weather.IsValid());
            Assert.AreEqual(.28,ambience.weather.leavesDelaySeconds);Assert.AreEqual(5,ambience.weather.monitorGain);
            var service=Service();ConfigureEnvironment(service,ambience);Assert.Greater(ambience.scenes.Length,0);
            foreach(var scene in ambience.scenes)Assert.AreEqual(scene.waterProfile,service.WaterProfileForPond(scene.pondId));
            Assert.AreEqual("calm",service.WaterProfileForPond("unknown-pond"));
            paths.AddRange(ambience.assets.Concat(ambience.waterProfiles).Concat(ambience.loops).Select(x=>x.path).Distinct());long shortBytes=0;
            foreach(var relative in paths)
            {
                Assert.IsTrue(DesktopAudioService.SafeAudioPath(relative));var streaming=relative.StartsWith("music/",StringComparison.Ordinal)||relative.StartsWith("ambience/",StringComparison.Ordinal);
                using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.Combine(dir,relative)).AbsoluteUri,AudioType.WAV))
                {
                    ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio=streaming;var op=request.SendWebRequest();var deadline=UnityEditor.EditorApplication.timeSinceStartup+20;
                    while(!op.isDone&&UnityEditor.EditorApplication.timeSinceStartup<deadline)yield return null;
                    Assert.IsTrue(op.isDone,relative+" timed out");Assert.AreEqual(UnityWebRequest.Result.Success,request.result,relative+": "+request.error);
                    var clip=DownloadHandlerAudioClip.GetContent(request);Assert.IsNotNull(clip,relative);Assert.Greater(clip.samples,0);Assert.AreEqual(44100,clip.frequency);
                    if(streaming){Assert.AreEqual(2,clip.channels);Assert.That(clip.length,relative.EndsWith("pond_wind_open_loop.wav",StringComparison.Ordinal)?Is.InRange(11f,30f):Is.InRange(40f,90f));}else shortBytes+=(long)clip.samples*clip.channels*sizeof(float);
                    UnityEngine.Object.DestroyImmediate(clip);
                }
            }
            Assert.LessOrEqual(shortBytes,4*1024*1024,"decoded PCM short-sound cache");
        }
    }
}
#endif
