using System.Collections.Generic;
using FishSocial.Desktop.Pet;

namespace FishSocial.Desktop.Onboarding
{
    /// <summary>
    /// STEAM-DESKTOP-11: scripted Overlay tutorial. No socket / ecology / bait.
    /// Phases follow the Web fishing SM: baiting → casting → waiting → hooked → resolving.
    /// </summary>
    public sealed class LocalOnboardingSession
    {
        public const string PondId = DesktopOnboardingController.NovicePondId;
        public const string PondName = "新手练习塘";
        public const int BaitingMs = 800;
        public const int CastingMs = 600;
        public const int WaitBiteMs = 5000;
        public const int HookMs = 5000;
        public const int ResolvingMs = 800;
        public const int CatchPromptMs = 5000;

        public const string PromptCatch = "catch";
        public const string PromptComplete = "complete";

        static readonly NativeOverlaySpotDto[] TutorialSpots =
        {
            Spot("novice-spot-1", 140f, 260f),
            Spot("novice-spot-2", 240f, 200f),
            Spot("novice-spot-3", 360f, 180f),
            Spot("novice-spot-4", 480f, 200f),
            Spot("novice-spot-5", 580f, 260f),
            Spot("novice-spot-6", 240f, 340f),
            Spot("novice-spot-7", 360f, 360f),
            Spot("novice-spot-8", 480f, 340f),
        };

        public string Phase { get; private set; } = "idle";
        public string SpotId { get; private set; } = string.Empty;
        public long HookDeadlineMs { get; private set; }
        public long PhaseEndsAtMs { get; private set; }
        public long FishingStartedAt { get; private set; }
        public string LastError { get; private set; } = string.Empty;
        public bool NeedsCatchGrant { get; private set; }
        public string OverlayPromptKind { get; private set; } = string.Empty;
        public string OverlayPromptTitle { get; private set; } = string.Empty;
        public string OverlayPromptBody { get; private set; } = string.Empty;
        public string OverlayPromptButton { get; private set; } = string.Empty;
        public long OverlayPromptDeadlineMs { get; private set; }

        public bool HasSpot => !string.IsNullOrEmpty(SpotId);
        public bool HasPrompt => !string.IsNullOrEmpty(OverlayPromptKind);
        public bool IsBusy => HasPrompt || NeedsCatchGrant;

        public void Reset()
        {
            Phase = "idle";
            SpotId = string.Empty;
            HookDeadlineMs = 0;
            PhaseEndsAtMs = 0;
            FishingStartedAt = 0;
            LastError = string.Empty;
            NeedsCatchGrant = false;
            ClearPrompt();
        }

        public bool Tick(long nowMs)
        {
            var changed = false;
            if (string.Equals(OverlayPromptKind, PromptCatch, System.StringComparison.Ordinal) &&
                OverlayPromptDeadlineMs > 0 &&
                nowMs >= OverlayPromptDeadlineMs)
            {
                ShowCompletePrompt();
                changed = true;
            }

            if (!IsTimedFishing || nowMs < PhaseEndsAtMs)
                return changed;

            AdvanceTimedPhase(nowMs);
            return true;
        }

        public bool ConsumeNeedsCatchGrant()
        {
            if (!NeedsCatchGrant)
                return false;
            NeedsCatchGrant = false;
            return true;
        }

        public void RestoreNeedsCatchGrant()
        {
            NeedsCatchGrant = true;
        }

        public void ShowCatchPrompt(long nowMs)
        {
            OverlayPromptKind = PromptCatch;
            OverlayPromptTitle = "🎣 鱼上钩了！";
            OverlayPromptBody =
                "鲫鱼\n【灰色】\n尺寸：0.18 米\n已自动收入背包";
            OverlayPromptButton = "获得";
            OverlayPromptDeadlineMs = nowMs + CatchPromptMs;
            LastError = string.Empty;
        }

        public bool ConfirmOverlayPrompt()
        {
            if (string.Equals(OverlayPromptKind, PromptCatch, System.StringComparison.Ordinal))
            {
                ShowCompletePrompt();
                return false;
            }

            if (string.Equals(OverlayPromptKind, PromptComplete, System.StringComparison.Ordinal))
            {
                ClearPrompt();
                return true;
            }

            return false;
        }

        public bool TryCommand(string command, string spotId, out string error)
        {
            error = null;
            LastError = string.Empty;
            switch (command)
            {
                case "take_spot":
                    return TakeSpot(spotId, out error);
                case "leave_spot":
                    return LeaveSpot(out error);
                case "start_fishing":
                    return StartFishing(out error);
                case "stop_fishing":
                    return StopFishing(out error);
                case "confirm_overlay_prompt":
                    return true;
                case "exit_pond":
                    error = "新手引导不可跳过。";
                    LastError = error;
                    return false;
                case "send_pond_chat":
                    error = "教学关暂不支持聊天。";
                    LastError = error;
                    return false;
                default:
                    return false;
            }
        }

        public void SetError(string error)
        {
            LastError = error ?? string.Empty;
        }

        public void FillOverlayState(
            NativeOverlayStateDto dto, string nickname, string playerId)
        {
            if (dto == null)
                return;

            dto.connectionState = "Connected";
            dto.pondId = PondId;
            dto.pondName = PondName;
            dto.fishingPhase = Phase;
            dto.petVisualState = PetStateController.ToWire(
                PetStateController.FromFishingPhase(Phase));
            dto.ownNickname = string.IsNullOrEmpty(nickname) ? "新钓手" : nickname;
            dto.ownPlayerId = playerId ?? string.Empty;
            dto.ownUserId = "local-onboarding";
            dto.ownPetId = DesktopDefaultAvatars.ResolvePetId(
                DesktopProfileCache.Latest != null ? DesktopProfileCache.Latest.avatarUrl : null,
                playerId);
            dto.ownSpotId = SpotId;
            dto.hasPendingCatch = false;
            dto.hookDeadlineMs = HookDeadlineMs;
            dto.ownFishingStartedAt = FishingStartedAt;
            dto.sessionFishingMs = ResolveSessionFishingMs();
            dto.spots = TutorialSpots;
            dto.users = new NativeOverlayActorDto[0];
            dto.recentChats = new NativeOverlayChatDto[0];
            dto.observation = null;
            dto.availableActions = MapActions();
            dto.guideTip = ResolveGuideTip();
            dto.errorMessage = LastError ?? string.Empty;
            dto.lockFeatureNav = true;
            dto.overlayPromptKind = OverlayPromptKind ?? string.Empty;
            dto.overlayPromptTitle = OverlayPromptTitle ?? string.Empty;
            dto.overlayPromptBody = OverlayPromptBody ?? string.Empty;
            dto.overlayPromptButton = OverlayPromptButton ?? string.Empty;
            dto.overlayPromptDeadlineMs = OverlayPromptDeadlineMs;

            dto.hasOwnPosition = false;
            dto.ownX = 0f;
            dto.ownY = 0f;
            if (HasSpot)
            {
                for (var i = 0; i < TutorialSpots.Length; i++)
                {
                    var spot = TutorialSpots[i];
                    if (spot == null || spot.id != SpotId)
                        continue;
                    dto.ownX = spot.x;
                    dto.ownY = spot.y;
                    dto.hasOwnPosition = true;
                    break;
                }
            }
        }

        void ShowCompletePrompt()
        {
            OverlayPromptKind = PromptComplete;
            OverlayPromptTitle = "新手引导已完成";
            OverlayPromptBody = "教学关结束。点击确认后将离开练习塘，可以选择开放鱼塘。";
            OverlayPromptButton = "确认";
            OverlayPromptDeadlineMs = 0;
            LastError = string.Empty;
        }

        void ClearPrompt()
        {
            OverlayPromptKind = string.Empty;
            OverlayPromptTitle = string.Empty;
            OverlayPromptBody = string.Empty;
            OverlayPromptButton = string.Empty;
            OverlayPromptDeadlineMs = 0;
        }

        void AdvanceTimedPhase(long nowMs)
        {
            LastError = string.Empty;
            if (string.Equals(Phase, "baiting", System.StringComparison.Ordinal))
            {
                EnterPhase("casting", nowMs, CastingMs);
                return;
            }

            if (string.Equals(Phase, "casting", System.StringComparison.Ordinal))
            {
                EnterPhase("waiting", nowMs, WaitBiteMs);
                return;
            }

            if (string.Equals(Phase, "waiting", System.StringComparison.Ordinal))
            {
                EnterPhase("hooked", nowMs, HookMs);
                HookDeadlineMs = PhaseEndsAtMs;
                return;
            }

            if (string.Equals(Phase, "hooked", System.StringComparison.Ordinal))
            {
                HookDeadlineMs = 0;
                EnterPhase("resolving", nowMs, ResolvingMs);
                return;
            }

            if (string.Equals(Phase, "resolving", System.StringComparison.Ordinal))
            {
                Phase = "seated";
                PhaseEndsAtMs = 0;
                HookDeadlineMs = 0;
                FishingStartedAt = 0;
                NeedsCatchGrant = true;
            }
        }

        void EnterPhase(string phase, long nowMs, int durationMs)
        {
            Phase = phase;
            PhaseEndsAtMs = nowMs + durationMs;
        }

        bool TakeSpot(string spotId, out string error)
        {
            error = null;
            if (IsBusy)
            {
                error = "请先关闭当前提示。";
                LastError = error;
                return false;
            }
            if (IsFishing)
            {
                error = "请先结束当前这一杆。";
                LastError = error;
                return false;
            }
            if (string.IsNullOrEmpty(spotId) || !IsKnownSpot(spotId))
            {
                error = "请点击岸边空钓位坐下。";
                LastError = error;
                return false;
            }
            SpotId = spotId;
            Phase = "seated";
            return true;
        }

        bool LeaveSpot(out string error)
        {
            error = null;
            if (!HasSpot)
                return true;
            if (HasPrompt || NeedsCatchGrant || IsFishing)
            {
                error = "请先结束当前这一杆。";
                LastError = error;
                return false;
            }
            SpotId = string.Empty;
            Phase = "idle";
            return true;
        }

        bool StartFishing(out string error)
        {
            error = null;
            if (IsBusy)
            {
                error = "请先关闭当前提示。";
                LastError = error;
                return false;
            }
            if (!HasSpot)
            {
                error = "请先选择钓位。";
                LastError = error;
                return false;
            }
            if (IsFishing)
            {
                error = "已经在钓鱼。";
                LastError = error;
                return false;
            }
            var now = NowMs();
            FishingStartedAt = now;
            HookDeadlineMs = 0;
            EnterPhase("baiting", now, BaitingMs);
            return true;
        }

        bool StopFishing(out string error)
        {
            error = null;
            if (string.Equals(Phase, "hooked", System.StringComparison.Ordinal))
            {
                Phase = "seated";
                HookDeadlineMs = 0;
                PhaseEndsAtMs = 0;
                FishingStartedAt = 0;
                error = "收杆会跑鱼。请等圆圈走完，鱼获会自动入包。";
                LastError = error;
                return true;
            }
            if (string.Equals(Phase, "baiting", System.StringComparison.Ordinal) ||
                string.Equals(Phase, "casting", System.StringComparison.Ordinal) ||
                string.Equals(Phase, "waiting", System.StringComparison.Ordinal))
            {
                Phase = "seated";
                PhaseEndsAtMs = 0;
                FishingStartedAt = 0;
                return true;
            }
            error = "当前未在钓鱼。";
            LastError = error;
            return false;
        }

        bool IsFishing
        {
            get
            {
                return string.Equals(Phase, "baiting", System.StringComparison.Ordinal) ||
                       string.Equals(Phase, "casting", System.StringComparison.Ordinal) ||
                       string.Equals(Phase, "waiting", System.StringComparison.Ordinal) ||
                       string.Equals(Phase, "hooked", System.StringComparison.Ordinal) ||
                       string.Equals(Phase, "resolving", System.StringComparison.Ordinal);
            }
        }

        bool IsTimedFishing => IsFishing;

        string[] MapActions()
        {
            var actions = new List<string>();
            if (IsBusy)
                return actions.ToArray();
            if (!HasSpot)
                actions.Add("take_spot");
            if (HasSpot && !IsFishing)
            {
                actions.Add("start_fishing");
                actions.Add("leave_spot");
            }
            if (IsFishing &&
                !string.Equals(Phase, "resolving", System.StringComparison.Ordinal))
                actions.Add("stop_fishing");
            return actions.ToArray();
        }

        string ResolveGuideTip()
        {
            if (HasPrompt)
                return string.Empty;
            if (NeedsCatchGrant)
                return "⑤ 正在收入背包…";
            if (!HasSpot)
                return "① 左键点击空钓位坐下（岸边圆点）。";
            if (string.Equals(Phase, "seated", System.StringComparison.Ordinal) ||
                string.Equals(Phase, "idle", System.StringComparison.Ordinal))
                return "② 点右下角「开始钓鱼」。";
            if (string.Equals(Phase, "baiting", System.StringComparison.Ordinal))
                return "③ 正在装饵…";
            if (string.Equals(Phase, "casting", System.StringComparison.Ordinal))
                return "③ 正在抛竿…";
            if (string.Equals(Phase, "waiting", System.StringComparison.Ordinal))
                return "③ 等待约 5 秒——教学关必上钩。";
            if (string.Equals(Phase, "hooked", System.StringComparison.Ordinal))
                return "④ 上钩了！等约 5 秒圆圈走完，不要点「收杆」（收杆会跑鱼）。";
            if (string.Equals(Phase, "resolving", System.StringComparison.Ordinal))
                return "⑤ 正在收鱼，马上自动入包。";
            return "新手引导进行中：按提示操作，不可跳过。";
        }

        long ResolveSessionFishingMs()
        {
            if (FishingStartedAt <= 0 || !IsFishing)
                return 0;
            var elapsed = NowMs() - FishingStartedAt;
            return elapsed > 0 ? elapsed : 0;
        }

        static bool IsKnownSpot(string spotId)
        {
            for (var i = 0; i < TutorialSpots.Length; i++)
            {
                if (TutorialSpots[i] != null && TutorialSpots[i].id == spotId)
                    return true;
            }
            return false;
        }

        static NativeOverlaySpotDto Spot(string id, float x, float y)
        {
            return new NativeOverlaySpotDto { id = id, x = x, y = y };
        }

        static long NowMs()
        {
            return System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
