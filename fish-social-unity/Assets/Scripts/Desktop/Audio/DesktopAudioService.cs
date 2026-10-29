using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using FishSocial.Desktop.Auth;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    [Serializable]
    public sealed class DesktopAmbienceLoop
    {
        public string id = "water", path = "ambience/pond_shore.wav";
        public float gain = .65f, fadeSeconds = 2;
    }
    [Serializable]
    public sealed class DesktopAmbienceConfiguration
    {
        public int version = 1;
        public DesktopAmbienceLoop runtime = new DesktopAmbienceLoop();
        public DesktopAmbienceLoop[] loops;
    }

    /// <summary>Unity owns four streaming loops and four bounded short-sound sources.</summary>
    public sealed class DesktopAudioService : MonoBehaviour
    {
        public const string MusicPreference = "FishSocial.Audio.MusicVolume";
        public const string EffectsPreference = "FishSocial.Audio.EffectsVolume";
        public const string BackgroundBitePreference = "FishSocial.Audio.BackgroundBite";
        public const string WaterPreference = "FishSocial.Audio.Ambience.Water";
        public const string WindPreference = "FishSocial.Audio.Ambience.Wind";
        public const string LeavesPreference = "FishSocial.Audio.Ambience.Leaves";
        public const float DefaultMusicVolume = .35f, DefaultEffectsVolume = .7f;
        // Fixed bus headroom; individual gains stay relative to the user's SFX slider.
        public const float MixHeadroom = 1f;
        sealed class Slot
        {
            public AudioSource source;
            public string group, cue, identity;
            public int priority;
            public double started, fadingAt = -1, pendingAt;
            public float gain;
            public AudioClip nextClip;
            public AudioCueDefinition nextDefinition;
            public string nextIdentity;
        }
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        readonly Dictionary<string, AudioCueDefinition> definitions = new Dictionary<string, AudioCueDefinition>(StringComparer.Ordinal);
        readonly HashSet<string> diagnostics = new HashSet<string>(StringComparer.Ordinal);
        readonly Slot[] slots = new Slot[4];
        readonly HashSet<string> authoritativeEvents = new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> authorityOrder = new Queue<string>();
        sealed class AmbienceTrack
        {
            public AudioSource source;
            public DesktopAmbienceLoop definition;
            public float level, from, target;
            public double changedAt;
        }
        AudioSource music;
        readonly Dictionary<string, AmbienceTrack> ambienceTracks = new Dictionary<string, AmbienceTrack>(StringComparer.Ordinal);
        bool fishingScene;
        DesktopAudioPolicy policy;
        AudioPresentationDto context = new AudioPresentationDto();
        string phase = "idle", lastPond, lastActor;
        bool connected, baseline, allHidden;
        long epoch;
        float scanAt, saveAt = -1, musicDuck = 1, duckFrom = 1, duckTarget = 1;
        double duckChangedAt;
        double automaticVoiceAt = -1;
        string automaticVoiceEvent;
        int navigationFrame = -1;
        public static DesktopAudioService Instance { get; private set; }
        public DesktopAudioPolicy Policy => policy;
        public AudioPresentationDto Presentation => context;
        public bool IsFishingScene => fishingScene;
        public int ActiveShortVoices { get { var count = 0; foreach (var slot in slots) if (slot != null && slot.source.isPlaying) count++; return count; } }
        public static float MusicVolume => ReadVolume(MusicPreference, DefaultMusicVolume);
        public static float EffectsVolume => ReadVolume(EffectsPreference, DefaultEffectsVolume);
        public static bool BackgroundBiteEnabled => PlayerPrefs.GetInt(BackgroundBitePreference, 1) != 0;
        static double Now => Time.realtimeSinceStartupAsDouble;
        static long UtcMs => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public static float NormalizeVolume(float value, float fallback)
        { return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp01(value); }
        static float ReadVolume(string key, float fallback) => NormalizeVolume(PlayerPrefs.GetFloat(key, fallback), fallback);
        public static void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(MusicPreference, NormalizeVolume(value, DefaultMusicVolume));
            if (Instance != null) { Instance.ApplyVolumes(); Instance.SaveSoon(); } else PlayerPrefs.Save();
        }
        public static void SetEffectsVolume(float value)
        {
            PlayerPrefs.SetFloat(EffectsPreference, NormalizeVolume(value, DefaultEffectsVolume));
            if (Instance != null) { Instance.ApplyVolumes(); Instance.SaveSoon(); if (EffectsVolume == 0) Instance.MuteEffects(); } else PlayerPrefs.Save();
        }
        static string AmbiencePreference(string id)
        {
            switch(id) { case "water": return WaterPreference; case "wind": return WindPreference; case "leaves": return LeavesPreference;
                default: throw new ArgumentException("Unknown ambience track", nameof(id)); }
        }
        public static float GetAmbienceLevel(string id) => ReadVolume(AmbiencePreference(id), 1);
        public static void SetAmbienceLevel(string id, float value)
        {
            PlayerPrefs.SetFloat(AmbiencePreference(id), NormalizeVolume(value, 1));
            if(Instance!=null){Instance.SyncSceneLoops();Instance.SaveSoon();}else PlayerPrefs.Save();
        }
        static DesktopAmbienceLoop[] DefaultAmbienceLoops() => new[] {
            new DesktopAmbienceLoop { id="water", path="ambience/pond_shore.wav", gain=.65f },
            new DesktopAmbienceLoop { id="wind", path="ambience/pond_wind.wav", gain=.45f },
            new DesktopAmbienceLoop { id="leaves", path="ambience/pond_leaves.wav", gain=.4f }
        };
        public static void SetBackgroundBite(bool value)
        {
            PlayerPrefs.SetInt(BackgroundBitePreference, value ? 1 : 0);
            if (Instance != null) Instance.SaveSoon(); else PlayerPrefs.Save();
        }
        void SaveSoon() { saveAt = Time.realtimeSinceStartup + .5f; }
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            music = Source("BackgroundMusic"); music.loop = true;
            foreach(var definition in DefaultAmbienceLoops())
            {
                var source=Source("PondAmbience_"+definition.id);source.loop=true;source.volume=0;
                ambienceTracks.Add(definition.id,new AmbienceTrack{source=source,definition=definition});
            }
            var names = new[] { "InterfaceSounds", "FishingSoundsA", "FishingSoundsB", "CatVoice" };
            for (var i = 0; i < slots.Length; i++) slots[i] = new Slot { source = Source(names[i]), group = i == 0 ? "ui" : i == 3 ? "voice" : "fishing" };
            Configure(DefaultConfiguration()); ApplyVolumes();
        }
        AudioSource Source(string label)
        {
            var node = new GameObject(label); node.transform.SetParent(transform, false);
            var source = node.AddComponent<AudioSource>(); source.playOnAwake = false;
            source.spatialBlend = 0; source.ignoreListenerPause = true; return source;
        }
        public static DesktopAudioConfiguration DefaultConfiguration()
        {
            var cues = new List<AudioCueDefinition>();
            Action<string,string,int,float,string[]> add = (cue,group,priority,gain,paths) => cues.Add(new AudioCueDefinition { cue=cue,group=group,priority=priority,gain=gain,paths=paths });
            foreach (var cue in new[] { "ui_click", "ui_open", "ui_close", "ui_toggle" }) add(cue,"ui",1,cue=="ui_open"? .6f:cue=="ui_close"? .55f:.5f,new[]{"sfx/"+cue+"_01.wav","sfx/"+cue+"_02.wav"});
            foreach (var cue in new[] { "ui_tick", "ui_success", "ui_error" }) add(cue,"ui",cue=="ui_tick"?0:2,cue=="ui_tick"? .35f:cue=="ui_error"? .5f:.55f,new[]{"sfx/"+cue+".wav"});
            foreach (var cue in new[] { "cast", "reel", "escape", "catch" }) add(cue,"fishing",cue=="escape"||cue=="catch"?2:1,cue=="cast"? .65f:cue=="escape"? .75f:.55f,new[]{"sfx/"+cue+".wav"});
            foreach (var cue in new[] { "splash", "groundbait" }) add(cue,"fishing",cue=="groundbait"?0:1,cue=="splash"? .85f:.75f,new[]{"water/"+cue+"_01.wav","water/"+cue+"_02.wav"});
            foreach (var cue in new[]{"bite","water_struggle","water_slap"}) foreach(var tier in new[]{"s","m","l","xl"})
                add(cue+"_"+tier,"fishing",cue=="bite"?3:1,tier=="s"? .85f:tier=="m"? .9f:tier=="l"? .95f:1,new[]{"water/"+cue+"_"+tier+"_01.wav","water/"+cue+"_"+tier+"_02.wav"});
            add("meow","voice",1,.6f,new[]{"sfx/meow_01.wav","sfx/meow_02.wav","sfx/meow_03.wav"});
            foreach(var cue in cues) if(cue.cue.StartsWith("bite_",StringComparison.Ordinal)){cue.duck=.5f;cue.duckSeconds=.7f;} else if(cue.cue.StartsWith("water_",StringComparison.Ordinal)||cue.cue=="splash"||cue.cue=="groundbait"||cue.cue=="escape"){cue.duck=.7f;cue.duckSeconds=cue.cue=="splash"||cue.cue=="groundbait"? .32f:.7f;}
            return new DesktopAudioConfiguration { cues = cues.ToArray() };
        }
        void Configure(DesktopAudioConfiguration config)
        {
            if(config==null||config.version!=2||config.cues==null)return;
            definitions.Clear();
            foreach(var cue in config.cues) if(cue!=null&&!string.IsNullOrEmpty(cue.cue)&&cue.paths!=null&&cue.paths.Length>0) definitions[cue.cue]=cue;
            policy = new DesktopAudioPolicy(config); policy.Reset(context);
        }
        void Start()
        {
            if(FindObjectOfType<AudioListener>()==null)gameObject.AddComponent<AudioListener>();
            StartCoroutine(LoadBank()); BindControls();
        }
        IEnumerator LoadBank()
        {
            var configPath=Path.Combine(Application.streamingAssetsPath,"Audio","cue-config.json");
            if(File.Exists(configPath))
            {
                DesktopAudioConfiguration parsed=null;
                try { parsed=JsonUtility.FromJson<DesktopAudioConfiguration>(File.ReadAllText(configPath)); }
                catch(Exception e) { Diagnose("config",e.Message); }
                if(parsed!=null)Configure(parsed);
            }
            StartCoroutine(LoadMusic());
            StartCoroutine(LoadAmbience());
            var loaded=new HashSet<string>();
            foreach(var definition in definitions.Values)foreach(var path in definition.paths)
                if(loaded.Add(path))yield return Load(path,false);
        }
        IEnumerator LoadMusic()
        {
            const string path="music/pond_daylight.wav";
            yield return Load(path,true);
            if(clips.TryGetValue(path,out var clip)){music.clip=clip;SyncSceneLoops();}
        }
        IEnumerator LoadAmbience()
        {
            var configPath=Path.Combine(Application.streamingAssetsPath,"Audio","ambience-manifest.json");
            if(File.Exists(configPath))
            {
                try
                {
                    var config=JsonUtility.FromJson<DesktopAmbienceConfiguration>(File.ReadAllText(configPath));
                    if(!ConfigureAmbience(config))Diagnose("ambience-config","invalid environment loop configuration");
                }
                catch(Exception e){Diagnose("ambience-config",e.Message);}
            }
            foreach(var track in ambienceTracks.Values)
            {
                if(track.definition==null)continue;
                var path=track.definition.path;
                yield return Load(path,true);
                if(clips.TryGetValue(path,out var clip)){track.source.clip=clip;SyncSceneLoops();}
            }
        }
        bool ConfigureAmbience(DesktopAmbienceConfiguration config)
        {
            var loops=config?.version==1?new[]{config.runtime}:config?.version==2?config.loops:null;
            if(loops==null||loops.Length!=(config.version==1?1:3))return false;
            var seen=new HashSet<string>(StringComparer.Ordinal);
            foreach(var loop in loops)
            {
                if(loop==null)return false;
                var id=config.version==1?"water":loop.id;
                if(id==null||!ambienceTracks.ContainsKey(id)||!seen.Add(id)||!SafeAudioPath(loop.path)||
                    !loop.path.StartsWith("ambience/",StringComparison.Ordinal)||float.IsNaN(loop.gain)||float.IsInfinity(loop.gain)||
                    float.IsNaN(loop.fadeSeconds)||float.IsInfinity(loop.fadeSeconds))return false;
            }
            foreach(var track in ambienceTracks.Values)track.definition=null;
            foreach(var loop in loops)
            {
                var id=config.version==1?"water":loop.id;
                ambienceTracks[id].definition=new DesktopAmbienceLoop{id=id,path=loop.path,gain=Mathf.Clamp01(loop.gain),fadeSeconds=Mathf.Clamp(loop.fadeSeconds,.1f,5)};
            }
            return true;
        }
        // Membership survives short reconnects; a default pond ID alone is not a fishing scene.
        void SetFishingScene(bool value)
        {
            if(fishingScene!=value)
            {
                fishingScene=value;
                if(value)
                {
                    var ui=slots[0];ui.source.Stop();ui.nextClip=null;ui.nextDefinition=null;ui.fadingAt=-1;
                }
            }
            SyncSceneLoops();
        }
        void SyncSceneLoops()
        {
            if(music==null)return;
            var musicAllowed=isActiveAndEnabled&&!fishingScene;
            music.volume=musicAllowed?MusicVolume*musicDuck*MixHeadroom:0;
            if(musicAllowed&&music.clip!=null&&MusicVolume>0){if(!music.isPlaying)music.UnPause();if(!music.isPlaying)music.Play();}
            else if(music.isPlaying)music.Pause();
            foreach(var pair in ambienceTracks)
            {
                var track=pair.Value;var definition=track.definition;var source=track.source;
                var userLevel=GetAmbienceLevel(pair.Key);
                var target=isActiveAndEnabled&&fishingScene&&!allHidden&&EffectsVolume>0&&userLevel>0&&definition!=null&&source.clip!=null?1f:0f;
                if(target!=track.target){track.from=track.level;track.target=target;track.changedAt=Now;}
                // Either mute is immediate; scene and visibility changes retain their individual fades.
                if(EffectsVolume<=0||userLevel<=0||!isActiveAndEnabled||definition==null)
                {track.level=track.from=track.target=0;source.Stop();}
                else
                {
                    var t=Mathf.Clamp01((float)((Now-track.changedAt)/definition.fadeSeconds));
                    track.level=Mathf.Lerp(track.from,track.target,t*t*(3-2*t));
                    if(track.target>0&&source.clip!=null&&!source.isPlaying)source.Play();
                    if(track.target==0&&track.level<=0&&source.isPlaying)source.Stop();
                }
                source.volume=EffectsVolume*(definition?.gain??0)*userLevel*track.level*MixHeadroom;
            }
        }
        IEnumerator Load(string relativePath,bool streaming)
        {
            if(!SafeAudioPath(relativePath)){Diagnose(relativePath,"unsafe configured path");yield break;}
            var path=Path.Combine(Application.streamingAssetsPath,"Audio",relativePath);
            using(var request=UnityWebRequestMultimedia.GetAudioClip(new Uri(Path.GetFullPath(path)).AbsoluteUri,AudioType.WAV))
            {
                ((DownloadHandlerAudioClip)request.downloadHandler).streamAudio=streaming;
                yield return request.SendWebRequest();
                if(request.result!=UnityWebRequest.Result.Success){Diagnose(relativePath,request.error);yield break;}
                var clip=DownloadHandlerAudioClip.GetContent(request);
                if(clip!=null){clip.name=relativePath;clips[relativePath]=clip;}
            }
        }
        public static bool SafeAudioPath(string path) => !string.IsNullOrEmpty(path)&&!Path.IsPathRooted(path)&&!path.Contains("..")&&!path.Contains(":")&&path.EndsWith(".wav",StringComparison.OrdinalIgnoreCase);
        void Diagnose(string key,string detail)
        { if(diagnostics.Add(key))Debug.LogWarning("[DesktopAudio] "+key+": "+detail); }
        public static void Play(string cue) { Instance?.Ui(cue,null); }
        public static void PlayResult(string cue,string operationId) { Instance?.Ui(cue,operationId); }
        void Ui(string cue,string operationId)
        {
            if(cue=="meow"||cue=="manual_meow"){ManualMeow();return;}
            if(cue=="ui_click" && navigationFrame==Time.frameCount)return;
            if(cue=="ui_open"||cue=="ui_close")navigationFrame=Time.frameCount;
            if(policy.TryUi(cue,operationId,Now,!fishingScene&&!allHidden&&EffectsVolume>0))Request(cue,operationId??Guid.NewGuid().ToString("N"));
        }
        public void HandleNativeUi(NativeOverlayCommandDto message)
        {
            if(message==null)return;
            // This is a semantic allowlist. The IPC never accepts paths or action audio.
            if(DesktopAudioPolicy.UiCue(message.text))Ui(message.text,message.actionId);
            else if(message.text=="manual_meow"||message.text=="meow")ManualMeow();
        }
        public static void PlayMeow() { Instance?.ManualMeow(); }
        void ManualMeow()
        {
            if(policy.TryVoice(Now,false,!allHidden&&EffectsVolume>0))Request("meow",Guid.NewGuid().ToString("N"));
        }
        void CancelAutomaticVoice(){automaticVoiceAt=-1;automaticVoiceEvent=null;}
        void MuteEffects()
        {
            CancelAutomaticVoice();foreach(var slot in slots)if(slot!=null){slot.source.Stop();slot.nextClip=null;slot.nextDefinition=null;slot.fadingAt=-1;}
        }
        void RefreshAudioClock()
        {
            var now=Now;var transition=policy.DuckUntil>0&&now<policy.DuckUntil+.25;
            foreach(var track in ambienceTracks.Values)transition|=track.level!=track.target;
            foreach(var slot in slots)transition|=slot!=null&&slot.fadingAt>=0;
            BackgroundRenderGate.SetAudioWork(transition || context.enabled && phase=="hooked" && context.eventDeadlineMs>UtcMs);
        }
        public void ObserveFishingPhase(string ignored) { /* Old phase-only audio is intentionally disabled. */ }
        public static void CelebrateCatch() { /* Confirmation requires a stable result ID and visible marker. */ }
        AudioPresentationDto CopyContext()
        {
            return new AudioPresentationDto { presentationSessionId=context.presentationSessionId,actorId=context.actorId,pondId=context.pondId,spotId=context.spotId,ownerEpoch=context.ownerEpoch,owner=context.owner,actionId=context.actionId,eventId=context.eventId,eventKind=context.eventKind,waterSizeTier=context.waterSizeTier,eventDeadlineMs=context.eventDeadlineMs,source=context.source,enabled=context.enabled };
        }
        public void UpdateContext(string pondId,PondUserDto user,bool isConnected,string owner,bool hidden,bool snapshot=false,string effectivePhase=null,FishingPresentationEnvelope shared=null)
        {
            if(hidden&&!allHidden){CancelAutomaticVoice();foreach(var slot in slots)if(slot!=null)Fade(slot);}
            allHidden=hidden;
            var actor=user?.id??"";
            SetFishingScene(!string.IsNullOrEmpty(pondId)&&!string.IsNullOrEmpty(actor));
            var changed=connected!=isConnected||lastPond!=pondId||lastActor!=actor;
            if(changed)
            {
                connected=isConnected;lastPond=pondId;lastActor=actor;baseline=false;
                epoch++;context=new AudioPresentationDto{presentationSessionId=Guid.NewGuid().ToString("N"),ownerEpoch=epoch,owner=owner,pondId=pondId??"",actorId=actor};
                policy.Reset(context);authoritativeEvents.Clear();authorityOrder.Clear();CancelActions();
            }
            else if(context.owner!=owner)
            {
                context=CopyContext();context.owner=owner;context.ownerEpoch=++epoch;context.source="snapshot";policy.SetContext(context);CancelActions();
            }
            if(shared!=null)
            {
                if(context.presentationSessionId!=shared.presentationSessionId)
                {context.presentationSessionId=shared.presentationSessionId;policy.Reset(context);authoritativeEvents.Clear();authorityOrder.Clear();baseline=false;CancelActions();}
                if(context.ownerEpoch!=shared.ownerEpoch){CancelActions();context.ownerEpoch=shared.ownerEpoch;}
                context.owner=shared.owner;context.actionId=shared.actionId??"";
            }
            var previousPhase=phase; phase=effectivePhase??user?.fishingPhase??"idle";
            RefreshAudioClock();
            context.spotId=user?.spotId??""; context.enabled=isConnected&&!string.IsNullOrEmpty(actor);
            if(!context.enabled){context.source="snapshot";return;}
            if(snapshot||!baseline)
            {
                // First state and reconnect snapshots establish silent identity, never a live event.
                var snapshotId=user?.phaseContext?.catchId??"";
                if(!string.IsNullOrEmpty(snapshotId))RecordAuthority("bite:"+snapshotId);
                if(!string.IsNullOrEmpty(user?.phaseContext?.resultId))RecordAuthority("escape:"+user.phaseContext.resultId);
                if(!baseline || snapshotId!=context.eventId && !string.IsNullOrEmpty(snapshotId))
                {
                    context.source="snapshot";context.eventId=snapshotId;context.eventKind="";
                    if(shared==null)context.actionId="";baseline=true;CancelAutomaticVoice();
                }
                policy.SetContext(context);return;
            }
            var id=user?.phaseContext?.catchId;
            if(phase=="hooked"&&!string.IsNullOrEmpty(id)&&id!=context.eventId)
                ArmEvent("bite",id,user.phaseContext.sizeM,user.phaseEndsAt);
            if(phase=="resolving"&&user?.phaseContext?.outcome=="escape"&&!string.IsNullOrEmpty(user.phaseContext.resultId))
                ArmEvent("escape",user.phaseContext.resultId,user.phaseContext.sizeM,0);
            if(phase=="seated"||phase=="idle")
            {
                if(previousPhase!=phase && (phase=="idle" || context.eventKind!="catch")){CancelAutomaticVoice();FadeActions();}
                if(context.eventKind!="catch"){context.actionId="";context.source="snapshot";}
            }
            if((phase=="casting"||phase=="baiting"||phase=="groundbaiting")&&string.IsNullOrEmpty(context.actionId)&&previousPhase!=phase)
            {
                // Authority stage revision is stable; snapshot origin above never starts this path.
                context.actionId=context.presentationSessionId+":phase:"+user.phaseEndsAt;
                context.source="authoritative";
            }
            policy.SetContext(context);
        }
        public void BeginAction(string command,long commandId)
        {
            if(!context.enabled)return;
            if(command=="leave_spot"||command=="exit_pond"){CancelActions();context.actionId="";context.eventKind="";context.source="snapshot";context.ownerEpoch=++epoch;return;}
            if(command=="stop_fishing")
            {
                CancelAutomaticVoice();FadeActions();context.ownerEpoch=++epoch;
                // An authoritative escape remains available; ordinary stop is never an escape event.
                if(context.eventKind!="escape"){context.eventKind="";context.eventId="";}
            }
            else if(command=="start_fishing"||command=="groundbait_start")
            {CancelAutomaticVoice();context.eventId="";context.eventKind="";}
            context.actionId=context.presentationSessionId+":command:"+commandId+":"+command;
            context.source=context.eventKind=="escape"?"authoritative":"prediction";policy.SetContext(context);
        }
        public void RejectAction(){CancelActions();context.ownerEpoch=++epoch;context.actionId="";context.source="snapshot";policy.SetContext(context);}
        bool RecordAuthority(string key)
        {
            if(!authoritativeEvents.Add(key))return false;
            authorityOrder.Enqueue(key);while(authorityOrder.Count>512)authoritativeEvents.Remove(authorityOrder.Dequeue());return true;
        }
        public void ArmEvent(string kind,string eventId,float size,long deadline)
        {
            if(!context.enabled||string.IsNullOrEmpty(eventId))return;
            if(!RecordAuthority(kind+":"+eventId))return;
            CancelAutomaticVoice();
            context.eventId=eventId;context.eventKind=kind;context.source="authoritative";
            context.waterSizeTier="S"; // Only published body geometry may select a visible tier.
            context.eventDeadlineMs=deadline;
            if(string.IsNullOrEmpty(context.actionId))context.actionId=context.presentationSessionId+":event:"+eventId;
            policy.SetContext(context);
            if (EffectsVolume <= 0 || allHidden && kind != "bite")
                policy.ConsumeAuthoritySilently(kind == "bite" ? "bite.onset" : kind == "catch" ? "result.presented" : "escape.contact", eventId);
            if(kind=="bite")
            {
                foreach(var slot in slots)if(slot.group=="voice")Fade(slot);
                if(allHidden&&policy.ConsumeHiddenBite(eventId,UtcMs,EffectsVolume>0&&BackgroundBiteEnabled))Request("bite_"+context.waterSizeTier.ToLowerInvariant(),eventId);
            }
        }
        public void HandleMarker(NativeOverlayCommandDto message,string sender="overlay")
        {
            if(message != null && message.sentAtMs > 0) message.elapsedMs += Math.Max(0, UtcMs-message.sentAtMs);
            if(policy.TryMarker(message,sender,phase,Now,UtcMs,!allHidden&&EffectsVolume>0,out var cue))
            {
                if(cue=="bite"||cue=="water_struggle"||cue=="water_slap")
                { var tier=DesktopAudioPolicy.VisibleTier(message.waterSizeTier); cue+="_"+tier.ToLowerInvariant(); }
                var identity=(message.eventId??message.actionId)+":"+message.marker+":"+message.occurrence;
                if(cue.StartsWith("water_",StringComparison.Ordinal))identity=message.actionId+":water:"+message.occurrence;
                Request(cue,identity);

            }
        }
        public void UnityMarker(string marker,int occurrence,string waterSizeTier="S")
        {
            if(context.owner!="unity")return;
            HandleMarker(new NativeOverlayCommandDto{protocolVersion=2,presentationSessionId=context.presentationSessionId,actorId=context.actorId,pondId=context.pondId,spotId=context.spotId,ownerEpoch=context.ownerEpoch,actionId=context.actionId,eventId=context.eventId,source=context.source,marker=marker,occurrence=occurrence,actorGeneration=1,waterSizeTier=waterSizeTier},"unity");
        }
        void Request(string cue,string identity)
        {
            if(!definitions.TryGetValue(cue,out var definition)){Diagnose(cue,"missing cue definition");return;}
            if(fishingScene&&definition.group=="ui")return;
            var index=policy.Variant(cue,identity,definition.paths.Length);
            if(!clips.TryGetValue(definition.paths[index],out var clip)){Diagnose(definition.paths[index],"audio unavailable; event consumed");return;}
            var candidates=new AudioVoiceCandidate[slots.Length];
            for(var i=0;i<slots.Length;i++)
            {
                var slot=slots[i];candidates[i]=new AudioVoiceCandidate{group=slot.group,cue=slot.nextDefinition?.cue??slot.cue,
                    identity=slot.nextClip!=null?slot.nextIdentity:slot.identity,active=slot.source.isPlaying||slot.nextClip!=null,
                    priority=slot.nextDefinition?.priority??slot.priority,started=slot.started};
            }
            var choice=DesktopAudioPolicy.ChooseVoice(candidates,definition.group,cue,definition.priority,identity);
            if(choice<0)return;
            var selected=slots[choice];
            if(selected.source.isPlaying||selected.nextClip!=null)
            {
                if(definition.priority<selected.priority)return;
                // Ordinary equal-priority water never replaces an active important result.
                selected.nextClip=clip;selected.nextDefinition=definition;selected.nextIdentity=identity;
                selected.pendingAt=Now;if(selected.fadingAt<0)selected.fadingAt=Now;RefreshAudioClock();return;
            }
            StartSlot(selected,clip,definition,identity);
        }
        void StartSlot(Slot slot,AudioClip clip,AudioCueDefinition definition,string identity)
        {
            slot.nextClip=null;slot.nextDefinition=null;slot.fadingAt=-1;
            slot.cue=definition.cue;slot.identity=identity;slot.priority=definition.priority;slot.gain=definition.gain;slot.started=Now;
            slot.source.Stop();slot.source.clip=clip;slot.source.volume=EffectsVolume*slot.gain*MixHeadroom;slot.source.Play();
            if(definition.cue=="catch"&&context.eventKind=="catch"&&identity.StartsWith(context.eventId+":",StringComparison.Ordinal))
            {automaticVoiceAt=Now+clip.length+.12;automaticVoiceEvent=context.eventId;}
            if(definition.duck<1)
            {
                var duration=definition.cue.StartsWith("bite_",StringComparison.Ordinal)? .7:
                    Math.Min(.7,Math.Min(clip.length,definition.duckSeconds>0?definition.duckSeconds:clip.length));
                policy.Duck(definition.duck,duration,Now);
            }
            RefreshAudioClock();
        }
        void Fade(Slot slot){slot.nextClip=null;slot.nextDefinition=null;if(slot.source.isPlaying&&slot.fadingAt<0)slot.fadingAt=Now;RefreshAudioClock();}
        void FadeActions(){foreach(var slot in slots)if(slot!=null&&slot.group!="ui")Fade(slot);}
        void CancelActions(){CancelAutomaticVoice();FadeActions();}
        void ApplyVolumes()
        {
            SyncSceneLoops();
            foreach(var slot in slots)if(slot!=null)slot.source.volume=EffectsVolume*slot.gain*MixHeadroom;
        }
        void BindControls()
        {
            foreach(var selectable in FindObjectsOfType<Selectable>(true))if(selectable is Button||selectable is Toggle)DesktopUiAudioFeedback.Ensure(selectable.gameObject);
            foreach(var cat in FindObjectsOfType<CozyCornerCharacter>(true))DesktopCatAudioFeedback.Bind(cat.transform);
            foreach(var cat in FindObjectsOfType<Pet.SpriteFramePetRenderer>(true))DesktopCatAudioFeedback.Bind(cat.transform);
        }
        void Update()
        {
            var now=Now;
            if(now>=scanAt){scanAt=(float)now+1;BindControls();}
            if(saveAt>=0&&now>=saveAt){saveAt=-1;PlayerPrefs.Save();}
            var target=policy.DuckTarget(now);
            if (Mathf.Abs(target-duckTarget)>.0001f) { duckFrom=musicDuck; duckTarget=target; duckChangedAt=now; }
            musicDuck=Mathf.Lerp(duckFrom,duckTarget,Mathf.Clamp01((float)((now-duckChangedAt)/(duckTarget<duckFrom? .08:.25))));
            SyncSceneLoops();
            foreach(var slot in slots)if(slot.fadingAt>=0)
            {
                var t=(float)((now-slot.fadingAt)/.04);slot.source.volume=EffectsVolume*slot.gain*MixHeadroom*Mathf.Clamp01(1-t);
                if(t>=1){slot.source.Stop();slot.fadingAt=-1;if(slot.nextClip!=null&&now-slot.pendingAt<=.25)StartSlot(slot,slot.nextClip,slot.nextDefinition,slot.nextIdentity);else{slot.nextClip=null;slot.nextDefinition=null;}}
            }
            RefreshAudioClock();
            if(automaticVoiceAt>=0&&now>=automaticVoiceAt)
            {
                var matches=automaticVoiceEvent==context.eventId&&context.eventKind=="catch";CancelAutomaticVoice();
                var voice=slots[3];
                if(matches&&!allHidden&&!voice.source.isPlaying&&voice.nextClip==null&&policy.TryVoice(now,true,EffectsVolume>0))Request("meow",context.eventId+":automatic");
            }
        }
        void OnApplicationQuit(){PlayerPrefs.Save();}
        void OnEnable(){SyncSceneLoops();}
        void OnDisable()
        {
            SyncSceneLoops();
            if(policy!=null)MuteEffects();
            BackgroundRenderGate.SetAudioWork(false);
        }
        void OnDestroy()
        {
            if(Instance!=this)return;Instance=null;BackgroundRenderGate.SetAudioWork(false);if(saveAt>=0)PlayerPrefs.Save();
            foreach(var clip in clips.Values)if(clip!=null)Destroy(clip);
        }
    }
}
