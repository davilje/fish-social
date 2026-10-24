using System;
using System.IO;
using UnityEngine;

namespace FishSocial.Desktop.Pet
{
    public struct UnityFishingWaterContact
    {
        public Vector2 Anchor;
        public RectTransform EffectRoot;
        public float SlotScale;
        public string Phase;
        public Rect WaterClip;
        // Only a body length already displayed to this viewer may be returned. Null means unknown/S.
        public float? VisibleLengthM;
        public float? VisibleBodyLengthAt128Px;
    }

    /// <summary>The actual pond view supplies its 22B contact and shoreline geometry. Portraits do not.
    /// Resolve the named published layer, including its current source transform/alpha bounds.
    /// Bobber/bait contact uses its actual water intersection; reel line uses the line-water crossing.
    /// Return false when that layer has not reached water; never substitute the actor centre.
    /// </summary>
    public interface IUnityFishingWaterAnchorProvider
    {
        bool TryResolvePublishedContact(string clip, int sourceFrame, string layer,
            out UnityFishingWaterContact contact);
    }

    public sealed class UnityFishingWaterMarker
    {
        public FishingPresentationEnvelope Envelope;
        public string Marker;
        public long Occurrence;
        public UnityFishingWaterContact Contact;
    }

    /// <summary>Consumes only the last nine-layer frame published this Unity frame, never skipped history.</summary>
    public sealed class UnityFishingWaterMarkerBridge
    {
        [Serializable] sealed class Marker { public string clip, cue, layer; public int source; }
        [Serializable] sealed class Manifest { public int schemaVersion; public Marker[] markers; }
        static Manifest manifest;
        static bool loaded;
        bool missingAnchorReported;
        string previousFrame;
        long waitingOccurrence;
        public IUnityFishingWaterAnchorProvider AnchorProvider { get; set; }
        public event Action<UnityFishingWaterMarker> MarkerPublished;

        public void Publish(string clip, int sourceFrame, long loopOccurrence)
        {
            var app = DesktopAppBootstrap.Instance;
            var envelope = app?.ResolveOwnFishingPresentation();
            if (envelope == null || envelope.owner != "unity") return;
            var frameIdentity = envelope.presentationSessionId + "|" + envelope.actorGeneration + "|" +
                envelope.actionId + "|" + clip + "|" + sourceFrame + "|" + loopOccurrence;
            var newFrame = frameIdentity != previousFrame;
            previousFrame = frameIdentity;
            EnsureManifest();
            if (manifest?.markers == null) return;
            if (newFrame && (clip == "hooked" || clip == "bite" || clip == "effort"))
            {
                Emit(envelope, "bite.onset", clip, sourceFrame, "fish", 0);
            }
            foreach (var marker in manifest.markers)
            {
                if (marker.clip != clip || marker.source != sourceFrame) continue;
                // D interpolation can reach the water later within the same source frame.
                // Re-check actual geometry for object contact; the session ledger consumes
                // its successful occurrence once. Continuous cues never become frame-rate driven.
                if (!newFrame && marker.cue != "float.contact" && marker.cue != "groundbait.contact" && marker.cue != "retrieve.contact" && marker.cue != "fish.surface") continue;
                Emit(envelope, marker.cue, clip, sourceFrame, marker.layer,
                    marker.cue == "waiting.ripple" ? ++waitingOccurrence : (marker.cue == "retrieve.contact" || marker.cue == "fish.surface" && FishingPresentationSession.IsRetrievePhase(app.OwnFishingPresentationPhase)) ? 0 : loopOccurrence);
            }
            // Surface/slap are emitted only by authored metadata plus the actual published layer contact.
        }

        void Emit(FishingPresentationEnvelope envelope, string cue, string clip, int sourceFrame, string layer, long occurrence)
        {
            if (AnchorProvider == null)
            {
                if (!missingAnchorReported)
                {
                    missingAnchorReported = true;
                    Debug.Log("[WaterEffects] Unity contact provider absent; portrait view silently degrades water cues.");
                }
                return;
            }
            if (!AnchorProvider.TryResolvePublishedContact(clip, sourceFrame, layer, out var contact) ||
                contact.WaterClip.width <= 0 || contact.WaterClip.height <= 0 ||
                !contact.WaterClip.Contains(contact.Anchor)) return;
            if (cue != "float.contact" && cue != "groundbait.contact" && cue != "waiting.ripple" && cue != "retrieve.contact")
                envelope.waterSizeTier = contact.VisibleLengthM.HasValue
                    ? FishingPresentationSession.VisibleWaterSizeTier(contact.VisibleLengthM)
                    : FishingPresentationSession.VisibleBodyWaterSizeTier(contact.VisibleBodyLengthAt128Px);
            else envelope.waterSizeTier = "S";
            if (!DesktopAppBootstrap.Instance.FishingPresentation.TryConsume(envelope, cue, occurrence, "unity-visual-marker")) return;
            MarkerPublished?.Invoke(new UnityFishingWaterMarker
            {
                Envelope = envelope, Marker = cue, Occurrence = occurrence, Contact = contact,
            });
        }

        public static void WarmupSourceMarkers() { EnsureManifest(); }

        static void EnsureManifest()
        {
            if (loaded) return;
            loaded = true;
            var path = Path.Combine(Application.streamingAssetsPath, "Fishing", "source-water-markers.json");
            try { manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(path)); }
            catch (IOException) { }
            catch (ArgumentException) { }
        }
    }
}
