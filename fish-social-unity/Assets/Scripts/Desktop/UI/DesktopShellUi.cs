using UnityEngine;
using UnityEngine.UI;
using FishSocial.Desktop.Auth;

namespace FishSocial.Desktop
{
    /// <summary>
    /// Runtime-built UGUI shell: home + four feature placeholders + toast notifications.
    /// </summary>
    public sealed class DesktopShellUi : MonoBehaviour
    {
        PanelRouter _router;
        Text _statusLogin;
        Text _statusConnection;
        Text _toast;
        float _toastUntil;
        NotificationSettings _notifyDraft;
        SteamAuthController _steamAuth;
        IAuthenticatedApiClient _authenticatedApi;
        SocialPondSessionController _pondSession;
        Text _pondStatus;
        Text _inventoryStatus;

        public void Build(PanelRouter router)
        {
            _router = router;
            _notifyDraft = DesktopNotificationService.Instance != null
                ? CloneNotify(DesktopNotificationService.Instance.Settings)
                : new NotificationSettings();
            _steamAuth = DesktopAppBootstrap.Instance != null
                ? DesktopAppBootstrap.Instance.SteamAuth
                : null;
            if (_steamAuth != null)
            {
                _authenticatedApi = _steamAuth.CreateAuthenticatedApiClient();
                _steamAuth.StateChanged += OnSteamStateChanged;
                _steamAuth.ErrorMessage += OnSteamError;
                OnSteamStateChanged(_steamAuth.State);
            }
            _pondSession = DesktopAppBootstrap.Instance != null
                ? DesktopAppBootstrap.Instance.PondSession
                : null;
            if (_pondSession != null)
            {
                _pondSession.StateChanged += OnPondStateChanged;
                _pondSession.SnapshotChanged += OnPondSnapshot;
                _pondSession.UserUpdated += OnPondUserUpdated;
                _pondSession.FishBiteReceived += OnPondFishBite;
                _pondSession.InventoryUpdated += OnInventoryUpdated;
                _pondSession.ErrorReceived += OnPondError;
                OnPondStateChanged(_pondSession.State, null);
            }

            var canvasGo = new GameObject("ShellCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.matchWidthOrHeight = 0.5f;

            EnsureEventSystem();

            var root = CreateStretchPanel("Root", canvasGo.transform, new Color(0.09f, 0.12f, 0.16f, 1f));
            var header = CreateBar("Header", root.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -64),
                new Color(0.12f, 0.18f, 0.24f, 1f));
            CreateText(header.transform, "Title", "Fish Social 桌面端", 28, TextAnchor.MiddleLeft,
                new Vector2(24, 0), new Vector2(480, 48));
            _statusLogin = CreateText(header.transform, "LoginStatus", "登录：未接入（占位）", 16, TextAnchor.MiddleRight,
                new Vector2(-280, 10), new Vector2(260, 24));
            _statusConnection = CreateText(header.transform, "ConnStatus", "连接：离线（占位）", 16, TextAnchor.MiddleRight,
                new Vector2(-280, -14), new Vector2(260, 24));

            var nav = CreateBar("Nav", root.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(220, 0),
                new Color(0.11f, 0.15f, 0.2f, 1f));
            var navLayout = nav.AddComponent<VerticalLayoutGroup>();
            navLayout.padding = new RectOffset(16, 16, 80, 16);
            navLayout.spacing = 10;
            navLayout.childAlignment = TextAnchor.UpperCenter;
            navLayout.childControlHeight = true;
            navLayout.childControlWidth = true;
            navLayout.childForceExpandHeight = false;
            navLayout.childForceExpandWidth = true;

            CreateNavButton(nav.transform, "鱼塘", () => _router.Show(ShellPanelId.Pond));
            CreateNavButton(nav.transform, "好友 / 聊天", () => _router.Show(ShellPanelId.Friends));
            CreateNavButton(nav.transform, "鱼获 / 背包", () => _router.Show(ShellPanelId.CatchBag));
            CreateNavButton(nav.transform, "设置", () => _router.Show(ShellPanelId.Settings));
            CreateNavButton(nav.transform, "主页", () => _router.Show(ShellPanelId.Home));

            var content = CreateBar("Content", root.transform, new Vector2(0, 0), new Vector2(1, 1), Vector2.zero,
                new Color(0.09f, 0.12f, 0.16f, 1f));
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.offsetMin = new Vector2(220, 0);
            contentRt.offsetMax = new Vector2(0, -64);

            RegisterPanel(content.transform, ShellPanelId.Home, BuildHome);
            RegisterPanel(content.transform, ShellPanelId.Pond, BuildPond);
            RegisterPanel(content.transform, ShellPanelId.Friends, BuildFriends);
            RegisterPanel(content.transform, ShellPanelId.CatchBag, BuildCatch);
            RegisterPanel(content.transform, ShellPanelId.Settings, BuildSettings);

            _toast = CreateText(root.transform, "Toast", string.Empty, 18, TextAnchor.LowerCenter,
                new Vector2(0, 48), new Vector2(720, 48));
            _toast.alignment = TextAnchor.MiddleCenter;
            _toast.gameObject.SetActive(false);
            _router.Show(ShellPanelId.Home);
        }

        void OnDestroy()
        {
            if (_steamAuth == null)
                return;
            _steamAuth.StateChanged -= OnSteamStateChanged;
            _steamAuth.ErrorMessage -= OnSteamError;
            if (_pondSession == null)
                return;
            _pondSession.StateChanged -= OnPondStateChanged;
            _pondSession.SnapshotChanged -= OnPondSnapshot;
            _pondSession.UserUpdated -= OnPondUserUpdated;
            _pondSession.FishBiteReceived -= OnPondFishBite;
            _pondSession.InventoryUpdated -= OnInventoryUpdated;
            _pondSession.ErrorReceived -= OnPondError;
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
            CreateText(go.transform, "H1", "欢迎回来", 36, TextAnchor.UpperLeft, new Vector2(32, -32), new Vector2(600, 48));
            CreateText(go.transform, "H2",
                "这是 Windows 桌面基础壳。Steam 身份登录和真实网络正在接入。\n请从左侧进入四个功能占位。",
                20, TextAnchor.UpperLeft, new Vector2(32, -100), new Vector2(900, 120));
            CreateButton(go.transform, "SteamLogin", "Steam 登录",
                new Vector2(32, -230), new Vector2(180, 46), BeginSteamLogin);
            CreateButton(go.transform, "SessionCheck", "验证当前会话",
                new Vector2(228, -230), new Vector2(200, 46), ValidateCurrentSession);
            CreateText(go.transform, "H3",
                "关闭窗口 → 隐藏到托盘（进程继续）\n托盘菜单可「显示窗口」或「退出游戏」",
                18, TextAnchor.UpperLeft, new Vector2(32, -305), new Vector2(900, 100));
            CreateText(go.transform, "H4", "通知测试（开启后右上角显示提示）", 18,
                TextAnchor.UpperLeft, new Vector2(32, -420), new Vector2(600, 32));
            CreateButton(go.transform, "HomeBite", "鱼咬钩", new Vector2(32, -465), new Vector2(150, 42),
                () => DesktopNotificationService.Instance?.PublishSimulated(NotificationKind.FishBite));
            CreateButton(go.transform, "HomeInvite", "好友邀请", new Vector2(195, -465), new Vector2(150, 42),
                () => DesktopNotificationService.Instance?.PublishSimulated(NotificationKind.FriendInvite));
            CreateButton(go.transform, "HomeError", "连接错误", new Vector2(358, -465), new Vector2(150, 42),
                () => DesktopNotificationService.Instance?.PublishSimulated(NotificationKind.ConnectionError));
        }

        void BeginSteamLogin()
        {
            if (_steamAuth == null)
            {
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
                if (ok)
                {
                    _statusLogin.text = "登录：会话验证成功";
                }
                ShowToast(message);
            }));
        }

        void OnSteamStateChanged(SteamLoginState state)
        {
            if (_statusLogin == null)
                return;
            switch (state)
            {
                case SteamLoginState.Initializing:
                    _statusLogin.text = "登录：初始化 Steam";
                    break;
                case SteamLoginState.RequestingTicket:
                    _statusLogin.text = "登录：获取 Steam Ticket";
                    break;
                case SteamLoginState.Authenticating:
                    _statusLogin.text = "登录：服务端验证中";
                    break;
                case SteamLoginState.Authenticated:
                    _statusLogin.text = "登录：Steam 已连接";
                    ShowToast("Steam 登录成功");
                    break;
                case SteamLoginState.Failed:
                    _statusLogin.text = "登录：失败";
                    break;
                default:
                    _statusLogin.text = "登录：未登录";
                    break;
            }
        }

        void OnSteamError(string message)
        {
            _statusLogin.text = "登录：失败";
            ShowToast(message);
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
        }

        void OnPondSnapshot(PondSnapshotDto snapshot)
        {
            if (_pondStatus == null)
                return;
            var count = snapshot?.users?.Length ?? 0;
            _pondStatus.text = "连接：在线 · 鱼塘用户：" + count +
                               "\n当前 phase：" + (_pondSession?.CurrentPhase ?? "idle");
        }

        void OnPondUserUpdated(PondUserDto user)
        {
            if (_pondStatus != null)
                _pondStatus.text = "连接：在线 · 当前 phase：" + (user?.fishingPhase ?? "idle") +
                                   "\n当前钓位：" + (user?.spotId ?? "未选择");
        }

        void OnPondFishBite(PendingFishCatchDto fishCatch)
        {
            ShowToast("收到服务端咬钩事件，请点击“领取鱼获”。");
        }

        void OnInventoryUpdated(FishInventoryItemDto[] items)
        {
            var count = items?.Length ?? 0;
            if (_inventoryStatus != null)
            {
                var text = "当前背包鱼获：" + count + " 条";
                var shown = Mathf.Min(count, 5);
                for (var i = 0; i < shown; i++)
                {
                    var item = items[i];
                    text += "\n" + (i + 1) + ". " + item.speciesId + " · " +
                            item.quality + " · " + item.sizeM.ToString("0.00") + "m";
                }
                _inventoryStatus.text = text;
            }
            ShowToast("背包已更新：" + count + " 条鱼获");
        }

        void OnPondError(string message)
        {
            ShowToast(message);
        }

        void ShowSocketResult(bool ok, string message)
        {
            ShowToast(message);
        }

        void BuildPond(GameObject go)
        {
            CreateText(go.transform, "P1", "鱼塘场景（占位）", 32, TextAnchor.UpperLeft, new Vector2(32, -32), new Vector2(600, 48));
            _pondStatus = CreateText(go.transform, "P2",
                "未来可替换为真实等距鱼塘场景，无需重做窗口壳。\n\n连接：未连接 · 当前 phase：—",
                20, TextAnchor.UpperLeft, new Vector2(32, -100), new Vector2(900, 160));
            CreateButton(go.transform, "ConnectPond", "连接并进塘", new Vector2(32, -280), new Vector2(180, 46),
                () => _pondSession?.ConnectAndJoin());
            CreateButton(go.transform, "TakeSpot", "选择 1 号钓位", new Vector2(228, -280), new Vector2(190, 46),
                () => _pondSession?.TakeFirstSpot(ShowSocketResult));
            CreateButton(go.transform, "StartFishing", "开始钓鱼", new Vector2(434, -280), new Vector2(150, 46),
                () => _pondSession?.StartFishingAtFirstSpot(ShowSocketResult));
            CreateButton(go.transform, "StopFishing", "收杆", new Vector2(600, -280), new Vector2(150, 46),
                () => _pondSession?.StopFishing(ShowSocketResult));
            CreateButton(go.transform, "AcceptCatch", "领取鱼获", new Vector2(766, -280), new Vector2(150, 46),
                () => _pondSession?.AcceptLatestCatch(ShowSocketResult));
        }

        void BuildFriends(GameObject go)
        {
            CreateText(go.transform, "F1", "好友 / 聊天（占位）", 32, TextAnchor.UpperLeft, new Vector2(32, -32), new Vector2(600, 48));
            CreateText(go.transform, "F2", "不接 Steam Lobby / 真实聊天。仅入口与通知边界。", 20,
                TextAnchor.UpperLeft, new Vector2(32, -100), new Vector2(900, 80));
            CreateButton(go.transform, "SimInvite", "模拟：好友邀请", new Vector2(32, -220), new Vector2(280, 48),
                () => DesktopNotificationService.Instance?.PublishSimulated(NotificationKind.FriendInvite));
        }

        void BuildCatch(GameObject go)
        {
            CreateText(go.transform, "C1", "鱼获 / 背包（占位）", 32, TextAnchor.UpperLeft, new Vector2(32, -32), new Vector2(600, 48));
            _inventoryStatus = CreateText(go.transform, "C2", "当前背包鱼获：尚未同步", 20,
                TextAnchor.UpperLeft, new Vector2(32, -100), new Vector2(900, 80));
        }

        void BuildSettings(GameObject go)
        {
            CreateText(go.transform, "S1", "设置", 32, TextAnchor.UpperLeft, new Vector2(32, -32), new Vector2(400, 48));

            float y = -100;
            CreateText(go.transform, "SM", "窗口模式", 22, TextAnchor.UpperLeft, new Vector2(32, y), new Vector2(200, 36));
            y -= 50;
            CreateButton(go.transform, "ModeWin", "普通窗口", new Vector2(32, y), new Vector2(160, 40),
                () => WindowManager.Instance?.SetMode(WindowDisplayMode.Windowed));
            CreateButton(go.transform, "ModeBorder", "无边框", new Vector2(210, y), new Vector2(160, 40),
                () => WindowManager.Instance?.SetMode(WindowDisplayMode.Borderless));
            CreateButton(go.transform, "ModeFull", "全屏", new Vector2(388, y), new Vector2(160, 40),
                () => WindowManager.Instance?.SetMode(WindowDisplayMode.Fullscreen));

            y -= 70;
            CreateText(go.transform, "SN", "通知", 22, TextAnchor.UpperLeft, new Vector2(32, y), new Vector2(200, 36));
            y -= 48;
            CreateToggle(go.transform, "EnAll", "启用通知", new Vector2(32, y), _notifyDraft.EnableNotifications,
                v => SetNotificationPreference(s => s.EnableNotifications = v));
            y -= 40;
            CreateToggle(go.transform, "Dnd", "免打扰", new Vector2(32, y), _notifyDraft.DoNotDisturb,
                v => SetNotificationPreference(s => s.DoNotDisturb = v));
            y -= 40;
            CreateToggle(go.transform, "Bite", "鱼咬钩", new Vector2(32, y), _notifyDraft.EnableFishBite,
                v => SetNotificationPreference(s => s.EnableFishBite = v));
            y -= 40;
            CreateToggle(go.transform, "Invite", "好友邀请", new Vector2(32, y), _notifyDraft.EnableFriendInvite,
                v => SetNotificationPreference(s => s.EnableFriendInvite = v));
            y -= 40;
            CreateToggle(go.transform, "Err", "连接错误", new Vector2(32, y), _notifyDraft.EnableConnectionError,
                v => SetNotificationPreference(s => s.EnableConnectionError = v));

            y -= 60;
            CreateButton(go.transform, "SavePref", "保存设置", new Vector2(32, y), new Vector2(180, 44), SavePrefs);
            CreateButton(go.transform, "SimErr", "模拟连接错误", new Vector2(230, y), new Vector2(200, 44),
                () => DesktopNotificationService.Instance?.PublishSimulated(NotificationKind.ConnectionError));
            CreateButton(go.transform, "HideTray", "隐藏到托盘", new Vector2(450, y), new Vector2(180, 44),
                () => WindowManager.Instance?.HideToTray());
            CreateButton(go.transform, "ExitGame", "退出游戏", new Vector2(650, y), new Vector2(160, 44),
                () => DesktopAppBootstrap.Instance?.QuitForReal());
        }

        void SavePrefs()
        {
            if (DesktopNotificationService.Instance != null)
            {
                var s = DesktopNotificationService.Instance.Settings;
                s.EnableNotifications = _notifyDraft.EnableNotifications;
                s.DoNotDisturb = _notifyDraft.DoNotDisturb;
                s.EnableFishBite = _notifyDraft.EnableFishBite;
                s.EnableFriendInvite = _notifyDraft.EnableFriendInvite;
                s.EnableConnectionError = _notifyDraft.EnableConnectionError;
                DesktopNotificationService.Instance.SaveSettings();
            }

            WindowManager.Instance?.ApplySettings(persist: true);
            ShowToast("设置已保存（仅本地窗口/通知偏好）");
        }

        void SetNotificationPreference(System.Action<NotificationSettings> change)
        {
            change(_notifyDraft);
            if (DesktopNotificationService.Instance == null)
                return;

            change(DesktopNotificationService.Instance.Settings);
            DesktopNotificationService.Instance.SaveSettings();
            ShowToast("通知设置已即时生效");
        }

        static NotificationSettings CloneNotify(NotificationSettings s)
        {
            return new NotificationSettings
            {
                EnableNotifications = s.EnableNotifications,
                DoNotDisturb = s.DoNotDisturb,
                EnableFishBite = s.EnableFishBite,
                EnableFriendInvite = s.EnableFriendInvite,
                EnableConnectionError = s.EnableConnectionError,
            };
        }

        static void EnsureEventSystem()
        {
            if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
                return;
            var es = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            DontDestroyOnLoad(es);
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
            rt.pivot = new Vector2(anchorMin.x < 1 && anchorMax.x > 0 ? 0.5f : anchorMin.x, anchorMin.y < 1 && anchorMax.y > 0 ? 0.5f : anchorMin.y);
            if (Mathf.Approximately(anchorMin.y, 1f) && Mathf.Approximately(anchorMax.y, 1f))
            {
                rt.pivot = new Vector2(0.5f, 1f);
                rt.sizeDelta = new Vector2(0, Mathf.Abs(sizeDelta.y));
                rt.anchoredPosition = Vector2.zero;
                rt.offsetMin = new Vector2(0, rt.offsetMin.y);
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
            text.verticalOverflow = VerticalWrapMode.Overflow;
            if (anchor == TextAnchor.MiddleLeft || anchor == TextAnchor.MiddleRight || anchor == TextAnchor.MiddleCenter)
            {
                rt.anchorMin = new Vector2(anchor == TextAnchor.MiddleRight ? 1 : 0, 1);
                rt.anchorMax = rt.anchorMin;
                rt.pivot = new Vector2(anchor == TextAnchor.MiddleRight ? 1 : 0, 0.5f);
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

        static void CreateNavButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = 44;
            go.GetComponent<Image>().color = new Color(0.18f, 0.28f, 0.36f, 1f);
            var text = CreateText(go.transform, "Label", label, 18, TextAnchor.MiddleLeft, new Vector2(12, -8), new Vector2(180, 28));
            text.alignment = TextAnchor.MiddleLeft;
            var tr = text.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(12, 0);
            tr.offsetMax = new Vector2(-8, 0);
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
