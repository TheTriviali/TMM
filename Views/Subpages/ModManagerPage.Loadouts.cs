using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// ModManagerPage — loadout menu and flows: save, apply, rename, delete,
    /// import/export .tmmpack (Block D).
    /// </summary>
    public partial class ModManagerPage
    {
        private void BtnLoadouts_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu();

            var saveItem = new MenuItem { Header = "Save Current Loadout..." };
            saveItem.Click += async (_, _) => await SaveLoadoutFlow();
            menu.Items.Add(saveItem);

            var importItem = new MenuItem { Header = "Import .tmmpack..." };
            importItem.Click += async (_, _) => await ImportPackFlow();
            menu.Items.Add(importItem);

            var loadouts = _core.ListLoadouts(_customProfile.Key).OrderBy(n => n).ToList();
            if (loadouts.Count >= 2)
            {
                var diffItem = new MenuItem { Header = "Compare Loadouts..." };
                diffItem.Click += (_, _) =>
                    new LoadoutDiffWindow(_core, _customProfile.Key, loadouts) { Owner = Window.GetWindow(this) }.ShowDialog();
                menu.Items.Add(diffItem);
            }

            if (loadouts.Count > 0)
            {
                menu.Items.Add(new Separator());
                foreach (var name in loadouts)
                {
                    var item = new MenuItem { Header = name };

                    var applyItem = new MenuItem { Header = "Apply" };
                    applyItem.Click += async (_, _) =>
                    {
                        try
                        {
                            await _core.ApplyLoadoutAsync(_customProfile.Key, name, _modsCustom);
                            SaveModsCustom();
                            _core.Activity.Record(ActivityKind.LoadoutApplied, _customProfile.Key, _customConfig.GameName, $"Applied '{name}'");
                            NotificationService.ShowSuccess($"Applied loadout '{name}'", "Loadouts");
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Apply loadout '{name}' failed", ex);
                            NotificationService.ShowError($"Could not apply loadout '{name}': {ex.Message}", "Loadouts", "TMM-E011");
                        }
                    };
                    item.Items.Add(applyItem);

                    var renameItem = new MenuItem { Header = "Rename..." };
                    renameItem.Click += (_, _) => RenameLoadoutFlow(name);
                    item.Items.Add(renameItem);

                    var exportItem = new MenuItem { Header = "Export as .tmmpack..." };
                    exportItem.Click += async (_, _) => await ExportLoadoutFlow(name);
                    item.Items.Add(exportItem);

                    item.Items.Add(new Separator());

                    var deleteItem = new MenuItem { Header = "Delete", Foreground = Brushes.IndianRed };
                    deleteItem.Click += (_, _) => DeleteLoadoutFlow(name);
                    item.Items.Add(deleteItem);

                    menu.Items.Add(item);
                }
            }

            menu.PlacementTarget = sender as Button;
            menu.IsOpen = true;
        }

        private async Task SaveLoadoutFlow()
        {
            var dlg = new RenameWindow("", "Save Loadout", "Name:") { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.NewName)) return;

            string name = dlg.NewName.Trim();
            if (!BackendCore.IsValidLoadoutName(name))
            {
                NotificationService.ShowWarning("Loadout name can't contain \\ / : * ? \" < > |");
                return;
            }
            if (_core.LoadoutExists(_customProfile.Key, name))
            {
                var result = MessageBox.Show($"A loadout named '{name}' already exists. Overwrite?",
                    "Confirm Overwrite", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }

            try
            {
                await _core.SaveLoadoutAsync(_customProfile.Key, name, _modsCustom);
                _core.Activity.Record(ActivityKind.LoadoutSaved, _customProfile.Key, _customConfig.GameName, $"Saved '{name}'", _modsCustom.Count);
                NotificationService.ShowSuccess($"Saved loadout '{name}'", "Loadouts");
            }
            catch (IOException ex)
            {
                Logger.Error($"Save loadout '{name}' failed (IO)", ex);
                NotificationService.ShowError($"Could not save loadout '{name}': {ex.Message}", "Loadouts", "TMM-E010");
            }
            catch (Exception ex)
            {
                Logger.Error($"Save loadout '{name}' failed", ex);
                NotificationService.ShowError($"Could not save loadout '{name}': {ex.Message}", "Loadouts", "TMM-E010");
            }
        }

        private async Task ImportPackFlow()
        {
            var ofd = new OpenFileDialog
            {
                Title  = "Import .tmmpack",
                Filter = "TMM pack (*.tmmpack)|*.tmmpack|All files (*.*)|*.*",
            };
            if (ofd.ShowDialog() != true) return;

            TmmPackBuilder.Manifest manifest;
            try
            {
                manifest = TmmPackInstaller.ReadManifest(ofd.FileName);
            }
            catch (Exception ex)
            {
                NotificationService.ShowError($"Couldn't read pack: {ex.Message}", "Loadouts", "TMM-E010");
                return;
            }

            var confirm = MessageBox.Show(
                $"This pack was made for '{manifest.GameName}' and contains {manifest.ModNames.Count} mod(s) " +
                $"(loadout '{manifest.LoadoutName}').\n\nImport into '{_customConfig.GameName}'?",
                "Import .tmmpack", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            ShowDeployOverlay("Importing pack...");
            try
            {
                var result = await TmmPackInstaller.ImportAsync(_core, ofd.FileName, _customProfile.Key);
                _core.Activity.Record(ActivityKind.Import, _customProfile.Key, _customConfig.GameName,
                    $"Imported pack '{result.LoadoutName}'", result.ModsImported);
                string renamedNote = result.RenamedMods.Count > 0
                    ? $" ({result.RenamedMods.Count} renamed to avoid collisions)" : "";
                NotificationService.ShowSuccess(
                    $"Imported {result.ModsImported} mod(s){renamedNote}. Loadout: '{result.LoadoutName}'", "Loadouts");
            }
            catch (IOException ex)
            {
                Logger.Error("Pack import failed (IO)", ex);
                NotificationService.ShowError($"Import failed: {ex.Message}", "Loadouts", "TMM-E010");
            }
            catch (Exception ex)
            {
                Logger.Error("Pack import failed", ex);
                NotificationService.ShowError($"Import failed: {ex.Message}", "Loadouts", "TMM-E010");
            }
            finally
            {
                HideDeployOverlay();
                await RefreshCustomAsync();
            }
        }

        private void RenameLoadoutFlow(string oldName)
        {
            var dlg = new RenameWindow(oldName, "Rename Loadout", "New name:") { Owner = Window.GetWindow(this) };
            if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.NewName) || dlg.NewName == oldName) return;

            string newName = dlg.NewName.Trim();
            if (!BackendCore.IsValidLoadoutName(newName))
            {
                NotificationService.ShowWarning("Loadout name can't contain \\ / : * ? \" < > |");
                return;
            }
            if (_core.LoadoutExists(_customProfile.Key, newName))
            {
                NotificationService.ShowWarning($"A loadout named '{newName}' already exists.");
                return;
            }

            if (_core.RenameLoadout(_customProfile.Key, oldName, newName))
                NotificationService.ShowSuccess($"Renamed to '{newName}'", "Loadouts");
            else
                NotificationService.ShowError($"Could not rename '{oldName}' — loadout may not exist or name is taken.", "Loadouts", "TMM-E010");
        }

        private void DeleteLoadoutFlow(string name)
        {
            var result = MessageBox.Show($"Delete loadout '{name}'? This cannot be undone.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            if (_core.DeleteLoadout(_customProfile.Key, name))
                NotificationService.ShowSuccess($"Deleted '{name}'", "Loadouts");
            else
                NotificationService.ShowError($"Could not delete '{name}' — file may not exist.", "Loadouts", "TMM-E010");
        }

        private async Task ExportLoadoutFlow(string loadoutName)
        {
            var sfd = new SaveFileDialog
            {
                Filter   = "TMM Pack|*.tmmpack",
                FileName = $"{loadoutName}.tmmpack",
                Title    = "Export Loadout",
            };
            if (sfd.ShowDialog() != true) return;

            try
            {
                int modCount = await TmmPackBuilder.ExportAsync(
                    _core, _customProfile.Key, _customConfig.GameName, loadoutName, sfd.FileName);
                NotificationService.ShowSuccess($"Exported '{loadoutName}' ({modCount} mods)", "Loadouts");
            }
            catch (Exception ex)
            {
                Logger.Error($"Export failed for loadout '{loadoutName}'", ex);
                NotificationService.ShowError($"Export failed: {ex.Message}", "Loadouts", "TMM-E010");
            }
        }
    }
}
