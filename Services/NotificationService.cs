using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace TMM
{
    public enum NotificationType { Info, Success, Warning, Error }

    public class NotificationItem
    {
        public string           Message    { get; set; } = "";
        public NotificationType Type       { get; set; } = NotificationType.Info;
        public int              DurationMs { get; set; } = 3500;
        public DateTime         CreatedAt  { get; set; } = DateTime.UtcNow;
    }

    public static class NotificationService
    {
        private static readonly ObservableCollection<NotificationItem> _queue = new();

        public static ObservableCollection<NotificationItem> Queue => _queue;

        public static void Show(string message, NotificationType type = NotificationType.Info, int durationMs = 3500)
        {
            var notification = new NotificationItem
            {
                Message = message,
                Type = type,
                DurationMs = durationMs,
                CreatedAt = DateTime.UtcNow
            };

            _queue.Add(notification);

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                _queue.Remove(notification);
            };
            timer.Start();
        }

        public static void ShowSuccess(string message) => Show(message, NotificationType.Success);
        public static void ShowWarning(string message) => Show(message, NotificationType.Warning);
        public static void ShowError(string message) => Show(message, NotificationType.Error);
        public static void ShowInfo(string message) => Show(message, NotificationType.Info);
    }
}
