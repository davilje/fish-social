using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Pet
{
    /// <summary>Unity fallback: one clock and a fixed shared pool. Real pond geometry is supplied by the view.</summary>
    public sealed class UnityFishingWaterEffects : MonoBehaviour
    {
        [Serializable] sealed class Tier { public int durationMs, arcCount, crownCount, dropCount; }
        [Serializable] sealed class Tiers { public Tier S, M, L, XL; }
        [Serializable] sealed class Cue { public string cue, sizeTier; public string[] markers, allowedPhases; public int durationMs, priority, cooldownMs; }
        [Serializable] sealed class Asset { public string id, file; public int width, height; }
        [Serializable] sealed class Config { public int schemaVersion; public Tiers tiers; public Cue[] cues; public Asset[] assets; }
        sealed class Particle { public RawImage Image; public RectTransform Rect; public Vector2 Origin; public int Kind; public float Direction, Height, Delay; public bool Used; }
        sealed class Active
        {
            public RectTransform Host, RippleHost;
            public readonly List<Particle> Particles = new List<Particle>(10);
            public UnityWaterEffectBudget.Group Budget;
            public FishingPresentationEnvelope Envelope;
            public string Cue, Quality;
            public float Started, Duration, EndAt = float.PositiveInfinity;
            public bool Used, Retrieve;
        }
        static UnityFishingWaterEffects instance;
        readonly UnityWaterEffectBudget budget = new UnityWaterEffectBudget();
        readonly Particle[] particles = new Particle[24];
        readonly Active[] groups = new Active[8];
        readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        readonly Dictionary<string, float> rippleVisibleTop = new Dictionary<string, float>();
        readonly Dictionary<string, float> lastCue = new Dictionary<string, float>();
        Config config;
        bool ready;
        public int ActiveInstances => budget.Instances;
        public int ActiveGroups => budget.Groups;
        public bool ResourcesReady => ready;
        public static UnityFishingWaterEffects Warmup()
        {
            if (instance != null) return instance;
            var host = new GameObject("FishingWaterEffectPool");
            instance = host.AddComponent<UnityFishingWaterEffects>();
            if (Application.isPlaying) DontDestroyOnLoad(host);
            return instance;
        }
        void Awake()
        {
            EnsurePoolObjects();
            if (Application.isPlaying) StartCoroutine(LoadResources());
        }
        void EnsurePoolObjects()
        {
            for (var i = 0; i < groups.Length; i++)
            {
                if (groups[i] == null) groups[i] = new Active();
                if (groups[i].Host != null) continue;
                if (groups[i].Used) Recycle(groups[i]);
                var host = new GameObject("WaterGroup" + i, typeof(RectTransform), typeof(RectMask2D));
                host.transform.SetParent(transform, false);
                groups[i].Host = (RectTransform)host.transform;
                groups[i].Host.anchorMin = groups[i].Host.anchorMax = Vector2.zero;
                groups[i].Host.pivot = Vector2.zero;
                var ripple = new GameObject("BelowWaterlineRipples", typeof(RectTransform), typeof(RectMask2D));
                ripple.transform.SetParent(host.transform, false);
                groups[i].RippleHost = (RectTransform)ripple.transform;
                groups[i].RippleHost.anchorMin = groups[i].RippleHost.anchorMax = Vector2.zero;
                groups[i].RippleHost.pivot = Vector2.zero;
                host.SetActive(false);
            }
            for (var i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null && particles[i].Rect != null) continue;
                var host = new GameObject("WaterParticle" + i, typeof(RectTransform), typeof(RawImage));
                host.transform.SetParent(transform, false);
                var image = host.GetComponent<RawImage>(); image.raycastTarget = false;
                var rect = (RectTransform)host.transform;
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = new Vector2(.5f, .3125f);
                particles[i] = new Particle { Image = image, Rect = rect };
                host.SetActive(false);
            }
        }
        IEnumerator LoadResources()
        {
            var root = Path.Combine(Application.streamingAssetsPath, "Effects", "Fishing", "v4");
            var task = Task.Run(() => File.ReadAllText(Path.Combine(root, "effects.json")));
            while (!task.IsCompleted) yield return null;
            if (task.IsFaulted) yield break;
            try { config = JsonUtility.FromJson<Config>(task.Result); } catch (ArgumentException) { yield break; }
            if (config?.schemaVersion != 2 || config.assets == null || config.tiers == null || config.cues == null) yield break;
            long decodedBytes = 0;
            foreach (var asset in config.assets)
            {
                if (string.IsNullOrEmpty(asset.id) || string.IsNullOrEmpty(asset.file) || Path.GetFileName(asset.file) != asset.file) yield break;
                var path = Path.Combine(root, asset.file);
                var read = Task.Run(() => File.ReadAllBytes(path));
                while (!read.IsCompleted) yield return null;
                if (read.IsFaulted) yield break;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!texture.LoadImage(read.Result, false)) { Destroy(texture); yield break; }
                decodedBytes += (long)texture.width * texture.height * 4;
                if (decodedBytes > 4 * 1024 * 1024) { Destroy(texture); yield break; }
                var pixels = texture.GetPixels32();
                if (asset.id.StartsWith("ripple-", StringComparison.Ordinal))
                {
                    var top = 0;
                    for (var pixel = 0; pixel < pixels.Length; pixel++)
                        if (pixels[pixel].a > 0) top = Math.Max(top, pixel / texture.width + 1);
                    rippleVisibleTop[asset.id] = Mathf.Max(0, top - texture.height * .3125f);
                }
                // Same cached pond-contrast palette as Overlay and Web. Preserve alpha.
                for (var pixel = 0; pixel < pixels.Length; pixel++)
                {
                    var colour = pixels[pixel];
                    var tone = Mathf.Clamp01((colour.g - 125) / 85f);
                    var upper = tone >= .32f;
                    var blend = upper ? (tone - .32f) / .68f : tone / .32f;
                    colour.r = (byte)Mathf.RoundToInt(upper ? 181 + 74 * blend : 27 + 154 * blend);
                    colour.g = (byte)Mathf.RoundToInt(upper ? 240 + 7 * blend : 82 + 158 * blend);
                    colour.b = (byte)Mathf.RoundToInt(upper ? 245 - 21 * blend : 114 + 131 * blend);
                    pixels[pixel] = colour;
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                texture.name = asset.id;
                texture.filterMode = FilterMode.Bilinear;
                textures[asset.id] = texture;
                yield return null;
            }
            ready = true;
            enabled = false; // No Update subscription when there are no active groups.
        }
        public static void Publish(UnityFishingWaterMarker marker)
        {
            if (marker == null) return;
            var renderer = Warmup();
            renderer.TryPublish(marker, true);
        }
        public bool TryPublish(UnityFishingWaterMarker marker, bool self)
        {
            var quality = DesktopFishingEffectsSettingsView.Quality;
            var contact = marker.Contact;
            if (!ready || !UnityWaterEffectBudget.CanRender(marker.Envelope,
                contact.EffectRoot != null && contact.WaterClip.Contains(contact.Anchor), quality, self, marker.Marker)) return false;
            EnsurePoolObjects();
            var app = DesktopAppBootstrap.Instance;
            var phase = self ? app?.OwnFishingPresentationPhase ?? contact.Phase : contact.Phase;
            Cue cue = null;
            foreach (var candidate in config.cues)
                if (candidate.cue == marker.Marker || Array.IndexOf(candidate.markers ?? new string[0], marker.Marker) >= 0) { cue = candidate; break; }
            if (cue == null || Array.IndexOf(cue.allowedPhases ?? new string[0], phase) < 0) return false;
            var now = Time.unscaledTime;
            var cooldownKey = marker.Envelope.actorId + "|" + marker.Envelope.actionId + "|" + marker.Marker;
            if (lastCue.TryGetValue(cooldownKey, out var previous) && (now - previous) * 1000 < cue.cooldownMs) return false;
            // Keep only current actor-action cooldowns, never a replay queue.
            if (lastCue.Count >= 64) lastCue.Clear();
            lastCue[cooldownKey] = now;
            var name = cue.sizeTier == "shared" ? marker.Envelope.waterSizeTier : "S";
            var tier = name == "XL" ? config.tiers.XL : name == "L" ? config.tiers.L : name == "M" ? config.tiers.M : config.tiers.S;
            var tierIndex = name == "XL" ? 3 : name == "L" ? 2 : name == "M" ? 1 : 0;
            var reduced = quality == "reduced";
            var splash = marker.Marker == "bite.onset" || marker.Marker == "fish.struggle" || marker.Marker == "groundbait.contact" || marker.Marker == "retrieve.contact" || marker.Marker == "fish.surface" || marker.Marker == "fish.slap" || marker.Marker == "float.contact";
            var arcs = reduced ? tierIndex > 0 ? 2 : 1 : !self ? 1 : cue.sizeTier == "shared" ? tier.arcCount : marker.Marker == "groundbait.contact" ? 2 : 1;
            var crowns = reduced || !self || !splash ? 0 : cue.sizeTier == "shared" ? tier.crownCount : 1;
            var drops = reduced || !self || !splash ? 0 : cue.sizeTier == "shared" ? tier.dropCount : 1;
            if (marker.Marker == "fish.struggle") { crowns = Math.Min(1, crowns); drops = Math.Min(1, drops); }
            var count = Math.Min(10, arcs + crowns + drops);
            if (marker.Marker == "bite.onset") RemoveCue(marker.Envelope.actorId, "waiting.ripple");
            if (marker.Marker == "fish.surface" || marker.Marker == "fish.slap") RemoveCue(marker.Envelope.actorId, "fish.struggle");
            Active group = null;
            // Admission may recycle a group; choose the free host after admission.
            Active admittedGroup = null;
            var admission = budget.Admit(marker.Envelope.actorId, self, count, cue.priority, () => { if (admittedGroup != null) Recycle(admittedGroup); });
            if (admission == null) return false;
            foreach (var candidate in groups) if (!candidate.Used) { group = candidate; break; }
            if (group == null) { budget.Release(admission); return false; }
            admittedGroup = group;
            group.Used = true; group.Budget = admission; group.Envelope = marker.Envelope; group.Cue = marker.Marker; group.Quality = quality;
            group.Retrieve = FishingPresentationSession.IsRetrievePhase(phase);
            group.Started = now; group.Duration = (reduced ? 150 + tierIndex * 20 : cue.durationMs > 0 ? cue.durationMs : tier.durationMs) / 1000f;
            if (marker.Marker == "fish.struggle") group.Duration = Mathf.Clamp(group.Duration, .3f, .6f);
            group.EndAt = float.PositiveInfinity;
            group.Host.SetParent(contact.EffectRoot, false);
            group.Host.anchoredPosition = contact.WaterClip.min - contact.EffectRoot.rect.min;
            group.Host.sizeDelta = contact.WaterClip.size;
            // Ripple geometry is independently clipped below this exact contact, after every
            // image transform. Splash crowns/drops retain the full shoreline clip above it.
            group.RippleHost.anchoredPosition = Vector2.zero;
            group.RippleHost.sizeDelta = new Vector2(contact.WaterClip.width,
                Mathf.Clamp(contact.Anchor.y - contact.WaterClip.yMin, 0, contact.WaterClip.height));
            group.Host.SetAsFirstSibling(); // Pond water decorations remain behind actors and progress UI.
            group.Host.gameObject.SetActive(true);
            var key = marker.Envelope.presentationSessionId + "|" + marker.Envelope.actorId + "|" +
                (marker.Marker == "bite.onset" ? marker.Envelope.eventId : marker.Envelope.actionId) + "|" + marker.Marker + "|" + marker.Occurrence;
            var variant = UnityWaterEffectBudget.Variant(key) == 0 ? "a" : "b";
            var scale = contact.SlotScale > 0 ? contact.SlotScale : 1;
            for (var i = 0; i < count; i++)
            {
                Particle p = null;
                foreach (var candidate in particles) if (!candidate.Used) { p = candidate; break; }
                if (p == null) break;
                var kind = i < arcs ? "ripple" : i < arcs + crowns ? "splash" : "drop";
                var asset = kind == "drop" ? i % 2 == 0 ? "drop-round" : "drop-short" : kind + "-" + name.ToLowerInvariant() + "-" + variant;
                if (!textures.TryGetValue(asset, out var texture)) continue;
                p.Used = true; p.Kind = kind == "ripple" ? 0 : kind == "splash" ? 1 : 2;
                p.Image.texture = texture; p.Image.color = Color.clear;
                p.Rect.SetParent(p.Kind == 0 ? group.RippleHost : group.Host, false);
                p.Image.uvRect = new Rect(0, 0, 1, 1);
                p.Rect.pivot = new Vector2(.5f, .3125f);
                var rippleScale = p.Kind == 0 ? marker.Marker == "waiting.ripple" ? 1.7f : 1.2f : 1;
                p.Rect.sizeDelta = new Vector2(texture.width, texture.height) * scale * rippleScale;
                if (kind == "splash" && crowns == 2)
                {
                    var half = i - arcs;
                    p.Image.uvRect = new Rect(half * .5f, 0, .5f, 1);
                    p.Rect.sizeDelta = new Vector2(texture.width * .5f, texture.height) * scale;
                    p.Rect.pivot = new Vector2(half == 0 ? 1 : 0, .3125f);
                }
                p.Origin = contact.Anchor - contact.WaterClip.min;
                if (p.Kind == 0 && rippleVisibleTop.TryGetValue(asset, out var visibleTop))
                    p.Origin.y -= visibleTop * scale * rippleScale;
                if (marker.Marker == "groundbait.contact" && p.Kind == 0)
                    p.Origin.x += (i - (arcs - 1) * .5f) * 7 * scale;
                p.Direction = (i % 2 == 0 ? -1 : 1) * (4 + i * 1.7f) * scale;
                p.Height = (4 + tierIndex * 7) * scale;
                p.Delay = p.Kind == 0 ? i * .08f : p.Kind == 2 ? .08f + i % 3 * .035f : .02f;
                p.Rect.anchoredPosition = p.Origin; p.Rect.gameObject.SetActive(true);
                group.Particles.Add(p);
            }
            enabled = true;
            return true;
        }
        void Update()
        {
            var now = Time.unscaledTime;
            var app = DesktopAppBootstrap.Instance;
            var current = app?.ResolveOwnFishingPresentation();
            var quality = DesktopFishingEffectsSettingsView.Quality;
            foreach (var group in groups)
            {
                if (!group.Used) continue;
                if (group.Host == null) { Recycle(group); continue; }
                var e = group.Envelope;
                if (current == null || current.owner != "unity" || current.presentationSessionId != e.presentationSessionId ||
                    current.ownerEpoch != e.ownerEpoch || current.actorGeneration != e.actorGeneration ||
                    current.actionId != e.actionId || current.source == "snapshot" && group.Retrieve ||
                    (group.Retrieve && (group.Cue == "retrieve.contact" || group.Cue == "fish.surface") ? !FishingPresentationSession.IsRetrievePhase(app.OwnFishingPresentationPhase) :
                        FishingPresentationSession.IsEnding(app.OwnFishingPresentationPhase)) || quality != group.Quality)
                    group.EndAt = Mathf.Min(group.EndAt, now + .1f);
                if (now - group.Started >= group.Duration || now >= group.EndAt) { Recycle(group); continue; }
                var progress = (now - group.Started) / group.Duration;
                foreach (var p in group.Particles)
                {
                    var t = Mathf.Clamp01((progress - p.Delay) / (1 - p.Delay));
                    // Preserve the PNG's painted translucency through the readable part
                    // of the effect; match Overlay and the review preview.
                    var fade = Mathf.Clamp01((t - .4f) / .6f);
                    var opacity = .95f * Mathf.Min(1, t / .06f) * (1 - fade * fade * (3 - 2 * fade));
                    var alpha = progress < p.Delay ? 0 : opacity * Mathf.Min(1, (group.EndAt - now) / .1f);
                    p.Image.color = new Color(1, 1, 1, alpha);
                    p.Rect.localScale = p.Kind == 0 ? new Vector3(.62f + t * .48f, .7f + t * .3f, 1) :
                        p.Kind == 1 ? new Vector3(.7f + Mathf.Sin(t * Mathf.PI / 2) * .3f, .35f + Mathf.Sin(t * Mathf.PI) * .65f, 1) : Vector3.one;
                    p.Rect.anchoredPosition = p.Origin + (p.Kind == 2 ? new Vector2(p.Direction * Mathf.Sqrt(t), Mathf.Sin(t * Mathf.PI) * p.Height) : new Vector2(p.Direction * .15f * t, -t * 2));
                }
            }
            if (budget.Groups == 0) enabled = false;
        }
        void RemoveCue(string actor, string cue) { foreach (var group in groups) if (group.Used && group.Envelope.actorId == actor && group.Cue == cue) Recycle(group); }
        void Recycle(Active group)
        {
            budget.Release(group.Budget);
            foreach (var p in group.Particles)
            {
                p.Used = false;
                if (p.Rect == null) continue;
                p.Image.texture = null; p.Rect.gameObject.SetActive(false); p.Rect.SetParent(transform, false);
            }
            group.Particles.Clear(); group.Used = false;
            if (group.Host != null) { group.Host.gameObject.SetActive(false); group.Host.SetParent(transform, false); }
        }
        void OnDestroy()
        {
            foreach (var group in groups) if (group != null && group.Used) Recycle(group);
            foreach (var texture in textures.Values)
                if (Application.isPlaying) Destroy(texture); else DestroyImmediate(texture);
            if (instance == this) instance = null;
        }
    }
}
