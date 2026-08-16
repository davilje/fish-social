using UnityEngine;
using UnityEngine.UI;

namespace FishSocial.Desktop
{
    public sealed class DesktopSettingsModalView : MonoBehaviour
    {
        NotificationSettings _draft;

        public void Bind()
        {
            if (transform.childCount == 0)
                Build();
        }

        public void OnOpened()
        {
            _draft = Clone(DesktopNotificationService.Instance != null
                ? DesktopNotificationService.Instance.Settings
                : new NotificationSettings());
        }

        void Build()
        {
            float y = 0f;
            AddTitle("窗口模式", ref y);
            AddButton("普通窗口 1280×720", ref y, () =>
            {
                var window = WindowManager.Instance;
                if (window == null)
                    return;
                window.Settings.Width = WindowManager.MainDefaultWidth;
                window.Settings.Height = WindowManager.MainDefaultHeight;
                window.SetMode(WindowDisplayMode.Windowed);
            });
            AddButton("无边框", ref y, () => WindowManager.Instance?.SetMode(WindowDisplayMode.Borderless));
            AddButton("全屏", ref y, () => WindowManager.Instance?.SetMode(WindowDisplayMode.Fullscreen));

            AddTitle("托盘", ref y);
            AddToggle("关闭窗口时隐藏到托盘",
                WindowManager.Instance != null && WindowManager.Instance.Settings.HideToTrayOnClose,
                value =>
                {
                    if (WindowManager.Instance == null)
                        return;
                    WindowManager.Instance.Settings.HideToTrayOnClose = value;
                    WindowManager.Instance.Settings.Save();
                }, ref y);
            AddButton("立即隐藏到托盘", ref y, () => WindowManager.Instance?.HideToTray());

            AddTitle("通知", ref y);
            AddToggle("启用通知",
                DesktopNotificationService.Instance != null &&
                DesktopNotificationService.Instance.Settings.EnableNotifications,
                v => SetNotify(s => s.EnableNotifications = v), ref y);
            AddToggle("免打扰",
                DesktopNotificationService.Instance != null &&
                DesktopNotificationService.Instance.Settings.DoNotDisturb,
                v => SetNotify(s => s.DoNotDisturb = v), ref y);
            AddToggle("鱼咬钩通知",
                DesktopNotificationService.Instance != null &&
                DesktopNotificationService.Instance.Settings.EnableFishBite,
                v => SetNotify(s => s.EnableFishBite = v), ref y);
            AddToggle("好友邀请通知",
                DesktopNotificationService.Instance != null &&
                DesktopNotificationService.Instance.Settings.EnableFriendInvite,
                v => SetNotify(s => s.EnableFriendInvite = v), ref y);
            AddToggle("连接错误通知",
                DesktopNotificationService.Instance != null &&
                DesktopNotificationService.Instance.Settings.EnableConnectionError,
                v => SetNotify(s => s.EnableConnectionError = v), ref y);

            AddButton("保存窗口设置", ref y, () => WindowManager.Instance?.ApplySettings(persist: true));
            AddButton("模拟连接错误", ref y,
                () => DesktopNotificationService.Instance?.PublishSimulated(NotificationKind.ConnectionError));
            AddButton("退出游戏", ref y, () => DesktopAppBootstrap.Instance?.QuitForReal());
        }

        void AddTitle(string text, ref float y)
        {
            y -= 36f;
            var label = DesktopModalUi.Label(transform, text, text, 20, TextAnchor.MiddleLeft);
            Place(label.rectTransform, 0f, y, 400f, 28f);
        }

        void AddButton(string text, ref float y, UnityEngine.Events.UnityAction onClick)
        {
            y -= 44f;
            var button = DesktopModalUi.MakeButton(transform, text, text, onClick);
            Place(button.GetComponent<RectTransform>(), 0f, y, 280f, 36f);
        }

        Toggle AddToggle(string text, bool value, System.Action<bool> onChanged, ref float y)
        {
            y -= 36f;
            var go = new GameObject(text, typeof(RectTransform), typeof(Toggle));
            go.transform.SetParent(transform, false);
            Place(go.GetComponent<RectTransform>(), 0f, y, 420f, 28f);
            var bg = DesktopModalUi.Panel("Bg", go.transform, new Color(0.25f, 0.3f, 0.35f, 1f));
            var bgRt = bg.GetComponent<RectTransform>();
            bgRt.anchorMin = new Vector2(0f, 0.5f);
            bgRt.anchorMax = new Vector2(0f, 0.5f);
            bgRt.pivot = new Vector2(0f, 0.5f);
            bgRt.sizeDelta = new Vector2(22f, 22f);
            var check = DesktopModalUi.Panel("Check", bg.transform, new Color(0.4f, 0.85f, 0.55f, 1f));
            var checkRt = DesktopModalUi.Stretch(check);
            checkRt.offsetMin = new Vector2(4f, 4f);
            checkRt.offsetMax = new Vector2(-4f, -4f);
            var label = DesktopModalUi.Label(go.transform, "L", text, 16, TextAnchor.MiddleLeft);
            var labelRt = label.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(32f, 0f);
            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bg.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = value;
            toggle.onValueChanged.AddListener(v => onChanged?.Invoke(v));
            return toggle;
        }

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
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
