using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop.Pet
{
    public sealed class FishingRestPresentation
    {
        readonly Image[] _layers;
        readonly FishingRestLayerEffect[] _effects;
        readonly FishingRestMotion _motion = new FishingRestMotion();
        readonly Text _badge;
        double _clockMs;
        string _sleepHeadSkin;
        Sprite _sleepHead;
        Sprite _awakeHead;
        Sprite _appliedSleepHead;
        Image _head;

        public FishingRestPresentation(Image[] layers, RectTransform host)
        {
            _layers = layers;
            _effects = new FishingRestLayerEffect[layers.Length];
            for (var index = 0; index < layers.Length; index++)
            {
                var name = FishingLayerArt.LayerNames[index];
                if (name == "body" || name == "head" || name == "far_hand" || name == "hands" ||
                    name == "rod" || name == "line")
                    _effects[index] = layers[index].gameObject.AddComponent<FishingRestLayerEffect>();
                if (name == "head")
                    _head = layers[index];
            }
            var badgeHost = new GameObject("FishingSleepBadge", typeof(RectTransform), typeof(Text));
            badgeHost.transform.SetParent(host, false);
            _badge = badgeHost.GetComponent<Text>();
            _badge.font = DesktopModalUi.Font;
            DesktopLocalization.SetText(_badge, "Zzz");
            _badge.alignment = TextAnchor.MiddleCenter;
            _badge.raycastTarget = false;
            _badge.maskable = false;
            _badge.rectTransform.anchorMin = _badge.rectTransform.anchorMax = new Vector2(0, 1);
            _badge.rectTransform.pivot = new Vector2(0, 1);
            _badge.gameObject.SetActive(false);
        }

        public static string SleepHeadResourcePath(string skinId)
        {
            return "Desktop/Pet/Fishing/" + skinId + "/body/fishing/0/head-sleep";
        }

        public void Update(float deltaSeconds, bool active, bool sleeping, string skinId, Sprite awakeHead)
        {
            if (!active)
            {
                Reset();
                return;
            }
            _clockMs += deltaSeconds * 1000d;
            var pose = _motion.Update(_clockMs, true, sleeping);
            var line = pose.Line(FishingRestMotion.RodTipX, FishingRestMotion.RodTipY, FishingRestMotion.LineBottom);
            for (var index = 0; index < _effects.Length; index++)
            {
                if (_effects[index] != null)
                {
                    var layer = FishingLayerArt.LayerNames[index];
                    _effects[index].SetPose(layer == "line" ? line : layer == "rod" ? pose.Rod : pose.Body,
                        layer == "line" || layer == "rod" ? 0 : pose.Nod);
                }
            }
            _awakeHead = awakeHead;
            if (sleeping && _sleepHeadSkin != skinId)
            {
                _sleepHeadSkin = skinId;
                var candidate = Resources.Load<Sprite>(SleepHeadResourcePath(skinId));
                _sleepHead = IsFullCanvas(candidate) && MatchesAwakeSource(skinId) ? candidate : null;
            }
            _appliedSleepHead = pose.SleepBlend >= .5 && _sleepHeadSkin == skinId ? _sleepHead : null;
            if (_head != null)
                _head.sprite = _appliedSleepHead != null ? _appliedSleepHead : awakeHead;
            var canvas = _layers[0].rectTransform;
            var scaleX = canvas.rect.width / FishingLayerArt.CanvasWidth;
            var scaleY = canvas.rect.height / FishingLayerArt.CanvasHeight;
            _badge.rectTransform.anchoredPosition = canvas.anchoredPosition +
                new Vector2(385 * scaleX, -(120 - (float)pose.BubbleRise) * scaleY);
            _badge.rectTransform.sizeDelta = new Vector2(64 * scaleX, 48 * scaleY);
            _badge.fontSize = Mathf.Max(1, Mathf.RoundToInt(26 * scaleY));
            _badge.color = new Color(.42f, .46f, .51f, (float)pose.BubbleOpacity);
            _badge.gameObject.SetActive(pose.SleepBlend > .01);
            _badge.transform.SetAsLastSibling();
        }

        public void Reset()
        {
            _motion.Update(0, false, false);
            _clockMs = 0;
            foreach (var effect in _effects)
            {
                if (effect != null)
                    effect.SetPose(FishingRestTransform.Identity);
            }
            if (_head != null && _appliedSleepHead != null && _head.sprite == _appliedSleepHead)
                _head.sprite = _awakeHead;
            _awakeHead = null;
            _appliedSleepHead = null;
            if (_badge != null)
                _badge.gameObject.SetActive(false);
        }

        static bool IsFullCanvas(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return false;
            var rect = sprite.rect;
            return rect.x == 0 && rect.y == 0 && rect.width == sprite.texture.width &&
                rect.height == sprite.texture.height &&
                Mathf.Abs(rect.width / rect.height - FishingLayerArt.CanvasWidth / (float)FishingLayerArt.CanvasHeight) < .0001f;
        }

        static bool MatchesAwakeSource(string skinId)
        {
            var provenance = Resources.Load<TextAsset>(SleepHeadResourcePath(skinId) + "-source");
            if (provenance == null)
                return false;
            var expected = provenance.text.Trim();
            var relative = Path.Combine(skinId, "body", "fishing", "0", "head.png");
            var streaming = Path.Combine(Application.streamingAssetsPath, "Fishing", relative);
            var found = false;
            if (File.Exists(streaming))
            {
                if (!MatchesSourceFile(streaming, expected))
                    return false;
                found = true;
            }
#if UNITY_EDITOR
            var native = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..",
                "desktop-overlay", "OverlayResources", "fishing", relative));
            if (File.Exists(native))
            {
                if (!MatchesSourceFile(native, expected))
                    return false;
                found = true;
            }
#endif
            return found;
        }

        public static bool MatchesSourceFile(string sourcePath, string expectedSha256)
        {
            if (string.IsNullOrEmpty(expectedSha256) || expectedSha256.Length != 64)
                return false;
            try
            {
                using (var stream = File.OpenRead(sourcePath))
                using (var hash = SHA256.Create())
                    return string.Equals(BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""),
                        expectedSha256, StringComparison.OrdinalIgnoreCase);
            }
            catch (IOException) { return false; }
            catch (UnauthorizedAccessException) { return false; }
        }
    }
}
