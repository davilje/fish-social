using System;
using System.Collections;
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
    /// Login retains the original Steam flow; the desktop shell uses a cream-paper
    /// header, a scenic home and a seven-destination engraved-icon dock.
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
        GameObject _toastRoot;
        PendingFishCatchDto _journalPendingCatch;
        int _shownCoins = int.MinValue;
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
        DesktopSocialComposeModal _settlementComposer;
        DesktopLeaderboardPanel _leaderboardPanel;
        readonly List<Button> _lockableNavButtons = new List<Button>();
        readonly Dictionary<ShellPanelId, Button> _tabs = new Dictionary<ShellPanelId, Button>();
        bool _onboardingLock;
        bool _suppressInventoryToast;
        Transform _settlementCanvas;
        DesktopShopPanel _equipmentPanel;
        DesktopProfileHubPanel _achievementPanel;
        CozyPondSelectionPanel _pondSelectionPanel;
        Text _headerCoins;
        Text _headerName;
        Text _headerLevel;
        Text _headerXp;
        Text _headerCatchCount;
        Image _headerXpFill;
        Image _headerAvatar;
        Text _headerUnread;
        GameObject _headerUnreadBadge;
        string _headerAvatarSkin;
        CozyHomeRoom _homeRoom;
        bool _balanceLoading;
        float _nextBalanceRefresh;

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

        void OpenProfileEditor()
        {
            if (!_onboardingLock && _router != null && _router.Current == ShellPanelId.Profile)
                _router.Show(ShellPanelId.ProfileEdit, true);
            else
                ShowMainPanel(ShellPanelId.ProfileEdit);
        }

        void ReturnToProfileFromEditor()
        {
            if (!_onboardingLock && _router != null && _router.Current == ShellPanelId.ProfileEdit)
            {
                _profileHubPanel?.ShowSelf(DesktopProfileHubPanel.HubTab.Profile);
                _router.Show(ShellPanelId.Profile, true);
            }
            else OpenProfileHub();
        }

        public void OpenProfileHub(DesktopProfileHubPanel.HubTab tab = DesktopProfileHubPanel.HubTab.Profile)
        {
            if (tab == DesktopProfileHubPanel.HubTab.Achievements)
            { ShowMainPanel(ShellPanelId.Achievements); return; }
            _profileHubPanel?.ShowSelf(tab);
            ShowMainPanel(ShellPanelId.Profile);
        }

        public void OpenDirectMessage(string playerId, string nickname)
        {
            _socialPanel?.ArmDirectMessage(playerId, nickname);
            ShowMainPanel(ShellPanelId.Friends);
        }

        public void OpenFriends()
        {
            _socialPanel?.ArmSection(SocialSection.Friends);
            ShowMainPanel(ShellPanelId.Friends);
        }

        public void OpenFriendRequests()
        {
            _socialPanel?.ArmSection(SocialSection.Requests);
            ShowMainPanel(ShellPanelId.Friends);
        }

        public void OpenNotifications()
        {
            _socialPanel?.ArmSection(SocialSection.Notifications);
            ShowMainPanel(ShellPanelId.Friends);
        }

        void RefreshSocialUnread(int count)
        {
            if (_headerUnreadBadge != null) _headerUnreadBadge.SetActive(count > 0);
            if (_headerUnread != null) _headerUnread.text = count > 99 ? "99+" : count.ToString();
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

            var canvasGo = TryLoadShellCanvas();
            if (canvasGo == null)
                return;
            EnsureEventSystem();
            if (!WireShellFromPrefab(canvasGo))
            {
                Destroy(canvasGo);
                return;
            }

            if (CozySurplus.Visible)
            {
                _productMenu = canvasGo.AddComponent<DesktopProductMenuView>();
                _productMenu.Bind(canvasGo.transform, _mainRoot.GetComponent<RectTransform>(), this);
            }
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

        GameObject TryLoadShellCanvas()
        {
            var prefab = Resources.Load<GameObject>("Desktop/Prefabs/DesktopShell");
            if (prefab == null)
            {
                Debug.LogWarning("[DesktopShell] Required DesktopShell.prefab missing; runtime layout generation is disabled. " +
                                 "Open Fish Social → UI Prefab 管理 → 补齐全部.");
                return null;
            }

            var instance = Instantiate(prefab, transform, false);
            DesktopLocalization.BindPrefab(instance, "Desktop/Prefabs/DesktopShell");
            instance.name = "ShellCanvas";
            NormalizeShellCanvasScale(instance);
            _scaler = instance.GetComponent<CanvasScaler>();
            if (_scaler != null)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                _scaler.referenceResolution = CozyJournalTheme.DesignResolution;
                _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                _scaler.matchWidthOrHeight = 0.5f;
            }
            return instance;
        }

        bool WireShellFromPrefab(GameObject canvasGo)
        {
            _loginRoot = canvasGo.transform.Find("LoginRoot")?.gameObject;
            _mainRoot = canvasGo.transform.Find("MainRoot")?.gameObject;
            if (_loginRoot == null || _mainRoot == null ||
                !DesktopModalUi.RequireNodes(_mainRoot.transform, "DesktopShell",
                    "Header/ProfileCard/Avatar", "Header/ProfileCard/Name", "Header/FishCoins/Amount", "Nav", "Content"))
                return false;

            _loginStatus = DesktopModalUi.FindComponent<Text>(_loginRoot.transform, "LoginHint");
            DesktopModalUi.BindButton(_loginRoot.transform, "SteamLogin", BeginSteamLogin);
            var header = _mainRoot.transform.Find("Header");
            _headerName = DesktopModalUi.FindComponent<Text>(header, "ProfileCard/Name");
            _headerLevel = DesktopModalUi.FindComponent<Text>(header, "ProfileCard/Level");
            _headerXp = DesktopModalUi.FindComponent<Text>(header, "ProfileCard/XpLabel");
            _headerXpFill = DesktopModalUi.FindXpFill(header);
            var headerXpBackground = DesktopModalUi.FindComponent<Image>(header, "ProfileCard/XpBackground");
            _headerAvatar = DesktopModalUi.FindComponent<Image>(header, "ProfileCard/Avatar");
            _headerCoins = DesktopModalUi.FindComponent<Text>(header, "FishCoins/Amount");
            _headerCatchCount = DesktopModalUi.FindComponent<Text>(header, "CatchCount/Amount");
            _statusPet = DesktopModalUi.FindComponent<Text>(header, "Status/PetStatus");
            _statusLogin = DesktopModalUi.FindComponent<Text>(header, "Status/LoginStatus");
            _statusConnection = DesktopModalUi.FindComponent<Text>(header, "Status/ConnStatus");
            _statusPond = DesktopModalUi.FindComponent<Text>(header, "Status/PondStatus");
            DesktopModalUi.BindButton(header, "ProfileCard", () => OpenProfileHub());
            DesktopModalUi.BindButton(header, "FishCoins/Plus", () => ShowMainPanel(ShellPanelId.Shop));
            DesktopModalUi.BindButton(header, "CatchCount", () => ShowMainPanel(ShellPanelId.CatchBag));
            _headerUnreadBadge = header.Find("Mail/UnreadBadge")?.gameObject;
            _headerUnread = DesktopModalUi.FindComponent<Text>(header, "Mail/UnreadBadge/Count");
            DesktopModalUi.BindButton(header, "Mail", OpenNotifications);
            RefreshSocialUnread(_socialPanel != null ? _socialPanel.UnreadCount : 0);
            DesktopModalUi.BindButton(header, "Settings", () => ShowMainPanel(ShellPanelId.Settings));
            DesktopModalUi.BindButton(header, "WorldMapEntry", () => ShowMainPanel(ShellPanelId.WorldMap));
            CozySurplus.Park(header.Find("Status")?.gameObject);
            CozySurplus.Park(header.Find("Home")?.gameObject);
            CozySurplus.Park(header.Find("Leaderboard")?.gameObject);
            CozyWebStage.AlignHeader(header);
            var worldEntry = DesktopModalUi.FindComponent<Button>(header, "WorldMapEntry");
            if (worldEntry != null) _lockableNavButtons.Add(worldEntry);
            DesktopModalUi.BindButton(header, "Leaderboard", () => ShowMainPanel(ShellPanelId.Leaderboard));
            DesktopModalUi.BindButton(header, "Home", ReturnToPetHome);
            DesktopModalUi.ConfigureXpBar(headerXpBackground, _headerXpFill);
            AlignHeaderActions(header, false);
            HideDuplicateProfileLabels(header);
            var nav = _mainRoot.transform.Find("Nav");
            BindCozyNav(nav, ShellPanelId.CatchBag, () => ShowMainPanel(ShellPanelId.CatchBag));
            BindCozyNav(nav, ShellPanelId.Equipment, () => ShowMainPanel(ShellPanelId.Equipment));
            BindCozyNav(nav, ShellPanelId.Shop, () => ShowMainPanel(ShellPanelId.Shop));
            BindCozyNav(nav, ShellPanelId.Achievements, () => ShowMainPanel(ShellPanelId.Achievements));
            BindCozyNav(nav, ShellPanelId.Leaderboard, () => ShowMainPanel(ShellPanelId.Leaderboard));
            BindCozyNav(nav, ShellPanelId.Profile, () => OpenProfileHub());
            BindCozyNav(nav, ShellPanelId.SocialFeed, () => ShowMainPanel(ShellPanelId.SocialFeed));

            var content = _mainRoot.transform.Find("Content");
            RegisterPanel(content, ShellPanelId.Home, BuildHome);
            RegisterPanel(content, ShellPanelId.Pond, BuildPond);
            RegisterPanel(content, ShellPanelId.WorldMap, BuildWorldMap);
            RegisterPanel(content, ShellPanelId.Shop, BuildShop);
            RegisterPanel(content, ShellPanelId.Friends, BuildSocialPanel);
            RegisterPanel(content, ShellPanelId.CatchBag, BuildCatchPanel);
            RegisterPanel(content, ShellPanelId.Gallery, BuildGalleryPanel);
            RegisterPanel(content, ShellPanelId.Profile, BuildProfilePanel);
            RegisterPanel(content, ShellPanelId.ProfileEdit, BuildProfileEditPanel);
            RegisterPanel(content, ShellPanelId.SocialFeed, BuildSocialFeedPanel);
            RegisterPanel(content, ShellPanelId.Leaderboard, BuildLeaderboardPanel);
            RegisterPanel(content, ShellPanelId.Settings, BuildSettingsPanel);
            RegisterPanel(content, ShellPanelId.Equipment, BuildEquipment);
            RegisterPanel(content, ShellPanelId.PondSelection, BuildPondSelection);
            RegisterPanel(content, ShellPanelId.Achievements, BuildAchievements);
            _toastRoot = canvasGo.transform.Find("Toast")?.gameObject;
            _toast = DesktopModalUi.FindComponent<Text>(canvasGo.transform, "Toast/Label");
            if (_toastRoot != null)
                _toastRoot.SetActive(false);
            SetAuthenticatedRoots(canvasGo.transform, false);
            CatTheme.SkinTree(canvasGo.transform);
            CozyMotion.EnsureHost(canvasGo);
            CozyMotion.EnsureBobber(canvasGo.transform.Find("MainRoot/Nav"));
            _homeRoom = CozyHomeRoom.Build(_mainRoot.transform);
            _homeRoom.Bind(() => ShowMainPanel(ShellPanelId.Equipment),
                () => OpenProfileHub(DesktopProfileHubPanel.HubTab.Showcase));
            var corner = _mainRoot.transform.Find("CozyCornerCat");
            if (corner != null) corner.gameObject.SetActive(false);
            return true;
        }

        void BindCozyNav(Transform nav, ShellPanelId id, UnityEngine.Events.UnityAction action)
        {
            var button = DesktopModalUi.FindComponent<Button>(nav, id.ToString());
            if (button == null)
                return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
            _tabs[id] = button;
            _lockableNavButtons.Add(button);
        }

        static void NormalizeShellCanvasScale(GameObject canvasGo)
        {
            if (canvasGo == null)
                return;
            var rt = canvasGo.GetComponent<RectTransform>();
            if (rt == null || rt.localScale.sqrMagnitude > 0.001f)
                return;
            rt.localScale = Vector3.one;
            Debug.LogWarning("[DesktopShell] Shell canvas scale was zero; reset to 1.");
        }

        void RefreshHeaderBalance()
        {
            if (_headerCoins == null || _balanceLoading || Time.unscaledTime < _nextBalanceRefresh
                || _authenticatedApi == null || !_authenticatedApi.CanUse)
                return;
            _balanceLoading = true;
            _nextBalanceRefresh = Time.unscaledTime + 30f;
            StartCoroutine(_authenticatedApi.GetCoins((ok, coins, message) =>
            {
                _balanceLoading = false;
                if (ok && _headerCoins != null)
                {
                    if (_shownCoins == int.MinValue)
                        _headerCoins.text = coins.ToString("N0");
                    else
                        CozyMotion.Roll(_headerCoins, _shownCoins, coins);
                    _shownCoins = coins;
                }
            }));
            StartCoroutine(_authenticatedApi.GetProfileHub(_authenticatedApi.PlayerId, "profile", (ok, hub, message) =>
            {
                if (!ok || hub == null)
                    return;
                if (_headerName != null && hub.profile != null)
                    _headerName.text = hub.profile.nickname;
                if (hub.progress != null)
                {
                    if (_headerLevel != null)
                        _headerLevel.text = "Lv." + hub.progress.level;
                    var required = RequiredLevelXp(hub.progress.level);
                    if (_headerXp != null)
                        _headerXp.text = required > 0 ? hub.progress.xp + " / " + required : DesktopLocalization.Text("ui.common.header.label.max_level", "满级");
                    if (_headerXpFill != null)
                        DesktopModalUi.SetXpFill(_headerXpFill, required > 0 ? Mathf.Clamp01((float)hub.progress.xp / required) : 1f);
                }
                var profile = _headerName != null ? _headerName.transform.parent : null;
                CozyWebStage.AlignHeader(profile != null ? profile.parent : null);
            }));
        }

        [Serializable]
        sealed class HudLevelRow
        {
            public int level;
            public int xpToNext;
        }

        [Serializable]
        sealed class HudLevelTable
        {
            public HudLevelRow[] rows;
        }

        static int RequiredLevelXp(int level)
        {
            var asset = Resources.Load<TextAsset>("Desktop/Theme/Cozy/data/player-levels");
            if (asset == null)
                return 0;
            var table = JsonUtility.FromJson<HudLevelTable>("{\"rows\":" + asset.text + "}");
            if (table == null || table.rows == null)
                return 0;
            foreach (var row in table.rows)
                if (row.level == level)
                    return row.xpToNext;
            return 0;
        }

        void HighlightTab(ShellPanelId id)
        {
            var shown = id == ShellPanelId.ProfileEdit || id == ShellPanelId.Gallery ? ShellPanelId.Profile : id;
            RectTransform selected = null;
            foreach (var pair in _tabs)
            {
                var visual = pair.Value.GetComponent<CozyButtonVisual>();
                if (visual != null)
                    visual.SetSelected(pair.Key == shown);
                if (pair.Key == shown)
                    selected = pair.Value.transform as RectTransform;
            }
            var nav = _mainRoot != null ? _mainRoot.transform.Find("Nav") : null;
            CozyMotion.MoveBobber(nav, id == ShellPanelId.Home ? null : selected);
        }

        // Login owns its full-window scene. The home backdrop must only be visible
        // with the authenticated shell, including before the first auth callback.
        public static void SetAuthenticatedRoots(Transform shell, bool authenticated)
        {
            if (shell == null) return;
            shell.Find("Backdrop")?.gameObject.SetActive(authenticated);
            shell.Find("MainRoot")?.gameObject.SetActive(authenticated);
            shell.Find("LoginRoot")?.gameObject.SetActive(!authenticated);
        }

        void EnterMainShell()
        {
            if (_mainShellEntered)
                return;
            _mainShellEntered = true;
            _homeRoom?.SetLayoutOwner(_authenticatedApi?.PlayerId);
            SetAuthenticatedRoots(_mainRoot != null ? _mainRoot.transform.parent : null, true);
            if (_scaler != null)
                _scaler.referenceResolution = new Vector2(
                    1672f, 941f);
            if (_scaler != null)
            {
                _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                _scaler.matchWidthOrHeight = .5f;
            }
            WindowManager.Instance?.ApplyMainShell();
            RefreshPetPresentation();
            RefreshHeaderBalance();
            Canvas.ForceUpdateCanvases();
            if (_router != null)
                HighlightTab(_router.Current);
        }

        void ReturnToPetHome()
        {
            ShowMainPanel(ShellPanelId.Home);
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
                WindowManager.Instance?.HideToTray();
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
                WindowManager.Instance?.HideToTray();
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
                    OpenFriends();
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

        void CloseFeaturePanelsExcept(ShellPanelId opening)
        {
            if (opening != ShellPanelId.Friends)
                _socialPanel?.OnClosed();
            if (opening != ShellPanelId.CatchBag)
                _catchPanel?.OnClosed();
            if (opening != ShellPanelId.Gallery)
                _galleryPanel?.OnClosed();
            if (opening != ShellPanelId.Shop)
                _shopPanel?.OnClosed();
            if (opening != ShellPanelId.Equipment)
                _equipmentPanel?.OnClosed();
            if (opening != ShellPanelId.Achievements)
                _achievementPanel?.OnClosed();
            if (opening != ShellPanelId.Profile)
                _profilePanel?.OnClosed();
            // Gallery is presented by the profile hub. Closing that hub here would
            // discard the section this same call is about to show.
            if (opening != ShellPanelId.Profile && opening != ShellPanelId.Gallery)
                _profileHubPanel?.OnClosed();
            if (opening != ShellPanelId.ProfileEdit)
                _profileEditPanel?.OnClosed();
            if (opening != ShellPanelId.SocialFeed)
                _socialFeedPanel?.OnClosed();
            if (opening != ShellPanelId.Leaderboard)
                _leaderboardPanel?.OnClosed();
        }

        void OnPanelChanged(ShellPanelId id)
        {
            HighlightTab(id);
            _homeRoom?.SetPageOpen(id != ShellPanelId.Home);
            if (_mainRoot != null)
            {
                var character = _mainRoot.GetComponentInChildren<CozyCornerCharacter>(true);
                character?.SetPageOpen(id != ShellPanelId.Home);
                character?.NotifyActivity();
            }
            var header = _mainRoot != null ? _mainRoot.transform.Find("Header") : null;
            var page = _mainRoot != null && _mainRoot.transform.Find("Content") != null
                ? _mainRoot.transform.Find("Content/" + id)
                : null;
            var view = page != null && page.childCount > 0 ? page.GetChild(0) : null;
            var authored = CozyItemArt.IsAuthored(view);
            AlignHeaderActions(header, id != ShellPanelId.Home && !authored);
            if (!authored)
            {
                DesktopModalUi.HideDirectCloseButtons(view);
                DesktopModalUi.HidePageUtilityButtons(view);
                DesktopModalUi.PlacePageSealLeft(view);
            }
            CozyWebStage.AlignPage(view);
            if (id == ShellPanelId.CatchBag)
                CozyWebStage.AlignBag(view);
            var competition = _mainRoot != null ? _mainRoot.transform.Find("Header/Leaderboard") : null;
            if (competition != null)
                competition.gameObject.SetActive(CozySurplus.Visible && id == ShellPanelId.Home);
            _productMenu?.Hide();
            // Closing the page that is about to open resets it, then OnOpened builds it again.
            CloseFeaturePanelsExcept(id);
            RefreshHeaderBalance();
            switch (id)
            {
                case ShellPanelId.Equipment:
                    _equipmentPanel?.OnOpened();
                    break;
                case ShellPanelId.PondSelection:
                    _pondSelectionPanel?.OnOpened();
                    break;
                case ShellPanelId.Achievements:
                    _achievementPanel?.ShowSelf(DesktopProfileHubPanel.HubTab.Achievements, false);
                    _achievementPanel?.OnOpened();
                    break;
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
                        _profileHubPanel.ShowSelf(DesktopProfileHubPanel.HubTab.Codex, false);
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
            // Page entrance belongs to the router; selection reveals belong to each presenter.
        }

        static void AlignHeaderActions(Transform header, bool showClose)
        {
            if (header == null)
                return;
            // The journal header is serialized at the approved design coordinates.
            if (header.Find("WorldMapEntry") != null)
            {
                var oldHome = header.Find("Home");
                if (oldHome != null) oldHome.gameObject.SetActive(false);
                return;
            }
            var settings = header.Find("Settings")?.GetComponent<Button>();
            var mail = header.Find("Mail")?.GetComponent<Button>();
            var home = header.Find("Home")?.GetComponent<Button>();
            DesktopModalUi.PlaceTopRight(settings, 28f, 28f, 70f);
            DesktopModalUi.PlaceTopRight(mail, 116f, 28f, 70f);
            DesktopModalUi.PlaceTopRight(home, showClose ? 22f : 150f, 18f, 54f);
            if (settings != null)
                settings.gameObject.SetActive(!showClose);
            if (mail != null)
                mail.gameObject.SetActive(!showClose);
            if (home != null)
            {
                home.gameObject.SetActive(showClose);
                if (showClose)
                {
                    DesktopModalUi.SetButtonGlyph(home, "×");
                    DesktopModalUi.StyleButton(home);
                }
            }
        }

        void HideDuplicateProfileLabels(Transform header)
        {
            var profile = header != null ? header.Find("ProfileCard") : null;
            if (profile == null)
                return;
            foreach (var label in profile.GetComponentsInChildren<Text>(true))
            {
                if (label == _headerName || label == _headerLevel || label == _headerXp)
                    continue;
                if (label.name == "Nickname" || label.name == "PlayerName" || label.name == "NameDuplicate" ||
                    label.name == "LevelDuplicate" || label.name == "XpDuplicate" ||
                    label.name.IndexOf("Duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                    label.gameObject.SetActive(false);
            }

            DesktopModalUi.ConfigureXpBar(
                DesktopModalUi.FindComponent<Image>(header, "ProfileCard/XpBackground"),
                DesktopModalUi.FindXpFill(header));
        }

        void OnDestroy()
        {
            if (_summaryPresentation != null) StopCoroutine(_summaryPresentation);
            _summaryPresentation = null;
            _pendingSessionSummary = null;
            if (_socialPanel != null)
            {
                _socialPanel.UnreadChanged -= RefreshSocialUnread;
                _socialPanel.PondRequested -= OpenSocialPond;
            }
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
            if (_mainShellEntered && _mainRoot != null && _mainRoot.activeInHierarchy)
            {
                RefreshHeaderBalance();
                RefreshHeaderAvatar();
            }
            if (_toast != null && _toast.gameObject.activeSelf && Time.unscaledTime > _toastUntil)
                (_toastRoot != null ? _toastRoot : _toast.gameObject).SetActive(false);
        }

        void RefreshHeaderAvatar()
        {
            var skin = PetArtLoader.CurrentOwnPetId();
            if (_headerAvatar == null)
                return;
            var profile = DesktopProfileCache.Latest;
            var sprite = DesktopDefaultAvatars.Portrait(profile?.avatarUrl, profile?.playerId);
            if (sprite == null)
                return;
            _headerAvatar.sprite = sprite;
            if (_homeRoom == null && _headerAvatarSkin != skin && _mainRoot != null)
                CozyMotion.SetCornerCharacter(_mainRoot.transform, skin == "orange" ? null : skin);
            _headerAvatarSkin = skin;
        }

        void ShowToast(string msg, bool error = false)
        {
            if (string.IsNullOrEmpty(msg)) return;
            if (_toastRoot != null) _toastRoot.SetActive(false);
            else if (_toast != null) _toast.gameObject.SetActive(false);
            var host = _mainRoot != null && _mainRoot.activeInHierarchy ? _mainRoot.transform : transform;
            CozyJournalToast.Show(host, msg, error);
        }

        void RegisterPanel(Transform parent, ShellPanelId id, System.Action<GameObject> bind)
        {
            var slot = parent != null ? parent.Find(id.ToString()) : null;
            if (slot == null)
            {
                Debug.LogError("[DesktopUI] Serialized page slot is missing: " + id);
                return;
            }
            if (id == ShellPanelId.ProfileEdit)
                FitJournalPageSlot(slot as RectTransform);
            var prefabName = PagePrefabName(id);
            var instance = DesktopUiPrefabFactory.Instantiate(prefabName, slot);
            if (instance == null)
                return;
            // Page prefabs can be saved inactive. The slot, not the page root, owns visibility.
            if (!instance.activeSelf)
                instance.SetActive(true);
            bind(instance);
            var close = instance.transform.Find("DesignHeader/Close")?.GetComponent<Button>();
            if (close != null)
            {
                close.onClick.RemoveAllListeners();
                close.onClick.AddListener(() => _router.Show(ShellPanelId.Home));
            }
            var share = instance.transform.Find("DesignFeedInfo/Share")?.GetComponent<Button>();
            if (share != null)
            {
                share.onClick.RemoveAllListeners();
                share.onClick.AddListener(() => _router.Show(ShellPanelId.CatchBag));
            }
            CatTheme.SkinTree(instance.transform);
            CozyWebStage.AlignPage(instance.transform);
            if (id == ShellPanelId.CatchBag)
                CozyWebStage.AlignBag(instance.transform);
            _router.Register(id, slot.gameObject);
        }

        static void FitJournalPageSlot(RectTransform slot)
        {
            if (slot == null)
                return;
            slot.anchorMin = slot.anchorMax = new Vector2(0f, 1f);
            slot.pivot = new Vector2(0f, 1f);
            slot.anchoredPosition = new Vector2(160f, -150f);
            slot.sizeDelta = new Vector2(1370f, 650f);
        }

        static string PagePrefabName(ShellPanelId id)
        {
            switch (id)
            {
                case ShellPanelId.Home: return "PanelHome";
                case ShellPanelId.Pond: return "PanelPond";
                case ShellPanelId.WorldMap: return "PanelWorldMap";
                case ShellPanelId.Shop: return "PanelShop";
                case ShellPanelId.Friends: return "PanelSocial";
                case ShellPanelId.CatchBag: return "PanelCatch";
                case ShellPanelId.Gallery: return "PanelGallery";
                case ShellPanelId.Profile: return "PanelProfileHub";
                case ShellPanelId.ProfileEdit: return "PanelProfileEdit";
                case ShellPanelId.SocialFeed: return "PanelSocialFeed";
                case ShellPanelId.Leaderboard: return "PanelLeaderboard";
                case ShellPanelId.Settings: return "PanelSettings";
                case ShellPanelId.Equipment: return "PanelEquipment";
                case ShellPanelId.PondSelection: return "PanelPondSelection";
                case ShellPanelId.Achievements: return "PanelAchievements";
                default: return null;
            }
        }

        void BuildHome(GameObject go)
        {
            _petStateLabel = DesktopModalUi.FindComponent<Text>(go.transform, "MenuCard/SessionState");
            _petPond = DesktopModalUi.FindComponent<Text>(go.transform, "MenuCard/CurrentPond");
            DesktopModalUi.BindButton(go.transform, "MenuCard/StartFishing", OpenPond);
            DesktopModalUi.BindButton(go.transform, "MenuCard/ExploreWorld", () => ShowMainPanel(ShellPanelId.WorldMap));
            DesktopModalUi.BindButton(go.transform, "MenuCard/DailyGift", () =>
            {
                ShowMainPanel(ShellPanelId.Shop);
                SetStatusMessage("每日登录礼物尚未开放；可在商店查看钓鱼补给。");
            });
            DesktopModalUi.BindButton(go.transform, "MenuCard/SelectPond", () => ShowMainPanel(ShellPanelId.PondSelection));
            var menu = go.transform.Find("MenuCard");
            if (menu != null)
                CozySurplus.Park(menu.gameObject);
            // The room owns the sole home companion. The real pond renderer remains independent.
            foreach (Transform child in go.transform) child.gameObject.SetActive(false);
            var graphic = go.GetComponent<Graphic>();
            if (graphic != null) graphic.enabled = false;
        }

        void OpenPond()
        {
            if (_pondSession == null)
            {
                ShowToast(DesktopLocalization.Text("ui.common.pond.error.session_missing", "鱼塘会话尚未初始化。"));
                return;
            }

            DesktopAppBootstrap.Instance?.StartNativeOverlay();
            WindowManager.Instance?.HideToTray();

            if (_pondSession.State == SocialSocketState.Connected ||
                _pondSession.State == SocialSocketState.Connecting ||
                _pondSession.State == SocialSocketState.Reconnecting)
            {
                ShowToast(_pondSession.State == SocialSocketState.Connected
                    ? DesktopLocalization.Text("ui.common.pond.message.resumed_window_hidden", "已恢复当前鱼塘，主窗口已隐藏。")
                    : DesktopLocalization.Text("ui.common.pond.status.connecting_window_hidden", "鱼塘会话正在连接，主窗口已隐藏。"));
            DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
            return;
            }

            _pondSession.ConnectAndJoin();
            ShowToast(DesktopLocalization.Text("ui.common.pond.status.entering", "正在进入鱼塘。"));
            DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void RefreshPetPresentation()
        {
            if (_headerCatchCount != null)
                _headerCatchCount.text = (_pondSession != null && _pondSession.CurrentInventory != null
                    ? _pondSession.CurrentInventory.Length : 0).ToString();
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
                ShowToast(DesktopLocalization.Text("ui.common.auth.error.component_missing", "Steam 登录组件尚未初始化。"));
                return;
            }
            _steamAuth.BeginLogin();
        }

        void ValidateCurrentSession()
        {
            if (_authenticatedApi == null || !_authenticatedApi.CanUse)
            {
                ShowToast(DesktopLocalization.Text("ui.common.auth.error.login_required", "请先完成 Steam 登录。"));
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
                    label = DesktopLocalization.Text("ui.common.auth.status.initializing_steam", "登录：初始化 Steam");
                    break;
                case SteamLoginState.RequestingTicket:
                    label = DesktopLocalization.Text("ui.common.auth.status.requesting_ticket", "登录：获取 Steam Ticket");
                    break;
                case SteamLoginState.Authenticating:
                    label = DesktopLocalization.Text("ui.common.auth.status.verifying", "登录：服务端验证中");
                    break;
                case SteamLoginState.Authenticated:
                    label = DesktopLocalization.Text("ui.common.auth.status.connected", "登录：Steam 已连接");
                    EnterMainShell();
                    ShowToast(DesktopLocalization.Text("ui.common.auth.message.login_success", "Steam 登录成功"));
                    break;
                case SteamLoginState.Failed:
                    label = DesktopLocalization.Text("ui.common.auth.status.failed", "登录：失败");
                    break;
                default:
                    label = "登录：未登录";
                    break;
            }

            if (state == SteamLoginState.SignedOut)
            {
                _journalPendingCatch = null;
                _mainRoot?.GetComponent<CozyCatchCelebration>()?.Clear();
                if (_mainShellEntered)
                {
                    _mainShellEntered = false;
                    SetAuthenticatedRoots(_mainRoot != null ? _mainRoot.transform.parent : null, false);
                    WindowManager.Instance?.ApplyLoginShell();
                }
            }

            if (_loginStatus != null)
                _loginStatus.text = state == SteamLoginState.SignedOut ? DesktopLocalization.Text("ui.common.auth.hint.use_steam", "请使用 Steam 登录") : label;
            if (_statusLogin != null)
                _statusLogin.text = label;
            DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void OnSteamError(string message)
        {
            if (_loginStatus != null)
                _loginStatus.text = DesktopLocalization.Text("ui.common.auth.status.failed", "登录：失败");
            if (_statusLogin != null)
                _statusLogin.text = "登录：失败";
            ShowToast(message);
            DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void OnPondStateChanged(SocialSocketState state, string message)
        {
            string label;
            switch (state)
            {
                case SocialSocketState.Connecting: label = DesktopLocalization.Text("ui.common.connection.status.connecting", "连接中"); break;
                case SocialSocketState.Connected: label = DesktopLocalization.Text("ui.common.connection.status.online", "在线"); break;
                case SocialSocketState.Reconnecting: label = DesktopLocalization.Text("ui.common.connection.status.reconnecting", "重连中"); break;
                case SocialSocketState.Failed: label = DesktopLocalization.Text("ui.common.connection.status.failed", "连接失败"); break;
                default: label = DesktopLocalization.Text("ui.common.connection.status.disconnected", "断开"); break;
            }
            if (_statusConnection != null)
                _statusConnection.text = "连接：" + label;
            if (_pondStatus != null && !string.IsNullOrEmpty(message))
                _pondStatus.text = DesktopLocalization.Format("ui.common.connection.message.details", (object)(label), (object)(message));
            // Bootstrap owns overlay publish + deferred pet refresh on pond state.
        }

        void OnPondSnapshot(PondSnapshotDto snapshot)
        {
            if (_pondStatus != null)
            {
                var count = snapshot?.users?.Length ?? 0;
                _pondStatus.text = DesktopLocalization.Format("ui.common.connection.message.pond_snapshot", (object)(count), (object)((_pondSession?.CurrentPhase ?? "idle")), (object)(FormatOtherPlayers()));
            }
            // Overlay publish is owned by DesktopAppBootstrap; only refresh the
            // in-window hero cat after IPC work (RequestPetVisualRefresh).
            DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
        }

        void OnPondUserUpdated(PondUserDto user)
        {
            var own = _pondSession?.CurrentUser;
            var isOwn = user != null && own != null && user.id == own.id;
            if (isOwn && _pondStatus != null)
                _pondStatus.text = DesktopLocalization.Format("ui.common.connection.message.user_snapshot", (object)((user?.fishingPhase ?? "idle")), (object)((user?.spotId ?? DesktopLocalization.Text("ui.common.connection.label.spot_unselected", "未选择"))), (object)(FormatOtherPlayers()));
            if (isOwn)
                DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
        }

        void OnPondUsersChanged()
        {
            // Bootstrap already coalesces UsersChanged → PublishNativeOverlayState.
        }

        string FormatOtherPlayers()
        {
            var others = _pondSession?.VisibleOthers;
            if (others == null || others.Length == 0)
                return DesktopLocalization.Text("ui.common.connection.message.no_other_players", "\n同塘玩家：无");
            var text = DesktopLocalization.Text("ui.common.connection.label.other_players", "\n同塘玩家：");
            var shown = Mathf.Min(others.Length, 6);
            for (var i = 0; i < shown; i++)
            {
                var user = others[i];
                text += "\n· " + (string.IsNullOrEmpty(user.nickname) ? user.playerId : user.nickname) +
                        " · " + PetStateController.ToChinese(
                            PetStateController.FromFishingPhase(user.fishingPhase));
            }

            if (others.Length > shown)
                text += DesktopLocalization.Format("ui.common.connection.label.additional_player_count", (object)(others.Length));
            return text;
        }

        void OnPondFishBite(PendingFishCatchDto fishCatch)
        {
            _journalPendingCatch = fishCatch;
            if (_mainRoot != null) CozyMotion.SetCornerPose(_mainRoot.transform, CozyCornerCharacter.Pose.LookUp);
            DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
            var species = fishCatch != null ? DesktopFishCatalog.SpeciesName(fishCatch.speciesId) : DesktopLocalization.Text("ui.common.bite.label.default_fish", "鱼");
            DesktopNotificationService.Instance?.Publish(new DesktopNotification(
                NotificationKind.FishBite, DesktopLocalization.Text("ui.common.bite.title.notification", "鱼咬钩"), DesktopLocalization.Format("ui.common.bite.message.notification", (object)(species))));
            ShowToast(DesktopLocalization.Format("ui.common.bite.message.toast", (object)(species)));
        }

        void OnPondFishCatchSettled(FishCatchSettledDto settled)
        {
            _suppressInventoryToast = true;
            DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
            var pending = _journalPendingCatch;
            _journalPendingCatch = null;
            if (settled != null && pending != null && pending.speciesId == settled.speciesId && _mainRoot != null)
            {
                var detail = DesktopFishCatalog.QualityName(settled.quality) + " · " + settled.sizeM.ToString("0.00") + " m";
                if (settled.autoReturned) detail += DesktopLocalization.Format("ui.common.settlement.label.release_income", (object)(settled.gold));
                var celebration = CozyMotion.CelebrateCatch(_mainRoot.transform, pending.catchId,
                    DesktopFishCatalog.SpeciesName(settled.speciesId), CozyItemArt.Fish(settled.speciesId), pending.isCodexNew, detail, settled.sizeM);
                celebration.CoinsTarget = _mainRoot.transform.Find("Header/FishCoins/Icon") as RectTransform;
                celebration.Character = _mainRoot.GetComponentInChildren<CozyCornerCharacter>(true);
            }
            if (settled != null && !string.IsNullOrEmpty(settled.message))
                ShowToast(settled.message);
            else if (settled != null && settled.autoReturned)
                ShowToast(DesktopLocalization.Format("ui.common.settlement.message.auto_release", (object)(settled.gold)));
        }

        void OnPondSessionSummary(PondSessionSummaryDto summary)
        {
            if (summary == null || _settlementCanvas == null)
                return;
            _pendingSessionSummary = summary;
            if (_summaryPresentation == null)
                _summaryPresentation = StartCoroutine(PresentSummaryAfterReturn());
        }

        PondSessionSummaryDto _pendingSessionSummary;
        Coroutine _summaryPresentation;
        bool _overlayReturnPending;
        public void SetOverlayReturnPending(bool pending) => _overlayReturnPending = pending;

        IEnumerator PresentSummaryAfterReturn()
        {
            // The server receipt can precede the leave acknowledgement. Do not
            // create even the backdrop until routing and native restore settle.
            do
            {
                yield return null;
            } while (_overlayReturnPending || CozyMotion.Paused ||
                (WindowManager.Instance != null && !WindowManager.Instance.IsWindowVisible) ||
                (_router != null && _router.IsTransitioning) ||
                (_pondSession != null && _pondSession.IsTransitioning));
            var summary = _pendingSessionSummary;
            _pendingSessionSummary = null;
            _summaryPresentation = null;
            PresentPondSessionSummary(summary);
        }

        void PresentPondSessionSummary(PondSessionSummaryDto summary)
        {
            if (summary == null || _settlementCanvas == null) return;
            // The leave ack and the summary event can both arrive. Keep the one already open.
            if (_settlementCanvas.GetComponentInChildren<DesktopPondSettlementModalView>(true) != null)
                return;
            var view = DesktopPondSettlementModalView.Show(_settlementCanvas, summary);
            if (view == null) return;
            view.ProfileRequested += () => OpenProfileHub();
            view.ShareRequested += fishId =>
            {
                if (_settlementComposer == null)
                    _settlementComposer = DesktopSocialComposeModal.Create(_settlementCanvas);
                if (_settlementComposer == null) return;
                _settlementComposer.Open(_authenticatedApi, _pondSession != null ? _pondSession.Nickname : DesktopLocalization.Text("ui.common.session.label.default_angler", "钓友"), () =>
                {
                    if (view != null) view.Close();
                    ShowMainPanel(ShellPanelId.SocialFeed);
                    _socialFeedPanel?.RefreshMyFeed();
                    ShowToast(DesktopLocalization.Text("ui.common.session.message.story_published", "鱼获故事已发布。"));
                }, DesktopProfileCache.Latest?.avatarUrl, fishId);
            };
        }

        public void ShowPondSessionSummary(PondSessionSummaryDto summary)
        {
            OnPondSessionSummary(summary);
        }

        void OnInventoryUpdated(FishInventoryItemDto[] items)
        {
            DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
            if (_suppressInventoryToast)
            {
                _suppressInventoryToast = false;
                return;
            }
            ShowToast(DesktopLocalization.Format("ui.common.inventory.message.updated", (object)((items != null ? items.Length : 0))));
        }

        void OnFriendRequestNotify(FriendRequestDto request)
        {
            // The social inbox classifies this socket event once and routes it to Requests.
            ShowToast(DesktopLocalization.Format("ui.common.friend_request.message.received", (object)((request != null ? request.fromNickname : DesktopLocalization.Text("ui.common.session.label.default_angler", "钓友")))));
        }

        void OnPondError(string message)
        {
            if (!string.IsNullOrEmpty(message) && message.IndexOf("金币不足") >= 0)
                SetStatusMessage(message);
            else
                ShowToast(message, true);
        }

        void OnSocialLobbyError(string message)
        {
            ShowToast(message, true);
        }

        void ShowSocketResult(bool ok, string message)
        {
            ShowToast(message, !ok);
            DesktopAppBootstrap.Instance?.RequestPetVisualRefresh();
            DesktopAppBootstrap.Instance?.PublishNativeOverlayState();
        }

        void BuildPond(GameObject go)
        {
            _pondStatus = DesktopModalUi.FindComponent<Text>(go.transform, "FishingControls/PondSession");
            DesktopModalUi.BindButton(go.transform, "FishingControls/EnterOverlay", OpenPond);
            DesktopModalUi.BindButton(go.transform, "FishingControls/SelectPond", () => ShowMainPanel(ShellPanelId.PondSelection));
            DesktopModalUi.BindButton(go.transform, "FishingControls/ConnectPond", () => _pondSession?.ConnectAndJoin());
            DesktopModalUi.BindButton(go.transform, "FishingControls/TakeSpot", () => _pondSession?.TakeFirstSpot(ShowSocketResult));
            DesktopModalUi.BindButton(go.transform, "FishingControls/StartFishing", () => _pondSession?.StartFishingAtFirstSpot(ShowSocketResult));
            DesktopModalUi.BindButton(go.transform, "FishingControls/StopFishing", () => _pondSession?.StopFishing(ShowSocketResult));
            DesktopModalUi.BindButton(go.transform, "FishingControls/CatchBag", () => ShowMainPanel(ShellPanelId.CatchBag));
            var image = DesktopModalUi.FindComponent<Image>(go.transform, "HeroCat");
            var renderer = image != null ? image.GetComponent<SpriteFramePetRenderer>() : null;
            if (renderer != null)
            {
                renderer.Bind(image);
                _petState?.AddRenderer(renderer);
            }
        }



        void BuildWorldMap(GameObject go)
        {
            _worldMapPanel = DesktopFeaturePanelFactory.BindExisting<DesktopWorldMapPanel>(
                go,
                view => view.Bind(_pondSession, _authenticatedApi));
            if (_pondSelectionPanel != null && _worldMapPanel != null)
                _pondSelectionPanel.BindSelection(_worldMapPanel.SelectionState);
            DesktopModalUi.BindButton(go.transform, "ListPonds", () => ShowMainPanel(ShellPanelId.PondSelection));
        }

        void BuildEquipment(GameObject go)
        {
            _equipmentPanel = DesktopFeaturePanelFactory.BindExisting<DesktopShopPanel>(go, view =>
            {
                view.ShopRequested = () => ShowMainPanel(ShellPanelId.Shop);
                view.SetEquipmentMode(true);
                view.Bind(_authenticatedApi);
            });
        }

        void BuildPondSelection(GameObject go)
        {
            _pondSelectionPanel = go.GetComponent<CozyPondSelectionPanel>();
            if (_pondSelectionPanel == null)
                return;
            _pondSelectionPanel.Bind(pondId =>
            {
                if (_onboardingLock)
                {
                    SetStatusMessage("请先完成新手引导。");
                    return;
                }
                ShowMainPanel(ShellPanelId.WorldMap);
                _worldMapPanel?.FocusPond(pondId);
            }, pondId =>
            {
                if (_onboardingLock)
                {
                    SetStatusMessage("请先完成新手引导。");
                    return;
                }
                _worldMapPanel?.EnterPond(pondId);
            });
            if (_worldMapPanel != null) _pondSelectionPanel.BindSelection(_worldMapPanel.SelectionState);
        }

        void BuildAchievements(GameObject go)
        {
            _achievementPanel = DesktopFeaturePanelFactory.BindExisting<DesktopProfileHubPanel>(go,
                view => view.Bind(_authenticatedApi, _pondSession,
                    () => OpenProfileEditor(), SetStatusMessage, () => ShowMainPanel(ShellPanelId.WorldMap),
                    () => ShowMainPanel(ShellPanelId.Achievements)));
        }

        void BuildShop(GameObject go)
        {
            _shopPanel = DesktopFeaturePanelFactory.BindExisting<DesktopShopPanel>(
                go,
                view => view.Bind(_authenticatedApi));
        }

        void BuildProfilePanel(GameObject go)
        {
            _profileHubPanel = DesktopFeaturePanelFactory.BindExisting<DesktopProfileHubPanel>(
                go,
                view => view.Bind(
                    _authenticatedApi,
                    _pondSession,
                    () => OpenProfileEditor(),
                    SetStatusMessage,
                    () => ShowMainPanel(ShellPanelId.WorldMap),
                    () => ShowMainPanel(ShellPanelId.Achievements)));
            // Keep legacy profile panel available for edit back-compat paths if needed.
            _profilePanel = null;
        }

        void BuildProfileEditPanel(GameObject go)
        {
            _profileEditPanel = DesktopFeaturePanelFactory.BindExisting<DesktopProfileEditPanel>(
                go,
                view => view.Bind(
                    _authenticatedApi,
                    _pondSession,
                    () => ReturnToProfileFromEditor(),
                    tab => OpenProfileHub(tab)));
            if (_profileEditPanel != null)
                _profileEditPanel.ProfileSaved += profile => _profileHubPanel?.ApplySavedProfile(profile);
        }

        void BuildSocialFeedPanel(GameObject go)
        {
            _socialFeedPanel = DesktopFeaturePanelFactory.BindExisting<DesktopSocialFeedPanel>(
                go,
                view => view.Bind(_authenticatedApi, _pondSession));
        }

        void BuildLeaderboardPanel(GameObject go)
        {
            _leaderboardPanel = DesktopFeaturePanelFactory.BindExisting<DesktopLeaderboardPanel>(
                go,
                view => view.Bind(_authenticatedApi, _pondSession, OpenOtherPlayerProfile));
        }

        void BuildSocialPanel(GameObject go)
        {
            _socialPanel = DesktopFeaturePanelFactory.BindExisting<DesktopSocialModalView>(go,
                view => view.Bind(_authenticatedApi, _pondSession, _socialLobby));
            if (_socialPanel != null)
            {
                _socialPanel.UnreadChanged += RefreshSocialUnread;
                _socialPanel.PondRequested += OpenSocialPond;
                RefreshSocialUnread(_socialPanel.UnreadCount);
            }
        }

        void OpenSocialPond() { ShowMainPanel(ShellPanelId.WorldMap); }

        void BuildCatchPanel(GameObject go)
        {
            _catchPanel = DesktopFeaturePanelFactory.BindExisting<DesktopCatchBagModalView>(go,
                view => view.Bind(_authenticatedApi, _pondSession));
        }

        void BuildGalleryPanel(GameObject go)
        {
            _galleryPanel = DesktopFeaturePanelFactory.BindExisting<DesktopGalleryModalView>(go,
                view => view.Bind(_authenticatedApi, _pondSession));
        }

        void BuildSettingsPanel(GameObject go)
        {
            _settingsPanel = DesktopFeaturePanelFactory.BindExisting<DesktopSettingsModalView>(go,
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

    }
}
