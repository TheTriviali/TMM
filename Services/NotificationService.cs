using System;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace TMM
{
    public static class NotificationService
    {
        private static readonly ObservableCollection<NotificationItem> _queue = new();
        private static readonly object _lockObj = new();

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

            lock (_lockObj)
            {
                _queue.Add(notification);
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                lock (_lockObj)
                {
                    _queue.Remove(notification);
                }
            };
            timer.Start();
        }

        public static void ShowSuccess(string message) => Show(message, NotificationType.Success);
        public static void ShowWarning(string message) => Show(message, NotificationType.Warning);
        public static void ShowError(string message) => Show(message, NotificationType.Error);
        public static void ShowInfo(string message) => Show(message, NotificationType.Info);
    }
}
