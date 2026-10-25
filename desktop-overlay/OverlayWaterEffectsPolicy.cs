using System;
using System.Collections.Generic;

namespace FishSocialOverlay
{
    // Pure event/budget core, also exercised on non-Windows build machines.
    internal sealed class OverlayWaterEffectsPolicy
    {
        readonly HashSet<string> _consumed = new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> _consumedOrder = new Queue<string>();
        readonly Dictionary<string, long> _highestOccurrences = new Dictionary<string, long>();
        readonly Dictionary<string, double> _last = new Dictionary<string, double>(StringComparer.Ordinal);
        FishingPresentationEnvelope _envelope;
        string _action;
        readonly Queue<string> _retiredOrder = new Queue<string>();
        readonly HashSet<string> _retired = new HashSet<string>(StringComparer.Ordinal);
        long _highestEpoch = -1;
        string _session;
        long _highestRevision;
        public long Version { get; private set; }
        public bool CanPublish(long token) => token == Version && _envelope != null;
        void Retire(string identity)
        {
            if (identity == null || !_retired.Add(identity)) return;
            _retiredOrder.Enqueue(identity);
            if (_retiredOrder.Count > 512) _retired.Remove(_retiredOrder.Dequeue());
        }
        public string LastRejection { get; private set; }
        public int ConsumedCount => _consumed.Count;
        public bool SuppressOneShots { get; set; }
        public FishingPresentationEnvelope Envelope => _envelope;
        public bool IsLive => _envelope != null && _envelope.Owner == "overlay" &&
            _envelope.Source != "snapshot" && !string.IsNullOrEmpty(_envelope.ActionId);
        public bool SetEnvelope(FishingPresentationEnvelope next)
        {
            if (next == null)
            {
                if (_envelope != null) Version++;
                Retire(_action);
                _envelope = null; SuppressOneShots = true; return true;
            }
            if (next.OwnerEpoch < _highestEpoch) return Reject("old-owner-epoch");
            if (next.OwnerEpoch > _highestEpoch && next.PresentationSessionId != _session)
            { _retired.Clear(); _retiredOrder.Clear(); _highestRevision = 0; _session = next.PresentationSessionId; }
            _highestEpoch = Math.Max(_highestEpoch, next.OwnerEpoch);
            if (next.Revision > 0 && next.Revision < _highestRevision) return Reject("old-authoritative-revision");
            _highestRevision = Math.Max(_highestRevision, next.Revision);
            if (_envelope != null && next.OwnerEpoch < _envelope.OwnerEpoch)
                return Reject("old-owner-epoch");
            if (_envelope != null && next.PresentationSessionId == _envelope.PresentationSessionId &&
                next.ActorGeneration < _envelope.ActorGeneration) return Reject("old-actor-generation");
            var identity = next.PresentationSessionId + "|" + next.ActorId + "|" + next.ActionId + "|" + next.ActorGeneration;
            if (_retired.Contains(identity)) return Reject("retired-action");
            var changed = identity != _action || next.OwnerEpoch != _envelope?.OwnerEpoch || next.Owner != _envelope?.Owner;
            if (changed)
            {
                if (_action != identity) Retire(_action);
                _consumed.Clear(); _consumedOrder.Clear(); _highestOccurrences.Clear(); _last.Clear(); _action = identity; SuppressOneShots = false;
            }
            var newEvent = _envelope != null && next.EventId != _envelope.EventId;
            if (changed || newEvent) Version++;
            if (newEvent && next.Source == "authoritative") SuppressOneShots = false;
            _envelope = next;
            return changed;
        }
        bool Reject(string reason) { LastRejection = reason; return false; }
        public bool Accept(string cue, string phase, long occurrence, double now, bool contact, out string key)
        {
            key = null;
            if (_envelope == null || _envelope.Owner != "overlay" || string.IsNullOrEmpty(_envelope.PresentationSessionId)
                || string.IsNullOrEmpty(_envelope.ActorId) || string.IsNullOrEmpty(_envelope.ActionId)) return Reject("missing-owner-or-identity");
            if (!Allowed(cue, phase)) return Reject("phase-not-allowed");
            if (cue == "retrieve.contact" && occurrence != 0) return Reject("retrieve-source-zero-only");
            var retrieveSurface = cue == "fish.surface" && (phase == "resolving" || phase == "stopping");
            if (retrieveSurface && occurrence != 0) return Reject("retrieve-surface-source-zero-only");
            if (_envelope.Source != "authoritative" && _envelope.Source != "prediction" && _envelope.Source != "snapshot")
                return Reject("unknown-source");
            if (_envelope.Source == "prediction" && cue != "float.contact" && cue != "groundbait.contact" && cue != "retrieve.contact" && !retrieveSurface)
                return Reject("prediction-cannot-synthesize-fish");
            var continuous = cue == "waiting.ripple" || cue == "fish.struggle";
            if ((_envelope.Source == "snapshot" || SuppressOneShots) && !continuous) return Reject("snapshot-baseline");
            if (cue == "bite.onset" && (_envelope.Source != "authoritative" || string.IsNullOrEmpty(_envelope.EventId)))
                return Reject("bite-requires-authoritative-id");
            if (!contact) return Reject("no-water-contact");
            key = _envelope.PresentationSessionId + "|" + _envelope.ActorId + "|" +
                (cue == "bite.onset" ? _envelope.EventId : _envelope.ActionId) + "|" + cue + "|" + occurrence;
            var stream = cue + (cue == "bite.onset" ? "|" + _envelope.EventId : "");
            if (_consumed.Contains(key) || _highestOccurrences.TryGetValue(stream, out var highest) && occurrence <= highest)
                return Reject("duplicate");
            // Monotonic authored occurrences fence old resends even after the
            // bounded diagnostic key ledger rotates during a long waiting loop.
            _highestOccurrences[stream] = occurrence;
            _consumed.Add(key); _consumedOrder.Enqueue(key);
            if (_consumedOrder.Count > 512) _consumed.Remove(_consumedOrder.Dequeue());
            var cooldown = cue == "waiting.ripple" ? 2400 : cue == "fish.struggle" ? 700 : cue == "groundbait.contact" ? 720 : 0;
            if (_last.TryGetValue(cue, out var previous) && now - previous < cooldown) return Reject("cooldown");
            _last[cue] = now; LastRejection = null; return true;
        }
        public static bool Allowed(string cue, string phase)
        {
            switch (cue)
            {
                case "float.contact": return phase == "casting" || phase == "waiting";
                case "waiting.ripple": return phase == "waiting";
                case "fish.surface": return phase == "hooked" || phase == "effort" || phase == "resolving" || phase == "stopping";
                case "bite.onset": case "fish.struggle": case "fish.slap":
                    return phase == "hooked" || phase == "effort";
                case "groundbait.contact": return phase == "groundbaiting";
                case "retrieve.contact": return phase == "resolving" || phase == "stopping";
                default: return false;
            }
        }
        public static bool Ending(string phase) => phase != "baiting" && phase != "casting" && phase != "waiting" &&
            phase != "hooked" && phase != "effort" && phase != "groundbaiting";
        public static bool SplashAllowed(string cue) => cue == "float.contact" || cue == "groundbait.contact" || cue == "retrieve.contact" ||
            cue == "fish.surface" || cue == "fish.slap" || cue == "bite.onset" || cue == "fish.struggle";
        internal readonly struct Shape
        {
            public readonly int Arcs, Crowns, Drops;
            public int Count => Arcs + Crowns + Drops;
            public Shape(int arcs, int crowns, int drops) { Arcs = arcs; Crowns = crowns; Drops = drops; }
        }
        public static Shape Components(string cue, int arcs, int crowns, int drops, bool self, bool reduced)
            => new Shape(self ? arcs : 1, self && !reduced && SplashAllowed(cue) ?
                    (cue == "fish.struggle" ? Math.Min(1, crowns) : crowns) : 0,
                self && !reduced && SplashAllowed(cue) ? (cue == "fish.struggle" ? Math.Min(1, drops) : drops) : 0);
        public static double Duration(string cue, double proposed) => cue == "fish.struggle" ? Math.Max(300, Math.Min(600, proposed)) : proposed;
        public static string VisibleTier(double bodyPixelsAt128)
            => !(bodyPixelsAt128 > 16) ? "S" : bodyPixelsAt128 <= 32 ? "M" : bodyPixelsAt128 <= 56 ? "L" : "XL";
        public static string Tier(string tier) => tier == "M" || tier == "L" || tier == "XL" ? tier : "S";
        public static int Variant(string key)
        {
            uint hash = 2166136261;
            foreach (var c in key ?? "") hash = (hash ^ c) * 16777619;
            return (int)(hash & 1);
        }
    }

    internal sealed class WaterEffectBudget
    {
        internal sealed class Group
        {
            public object Owner;
            public bool Self;
            public int Instances, Priority;
            public Action Recycle;
        }
        readonly List<Group> _groups = new List<Group>(8);
        public int Groups => _groups.Count;
        public int Instances { get { var total = 0; foreach (var g in _groups) total += g.Instances; return total; } }
        public Group Admit(object owner, bool self, int count, int priority, Action recycle)
        {
            if (count < 1 || count > 12) return null;
            int OwnCount() { var n = 0; foreach (var g in _groups) if (ReferenceEquals(g.Owner, owner)) n++; return n; }
            int OtherGroups() { var n = 0; foreach (var g in _groups) if (!g.Self) n++; return n; }
            int OtherInstances() { var n = 0; foreach (var g in _groups) if (!g.Self) n += g.Instances; return n; }
            bool Full() => Groups >= 8 || Instances + count > 24 || OwnCount() >= 2 ||
                (!self && (OtherGroups() >= 6 || OtherInstances() + count > 12));
            while (Full())
            {
                Group victim = null;
                foreach (var g in _groups)
                {
                    if (OwnCount() >= 2 && !ReferenceEquals(g.Owner, owner)) continue;
                    if (g.Self && !self || g.Priority > priority) continue;
                    if (victim == null || g.Priority < victim.Priority) victim = g;
                }
                if (victim == null) return null;
                _groups.Remove(victim); victim.Recycle?.Invoke();
            }
            var group = new Group { Owner = owner, Self = self, Instances = count, Priority = priority, Recycle = recycle };
            _groups.Add(group); return group;
        }
        public void Release(Group group) { if (group != null) _groups.Remove(group); }
    }
}
