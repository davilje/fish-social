namespace FishSocial.Desktop
{
    public enum NotificationKind
    {
        FishBite = 0,
        FriendInvite = 1,
        ConnectionError = 2,
        SystemWarning = 3,
    }

    public readonly struct DesktopNotification
    {
        public readonly NotificationKind Kind;
        public readonly string Title;
        public readonly string Body;

        public DesktopNotification(NotificationKind kind, string title, string body)
        {
            Kind = kind;
            Title = title;
            Body = body;
        }
    }

    public interface INotificationService
    {
        void Publish(DesktopNotification notification);
        void PublishSimulated(NotificationKind kind);
    }
}
