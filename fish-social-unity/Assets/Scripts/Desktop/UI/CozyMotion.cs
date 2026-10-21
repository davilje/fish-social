using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Journal motion tokens from docs/design/ui/2026-10-21-cozy-journal-motion.md.
    /// Runs on unscaled time. Reduce-motion keeps fades at most 120ms and drops loops.
    /// </summary>
    public static class CozyMotion
    {
        public const string ReducePrefsKey = "fishsocial.desktop.motion.reduce";

        const float Tap = 0.06f;
        const float Snap = 0.22f;
        const float Slide = 0.20f;
        const float Hop = 0.42f;
        const float FloatPeriod = 2.4f;

        enum Channel { Scale, Anchor, Alpha, Number, Loop }

        sealed class Job
        {
            public RectTransform Target;
            public Channel Channel;
            public float Time;
            public float Duration;
            public Vector2 From;
            public Vector2 To;
            public float FromF;
            public float ToF;
            public CanvasGroup Group;
            public Text Label;
            public string Format;
            public bool Loop;
            public float Phase;
            public Action Done;
        }

        static readonly List<Job> Jobs = new List<Job>();
        static bool _paused;
        static Sprite _bobber;
        static Sprite _ripple;
        static Action _cancelFlip;

        public static bool Reduced { get; private set; }

        public static void LoadPreference()
        {
            Reduced = PlayerPrefs.GetInt(ReducePrefsKey, 0) == 1;
        }

        public static void SetReduced(bool value)
        {
            Reduced = value;
            PlayerPrefs.SetInt(ReducePrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (value)
                StopLoops();
        }

        public static void SetPaused(bool paused)
        {
            _paused = paused;
        }

        public static void Tick(float delta)
        {
            if (_paused || delta <= 0f)
                return;
            for (var i = Jobs.Count - 1; i >= 0; i--)
            {
                var job = Jobs[i];
                if (job.Target == null)
                {
                    Jobs.RemoveAt(i);
                    continue;
                }
                if (job.Loop)
                {
                    if (Reduced)
                        continue;
                    job.Time += delta;
                    var wave = Mathf.Sin((job.Time / FloatPeriod + job.Phase) * Mathf.PI * 2f);
                    var rest = job.From;
                    job.Target.anchoredPosition = rest + new Vector2(0f, wave * 3f);
                    job.Target.localRotation = Quaternion.Euler(0f, 0f, wave * 4f);
                    continue;
                }

                job.Time += delta;
                var t = job.Duration <= 0f ? 1f : Mathf.Clamp01(job.Time / job.Duration);
                var e = EaseOutCubic(t);
                switch (job.Channel)
                {
                    case Channel.Scale:
                        var scale = Vector2.LerpUnclamped(job.From, job.To, EaseOutBack(t));
                        job.Target.localScale = new Vector3(scale.x, scale.y, 1f);
                        break;
                    case Channel.Anchor:
                        job.Target.anchoredPosition = Vector2.LerpUnclamped(job.From, job.To, e);
                        break;
                    case Channel.Alpha:
                        if (job.Group != null)
                            job.Group.alpha = Mathf.Lerp(job.FromF, job.ToF, e);
                        break;
                    case Channel.Number:
                        if (job.Label != null)
                        {
                            var value = Mathf.Lerp(job.FromF, job.ToF, e);
                            job.Label.text = string.IsNullOrEmpty(job.Format)
                                ? Mathf.RoundToInt(value).ToString("N0")
                                : string.Format(job.Format, Mathf.RoundToInt(value));
                        }
                        break;
                }

                if (t < 1f)
                    continue;
                var done = job.Done;
                Jobs.RemoveAt(i);
                if (done != null)
                    done();
            }
        }

        public static void PressDown(RectTransform target, bool enabled)
        {
            if (target == null)
                return;
            if (!enabled)
            {
                Shake(target);
                return;
            }
            Replace(target, Channel.Scale);
            var job = new Job
            {
                Target = target,
                Channel = Channel.Scale,
                Duration = Reduced ? 0.08f : Tap,
                From = Vector2.one,
                To = new Vector2(1.06f, 0.92f),
            };
            Jobs.Add(job);
        }

        public static void PressUp(RectTransform target)
        {
            if (target == null)
                return;
            Replace(target, Channel.Scale);
            if (Reduced)
            {
                target.localScale = Vector3.one;
                return;
            }
            var from = new Vector2(target.localScale.x, target.localScale.y);
            Jobs.Add(new Job
            {
                Target = target,
                Channel = Channel.Scale,
                Duration = Snap * 0.45f,
                From = from,
                To = new Vector2(0.98f, 1.04f),
                Done = () =>
                {
                    if (target == null)
                        return;
                    Jobs.Add(new Job
                    {
                        Target = target,
                        Channel = Channel.Scale,
                        Duration = Snap * 0.55f,
                        From = new Vector2(0.98f, 1.04f),
                        To = Vector2.one,
                    });
                },
            });
        }

        public static void Shake(RectTransform target)
        {
            if (target == null || Reduced)
                return;
            var rest = target.anchoredPosition;
            Replace(target, Channel.Anchor);
            Nudge(target, rest, rest + new Vector2(-4f, 0f), 0.04f, () =>
                Nudge(target, rest + new Vector2(-4f, 0f), rest + new Vector2(4f, 0f), 0.04f, () =>
                    Nudge(target, rest + new Vector2(4f, 0f), rest, 0.05f, null)));
        }

        public static void Punch(RectTransform target)
        {
            if (target == null || Reduced)
                return;
            Replace(target, Channel.Scale);
            Jobs.Add(new Job
            {
                Target = target,
                Channel = Channel.Scale,
                Duration = Snap,
                From = new Vector2(1.12f, 0.9f),
                To = Vector2.one,
            });
        }

        public static void Float(RectTransform target, float phase)
        {
            if (target == null)
                return;
            Replace(target, Channel.Loop);
            if (Reduced)
                return;
            Jobs.Add(new Job
            {
                Target = target,
                Channel = Channel.Loop,
                Loop = true,
                Phase = phase,
                From = target.anchoredPosition,
            });
        }

        public static void Roll(Text label, int from, int to)
        {
            if (label == null)
                return;
            if (Reduced || from == to)
            {
                label.text = to.ToString("N0");
                return;
            }
            ReplaceLabel(label);
            Jobs.Add(new Job
            {
                Target = label.rectTransform,
                Label = label,
                Channel = Channel.Number,
                Duration = 0.4f,
                FromF = from,
                ToF = to,
            });
        }

        public static void SlideToast(RectTransform target)
        {
            if (target == null)
                return;
            var group = EnsureGroup(target.gameObject);
            var rest = target.anchoredPosition;
            if (Reduced)
            {
                group.alpha = 1f;
                return;
            }
            target.anchoredPosition = rest + new Vector2(80f, 0f);
            group.alpha = 0f;
            Replace(target, Channel.Anchor);
            Replace(target, Channel.Alpha);
            Jobs.Add(new Job
            {
                Target = target,
                Channel = Channel.Anchor,
                Duration = Slide,
                From = target.anchoredPosition,
                To = rest,
            });
            Jobs.Add(new Job
            {
                Target = target,
                Group = group,
                Channel = Channel.Alpha,
                Duration = Slide,
                FromF = 0f,
                ToF = 1f,
            });
        }

        public static void Flip(GameObject from, GameObject to, Action done)
        {
            if (to == null)
            {
                if (done != null)
                    done();
                return;
            }
            if (_cancelFlip != null)
            {
                var cancel = _cancelFlip;
                _cancelFlip = null;
                cancel();
            }
            if (Reduced || from == null || from == to)
            {
                if (from != null && from != to)
                    from.SetActive(false);
                to.SetActive(true);
                if (done != null)
                    done();
                return;
            }

            var fromRt = from.GetComponent<RectTransform>();
            var toRt = to.GetComponent<RectTransform>();
            var fromGroup = EnsureGroup(from);
            var toGroup = EnsureGroup(to);
            var fromRest = fromRt.anchoredPosition;
            to.SetActive(true);
            var toRest = toRt.anchoredPosition;
            toRt.anchoredPosition = toRest + new Vector2(24f, 0f);
            toGroup.alpha = 0f;
            toGroup.blocksRaycasts = false;
            fromGroup.alpha = 1f;
            fromGroup.blocksRaycasts = false;
            _cancelFlip = () =>
            {
                Drop(fromRt);
                Drop(toRt);
                if (from != null)
                {
                    fromRt.anchoredPosition = fromRest;
                    fromGroup.alpha = 1f;
                    fromGroup.blocksRaycasts = true;
                    from.SetActive(false);
                }
                if (to != null)
                {
                    toRt.anchoredPosition = toRest;
                    toGroup.alpha = 1f;
                    toGroup.blocksRaycasts = true;
                }
            };
            Replace(fromRt, Channel.Anchor);
            Replace(fromRt, Channel.Alpha);
            Jobs.Add(new Job
            {
                Target = fromRt,
                Channel = Channel.Anchor,
                Duration = 0.12f,
                From = fromRest,
                To = fromRest + new Vector2(-24f, 0f),
            });
            Jobs.Add(new Job
            {
                Target = fromRt,
                Group = fromGroup,
                Channel = Channel.Alpha,
                Duration = 0.12f,
                FromF = 1f,
                ToF = 0f,
                Done = () =>
                {
                    if (from != null)
                    {
                        fromRt.anchoredPosition = fromRest;
                        fromGroup.alpha = 1f;
                        fromGroup.blocksRaycasts = true;
                        from.SetActive(false);
                    }
                    if (toRt == null)
                    {
                        if (done != null)
                            done();
                        return;
                    }
                    Replace(toRt, Channel.Anchor);
                    Replace(toRt, Channel.Alpha);
                    Jobs.Add(new Job
                    {
                        Target = toRt,
                        Channel = Channel.Anchor,
                        Duration = Slide,
                        From = toRest + new Vector2(24f, 0f),
                        To = toRest,
                    });
                    Jobs.Add(new Job
                    {
                        Target = toRt,
                        Group = toGroup,
                        Channel = Channel.Alpha,
                        Duration = Slide,
                        FromF = 0f,
                        ToF = 1f,
                        Done = () =>
                        {
                            if (toGroup != null)
                            {
                                toGroup.alpha = 1f;
                                toGroup.blocksRaycasts = true;
                            }
                            _cancelFlip = null;
                            if (done != null)
                                done();
                        },
                    });
                },
            });
        }

        public static void Ripple(RectTransform parent)
        {
            if (parent == null || Reduced)
                return;
            var ring = new GameObject("CozyRipple", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ring.transform.SetParent(parent, false);
            var rect = ring.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(36f, 36f);
            rect.anchoredPosition = Vector2.zero;
            var image = ring.GetComponent<Image>();
            image.sprite = RippleSprite();
            image.color = new Color(0.36f, 0.68f, 0.89f, 0.9f);
            image.raycastTarget = false;
            var group = ring.AddComponent<CanvasGroup>();
            Jobs.Add(new Job
            {
                Target = rect,
                Channel = Channel.Scale,
                Duration = 0.36f,
                From = new Vector2(0.4f, 0.4f),
                To = new Vector2(1.8f, 1.8f),
            });
            Jobs.Add(new Job
            {
                Target = rect,
                Group = group,
                Channel = Channel.Alpha,
                Duration = 0.36f,
                FromF = 0.9f,
                ToF = 0f,
                Done = () =>
                {
                    if (ring != null)
                        UnityEngine.Object.Destroy(ring);
                },
            });
        }

        public static Sprite BobberSprite()
        {
            if (_bobber != null)
                return _bobber;
            var texture = new Texture2D(40, 58, TextureFormat.RGBA32, false);
            for (var y = 0; y < 58; y++)
            for (var x = 0; x < 40; x++)
            {
                var dx = x - 19.5f;
                var dy = y - 28f;
                var inBall = dx * dx + dy * dy <= 14f * 14f;
                var stem = x >= 18 && x <= 21 && (y < 16 || y > 42);
                Color color;
                if (inBall)
                    color = y >= 28 ? new Color(0.89f, 0.33f, 0.25f, 1f) : new Color(1f, 0.98f, 0.94f, 1f);
                else if (stem)
                    color = new Color(0.46f, 0.28f, 0.18f, 1f);
                else
                    color = Color.clear;
                texture.SetPixel(x, y, color);
            }
            texture.Apply(false, false);
            _bobber = Sprite.Create(texture, new Rect(0, 0, 40, 58), new Vector2(0.5f, 0.15f), 100f);
            texture.Apply(false, true);
            return _bobber;
        }

        public static void EnsureHost(GameObject canvas)
        {
            LoadPreference();
            if (canvas == null || canvas.GetComponent<CozyMotionHost>() != null)
                return;
            canvas.AddComponent<CozyMotionHost>();
        }

        public static void EnsureBobber(Transform nav)
        {
            if (nav == null || nav.Find("CozyBobber") != null)
                return;
            var go = new GameObject("CozyBobber", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(nav, false);
            var rect = go.GetComponent<RectTransform>();
            var navRect = nav as RectTransform;
            rect.anchorMin = rect.anchorMax = navRect != null ? navRect.pivot : new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(28f, 40f);
            var image = go.GetComponent<Image>();
            image.sprite = BobberSprite();
            image.raycastTarget = false;
            image.color = Color.white;
            Float(rect, 0.15f);
        }

        public static void MoveBobber(Transform nav, RectTransform tab)
        {
            if (nav == null || tab == null)
                return;
            EnsureBobber(nav);
            Canvas.ForceUpdateCanvases();
            var bobber = nav.Find("CozyBobber") as RectTransform;
            if (bobber == null)
                return;
            Replace(bobber, Channel.Loop);
            var parent = nav as RectTransform;
            var world = tab.TransformPoint(new Vector3(tab.rect.center.x, tab.rect.yMax, 0f));
            var local = parent != null ? (Vector2)parent.InverseTransformPoint(world) : tab.anchoredPosition;
            var target = local + new Vector2(0f, 10f);
            var from = bobber.anchoredPosition;
            if (Reduced)
            {
                bobber.anchoredPosition = target;
                Float(bobber, 0.15f);
                return;
            }
            var peak = Vector2.Lerp(from, target, 0.5f) + new Vector2(0f, 46f);
            Replace(bobber, Channel.Anchor);
            Jobs.Add(new Job
            {
                Target = bobber,
                Channel = Channel.Anchor,
                Duration = Hop * 0.45f,
                From = from,
                To = peak,
                Done = () =>
                {
                    if (bobber == null)
                        return;
                    Jobs.Add(new Job
                    {
                        Target = bobber,
                        Channel = Channel.Anchor,
                        Duration = Hop * 0.55f,
                        From = peak,
                        To = target,
                        Done = () =>
                        {
                            Ripple(bobber);
                            Punch(tab);
                            Float(bobber, 0.15f);
                        },
                    });
                },
            });
        }

        public static void EnsureCornerCat(Transform parent)
        {
            if (parent == null || parent.Find("CozyCornerCat") != null)
                return;
            var sprite = Resources.Load<Sprite>("Desktop/Theme/Cozy/characters/poses/orange-ready");
            if (sprite == null)
                sprite = Resources.Load<Sprite>("Desktop/Theme/Cozy/characters/avatars/orange");
            if (sprite == null)
                return;
            var go = new GameObject("CozyCornerCat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-8f, 8f);
            rect.sizeDelta = sprite.rect.width > sprite.rect.height ? new Vector2(220f, 176f) : new Vector2(140f, 140f);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            if (!Reduced)
                Float(rect, 0.4f);
        }

        static void Nudge(RectTransform target, Vector2 from, Vector2 to, float duration, Action done)
        {
            if (target == null)
                return;
            Jobs.Add(new Job
            {
                Target = target,
                Channel = Channel.Anchor,
                Duration = duration,
                From = from,
                To = to,
                Done = done,
            });
        }

        static void Drop(RectTransform target)
        {
            if (target == null)
                return;
            for (var i = Jobs.Count - 1; i >= 0; i--)
                if (Jobs[i].Target == target)
                    Jobs.RemoveAt(i);
        }

        static void Replace(RectTransform target, Channel channel)
        {
            for (var i = Jobs.Count - 1; i >= 0; i--)
                if (Jobs[i].Target == target && Jobs[i].Channel == channel)
                    Jobs.RemoveAt(i);
        }

        static void ReplaceLabel(Text label)
        {
            for (var i = Jobs.Count - 1; i >= 0; i--)
                if (Jobs[i].Label == label)
                    Jobs.RemoveAt(i);
        }

        static void StopLoops()
        {
            for (var i = Jobs.Count - 1; i >= 0; i--)
            {
                if (!Jobs[i].Loop || Jobs[i].Target == null)
                    continue;
                Jobs[i].Target.localRotation = Quaternion.identity;
                Jobs[i].Target.anchoredPosition = Jobs[i].From;
                Jobs.RemoveAt(i);
            }
        }

        static CanvasGroup EnsureGroup(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            if (group == null)
                group = go.AddComponent<CanvasGroup>();
            return group;
        }

        static Sprite RippleSprite()
        {
            if (_ripple != null)
                return _ripple;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = (size - 1) * 0.5f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                var ring = Mathf.Clamp01(1f - Mathf.Abs(distance - 0.72f) * 8f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, ring));
            }
            texture.Apply(false, false);
            _ripple = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            texture.Apply(false, true);
            return _ripple;
        }

        static float EaseOutCubic(float t)
        {
            var v = 1f - t;
            return 1f - v * v * v;
        }

        static float EaseOutBack(float t)
        {
            const float s = 1.70158f;
            var v = t - 1f;
            return 1f + (s + 1f) * v * v * v + s * v * v;
        }
    }

    sealed class CozyMotionHost : MonoBehaviour
    {
        void Update()
        {
            CozyMotion.Tick(Time.unscaledDeltaTime);
        }

        void OnApplicationPause(bool paused)
        {
            CozyMotion.SetPaused(paused);
        }
    }
}
