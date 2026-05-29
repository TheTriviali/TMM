using System;
using System.Collections.Generic;
using System.Linq;

namespace TMM.Services
{
    /// <summary>
    /// Persists a rolling list of user actions (deploys, rollbacks, imports, loadout changes)
    /// for surfacing on the dashboard. Backed by AppSettings.RecentActivity so it survives restarts.
    /// </summary>
    public sealed class ActivityLogger
    {
        private const int MaxEntries = 20;
        private readonly BackendCore _core;

        public ActivityLogger(BackendCore core) { _core = core; }

        /// <summary>Snapshot of the activity feed, newest first.</summary>
        public IReadOnlyList<ActivityEntry> Recent =>
            _core.Settings.RecentActivity.OrderByDescending(a => a.Timestamp).ToList();

        public void Record(ActivityKind kind, string gameKey, string gameName, string detail, int count = 0)
        {
            var entry = new ActivityEntry
            {
                Timestamp = DateTime.Now,
                Kind = kind,
                GameKey = gameKey,
                GameName = gameName,
                Detail = detail,
                Count = count,
            };

            var list = _core.Settings.RecentActivity;
            list.Insert(0, entry);
            if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
            _core.SaveSettings();
        }

        public void Clear()
        {
            _core.Settings.RecentActivity.Clear();
            _core.SaveSettings();
        }
    }
}
