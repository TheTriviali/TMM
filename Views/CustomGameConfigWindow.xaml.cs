using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace TMM
{
    public class MappingRow : INotifyPropertyChanged
    {
        private string _extension = "";
        private string _outputFolder = ".";

        public string Extension
        {
            get => _extension;
            set { _extension = value; OnPropertyChanged(nameof(Extension)); }
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set { _outputFolder = value; OnPropertyChanged(nameof(OutputFolder)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class CustomGameConfigWindow : Window
    {
        public CustomGameProfile? Result { get; private set; }
        private readonly bool _isEdit;
        private readonly ObservableCollection<MappingRow> _mappings = new();

        public CustomGameConfigWindow(CustomGameProfile? existing)
        {
            _isEdit = existing != null;
            InitializeComponent();
            dgMappings.ItemsSource = _mappings;

            if (_isEdit && existing != null)
            {
                txtWindowTitle.Text = "Edit Custom Game";
                btnSave.Content     = "Update";
                txtGameName.Text    = existing.GameName;
                txtGameDir.Text     = existing.GameDirectory;
                txtExePath.Text     = existing.ExePath ?? "";
                txtFileTypes.Text   = existing.ModFileTypes;

                foreach (var kvp in existing.OutputDirectories)
                    _mappings.Add(new MappingRow { Extension = kvp.Key, OutputFolder = kvp.Value });
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeEngine.ApplyTheme(((App)Application.Current).Core.Settings);
        }

        private void BtnBrowseDir_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFolderDialog
            {
                Title = "Select Game Directory",
                InitialDirectory = string.IsNullOrEmpty(txtGameDir.Text)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                    : txtGameDir.Text
            };

            if (dlg.ShowDialog() == true)
            {
                txtGameDir.Text = dlg.FolderName;

                if (string.IsNullOrWhiteSpace(txtGameName.Text))
                    txtGameName.Text = System.IO.Path.GetFileName(dlg.FolderName);
            }
        }

        private void BtnBrowseExe_Click(object sender, RoutedEventArgs e)
        {
            string gameDir = txtGameDir.Text.Trim();
            var dlg = new OpenFileDialog
            {
                Title = "Select Game Executable",
                Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
                InitialDirectory = System.IO.Directory.Exists(gameDir)
                    ? gameDir
                    : Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            if (dlg.ShowDialog() == true)
            {
                string exePath = dlg.FileName;
                if (!string.IsNullOrEmpty(gameDir) && exePath.StartsWith(gameDir, StringComparison.OrdinalIgnoreCase))
                    exePath = exePath[gameDir.Length..].TrimStart(System.IO.Path.DirectorySeparatorChar,
                                                                  System.IO.Path.AltDirectorySeparatorChar);
                txtExePath.Text = exePath;
            }
        }

        private void BtnAddMapping_Click(object sender, RoutedEventArgs e)
        {
            var row = new MappingRow { Extension = ".ext", OutputFolder = "." };
            _mappings.Add(row);
            dgMappings.ScrollIntoView(row);
            dgMappings.SelectedItem = row;
            dgMappings.CurrentCell  = new System.Windows.Controls.DataGridCellInfo(row, dgMappings.Columns[0]);
            dgMappings.BeginEdit();
        }

        private void BtnRemoveMapping_Click(object sender, RoutedEventArgs e)
        {
            if (dgMappings.SelectedItem is MappingRow row)
                _mappings.Remove(row);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            dgMappings.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

            string name = txtGameName.Text.Trim();
            string dir  = txtGameDir.Text.Trim();

            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("Please enter a game name.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtGameName.Focus();
                return;
            }

            if (string.IsNullOrEmpty(dir))
            {
                MessageBox.Show("Please select the game directory.", "Required Field", MessageBoxButton.OK, MessageBoxImage.Warning);
                txtGameDir.Focus();
                return;
            }

            string fileTypes = string.IsNullOrWhiteSpace(txtFileTypes.Text)
                ? ".zip, .rar, .7z"
                : txtFileTypes.Text.Trim();

            var outputDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in _mappings)
            {
                string ext = row.Extension.Trim().ToLowerInvariant();
                string folder = string.IsNullOrWhiteSpace(row.OutputFolder) ? "." : row.OutputFolder.Trim();
                if (!string.IsNullOrEmpty(ext) && ext != ".ext")
                    outputDirs[ext] = folder;
            }

            Result = new CustomGameProfile
            {
                GameName          = name,
                GameDirectory     = dir,
                ExePath           = string.IsNullOrWhiteSpace(txtExePath.Text) ? null : txtExePath.Text.Trim(),
                ModFileTypes      = fileTypes,
                OutputDirectories = outputDirs
            };

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
