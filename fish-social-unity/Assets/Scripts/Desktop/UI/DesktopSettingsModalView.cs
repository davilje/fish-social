using UnityEngine;
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
            ValidatePrefabBindings();
        }

        public void OnOpened()
        {
            _draft = Clone(DesktopNotificationService.Instance != null
                ? DesktopNotificationService.Instance.Settings
                : new NotificationSettings());
            SyncControls();
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
