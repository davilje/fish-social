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

        public const float Tap = 0.06f;
        public const float Snap = 0.22f;
        public const float Slide = 0.20f;
        public const float Hop = 0.42f;
        public const float FloatPeriod = 2.4f;

        public const float Celebrate = 0.9f;
        public const float Stagger = 0.04f;
        public const int StaggerLimit = 8;
        public const float Fade = 0.12f;
        public const int MaxJobs = 64;
        public const int NavigationPriority = 10;
        public const int EmptyStatePriority = 20;
        public const int MapPriority = 30;
        public const int ShowcasePriority = 40;

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
            public bool Loop;
            public float Phase;
            public Action Done;
            public Vector2 Rest;
            public bool HasRest;
            public bool HoldOnCancel;
            public Action Cleanup;
            public Vector3 RestScale = Vector3.one;
            public Quaternion Rotation = Quaternion.identity;
            public int Priority;
            public CozyButtonVisual ButtonVisual;
        }

        static readonly List<Job> Jobs = new List<Job>();
        static readonly List<Job> TickJobs = new List<Job>();
        static readonly List<Job> Loops = new List<Job>();
        static readonly List<CozyMotionHost> Hosts = new List<CozyMotionHost>();
        static Job _loopOwner;
        static bool _paused;
        static Action _finishFlip;
        public static int ActiveJobCount => Jobs.Count + (_loopOwner != null ? 1 : 0);
        public static RectTransform ActiveLoopOwner => _loopOwner != null ? _loopOwner.Target : null;
        public static event Action<bool> ReducedChanged;
        public static event Action<bool> PausedChanged;
        public static bool Paused => _paused;
        static Sprite _bobber;
        static Sprite _ripple;
        static Action _cancelFlip;

        public static bool Reduced { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Jobs.Clear();
            Loops.Clear();
            Hosts.Clear();
            TickJobs.Clear();
            _loopOwner = null;
            _cancelFlip = null;
            _finishFlip = null;
            _paused = false;
            ReducedChanged = null;
            PausedChanged = null;
            Reduced = PlayerPrefs.GetInt(ReducePrefsKey, 0) == 1;
        }

        public static void LoadPreference()
        {
            ApplyReduced(PlayerPrefs.GetInt(ReducePrefsKey, 0) == 1);
        }

        public static void SetReduced(bool value)
        {
            ApplyReduced(value);
            PlayerPrefs.SetInt(ReducePrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        static void ApplyReduced(bool value)
        {
            if (Reduced == value)
                return;
            Reduced = value;
            if (value)
                SettleMotion();
            SelectLoop();
            ReducedChanged?.Invoke(value);
        }

        // The tray/window owner calls this explicitly; OS focus loss alone is not hiding.
        public static void SetPaused(bool paused)
        {
            if (_paused == paused)
                return;
            _paused = paused;
            if (paused)
                SettleMotion();
            SelectLoop();
            PausedChanged?.Invoke(paused);
        }

        static void SettleMotion()
        {
            var finish = _finishFlip;
            _finishFlip = null;
            CancelPageTransition();
            finish?.Invoke();
            var pending = Jobs.ToArray();
            foreach (var job in pending)
            {
                if (!Jobs.Remove(job))
                    continue;
                if (job.Channel == Channel.Scale)
                {
                    if (job.ButtonVisual != null) job.ButtonVisual.SetMotionScale(Vector2.one);
                    else if (job.Target != null) job.Target.localScale = job.RestScale;
                    job.Done?.Invoke();
                }
                else Complete(job);
            }
            RestoreLoop(_loopOwner);
            _loopOwner = null;
        }

        public static void Tick(float delta)
        {
            SelectLoop();
            if (_paused || delta <= 0f)
                return;
            if (_loopOwner != null)
            {
                var loop = _loopOwner;
                loop.Time += delta;
                var wave = Mathf.Sin((loop.Time / FloatPeriod + loop.Phase) * Mathf.PI * 2f);
                loop.Target.anchoredPosition = loop.From + new Vector2(0f, wave * 3f);
                loop.Target.localRotation = loop.Rotation * Quaternion.Euler(0f, 0f, wave * 4f);
            }
            // Completion can cancel another page's jobs or add the next sequence step.
            TickJobs.Clear();
            TickJobs.AddRange(Jobs);
            foreach (var job in TickJobs)
            {
                if (!Jobs.Contains(job))
                    continue;
                if (job.Target == null)
                {
                    Jobs.Remove(job);
                    continue;
                }
                if (!job.Target.gameObject.activeInHierarchy)
                {
                    Jobs.Remove(job);
                    Complete(job);
                    continue;
                }
                job.Time += delta;
                var t = job.Duration <= 0f ? 1f : Mathf.Clamp01(job.Time / job.Duration);
                Apply(job, t);
                if (t >= 1f)
                {
                    Jobs.Remove(job);
                    job.Done?.Invoke();
                }
            }
        }

        static void Apply(Job job, float t)
        {
            if (job.Target == null)
                return;
            var eased = EaseOutCubic(t);
            switch (job.Channel)
            {
                case Channel.Scale:
                    var scale = Vector2.LerpUnclamped(job.From, job.To, EaseOutBack(t));
                    if (job.ButtonVisual != null)
                        job.ButtonVisual.SetMotionScale(scale);
                    else
                        job.Target.localScale = new Vector3(scale.x, scale.y, job.RestScale.z);
                    break;
                case Channel.Anchor:
                    job.Target.anchoredPosition = Vector2.LerpUnclamped(job.From, job.To, eased);
                    break;
                case Channel.Alpha:
                    if (job.Group != null) job.Group.alpha = Mathf.Lerp(job.FromF, job.ToF, eased);
                    break;
                case Channel.Number:
                    if (job.Label != null)
                        job.Label.text = Mathf.RoundToInt(Mathf.Lerp(job.FromF, job.ToF, eased)).ToString("N0");
                    break;
            }
        }

        static void Complete(Job job)
        {
            Apply(job, 1f);
            job.Done?.Invoke();
        }

        static void Add(Job job)
        {
            if (job.Target == null)
                return;
            if (_paused || (Reduced && job.Channel != Channel.Alpha))
            {
                Complete(job);
                return;
            }
            if (Reduced) job.Duration = Mathf.Min(job.Duration, Fade);
            // Preserve sequence completion and transient cleanup even under a burst.
            while (Jobs.Count >= MaxJobs - 1)
            {
                var oldest = Jobs[0];
                Jobs.RemoveAt(0);
                Complete(oldest);
            }
            Jobs.Add(job);
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
            var visual = target.GetComponent<CozyButtonVisual>();
            if (Reduced || _paused)
            {
                if (visual != null) visual.SetMotionScale(Vector2.one);
                return;
            }
            var job = new Job
            {
                Target = target,
                Channel = Channel.Scale,
                Duration = Tap,
                ButtonVisual = visual,
                From = Vector2.one,
                To = new Vector2(1.06f, 0.92f),
            };
            Add(job);
        }

        public static void PressUp(RectTransform target)
        {
            if (target == null)
                return;
            Replace(target, Channel.Scale);
            var visual = target.GetComponent<CozyButtonVisual>();
            if (Reduced || _paused)
            {
                if (visual != null) visual.SetMotionScale(Vector2.one);
                else target.localScale = Vector3.one;
                return;
            }
            var from = visual != null ? visual.MotionScale : new Vector2(target.localScale.x, target.localScale.y);
            Add(new Job
            {
                Target = target,
                Channel = Channel.Scale,
                Duration = Snap * 0.45f,
                ButtonVisual = visual,
                From = from,
                To = new Vector2(0.98f, 1.04f),
                Done = () =>
                {
                    if (target == null)
                        return;
                    Add(new Job
                    {
                        Target = target,
                        Channel = Channel.Scale,
                        Duration = Snap * 0.55f,
                        ButtonVisual = visual,
                        From = new Vector2(0.98f, 1.04f),
                        To = Vector2.one,
                    });
                },
            });
        }

        public static void Shake(RectTransform target)
        {
            if (target == null || Reduced || _paused)
                return;
            if (target.GetComponent<CozyButtonVisual>() != null) return;
            ResetAnchor(target);
            var rest = target.anchoredPosition;
            Nudge(target, rest, rest + new Vector2(-4f, 0f), 0.04f, () =>
                Nudge(target, rest + new Vector2(-4f, 0f), rest + new Vector2(4f, 0f), 0.04f, () =>
                    Nudge(target, rest + new Vector2(4f, 0f), rest, 0.05f, null, rest), rest), rest);
        }

        public static void Punch(RectTransform target)
        {
            if (target == null || Reduced)
                return;
            Replace(target, Channel.Scale);
            Add(new Job
            {
                Target = target,
                Channel = Channel.Scale,
                Duration = Snap,
                ButtonVisual = target.GetComponent<CozyButtonVisual>(),
                From = new Vector2(1.12f, 0.9f),
                To = Vector2.one,
            });
        }

        public static void Float(RectTransform target, float phase, int priority = MapPriority)
        {
            if (target == null) return;
            StopFloat(target);
            Loops.Add(new Job { Target = target, Channel = Channel.Loop, Loop = true,
                Phase = phase, From = target.anchoredPosition, Rotation = target.localRotation,
                Priority = priority });
            SelectLoop();
        }

        public static void StopFloat(RectTransform target)
        {
            for (var i = Loops.Count - 1; i >= 0; i--)
            {
                if (Loops[i].Target != target) continue;
                if (_loopOwner == Loops[i])
                {
                    RestoreLoop(_loopOwner);
                    _loopOwner = null;
                }
                Loops.RemoveAt(i);
            }
            SelectLoop();
        }

        static void SelectLoop()
        {
            Job next = null;
            for (var i = Loops.Count - 1; i >= 0; i--)
            {
                var candidate = Loops[i];
                if (candidate.Target == null) { Loops.RemoveAt(i); continue; }
                if (_paused || Reduced || !candidate.Target.gameObject.activeInHierarchy) continue;
                if (next == null || candidate.Priority > next.Priority) next = candidate;
            }
            if (next == _loopOwner) return;
            RestoreLoop(_loopOwner);
            _loopOwner = next;
        }

        static void RestoreLoop(Job loop)
        {
            if (loop == null || loop.Target == null) return;
            loop.Target.anchoredPosition = loop.From;
            loop.Target.localRotation = loop.Rotation;
            loop.Time = 0f;
        }

        public static void Roll(Text label, int from, int to)
        {
            if (label == null)
                return;
            ReplaceLabel(label);
            if (Reduced || from == to)
            {
                label.text = to.ToString("N0");
                return;
            }
            ReplaceLabel(label);
            Add(new Job
            {
                Target = label.rectTransform,
                Label = label,
                Channel = Channel.Number,
                Duration = 0.4f,
                FromF = from,
                ToF = to,
            });
        }

        /// <summary>Focus animation; cancelling for a drag keeps its current position.</summary>
        public static void MoveTo(RectTransform target, Vector2 destination, float duration = .35f)
        {
            if (target == null) return;
            Replace(target, Channel.Anchor);
            Add(new Job { Target = target, Channel = Channel.Anchor, Duration = duration,
                From = target.anchoredPosition, To = destination, HoldOnCancel = true });
        }

        public static void SlideToast(RectTransform target)
        {
            if (target == null)
                return;
            var group = EnsureGroup(target.gameObject);
            ResetAnchor(target);
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
            Add(new Job
            {
                Target = target,
                Channel = Channel.Anchor,
                Duration = Slide,
                From = target.anchoredPosition,
                To = rest,
            });
            Add(new Job
            {
                Target = target,
                Group = group,
                Channel = Channel.Alpha,
                Duration = Slide,
                FromF = 0f,
                ToF = 1f,
            });
        }

        public static void CancelPageTransition()
        {
            var cancel = _cancelFlip;
            _cancelFlip = null;
            _finishFlip = null;
            cancel?.Invoke();
        }

        public static void Flip(GameObject from, GameObject to, Action done)
        {
            if (to == null)
            {
                if (done != null)
                    done();
                return;
            }
            CancelPageTransition();
            if (Reduced || _paused || from == null || from == to)
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
            _finishFlip = done;
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
            Add(new Job
            {
                Target = fromRt,
                Channel = Channel.Anchor,
                Duration = Fade,
                From = fromRest,
                To = fromRest + new Vector2(-24f, 0f),
            });
            Add(new Job
            {
                Target = fromRt,
                Group = fromGroup,
                Channel = Channel.Alpha,
                Duration = Fade,
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
                    Add(new Job
                    {
                        Target = toRt,
                        Channel = Channel.Anchor,
                        Duration = Slide,
                        From = toRest + new Vector2(24f, 0f),
                        To = toRest,
                    });
                    Add(new Job
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
                            _finishFlip = null;
                            if (done != null)
                                done();
                        },
                    });
                },
            });
        }

        public static void Ripple(RectTransform parent)
        {
            if (parent == null || Reduced || _paused || RippleSprite() == null)
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
            image.color = Color.white;
            image.raycastTarget = false;
            var group = ring.AddComponent<CanvasGroup>();
            Add(new Job
            {
                Target = rect,
                Channel = Channel.Scale,
                Duration = 0.36f,
                From = new Vector2(0.4f, 0.4f),
                To = new Vector2(1.8f, 1.8f),
            });
            Action cleanup = () =>
            {
                if (ring == null) return;
                if (Application.isPlaying) UnityEngine.Object.Destroy(ring);
                else UnityEngine.Object.DestroyImmediate(ring);
            };
            Add(new Job
            {
                Target = rect,
                Cleanup = cleanup,
                Group = group,
                Channel = Channel.Alpha,
                Duration = 0.36f,
                FromF = 0.9f,
                ToF = 0f,
                Done = cleanup,
            });
        }

        public static Sprite BobberSprite()
        {
            if (_bobber != null)
                return _bobber;
            _bobber = Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/motion/bobber");
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
            Float(rect, 0.15f, NavigationPriority);
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
            StopFloat(bobber);
            ResetAnchor(bobber);
            var parent = nav as RectTransform;
            var world = tab.TransformPoint(new Vector3(tab.rect.center.x, tab.rect.yMax, 0f));
            var local = parent != null ? (Vector2)parent.InverseTransformPoint(world) : tab.anchoredPosition;
            var target = local + new Vector2(0f, 10f);
            var from = bobber.anchoredPosition;
            if (Reduced)
            {
                bobber.anchoredPosition = target;
                Float(bobber, 0.15f, NavigationPriority);
                return;
            }
            var peak = Vector2.Lerp(from, target, 0.5f) + new Vector2(0f, 46f);
            Replace(bobber, Channel.Anchor);
            Add(new Job
            {
                Target = bobber,
                Channel = Channel.Anchor,
                Duration = Hop * 0.45f,
                Rest = target,
                HasRest = true,
                From = from,
                To = peak,
                Done = () =>
                {
                    if (bobber == null)
                        return;
                    Add(new Job
                    {
                        Target = bobber,
                        Channel = Channel.Anchor,
                        Duration = Hop * 0.55f,
                        Rest = target,
                        HasRest = true,
                        From = peak,
                        To = target,
                        Done = () =>
                        {
                            Ripple(bobber);
                            Punch(tab);
                            Float(bobber, 0.15f, NavigationPriority);
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
            go.AddComponent<CozyCornerCharacter>();
        }

        public static CozyCatchCelebration CelebrateCatch(Transform parent, string catchId, string fishName,
            Sprite fishSprite, bool isCodexNew, string detail = null)
        {
            if (parent == null) return null;
            var presenter = parent.GetComponent<CozyCatchCelebration>();
            if (presenter == null) presenter = parent.gameObject.AddComponent<CozyCatchCelebration>();
            presenter.Enqueue(catchId, fishName, fishSprite, isCodexNew, detail);
            return presenter;
        }

        public static bool SetCornerCharacter(Transform parent, string explicitSkinId)
        {
            if (parent == null) return false;
            EnsureCornerCat(parent);
            var cat = parent.Find("CozyCornerCat");
            if (cat == null) return false;
            var character = cat.GetComponent<CozyCornerCharacter>() ?? cat.gameObject.AddComponent<CozyCornerCharacter>();
            return character.SetSkin(explicitSkinId);
        }

        public static void SetCornerPose(Transform parent, CozyCornerCharacter.Pose pose, float holdSeconds = 0f)
        {
            if (parent == null) return;
            EnsureCornerCat(parent);
            var cat = parent.Find("CozyCornerCat");
            if (cat == null) return;
            var character = cat.GetComponent<CozyCornerCharacter>() ?? cat.gameObject.AddComponent<CozyCornerCharacter>();
            character.SetPose(pose, holdSeconds);
        }

        static void Nudge(RectTransform target, Vector2 from, Vector2 to, float duration, Action done, Vector2 rest)
        {
            if (target == null)
                return;
            Add(new Job
            {
                Target = target,
                Channel = Channel.Anchor,
                Duration = duration,
                Rest = rest,
                HasRest = true,
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

        static void ResetAnchor(RectTransform target)
        {
            for (var i = Jobs.Count - 1; i >= 0; i--)
                if (Jobs[i].Target == target && Jobs[i].Channel == Channel.Anchor)
                {
                    target.anchoredPosition = Jobs[i].HasRest ? Jobs[i].Rest : Jobs[i].To;
                    Jobs.RemoveAt(i);
                }
        }

        internal static void CancelPress(RectTransform target)
        {
            if (target == null) return;
            Replace(target, Channel.Scale);
            var visual = target.GetComponent<CozyButtonVisual>();
            if (visual != null) visual.SetMotionScale(Vector2.one);
        }

        public static void Cancel(RectTransform target, bool includeChildren = false)
        {
            if (target == null) return;
            for (var i = Jobs.Count - 1; i >= 0; i--)
            {
                var job = Jobs[i];
                if (job.Target != target && !(includeChildren && job.Target != null && job.Target.IsChildOf(target))) continue;
                Jobs.RemoveAt(i);
                if (job.Channel == Channel.Scale)
                {
                    if (job.ButtonVisual != null) job.ButtonVisual.SetMotionScale(Vector2.one);
                    else if (job.Target != null) job.Target.localScale = job.RestScale;
                }
                else if (job.HoldOnCancel) { }
                else if (job.Channel == Channel.Anchor && job.HasRest && job.Target != null)
                    job.Target.anchoredPosition = job.Rest;
                else Apply(job, 1f);
                job.Cleanup?.Invoke();
            }
            for (var i = Loops.Count - 1; i >= 0; i--)
                if (Loops[i].Target == target || (includeChildren && Loops[i].Target != null && Loops[i].Target.IsChildOf(target)))
                {
                    RestoreLoop(Loops[i]);
                    if (_loopOwner == Loops[i]) _loopOwner = null;
                    Loops.RemoveAt(i);
                }
            SelectLoop();
        }

        internal static void RegisterHost(CozyMotionHost host)
        {
            if (!Hosts.Contains(host)) Hosts.Add(host);
        }

        internal static void UnregisterHost(CozyMotionHost host)
        {
            Hosts.Remove(host);
            SelectLoop();
        }

        internal static void TickHost(CozyMotionHost host, float delta)
        {
            for (var i = Hosts.Count - 1; i >= 0; i--)
                if (Hosts[i] == null) Hosts.RemoveAt(i);
            if (Hosts.Count > 0 && Hosts[0] == host) Tick(delta);
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
            _ripple = Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/motion/ripple-ring");
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
        void OnEnable() { CozyMotion.RegisterHost(this); }
        void OnDisable()
        {
            CozyMotion.UnregisterHost(this);
        }
        void OnDestroy() { CozyMotion.Cancel(transform as RectTransform, true); }

        void Update()
        {
            CozyMotion.TickHost(this, Time.unscaledDeltaTime);
        }

        void OnApplicationPause(bool paused)
        {
            CozyMotion.SetPaused(paused);
        }
    }
}
