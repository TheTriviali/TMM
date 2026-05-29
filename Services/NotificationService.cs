using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Threading;
using TMM.Services;

namespace TMM
{
    public enum NotificationType { Info, Success, Warning, Error }

    public class NotificationItem
    {
        public string           Message    { get; set; } = "";
        public NotificationType Type       { get; set; } = NotificationType.Info;
        public int              DurationMs { get; set; } = 3500;
        public DateTime         CreatedAt  { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Originating subsystem (e.g. "Deploy", "Backup", "Import", "Settings"). Free-form
        /// label shown in the Notifications tab. Empty for generic/UI-level notifications.
        /// </summary>
        public string Source { get; set; } = "";
    }

    /// <summary>
    /// Two-tier notification surface:
    /// <list type="bullet">
    /// <item><b>Queue</b> — the transient toast queue. Items auto-expire after their
    /// <see cref="NotificationItem.DurationMs"/> and are bound by the toast host.</item>
    /// <item><b>History</b> — a persistent, newest-first ring (cap <see cref="HistoryCapacity"/>)
    /// the Notifications tab reads. A tail of <see cref="PersistTail"/> entries is mirrored to
    /// <c>%APPDATA%\TMM\notifications.json</c> so history survives restarts.</item>
    /// </list>
    /// Every <c>Show*</c> call records to history. <see cref="ShowVerbose"/> always records but
    /// only raises a toast when verbose mode is enabled (see <see cref="Initialize"/>).
    /// </summary>
    public static class NotificationService
    {
        public const int HistoryCapacity = 500;
        public const int PersistTail      = 200;

        private static readonly ObservableCollection<NotificationItem> _queue   = new();
        private static readonly ObservableCollection<NotificationItem> _history = new();
        private static readonly object _ioGate = new();

        private static string?      _persistPath;
        private static Func<bool>?  _verboseEnabled;

        /// <summary>Transient, auto-expiring toast queue.</summary>
        public static ObservableCollection<NotificationItem> Queue => _queue;

        /// <summary>Persistent, newest-first notification history (the Notifications tab binds here).</summary>
        public static ObservableCollection<NotificationItem> History => _history;

        /// <summary>True when verbose toasts are enabled (per <see cref="Settings.VerboseNotifications"/>).</summary>
        public static bool IsVerbose => _verboseEnabled?.Invoke() ?? false;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        };

        /// <summary>
        /// Wires the service to persistence and the live verbose setting. Call once at startup
        /// (after settings load). <paramref name="verboseEnabled"/> is queried live so a runtime
        /// settings toggle takes effect immediately — pass <c>() =&gt; core.Settings.VerboseNotifications</c>.
        /// </summary>
        public static void Initialize(string appDataPath, Func<bool> verboseEnabled)
        {
            _verboseEnabled = verboseEnabled;
            _persistPath = Path.Combine(appDataPath, "notifications.json");
            LoadHistory();
        }

        // ── Toast + history ────────────────────────────────────────────────────────

        public static void Show(string message, NotificationType type = NotificationType.Info,
                                int durationMs = 3500, string source = "")
        {
            var item = new NotificationItem
            {
                Message    = message,
                Type       = type,
                DurationMs = durationMs,
                CreatedAt  = DateTime.UtcNow,
                Source     = source
            };

            RecordHistory(item);
            RaiseToast(item);
        }

        public static void ShowSuccess(string message, string source = "") => Show(message, NotificationType.Success, source: source);
        public static void ShowWarning(string message, string source = "") => Show(message, NotificationType.Warning, source: source);
        public static void ShowError(string message,   string source = "") => Show(message, NotificationType.Error,   source: source);
        public static void ShowInfo(string message,    string source = "") => Show(message, NotificationType.Info,    source: source);

        /// <summary>
        /// Records a low-level/verbose event to history. Raises a toast only when verbose mode
        /// is enabled, so instrumenting chatty internals (folder creation, plan freeze, backup
        /// prune, import steps…) is free when the user hasn't opted in.
        /// </summary>
        public static void ShowVerbose(string message, string source, NotificationType type = NotificationType.Info)
        {
            var item = new NotificationItem
            {
                Message   = message,
                Type      = type,
                CreatedAt = DateTime.UtcNow,
                Source    = source
            };

            RecordHistory(item);
            if (IsVerbose) RaiseToast(item);
        }

        /// <summary>Empties the persistent history (does not touch live toasts).</summary>
        public static void ClearHistory()
        {
            OnUi(() => _history.Clear());
            SaveHistory();
        }

        // ── Internals ────────────────────────────────────────────────────────────────

        private static void RaiseToast(NotificationItem item)
        {
            OnUi(() =>
            {
                _queue.Add(item);
                var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(item.DurationMs) };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    _queue.Remove(item);
                };
                timer.Start();
            });
        }

        private static void RecordHistory(NotificationItem item)
        {
            OnUi(() =>
            {
                _history.Insert(0, item); // newest-first
                while (_history.Count > HistoryCapacity) _history.RemoveAt(_history.Count - 1);
            });
            SaveHistory();
        }

        private static void LoadHistory()
        {
            if (_persistPath is null || !File.Exists(_persistPath)) return;
            try
            {
                string json = File.ReadAllText(_persistPath);
                var items = JsonSerializer.Deserialize<List<NotificationItem>>(json, JsonOptions);
                if (items is null) return;

                OnUi(() =>
                {
                    _history.Clear();
                    foreach (var it in items.Take(HistoryCapacity)) _history.Add(it);
                });
            }
            catch (Exception ex)
            {
                Logger.Warn($"NotificationService: failed to load history — {ex.Message}");
            }
        }

        private static void SaveHistory()
        {
            if (_persistPath is null) return;
            // Snapshot the persisted tail on the current (UI) thread, then write off-thread-safe under a gate.
            List<NotificationItem> tail;
            lock (_ioGate)
            {
                tail = _history.Take(PersistTail).ToList();
            }
            try
            {
                lock (_ioGate)
                {
                    string json = JsonSerializer.Serialize(tail, JsonOptions);
                    File.WriteAllText(_persistPath, json);
                }
            }
            catch (Exception ex)
            {
                Logger.Warn($"NotificationService: failed to save history — {ex.Message}");
            }
        }

        /// <summary>Marshals to the UI dispatcher when needed so background-thread callers are safe.</summary>
        private static void OnUi(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher is null || dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }
    }
}
