using System;

namespace TMM
{
    public enum NotificationType { Info, Success, Warning, Error }

    public class NotificationItem
    {
        public string Message { get; set; } = "";
        public NotificationType Type { get; set; } = NotificationType.Info;
        public int DurationMs { get; set; } = 3500;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
