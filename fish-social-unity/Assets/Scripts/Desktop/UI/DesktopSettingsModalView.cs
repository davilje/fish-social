using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

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
            if (_serverUrlInput != null)
                return;

            _serverUrlText = DesktopModalUi.FindDescendantComponent<Text>(transform, "当前服务器");
            if (_serverUrlText == null)
            {
                var labelGo = new GameObject("当前服务器", typeof(RectTransform), typeof(Text));
                labelGo.transform.SetParent(transform, false);
                var labelRt = labelGo.GetComponent<RectTransform>();
                labelRt.anchorMin = new Vector2(0f, 1f);
                labelRt.anchorMax = new Vector2(1f, 1f);
                labelRt.pivot = new Vector2(0.5f, 1f);
                labelRt.anchoredPosition = new Vector2(0f, -8f);
                labelRt.sizeDelta = new Vector2(-32f, 28f);
                _serverUrlText = labelGo.GetComponent<Text>();
                _serverUrlText.font = DesktopModalUi.Font;
                _serverUrlText.fontSize = 15;
                _serverUrlText.alignment = TextAnchor.UpperLeft;
                _serverUrlText.color = new Color(0.85f, 0.9f, 0.93f, 1f);
                _serverUrlText.raycastTarget = false;
            }

            _serverUrlInput = DesktopModalUi.FindDescendantComponent<InputField>(
                transform, "服务器地址输入");
            if (_serverUrlInput == null)
            {
                _serverUrlInput = DesktopModalUi.MakeInput(
                    transform, "服务器地址输入", "http://公网或局域网IP:3001", 256);
                var inputRt = _serverUrlInput.GetComponent<RectTransform>();
                inputRt.anchorMin = new Vector2(0f, 1f);
                inputRt.anchorMax = new Vector2(1f, 1f);
                inputRt.pivot = new Vector2(0.5f, 1f);
                inputRt.anchoredPosition = new Vector2(0f, -40f);
                inputRt.sizeDelta = new Vector2(-32f, 36f);
            }

            if (DesktopModalUi.FindDescendant(transform, "保存服务器地址") == null)
            {
                var save = DesktopModalUi.MakeButton(transform, "保存服务器地址", "保存服务器地址", SaveServerUrl);
                var saveRt = save.GetComponent<RectTransform>();
                saveRt.anchorMin = new Vector2(0f, 1f);
                saveRt.anchorMax = new Vector2(0f, 1f);
                saveRt.pivot = new Vector2(0f, 1f);
                saveRt.anchoredPosition = new Vector2(16f, -84f);
                saveRt.sizeDelta = new Vector2(160f, 36f);
            }
            else
            {
                DesktopModalUi.BindDescendantButton(transform, "保存服务器地址", SaveServerUrl);
            }

            if (DesktopModalUi.FindDescendant(transform, "测试服务器连接") == null)
            {
                var test = DesktopModalUi.MakeButton(transform, "测试服务器连接", "测试服务器连接", TestServerUrl);
                var testRt = test.GetComponent<RectTransform>();
                testRt.anchorMin = new Vector2(0f, 1f);
                testRt.anchorMax = new Vector2(0f, 1f);
                testRt.pivot = new Vector2(0f, 1f);
                testRt.anchoredPosition = new Vector2(188f, -84f);
                testRt.sizeDelta = new Vector2(160f, 36f);
            }
            else
            {
                DesktopModalUi.BindDescendantButton(transform, "测试服务器连接", TestServerUrl);
            }

            _serverStatusText = DesktopModalUi.FindDescendantComponent<Text>(transform, "服务器连接状态");
            if (_serverStatusText == null)
            {
                var statusGo = new GameObject("服务器连接状态", typeof(RectTransform), typeof(Text));
                statusGo.transform.SetParent(transform, false);
                var statusRt = statusGo.GetComponent<RectTransform>();
                statusRt.anchorMin = new Vector2(0f, 1f);
                statusRt.anchorMax = new Vector2(1f, 1f);
                statusRt.pivot = new Vector2(0.5f, 1f);
                statusRt.anchoredPosition = new Vector2(0f, -128f);
                statusRt.sizeDelta = new Vector2(-32f, 48f);
                _serverStatusText = statusGo.GetComponent<Text>();
                _serverStatusText.font = DesktopModalUi.Font;
                _serverStatusText.fontSize = 14;
                _serverStatusText.alignment = TextAnchor.UpperLeft;
                _serverStatusText.color = new Color(0.75f, 0.82f, 0.88f, 1f);
                _serverStatusText.raycastTarget = false;
            }

            RefreshServerUrlControls();
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
                _connectionErrorToggle == null)
                Debug.LogError("[DesktopUI] PanelSettings prefab is missing required toggles.");
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
