using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using FishSocial.Desktop.Onboarding;

namespace FishSocial.Desktop
{
    public sealed class DesktopSettingsModalView : MonoBehaviour
    {
        public static readonly string[] TabNames = { "Window", "Notify", "Server", "Other" };
        public static readonly string[] TabTitles = { "窗口显示", "通知偏好", "服务器连接", "托盘与其他" };
        public static readonly string[][] TabNodes =
        {
            new[] { "窗口模式", "1280×720", "1600×900", "1920×1080", "2560×1440", "DesignDisplayHeading", "无边框", "全屏" },
            new[] { "通知", "启用通知", "免打扰", "鱼咬钩通知", "好友邀请通知", "连接错误通知", "模拟连接错误" },
            new[] { "服务器", "当前服务器", "服务器地址输入", "保存服务器地址", "测试服务器连接", "服务器连接状态" },
            new[] { "托盘", "关闭窗口时隐藏到托盘", "立即隐藏到托盘", "重置新手引导", "退出游戏", "减少动效" },
        };

        NotificationSettings _draft;
        int _tab;
        Text _tabHeading;
        Text _tabInfo;
        Toggle _hideToTrayToggle;
        Toggle _enableNotificationsToggle;
        Toggle _doNotDisturbToggle;
        Toggle _fishBiteToggle;
        Toggle _friendInviteToggle;
        Toggle _connectionErrorToggle;
        Text _serverUrlText;
        InputField _serverUrlInput;
        Text _serverUrlHint;
        string _serverUrlHintText;
        Text _serverStatusText;
        Coroutine _healthRoutine;

        public void Bind()
        {
            _hideToTrayToggle = DesktopModalUi.FindDescendantComponent<Toggle>(
                transform, "关闭窗口时隐藏到托盘");
            _enableNotificationsToggle = DesktopModalUi.FindDescendantComponent<Toggle>(
                transform, "启用通知");
            _doNotDisturbToggle = DesktopModalUi.FindDescendantComponent<Toggle>(
                transform, "免打扰");
            _fishBiteToggle = DesktopModalUi.FindDescendantComponent<Toggle>(
                transform, "鱼咬钩通知");
            _friendInviteToggle = DesktopModalUi.FindDescendantComponent<Toggle>(
                transform, "好友邀请通知");
            _connectionErrorToggle = DesktopModalUi.FindDescendantComponent<Toggle>(
                transform, "连接错误通知");

            EnsureResolutionButtons();
            DesktopModalUi.BindDescendantButton(transform, "无边框", () =>
            {
                WindowManager.Instance?.SetMode(WindowDisplayMode.Borderless);
                RefreshWindowSelection();
                RefreshTabInfo();
            });
            DesktopModalUi.BindDescendantButton(transform, "全屏", () =>
            {
                WindowManager.Instance?.SetMode(WindowDisplayMode.Fullscreen);
                RefreshWindowSelection();
                RefreshTabInfo();
            });
            DesktopModalUi.BindDescendantToggle(transform, "关闭窗口时隐藏到托盘",
                WindowManager.Instance != null &&
                WindowManager.Instance.Settings.HideToTrayOnClose,
                value =>
                {
                    if (WindowManager.Instance == null)
                        return;
                    WindowManager.Instance.Settings.HideToTrayOnClose = value;
                    WindowManager.Instance.Settings.Save();
                });
            DesktopModalUi.BindDescendantButton(transform, "立即隐藏到托盘",
                () => WindowManager.Instance?.HideToTray());
            DesktopModalUi.BindDescendantToggle(transform, "启用通知",
                ReadNotification(s => s.EnableNotifications),
                value => SetNotify(s => s.EnableNotifications = value));
            DesktopModalUi.BindDescendantToggle(transform, "免打扰",
                ReadNotification(s => s.DoNotDisturb),
                value => SetNotify(s => s.DoNotDisturb = value));
            DesktopModalUi.BindDescendantToggle(transform, "鱼咬钩通知",
                ReadNotification(s => s.EnableFishBite),
                value => SetNotify(s => s.EnableFishBite = value));
            DesktopModalUi.BindDescendantToggle(transform, "好友邀请通知",
                ReadNotification(s => s.EnableFriendInvite),
                value => SetNotify(s => s.EnableFriendInvite = value));
            DesktopModalUi.BindDescendantToggle(transform, "连接错误通知",
                ReadNotification(s => s.EnableConnectionError),
                value => SetNotify(s => s.EnableConnectionError = value));
            DesktopModalUi.BindDescendantButton(transform, "保存窗口设置",
                () => WindowManager.Instance?.ApplySettings(persist: true));
            DesktopModalUi.BindDescendantButton(transform, "模拟连接错误",
                () => DesktopNotificationService.Instance?.PublishSimulated(
                    NotificationKind.ConnectionError));
            DesktopModalUi.BindDescendantButton(transform, "退出游戏",
                () => DesktopAppBootstrap.Instance?.QuitForReal());
            EnsureServerUrlControls();
            EnsureResetOnboardingButton();
            BindTabs();
            ValidatePrefabBindings();
            DesktopModalUi.StyleTree(transform);
            EnsureReduceMotionToggle();
            ShowTab(_tab);
        }

        void EnsureReduceMotionToggle()
        {
            CozyMotion.LoadPreference();
            var existing = DesktopModalUi.FindDescendant(transform, "减少动效");
            var copy = existing != null ? existing.gameObject : null;
            if (copy == null)
            {
                // Compatibility for older prefabs. Authored journal prefabs own this control.
                var source = DesktopModalUi.FindDescendant(transform, "关闭窗口时隐藏到托盘");
                if (source == null)
                    return;
                copy = Instantiate(source.gameObject, source.parent);
                copy.name = "减少动效";
                var rect = copy.GetComponent<RectTransform>();
                var exit = DesktopModalUi.FindDescendant(transform, "退出游戏") as RectTransform;
                if (rect != null && exit != null)
                {
                    rect.anchoredPosition = new Vector2(exit.anchoredPosition.x + exit.rect.width + 16f, exit.anchoredPosition.y);
                    rect.sizeDelta = new Vector2(310f, exit.rect.height);
                }
            }
            var label = DesktopModalUi.FindComponent<Text>(copy.transform, "Label");
            if (label == null)
                label = copy.GetComponentInChildren<Text>();
            if (label != null)
                label.text = "减少动效";
            var toggle = copy.GetComponent<Toggle>();
            if (toggle == null)
                toggle = copy.GetComponentInChildren<Toggle>();
            if (toggle == null)
                return;
            toggle.onValueChanged = new Toggle.ToggleEvent();
            toggle.SetIsOnWithoutNotify(CozyMotion.Reduced);
            toggle.onValueChanged.AddListener(CozyMotion.SetReduced);
            copy.SetActive(false);
        }

        public void OnOpened()
        {
            _draft = Clone(DesktopNotificationService.Instance != null
                ? DesktopNotificationService.Instance.Settings
                : new NotificationSettings());
            SyncControls();
            RefreshServerUrlControls();
            ShowTab(_tab);
        }

        void BindTabs()
        {
            _tabHeading = DesktopModalUi.FindDescendantComponent<Text>(transform, "DesignSettingsHeading");
            _tabInfo = DesktopModalUi.FindComponent<Text>(transform, "DesignSettingsInfo/Body");
            for (var i = 0; i < TabNames.Length; i++)
            {
                var index = i;
                if (!DesktopModalUi.BindButton(transform, "SettingsTabs/" + TabNames[i], () => ShowTab(index)))
                    Debug.LogError("[DesktopUI] PanelSettings requires Button: SettingsTabs/" + TabNames[i], this);
            }
            if (!Debug.isDebugBuild)
            {
                var serverTab = DesktopModalUi.FindDescendant(transform, "Server");
                if (serverTab != null)
                    serverTab.gameObject.SetActive(false);
            }
            var saveWindow = DesktopModalUi.FindDescendant(transform, "保存窗口设置");
            if (saveWindow != null)
                saveWindow.gameObject.SetActive(false);
        }

        void RefreshWindowSelection()
        {
            var window = WindowManager.Instance != null ? WindowManager.Instance.Settings : null;
            var presets = WindowSettings.Presets;
            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var selected = window != null && window.Mode == WindowDisplayMode.Windowed &&
                    window.Width == preset.Width && window.Height == preset.Height;
                DesktopModalUi.FindDescendantComponent<CozyButtonVisual>(transform, preset.Label)?.SetSelected(selected);
            }
            DesktopModalUi.FindDescendantComponent<CozyButtonVisual>(transform, "无边框")
                ?.SetSelected(window != null && window.Mode == WindowDisplayMode.Borderless);
            DesktopModalUi.FindDescendantComponent<CozyButtonVisual>(transform, "全屏")
                ?.SetSelected(window != null && window.Mode == WindowDisplayMode.Fullscreen);
            var note = DesktopModalUi.FindDescendantComponent<Text>(transform, "DesignSettingsNote");
            if (note == null || window == null)
                return;
            var current = window.Mode == WindowDisplayMode.Fullscreen ? "全屏"
                : window.Mode == WindowDisplayMode.Borderless ? "无边框"
                : window.Width + "×" + window.Height;
            note.text = "当前 " + current + "\n切换后立即保存";
        }

        void ShowTab(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, TabNames.Length - 1);
            for (var i = 0; i < TabNodes.Length; i++)
                foreach (var name in TabNodes[i])
                {
                    var node = DesktopModalUi.FindDescendant(transform, name);
                    if (node != null)
                        node.gameObject.SetActive(i == _tab);
                }
            for (var i = 0; i < TabNames.Length; i++)
                DesktopModalUi.FindComponent<CozyButtonVisual>(transform, "SettingsTabs/" + TabNames[i])
                    ?.SetSelected(i == _tab);
            if (_tabHeading != null)
                _tabHeading.text = TabTitles[_tab];
            var hint = DesktopModalUi.FindDescendant(transform, "DesignSettingsHint");
            if (hint != null)
                hint.gameObject.SetActive(_tab == 0);
            var simulate = DesktopModalUi.FindDescendant(transform, "模拟连接错误");
            if (simulate != null && !Debug.isDebugBuild)
                simulate.gameObject.SetActive(false);
            SyncServerPlaceholder();
            RefreshWindowSelection();
            RefreshTabInfo();
        }

        void RefreshTabInfo()
        {
            if (_tabInfo == null)
                return;
            switch (_tab)
            {
                case 0:
                    var window = WindowManager.Instance != null ? WindowManager.Instance.Settings : null;
                    var current = window == null ? "—" : window.Mode == WindowDisplayMode.Fullscreen ? "全屏"
                        : window.Mode == WindowDisplayMode.Borderless ? "无边框" : window.Width + "×" + window.Height + " 窗口";
                    _tabInfo.text = "当前：" + current +
                        "\n\n选择分辨率后窗口立即调整，仅支持四种常用尺寸。\n\n无边框与全屏可随时切换。\n\n点击「保存窗口设置」，下次启动沿用当前选择。";
                    break;
                case 1:
                    _tabInfo.text = "关闭「启用通知」后，所有桌面提醒都会静音。\n\n免打扰期间仍会记录消息，但不会弹出提示。\n\n「模拟连接错误」用于检查提醒是否正常。";
                    break;
                case 2:
                    _tabInfo.text = "填写服务器地址并保存，重启客户端后生效。\n\n保存前可先「测试服务器连接」，检查 /health 是否可达。";
                    break;
                default:
                    _tabInfo.text = "开启「关闭窗口时隐藏到托盘」后，关闭窗口不会退出游戏，猫咪会继续钓鱼。\n\n「重置新手引导」会重新播放一次教学。";
                    break;
            }
        }

        void EnsureServerUrlControls()
        {
            _serverUrlText = DesktopModalUi.FindDescendantComponent<Text>(transform, "当前服务器");
            _serverUrlInput = DesktopModalUi.FindDescendantComponent<InputField>(
                transform, "服务器地址输入");
            _serverStatusText = DesktopModalUi.FindDescendantComponent<Text>(
                transform, "服务器连接状态");
            if (_serverUrlInput != null)
            {
                _serverUrlInput.onValueChanged.RemoveListener(OnServerUrlEdited);
                _serverUrlInput.onValueChanged.AddListener(OnServerUrlEdited);
            }
            DesktopModalUi.BindDescendantButton(transform, "保存服务器地址", SaveServerUrl);
            DesktopModalUi.BindDescendantButton(transform, "测试服务器连接", TestServerUrl);
            RefreshServerUrlControls();
        }

        void EnsureResetOnboardingButton()
        {
            DesktopModalUi.BindDescendantButton(transform, "重置新手引导", OnResetOnboardingClicked);
        }

        void OnResetOnboardingClicked()
        {
            var onboarding = DesktopOnboardingController.Instance;
            if (onboarding == null)
            {
                SetServerStatus("新手引导未就绪。", false);
                return;
            }

            SetServerStatus("正在重置新手引导…", true);
            onboarding.ResetAndRestartOnboarding();
        }

        void RefreshServerUrlControls()
        {
            var url = DesktopAppBootstrap.Instance != null
                ? DesktopAppBootstrap.Instance.ServerBaseUrl
                : DesktopServerConfig.DefaultServerBaseUrl;
            if (_serverUrlText != null)
                _serverUrlText.text = "当前服务器：" + url;
            if (_serverUrlInput != null)
                _serverUrlInput.text = url;
            SyncServerPlaceholder();
            if (_serverStatusText != null && string.IsNullOrEmpty(_serverStatusText.text))
                _serverStatusText.text = "保存后请重启客户端；可用「测试服务器连接」检查 /health。";
        }

        void LateUpdate()
        {
            if (_serverUrlInput != null && _serverUrlInput.gameObject.activeInHierarchy)
                SyncServerPlaceholder();
        }

        void OnServerUrlEdited(string _)
        {
            SyncServerPlaceholder();
        }

        void SyncServerPlaceholder()
        {
            if (_serverUrlInput == null)
                return;
            if (_serverUrlHint == null)
            {
                _serverUrlHint = _serverUrlInput.placeholder as Text;
                if (_serverUrlHint == null)
                    _serverUrlHint = _serverUrlInput.transform.Find("Placeholder")?.GetComponent<Text>();
                if (_serverUrlHint != null && _serverUrlInput.placeholder == null)
                    _serverUrlInput.placeholder = _serverUrlHint;
            }
            if (_serverUrlHint == null || _serverUrlHint == _serverUrlInput.textComponent)
                return;
            if (_serverUrlHintText == null)
                _serverUrlHintText = _serverUrlHint.text ?? string.Empty;
            var showHint = string.IsNullOrEmpty(_serverUrlInput.text);
            _serverUrlHint.text = showHint ? _serverUrlHintText : string.Empty;
            if (_serverUrlHint.gameObject.activeSelf != showHint)
                _serverUrlHint.gameObject.SetActive(showHint);
        }

        void SaveServerUrl()
        {
            var raw = _serverUrlInput != null ? _serverUrlInput.text : string.Empty;
            if (!DesktopServerConfig.TryWriteServerBaseUrl(raw, out var normalized, out var error))
            {
                SetServerStatus(error ?? "保存失败。", false);
                return;
            }

            if (_serverUrlInput != null)
                _serverUrlInput.text = normalized;
            SetServerStatus(
                "已写入 server.json（" + normalized + "）。请重启客户端后生效。",
                true);
            Debug.Log("[DesktopShell] server.json updated to " + normalized);
        }

        void TestServerUrl()
        {
            var raw = _serverUrlInput != null ? _serverUrlInput.text : string.Empty;
            if (!DesktopServerConfig.TryNormalize(raw, out var normalized))
            {
                SetServerStatus("服务器地址无效，无法测试。", false);
                return;
            }

            if (_healthRoutine != null)
                StopCoroutine(_healthRoutine);
            _healthRoutine = StartCoroutine(HealthCheckRoutine(normalized));
        }

        IEnumerator HealthCheckRoutine(string serverBaseUrl)
        {
            SetServerStatus("正在测试 " + serverBaseUrl + "/health …", true);
            var url = serverBaseUrl.TrimEnd('/') + "/health";
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 8;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success &&
                    request.responseCode >= 200 && request.responseCode < 300)
                {
                    SetServerStatus("连接正常。", true);
                }
                else
                {
                    var detail = request.error;
                    if (string.IsNullOrEmpty(detail))
                        detail = "HTTP " + request.responseCode;
                    SetServerStatus("连接失败：" + detail, false);
                }
            }

            _healthRoutine = null;
        }

        void SetServerStatus(string message, bool ok)
        {
            if (_serverStatusText == null)
                return;
            _serverStatusText.text = message ?? string.Empty;
            _serverStatusText.color = ok
                ? CatTheme.Ink
                : new Color(0.65f, 0.29f, 0.18f, 1f);
        }

        void EnsureResolutionButtons()
        {
            const string legacyName = "普通窗口 1280×720";
            var presets = WindowSettings.Presets;
            var template = DesktopModalUi.FindDescendant(transform, legacyName);
            if (template == null && presets.Length > 0)
                template = DesktopModalUi.FindDescendant(transform, presets[0].Label);
            if (template == null)
            {
                Debug.LogError("[DesktopUI] PanelSettings requires Button: " + legacyName, this);
                return;
            }

            var templateRt = template.GetComponent<RectTransform>();
            var origin = templateRt.anchoredPosition;
            var sourceWidth = templateRt.sizeDelta.x;
            var sourceHeight = templateRt.sizeDelta.y;
            var alreadySplit = presets.Length > 1 &&
                DesktopModalUi.FindDescendant(transform, presets[presets.Length - 1].Label) != null;
            var gap = 8f;
            var buttonWidth = alreadySplit
                ? sourceWidth
                : (sourceWidth - gap * (presets.Length - 1)) / presets.Length;

            for (var i = 0; i < presets.Length; i++)
            {
                var preset = presets[i];
                var label = preset.Label;
                var node = DesktopModalUi.FindDescendant(transform, label);
                if (node == null)
                {
                    if (i == 0)
                    {
                        template.gameObject.name = label;
                        node = template;
                    }
                    else
                    {
                        var clone = Instantiate(template.gameObject, template.parent);
                        clone.name = label;
                        node = clone.transform;
                    }
                }

                if (!alreadySplit)
                {
                    var rt = node.GetComponent<RectTransform>();
                    rt.anchoredPosition = new Vector2(origin.x + i * (buttonWidth + gap), origin.y);
                    rt.sizeDelta = new Vector2(buttonWidth, sourceHeight);
                }

                var text = node.GetComponentInChildren<Text>(true);
                if (text != null)
                {
                    text.text = label;
                    text.alignment = TextAnchor.MiddleCenter;
                    text.resizeTextForBestFit = true;
                    text.resizeTextMinSize = 12;
                    text.resizeTextMaxSize = 18;
                }

                var width = preset.Width;
                var height = preset.Height;
                DesktopModalUi.BindDescendantButton(transform, label, () =>
                {
                    WindowManager.Instance?.ApplyWindowedPreset(width, height);
                    RefreshWindowSelection();
                    RefreshTabInfo();
                });
            }
        }

        bool ReadNotification(System.Func<NotificationSettings, bool> read)
        {
            var settings = DesktopNotificationService.Instance != null
                ? DesktopNotificationService.Instance.Settings
                : new NotificationSettings();
            return read(settings);
        }

        void SyncControls()
        {
            var window = WindowManager.Instance;
            if (_hideToTrayToggle != null)
                _hideToTrayToggle.SetIsOnWithoutNotify(
                    window != null && window.Settings.HideToTrayOnClose);
            var settings = DesktopNotificationService.Instance != null
                ? DesktopNotificationService.Instance.Settings
                : new NotificationSettings();
            _enableNotificationsToggle?.SetIsOnWithoutNotify(settings.EnableNotifications);
            _doNotDisturbToggle?.SetIsOnWithoutNotify(settings.DoNotDisturb);
            _fishBiteToggle?.SetIsOnWithoutNotify(settings.EnableFishBite);
            _friendInviteToggle?.SetIsOnWithoutNotify(settings.EnableFriendInvite);
            _connectionErrorToggle?.SetIsOnWithoutNotify(settings.EnableConnectionError);
            DesktopModalUi.FindDescendantComponent<Toggle>(transform, "减少动效")
                ?.SetIsOnWithoutNotify(CozyMotion.Reduced);
        }

        void ValidatePrefabBindings()
        {
            foreach (var name in new[] { "1280×720", "1600×900", "1920×1080", "2560×1440", "无边框", "全屏", "立即隐藏到托盘",
                "保存窗口设置", "模拟连接错误", "退出游戏", "保存服务器地址", "测试服务器连接", "重置新手引导" })
            {
                if (name == "模拟连接错误" && !Debug.isDebugBuild)
                    continue;
                if (DesktopModalUi.FindDescendantComponent<Button>(transform, name) == null)
                    Debug.LogError("[DesktopUI] PanelSettings requires Button: " + name, this);
            }
            if (_hideToTrayToggle == null ||
                _enableNotificationsToggle == null ||
                _doNotDisturbToggle == null ||
                _fishBiteToggle == null ||
                _friendInviteToggle == null ||
                _connectionErrorToggle == null ||
                _serverUrlText == null ||
                _serverUrlInput == null ||
                _serverStatusText == null ||
                DesktopModalUi.FindDescendant(transform, "保存服务器地址") == null ||
                DesktopModalUi.FindDescendant(transform, "测试服务器连接") == null ||
                DesktopModalUi.FindDescendant(transform, "重置新手引导") == null)
                Debug.LogError("[DesktopUI] PanelSettings prefab is missing required settings controls.");
        }

        void SetNotify(System.Action<NotificationSettings> change)
        {
            if (_draft == null)
                _draft = new NotificationSettings();
            change(_draft);
            if (DesktopNotificationService.Instance == null)
                return;
            change(DesktopNotificationService.Instance.Settings);
            DesktopNotificationService.Instance.SaveSettings();
        }

        static NotificationSettings Clone(NotificationSettings s)
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
    }
}
