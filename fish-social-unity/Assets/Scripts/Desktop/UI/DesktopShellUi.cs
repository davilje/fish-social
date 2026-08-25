using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;
using FishSocial.Desktop.Pet;
using FishSocial.Desktop.Social;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Runtime-built UGUI shell: 480×320 login, 1280×720 main with bottom nav and feature tabs.
    /// </summary>
    public sealed class DesktopShellUi : MonoBehaviour, IDesktopProductMenuHandler
    {
        PanelRouter _router;
        CanvasScaler _scaler;
        GameObject _loginRoot;
        GameObject _mainRoot;
        Text _loginStatus;
        Text _statusLogin;
        Text _statusConnection;
        Text _statusPond;
        Text _statusPet;
        Text _toast;
        float _toastUntil;
        SteamAuthController _steamAuth;
        IAuthenticatedApiClient _authenticatedApi;
        SocialPondSessionController _pondSession;
        SocialLobbyController _socialLobby;
        PetStateController _petState;
        Text _pondStatus;
        Text _petStateLabel;
        Text _petPond;
        bool _mainShellEntered;
        DesktopProductMenuView _productMenu;
        DesktopSocialModalView _socialPanel;
        DesktopCatchBagModalView _catchPanel;
        DesktopGalleryModalView _galleryPanel;
        DesktopSettingsModalView _settingsPanel;
        DesktopWorldMapPanel _worldMapPanel;
        DesktopShopPanel _shopPanel;
        DesktopProfilePanel _profilePanel;
        DesktopProfileHubPanel _profileHubPanel;
        DesktopProfileEditPanel _profileEditPanel;
        DesktopSocialFeedPanel _socialFeedPanel;
        DesktopLeaderboardPanel _leaderboardPanel;
        readonly List<Button> _lockableNavButtons = new List<Button>();
        bool _onboardingLock;
        bool _suppressInventoryToast;
        Transform _settlementCanvas;

        public IAuthenticatedApiClient AuthenticatedApi => _authenticatedApi;

        public void SetStatusMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            if (_statusPond != null)
                _statusPond.text = message;
            else if (_statusConnection != null)
                _statusConnection.text = message;
            ShowToast(message);
        }

        public void OpenOtherPlayerProfile(string playerId)
        {
            _profileHubPanel?.ShowOtherPlayer(playerId);
            ShowMainPanel(ShellPanelId.Profile);
        }

        public void OpenProfileHub(DesktopProfileHubPanel.HubTab tab = DesktopProfileHubPanel.HubTab.Profile)
        {
            _profileHubPanel?.ShowSelf(tab);
            ShowMainPanel(ShellPanelId.Profile);
        }

        public void OpenDirectMessage(string playerId, string nickname)
        {
            ShowMainPanel(ShellPanelId.Friends);
            _socialPanel?.OpenDirectMessage(playerId, nickname);
        }

        public void Build(PanelRouter router)
        {
            _router = router;
            _steamAuth = DesktopAppBootstrap.Instance != null
                ? DesktopAppBootstrap.Instance.SteamAuth
                : null;
            _petState = DesktopAppBootstrap.Instance != null
                ? DesktopAppBootstrap.Instance.PetState
                : null;
            if (_steamAuth != null)
            {
                _authenticatedApi = _steamAuth.CreateAuthenticatedApiClient();
                _steamAuth.StateChanged += OnSteamStateChanged;
                _steamAuth.ErrorMessage += OnSteamError;
            }
            _pondSession = DesktopAppBootstrap.Instance != null
                ? DesktopAppBootstrap.Instance.PondSession
                : null;
            _socialLobby = DesktopAppBootstrap.Instance != null
                ? DesktopAppBootstrap.Instance.SocialLobby
                : null;
            if (_pondSession != null)
            {
                _pondSession.StateChanged += OnPondStateChanged;
                _pondSession.SnapshotChanged += OnPondSnapshot;
                _pondSession.UserUpdated += OnPondUserUpdated;
                _pondSession.UsersChanged += OnPondUsersChanged;
                _pondSession.FishBiteReceived += OnPondFishBite;
                _pondSession.FishCatchSettled += OnPondFishCatchSettled;
                _pondSession.PondSessionSummaryReceived += OnPondSessionSummary;
                _pondSession.InventoryUpdated += OnInventoryUpdated;
                _pondSession.FriendRequestReceived += OnFriendRequestNotify;
                _pondSession.ErrorReceived += OnPondError;
            }
            if (_socialLobby != null)
                _socialLobby.Error += OnSocialLobbyError;
            if (_petState != null)
                _petState.StateChanged += OnPetVisualStateChanged;

            var canvasGo = new GameObject("ShellCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _scaler = canvasGo.GetComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(WindowManager.LoginWidth, WindowManager.LoginHeight);
            _scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            _loginRoot = CreateStretchPanel("LoginRoot", canvasGo.transform, new Color(0.09f, 0.12f, 0.16f, 1f));
            BuildLogin(_loginRoot);

            _mainRoot = CreateStretchPanel("MainRoot", canvasGo.transform, new Color(0.09f, 0.12f, 0.16f, 1f));
            BuildMain(_mainRoot);
            _mainRoot.SetActive(false);

            _toast = CreateText(canvasGo.transform, "Toast", string.Empty, 18, TextAnchor.LowerCenter,
                new Vector2(0, 24), new Vector2(400, 48));
            var toastRt = _toast.rectTransform;
            toastRt.anchorMin = new Vector2(0.08f, 0f);
            toastRt.anchorMax = new Vector2(0.92f, 0f);
            toastRt.pivot = new Vector2(0.5f, 0f);
            toastRt.anchoredPosition = new Vector2(0f, 78f);
            toastRt.sizeDelta = new Vector2(0f, 44f);
            _toast.alignment = TextAnchor.MiddleCenter;
            _toast.gameObject.SetActive(false);

            _productMenu = canvasGo.AddComponent<DesktopProductMenuView>();
            _productMenu.Bind(canvasGo.transform, _mainRoot.GetComponent<RectTransform>(), this);
            _settlementCanvas = canvasGo.transform;
            _router.PanelChanged += OnPanelChanged;

            if (_steamAuth != null && _steamAuth.IsAuthenticated)
                EnterMainShell();
            else
                WindowManager.Instance?.ApplyLoginShell();

            if (_steamAuth != null)
                OnSteamStateChanged(_steamAuth.State);
            if (_pondSession != null)
                OnPondStateChanged(_pondSession.State, null);
            RefreshPetPresentation();
            _router.Show(ShellPanelId.Home);
        }

        void BuildLogin(GameObject go)
        {
            CreateText(go.transform, "Brand", "Fish Social", 26, TextAnchor.MiddleCenter,
                new Vector2(0, 70), new Vector2(400, 36));
            _loginStatus = CreateText(go.transform, "LoginHint", "请使用 Steam 登录", 16, TextAnchor.MiddleCenter,
                new Vector2(0, 24), new Vector2(400, 48));
            _loginStatus.color = new Color(0.78f, 0.84f, 0.9f, 1f);
            CreateCenteredButton(go.transform, "SteamLogin", "Steam 登录",
                new Vector2(0, -36), new Vector2(200, 44), BeginSteamLogin);
        }

        void BuildMain(GameObject go)
        {
            var header = CreateBar("Header", go.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -80),
                new Color(0.12f, 0.18f, 0.24f, 1f));
            BindSquarePet(header.transform, "HeaderPet", new Vector2(16, -16), 48, false);
            CreateText(header.transform, "Title", "Fish Social", 22, TextAnchor.MiddleLeft,
                new Vector2(76, -22), new Vector2(240, 28));
            _statusPet = CreateText(header.transform, "PetStatus", "宠物：离线", 14, TextAnchor.MiddleLeft,
                new Vector2(76, -50), new Vector2(280, 22));
            _statusLogin = CreateText(header.transform, "LoginStatus", "登录：未登录", 14, TextAnchor.MiddleRight,
                new Vector2(-16, -14), new Vector2(320, 20));
            _statusConnection = CreateText(header.transform, "ConnStatus", "连接：离线", 14, TextAnchor.MiddleRight,
                new Vector2(-16, -34), new Vector2(320, 20));
            _statusPond = CreateText(header.transform, "PondStatus", "鱼塘：未进入", 14, TextAnchor.MiddleRight,
                new Vector2(-16, -54), new Vector2(320, 20));

            var nav = CreateBar("Nav", go.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 64),
                new Color(0.11f, 0.15f, 0.2f, 1f));
            var navLayout = nav.AddComponent<HorizontalLayoutGroup>();
            navLayout.padding = new RectOffset(12, 12, 8, 8);
            navLayout.spacing = 8;
            navLayout.childAlignment = TextAnchor.MiddleCenter;
            navLayout.childControlHeight = true;
            navLayout.childControlWidth = true;
            navLayout.childForceExpandHeight = true;
            navLayout.childForceExpandWidth = true;

            CreateNavButton(nav.transform, "主页", ReturnToPetHome, true);
            CreateNavButton(nav.transform, "鱼塘", ShowPondPanel, true);
            CreateNavButton(nav.transform, "世界地图",
                () => ShowMainPanel(ShellPanelId.WorldMap), true);
            CreateNavButton(nav.transform, "商店", () => ShowMainPanel(ShellPanelId.Shop), true);
            CreateNavButton(nav.transform, "好友/聊天", () => ShowMainPanel(ShellPanelId.Friends), true);
            CreateNavButton(nav.transform, "鱼获/背包", () => ShowMainPanel(ShellPanelId.CatchBag), true);
            CreateNavButton(nav.transform, "个人中心", () => OpenProfileHub(), true);
            CreateNavButton(nav.transform, "动态", () => ShowMainPanel(ShellPanelId.SocialFeed), true);
            CreateNavButton(nav.transform, "排行榜", () => ShowMainPanel(ShellPanelId.Leaderboard), true);
            CreateNavButton(nav.transform, "设置", () => ShowMainPanel(ShellPanelId.Settings), false);

            var content = CreateBar("Content", go.transform, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero,
                new Color(0.09f, 0.12f, 0.16f, 1f));
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.offsetMin = new Vector2(0, 64);
            contentRt.offsetMax = new Vector2(0, -80);

            RegisterPanel(content.transform, ShellPanelId.Home, BuildHome);
            RegisterPanel(content.transform, ShellPanelId.Pond, BuildPond);
            RegisterPanel(content.transform, ShellPanelId.WorldMap, BuildWorldMap);
            RegisterPanel(content.transform, ShellPanelId.Shop, BuildShop);
            RegisterPanel(content.transform, ShellPanelId.Friends, BuildSocialPanel);
            RegisterPanel(content.transform, ShellPanelId.CatchBag, BuildCatchPanel);
            RegisterPanel(content.transform, ShellPanelId.Gallery, BuildGalleryPanel);
            RegisterPanel(content.transform, ShellPanelId.Profile, BuildProfilePanel);
            RegisterPanel(content.transform, ShellPanelId.ProfileEdit, BuildProfileEditPanel);
            RegisterPanel(content.transform, ShellPanelId.SocialFeed, BuildSocialFeedPanel);
            RegisterPanel(content.transform, ShellPanelId.Leaderboard, BuildLeaderboardPanel);
            RegisterPanel(content.transform, ShellPanelId.Settings, BuildSettingsPanel);
        }

        void EnterMainShell()
        {
            if (_mainShellEntered)
                return;
            _mainShellEntered = true;
            if (_loginRoot != null)
                _loginRoot.SetActive(false);
            if (_mainRoot != null)
                _mainRoot.SetActive(true);
            if (_scaler != null)
                _scaler.referenceResolution = new Vector2(
                    WindowManager.MainDefaultWidth, WindowManager.MainDefaultHeight);
            WindowManager.Instance?.ApplyMainShell();
            RefreshPetPresentation();
        }

        void ReturnToPetHome()
        {
            ShowMainPanel(ShellPanelId.Home);
        }

        void ShowPondPanel()
        {
            ShowMainPanel(ShellPanelId.Pond);
        }

        public void SetOnboardingLock(bool locked)
        {
            _onboardingLock = locked;
            for (var i = 0; i < _lockableNavButtons.Count; i++)
            {
                if (_lockableNavButtons[i] != null)
                    _lockableNavButtons[i].interactable = !locked;
            }
        }

        void ShowMainPanel(ShellPanelId id)
        {
            if (_onboardingLock && id != ShellPanelId.Settings)
            {
                SetStatusMessage("请先完成新手引导。");
                return;
            }

            // FEAT-ALBUM-01：独立图鉴入口并入个人中心
            if (id == ShellPanelId.Gallery)
            {
                _profileHubPanel?.ShowSelf(DesktopProfileHubPanel.HubTab.Codex);
                id = ShellPanelId.Profile;
            }

            DesktopAppBootstrap.Instance?.RaiseMainWindow(id);
        }

        public void HandleProductMenu(DesktopProductMenuAction action)
        {
            _productMenu?.Hide();
            if (_onboardingLock &&
                action != DesktopProductMenuAction.Settings &&
                action != DesktopProductMenuAction.HideToTray &&
                action != DesktopProductMenuAction.Quit)
            {
                SetStatusMessage("请先完成新手引导。");
                return;
            }

            switch (action)
            {
                case DesktopProductMenuAction.CurrentPond:
                    ShowMainPanel(ShellPanelId.Pond);
                    break;
                case DesktopProductMenuAction.WorldMap:
                    ShowMainPanel(ShellPanelId.WorldMap);
                    break;
                case DesktopProductMenuAction.Shop:
                    ShowMainPanel(ShellPanelId.Shop);
                    break;
                case DesktopProductMenuAction.Friends:
                    ShowMainPanel(ShellPanelId.Friends);
                    break;
                case DesktopProductMenuAction.CatchBag:
                    ShowMainPanel(ShellPanelId.CatchBag);
                    break;
                case DesktopProductMenuAction.Gallery:
                    OpenProfileHub(DesktopProfileHubPanel.HubTab.Codex);
                    break;
                case DesktopProductMenuAction.Profile:
                    OpenProfileHub();
                    break;
                case DesktopProductMenuAction.Settings:
                    ShowMainPanel(ShellPanelId.Settings);
                    break;
                case DesktopProductMenuAction.SocialFeed:
                    ShowMainPanel(ShellPanelId.SocialFeed);
                    break;
                case DesktopProductMenuAction.Leaderboard:
                    ShowMainPanel(ShellPanelId.Leaderboard);
                    break;
                case DesktopProductMenuAction.HideToTray:
                    WindowManager.Instance?.HideToTray();
                    break;
                case DesktopProductMenuAction.Quit:
                    DesktopAppBootstrap.Instance?.QuitForReal();
                    break;
            }
        }

        void OnPanelChanged(ShellPanelId id)
        {
            _productMenu?.Hide();
            _socialPanel?.OnClosed();
            _catchPanel?.OnClosed();
            _galleryPanel?.OnClosed();
            _shopPanel?.OnClosed();
            _profilePanel?.OnClosed();
            _profileHubPanel?.OnClosed();
            _profileEditPanel?.OnClosed();
            _socialFeedPanel?.OnClosed();
            _leaderboardPanel?.OnClosed();
            switch (id)
            {
                case ShellPanelId.Shop:
                    _shopPanel?.OnOpened();
                    break;
                case ShellPanelId.Friends:
                    _socialPanel?.OnOpened();
                    break;
                case ShellPanelId.CatchBag:
                    _catchPanel?.OnOpened();
                    break;
                case ShellPanelId.Gallery:
                    // FEAT-ALBUM-01：图鉴并入个人中心
                    if (_profileHubPanel != null)
                    {
                        _profileHubPanel.ShowSelf(DesktopProfileHubPanel.HubTab.Codex);
                        _profileHubPanel.OnOpened();
                    }
                    else
                        _galleryPanel?.OnOpened();
                    break;
                case ShellPanelId.Profile:
                    _profileHubPanel?.OnOpened();
                    break;
                case ShellPanelId.ProfileEdit:
                    _profileEditPanel?.OnOpened();
                    break;
                case ShellPanelId.SocialFeed:
                    _socialFeedPanel?.OnOpened();
                    break;
                case ShellPanelId.Leaderboard:
                    _leaderboardPanel?.OnOpened();
                    break;
                case ShellPanelId.WorldMap:
                    _worldMapPanel?.OnOpened();
                    break;
                case ShellPanelId.Settings:
                    _settingsPanel?.OnOpened();
                    break;
            }
        }

        void OnDestroy()
        {
            if (_router != null)
                _router.PanelChanged -= OnPanelChanged;
            _socialPanel?.OnClosed();
            _catchPanel?.OnClosed();
            _galleryPanel?.OnClosed();
            _shopPanel?.OnClosed();
            _profilePanel?.OnClosed();
            _profileHubPanel?.OnClosed();
            _profileEditPanel?.OnClosed();
            _socialFeedPanel?.OnClosed();
            _leaderboardPanel?.OnClosed();
            if (_petState != null)
                _petState.StateChanged -= OnPetVisualStateChanged;
            if (_steamAuth == null)
                return;
            _steamAuth.StateChanged -= OnSteamStateChanged;
            _steamAuth.ErrorMessage -= OnSteamError;
            if (_pondSession == null)
                return;
            _pondSession.StateChanged -= OnPondStateChanged;
            _pondSession.SnapshotChanged -= OnPondSnapshot;
            _pondSession.UserUpdated -= OnPondUserUpdated;
            _pondSession.UsersChanged -= OnPondUsersChanged;
            _pondSession.FishBiteReceived -= OnPondFishBite;
            _pondSession.FishCatchSettled -= OnPondFishCatchSettled;
            _pondSession.PondSessionSummaryReceived -= OnPondSessionSummary;
            _pondSession.InventoryUpdated -= OnInventoryUpdated;
            _pondSession.FriendRequestReceived -= OnFriendRequestNotify;
            _pondSession.ErrorReceived -= OnPondError;
            if (_socialLobby != null)
                _socialLobby.Error -= OnSocialLobbyError;
        }

        void Update()
        {
            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
                _toast.gameObject.SetActive(false);
        }

        void ShowToast(string msg)
        {
            _toast.text = msg;
            _toast.gameObject.SetActive(true);
            _toastUntil = Time.unscaledTime + 4f;
        }

        void RegisterPanel(Transform parent, ShellPanelId id, System.Action<GameObject> builder)
        {
            var go = CreateStretchPanel(id.ToString(), parent, new Color(0, 0, 0, 0));
            builder(go);
            _router.Register(id, go);
        }

        void BuildHome(GameObject go)
        {
            CreateText(go.transform, "H1", "桌面宠物", 28, TextAnchor.UpperLeft,
                new Vector2(32, -24), new Vector2(400, 40));
            BindSquarePet(go.transform, "HomePet", new Vector2(0, 36), 256, true);
            _petStateLabel = CreateText(go.transform, "SessionState", "宠物状态：离线", 20,
                TextAnchor.MiddleCenter, new Vector2(0, -140), new Vector2(480, 32));
            _petPond = CreateText(go.transform, "CurrentPond", "当前鱼塘：未进入", 16,
                TextAnchor.MiddleCenter, new Vector2(0, -172), new Vector2(480, 28));
            CreateBottomCenteredButton(go.transform, "OpenPond", "进入 / 恢复鱼塘",
                20f, new Vector2(220, 44), OpenPond);
            CreateBottomCenteredButton(go.transform, "SessionCheck", "验证当前会话",
                72f, new Vector2(180, 36), ValidateCurrentSession);
        }

        void OpenPond()
        {
            if (_pondSession == null)
            {
                ShowToast("鱼塘会话尚未初始化。");
                return;
            }

            _router.Show(ShellPanelId.Pond);
            DesktopAppBootstrap.Instance?.StartNativeOverlay();
            WindowManager.Instance?.HideToTray();

            if (_pondSession.State == SocialSocketState.Connected ||
                _pondSession.State == SocialSocketState.Connecting ||
                _pondSession.State == SocialSocketState.Reconnecting)
            {
                ShowToast(_pondSession.State == SocialSocketState.Connected
                    ? "已恢复当前鱼塘，主窗口已隐藏。"
                    : "鱼塘会话正在连接，主窗口已隐藏。");
                RefreshPetPresentation();
                DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
                return;
            }

            _pondSession.ConnectAndJoin();
            ShowToast("正在进入鱼塘。");
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void RefreshPetPresentation()
        {
            if (_petState != null)
                _petState.RefreshFromApp();
            ApplyPetLabels(_petState != null ? _petState.Current : PetVisualState.Offline);
        }

        void OnPetVisualStateChanged(PetVisualState state)
        {
            ApplyPetLabels(state);
        }

        void ApplyPetLabels(PetVisualState state)
        {
            var label = "宠物：" + PetStateController.ToChinese(state);
            if (_statusPet != null)
                _statusPet.text = label;
            if (_petStateLabel != null)
                _petStateLabel.text = "宠物状态：" + PetStateController.ToChinese(state);
            if (_petPond != null || _statusPond != null)
            {
                var inPond = _pondSession != null &&
                             _pondSession.State == SocialSocketState.Connected;
                var pondName = inPond
                    ? (_pondSession.LatestSnapshot?.pond?.name ?? _pondSession.CurrentPondId)
                    : null;
                var pondLabel = "鱼塘：" + (string.IsNullOrEmpty(pondName) ? "未进入" : pondName);
                if (_statusPond != null)
                    _statusPond.text = pondLabel;
                if (_petPond != null)
                    _petPond.text = "当前" + pondLabel;
            }
        }

        void BeginSteamLogin()
        {
            Debug.Log("[SteamAuth] BeginLogin clicked from desktop UI.");
            if (_steamAuth == null)
            {
                Debug.LogWarning("[SteamAuth] Login click ignored: SteamAuthController is null.");
                ShowToast("Steam 登录组件尚未初始化。");
                return;
            }
            _steamAuth.BeginLogin();
        }

        void ValidateCurrentSession()
        {
            if (_authenticatedApi == null || !_authenticatedApi.CanUse)
            {
                ShowToast("请先完成 Steam 登录。");
                return;
            }
            StartCoroutine(_authenticatedApi.GetInventory((ok, message) =>
            {
                if (ok && _statusLogin != null)
                    _statusLogin.text = "登录：会话验证成功";
                ShowToast(message);
            }));
        }

        void OnSteamStateChanged(SteamLoginState state)
        {
            string label;
            switch (state)
            {
                case SteamLoginState.Initializing:
                    label = "登录：初始化 Steam";
                    break;
                case SteamLoginState.RequestingTicket:
                    label = "登录：获取 Steam Ticket";
                    break;
                case SteamLoginState.Authenticating:
                    label = "登录：服务端验证中";
                    break;
                case SteamLoginState.Authenticated:
                    label = "登录：Steam 已连接";
                    EnterMainShell();
                    ShowToast("Steam 登录成功");
                    break;
                case SteamLoginState.Failed:
                    label = "登录：失败";
                    break;
                default:
                    label = "登录：未登录";
                    break;
            }

            if (_loginStatus != null)
                _loginStatus.text = state == SteamLoginState.SignedOut ? "请使用 Steam 登录" : label;
            if (_statusLogin != null)
                _statusLogin.text = label;
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void OnSteamError(string message)
        {
            if (_loginStatus != null)
                _loginStatus.text = "登录：失败";
            if (_statusLogin != null)
                _statusLogin.text = "登录：失败";
            ShowToast(message);
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void OnPondStateChanged(SocialSocketState state, string message)
        {
            string label;
            switch (state)
            {
                case SocialSocketState.Connecting: label = "连接中"; break;
                case SocialSocketState.Connected: label = "在线"; break;
                case SocialSocketState.Reconnecting: label = "重连中"; break;
                case SocialSocketState.Failed: label = "连接失败"; break;
                default: label = "断开"; break;
            }
            if (_statusConnection != null)
                _statusConnection.text = "连接：" + label;
            if (_pondStatus != null && !string.IsNullOrEmpty(message))
                _pondStatus.text = "连接：" + label + "\n" + message;
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void OnPondSnapshot(PondSnapshotDto snapshot)
        {
            if (_pondStatus != null)
            {
                var count = snapshot?.users?.Length ?? 0;
                _pondStatus.text = "连接：在线 · 鱼塘用户：" + count +
                                   "\n当前 phase：" + (_pondSession?.CurrentPhase ?? "idle") +
                                   FormatOtherPlayers();
            }
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void OnPondUserUpdated(PondUserDto user)
        {
            if (_pondStatus != null)
                _pondStatus.text = "连接：在线 · 当前 phase：" + (user?.fishingPhase ?? "idle") +
                                   "\n当前钓位：" + (user?.spotId ?? "未选择") +
                                   FormatOtherPlayers();
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void OnPondUsersChanged()
        {
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        string FormatOtherPlayers()
        {
            var others = _pondSession?.VisibleOthers;
            if (others == null || others.Length == 0)
                return "\n同塘玩家：无";
            var text = "\n同塘玩家：";
            var shown = Mathf.Min(others.Length, 6);
            for (var i = 0; i < shown; i++)
            {
                var user = others[i];
                text += "\n· " + (string.IsNullOrEmpty(user.nickname) ? user.playerId : user.nickname) +
                        " · " + PetStateController.ToChinese(
                            PetStateController.FromFishingPhase(user.fishingPhase));
            }

            if (others.Length > shown)
                text += "\n· …共 " + others.Length + " 人";
            return text;
        }

        void OnPondFishBite(PendingFishCatchDto fishCatch)
        {
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
            var species = fishCatch != null ? DesktopFishCatalog.SpeciesName(fishCatch.speciesId) : "鱼";
            DesktopNotificationService.Instance?.Publish(new DesktopNotification(
                NotificationKind.FishBite, "鱼咬钩", "有" + species + "上钩，请打开主界面领取。"));
            ShowToast("收到服务端咬钩事件，请打开主界面领取鱼获。");
        }

        void OnPondFishCatchSettled(FishCatchSettledDto settled)
        {
            _suppressInventoryToast = true;
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
            if (settled != null && !string.IsNullOrEmpty(settled.message))
                ShowToast(settled.message);
            else if (settled != null && settled.autoReturned)
                ShowToast("已自动回塘 +" + settled.gold + " 金币");
        }

        void OnPondSessionSummary(PondSessionSummaryDto summary)
        {
            if (summary == null || _settlementCanvas == null)
                return;
            DesktopPondSettlementModalView.Show(_settlementCanvas, summary);
        }

        public void ShowPondSessionSummary(PondSessionSummaryDto summary)
        {
            OnPondSessionSummary(summary);
        }

        void OnInventoryUpdated(FishInventoryItemDto[] items)
        {
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
            if (_suppressInventoryToast)
            {
                _suppressInventoryToast = false;
                return;
            }
            ShowToast("背包已更新：" + (items != null ? items.Length : 0) + " 条鱼获");
        }

        void OnFriendRequestNotify(FriendRequestDto request)
        {
            DesktopNotificationService.Instance?.Publish(new DesktopNotification(
                NotificationKind.FriendInvite, "好友请求",
                (request != null ? request.fromNickname : "钓友") + " 想添加你为好友。"));
        }

        void OnPondError(string message)
        {
            if (!string.IsNullOrEmpty(message) && message.IndexOf("金币不足") >= 0)
                SetStatusMessage(message);
            else
                ShowToast(message);
        }

        void OnSocialLobbyError(string message)
        {
            ShowToast(message);
        }

        void ShowSocketResult(bool ok, string message)
        {
            ShowToast(message);
            RefreshPetPresentation();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void BuildPond(GameObject go)
        {
            CreateText(go.transform, "P1", "鱼塘（会话工具）", 28, TextAnchor.UpperLeft,
                new Vector2(32, -24), new Vector2(600, 40));
            _pondStatus = CreateText(go.transform, "P2",
                "鱼塘场景在 960×560 Overlay 中渲染。本页只操作会话，打开主界面不会离塘。\n\n连接：未连接 · 当前 phase：—",
                18, TextAnchor.UpperLeft, new Vector2(32, -80), new Vector2(900, 140));
            _pondStatus.verticalOverflow = VerticalWrapMode.Truncate;
            CreateButton(go.transform, "ConnectPond", "连接并进塘", new Vector2(32, -240), new Vector2(180, 46),
                () => _pondSession?.ConnectAndJoin());
            CreateButton(go.transform, "TakeSpot", "选择 1 号钓位", new Vector2(228, -240), new Vector2(190, 46),
                () => _pondSession?.TakeFirstSpot(ShowSocketResult));
            CreateButton(go.transform, "StartFishing", "开始钓鱼", new Vector2(434, -240), new Vector2(150, 46),
                () => _pondSession?.StartFishingAtFirstSpot(ShowSocketResult));
            CreateButton(go.transform, "StopFishing", "收杆", new Vector2(600, -240), new Vector2(150, 46),
                () => _pondSession?.StopFishing(ShowSocketResult));
            CreateButton(go.transform, "AcceptCatch", "领取鱼获", new Vector2(766, -240), new Vector2(150, 46),
                () => _pondSession?.AcceptLatestCatch(ShowSocketResult));
            CreateButton(go.transform, "ReturnHome", "返回主视图", new Vector2(32, -304), new Vector2(180, 46),
                ReturnToPetHome);
            CreateButton(go.transform, "EnterOverlay", "进入 / 恢复鱼塘", new Vector2(228, -304), new Vector2(220, 46),
                OpenPond);
        }

        void BuildWorldMap(GameObject go)
        {
            _worldMapPanel = DesktopFeaturePanelFactory.Mount<DesktopWorldMapPanel>(
                go.transform,
                view => view.Bind(_pondSession, _authenticatedApi));
        }

        void BuildShop(GameObject go)
        {
            _shopPanel = DesktopFeaturePanelFactory.Mount<DesktopShopPanel>(
                go.transform,
                view => view.Bind(_authenticatedApi));
        }

        void BuildProfilePanel(GameObject go)
        {
            _profileHubPanel = DesktopFeaturePanelFactory.Mount<DesktopProfileHubPanel>(
                go.transform,
                view => view.Bind(
                    _authenticatedApi,
                    _pondSession,
                    () => ShowMainPanel(ShellPanelId.ProfileEdit),
                    SetStatusMessage));
            // Keep legacy profile panel available for edit back-compat paths if needed.
            _profilePanel = null;
        }

        void BuildProfileEditPanel(GameObject go)
        {
            _profileEditPanel = DesktopFeaturePanelFactory.Mount<DesktopProfileEditPanel>(
                go.transform,
                view => view.Bind(
                    _authenticatedApi,
                    _pondSession,
                    () => OpenProfileHub()));
        }

        void BuildSocialFeedPanel(GameObject go)
        {
            _socialFeedPanel = DesktopFeaturePanelFactory.Mount<DesktopSocialFeedPanel>(
                go.transform,
                view => view.Bind(_authenticatedApi, _pondSession));
        }

        void BuildLeaderboardPanel(GameObject go)
        {
            _leaderboardPanel = DesktopFeaturePanelFactory.Mount<DesktopLeaderboardPanel>(
                go.transform,
                view => view.Bind(_authenticatedApi, _pondSession));
        }

        void BuildSocialPanel(GameObject go)
        {
            _socialPanel = DesktopFeaturePanelFactory.Mount<DesktopSocialModalView>(go.transform,
                view => view.Bind(_authenticatedApi, _pondSession, _socialLobby));
        }

        void BuildCatchPanel(GameObject go)
        {
            _catchPanel = DesktopFeaturePanelFactory.Mount<DesktopCatchBagModalView>(go.transform,
                view => view.Bind(_authenticatedApi, _pondSession));
        }

        void BuildGalleryPanel(GameObject go)
        {
            _galleryPanel = DesktopFeaturePanelFactory.Mount<DesktopGalleryModalView>(go.transform,
                view => view.Bind(_authenticatedApi, _pondSession));
        }

        void BuildSettingsPanel(GameObject go)
        {
            _settingsPanel = DesktopFeaturePanelFactory.Mount<DesktopSettingsModalView>(go.transform,
                view => view.Bind());
        }

        static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null)
                return;
            var es = new GameObject("EventSystem",
                typeof(EventSystem),
                typeof(StandaloneInputModule));
            DontDestroyOnLoad(es);
        }

        void BindSquarePet(Transform parent, string name, Vector2 anchoredPos, float size, bool enableDrag)
        {
            var slot = new GameObject(name + "Slot", typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            var slotRt = slot.GetComponent<RectTransform>();
            if (Mathf.Approximately(size, 256f))
            {
                slotRt.anchorMin = new Vector2(0.5f, 0.5f);
                slotRt.anchorMax = new Vector2(0.5f, 0.5f);
                slotRt.pivot = new Vector2(0.5f, 0.5f);
                slotRt.anchoredPosition = anchoredPos;
            }
            else
            {
                slotRt.anchorMin = new Vector2(0f, 1f);
                slotRt.anchorMax = new Vector2(0f, 1f);
                slotRt.pivot = new Vector2(0f, 1f);
                slotRt.anchoredPosition = anchoredPos;
            }
            slotRt.sizeDelta = new Vector2(size, size);

            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            go.transform.SetParent(slot.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            var fitter = go.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
            fitter.aspectRatio = 1f;
            var image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = enableDrag;
            image.color = Color.white;

            var renderer = go.AddComponent<SpriteFramePetRenderer>();
            renderer.Bind(image);
            if (_petState != null)
                _petState.AddRenderer(renderer);

            if (!enableDrag || _petState == null)
                return;

            var trigger = go.AddComponent<EventTrigger>();
            var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            down.callback.AddListener(evt =>
            {
                var pointer = evt as PointerEventData;
                if (pointer != null && pointer.button != PointerEventData.InputButton.Left)
                    return;
                _petState.SetDragging(true);
            });
            trigger.triggers.Add(down);
            var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            up.callback.AddListener(evt =>
            {
                var pointer = evt as PointerEventData;
                if (pointer != null && pointer.button != PointerEventData.InputButton.Left)
                    return;
                _petState.SetDragging(false);
            });
            trigger.triggers.Add(up);
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => _petState.SetDragging(false));
            trigger.triggers.Add(exit);
        }

        static GameObject CreateStretchPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = color;
            go.GetComponent<Image>().raycastTarget = false;
            return go;
        }

        static GameObject CreateBar(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            if (Mathf.Approximately(anchorMin.y, 1f) && Mathf.Approximately(anchorMax.y, 1f))
            {
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = Vector2.zero;
                rt.offsetMin = new Vector2(0, rt.offsetMin.y);
                rt.offsetMax = new Vector2(0, 0);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Abs(sizeDelta.y));
            }
            else if (Mathf.Approximately(anchorMin.y, 0f) && Mathf.Approximately(anchorMax.y, 0f) &&
                     anchorMax.x > anchorMin.x)
            {
                rt.pivot = new Vector2(0.5f, 0f);
                rt.anchoredPosition = Vector2.zero;
                rt.offsetMin = new Vector2(0, 0);
                rt.offsetMax = new Vector2(0, 0);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Abs(sizeDelta.y));
            }
            else if (Mathf.Approximately(anchorMin.x, 0f) && Mathf.Approximately(anchorMax.x, 0f))
            {
                rt.pivot = new Vector2(0f, 0.5f);
                rt.offsetMin = new Vector2(0, 0);
                rt.offsetMax = new Vector2(sizeDelta.x, 0);
            }
            else
            {
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            go.GetComponent<Image>().color = color;
            return go;
        }

        static Text CreateText(Transform parent, string name, string content, int size, TextAnchor anchor, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.text = content;
            text.fontSize = size;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            if (anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.MiddleCenter)
            {
                if (anchor == TextAnchor.MiddleCenter)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                }
                else
                {
                    rt.anchorMin = new Vector2(anchor == TextAnchor.MiddleRight ? 1 : 0, 1);
                    rt.anchorMax = rt.anchorMin;
                    rt.pivot = new Vector2(anchor == TextAnchor.MiddleRight ? 1 : 0, 0.5f);
                }
                text.alignment = anchor;
            }

            if (anchor == TextAnchor.LowerCenter)
            {
                rt.anchorMin = new Vector2(0.5f, 0);
                rt.anchorMax = new Vector2(0.5f, 0);
                rt.pivot = new Vector2(0.5f, 0);
                text.alignment = TextAnchor.MiddleCenter;
            }

            return text;
        }

        void CreateNavButton(
            Transform parent, string label, UnityEngine.Events.UnityAction onClick, bool lockDuringOnboarding)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 48;
            go.GetComponent<LayoutElement>().flexibleWidth = 1f;
            go.GetComponent<Image>().color = new Color(0.18f, 0.28f, 0.36f, 1f);
            var text = CreateText(go.transform, "Label", label, 16, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(120, 28));
            var tr = text.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            tr.pivot = new Vector2(0.5f, 0.5f);
            text.alignment = TextAnchor.MiddleCenter;
            var button = go.GetComponent<Button>();
            button.onClick.AddListener(onClick);
            if (lockDuringOnboarding)
                _lockableNavButtons.Add(button);
        }

        static void CreateCenteredButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.55f, 1f);
            var text = CreateText(go.transform, "Label", label, 16, TextAnchor.MiddleCenter, Vector2.zero, size);
            var tr = text.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            tr.pivot = new Vector2(0.5f, 0.5f);
            text.alignment = TextAnchor.MiddleCenter;
            go.GetComponent<Button>().onClick.AddListener(onClick);
        }

        static void CreateBottomCenteredButton(Transform parent, string name, string label, float bottom,
            Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, bottom);
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.55f, 1f);
            var text = CreateText(go.transform, "Label", label, 16, TextAnchor.MiddleCenter, Vector2.zero, size);
            var tr = text.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            tr.pivot = new Vector2(0.5f, 0.5f);
            text.alignment = TextAnchor.MiddleCenter;
            go.GetComponent<Button>().onClick.AddListener(onClick);
        }

        static void CreateButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;
            go.GetComponent<Image>().color = new Color(0.2f, 0.45f, 0.55f, 1f);
            var text = CreateText(go.transform, "Label", label, 16, TextAnchor.MiddleCenter, Vector2.zero, size);
            var tr = text.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            tr.pivot = new Vector2(0.5f, 0.5f);
            text.alignment = TextAnchor.MiddleCenter;
            go.GetComponent<Button>().onClick.AddListener(onClick);
        }

        static void CreateToggle(Transform parent, string name, string label, Vector2 anchoredPos, bool value, System.Action<bool> onChanged)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(360, 32);

            var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0, 0.5f);
            bgRt.anchorMax = new Vector2(0, 0.5f);
            bgRt.pivot = new Vector2(0, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = new Vector2(28, 28);
            bg.GetComponent<Image>().color = new Color(0.25f, 0.3f, 0.35f, 1f);

            var check = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            check.transform.SetParent(bg.transform, false);
            var cRt = check.GetComponent<RectTransform>();
            cRt.anchorMin = Vector2.zero;
            cRt.anchorMax = Vector2.one;
            cRt.offsetMin = new Vector2(4, 4);
            cRt.offsetMax = new Vector2(-4, -4);
            check.GetComponent<Image>().color = new Color(0.4f, 0.85f, 0.55f, 1f);

            var labelText = CreateText(go.transform, "Label", label, 18, TextAnchor.MiddleLeft, new Vector2(40, -2), new Vector2(300, 28));
            labelText.alignment = TextAnchor.MiddleLeft;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
        }
    }
}
