using System;

namespace TMM
{
    public enum ActivityKind { Deploy, Rollback, Import, LoadoutSaved, LoadoutApplied, ModAdded, ModRemoved }

    /// <summary>
    /// One row in the recent activity feed.
    /// Kept intentionally lightweight — last 20 are persisted in settings.json.
    /// </summary>
    public class ActivityEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public ActivityKind Kind { get; set; }
        public string GameKey { get; set; } = string.Empty;
        public string GameName { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
