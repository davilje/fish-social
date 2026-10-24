using System;
using System.Collections.Generic;

namespace FishSocial.Desktop
{
    [Serializable]
    public sealed class FishingPresentationEnvelope
    {
        public string presentationSessionId, actorId, pondId, spotId;
        public string actionId, eventId, source, owner, waterSizeTier = "S";
        public long ownerEpoch, revision;
        public int actorGeneration;
    }

    /// <summary>H/I identity and owner arbitration. No audio device or animation delays live here.</summary>
    public sealed class FishingPresentationSession
    {
        sealed class Actor
        {
            public string Action, Event, AuthorityAction, Phase, Spot, Skin, Source, PredictionCommand, PredictionFamily;
            public long Revision;
            public int Generation = 1;
            public bool Predicted, Cancelled;
        }
        readonly Dictionary<string, Actor> actors = new Dictionary<string, Actor>();
        readonly Dictionary<string, long> consumed = new Dictionary<string, long>();
        long commandOrdinal;
        public string SessionId { get; private set; } = Guid.NewGuid().ToString("N");
        public string PondId { get; private set; } = string.Empty;
        public string Owner { get; private set; } = "unity";
        public long OwnerEpoch { get; private set; } = 1;

        public void Reset(string pondId)
        {
            SessionId = Guid.NewGuid().ToString("N");
            PondId = pondId ?? string.Empty;
            OwnerEpoch++;
            actors.Clear();
            consumed.Clear();
        }

        public void SetOwner(bool nativeVisible)
        {
            var next = nativeVisible ? "overlay" : "unity";
            if (next == Owner) return;
            Owner = next;
            OwnerEpoch++;
            consumed.Clear();
            // A newly visible owner restores state; it never replays the previous owner's one-shots.
            foreach (var actor in actors.Values) actor.Source = "snapshot";
        }

        public void Observe(string actorId, string actionId, string eventId, long revision,
            string phase, string spotId, string skin, bool snapshot)
        {
            if (string.IsNullOrEmpty(actorId)) return;
            if (!actors.TryGetValue(actorId, out var actor))
            {
                if (actors.Count >= 64) return;
                actor = new Actor();
                actors.Add(actorId, actor);
            }
            if (!snapshot && revision > 0 && actor.Revision > revision) return;
            var previousAction = actor.Action;
            var cancelledAuthorityAction = actor.AuthorityAction;
            var firstAuthorityOfPrediction = actor.Predicted && string.IsNullOrEmpty(actor.AuthorityAction) &&
                (SameActionFamily(actor.PredictionFamily, phase) ||
                    actor.PredictionFamily == "fishing" && actor.Phase == "stopping" && IsRetrievePhase(phase));
            var cancelledAwaitingAcknowledgement = actor.Cancelled && firstAuthorityOfPrediction;
            var changed = !string.IsNullOrEmpty(eventId) && eventId != actor.Event;
            var restarted = actor.Spot != null && (actor.Spot != spotId || actor.Skin != skin);
            if (restarted) actor.Generation++;
            if (snapshot || restarted) actor.Source = "snapshot";
            else if (changed) actor.Source = "authoritative";
            // The first start packets can be lost. Bind the first non-ending phase of the same
            // predicted family; once bound, a different authority action is always a new round.
            if (firstAuthorityOfPrediction)
                actor.AuthorityAction = actionId;
            else if (!actor.Predicted || actor.AuthorityAction != actionId)
            {
                actor.Action = actionId;
                actor.AuthorityAction = actionId;
                actor.Predicted = false;
            }
            if (previousAction != actor.Action) ForgetConsumed(actorId);
            var remainsCancelled = actor.Cancelled && (cancelledAwaitingAcknowledgement ||
                !string.IsNullOrEmpty(cancelledAuthorityAction) && cancelledAuthorityAction == actionId);
            actor.Event = eventId;
            actor.Revision = revision;
            actor.Phase = phase;
            actor.Spot = spotId;
            actor.Skin = skin;
            if (IsEnding(phase) && !IsRetrievePhase(phase) || remainsCancelled)
            {
                if (!actor.Cancelled) actor.Generation++;
                actor.Cancelled = true;
            }
            else actor.Cancelled = false;
        }

        public string Predict(string actorId, string phase, long commandId, string commandSessionId = null)
        {
            if (string.IsNullOrEmpty(actorId)) return string.Empty;
            if (!actors.TryGetValue(actorId, out var actor))
            {
                actor = new Actor();
                actors.Add(actorId, actor);
            }
            var commandKey = (commandSessionId ?? SessionId) + ":" + commandId;
            if (actor.PredictionCommand == commandKey) return actor.Action;
            actor.PredictionCommand = commandKey;
            // command IDs are process-local and can repeat after an overlay restart; namespace and ordinal are required.
            ForgetConsumed(actorId);
            actor.Action = SessionId + ":command:" + commandKey + ":" + (++commandOrdinal);
            actor.AuthorityAction = null;
            actor.Event = null;
            actor.Phase = phase;
            actor.PredictionFamily = phase == "groundbaiting" ? "groundbait" : "fishing";
            actor.Source = "prediction";
            actor.Predicted = true;
            actor.Cancelled = false;
            actor.Generation++;
            return actor.Action;
        }

        // An active stop keeps only its actual reel contact eligible. Hard cancellation remains stronger.
        public void BeginRetrieve(string actorId)
        {
            if (string.IsNullOrEmpty(actorId) || !actors.TryGetValue(actorId, out var actor) || actor.Cancelled) return;
            actor.Phase = "stopping";
            actor.Source = "prediction";
        }

        public void Cancel(string actorId)
        {
            if (string.IsNullOrEmpty(actorId) || !actors.TryGetValue(actorId, out var actor)) return;
            actor.Generation++;
            actor.Cancelled = true;
            actor.Source = "snapshot";
        }

        public void RetainActors(IEnumerable<string> actorIds)
        {
            var keep = new HashSet<string>(actorIds);
            var removed = new List<string>();
            foreach (var key in actors.Keys) if (!keep.Contains(key)) removed.Add(key);
            foreach (var key in removed) { actors.Remove(key); ForgetConsumed(key); }
        }

        public FishingPresentationEnvelope Envelope(string actorId, string phase, string spotId)
        {
            if (string.IsNullOrEmpty(actorId) || !actors.TryGetValue(actorId, out var actor)) return null;
            return new FishingPresentationEnvelope
            {
                presentationSessionId = SessionId, actorId = actorId, pondId = PondId, spotId = spotId,
                actionId = actor.Action, eventId = actor.Event,
                source = actor.Cancelled || IsEnding(phase) && !IsRetrievePhase(phase) ? "snapshot" : actor.Source ?? "snapshot",
                owner = Owner, ownerEpoch = OwnerEpoch, actorGeneration = actor.Generation, revision = actor.Revision,
                // No size from phaseContext, debug fish, future inventory, or hidden pending catch is allowed.
                waterSizeTier = "S",
            };
        }

        void ForgetConsumed(string actorId)
        {
            var prefix = actorId + "|";
            var remove = new List<string>();
            foreach (var key in consumed.Keys) if (key.StartsWith(prefix, StringComparison.Ordinal)) remove.Add(key);
            foreach (var key in remove) consumed.Remove(key);
        }

        public static bool IsWaterMarker(string marker)
        {
            return marker == "float.contact" || marker == "waiting.ripple" || marker == "bite.onset" ||
                marker == "fish.struggle" || marker == "fish.surface" || marker == "fish.slap" || marker == "groundbait.contact" || marker == "retrieve.contact";
        }

        /// <summary>Each consumer has its own bit; monotonic loop occurrences keep a long action bounded.</summary>
        public bool TryConsume(FishingPresentationEnvelope envelope, string marker, long occurrence, string consumer)
        {
            var continuous = marker == "waiting.ripple" || marker == "fish.struggle";
            if (!IsWaterMarker(marker) || envelope == null || envelope.source == "snapshot" && (!continuous || consumer != "visual" && consumer != "unity-visual-marker") || occurrence < 0 ||
                envelope.presentationSessionId != SessionId || envelope.owner != Owner ||
                envelope.ownerEpoch != OwnerEpoch || string.IsNullOrEmpty(envelope.actorId) ||
                !actors.TryGetValue(envelope.actorId, out var actor) || actor.Cancelled ||
                envelope.actorGeneration != actor.Generation || envelope.actionId != actor.Action ||
                envelope.pondId != PondId || envelope.spotId != actor.Spot) return false;
            if (envelope.source != "authoritative" && envelope.source != "prediction" && envelope.source != "snapshot") return false;
            if (envelope.source == "prediction" && marker != "float.contact" && marker != "groundbait.contact" && marker != "retrieve.contact" &&
                !(marker == "fish.surface" && IsRetrievePhase(actor.Phase) && occurrence == 0)) return false;
            if (marker == "retrieve.contact" && (!IsRetrievePhase(actor.Phase) || occurrence != 0)) return false;
            if (marker == "bite.onset" && (envelope.eventId != actor.Event || actor.Phase != "hooked" && actor.Phase != "effort")) return false;
            if ((marker == "fish.struggle" || marker == "fish.slap") && actor.Phase != "hooked" && actor.Phase != "effort") return false;
            if (marker == "fish.surface" && (IsRetrievePhase(actor.Phase) ? occurrence != 0 : actor.Phase != "hooked" && actor.Phase != "effort")) return false;
            if (marker == "groundbait.contact" && actor.Phase != "groundbaiting") return false;
            if ((marker == "float.contact" || marker == "waiting.ripple") &&
                actor.Phase != "casting" && actor.Phase != "waiting" && actor.Phase != "baiting") return false;
            var identity = marker == "bite.onset" ? envelope.eventId : envelope.actionId;
            if (string.IsNullOrEmpty(identity) || string.IsNullOrEmpty(marker) || string.IsNullOrEmpty(consumer)) return false;
            var key = envelope.actorId + "|" + identity + "|" + marker + "|" + consumer;
            if (consumed.TryGetValue(key, out var last))
            {
                if (occurrence <= last) return false;
            }
            else if (consumed.Count >= 512) return false;
            consumed[key] = occurrence;
            return true;
        }

        static bool SameActionFamily(string family, string phase)
        {
            return family == "groundbait" ? phase == "groundbaiting" : family == "fishing" &&
                (phase == "baiting" || phase == "casting" || phase == "waiting" || phase == "hooked" || phase == "effort");
        }

        public static bool IsRetrievePhase(string phase) => phase == "stopping" || phase == "resolving";

        public static bool IsEnding(string phase)
        {
            return phase != "baiting" && phase != "casting" && phase != "waiting" &&
                phase != "hooked" && phase != "effort" && phase != "groundbaiting";
        }

        public static string VisibleBodyWaterSizeTier(float? visibleBodyLengthAt128Px)
        {
            if (!visibleBodyLengthAt128Px.HasValue || float.IsNaN(visibleBodyLengthAt128Px.Value) || float.IsInfinity(visibleBodyLengthAt128Px.Value)) return "S";
            var length = visibleBodyLengthAt128Px.Value;
            return length <= 16 ? "S" : length <= 32 ? "M" : length <= 56 ? "L" : "XL";
        }

        /// <summary>Only call with body length already permitted to be visible to this viewer.</summary>
        public static string VisibleWaterSizeTier(float? visibleLengthM)
        {
            if (!visibleLengthM.HasValue || float.IsNaN(visibleLengthM.Value) ||
                float.IsInfinity(visibleLengthM.Value) || visibleLengthM.Value <= 0) return "S";
            return visibleLengthM.Value < .30f ? "S" : visibleLengthM.Value < .80f ? "M" :
                visibleLengthM.Value < 1.50f ? "L" : "XL";
        }
    }
}
