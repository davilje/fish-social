using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using FishSocialOverlay;

static class WaterEffectsTests
{
    static int passed;
    static void Check(bool value, string message) { if (!value) throw new Exception(message); passed++; }
    static FishingPresentationEnvelope Envelope(string action = "action-a", string source = "authoritative", long epoch = 1, int generation = 1)
        => new FishingPresentationEnvelope { PresentationSessionId = "session", ActorId = "cat", ActionId = action,
            EventId = "bite-a", Source = source, Owner = "overlay", OwnerEpoch = epoch, ActorGeneration = generation, WaterSizeTier = "S", Revision = 1 };
    static bool Accept(OverlayWaterEffectsPolicy p, string cue, string phase, long occurrence = 0, double now = 0, bool contact = true)
        => p.Accept(cue, phase, occurrence, now, contact, out _);
    public static void Main()
    {
        var p = new OverlayWaterEffectsPolicy(); p.SetEnvelope(Envelope());
        Check(Accept(p, "float.contact", "casting"), "first actual source marker rejected");
        Check(!Accept(p, "float.contact", "waiting"), "duplicate ACK/snapshot replayed contact");
        Check(Accept(p, "bite.onset", "hooked"), "authority bite not visible");
        Check(!Accept(p, "bite.onset", "effort"), "hooked/effort transition replayed bite");
        foreach (var phase in new[] { "resolving", "retrieve", "reel", "catch", "escape", "reset", "seated", "offline" })
            foreach (var cue in new[] { "float.contact", "waiting.ripple", "bite.onset", "fish.struggle", "fish.surface", "fish.slap", "groundbait.contact", "FX06", "FX07", "FX09" })
                Check(!Accept(p, cue, phase, 42, 20000), "ending phase emitted " + cue + " in " + phase);
        Check(!Accept(p, "fish.surface", "effort", 1, 20000, false), "non-contact fish sprayed");
        Check(!Accept(p, "fish.slap", "hooked", 1, 20000, false), "non-contact slap sprayed");
        Check(Accept(p, "fish.struggle", "effort", 1, 1000), "contact disturbance missing");
        Check(!Accept(p, "fish.struggle", "effort", 2, 1699), "disturbance exceeds 700ms cap");
        Check(Accept(p, "fish.struggle", "effort", 3, 1700), "700ms boundary rejected");
        Check(!Accept(p, "fish.struggle", "effort", 2, 3000), "throttled marker queued for later");
        Check(Accept(p, "groundbait.contact", "groundbaiting", 0, 3000), "bait contact missing");
        Check(!Accept(p, "groundbait.contact", "groundbaiting", 1, 3719), "bait exceeds loop limit");
        Check(Accept(p, "groundbait.contact", "groundbaiting", 2, 3720), "bait exact loop rejected");
        p.SetEnvelope(Envelope("action-b", "snapshot"));
        Check(!Accept(p, "bite.onset", "hooked"), "cold hooked snapshot replayed bite");
        Check(!Accept(p, "float.contact", "waiting"), "cold waiting snapshot replayed cast");
        Check(Accept(p, "waiting.ripple", "waiting", 1, 100), "snapshot cannot restore persistent waiting");
        Check(!Accept(p, "waiting.ripple", "waiting", 2, 2499), "waiting exceeds frequency");
        Check(Accept(p, "waiting.ripple", "waiting", 3, 2500), "waiting exact frequency rejected");
        p.SetEnvelope(Envelope("action-c", "prediction"));
        Check(!Accept(p, "bite.onset", "hooked"), "prediction synthesized bite");
        Check(!Accept(p, "fish.surface", "effort"), "prediction synthesized slap");
        Check(Accept(p, "float.contact", "casting"), "reversible prediction not allowed");
        p.SetEnvelope(Envelope("action-d")); p.SuppressOneShots = true;
        Check(!Accept(p, "bite.onset", "hooked"), "hidden resume replayed onset");
        Check(Accept(p, "waiting.ripple", "waiting", 1), "hidden resume cannot rebuild waiting");
        p.SetEnvelope(Envelope("action-e", generation: 3));
        Check(!p.SetEnvelope(Envelope("action-old", generation: 2)), "old actor replaced generation");
        Check(p.Envelope.ActionId == "action-e", "stale generation mutated identity");
        p.SetEnvelope(Envelope("action-f", epoch: 3, generation: 4));
        Check(!p.SetEnvelope(Envelope("action-old", epoch: 2, generation: 99)), "old owner accepted");
        Check(!p.SetEnvelope(Envelope("action-e", epoch: 3, generation: 3)), "retired action came back");
        p.SetEnvelope(null);
        Check(!p.SetEnvelope(Envelope("action-f", epoch: 3, generation: 4)), "cancelled action revived");
        p.SetEnvelope(Envelope("long-wait", epoch: 4, generation: 5));
        for (var i = 1; i <= 1000; i++) Check(Accept(p, "waiting.ripple", "waiting", i, i * 2400), "long wait stopped at ledger cap");
        Check(p.ConsumedCount == 512, "ledger unbounded");
        Check(!Accept(p, "waiting.ripple", "waiting", 1, 3000000), "ledger eviction made old occurrence eligible");
        Check(OverlayWaterEffectsPolicy.VisibleTier(16) == "S" && OverlayWaterEffectsPolicy.VisibleTier(16.001) == "M", "S/M visible limit");
        Check(OverlayWaterEffectsPolicy.VisibleTier(32) == "M" && OverlayWaterEffectsPolicy.VisibleTier(32.001) == "L", "M/L visible limit");
        Check(OverlayWaterEffectsPolicy.VisibleTier(56) == "L" && OverlayWaterEffectsPolicy.VisibleTier(56.001) == "XL", "L/XL visible limit");
        Check(OverlayWaterEffectsPolicy.VisibleTier(double.NaN) == "S" && OverlayWaterEffectsPolicy.Tier("secret") == "S", "unknown not S");
        var variant = OverlayWaterEffectsPolicy.Variant("session|cat|action|fish.surface|2");
        Check(variant == OverlayWaterEffectsPolicy.Variant("session|cat|action|fish.surface|2"), "unstable retransmission variant");
        var budget = new WaterEffectBudget();
        var owners = new List<object>();
        for (var i = 0; i < 20; i++) { var owner = new object(); owners.Add(owner); budget.Admit(owner, false, 2, 30, null); }
        Check(budget.Groups <= 6 && budget.Instances <= 12, "remote decorations stole self reserve");
        var self = new object();
        Check(budget.Admit(self, true, 10, 100, null) != null, "XL own bite dropped");
        Check(budget.Admit(self, true, 2, 90, null) != null, "own transition reserve missing");
        Check(budget.Groups <= 8 && budget.Instances <= 24, "global hard budget exceeded");
        Check(budget.Admit(new object(), false, 4, 10, null) == null, "decoration evicted own critical FX");
        Check(budget.Admit(self, true, 10, 100, null) != null && budget.Groups <= 8 && budget.Instances <= 24, "own per actor reclamation failed");
        Check(budget.Admit(self, true, 25, 100, null) == null, "oversized group admitted");
        var timeline = new OverlayGroundbaitTimeline(new[] { 180,120,120,120,120,120,120,180 }, 1, 7, 6, 0);
        Check(timeline.Sample(180 + 720 * 7 + 360).Occurrence == 7, "source loop occurrence derives from transport instead of authored time");
        var cold = new OverlayWaterEffectsPolicy(); cold.SetEnvelope(Envelope());
        foreach (var decodeMs in new[] { 0, 150, 400 })
        {
            var token = cold.Version;
            var newer = Envelope("cold-" + decodeMs, epoch: decodeMs + 10, generation: 10); newer.Revision = decodeMs + 2;
            cold.SetEnvelope(newer);
            Check(!cold.CanPublish(token), "old cold decode admitted under new identity at " + decodeMs + "ms");
            Check(cold.CanPublish(cold.Version), "current complete frame not publishable");
        }
        var live = Envelope("live", epoch: 1000, generation: 20); live.EventId = "old-bite"; live.Revision = 1000;
        cold.SetEnvelope(live); cold.SuppressOneShots = true;
        var nextBite = Envelope("live", epoch: 1000, generation: 20); nextBite.EventId = "new-bite"; nextBite.Revision = 1001;
        cold.SetEnvelope(nextBite);
        Check(Accept(cold, "bite.onset", "hooked"), "new authority bite suppressed after visible resume");
        for (var action = 1002; action < 1700; action++)
        {
            var next = Envelope("long-session-" + action, epoch: 1000, generation: 20); next.Revision = action;
            cold.SetEnvelope(next);
        }
        Check(cold.Envelope.ActionId == "long-session-1699", "long session stopped at retired-action cap");
        live.Revision = 1000;
        Check(!cold.SetEnvelope(live), "old revision bypassed bounded retired ledger");
        Config();
        Console.WriteLine("PASS " + passed + " water effects event/budget/config assertions (platform-neutral; no Windows render claims)");
    }
    static void Config()
    {
        using var effects = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"effects.json")));
        var cues = new Dictionary<string, JsonElement>();
        foreach (var cue in effects.RootElement.GetProperty("cues").EnumerateArray()) cues.Add(cue.GetProperty("cue").GetString(), cue);
        foreach (var cue in new[] { "float.contact", "waiting.ripple", "bite.onset", "fish.struggle", "fish.surface", "groundbait.contact" })
            Check(cues.ContainsKey(cue), "policy cue absent from shipped effects.json: " + cue);
        foreach (var marker in cues["fish.surface"].GetProperty("markers").EnumerateArray())
            Check(OverlayWaterEffectsPolicy.Allowed(marker.GetString(), "effort"), "FX05 marker alias not supported");
        Check(cues.Count == 6, "withdrawn effects returned");
        foreach (var tier in effects.RootElement.GetProperty("tiers").EnumerateObject())
        {
            var t = tier.Value;
            Check(t.GetProperty("dropCount").GetInt32() + t.GetProperty("arcCount").GetInt32() + t.GetProperty("crownCount").GetInt32()
                == t.GetProperty("spawnCount").GetInt32(), "hidden draw count in tier " + tier.Name);
            Check(t.GetProperty("spawnCount").GetInt32() <= 10, "tier breaks reserve");
            var duration = t.GetProperty("durationMs").GetInt32();
            var struggle = OverlayWaterEffectsPolicy.Duration("fish.struggle", duration);
            Check(struggle >= 300 && struggle <= 600, "real FX04 tier duration out of 300–600ms range");
            Check(OverlayWaterEffectsPolicy.Duration("bite.onset", duration) == duration, "bite lost authored tier timing");
        }
        using var source = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"source-water-markers.json")));
        using var canonical = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"presentation-markers.json")));
        foreach (var marker in canonical.RootElement.GetProperty("markers").EnumerateArray())
            Check(marker.GetProperty("marker").GetString() != "fish.surface" && marker.GetProperty("marker").GetString() != "fish.slap",
                "canonical H/I markers fabricated a submerged fish slap");
        foreach (var marker in source.RootElement.GetProperty("markers").EnumerateArray())
        {
            var cue = marker.GetProperty("cue").GetString();
            Check(cue != "fish.surface" && cue != "fish.slap", "approved submerged clips fabricated a slap");
            if (cue == "float.contact" || cue == "groundbait.contact")
            {
                var found = false;
                foreach (var shared in canonical.RootElement.GetProperty("markers").EnumerateArray())
                    if (shared.GetProperty("marker").GetString() == cue && shared.GetProperty("clip").GetString() == marker.GetProperty("clip").GetString() &&
                        shared.GetProperty("sourceIndex").GetInt32() == marker.GetProperty("source").GetInt32()) found = true;
                Check(found, "H derived one-shot diverged from canonical H/I source marker");
            }
            var key = marker.GetProperty("clip").GetString() + "/" + marker.GetProperty("source").GetInt32();
            Check(source.RootElement.GetProperty("frames").GetProperty(key).TryGetProperty(marker.GetProperty("layer").GetString(), out _), "marker missing actual source alpha bounds");
        }
    }
}
