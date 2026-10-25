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
            foreach (var cue in new[] { "float.contact", "waiting.ripple", "bite.onset", "fish.struggle", "fish.slap", "groundbait.contact", "FX06", "FX07", "FX09" })
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
        ContactRules();
        Config();
        Console.WriteLine("PASS " + passed + " water effects event/budget/config assertions (platform-neutral; no Windows render claims)");
    }
    static void ContactRules()
    {
        foreach (var source in new[] { "authoritative", "prediction", "snapshot" })
        foreach (var phase in new[] { "stopping", "resolving", "casting", "waiting", "hooked", "catch", "reset", "seated" })
        {
            var p = new OverlayWaterEffectsPolicy(); p.SetEnvelope(Envelope(source: source));
            var allowed = source != "snapshot" && (phase == "stopping" || phase == "resolving");
            Check(Accept(p, "retrieve.contact", phase) == allowed, "retrieve contact source/phase gate: " + source + "/" + phase);
            Check(!Accept(p, "retrieve.contact", phase), "retrieve repeated source zero");
            Check(!Accept(p, "retrieve.start", phase), "audio-only retrieve.start invented a water contact");
        }
        var stop = new OverlayWaterEffectsPolicy(); stop.SetEnvelope(Envelope());
        Check(!Accept(stop, "retrieve.contact", "stopping", occurrence: 1), "retrieve action repeated a later loop");
        Check(!Accept(stop, "retrieve.contact", "stopping", contact: false), "non-contact retrieve sprayed");
        Check(Accept(stop, "retrieve.contact", "stopping"), "false contact consumed later real contact");
        stop.SetEnvelope(null);
        Check(!Accept(stop, "retrieve.contact", "stopping"), "hard cancellation retained retrieve permission");
        foreach (var source in new[] { "authoritative", "prediction", "snapshot" })
        foreach (var phase in new[] { "stopping", "resolving", "catch", "reset", "seated" })
        {
            var surface = new OverlayWaterEffectsPolicy(); surface.SetEnvelope(Envelope(source: source));
            Check(!Accept(surface, "fish.surface", phase, contact: false), "fish surface invented contact");
            var allowed = source != "snapshot" && (phase == "stopping" || phase == "resolving");
            Check(Accept(surface, "fish.surface", phase) == allowed, "retrieve surface phase/source gate");
            Check(!Accept(surface, "fish.surface", phase), "fish surface replayed within action");
            Check(!Accept(surface, "fish.surface", phase, occurrence: 1), "later retrieve occurrence sprayed");
            Check(!Accept(surface, "fish.slap", phase), "slap reused retrieve surface exception");
            surface.SetEnvelope(null);
            Check(!Accept(surface, "fish.surface", phase), "hard cancel retained surface permission");
        }
        foreach (var cue in new[] { "waiting.ripple" })
        {
            var shape = OverlayWaterEffectsPolicy.Components(cue, 2, 2, 2, true, false);
            Check(shape.Arcs == 2 && shape.Crowns == 0 && shape.Drops == 0, "ripple cue created splash/drop " + cue);
        }
        foreach (var cue in new[] { "float.contact", "groundbait.contact", "retrieve.contact", "fish.surface", "fish.slap", "bite.onset", "fish.struggle" })
        {
            Check(OverlayWaterEffectsPolicy.Components(cue, 1, 1, 1, true, false).Count == 3, "real contact lost small splash " + cue);
            var reduced = OverlayWaterEffectsPolicy.Components(cue, 1, 1, 1, true, true);
            Check(reduced.Count == 1 && reduced.Arcs == 1, "reduced contact creates splash/drop");
            Check(OverlayWaterEffectsPolicy.Components(cue, 2, 2, 2, false, false).Count == 1, "remote contact creates splash/drop");
        }
    }
    static void Config()
    {
        using var effects = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"effects.json")));
        var cues = new Dictionary<string, JsonElement>();
        foreach (var cue in effects.RootElement.GetProperty("cues").EnumerateArray()) cues.Add(cue.GetProperty("cue").GetString(), cue);
        foreach (var cue in new[] { "float.contact", "waiting.ripple", "bite.onset", "fish.struggle", "fish.surface", "groundbait.contact", "retrieve.contact" })
            Check(cues.ContainsKey(cue), "policy cue absent from shipped effects.json: " + cue);
        foreach (var marker in cues["fish.surface"].GetProperty("markers").EnumerateArray())
            Check(OverlayWaterEffectsPolicy.Allowed(marker.GetString(), "effort"), "FX05 marker alias not supported");
        var surfacePhases = new HashSet<string>();
        foreach(var phase in cues["fish.surface"].GetProperty("allowedPhases").EnumerateArray()) surfacePhases.Add(phase.GetString());
        Check(surfacePhases.SetEquals(new[]{"hooked","effort","resolving","stopping"}), "real FX05 config blocks retrieve fish exit");
        Check(cues.Count == 7, "unexpected effect family count");
        Check(cues["bite.onset"].GetProperty("renderKind").GetString() == "splash" &&
            cues["fish.struggle"].GetProperty("renderKind").GetString() == "splash", "shipped bite/struggle lacks splash");
        Check(cues["groundbait.contact"].GetProperty("spawnCount").GetInt32() == 4, "groundbait small splash count mismatch");
        foreach (var phase in cues["retrieve.contact"].GetProperty("allowedPhases").EnumerateArray())
            Check(phase.GetString() == "resolving" || phase.GetString() == "stopping", "retrieve config permits results or idle");
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
        var retrieveSurfaceMarkers = 0;
        foreach (var marker in source.RootElement.GetProperty("markers").EnumerateArray())
        {
            var cue = marker.GetProperty("cue").GetString();
            Check(cue != "fish.slap", "approved clips fabricated a slap");
            if(cue == "fish.surface")
            {
                retrieveSurfaceMarkers++;
                Check(marker.GetProperty("clip").GetString() == "reel" && marker.GetProperty("source").GetInt32() == 0 &&
                    marker.GetProperty("layer").GetString() == "fish", "fish surface missing actual retrieve fish layer gate");
            }
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
        Check(retrieveSurfaceMarkers == 1, "real water metadata missing unique retrieve fish surface candidate");
        Geometry(source.RootElement);
    }
    static void Geometry(JsonElement source)
    {
        using var contacts = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"water-contact.json")));
        using var motion = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"motion-samples.json")));
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        const double surface = 134, scale = 139.0 / 512;
        var origin = 101 - 450 * scale;
        var bait = source.GetProperty("frames").GetProperty("groundbait/5").GetProperty("bait").GetProperty("bounds");
        var bottom = bait[3].GetDouble();
        var first = origin + bottom * scale + OverlayFishingWaterContact.GroundbaitOffset(5, scale, origin, surface);
        var next = origin + bottom * scale + OverlayFishingWaterContact.GroundbaitOffset(5 + 1.0 / 3, scale, origin, surface);
        Check(first < surface - scale && next >= surface, "real bait source5 marker substituted for actual 620ms geometry contact");
        var elapsed = 0; var sampledContact = false;
        foreach (var sample in motion.RootElement.GetProperty("Clips").GetProperty("groundbait").GetProperty("Frames").EnumerateArray())
        {
            if (sample.GetProperty("Position").GetDouble() == 5 + 1.0 / 3)
            {
                Check(elapsed == 620, "actual groundbait first contact timing changed");
                var transform = sample.GetProperty("Transforms")[8];
                Check(transform[4].GetDouble() == 0 && transform[5].GetDouble() == 0, "GB contact needs additional source transform");
                sampledContact = true;
            }
            elapsed += sample.GetProperty("DurationMs").GetInt32();
        }
        Check(sampledContact, "real authored GB contact interpolation missing");
        foreach (var clip in new[] { "reel", "reel_empty" })
        {
            var frame = JsonSerializer.Deserialize<FishingContactFrame>(contacts.RootElement.GetProperty(clip)[0].GetRawText(), options);
            var pose = OverlayFishingWaterContact.Solve(frame, scale, origin, surface, 1);
            var data = source.GetProperty("frames").GetProperty(clip + "/0");
            if (clip == "reel")
            {
                var min = double.MaxValue; var max = double.MinValue;
                foreach (var row in data.GetProperty("line").GetProperty("rowCenters").EnumerateArray())
                { var y = origin + pose.LinePivot + (row[0].GetDouble() * scale - pose.LinePivot) * pose.LineScale; min = Math.Min(min,y); max = Math.Max(max,y); }
                Check(min < surface && max > surface, "reel source0 true line does not cross water");
                Check(origin + data.GetProperty("fish").GetProperty("bounds")[1].GetDouble() * scale + pose.FishOffset > surface,
                    "retrieve incorrectly depends on fish exit");
            }
            else
            {
                var bounds = data.GetProperty("bobber").GetProperty("bounds");
                Check(origin + bounds[1].GetDouble()*scale + pose.BobberOffset <= surface &&
                    origin + bounds[3].GetDouble()*scale + pose.BobberOffset >= surface, "reel_empty source0 actual bobber misses surface");
            }
        }
        var fishBounds = source.GetProperty("frames").GetProperty("reel/0").GetProperty("fish").GetProperty("bounds");
        var reelElapsed = 0; var crossings = 0;
        foreach (var sample in motion.RootElement.GetProperty("Clips").GetProperty("reel").GetProperty("Frames").EnumerateArray())
        {
            if(sample.GetProperty("Source").GetInt32() != 0) break;
            var position = sample.GetProperty("Position").GetDouble();
            var frame = JsonSerializer.Deserialize<FishingContactFrame>(sample.GetProperty("Contact").GetRawText(),options);
            var pose = OverlayFishingWaterContact.Solve(frame,scale,origin,surface,OverlayFishingWaterContact.Weight("reel",position,6));
            var matrix = sample.GetProperty("Transforms")[5];
            var left = double.MaxValue; var right = double.MinValue; var top = double.MaxValue; var bottomY = double.MinValue;
            foreach(var x in new[]{fishBounds[0].GetDouble(),fishBounds[2].GetDouble()})
            foreach(var y in new[]{fishBounds[1].GetDouble(),fishBounds[3].GetDouble()})
            {
                var tx = (matrix[0].GetDouble()*x + matrix[2].GetDouble()*y + matrix[4].GetDouble()) * 174/640;
                var ty = origin + (matrix[1].GetDouble()*x + matrix[3].GetDouble()*y + matrix[5].GetDouble()) * scale + pose.FishOffset;
                left = Math.Min(left,tx); right=Math.Max(right,tx);top=Math.Min(top,ty);bottomY=Math.Max(bottomY,ty);
            }
            if(top <= surface && bottomY >= surface)
            {
                crossings++;
                Check(reelElapsed == 67, "retrieve fish first real crossing changed or fixed-delay substituted");
                Check(OverlayWaterEffectsPolicy.VisibleTier(right-left) == "L", "visible rotated source fish size not used for surface tier");
            }
            else Check(top > surface, "source0 no longer starts below water");
            reelElapsed += sample.GetProperty("DurationMs").GetInt32();
        }
        Check(crossings == 1, "retrieve source0 fish crossing missing or duplicated");
    }
}
