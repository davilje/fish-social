using System;
using System.Collections.Generic;

namespace FishSocial.Desktop
{
    [Serializable]
    public sealed class AudioCueDefinition
    {
        public string cue, group;
        public int priority;
        public float gain, duck = 1, duckSeconds;
        public string[] paths;
    }
    [Serializable]
    public sealed class DesktopAudioConfiguration
    {
        public int version = 2;
        public float uiInterval = .09f, tickInterval = .18f, waterInterval = .9f,
            groundbaitInterval = 2.16f, manualMeowInterval = 2.5f, autoMeowInterval = 12;
        public AudioCueDefinition[] cues;
    }
    [Serializable]
    public sealed class AudioPresentationDto
    {
        public int protocolVersion = 2;
        public string presentationSessionId = "", actorId = "", pondId = "", spotId = "";
        public long ownerEpoch;
        public string owner = "unity", actionId = "", eventId = "", eventKind = "";
        public string waterSizeTier = "S", source = "snapshot";
        public long eventDeadlineMs;
        public bool enabled;
    }

    public struct AudioVoiceCandidate
    {
        public string group, cue, identity;
        public bool active;
        public int priority;
        public double started;
    }

    /// <summary>Pure event policy. Silence consumes valid identities; playback never creates event identity.</summary>
    public sealed class DesktopAudioPolicy
    {
        public readonly DesktopAudioConfiguration Config;
        public AudioPresentationDto Context { get; private set; } = new AudioPresentationDto();
        readonly HashSet<string> consumed = new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> order = new Queue<string>();
        readonly HashSet<string> results = new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> resultOrder = new Queue<string>();
        readonly Dictionary<string,string> activeKeys = new Dictionary<string,string>(StringComparer.Ordinal);
        readonly Dictionary<string, int> variants = new Dictionary<string, int>(StringComparer.Ordinal);
        long actorGeneration, trackedEpoch;
        string trackedOwner;
        double uiAt = -100, tickAt = -100, waterAt = -100, groundbaitAt = -100, voiceAt = -100, autoAt = -100;
        public double DuckUntil { get; private set; }
        public float DuckMultiplier { get; private set; } = 1;
        public int ConsumedCount => consumed.Count;
        public DesktopAudioPolicy(DesktopAudioConfiguration configuration) { Config = configuration; }
        public void Reset(AudioPresentationDto context)
        {
            Context = context; consumed.Clear(); order.Clear(); results.Clear(); resultOrder.Clear(); variants.Clear(); activeKeys.Clear();
            actorGeneration = 0; trackedEpoch = context.ownerEpoch; trackedOwner = context.owner; waterAt = groundbaitAt = -100;
        }
        public void SetContext(AudioPresentationDto context)
        { if (trackedEpoch != context.ownerEpoch || trackedOwner != context.owner) actorGeneration = 0; trackedEpoch = context.ownerEpoch; trackedOwner = context.owner; Context = context;
            var expired = new List<string>(); foreach(var key in activeKeys) if(key.Value != "event:"+context.eventId && key.Value != "action:"+context.actionId) expired.Add(key.Key);
            foreach(var key in expired) activeKeys.Remove(key); }
        bool Consume(string key, bool result = false)
        {
            if (consumed.Contains(key) || activeKeys.ContainsKey(key) || (result && results.Contains(key))) return false;
            // Active authority identities survive history eviction without pinning loop occurrences.
            if (result && !string.IsNullOrEmpty(Context.eventId) && key.StartsWith(Context.presentationSessionId + ":" + Context.actorId + ":" + Context.eventId + ":", StringComparison.Ordinal))
                activeKeys[key] = "event:"+Context.eventId;
            consumed.Add(key); order.Enqueue(key);
            if (result) { results.Add(key); resultOrder.Enqueue(key); while (resultOrder.Count > 512) results.Remove(resultOrder.Dequeue()); }
            while (order.Count > 512) consumed.Remove(order.Dequeue());
            return true;
        }
        public static bool UiCue(string cue)
        { return cue == "ui_click" || cue == "ui_open" || cue == "ui_close" || cue == "ui_toggle" || cue == "ui_tick" || cue == "ui_success" || cue == "ui_error"; }
        public bool TryUi(string cue, string operationId, double now, bool audible)
        {
            if (!UiCue(cue)) return false;
            if (!string.IsNullOrEmpty(operationId) && !Consume("ui:" + operationId + ":" + cue, cue == "ui_success" || cue == "ui_error")) return false;
            if (cue == "ui_tick") { if (now - tickAt < Config.tickInterval) return false; tickAt = now; }
            else { if (now - uiAt < Config.uiInterval) return false; uiAt = now; }
            return audible;
        }
        public bool TryVoice(double now, bool automatic, bool audible)
        {
            if (now - voiceAt < Config.manualMeowInterval || automatic && now - autoAt < Config.autoMeowInterval) return false;
            voiceAt = now; if (automatic) autoAt = now;
            return audible;
        }
        public bool ConsumeHiddenBite(string eventId, long utcMs, bool audible)
        {
            if (Context.source != "authoritative" || Context.eventKind != "bite" || eventId != Context.eventId || Context.eventDeadlineMs <= utcMs) return false;
            return Consume(ResultKey("bite.onset", eventId), true) && audible;
        }
        public void ConsumeAuthoritySilently(string marker,string eventId) { Consume(ResultKey(marker,eventId),true); }
        string ResultKey(string marker, string eventId) => Context.presentationSessionId + ":" + Context.actorId + ":" + eventId + ":" + marker;
        public bool TryMarker(NativeOverlayCommandDto message, string sender, string phase, double now, long utcMs, bool audible, out string cue)
        {
            cue = null;
            var c = Context;
            if (!c.enabled || message == null || message.protocolVersion != 2 || c.owner != sender ||
                c.presentationSessionId != message.presentationSessionId || c.ownerEpoch != message.ownerEpoch ||
                c.actorId != message.actorId || c.pondId != message.pondId || c.spotId != message.spotId ||
                c.source == "snapshot" || message.source == "snapshot" || message.elapsedMs < 0 || message.elapsedMs > 250 ||
                message.occurrence < 0 || message.actorGeneration < actorGeneration) return false;
            actorGeneration = message.actorGeneration;
            var marker = message.marker;
            bool result = marker == "bite.onset" || marker == "result.presented" || marker == "escape.contact";
            if (result && (message.source != "authoritative" || c.source != "authoritative" || string.IsNullOrEmpty(c.eventId) || c.eventId != message.eventId)) return false;
            if (!result && (string.IsNullOrEmpty(c.actionId) || c.actionId != message.actionId)) return false;
            switch (marker)
            {
                case "cast.release": if (phase != "casting") return false; cue = "cast"; break;
                case "float.contact": if (phase != "casting" && phase != "waiting") return false; cue = "splash"; break;
                case "bite.onset": if (c.eventKind != "bite" || phase != "hooked" || c.eventDeadlineMs <= utcMs) return false; cue = "bite"; break;
                case "fish.surface": case "fish.slap":
                    if (phase != "hooked" || c.eventKind != "bite" || c.eventDeadlineMs <= utcMs) return false;
                    cue = marker == "fish.slap" ? "water_slap" : "water_struggle"; break;
                case "groundbait.contact": if (phase != "groundbaiting") return false; cue = "groundbait"; break;
                case "retrieve.start": if (phase != "resolving" && phase != "stopping") return false; cue = "reel"; break;
                case "escape.contact": if (c.eventKind != "escape" || phase != "resolving") return false; cue = "escape"; break;
                case "result.presented": if (c.eventKind != "catch" || phase != "seated" && phase != "resolving") return false; cue = "catch"; break;
                default: return false;
            }
            var key = result ? ResultKey(marker, c.eventId) : c.presentationSessionId + ":" + c.actorId + ":" + c.actionId + ":" + marker + ":" + message.occurrence;
            if (!Consume(key, result)) return false;
            if(marker=="cast.release"||marker=="float.contact"||marker=="retrieve.start")activeKeys[key]="action:"+c.actionId;
            if (cue == "water_struggle" || cue == "water_slap")
            {
                // Strong contact supersedes its weak partner, even inside the shared 900ms budget.
                var strongKey = c.actionId + ":strong:" + message.occurrence;
                if (cue == "water_slap") { if (!Consume(strongKey)) return false; }
                else if (consumed.Contains(strongKey) || message.occurrence % 3 != 0) return false;
                if (now - waterAt < Config.waterInterval && cue != "water_slap") return false;
                if (cue == "water_slap" && now - waterAt < Config.waterInterval && !consumed.Contains(c.actionId + ":weak:" + message.occurrence)) return false;
                if (cue == "water_struggle") Consume(c.actionId + ":weak:" + message.occurrence);
                waterAt = now;
            }
            if (cue == "groundbait") { if (message.occurrence % 3 != 0 || now - groundbaitAt < Config.groundbaitInterval) return false; groundbaitAt = now; }
            return audible;
        }
        public int Variant(string cue, string identity, int count)
        {
            if (count <= 1) return 0;
            uint hash = 2166136261; foreach (var ch in identity ?? "") hash = (hash ^ ch) * 16777619;
            var index = (int)(hash % (uint)count);
            if (variants.TryGetValue(cue, out var previous) && previous == index) index = (index + 1) % count;
            variants[cue] = index; return index;
        }
        public void Duck(float multiplier, double duration, double now)
        {
            if (now >= DuckUntil) DuckMultiplier = 1;
            DuckMultiplier = Math.Min(DuckMultiplier, multiplier);
            DuckUntil = Math.Max(DuckUntil, now + duration);
        }
        public float DuckTarget(double now) => now < DuckUntil ? DuckMultiplier : 1;
        public static int ChooseVoice(AudioVoiceCandidate[] voices, string group, string cue, int priority, string identity)
        {
            var selected=-1;
            if(cue.StartsWith("water_slap",StringComparison.Ordinal))
                for(var i=0;i<voices.Length;i++)if(voices[i].active&&voices[i].group==group&&voices[i].identity==identity&&
                    voices[i].cue!=null&&voices[i].cue.StartsWith("water_struggle",StringComparison.Ordinal))return i;
            for(var i=0;i<voices.Length;i++)
            {
                var voice=voices[i];if(voice.group!=group)continue;
                if(!voice.active)return i;
                if(selected<0||voice.priority<voices[selected].priority||voice.priority==voices[selected].priority&&voice.started<voices[selected].started)selected=i;
            }
            if(selected<0)return -1;
            var old=voices[selected];
            if(priority<old.priority||priority==old.priority&&priority<2&&group!="ui")return -1;
            return selected;
        }
        public static string VisibleTier(string tier) => tier=="S"||tier=="M"||tier=="L"||tier=="XL"?tier:"S";
        public static string Tier(float size) => float.IsNaN(size) || size <= 0 ? "S" : size < .30f ? "S" : size < .8f ? "M" : size < 1.5f ? "L" : "XL";
    }
}
