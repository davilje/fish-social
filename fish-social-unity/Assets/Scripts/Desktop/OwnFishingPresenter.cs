using System;
using System.Collections;
using System.Collections.Generic;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
using UnityEngine;

namespace FishSocial.Desktop
{
    /// <summary>
    /// BUG-54: own-player fishing presentation advances locally; server remains
    /// authoritative for validation and settle. Disable with
    /// FISH_SOCIAL_OWN_FISHING_PRESENTER=0.
    /// </summary>
    public sealed class OwnFishingPresenter
    {
        // Align with server/src/fishingStateMachine.ts PHASE_MS.
        public const int BaitingMs = 800;
        public const int CastingMs = 600;
        public const int StoppingMs = 250;

        readonly MonoBehaviour _host;
        readonly Func<SocialPondSessionController> _pond;
        readonly Action _onChanged;

        string _phase;
        string _spotId;
        bool _leavesSpot;
        bool _groundbaitActive;
        long _hookDeadlineMs;
        string _pendingCommand;
        bool _stopLatched;
        string _rollbackPhase;
        string _rollbackSpotId;
        bool _rollbackHadSpot;
        Coroutine _timeline;

        public OwnFishingPresenter(
            MonoBehaviour host,
            Func<SocialPondSessionController> pond,
            Action onChanged)
        {
            _host = host;
            _pond = pond;
            _onChanged = onChanged;
        }

        public static bool IsEnabled
        {
            get
            {
                var raw = Environment.GetEnvironmentVariable("FISH_SOCIAL_OWN_FISHING_PRESENTER");
                if (string.IsNullOrEmpty(raw))
                    return true;
                return !string.Equals(raw, "0", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(raw, "false", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase);
            }
        }

        public bool IsActive => IsEnabled && !string.IsNullOrEmpty(_phase) &&
                                !(_pond?.Invoke()?.IsFishingAsleep ?? false);

        public string Phase => _phase;

        public string SpotId => _leavesSpot ? string.Empty : _spotId;

        public long HookDeadlineMs => _hookDeadlineMs;

        public bool GroundbaitActive => _groundbaitActive;

        public void BeginTakeSpot(string spotId, long commandId)
        {
            if (!IsEnabled || string.IsNullOrEmpty(spotId))
                return;
            CaptureRollback();
            _stopLatched = false;
            _pendingCommand = "take_spot";
            _leavesSpot = false;
            _groundbaitActive = false;
            _hookDeadlineMs = 0;
            _spotId = spotId;
            SetPhase("seated", commandId);
            StopTimeline();
        }

        public void BeginStartFishing(long commandId)
        {
            if (!IsEnabled || (_pond?.Invoke()?.IsFishingAsleep ?? false))
                return;
            if (_pond?.Invoke()?.IsDailyQuotaExhausted ?? false)
                return;
            var pond = _pond?.Invoke();
            var spot = EffectiveSpot(pond);
            if (string.IsNullOrEmpty(spot))
                return;
            if (_pendingCommand == "start_fishing" &&
                (_phase == "baiting" || _phase == "casting" || _phase == "waiting"))
                return;
            CaptureRollback();
            _stopLatched = false;
            _pendingCommand = "start_fishing";
            _leavesSpot = false;
            _groundbaitActive = false;
            _hookDeadlineMs = 0;
            _spotId = spot;
            SetPhase("baiting", commandId);
            StopTimeline();
            _timeline = _host.StartCoroutine(StartFishingTimeline(commandId));
        }

        public void BeginStopFishing(long commandId)
        {
            if (!IsEnabled || (_pond?.Invoke()?.IsFishingAsleep ?? false))
                return;
            // Clear() drops _phase after a stop reaches seated. Without this latch,
            // EffectivePhase falls back to a server phase that is still fishable and
            // a second stop_fishing replays the reel.
            if (_stopLatched)
            {
                LogStopIgnored(commandId);
                return;
            }
            var phase = EffectivePhase(_pond?.Invoke());
            if (phase != "baiting" && phase != "casting" && phase != "waiting" &&
                phase != "hooked" && phase != "groundbaiting")
                return;
            CaptureRollback();
            _stopLatched = true;
            _pendingCommand = "stop_fishing";
            _leavesSpot = false;
            _groundbaitActive = false;
            var spot = EffectiveSpot(_pond?.Invoke());
            if (!string.IsNullOrEmpty(spot))
                _spotId = spot;
            var resolving = phase == "hooked";
            _hookDeadlineMs = 0;
            SetPhase(resolving ? "resolving" : "stopping", commandId);
            StopTimeline();
            _timeline = _host.StartCoroutine(StopFishingTimeline(commandId));
        }

        public void BeginGroundbait(long commandId)
        {
            if (!IsEnabled || (_pond?.Invoke()?.IsFishingAsleep ?? false))
                return;
            var pond = _pond?.Invoke();
            var spot = EffectiveSpot(pond);
            if (string.IsNullOrEmpty(spot))
                return;
            if (_phase == "groundbaiting" && _pendingCommand == "groundbait_start")
                return;
            CaptureRollback();
            _stopLatched = false;
            _pendingCommand = "groundbait_start";
            _leavesSpot = false;
            _groundbaitActive = true;
            _hookDeadlineMs = 0;
            _spotId = spot;
            SetPhase("groundbaiting", commandId);
            StopTimeline();
        }

        public void BeginLeaveSpot(long commandId)
        {
            if (!IsEnabled)
                return;
            CaptureRollback();
            _stopLatched = false;
            _pendingCommand = "leave_spot";
            _leavesSpot = true;
            _groundbaitActive = false;
            _hookDeadlineMs = 0;
            _spotId = string.Empty;
            SetPhase("idle", commandId);
            StopTimeline();
        }

        public void BeginHooked(int hookDurationMs, long commandId = 0)
        {
            if (!IsEnabled || (_pond?.Invoke()?.IsFishingAsleep ?? false))
                return;
            var pond = _pond?.Invoke();
            var spot = EffectiveSpot(pond);
            if (string.IsNullOrEmpty(spot) && string.IsNullOrEmpty(_spotId))
                return;
            StopTimeline();
            _pendingCommand = null;
            _leavesSpot = false;
            _groundbaitActive = false;
            if (!string.IsNullOrEmpty(spot))
                _spotId = spot;
            var duration = hookDurationMs > 0 ? hookDurationMs : 5000;
            _hookDeadlineMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + duration;
            SetPhase("hooked", commandId);
        }

        public void BeginMissEscape(long commandId = 0)
        {
            if (!IsEnabled || (_pond?.Invoke()?.IsFishingAsleep ?? false))
                return;
            StopTimeline();
            _pendingCommand = null;
            _stopLatched = true;
            _groundbaitActive = false;
            _hookDeadlineMs = 0;
            SetPhase("resolving", commandId);
            _timeline = _host.StartCoroutine(StopFishingTimeline(commandId));
        }

        public void OnCommandFinished(string command, bool ok)
        {
            if (!IsEnabled || string.IsNullOrEmpty(command))
                return;
            if (!string.Equals(_pendingCommand, command, StringComparison.Ordinal))
                return;
            _pendingCommand = null;
            if (ok)
            {
                // Keep presentation until server catches up (or timeline finishes).
                if (command == "groundbait_start")
                {
                    // End when server returns seated; no fixed cast duration here.
                    Notify();
                }
                return;
            }

            Rollback(command);
        }

        public void ReconcileFromServer()
        {
            if ((_pond?.Invoke()?.IsFishingAsleep ?? false) && !string.IsNullOrEmpty(_phase))
            {
                ClearRound();
                return;
            }
            if (!IsActive)
                return;
            var pond = _pond?.Invoke();
            if (pond == null || pond.CurrentUser == null)
            {
                Clear();
                return;
            }

            var serverPhase = pond.CurrentPhase ?? "idle";
            var serverSpot = pond.CurrentUser.spotId ?? string.Empty;

            if (_leavesSpot)
            {
                if (string.IsNullOrEmpty(serverSpot) || serverPhase == "idle")
                    Clear();
                return;
            }

            if (string.Equals(serverPhase, _phase, StringComparison.Ordinal))
            {
                if (_phase == "hooked")
                    _hookDeadlineMs = pond.CurrentUser.phaseEndsAt;
                // Caught up — prefer server unless a local timeline is still running.
                if (_timeline == null)
                    Clear();
                return;
            }

            var sRank = PhaseRank(serverPhase);
            var pRank = PhaseRank(_phase);
            // Local start sequence ahead of server.
            if (pRank >= 10 && pRank <= 12 && sRank >= 1 && sRank < pRank)
                return;
            // Local stop already seated while server still winding down.
            if (_phase == "seated" &&
                (serverPhase == "stopping" || serverPhase == "resolving" || sRank >= 10))
                return;
            if ((_phase == "stopping" || _phase == "resolving") && sRank >= 10 && sRank <= 20)
                return;
            if (_phase == "hooked" && serverPhase == "waiting")
                return;
            if (_phase == "groundbaiting" && serverPhase == "seated" &&
                _pendingCommand == "groundbait_start")
                return;
            if (_phase == "groundbaiting" && serverPhase == "seated")
            {
                Clear();
                return;
            }
            if (_phase == "seated" && _pendingCommand == "take_spot" &&
                !string.IsNullOrEmpty(serverSpot))
            {
                Clear();
                return;
            }

            // Server jumped elsewhere (e.g. bite while we showed waiting).
            if (sRank > pRank || serverPhase == "hooked" || serverPhase == "idle")
            {
                if (serverPhase == "hooked")
                {
                    _phase = "hooked";
                    _spotId = serverSpot;
                    _hookDeadlineMs = pond.CurrentUser.phaseEndsAt;
                    _leavesSpot = false;
                    StopTimeline();
                    Notify();
                    return;
                }
                Clear();
            }
        }

        public void ApplyToDto(NativeOverlayStateDto dto, SocialPondSessionController pond)
        {
            if (dto == null || !IsActive)
                return;
            dto.fishingPhase = _phase ?? dto.fishingPhase;
            if (_leavesSpot)
                dto.ownSpotId = string.Empty;
            else if (!string.IsNullOrEmpty(_spotId))
                dto.ownSpotId = _spotId;
            if (_hookDeadlineMs > 0)
                dto.hookDeadlineMs = _hookDeadlineMs;
            dto.availableActions = MapAvailableActions(pond);
            dto.petVisualState = PetStateController.ToWire(ResolveVisual(pond));
        }

        public PetVisualState ResolveVisual(SocialPondSessionController pond)
        {
            if (pond != null && pond.IsFishingAsleep)
                return PetVisualState.Sleep;
            var phase = IsActive ? _phase : (pond?.CurrentPhase ?? "idle");
            var outcome = pond?.CurrentUser?.phaseContext?.outcome ?? pond?.LastFishingOutcome;
            var hasCatch = pond != null && pond.HasPendingCatch;
            if (phase == "resolving" || phase == "stopping")
                return PetStateController.FromFishingPhase(phase, hasCatch, outcome ?? "escape");
            return PetStateController.FromFishingPhase(phase, hasCatch, outcome);
        }

        public void Tick()
        {
            if (!IsActive || _phase != "hooked" || _hookDeadlineMs <= 0)
                return;
            if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() < _hookDeadlineMs + 120)
                return;
            _hookDeadlineMs = 0;
            _stopLatched = true;
            StopTimeline();
            SetPhase("resolving", 0);
            _timeline = _host.StartCoroutine(StopFishingTimeline(0));
        }

        public void ClearRound()
        {
            _stopLatched = false;
            Clear();
        }

        public void Clear()
        {
            StopTimeline();
            _phase = null;
            _spotId = null;
            _leavesSpot = false;
            _groundbaitActive = false;
            _hookDeadlineMs = 0;
            _pendingCommand = null;
            _rollbackPhase = null;
            _rollbackSpotId = null;
            _rollbackHadSpot = false;
            Notify();
        }

        string[] MapAvailableActions(SocialPondSessionController pond)
        {
            var actions = new List<string>();
            if (GameplayDebugGate.IsClientEnabled())
                actions.Add("gameplay_debug");
            if (pond == null || pond.State != SocialSocketState.Connected)
                return actions.ToArray();

            var hasSpot = !_leavesSpot &&
                          (!string.IsNullOrEmpty(_spotId) ||
                           !string.IsNullOrEmpty(pond.CurrentUser?.spotId));
            var phase = _phase ?? "idle";
            if (pond.IsFishingAsleep || pond.CanStopFishing ||
                phase == "baiting" || phase == "casting" || phase == "waiting" || phase == "hooked")
                actions.Add("fishing_attention");
            if (!hasSpot)
                actions.Add("take_spot");
            if (hasSpot)
            {
                if (phase == "seated")
                {
                    actions.Add("start_fishing");
                    actions.Add("groundbait_start");
                    actions.Add("leave_spot");
                }
                else if (phase == "groundbaiting")
                {
                    actions.Add("leave_spot");
                }
                else if (phase == "baiting" || phase == "casting" ||
                         phase == "waiting" || phase == "hooked")
                {
                    actions.Add("stop_fishing");
                }
            }
            if (!pond.IsTransitioning)
                actions.Add("exit_pond");
            return actions.ToArray();
        }

        IEnumerator StartFishingTimeline(long commandId)
        {
            yield return new WaitForSecondsRealtime(BaitingMs / 1000f);
            if (_pendingCommand == "stop_fishing" || _phase == "stopping" || _phase == "resolving")
                yield break;
            if (_phase != "baiting")
                yield break;
            SetPhase("casting", commandId);
            yield return new WaitForSecondsRealtime(CastingMs / 1000f);
            if (_pendingCommand == "stop_fishing" || _phase == "stopping" || _phase == "resolving")
                yield break;
            if (_phase != "casting")
                yield break;
            SetPhase("waiting", commandId);
            _timeline = null;
        }

        IEnumerator StopFishingTimeline(long commandId)
        {
            yield return new WaitForSecondsRealtime(StoppingMs / 1000f);
            if (_phase != "stopping" && _phase != "resolving")
                yield break;
            _hookDeadlineMs = 0;
            _groundbaitActive = false;
            SetPhase("seated", commandId);
            _timeline = null;
            // Drop presentation once seated so server owns the rest.
            if (_pendingCommand == null)
                Clear();
        }

        void LogStopIgnored(long commandId)
        {
            var pond = _pond?.Invoke();
            Debug.Log(
                "[Latency][Unity] stop_fishing_ignored commandId=" + commandId +
                " localPhase=" + (_phase ?? "") +
                " serverPhase=" + (pond?.CurrentPhase ?? "") +
                " atMs=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }

        void SetPhase(string phase, long commandId)
        {
            _phase = phase ?? "idle";
            Debug.Log(
                "[Latency][Unity] local_presentation_applied commandId=" + commandId +
                " phase=" + _phase +
                " spot=" + (SpotId ?? string.Empty) +
                " atMs=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Notify();
        }

        void CaptureRollback()
        {
            var pond = _pond?.Invoke();
            _rollbackPhase = pond?.CurrentPhase ?? "idle";
            _rollbackSpotId = pond?.CurrentUser?.spotId ?? string.Empty;
            _rollbackHadSpot = !string.IsNullOrEmpty(_rollbackSpotId);
        }

        void Rollback(string command)
        {
            StopTimeline();
            _pendingCommand = null;
            _groundbaitActive = false;
            _hookDeadlineMs = 0;
            _leavesSpot = false;
            if (command == "stop_fishing")
                _stopLatched = false;
            if (command == "take_spot" && !_rollbackHadSpot)
            {
                _spotId = string.Empty;
                _phase = "idle";
            }
            else
            {
                _spotId = _rollbackSpotId ?? string.Empty;
                _phase = string.IsNullOrEmpty(_rollbackPhase) ? "seated" : _rollbackPhase;
            }
            Debug.Log(
                "[Latency][Unity] local_presentation_rollback command=" + command +
                " phase=" + _phase +
                " atMs=" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Notify();
        }

        void StopTimeline()
        {
            if (_timeline == null || _host == null)
                return;
            _host.StopCoroutine(_timeline);
            _timeline = null;
        }

        void Notify()
        {
            _onChanged?.Invoke();
        }

        string EffectiveSpot(SocialPondSessionController pond)
        {
            if (!string.IsNullOrEmpty(_spotId) && !_leavesSpot)
                return _spotId;
            return pond?.CurrentUser?.spotId ?? string.Empty;
        }

        string EffectivePhase(SocialPondSessionController pond)
        {
            if (IsActive)
                return _phase;
            return pond?.CurrentPhase ?? "idle";
        }

        static int PhaseRank(string phase)
        {
            switch (phase)
            {
                case "idle": return 0;
                case "seated": return 1;
                case "groundbaiting": return 2;
                case "baiting": return 10;
                case "casting": return 11;
                case "waiting": return 12;
                case "stopping": return 15;
                case "hooked": return 20;
                case "resolving": return 21;
                default: return -1;
            }
        }
    }
}
