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
        string _headerAvatarSkin;
        SpriteFramePetRenderer _homeRenderer;
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

            var canvasGo = TryLoadShellCanvas();
            if (canvasGo == null)
                return;
            EnsureEventSystem();
            if (!WireShellFromPrefab(canvasGo))
            {
                Destroy(canvasGo);
                return;
            }

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
            instance.name = "ShellCanvas";
            NormalizeShellCanvasScale(instance);
            _scaler = instance.GetComponent<CanvasScaler>();
            if (_scaler != null)
            {
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                _scaler.referenceResolution = new Vector2(WindowManager.LoginWidth, WindowManager.LoginHeight);
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
            _headerXpFill = DesktopModalUi.FindComponent<Image>(header, "ProfileCard/XpFill");
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
            DesktopModalUi.BindButton(header, "Mail", () => ShowMainPanel(ShellPanelId.Friends));
            DesktopModalUi.BindButton(header, "Settings", () => ShowMainPanel(ShellPanelId.Settings));
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
            _mainRoot.SetActive(false);
            CatTheme.SkinTree(canvasGo.transform);
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
                    _headerCoins.text = coins.ToString("N0");
            }));
            StartCoroutine(_authenticatedApi.GetProfileHub(_authenticatedApi.PlayerId, "profile", (ok, hub, message) =>
            {
                if (!ok || hub == null)
                    return;
                if (_headerName != null && hub.profile != null)
                    _headerName.text = hub.profile.nickname;
                if (hub.progress == null)
                    return;
                if (_headerLevel != null)
                    _headerLevel.text = "Lv." + hub.progress.level;
                var required = RequiredLevelXp(hub.progress.level);
                if (_headerXp != null)
                    _headerXp.text = required > 0 ? hub.progress.xp + " / " + required : "满级";
                if (_headerXpFill != null)
                    _headerXpFill.fillAmount = required > 0 ? Mathf.Clamp01((float)hub.progress.xp / required) : 1f;
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
            foreach (var pair in _tabs)
            {
                var visual = pair.Value.GetComponent<CozyButtonVisual>();
                if (visual != null)
                    visual.SetSelected(pair.Key == shown);
            }
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
                    1672f, 941f);
            if (_scaler != null)
                _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            WindowManager.Instance?.ApplyMainShell();
            RefreshPetPresentation();
            RefreshHeaderBalance();
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
            HighlightTab(id);
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
            var competition = _mainRoot != null ? _mainRoot.transform.Find("Header/Leaderboard") : null;
            if (competition != null)
                competition.gameObject.SetActive(id == ShellPanelId.Home);
            _productMenu?.Hide();
            _socialPanel?.OnClosed();
            _catchPanel?.OnClosed();
            _galleryPanel?.OnClosed();
            _shopPanel?.OnClosed();
            _equipmentPanel?.OnClosed();
            _achievementPanel?.OnClosed();
            _profilePanel?.OnClosed();
            _profileHubPanel?.OnClosed();
            _profileEditPanel?.OnClosed();
            _socialFeedPanel?.OnClosed();
            _leaderboardPanel?.OnClosed();
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
                    _achievementPanel?.ShowSelf(DesktopProfileHubPanel.HubTab.Achievements);
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

        static void AlignHeaderActions(Transform header, bool showClose)
        {
            if (header == null)
                return;
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
                DesktopModalUi.FindComponent<Image>(header, "ProfileCard/XpFill"));
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
            if (_headerAvatar == null || skin == _headerAvatarSkin)
                return;
            var sprite = CatTheme.LoadCozy("characters/portraits/" + skin + ".png", Vector4.zero)
                ?? CatTheme.LoadCozy("characters/cat-portrait.png", Vector4.zero);
            if (sprite == null)
                return;
            _headerAvatar.sprite = sprite;
            _headerAvatarSkin = skin;
            if (_homeRenderer != null && _homeRenderer.ActiveSkinId != skin)
                _homeRenderer.PlayClip(skin, "fishing");
        }

        void ShowToast(string msg)
        {
            if (_toast == null)
                return;
            _toast.text = msg;
            (_toastRoot != null ? _toastRoot : _toast.gameObject).SetActive(true);
            _toastUntil = Time.unscaledTime + 4f;
        }

        void RegisterPanel(Transform parent, ShellPanelId id, System.Action<GameObject> bind)
        {
            var slot = parent != null ? parent.Find(id.ToString()) : null;
            if (slot == null)
            {
                Debug.LogError("[DesktopUI] Serialized page slot is missing: " + id);
                return;
            }
            var prefabName = PagePrefabName(id);
            var instance = DesktopUiPrefabFactory.Instantiate(prefabName, slot);
            if (instance == null)
                return;
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
            _router.Register(id, slot.gameObject);
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
            var hero = DesktopModalUi.FindComponent<Image>(go.transform, "HeroCat");
            if (hero != null)
            {
                var renderer = hero.GetComponent<SpriteFramePetRenderer>();
                if (renderer != null)
                {
                    _homeRenderer = renderer;
                    renderer.Bind(hero);
                    renderer.Apply(PetVisualState.Fishing);
                    renderer.PlayClip(PetArtLoader.CurrentOwnPetId(), "fishing");
                }
            }
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
                NotificationKind.FishBite, "鱼咬钩", "有" + species + "上钩。"));
            ShowToast("收到服务端咬钩事件。");
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
        }

        void BuildEquipment(GameObject go)
        {
            _equipmentPanel = DesktopFeaturePanelFactory.BindExisting<DesktopShopPanel>(go, view =>
            {
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
            });
        }

        void BuildAchievements(GameObject go)
        {
            _achievementPanel = DesktopFeaturePanelFactory.BindExisting<DesktopProfileHubPanel>(go,
                view => view.Bind(_authenticatedApi, _pondSession,
                    () => ShowMainPanel(ShellPanelId.ProfileEdit), SetStatusMessage));
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
                    () => ShowMainPanel(ShellPanelId.ProfileEdit),
                    SetStatusMessage));
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
                    () => OpenProfileHub()));
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
                view => view.Bind(_authenticatedApi, _pondSession));
        }

        void BuildSocialPanel(GameObject go)
        {
            _socialPanel = DesktopFeaturePanelFactory.BindExisting<DesktopSocialModalView>(go,
                view => view.Bind(_authenticatedApi, _pondSession, _socialLobby));
        }

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
