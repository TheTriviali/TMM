using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// ModManagerPage — batch wrappers over the existing single-mod operations.
    /// These are the backing methods for the bulk-action bar (multi-select).
    /// One refresh + one summary toast per batch; no per-item toasts.
    /// </summary>
    public partial class ModManagerPage
    {
        /// <summary>
        /// Enables all mods in <paramref name="mods"/> that are currently disabled.
        /// </summary>
        public void BatchEnable(IEnumerable<ModItem> mods)
        {
            var targets = mods.Where(m => !m.IsEnabled).ToList();
            if (targets.Count == 0) return;

            foreach (var m in targets)
            {
                m.IsEnabled = true;
                SyncModInfoToFolder(m);
            }

            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
            NotificationService.ShowSuccess($"Enabled {targets.Count} mod{(targets.Count == 1 ? "" : "s")}.");
        }

        /// <summary>
        /// Disables all mods in <paramref name="mods"/> that are currently enabled.
        /// </summary>
        public void BatchDisable(IEnumerable<ModItem> mods)
        {
            var targets = mods.Where(m => m.IsEnabled).ToList();
            if (targets.Count == 0) return;

            foreach (var m in targets)
            {
                m.IsEnabled = false;
                SyncModInfoToFolder(m);
            }

            _pendingCustom = true;
            UpdateDeployButtonCustom();
            SaveModsCustom();
            NotificationService.ShowSuccess($"Disabled {targets.Count} mod{(targets.Count == 1 ? "" : "s")}.");
        }

        /// <summary>
        /// Assigns <paramref name="groupName"/> to all mods in <paramref name="mods"/>.
        /// Pass null or empty string to clear the group.
        /// </summary>
        public void BatchSetGroup(IEnumerable<ModItem> mods, string? groupName)
        {
            var targets = mods.ToList();
            if (targets.Count == 0) return;

            string? normalized = string.IsNullOrWhiteSpace(groupName) ? null : groupName.Trim();
            foreach (var m in targets)
            {
                m.GroupName = normalized;
                SyncModInfoToFolder(m);
            }

            _pendingCustom = true;
            UpdateDeployButtonCustom();
            RefreshCustomView();
            SaveModsCustom();

            string desc = normalized is null ? "cleared group on" : $"set group '{normalized}' on";
            NotificationService.ShowSuccess($"Updated: {desc} {targets.Count} mod{(targets.Count == 1 ? "" : "s")}.");

            // Re-plan each mod that changed groups (group affects deployment routing).
            foreach (var m in targets)
                _ = ReplanGroupedModAsync(m);
        }

        /// <summary>
        /// Removes all mods in <paramref name="mods"/> from disk after user confirmation.
        /// </summary>
        public void BatchRemove(IEnumerable<ModItem> mods)
        {
            var targets = mods.ToList();
            if (targets.Count == 0) return;

            if (MessageBox.Show(
                    $"Delete {targets.Count} mod{(targets.Count == 1 ? "" : "s")}? This cannot be undone.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            int removed = 0;
            foreach (var m in targets)
            {
                try
                {
                    if (Directory.Exists(m.RawFolderPath))
                        BackendCore.ForceDeleteDirectory(m.RawFolderPath);
                    _modsCustom.Remove(m);
                    removed++;
                }
                catch (Exception ex)
                {
                    NotificationService.ShowError($"Error deleting '{m.Name}': {ex.Message}");
                }
            }

            SaveModsCustom();
            NotificationService.ShowSuccess($"Removed {removed} mod{(removed == 1 ? "" : "s")}.");
        }
    }
}
