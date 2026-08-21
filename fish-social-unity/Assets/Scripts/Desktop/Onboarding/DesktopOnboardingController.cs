using System.Collections;
using UnityEngine;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop.Onboarding
{
    /// <summary>
    /// STEAM-DESKTOP-11: Overlay local tutorial until onboardingCompleted.
    /// Does not join pond-novice or the live fishing state machine.
    /// </summary>
    public sealed class DesktopOnboardingController : MonoBehaviour
    {
        public const string NovicePondId = "pond-novice";

        static readonly string[] LockedMenuCommands =
        {
            "menu_pond",
            "menu_map",
            "menu_shop",
            "menu_friends",
            "menu_catch",
            "menu_gallery",
            "menu_profile",
            "menu_feed",
            "menu_leaderboard",
        };

        public static DesktopOnboardingController Instance { get; private set; }

        public bool IsOnboardingActive { get; private set; }

        readonly LocalOnboardingSession _session = new LocalOnboardingSession();
        SteamAuthController _steamAuth;
        IAuthenticatedApiClient _api;
        SocialPondSessionController _pond;
        DesktopShellUi _shellUi;
        Coroutine _resetRoutine;
        Coroutine _grantRoutine;
        bool _checkStarted;
        bool _completing;

        public void Configure(
            SteamAuthController steamAuth,
            IAuthenticatedApiClient api,
            SocialPondSessionController pond,
            DesktopShellUi shellUi)
        {
            Unhook();
            _steamAuth = steamAuth;
            _api = api;
            _pond = pond;
            _shellUi = shellUi;
            Hook();
            if (_steamAuth != null && _steamAuth.IsAuthenticated)
                BeginProgressCheck();
        }

        public void FillOverlayState(NativeOverlayStateDto dto)
        {
            if (dto == null || !IsOnboardingActive)
                return;
            var nickname = _pond != null ? _pond.Nickname : "新钓手";
            var playerId = _steamAuth != null ? _steamAuth.AuthenticatedPlayerId : string.Empty;
            _session.FillOverlayState(dto, nickname, playerId);
        }

        public bool HandleOverlayCommand(NativeOverlayCommandDto message)
        {
            if (!IsOnboardingActive || _completing || message == null)
                return false;

            var command = message.command;
            if (string.Equals(command, "open_main", System.StringComparison.Ordinal) ||
                string.Equals(command, "request_snapshot", System.StringComparison.Ordinal) ||
                string.Equals(command, "menu_settings", System.StringComparison.Ordinal))
                return false;

            if (string.Equals(command, "hide_overlay", System.StringComparison.Ordinal) ||
                string.Equals(command, "hide_to_tray", System.StringComparison.Ordinal))
                return true;

            if (IsLockedMenuCommand(command))
            {
                const string locked = "请先完成新手引导。";
                _session.SetError(locked);
                _shellUi?.SetStatusMessage(locked);
                PublishOverlay();
                return true;
            }

            if (string.Equals(command, "confirm_overlay_prompt", System.StringComparison.Ordinal))
            {
                if (_session.ConfirmOverlayPrompt())
                    FinishOnboarding();
                else
                    PublishOverlay();
                return true;
            }

            if (_grantRoutine != null)
                return true;

            if (!IsTutorialCommand(command))
                return false;

            string error;
            var ok = _session.TryCommand(command, message.spotId, out error);
            if (!ok && !string.IsNullOrEmpty(error))
                _shellUi?.SetStatusMessage(error);
            else if (!string.IsNullOrEmpty(error))
                _shellUi?.SetStatusMessage(error);
            PublishOverlay();
            return true;
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            Unhook();
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (!IsOnboardingActive || _completing)
                return;
            var now = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var changed = _session.Tick(now);
            if (_session.ConsumeNeedsCatchGrant() && _grantRoutine == null)
                _grantRoutine = StartCoroutine(GrantCatchThenPrompt());
            if (changed)
                PublishOverlay();
        }

        void Hook()
        {
            if (_steamAuth != null)
                _steamAuth.StateChanged += OnSteamStateChanged;
        }

        void Unhook()
        {
            if (_steamAuth != null)
                _steamAuth.StateChanged -= OnSteamStateChanged;
        }

        void OnSteamStateChanged(SteamLoginState state)
        {
            if (state == SteamLoginState.Authenticated)
            {
                BeginProgressCheck();
                return;
            }

            if (state == SteamLoginState.SignedOut || state == SteamLoginState.Failed)
                AbortOnboardingLocal();
        }

        void BeginProgressCheck()
        {
            if (_checkStarted)
                return;
            if (_api == null || !_api.CanUse)
                return;
            _checkStarted = true;
            StartCoroutine(FetchProgressAndMaybeStart());
        }

        IEnumerator FetchProgressAndMaybeStart()
        {
            FishingProgressDto progress = null;
            var ok = false;
            string error = null;
            yield return _api.GetFishingProgress((success, dto, message) =>
            {
                ok = success;
                progress = dto;
                error = message;
            });

            if (!ok || progress == null)
            {
                Debug.LogWarning("[Onboarding] progress fetch failed: " + error);
                _checkStarted = false;
                yield break;
            }

            if (progress.onboardingCompleted)
            {
                Debug.Log("[Onboarding] already completed; skip guide.");
                yield break;
            }

            EnterOnboarding();
        }

        void EnterOnboarding()
        {
            if (IsOnboardingActive)
                return;

            IsOnboardingActive = true;
            _completing = false;
            _session.Reset();
            if (_pond != null)
                _pond.AllowedPondIdOnly = NovicePondId;

            _shellUi?.SetOnboardingLock(true);
            Debug.Log("[Onboarding] starting local Overlay tutorial (no pond-novice join).");
            DesktopAppBootstrap.Instance?.StartNativeOverlay();
            PublishOverlay();
        }

        public void ResetAndRestartOnboarding()
        {
            if (_resetRoutine != null)
            {
                _shellUi?.SetStatusMessage("正在重置新手引导，请稍候。");
                return;
            }
            if (_api == null || !_api.CanUse)
            {
                _shellUi?.SetStatusMessage("请先登录后再重置新手引导。");
                return;
            }
            _resetRoutine = StartCoroutine(ResetAndRestartRoutine());
        }

        IEnumerator ResetAndRestartRoutine()
        {
            _shellUi?.SetStatusMessage("正在重置新手引导…");
            var ok = false;
            string error = null;
            yield return _api.ResetOnboarding((success, _, message) =>
            {
                ok = success;
                error = message;
            });

            if (!ok)
            {
                _resetRoutine = null;
                _shellUi?.SetStatusMessage(error ?? "重置新手引导失败。请确认服务端已重启。");
                yield break;
            }

            AbortOnboardingLocal();
            EnterOnboarding();
            _shellUi?.SetStatusMessage("新手引导已重置，正在进入教学关。");
            Debug.Log("[Onboarding] reset and restarted local tutorial.");
            _resetRoutine = null;
        }

        IEnumerator GrantCatchThenPrompt()
        {
            _shellUi?.SetStatusMessage("正在收入教学鱼获…");
            var ok = false;
            string error = null;
            yield return _api.CompleteOnboarding((success, _, message) =>
            {
                ok = success;
                error = message;
            });
            _grantRoutine = null;
            if (!ok)
            {
                _shellUi?.SetStatusMessage(error ?? "鱼获入包失败，将自动重试。");
                PublishOverlay();
                yield return new WaitForSeconds(2f);
                _session.RestoreNeedsCatchGrant();
                yield break;
            }

            _session.ShowCatchPrompt(System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            PublishOverlay();
        }

        void FinishOnboarding()
        {
            if (!IsOnboardingActive || _completing)
                return;
            _completing = true;
            IsOnboardingActive = false;
            if (_pond != null)
                _pond.AllowedPondIdOnly = null;
            _session.Reset();
            _shellUi?.SetOnboardingLock(false);
            var bootstrap = DesktopAppBootstrap.Instance;
            bootstrap?.CloseNativeOverlay();
            bootstrap?.RaiseMainWindow(ShellPanelId.WorldMap);
            _shellUi?.SetStatusMessage("新手引导已完成，请选择开放鱼塘。");
            Debug.Log("[Onboarding] local tutorial completed; Overlay closed.");
        }

        void AbortOnboardingLocal()
        {
            _checkStarted = false;
            _completing = false;
            IsOnboardingActive = false;
            if (_pond != null)
                _pond.AllowedPondIdOnly = null;
            if (_grantRoutine != null)
            {
                StopCoroutine(_grantRoutine);
                _grantRoutine = null;
            }
            _session.Reset();
            _shellUi?.SetOnboardingLock(false);
        }

        void PublishOverlay()
        {
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        static bool IsTutorialCommand(string command)
        {
            return command == "take_spot" ||
                   command == "leave_spot" ||
                   command == "start_fishing" ||
                   command == "stop_fishing" ||
                   command == "exit_pond" ||
                   command == "send_pond_chat" ||
                   command == "hide_overlay";
        }

        static bool IsLockedMenuCommand(string command)
        {
            for (var i = 0; i < LockedMenuCommands.Length; i++)
            {
                if (string.Equals(command, LockedMenuCommands[i], System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }
    }
}
