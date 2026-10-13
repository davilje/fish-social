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
    /// Login stays on a 480×320 canvas. The main shell is a 1280×720 reference
    /// with a 64px header and a 40px row of five tabs. Map, bag, feed, and rank
    /// stay on the product menu and on the pages that already own them.
    /// </summary>
    public sealed class DesktopShellUi : MonoBehaviour, IDesktopProductMenuHandler
    {
        const float HeaderHeight = 64f;
        const float TabHeight = 40f;

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
        readonly Dictionary<ShellPanelId, Button> _tabs = new Dictionary<ShellPanelId, Button>();
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

            var canvasGo = TryLoadShellCanvas();
            var usedPrefabShell = false;
            if (canvasGo != null)
            {
                EnsureEventSystem();
                if (WireShellFromPrefab(canvasGo))
                    usedPrefabShell = true;
                else
                {
                    Destroy(canvasGo);
                    canvasGo = null;
                }
            }

            if (!usedPrefabShell)
            {
                canvasGo = new GameObject("ShellCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
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
                ApplyShellFrame();
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
                Debug.LogWarning("[DesktopShell] DesktopShell.prefab missing; building chrome at runtime. " +
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
            if (_loginRoot == null || _mainRoot == null)
            {
                Debug.LogError("[DesktopShell] DesktopShell.prefab missing LoginRoot/MainRoot.");
                return false;
            }

            _loginStatus = _loginRoot.transform.Find("LoginHint")?.GetComponent<Text>();
            var steamBtn = _loginRoot.transform.Find("SteamLogin")?.GetComponent<Button>();
            if (steamBtn != null)
            {
                steamBtn.onClick.RemoveAllListeners();
                steamBtn.onClick.AddListener(BeginSteamLogin);
            }

            var header = _mainRoot.transform.Find("Header");
            if (header != null)
            {
                BindSquarePet(header, "HeaderPet", new Vector2(8, -8), 40, false);
                _statusPet = header.Find("PetStatus")?.GetComponent<Text>();
                _statusLogin = header.Find("LoginStatus")?.GetComponent<Text>();
                _statusConnection = header.Find("ConnStatus")?.GetComponent<Text>();
                _statusPond = header.Find("PondStatus")?.GetComponent<Text>();
            }

            var nav = _mainRoot.transform.Find("Nav");
            if (nav != null)
            {
                BindNav(nav, "主页", ReturnToPetHome, true);
                BindNav(nav, "鱼塘", ShowPondPanel, true);
                BindNav(nav, "世界地图", () => ShowMainPanel(ShellPanelId.WorldMap), true);
                BindNav(nav, "商店", () => ShowMainPanel(ShellPanelId.Shop), true);
                // Prefab may use safe names (no '/') or legacy "好友/聊天" / "鱼获/背包".
                BindNav(nav, FindNavChildName(nav, "好友聊天", "好友/聊天"),
                    () => ShowMainPanel(ShellPanelId.Friends), true);
                BindNav(nav, FindNavChildName(nav, "鱼获背包", "鱼获/背包"),
                    () => ShowMainPanel(ShellPanelId.CatchBag), true);
                BindNav(nav, "个人中心", () => OpenProfileHub(), true);
                BindNav(nav, "动态", () => ShowMainPanel(ShellPanelId.SocialFeed), true);
                BindNav(nav, "排行榜", () => ShowMainPanel(ShellPanelId.Leaderboard), true);
                BindNav(nav, "设置", () => ShowMainPanel(ShellPanelId.Settings), false);
            }

            var content = _mainRoot.transform.Find("Content");
            if (content != null)
            {
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
            }

            _toast = canvasGo.transform.Find("Toast")?.GetComponent<Text>();
            if (_toast != null)
                _toast.gameObject.SetActive(false);

            _mainRoot.SetActive(false);
            _loginRoot.SetActive(true);
            ApplyLoginFrame();
            ApplyShellFrame();
            return true;
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

        static string FindNavChildName(Transform nav, string preferred, string legacy)
        {
            for (var i = 0; i < nav.childCount; i++)
            {
                var n = nav.GetChild(i).name;
                if (n == preferred || n == legacy)
                    return n;
            }
            return preferred;
        }

        void BindNav(Transform nav, string name, Action onClick, bool lockable)
        {
            // Transform.Find treats '/' as a path separator — never use Find for labels like "好友/聊天".
            Transform child = null;
            for (var i = 0; i < nav.childCount; i++)
            {
                var c = nav.GetChild(i);
                if (c != null && c.name == name)
                {
                    child = c;
                    break;
                }
            }
            var button = child != null ? child.GetComponent<Button>() : null;
            if (button == null)
            {
                Debug.LogWarning("[DesktopShell] Nav button missing: " + name);
                return;
            }
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => onClick());
            if (lockable)
                _lockableNavButtons.Add(button);
        }

        void BuildLogin(GameObject go)
        {
            CreateText(go.transform, "Brand", "Fish Social", 26, TextAnchor.MiddleCenter,
                new Vector2(0, 70), new Vector2(400, 36));
            _loginStatus = CreateText(go.transform, "LoginHint", "请使用 Steam 登录", 16, TextAnchor.MiddleCenter,
                new Vector2(0, 24), new Vector2(400, 48));
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
            ApplyLoginFrame();
            ApplyShellFrame();
        }

        void ApplyLoginFrame()
        {
            if (_loginRoot == null)
                return;
            var paper = _loginRoot.GetComponent<Image>();
            if (paper != null)
                paper.color = CatTheme.Cream;
            var layout = _loginRoot.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
                layout = _loginRoot.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            SetPreferred(_loginRoot.transform, "Brand", 360f, 36f);
            SetPreferred(_loginRoot.transform, "LoginHint", 360f, 48f);
            SetPreferred(_loginRoot.transform, "SteamLogin", 200f, 44f);
            Paint(_loginRoot.transform, "Brand", CatTheme.Ink);
            Paint(_loginRoot.transform, "LoginHint", CatTheme.Ink);
            var steam = _loginRoot.transform.Find("SteamLogin");
            if (steam != null)
            {
                CatTheme.ApplyPlate(steam.GetComponent<Image>(), "btn-coral");
                Paint(steam, "Label", CatTheme.Cream);
            }
        }

        void ApplyShellFrame()
        {
            if (_mainRoot == null)
                return;

            var paper = _mainRoot.GetComponent<Image>();
            if (paper != null)
                paper.color = CatTheme.Cream;

            var header = _mainRoot.transform.Find("Header") as RectTransform;
            var nav = _mainRoot.transform.Find("Nav") as RectTransform;
            var content = _mainRoot.transform.Find("Content") as RectTransform;
            if (header != null)
            {
                var headerImage = header.GetComponent<Image>();
                if (headerImage != null)
                    headerImage.color = CatTheme.Teal;
                header.anchorMin = new Vector2(0f, 1f);
                header.anchorMax = new Vector2(1f, 1f);
                header.pivot = new Vector2(0.5f, 1f);
                header.offsetMin = new Vector2(0f, -HeaderHeight);
                header.offsetMax = Vector2.zero;
                var pet = header.Find("HeaderPetSlot") as RectTransform;
                if (pet != null)
                {
                    pet.anchoredPosition = new Vector2(12f, -12f);
                    pet.sizeDelta = new Vector2(40f, 40f);
                }

                SetShown(header, "Title", false);
                SetShown(header, "LoginStatus", false);
                PlaceHeaderLine(header, "PetStatus", 64f, 20f, 420f, 24f, TextAnchor.MiddleLeft);
                PlaceHeaderLine(header, "ConnStatus", -392f, 20f, 160f, 24f, TextAnchor.MiddleRight);
                PlaceHeaderLine(header, "PondStatus", -224f, 20f, 160f, 24f, TextAnchor.MiddleRight);
            }

            if (nav != null)
            {
                var settings = FindDirect(nav, "设置") ?? (header != null ? FindDirect(header, "设置") : null);
                if (settings != null && header != null && settings.parent != header)
                    settings.SetParent(header, false);
                if (settings != null)
                    PlaceSettingsButton(settings);

                nav.anchorMin = new Vector2(0f, 1f);
                nav.anchorMax = new Vector2(1f, 1f);
                nav.pivot = new Vector2(0.5f, 1f);
                nav.offsetMin = new Vector2(0f, -(HeaderHeight + TabHeight));
                nav.offsetMax = new Vector2(0f, -HeaderHeight);
                var navImage = nav.GetComponent<Image>();
                if (navImage != null)
                    navImage.color = CatTheme.Cream;
                var existingHorizontal = nav.GetComponent<HorizontalLayoutGroup>();
                if (existingHorizontal != null)
                    DestroyImmediate(existingHorizontal);
                var existingVertical = nav.GetComponent<VerticalLayoutGroup>();
                if (existingVertical != null)
                    DestroyImmediate(existingVertical);
                var tabs = nav.gameObject.AddComponent<HorizontalLayoutGroup>();
                tabs.padding = new RectOffset(16, 16, 0, 0);
                tabs.spacing = 8f;
                tabs.childAlignment = TextAnchor.MiddleLeft;
                tabs.childControlWidth = true;
                tabs.childControlHeight = true;
                tabs.childForceExpandWidth = false;
                tabs.childForceExpandHeight = true;
                StyleTabRow(nav);
            }

            if (content != null)
            {
                var contentImage = content.GetComponent<Image>();
                if (contentImage != null)
                    contentImage.color = CatTheme.Cream;
                content.anchorMin = Vector2.zero;
                content.anchorMax = Vector2.one;
                content.offsetMin = Vector2.zero;
                content.offsetMax = new Vector2(0f, -(HeaderHeight + TabHeight));
            }

            if (_toast != null)
            {
                var toastRt = _toast.rectTransform;
                toastRt.anchorMin = new Vector2(0f, 0f);
                toastRt.anchorMax = new Vector2(1f, 0f);
                toastRt.pivot = new Vector2(0.5f, 0f);
                toastRt.offsetMin = new Vector2(16f, 16f);
                toastRt.offsetMax = new Vector2(-16f, 60f);
                _toast.color = CatTheme.Coral;
            }

            HighlightTab(ShellPanelId.Home);
        }

        void StyleTabRow(Transform nav)
        {
            _tabs.Clear();
            var ordered = new List<Transform>();
            for (var i = 0; i < nav.childCount; i++)
            {
                var child = nav.GetChild(i);
                if (IsSecondaryNav(child.name))
                {
                    child.gameObject.SetActive(false);
                    continue;
                }

                child.gameObject.SetActive(true);
                ordered.Add(child);
            }

            ordered.Sort((a, b) => TabOrder(a.name).CompareTo(TabOrder(b.name)));
            for (var i = 0; i < ordered.Count; i++)
            {
                var child = ordered[i];
                child.SetSiblingIndex(i);
                var element = child.GetComponent<LayoutElement>();
                if (element == null)
                    element = child.gameObject.AddComponent<LayoutElement>();
                element.preferredWidth = 112f;
                element.preferredHeight = TabHeight;
                element.flexibleWidth = 0f;
                element.flexibleHeight = 0f;
                CatTheme.ApplyPlate(child.GetComponent<Image>(), "tab");
                var label = child.Find("Label")?.GetComponent<Text>();
                if (label != null)
                {
                    label.text = TabLabel(child.name);
                    label.fontSize = 16;
                    label.color = CatTheme.Ink;
                }

                if (TabPanel(child.name, out var panel))
                {
                    var button = child.GetComponent<Button>();
                    if (button != null)
                        _tabs[panel] = button;
                }
            }
        }

        static void PlaceSettingsButton(Transform settings)
        {
            var rt = settings as RectTransform;
            if (rt == null)
                return;
            var element = settings.GetComponent<LayoutElement>();
            if (element != null)
                DestroyImmediate(element);
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-12f, -12f);
            rt.sizeDelta = new Vector2(40f, 40f);
            CatTheme.ApplyIcon(settings.GetComponent<Image>(), "icon-settings");
            var label = settings.Find("Label");
            if (label != null)
                label.gameObject.SetActive(false);
        }

        static bool IsSecondaryNav(string name)
        {
            return name == "世界地图" || name == "鱼获背包" || name == "鱼获/背包"
                || name == "动态" || name == "排行榜";
        }

        static int TabOrder(string name)
        {
            if (name == "主页")
                return 0;
            if (name == "鱼塘")
                return 1;
            if (name == "好友聊天" || name == "好友/聊天")
                return 2;
            if (name == "商店")
                return 3;
            if (name == "个人中心")
                return 4;
            return 8;
        }

        static string TabLabel(string name)
        {
            if (name == "主页")
                return "首页";
            if (name == "好友聊天" || name == "好友/聊天")
                return "好友";
            if (name == "个人中心")
                return "我的";
            return name;
        }

        static bool TabPanel(string name, out ShellPanelId panel)
        {
            switch (name)
            {
                case "主页":
                    panel = ShellPanelId.Home;
                    return true;
                case "鱼塘":
                    panel = ShellPanelId.Pond;
                    return true;
                case "好友聊天":
                case "好友/聊天":
                    panel = ShellPanelId.Friends;
                    return true;
                case "商店":
                    panel = ShellPanelId.Shop;
                    return true;
                case "个人中心":
                    panel = ShellPanelId.Profile;
                    return true;
                default:
                    panel = ShellPanelId.Home;
                    return false;
            }
        }

        void HighlightTab(ShellPanelId id)
        {
            var shown = id;
            if (id == ShellPanelId.WorldMap)
                shown = ShellPanelId.Pond;
            else if (id == ShellPanelId.Gallery || id == ShellPanelId.ProfileEdit
                || id == ShellPanelId.CatchBag || id == ShellPanelId.Leaderboard
                || id == ShellPanelId.SocialFeed)
                shown = ShellPanelId.Profile;
            else if (id == ShellPanelId.Settings)
                shown = (ShellPanelId)(-1);

            foreach (var pair in _tabs)
            {
                var on = pair.Key == shown;
                CatTheme.ApplyPlate(pair.Value.GetComponent<Image>(), on ? "tab-on" : "tab");
                var label = pair.Value.transform.Find("Label")?.GetComponent<Text>();
                if (label != null)
                    label.color = on ? CatTheme.Cream : CatTheme.Ink;
            }
        }

        static Transform FindDirect(Transform parent, string name)
        {
            if (parent == null)
                return null;
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child != null && child.name == name)
                    return child;
            }

            return null;
        }

        static void SetShown(Transform parent, string name, bool shown)
        {
            var node = parent != null ? parent.Find(name) : null;
            if (node != null)
                node.gameObject.SetActive(shown);
        }

        static void Paint(Transform parent, string name, Color color)
        {
            var text = parent != null ? parent.Find(name)?.GetComponent<Text>() : null;
            if (text != null)
                text.color = color;
        }

        static void PlaceHeaderLine(
            Transform header, string name, float x, float y, float width, float height, TextAnchor align)
        {
            var node = header.Find(name) as RectTransform;
            if (node == null)
                return;
            var right = align == TextAnchor.MiddleRight;
            node.anchorMin = new Vector2(right ? 1f : 0f, 1f);
            node.anchorMax = node.anchorMin;
            node.pivot = new Vector2(right ? 1f : 0f, 1f);
            node.anchoredPosition = new Vector2(x, -y);
            node.sizeDelta = new Vector2(width, height);
            var text = node.GetComponent<Text>();
            if (text == null)
                return;
            text.alignment = align;
            text.fontSize = 14;
            text.color = CatTheme.Cream;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
        }

        static void SetPreferred(Transform parent, string name, float width, float height)
        {
            var node = parent.Find(name);
            if (node == null)
                return;
            var element = node.GetComponent<LayoutElement>();
            if (element == null)
                element = node.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
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
            HighlightTab(id);
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
            var column = go.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(24, 24, 24, 24);
            column.spacing = 16f;
            column.childAlignment = TextAnchor.UpperCenter;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var title = CreateText(go.transform, "H1", "桌面宠物", 28, TextAnchor.MiddleCenter,
                Vector2.zero, new Vector2(400, 40));
            title.alignment = TextAnchor.MiddleCenter;
            SetPreferred(go.transform, "H1", 400f, 40f);

            var stage = new GameObject("PetStage", typeof(RectTransform), typeof(LayoutElement));
            stage.transform.SetParent(go.transform, false);
            var stageElement = stage.GetComponent<LayoutElement>();
            stageElement.minHeight = 160f;
            stageElement.preferredHeight = 280f;
            stageElement.flexibleHeight = 1f;
            var horizon = new GameObject("Horizon", typeof(RectTransform), typeof(Image));
            horizon.transform.SetParent(stage.transform, false);
            var horizonRt = horizon.GetComponent<RectTransform>();
            horizonRt.anchorMin = new Vector2(0.5f, 0f);
            horizonRt.anchorMax = new Vector2(0.5f, 0f);
            horizonRt.pivot = new Vector2(0.5f, 0f);
            horizonRt.anchoredPosition = new Vector2(0f, 28f);
            horizonRt.sizeDelta = new Vector2(360f, 8f);
            horizon.GetComponent<Image>().color = CatTheme.Teal;
            BindSquarePet(stage.transform, "HomePet", new Vector2(0f, -16f), 220f, true);

            _petStateLabel = CreateText(go.transform, "SessionState", "宠物状态：离线", 20,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(480, 32));
            _petStateLabel.alignment = TextAnchor.MiddleCenter;
            SetPreferred(go.transform, "SessionState", 480f, 32f);
            _petPond = CreateText(go.transform, "CurrentPond", "当前鱼塘：未进入", 16,
                TextAnchor.MiddleCenter, Vector2.zero, new Vector2(480, 28));
            _petPond.alignment = TextAnchor.MiddleCenter;
            SetPreferred(go.transform, "CurrentPond", 480f, 28f);

            var actions = CreateActionRow(go.transform, "HomeActions");
            CreateRowButton(actions.transform, "OpenPond", "进入鱼塘", new Vector2(200, 44), OpenPond, true);
            CreateRowButton(actions.transform, "SessionCheck", "验证当前会话", new Vector2(160, 40), ValidateCurrentSession);
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
            var column = go.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(24, 24, 24, 24);
            column.spacing = 16f;
            column.childAlignment = TextAnchor.UpperLeft;
            column.childControlWidth = true;
            column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;

            var title = CreateText(go.transform, "P1", "鱼塘", 28, TextAnchor.MiddleLeft,
                Vector2.zero, new Vector2(600, 40));
            title.alignment = TextAnchor.MiddleLeft;
            SetPreferred(go.transform, "P1", 600f, 40f);
            _pondStatus = CreateText(go.transform, "P2",
                "鱼塘场景在 960×560 Overlay 中渲染。本页只操作会话，打开主界面不会离塘。\n\n连接：未连接 · 当前 phase：—",
                16, TextAnchor.UpperLeft, Vector2.zero, new Vector2(640, 96));
            _pondStatus.alignment = TextAnchor.UpperLeft;
            _pondStatus.verticalOverflow = VerticalWrapMode.Truncate;
            SetPreferred(go.transform, "P2", 640f, 96f);

            var primary = CreateActionRow(go.transform, "PondActions");
            CreateRowButton(primary.transform, "SelectPond", "选塘", new Vector2(140, 44),
                () => ShowMainPanel(ShellPanelId.WorldMap));
            CreateRowButton(primary.transform, "EnterOverlay", "进入鱼塘", new Vector2(180, 44), OpenPond, true);

            var secondary = CreateActionRow(go.transform, "PondNav");
            CreateRowButton(secondary.transform, "ConnectPond", "连接并进塘", new Vector2(160, 44),
                () => _pondSession?.ConnectAndJoin());
            CreateRowButton(secondary.transform, "TakeSpot", "选择 1 号钓位", new Vector2(160, 44),
                () => _pondSession?.TakeFirstSpot(ShowSocketResult));
            CreateRowButton(secondary.transform, "StartFishing", "开始钓鱼", new Vector2(120, 44),
                () => _pondSession?.StartFishingAtFirstSpot(ShowSocketResult));
            CreateRowButton(secondary.transform, "StopFishing", "收杆", new Vector2(120, 44),
                () => _pondSession?.StopFishing(ShowSocketResult));
            CreateRowButton(secondary.transform, "ReturnHome", "返回主页", new Vector2(120, 44), ReturnToPetHome);
        }

        static GameObject CreateActionRow(Transform parent, string name)
        {
            var row = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var element = row.GetComponent<LayoutElement>();
            element.preferredHeight = 44f;
            element.minHeight = 44f;
            return row;
        }

        static void CreateRowButton(
            Transform parent, string name, string label, Vector2 size, UnityEngine.Events.UnityAction onClick, bool coral = false)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var element = go.GetComponent<LayoutElement>();
            element.preferredWidth = size.x;
            element.preferredHeight = size.y;
            CatTheme.ApplyPlate(go.GetComponent<Image>(), coral ? "btn-coral" : "btn-cream");
            var text = CreateText(go.transform, "Label", label, 16, TextAnchor.MiddleCenter, Vector2.zero, size);
            var tr = text.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
            tr.pivot = new Vector2(0.5f, 0.5f);
            text.alignment = TextAnchor.MiddleCenter;
            text.color = coral ? CatTheme.Cream : CatTheme.Ink;
            go.GetComponent<Button>().onClick.AddListener(onClick);
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
            if (enableDrag)
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
            text.color = CatTheme.Ink;
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
