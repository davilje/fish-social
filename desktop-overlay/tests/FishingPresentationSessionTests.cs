using System;
using FishSocial.Desktop;
using FishSocial.Desktop.Pet;

static class FishingPresentationSessionTests
{
    static int checks;
    static void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
    static void Main()
    {
        var s = new FishingPresentationSession();
        s.Reset("pond"); s.SetOwner(true);
        s.Observe("self", "a", "waiting0", 1, "waiting", "spot", "orange", true);
        Check(s.Envelope("self", "waiting", "spot").source == "snapshot", "snapshot silent");
        var baseline = s.Envelope("self", "waiting", "spot");
        Check(s.TryConsume(baseline, "waiting.ripple", 0, "visual"), "snapshot restores only visual continuous");
        Check(!s.TryConsume(baseline, "waiting.ripple", 0, "audio"), "snapshot cannot replay sound");
        Check(!s.TryConsume(baseline, "float.contact", 0, "visual"), "snapshot cannot replay entry splash");
        s.Observe("self", "a", "waiting0", 1, "waiting", "spot", "orange", false);
        Check(s.Envelope("self", "waiting", "spot").source == "snapshot", "duplicate update cannot replay baseline");
        var predicted = s.Predict("self", "baiting", 1, "overlay-process-1");
        Check(s.Predict("self", "baiting", 1, "overlay-process-1") == predicted, "duplicate command identity stable");
        s.Observe("self", "b", "bait1", 2, "baiting", "spot", "orange", false);
        Check(s.Envelope("self", "baiting", "spot").actionId == predicted, "authority aliases prediction");
        s.Observe("self", "b", "cast1", 3, "casting", "spot", "orange", false);
        Check(s.Envelope("self", "casting", "spot").actionId == predicted, "cast retains alias");
        s.Observe("self", "b", "bite1", 4, "hooked", "spot", "orange", false);
        Check(s.Envelope("self", "hooked", "spot").eventId == "bite1", "bite uses stable authority event");
        s.Observe("self", "b", "cast1", 3, "casting", "spot", "orange", false);
        Check(s.Envelope("self", "hooked", "spot").eventId == "bite1", "out of order ignored");
        var live = s.Envelope("self", "hooked", "spot");
        Check(!s.TryConsume(live, "invented", 0, "visual"), "unknown cue rejected");
        Check(s.TryConsume(live, "bite.onset", 0, "visual"), "visual first consumption");
        Check(!s.TryConsume(live, "bite.onset", 0, "visual"), "visual duplicate rejected");
        Check(s.TryConsume(live, "bite.onset", 0, "audio"), "audio independent from visual");
        Check(s.TryConsume(live, "fish.struggle", 1, "visual"), "hooked struggle");
        s.Cancel("self");
        Check(!s.TryConsume(live, "fish.struggle", 2, "visual"), "old generation cannot consume");
        s.Observe("self", "b", "lateBite", 5, "hooked", "spot", "orange", false);
        Check(s.Envelope("self", "hooked", "spot").source == "snapshot", "cancel beats late marker");
        s.Observe("self", "c", "auto1", 6, "baiting", "spot", "orange", false);
        Check(s.Envelope("self", "baiting", "spot").actionId == "c", "new real round distinct");
        Check(s.Envelope("self", "baiting", "spot").source == "authoritative", "new round clears old cancellation");
        var generation = s.Envelope("self", "baiting", "spot").actorGeneration;
        s.Observe("self", "c", "auto1", 6, "baiting", "spot", "calico", false);
        Check(s.Envelope("self", "baiting", "spot").actorGeneration > generation, "skin invalidates callbacks");
        var epoch = s.OwnerEpoch; s.SetOwner(false);
        Check(s.OwnerEpoch > epoch && s.Envelope("self", "baiting", "spot").source == "snapshot", "owner switch silent");
        Check(s.Owner == "unity", "single owner fallback");
        var oldSession = s.SessionId; s.Reset("pond2");
        Check(s.SessionId != oldSession && s.Envelope("self", "hooked", "spot") == null, "reconnect clears epoch");
        s.Observe("self", "a", "w", 1, "waiting", "spot", "orange", true);
        s.Predict("self", "baiting", 2); s.Cancel("self");
        s.Observe("self", "serverDelayed", "delayedAck", 2, "baiting", "spot", "orange", false);
        Check(s.Envelope("self", "baiting", "spot").source == "snapshot", "cancel before authority ack");
        var bodyCases = new (float?, string)[] { (null,"S"), (float.NaN,"S"), (16,"S"), (16.1f,"M"), (32,"M"), (32.1f,"L"), (56,"L"), (56.1f,"XL") };
        foreach (var pair in bodyCases) Check(FishingPresentationSession.VisibleBodyWaterSizeTier(pair.Item1) == pair.Item2, "visible body tier " + pair);
        var tierCases = new (float?, string)[] { (null,"S"), (float.NaN,"S"), (float.PositiveInfinity,"S"), (-1,"S"), (.29f,"S"), (.30f,"M"), (.79f,"M"), (.80f,"L"), (1.49f,"L"), (1.50f,"XL") };
        foreach (var pair in tierCases) Check(FishingPresentationSession.VisibleWaterSizeTier(pair.Item1) == pair.Item2, "tier " + pair);
        Check(s.Envelope("self", "baiting", "spot").waterSizeTier == "S", "hidden length never consulted");
        s.RetainActors(Array.Empty<string>());
        Check(s.Envelope("self", "waiting", "spot") == null, "departed actor released");
        s.Reset("pond");
        for (var round = 1; round <= 1000; round++)
        {
            s.Observe("self", "action" + round, "event" + round, round, "groundbaiting", "spot", "orange", false);
            var e = s.Envelope("self", "groundbaiting", "spot");
            Check(s.TryConsume(e, "groundbait.contact", 0, "visual"), "old actions evicted " + round);
            Check(!s.TryConsume(e, "fish.struggle", 0, "visual"), "struggle stage restriction " + round);
            for (var loop = 1; loop < 1000; loop++)
                if (!s.TryConsume(e, "groundbait.contact", loop, "visual")) throw new Exception("long loop capacity");
        }
        foreach (var firstPhase in new[] { "waiting", "hooked", "effort" })
        {
            s.Reset("pond");
            s.Observe("self", "previous", "seated", 1, "seated", "spot", "orange", true);
            var action = s.Predict("self", "baiting", 42, "process");
            var castEnvelope = s.Envelope("self", "casting", "spot");
            Check(s.TryConsume(castEnvelope, "float.contact", 0, "visual"), "predicted contact " + firstPhase);
            s.Observe("self", "server-round", "server-event", 2, firstPhase, "spot", "orange", false);
            var reconciled = s.Envelope("self", firstPhase, "spot");
            Check(reconciled.actionId == action, "lost start packets reconcile " + firstPhase);
            Check(reconciled.source == "authoritative", "lost start packets stay live " + firstPhase);
            Check(!s.TryConsume(reconciled, "float.contact", 0, "visual"), "predicted/authority contact once " + firstPhase);
            if (firstPhase != "waiting")
            {
                Check(s.TryConsume(reconciled, "bite.onset", 0, "visual"), "authoritative bite after loss " + firstPhase);
                Check(!s.TryConsume(reconciled, "bite.onset", 0, "visual"), "bite duplicate after loss " + firstPhase);
            }
            s.Observe("self", "server-rebait", "next", 3, "baiting", "spot", "orange", false);
            Check(s.Envelope("self", "baiting", "spot").actionId == "server-rebait", "auto-rebait new round " + firstPhase);
            Check(s.Envelope("self", "baiting", "spot").source == "authoritative", "auto-rebait still live " + firstPhase);
        }
        s.Reset("pond");
        s.Observe("self", "a", "idle", 1, "seated", "spot", "orange", true);
        var baitAction = s.Predict("self", "groundbaiting", 9);
        s.Observe("self", "fishing-authority", "waiting", 2, "waiting", "spot", "orange", false);
        Check(s.Envelope("self", "waiting", "spot").actionId != baitAction, "groundbait cannot alias fishing family");
        s.Reset("pond");
        s.Observe("self", "a", "idle", 1, "seated", "spot", "orange", true);
        var fishingAction = s.Predict("self", "baiting", 10);
        s.Observe("self", "groundbait-authority", "groundbait", 2, "groundbaiting", "spot", "orange", false);
        Check(s.Envelope("self", "groundbaiting", "spot").actionId != fishingAction, "fishing cannot alias groundbait family");
        s.Reset("pond");
        s.Observe("self", "a", "idle", 1, "seated", "spot", "orange", true);
        s.Predict("self", "baiting", 11); s.Cancel("self");
        s.Observe("self", "delayed-authority", "bite", 2, "hooked", "spot", "orange", false);
        Check(s.Envelope("self", "hooked", "spot").source == "snapshot", "cancel before missing-phase authority bite wins");
        s.Observe("self", "next-real-round", "next", 3, "baiting", "spot", "orange", false);
        Check(s.Envelope("self", "baiting", "spot").source == "authoritative", "later real round clears cancelled lost-packet prediction");
        // Retrieval is the only water cue admitted during actual stop/resolve publication.
        foreach (var phase in new[] { "stopping", "resolving" })
        {
            s.Reset("pond");
            s.Observe("self", "reel", "waiting", 1, "waiting", "spot", "orange", false);
            if (phase == "stopping") s.BeginRetrieve("self");
            else s.Observe("self", "reel", "resolve", 2, phase, "spot", "orange", false);
            var reel = s.Envelope("self", phase, "spot");
            Check(s.TryConsume(reel, "retrieve.contact", 0, "visual"), "real retrieval eligible " + phase);
            Check(!s.TryConsume(reel, "retrieve.contact", 0, "visual"), "retrieval one-shot " + phase);
            Check(!s.TryConsume(reel, "retrieve.contact", 1, "audio"), "retrieval cannot loop " + phase);
            Check(s.TryConsume(reel, "fish.surface", 0, "visual"), "real surface contact allowed in reel window " + phase);
            Check(!s.TryConsume(reel, "fish.surface", 1, "visual"), "retrieve surface cannot loop " + phase);
            Check(!s.TryConsume(reel, "fish.slap", 0, "visual"), "terminal window cannot invent slap " + phase);
            Check(!s.TryConsume(reel, "bite.onset", 0, "visual"), "terminal window rejects bite " + phase);
            Check(!s.TryConsume(reel, "retrieve.start", 0, "visual"), "audio start is not a water contact " + phase);
            s.Cancel("self");
            s.Observe("self", "reel", "late", 3, phase, "spot", "orange", false);
            Check(!s.TryConsume(s.Envelope("self", phase, "spot"), "retrieve.contact", 0, "ipc-relay"), "hard cancel beats retrieve " + phase);
        }
        foreach (var phase in new[] { "stopping", "resolving", "result", "catch", "escape", "seated", "idle", "waiting" })
        {
            s.Reset("pond");
            s.Observe("self", "reel", "e", 1, phase, "spot", "orange", phase == "stopping" || phase == "resolving");
            Check(!s.TryConsume(s.Envelope("self", phase, "spot"), "retrieve.contact", 0, "visual"), "snapshot/other phase retrieval denied " + phase);
            Check(!s.TryConsume(s.Envelope("self", phase, "spot"), "fish.surface", 0, "visual"), "snapshot/other phase surface denied " + phase);
        }
        s.Reset("pond");
        s.Observe("self", "old", "idle", 1, "seated", "spot", "orange", true);
        var quickStop = s.Predict("self", "baiting", 99);
        s.BeginRetrieve("self");
        Check(s.TryConsume(s.Envelope("self", "stopping", "spot"), "retrieve.contact", 0, "visual"), "quick predicted stop contact");
        s.Observe("self", "first-server-ack", "resolve", 2, "resolving", "spot", "orange", false);
        var resolveAck = s.Envelope("self", "resolving", "spot");
        Check(resolveAck.actionId == quickStop, "quick stop preserves prediction action on first resolve ack");
        Check(!s.TryConsume(resolveAck, "retrieve.contact", 0, "visual"), "quick stop resolve ack cannot replay contact");
        var budget = new UnityWaterEffectBudget();
        var recycled = 0;
        for (var i = 0; i < 6; i++) Check(budget.Admit("remote" + i, false, 2, 10, () => recycled++) != null, "remote admission");
        Check(budget.Instances == 12 && budget.Groups == 6, "remote budget reserves own capacity");
        Check(budget.Admit("own", true, 6, 100, () => recycled++) != null, "own first reserved group");
        Check(budget.Admit("own", true, 6, 100, () => recycled++) != null, "own second reserved group");
        Check(budget.Instances == 24 && budget.Groups == 8, "pool maximum");
        Check(budget.Admit("newRemote", false, 3, 1, () => recycled++) == null, "low priority cannot evict");
        Check(budget.Admit("own", true, 8, 100, () => recycled++) != null && budget.Groups <= 8 && budget.Instances <= 24, "replacement recycles without queue");
        Check(recycled > 0, "recycle called");
        var owner = new FishingPresentationEnvelope { owner = "unity" };
        Check(UnityWaterEffectBudget.CanRender(owner, true, "standard", true, "bite.onset"), "injected real contact draws fallback");
        Check(!UnityWaterEffectBudget.CanRender(owner, false, "standard", true, "bite.onset"), "absent contact silently drops");
        owner.owner = "overlay";
        Check(!UnityWaterEffectBudget.CanRender(owner, true, "standard", true, "bite.onset"), "native owner excludes fallback");
        owner.owner = "unity";
        Check(!UnityWaterEffectBudget.CanRender(owner, true, "off", true, "bite.onset"), "off quality drops");
        Check(!UnityWaterEffectBudget.CanRender(owner, true, "reduced", false, "bite.onset"), "reduced removes others");
        Check(!UnityWaterEffectBudget.CanRender(owner, true, "reduced", true, "fish.struggle"), "reduced removes ongoing disturbance");
        Check(UnityWaterEffectBudget.CanRender(owner, true, "reduced", true, "bite.onset"), "reduced retains own key cue");
        Check(UnityWaterEffectBudget.CanRender(owner, true, "reduced", true, "retrieve.contact"), "reduced retains own real retrieve arcs");
        Console.WriteLine("Presentation session: " + checks + " checks passed.");
    }
}
