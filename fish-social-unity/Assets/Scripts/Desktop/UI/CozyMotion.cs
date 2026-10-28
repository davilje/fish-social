using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Journal motion tokens from docs/设计/界面/手账/2026-10-21-cozy-journal-motion.md.
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

        enum Channel { Scale, Anchor, Alpha, Number, Loop, Custom }

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
            public CozyWebMotion.Ease Ease = CozyWebMotion.Ease.OutCubic;
            public Action<float> Sample;
            public float Delay;
            public bool FadeWhenReduced;
        }

        static readonly List<Job> Jobs = new List<Job>();
        static readonly List<Job> TickJobs = new List<Job>();
        static readonly List<Job> Loops = new List<Job>();
        static readonly List<CozyMotionHost> Hosts = new List<CozyMotionHost>();
        static Job _loopOwner;
        static bool _paused;
        static Action _finishFlip;
        /// <summary>Home open parks the sticker entrance at frame 0 until the sheet has risen.</summary>
        public static bool EntranceHeld { get; private set; }
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
            EntranceHeld = false;
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
                var phase = Mathf.Repeat(loop.Time / (loop.Priority == EmptyStatePriority ? 1.5f : FloatPeriod * .5f) + loop.Phase * 2, 2);
                var progress = phase <= 1 ? phase : 2 - phase;
                var wave = Mathf.LerpUnclamped(-1, 1, CozyWebMotion.Evaluate(CozyWebMotion.Ease.InOutSine, progress));
                if (loop.Priority == EmptyStatePriority)
                    loop.Target.localScale = Vector3.Scale(loop.RestScale,
                        Vector3.LerpUnclamped(Vector3.one, new Vector3(1.02f, .97f, 1), (wave + 1) / 2));
                else if (loop.Priority == ShowcasePriority)
                    loop.Target.localRotation = loop.Rotation * Quaternion.Euler(0, 0, -wave * 3);
                else
                {
                    loop.Target.anchoredPosition = loop.From + new Vector2(0f, -wave * 3f);
                    loop.Target.localRotation = loop.Rotation * Quaternion.Euler(0f, 0f, -wave * 4f);
                }
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
                var t = job.Duration <= 0f ? 1f : Mathf.Clamp01((job.Time - job.Delay) / job.Duration);
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
            var eased = CozyWebMotion.Evaluate(job.Ease, t);
            switch (job.Channel)
            {
                case Channel.Scale:
                    var scale = Vector2.LerpUnclamped(job.From, job.To, eased);
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
                case Channel.Custom:
                    job.Sample?.Invoke(eased);
                    break;
                case Channel.Number:
                    if (job.Label != null)
                        DesktopLocalization.SetText(job.Label, Mathf.RoundToInt(Mathf.Lerp(job.FromF, job.ToF, eased)).ToString("N0"));
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
            if (_paused || (Reduced && job.Channel != Channel.Alpha && !job.FadeWhenReduced))
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
            Replace(target, Channel.Custom);
            var visual = target.GetComponent<CozyButtonVisual>();
            if (visual != null) visual.SetMotionOffset(Vector2.zero);
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
                Ease = CozyWebMotion.Ease.OutQuad,
                From = Vector2.one,
                To = visual != null && visual.LightPress ? new Vector2(1.02f, .96f) : new Vector2(1.06f, .92f),
            };
            Add(job);
        }

        public static void PressUp(RectTransform target)
        {
            if (target == null)
                return;
            Replace(target, Channel.Scale);
            Replace(target, Channel.Custom);
            var visual = target.GetComponent<CozyButtonVisual>();
            if (visual != null) visual.SetMotionOffset(Vector2.zero);
            if (Reduced || _paused)
            {
                if (visual != null) visual.SetMotionScale(Vector2.one);
                else target.localScale = Vector3.one;
                return;
            }
            var light = visual != null && visual.LightPress;
            var down = light ? new Vector2(1.02f, .96f) : new Vector2(1.06f, .92f);
            var rebound = light ? new Vector2(.99f, 1.02f) : new Vector2(.98f, 1.04f);
            Add(new Job { Target = target, Channel = Channel.Custom, Duration = Snap,
                Ease = CozyWebMotion.Ease.OutCubic, Sample = t =>
                {
                    if (target == null) return;
                    var value = t <= .5f ? Vector2.LerpUnclamped(down, rebound, t * 2)
                        : Vector2.LerpUnclamped(rebound, Vector2.one, (t - .5f) * 2);
                    if (visual != null) visual.SetMotionScale(value);
                    else target.localScale = new Vector3(value.x, value.y, 1);
                }});
        }

        public static void Shake(RectTransform target)
        {
            if (target == null || Reduced || _paused) return;
            var visual = target.GetComponent<CozyButtonVisual>();
            var rest = target.anchoredPosition;
            Animate(target, .18f, t =>
            {
                if (target == null) return;
                var x = CozyWebMotion.Frames(t, 0, -4, 4, -3, 3, 0);
                if (visual != null) visual.SetMotionOffset(new Vector2(x, 0));
                else target.anchoredPosition = rest + new Vector2(x, 0);
            }, null, CozyWebMotion.Ease.Linear);
        }

        public static void Animate(RectTransform target, float duration, Action<float> sample,
            Action done = null, CozyWebMotion.Ease easing = CozyWebMotion.Ease.OutCubic, float delay = 0, Action cleanup = null,
            bool fadeWhenReduced = false)
        {
            if (target == null) return;
            Replace(target, Channel.Custom);
            Add(new Job { Target = target, Channel = Channel.Custom, Duration = duration,
                Ease = easing, Sample = sample, Done = done, Delay = Reduced ? 0 : delay, Cleanup = cleanup,
                FadeWhenReduced = fadeWhenReduced });
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
                Ease = CozyWebMotion.Ease.OutBack,
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
                Phase = phase, From = target.anchoredPosition, Rotation = target.localRotation, RestScale = target.localScale,
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
            loop.Target.localScale = loop.RestScale;
            loop.Time = 0f;
        }

        public static void Roll(Text label, int from, int to)
        {
            if (label == null)
                return;
            ReplaceLabel(label);
            if (Reduced || from == to)
            {
                DesktopLocalization.SetText(label, to.ToString("N0"));
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
            target.anchoredPosition = rest + new Vector2(460f, 0f);
            group.alpha = 0f;
            Replace(target, Channel.Anchor);
            Replace(target, Channel.Alpha);
            Add(new Job
            {
                Target = target,
                Channel = Channel.Anchor,
                Duration = .42f,
                From = target.anchoredPosition,
                To = rest,
            });
            Add(new Job
            {
                Target = target,
                Group = group,
                Channel = Channel.Alpha,
                Duration = .42f,
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

        /// <summary>Workspace edit/return: old body fades for 100ms, new sections enter
        /// on their own clocks. Paper, binding and index rail stay stationary.</summary>
        public static void RefreshContent(GameObject from, GameObject to, Action done)
        {
            CancelPageTransition();
            if (to == null) { done?.Invoke(); return; }
            CozyJournalMotionBindings.ConfigureTree(to.transform);
            to.SetActive(true);
            var group=EnsureGroup(to);
            var incoming=to.GetComponent<CozyJournalFlipPose>()??to.AddComponent<CozyJournalFlipPose>();
            incoming.ResetPose();
            if (_paused || from==null || from==to)
            {
                if(from!=null&&from!=to)from.SetActive(false);
                group.alpha=1;group.blocksRaycasts=true;
                if(_paused)SettleJournalPage(to);else StartJournalEntrance(to,false);
                done?.Invoke();return;
            }
            var fromRect=(RectTransform)from.transform;
            var oldGroup=EnsureGroup(from);
            var oldRegions=from.GetComponentsInChildren<CozyJournalPageMotion>();
            foreach(var region in oldRegions)region.BeginExit();
            group.alpha=0;group.blocksRaycasts=oldGroup.blocksRaycasts=false;
            _cancelFlip=()=>{Settle();SettleJournalPage(to);};_finishFlip=done;
            Animate(fromRect,.1f,t=>{foreach(var region in oldRegions)if(region!=null)region.SampleExit(t*.1f);},()=>
            {
                Settle();StartJournalEntrance(to,false);
                _cancelFlip=null;_finishFlip=null;done?.Invoke();
            },CozyWebMotion.Ease.Linear,fadeWhenReduced:true);
            void Settle()
            {
                Drop(fromRect);
                foreach(var region in oldRegions)if(region!=null)region.Finish();
                if(from!=null){oldGroup.alpha=1;oldGroup.blocksRaycasts=true;from.SetActive(false);}
                if(to!=null){group.alpha=1;group.blocksRaycasts=true;incoming.ResetPose();}
            }
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
            CozyJournalMotionBindings.ConfigureTree(to.transform);
            if (_paused || from == null || from == to)
            {
                if (from != null && from != to)
                    from.SetActive(false);
                to.SetActive(true);
                var settled = EnsureGroup(to);
                settled.alpha = 1f;
                settled.blocksRaycasts = true;
                // The animated paths reveal once inside StartJournalEntrance.
                // Reduced motion still has to settle that same entrance, or the page stays mid-pose.
                if (Reduced || _paused)
                    SettleJournalPage(to);
                if (done != null)
                    done();
                return;
            }

            if (from.name == "Home" || to.name == "Home")
            {
                JournalDoor(from, to, done, from.name == "Home");
                return;
            }

            var fromRt = (RectTransform)from.transform;
            var toRt = (RectTransform)to.transform;
            var fromGroup = EnsureGroup(from);
            var toGroup = EnsureGroup(to);
            var outgoing = from.GetComponent<CozyJournalFlipPose>();
            if (outgoing == null) outgoing = from.AddComponent<CozyJournalFlipPose>();
            var incoming = to.GetComponent<CozyJournalFlipPose>();
            if (incoming == null) incoming = to.AddComponent<CozyJournalFlipPose>();
            to.SetActive(true);
            // Only the old sheet is visible during its content exit. The new paper replaces it at 120ms.
            toGroup.alpha = 0;
            fromGroup.blocksRaycasts = toGroup.blocksRaycasts = false;
            Action settle = () =>
            {
                Drop(fromRt); Drop(toRt);
                if (outgoing != null) outgoing.ResetPose();
                if (incoming != null) incoming.ResetPose();
                if (fromGroup != null) { fromGroup.alpha=1; fromGroup.blocksRaycasts=true; }
                if (toGroup != null) { toGroup.alpha=1; toGroup.blocksRaycasts=true; }
                if (from != null) from.SetActive(false);
            };
            _cancelFlip=settle; _finishFlip=done;
            Animate(fromRt, .12f, t => outgoing.Sample(Reduced ? 0 : -24*t,1-t), () =>
            {
                if (from != null) from.SetActive(false);
                if (to == null) { settle(); _cancelFlip=null; _finishFlip=null; done?.Invoke(); return; }
                toGroup.alpha=1;
                incoming.Sample(Reduced ? 0 : 24,0,Reduced ? 0 : 10,0);
                StartJournalEntrance(to,false);
                Animate(toRt,Slide,t => incoming.Sample(Reduced ? 0 : 24*(1-t),t,Reduced ? 0 : 10*(1-t),t), () =>
                {
                    settle(); _cancelFlip=null; _finishFlip=null; done?.Invoke();
                }, CozyWebMotion.Ease.OutCubic, fadeWhenReduced: true);
            }, CozyWebMotion.Ease.InQuad, fadeWhenReduced: true);
        }

        /// <summary>Shared bottom-navigation sheet entrance: 16px rise, 200ms OutCubic.</summary>
        public static void OpenJournalSheet(RectTransform sheet, Vector2 rest, Action done = null, Action<float> progress = null)
        {
            if (sheet == null) { done?.Invoke(); return; }
            var group = EnsureGroup(sheet.gameObject);
            void Sample(float t)
            {
                if (sheet == null) return;
                sheet.anchoredPosition = rest + Vector2.down * (Reduced ? 0 : 16 * (1-t));
                group.alpha = t;
                progress?.Invoke(t);
            }
            Sample(0);
            Animate(sheet, Slide, Sample, () => { Sample(1); done?.Invoke(); },
                CozyWebMotion.Ease.OutCubic, cleanup: () => Sample(1), fadeWhenReduced: true);
        }

        static void JournalDoor(GameObject from, GameObject to, Action done, bool opening)
        {
            var fromRect = (RectTransform)from.transform; var toRect = (RectTransform)to.transform;
            var fromRest = fromRect.anchoredPosition; var toRest = toRect.anchoredPosition;
            var fromGroup = EnsureGroup(from); var toGroup = EnsureGroup(to);
            to.SetActive(true); fromGroup.blocksRaycasts = false; toGroup.blocksRaycasts = false;
            var moving = opening ? toRect : fromRect;
            var content = to.GetComponent<CozyJournalFlipPose>() ?? to.AddComponent<CozyJournalFlipPose>();
            var entrancePlayed = false;
            if (opening)
            {
                EntranceHeld = true;
                content.CoverContent();
                toRect.anchoredPosition = toRest + Vector2.down * (Reduced ? 0 : 16);
                toGroup.alpha = 0;
            }
            _cancelFlip = settle; _finishFlip = done;
            if (opening) OpenJournalSheet(toRect, toRest, complete);
            else Add(new Job { Target = moving, Channel = Channel.Custom, Duration = .14f, FadeWhenReduced = true,
                Ease = CozyWebMotion.Ease.InQuad,
                Sample = t =>
                {
                    fromRect.anchoredPosition = fromRest + Vector2.down * (Reduced ? 0 : 12 * t);
                    fromGroup.alpha = 1 - t;
                },
                Done = complete });

            void complete()
            {
                entrancePlayed = true;
                // Install the contents' first frame before uncovering them.
                if (opening) StartJournalEntrance(to, true);
                settle();
                _cancelFlip = null;
                _finishFlip = null;
                done?.Invoke();
            }

            void settle()
            {
                var aborted = EntranceHeld && !entrancePlayed;
                EntranceHeld = false;
                if (content != null) content.ResetPose();
                Drop(fromRect); Drop(toRect);
                if (from != null) { fromRect.anchoredPosition = fromRest; fromGroup.alpha = 1; fromGroup.blocksRaycasts = true; from.SetActive(false); }
                if (to != null) { toRect.anchoredPosition = toRest; toGroup.alpha = 1; toGroup.blocksRaycasts = true; }
                if (aborted)
                    SettleJournalPage(to, false);
            }
        }

        static void SettleJournalPage(GameObject page, bool revealPolaroid = true)
        {
            if (page == null) return;
            foreach (var sticker in page.GetComponentsInChildren<CozyStickerMotion>())
                sticker.ApplyFinalPose();
            foreach (var motion in page.GetComponentsInChildren<CozyJournalPageMotion>()) motion.Finish();
            foreach (var body in page.GetComponentsInChildren<CozyWorkspaceBodyMotion>()) body.Finish();
            if (revealPolaroid)
                foreach (var pose in page.GetComponentsInChildren<CozyJournalStaticPose>()) pose.ApplyFinalPose();
        }

        static void StartJournalEntrance(GameObject page, bool rail)
        {
            if(page==null) return;
            var owner = page.GetComponent<CozyJournalFlipPose>() ?? page.AddComponent<CozyJournalFlipPose>();
            owner.BeginEntrance();
            // Initial selection is data binding, not a user selection. The Web entrance fades
            // the entire aside; it does not append a second polaroid reveal after that fade.
            foreach (var pose in page.GetComponentsInChildren<CozyJournalStaticPose>()) pose.ApplyFinalPose();
            foreach(var sticker in page.GetComponentsInChildren<CozyStickerMotion>()) sticker.RestartEntrance();
            foreach(var motion in page.GetComponentsInChildren<CozyJournalPageMotion>())
                motion.PlayEntrance(motion.GetComponentsInChildren<CozyStickerMotion>().Length);
            foreach(var body in page.GetComponentsInChildren<CozyWorkspaceBodyMotion>()) body.PlayEntrance();
            if(!rail) return;
            CozyButtonVisual.BeginRailEntrance(page.transform);
            for(var i=0;i<3;i++)
                SnapRing(FindRing(page.transform, i), i);
        }

        /// <summary>Rings live on the page prefab, one level under the router slot.</summary>
        static RectTransform FindRing(Transform page, int index)
        {
            if (page == null) return null;
            var name = DesktopLocalization.Concat("JournalRing", index);
            var direct = page.Find(name) as RectTransform;
            if (direct != null) return direct;
            foreach (var rect in page.GetComponentsInChildren<RectTransform>(true))
                if (rect.name == name) return rect;
            return null;
        }

        /// <summary>app.js scale 1 0.6 → 1 1, origin center. Prefab pivots are the top-left corner.</summary>
        static void SnapRing(RectTransform ring, int index)
        {
            if (ring == null || Reduced || _paused) return;
            var rest = ring.anchoredPosition;
            var size = ring.rect.size;
            if (size.y < 1f) size = ring.sizeDelta;
            var pivot = ring.pivot;
            void Pose(float t)
            {
                var scaleY = Mathf.LerpUnclamped(.6f, 1f, t);
                ring.localScale = new Vector3(1f, scaleY, 1f);
                ring.anchoredPosition = rest + new Vector2(0f, (0.5f - pivot.y) * size.y * (1f - scaleY));
            }
            Pose(0f);
            Animate(ring, .08f, Pose, null, CozyWebMotion.Ease.OutBack, index * .03f);
        }

        public static void Ripple(RectTransform parent, int count = 2, float width = 90, float height = 34)
        {
            if (parent == null || Reduced || _paused || RippleSprite() == null) return;
            for (var i = 0; i < count; i++)
            {
                var ring = new GameObject("CozyRipple", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                ring.transform.SetParent(parent.parent != null ? parent.parent : parent, false);
                var rect = ring.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
                rect.sizeDelta = new Vector2(width, height);
                rect.position = parent.TransformPoint(new Vector3(parent.rect.center.x, parent.rect.yMin, 0));
                var image = ring.GetComponent<Image>();
                image.sprite = RippleSprite(); image.color = Color.white; image.raycastTarget = false;
                var group = ring.AddComponent<CanvasGroup>();
                Action cleanup = () =>
                {
                    if (ring == null) return;
                    if (Application.isPlaying) UnityEngine.Object.Destroy(ring); else UnityEngine.Object.DestroyImmediate(ring);
                };
                Add(new Job { Target = rect, Channel = Channel.Custom, Duration = .36f, Delay = i * .12f,
                    Ease = CozyWebMotion.Ease.OutCubic, Cleanup = cleanup, Done = cleanup, Sample = t =>
                    {
                        if (rect == null) return;
                        rect.localScale = Vector3.one * Mathf.LerpUnclamped(.4f, 1.5f, t);
                        group.alpha = .9f * (1 - t);
                    }});
            }
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
            if (nav == null) return;
            var existing = nav.Find("CozyBobber");
            var go = existing != null ? existing.gameObject
                : new GameObject("CozyBobber", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (existing == null) go.transform.SetParent(nav, false);
            var rect = go.GetComponent<RectTransform>();
            var navRect = nav as RectTransform;
            // Normalize persisted older prefabs too. CSS 50% 80% is Unity pivot (.5,.2).
            rect.anchorMin = rect.anchorMax = navRect != null ? navRect.pivot : new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .2f);
            rect.sizeDelta = new Vector2(40f, 58f);
            var image = go.GetComponent<Image>();
            if (image == null) image = go.AddComponent<Image>();
            image.sprite = BobberSprite();
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = Color.white;
            if (go.GetComponent<CozyBobberNavigation>() == null) go.AddComponent<CozyBobberNavigation>();
        }

        public static void MoveBobber(Transform nav, RectTransform tab)
        {
            if (nav == null) return;
            EnsureBobber(nav);
            var bobber = nav.Find("CozyBobber");
            if (bobber == null) return;
            var motion = bobber.GetComponent<CozyBobberNavigation>();
            if (motion == null) motion = bobber.gameObject.AddComponent<CozyBobberNavigation>();
            if (tab == null) { motion.Hide(); return; }
            Canvas.ForceUpdateCanvases();
            var parent = nav as RectTransform;
            var world = tab.TransformPoint(tab.rect.center);
            var local = parent != null ? (Vector2)parent.InverseTransformPoint(world) : tab.anchoredPosition;
            // CSS top -60 + transform-origin 58*.8 -> pivot 13.6 above nav top.
            // Therefore the SVG body center (y=30) is 30px above nav top, matching source y=777 at nav y=807.
            var target = new Vector2(local.x, parent != null ? parent.rect.yMax + 13.6f : local.y + 13.6f);
            var icon = tab.Find("Icon") as RectTransform ?? tab.Find("CozyIcon") as RectTransform;
            motion.Move(target, icon);
        }

        public static void EnsureCornerCat(Transform parent)
        {
            if (parent != null && parent.Find("HomeRoom") != null) return;
            if (parent == null || parent.Find("CozyCornerCat") != null)
                return;
            var sprite = Resources.Load<Sprite>("Desktop/Theme/Cozy/journal/illustrations/cat-fishing")
                ?? Resources.Load<Sprite>("Desktop/Theme/Cozy/characters/poses/orange-ready");
            if (sprite == null)
                sprite = Resources.Load<Sprite>("Desktop/Theme/Cozy/characters/avatars/orange");
            if (sprite == null)
                return;
            var go = new GameObject("CozyCornerCat", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(12f, -4f);
            rect.sizeDelta = new Vector2(250f, 250f * sprite.rect.height / sprite.rect.width);
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            go.AddComponent<CozyCornerCharacter>();
        }

        public static CozyCatchCelebration CelebrateCatch(Transform parent, string catchId, string fishName,
            Sprite fishSprite, bool isCodexNew, string detail = null, float? sizeM = null)
        {
            if (parent == null) return null;
            var presenter = parent.GetComponent<CozyCatchCelebration>();
            if (presenter == null) presenter = parent.gameObject.AddComponent<CozyCatchCelebration>();
            presenter.Enqueue(catchId, fishName, fishSprite, isCodexNew, detail, sizeM);
            return presenter;
        }

        public static bool SetCornerCharacter(Transform parent, string explicitSkinId)
        {
            if (parent != null && parent.Find("HomeRoom") != null) return false;
            if (parent == null) return false;
            EnsureCornerCat(parent);
            var cat = parent.Find("CozyCornerCat");
            if (cat == null) return false;
            var character = cat.GetComponent<CozyCornerCharacter>();
            if (character == null) character = cat.gameObject.AddComponent<CozyCornerCharacter>();
            return character.SetSkin(explicitSkinId);
        }

        public static void SetCornerPose(Transform parent, CozyCornerCharacter.Pose pose, float holdSeconds = 0f)
        {
            if (parent != null && parent.Find("HomeRoom") != null) return;
            if (parent == null) return;
            EnsureCornerCat(parent);
            var cat = parent.Find("CozyCornerCat");
            if (cat == null) return;
            var character = cat.GetComponent<CozyCornerCharacter>();
            if (character == null) character = cat.gameObject.AddComponent<CozyCornerCharacter>();
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
            Replace(target, Channel.Custom);
            var visual = target.GetComponent<CozyButtonVisual>();
            if (visual != null) { visual.SetMotionScale(Vector2.one); visual.SetMotionOffset(Vector2.zero); }
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
