using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using FishSocial.Desktop.Onboarding;

namespace FishSocial.Desktop
{
    public sealed class DesktopSettingsModalView : MonoBehaviour
    {
        NotificationSettings _draft;
        Toggle _hideToTrayToggle;
        Toggle _enableNotificationsToggle;
        Toggle _doNotDisturbToggle;
        Toggle _fishBiteToggle;
        Toggle _friendInviteToggle;
        Toggle _connectionErrorToggle;
        Text _serverUrlText;
        InputField _serverUrlInput;
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

            DesktopModalUi.BindDescendantButton(transform, "普通窗口 1280×720", ApplyWindowed);
            DesktopModalUi.BindDescendantButton(transform, "无边框",
                () => WindowManager.Instance?.SetMode(WindowDisplayMode.Borderless));
            DesktopModalUi.BindDescendantButton(transform, "全屏",
                () => WindowManager.Instance?.SetMode(WindowDisplayMode.Fullscreen));
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
            ValidatePrefabBindings();
        }

        public void OnOpened()
        {
            _draft = Clone(DesktopNotificationService.Instance != null
                ? DesktopNotificationService.Instance.Settings
                : new NotificationSettings());
            SyncControls();
            RefreshServerUrlControls();
        }

        void EnsureServerUrlControls()
        {
            _serverUrlText = DesktopModalUi.FindDescendantComponent<Text>(transform, "当前服务器");
            _serverUrlInput = DesktopModalUi.FindDescendantComponent<InputField>(
                transform, "服务器地址输入");
            _serverStatusText = DesktopModalUi.FindDescendantComponent<Text>(
                transform, "服务器连接状态");
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
            if (_serverStatusText != null && string.IsNullOrEmpty(_serverStatusText.text))
                _serverStatusText.text = "保存后请重启客户端；可用「测试服务器连接」检查 /health。";
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
                    var body = request.downloadHandler != null
                        ? request.downloadHandler.text
                        : string.Empty;
                    if (body != null && body.Length > 120)
                        body = body.Substring(0, 120) + "…";
                    SetServerStatus("连接成功（HTTP " + request.responseCode + "）：" + body, true);
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
                ? new Color(0.55f, 0.9f, 0.7f, 1f)
                : new Color(1f, 0.55f, 0.5f, 1f);
        }

        void ApplyWindowed()
        {
            var window = WindowManager.Instance;
            if (window == null)
                return;
            window.Settings.Width = WindowManager.MainDefaultWidth;
            window.Settings.Height = WindowManager.MainDefaultHeight;
            window.SetMode(WindowDisplayMode.Windowed);
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
        }

        void ValidatePrefabBindings()
        {
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
