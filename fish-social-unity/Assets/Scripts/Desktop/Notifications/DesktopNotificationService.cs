using System;
using UnityEngine;

namespace FishSocial.Desktop
{
    public sealed class DesktopNotificationService : MonoBehaviour, INotificationService
    {
        public static DesktopNotificationService Instance { get; private set; }

        public NotificationSettings Settings { get; private set; } = new NotificationSettings();
        public event Action<DesktopNotification> NotificationReceived;
        DesktopNotification _lastNotification;
        float _notificationUntil;
        bool _hasVisibleNotification;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Settings = NotificationSettings.LoadOrDefault();
        }

        public void Publish(DesktopNotification notification)
        {
            if (!Settings.Allows(notification.Kind))
            {
                // Disabled notifications are intentionally silent.
                Debug.Log($"[Notify] suppressed ({notification.Kind}): {notification.Title}");
                return;
            }

            NotificationReceived?.Invoke(notification);
            _lastNotification = notification;
            _notificationUntil = Time.unscaledTime + 6f;
            _hasVisibleNotification = true;
            Debug.Log($"[Notify] {notification.Kind}: {notification.Title} — {notification.Body}");
        }

        void OnGUI()
        {
            if (!_hasVisibleNotification)
                return;
            if (Time.unscaledTime > _notificationUntil)
            {
                _hasVisibleNotification = false;
                return;
            }

            var box = new Rect(Screen.width - 430, 24, 390, 92);
            GUI.Box(box, string.Empty);
            GUI.Label(new Rect(box.x + 16, box.y + 12, box.width - 32, 26),
                _lastNotification.Title);
            GUI.Label(new Rect(box.x + 16, box.y + 42, box.width - 32, 38),
                _lastNotification.Body);
        }

        public void PublishSimulated(NotificationKind kind)
        {
            switch (kind)
            {
                case NotificationKind.FishBite:
                    Publish(new DesktopNotification(kind, "鱼咬钩了", "平静湖 · 钓点 3（模拟）"));
                    break;
                case NotificationKind.FriendInvite:
                    Publish(new DesktopNotification(kind, "好友邀请", "云中鹤 邀请你进塘（模拟）"));
                    break;
                case NotificationKind.ConnectionError:
                    Publish(new DesktopNotification(kind, "连接错误", "无法连接服务器（模拟，未接网络）"));
                    break;
            }
        }

        public void SaveSettings()
        {
            Settings.Save();
        }
    }
}
