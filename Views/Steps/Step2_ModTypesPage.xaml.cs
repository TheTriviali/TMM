using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TMM
{
    // ── ModTypeViewModel ───────────────────────────────────────────────────────

    public class ModTypeViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private bool _isExpanded = false;

        public string Name
        {
            get => _name;
            set { if (_name != value) { _name = value; OnPropertyChanged(); } }
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditButtonLabel)); } }
        }

        public string EditButtonLabel => IsExpanded ? "Collapse" : "Edit";

        public ObservableCollection<string> FileExtensions { get; } = new();

        public bool HasNoExtensions => FileExtensions.Count == 0;

        public ModTypeViewModel()
        {
            FileExtensions.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoExtensions));
        }

        public ModType ToModel() => new()
        {
            Name           = Name,
            FileExtensions = new(FileExtensions),
        };

        public static ModTypeViewModel FromModel(ModType m)
        {
            var vm = new ModTypeViewModel { Name = m.Name };
            foreach (var ext in m.FileExtensions) vm.FileExtensions.Add(ext);
            return vm;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? n = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    // ── Step2_ModTypesPage ─────────────────────────────────────────────────────

    public partial class Step2_ModTypesPage : UserControl, IWizardStep
    {
        public event EventHandler? ValidationChanged;
        public bool IsValid => true; // mod types are optional

        private readonly ObservableCollection<ModTypeViewModel> _types = new();

        public Step2_ModTypesPage()
        {
            InitializeComponent();
            icModTypes.ItemsSource = _types;
            _types.CollectionChanged += (_, _) => UpdateEmptyState();
        }

        public void LoadProfile(GameConfig profile)
        {
            _types.Clear();
            foreach (var mt in profile.ModTypes)
                _types.Add(ModTypeViewModel.FromModel(mt));
            UpdateEmptyState();
        }

        public void SaveProfile(GameConfig profile)
        {
            profile.ModTypes.Clear();
            foreach (var vm in _types)
                profile.ModTypes.Add(vm.ToModel());
        }

        private void UpdateEmptyState()
        {
            if (pnlEmpty != null)
                pnlEmpty.Visibility = _types.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnAddModType_Click(object sender, RoutedEventArgs e)
        {
            var vm = new ModTypeViewModel { Name = "New Mod Type", IsExpanded = true };
            _types.Add(vm);
        }

        private void BtnAddPreset_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not string tag) return;

            ModTypeViewModel vm = tag switch
            {
                "asi"     => MakePreset("ASI Plugin",    ".asi", ".dll"),
                "cleo"    => MakePreset("CLEO Script",   ".cs", ".cleo", ".fxt"),
                "dll"     => MakePreset("DLL Plugin",    ".dll"),
                "texture" => MakePreset("Texture Pack",  ".dds", ".txd"),
                _         => new ModTypeViewModel { Name = "Mod Type" }
            };

            // Don't add duplicate preset names
            foreach (var existing in _types)
                if (existing.Name.Equals(vm.Name, StringComparison.OrdinalIgnoreCase)) return;

            _types.Add(vm);
        }

        private static ModTypeViewModel MakePreset(string name, params string[] exts)
        {
            var vm = new ModTypeViewModel { Name = name };
            foreach (var ext in exts) vm.FileExtensions.Add(ext);
            return vm;
        }

        private void BtnToggleExpand_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ModTypeViewModel vm)
                vm.IsExpanded = !vm.IsExpanded;
        }

        private void BtnDeleteModType_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is ModTypeViewModel vm)
                _types.Remove(vm);
        }

        private void BtnAddExtension_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.Tag is not ModTypeViewModel vm) return;

            // Find the companion TextBox via Tag lookup in the same DataTemplate
            var btn = (Button)sender;
            var panel = btn.Parent as Grid;
            if (panel?.Children[0] is TextBox tb) AddExtension(vm, tb);
        }

        private void TxtNewExtension_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter) return;
            if (sender is TextBox tb && tb.Tag is ModTypeViewModel vm)
                AddExtension(vm, tb);
        }

        private static void AddExtension(ModTypeViewModel vm, TextBox tb)
        {
            string ext = tb.Text.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) return;
            if (!ext.StartsWith('.')) ext = "." + ext;
            if (!vm.FileExtensions.Contains(ext))
                vm.FileExtensions.Add(ext);
            tb.Clear();
        }

        private void BtnRemoveExtension_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn) return;
            string? ext = btn.Tag as string;
            if (ext is null) return;

            // Walk up to find the ModTypeViewModel from DataContext
            var parent = btn.Parent;
            while (parent is not null)
            {
                if (parent is FrameworkElement fe && fe.DataContext is ModTypeViewModel vm)
                {
                    vm.FileExtensions.Remove(ext);
                    return;
                }
                parent = (parent as FrameworkElement)?.Parent;
            }
        }
    }
}
