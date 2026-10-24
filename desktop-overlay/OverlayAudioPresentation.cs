using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;

namespace FishSocialOverlay
{
    // An atomic capability issued by Unity. Snapshot state can restore poses but
    // never grants permission to replay a past action or result.
    [DataContract]
    public sealed class OverlayAudioPresentation
    {
        [DataMember(Name = "protocolVersion")] public int ProtocolVersion { get; set; }
        [DataMember(Name = "presentationSessionId")] public string PresentationSessionId { get; set; }
        [DataMember(Name = "actorId")] public string ActorId { get; set; }
        [DataMember(Name = "pondId")] public string PondId { get; set; }
        [DataMember(Name = "spotId")] public string SpotId { get; set; }
        [DataMember(Name = "ownerEpoch")] public long OwnerEpoch { get; set; }
        [DataMember(Name = "owner")] public string Owner { get; set; }
        [DataMember(Name = "actionId")] public string ActionId { get; set; }
        [DataMember(Name = "eventId")] public string EventId { get; set; }
        [DataMember(Name = "eventKind")] public string EventKind { get; set; }
        [DataMember(Name = "eventDeadlineMs")] public long EventDeadlineMs { get; set; }
        [DataMember(Name = "waterSizeTier")] public string WaterSizeTier { get; set; }
        [DataMember(Name = "source")] public string Source { get; set; }
        [DataMember(Name = "enabled")] public bool Enabled { get; set; }
    }

    public sealed class OverlayAudioMarker
    {
        public OverlayAudioPresentation Identity { get; internal set; }
        public string Marker { get; internal set; }
        public long Occurrence { get; internal set; }
        public long ActorGeneration { get; internal set; }
        public string WaterSizeTier { get; internal set; }
    }

    // Pure policy, also exercised by the portable tests. It runs only on the
    // dispatcher and only after the complete image composite has been published.
    internal sealed class OverlayAudioMarkerPolicy
    {
        static long _nextGeneration;
        readonly HashSet<string> _consumed = new HashSet<string>(StringComparer.Ordinal);
        readonly Queue<string> _order = new Queue<string>();
        readonly HashSet<string> _snapshotActions = new HashSet<string>(StringComparer.Ordinal);
        readonly HashSet<string> _snapshotEvents = new HashSet<string>(StringComparer.Ordinal);
        readonly Dictionary<string, long> _activeActionOccurrences = new Dictionary<string, long>(StringComparer.Ordinal);
        readonly Dictionary<string, long> _activeEventOccurrences = new Dictionary<string, long>(StringComparer.Ordinal);
        long _highestOwnerEpoch = -1;
        OverlayAudioPresentation _identity;
        string _phase, _skin, _cancelledAction;
        bool _visible = true;
        public int Revision { get; private set; }
        public long Generation { get; private set; }
        public int ConsumedCount => _consumed.Count;
        public string ActionId => _identity?.ActionId;

        public static string VisibleTier(OverlayAudioPresentation audio, FishingPresentationEnvelope water, string phase)
        {
            if (audio == null || water == null || water.Owner != "overlay" ||
                water.PresentationSessionId != audio.PresentationSessionId || water.ActorId != audio.ActorId ||
                water.PondId != audio.PondId || water.SpotId != audio.SpotId || water.ActionId != audio.ActionId ||
                water.OwnerEpoch != audio.OwnerEpoch || (phase != "hooked" && phase != "effort")) return "S";
            switch (water.WaterSizeTier)
            { case "M": case "L": case "XL": return water.WaterSizeTier; default: return "S"; }
        }

        public void Update(OverlayAudioPresentation value, string phase, string skin)
        {
            if (value != null && value.OwnerEpoch < _highestOwnerEpoch) return;
            if (value != null) _highestOwnerEpoch = Math.Max(_highestOwnerEpoch, value.OwnerEpoch);
            var epochChanged = value?.PresentationSessionId != _identity?.PresentationSessionId ||
                value?.OwnerEpoch != _identity?.OwnerEpoch;
            if (epochChanged || value?.ActionId != _identity?.ActionId) _activeActionOccurrences.Clear();
            if (epochChanged || value?.EventId != _identity?.EventId) _activeEventOccurrences.Clear();
            if (epochChanged)
            {
                _consumed.Clear(); _order.Clear(); _snapshotActions.Clear(); _snapshotEvents.Clear();
                _cancelledAction = null;
            }
            if (epochChanged || skin != _skin) Generation = Interlocked.Increment(ref _nextGeneration);
            if (epochChanged || skin != _skin || phase != _phase || !Same(value, _identity)) Revision++;
            _skin = skin; _phase = phase; _identity = value;
            if (value != null && (value.Source == "snapshot" || !_visible)) SuppressCurrent();
        }

        static bool Same(OverlayAudioPresentation a, OverlayAudioPresentation b)
            => a?.ActorId == b?.ActorId && a?.PondId == b?.PondId && a?.SpotId == b?.SpotId &&
               a?.Owner == b?.Owner && a?.ActionId == b?.ActionId && a?.EventId == b?.EventId &&
               a?.EventKind == b?.EventKind && a?.Source == b?.Source && a?.Enabled == b?.Enabled;

        void SuppressCurrent()
        {
            if (!string.IsNullOrEmpty(_identity?.ActionId)) _snapshotActions.Add(_identity.ActionId);
            if (!string.IsNullOrEmpty(_identity?.EventId)) _snapshotEvents.Add(EventBaseline(_identity));
            // There is no replay log here; retain only current baseline identities.
            if (_snapshotActions.Count > 512) { var current = _identity?.ActionId; _snapshotActions.Clear(); if (current != null) _snapshotActions.Add(current); }
            if (_snapshotEvents.Count > 512) { var current = _identity == null ? null : EventBaseline(_identity); _snapshotEvents.Clear(); if (current != null) _snapshotEvents.Add(current); }
        }

        static string EventBaseline(OverlayAudioPresentation value) => value.EventKind + "|" + value.EventId;

        public void SetVisible(bool value)
        {
            if (_visible == value) return;
            _visible = value; Revision++;
            if (!value) SuppressCurrent();
        }

        public void Cancel()
        {
            _cancelledAction = _identity?.ActionId;
            SuppressCurrent(); Revision++;
        }

        public OverlayAudioMarker Consume(string marker, long occurrence, int revision,
            long generation, string clipActionId, bool contact, long nowMs)
        {
            var c = _identity;
            if (revision != Revision || generation != Generation || !_visible || c == null ||
                c.ProtocolVersion != 2 || !c.Enabled || c.Owner != "overlay" ||
                string.IsNullOrEmpty(c.PresentationSessionId) || string.IsNullOrEmpty(c.ActorId) ||
                string.IsNullOrEmpty(c.PondId) || string.IsNullOrEmpty(c.SpotId) ||
                string.IsNullOrEmpty(c.ActionId) ||
                c.Source == "snapshot" || (c.Source != "prediction" && c.Source != "authoritative") ||
                (!string.IsNullOrEmpty(_cancelledAction) && c.ActionId == _cancelledAction)) return null;

            var isEvent = marker == "bite.onset" || marker == "fish.surface" || marker == "fish.slap" ||
                marker == "result.presented" || marker == "escape.contact";
            var id = isEvent ? c.EventId : c.ActionId;
            if (string.IsNullOrEmpty(id) || occurrence < 0) return null;
            if (isEvent)
            {
                if (c.Source != "authoritative" || _snapshotEvents.Contains(EventBaseline(c)) ||
                    (c.EventDeadlineMs > 0 && nowMs >= c.EventDeadlineMs)) return null;
            }
            else if (clipActionId != c.ActionId || _snapshotActions.Contains(id)) return null;

            switch (marker)
            {
                case "cast.release": if (_phase != "casting") return null; break;
                case "float.contact": if ((_phase != "casting" && _phase != "waiting") || !contact) return null; break;
                case "bite.onset":
                case "fish.surface":
                case "fish.slap":
                    if ((_phase != "hooked" && _phase != "effort") || c.EventKind != "bite" || !contact) return null;
                    break;
                case "groundbait.contact": if (_phase != "groundbaiting" || !contact) return null; break;
                case "retrieve.start":
                    if (_phase != "resolving" && _phase != "stopping" && _phase != "reeling" && _phase != "retrieving") return null;
                    break;
                case "escape.contact": if (c.EventKind != "escape" || !contact) return null; break;
                case "result.presented": if (c.EventKind != "catch") return null; break;
                default: return null;
            }
            var key = c.PresentationSessionId + "|" + c.ActorId + "|" + id + "|" + marker + "|" + occurrence;
            var highest = isEvent ? _activeEventOccurrences : _activeActionOccurrences;
            if (highest.TryGetValue(marker, out var previous) && occurrence <= previous) return null;
            if (!_consumed.Add(key)) return null;
            highest[marker] = occurrence;
            _order.Enqueue(key);
            // Unity also retains its bounded authoritative event ledger. Locally,
            // current markers are a small subset; no repeated frame allocates keys.
            while (_order.Count > 512) _consumed.Remove(_order.Dequeue());
            return new OverlayAudioMarker { Identity = c, Marker = marker, Occurrence = occurrence,
                ActorGeneration = Generation, WaterSizeTier = c.WaterSizeTier };
        }
    }
}
