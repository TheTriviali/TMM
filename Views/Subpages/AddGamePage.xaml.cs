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
    public partial class AddGamePage : UserControl
    {
        // ── State ─────────────────────────────────────────────────────────────────

        private readonly BackendCore _core;
        private CustomGameProfile _profile = new();
        private string? _editKey; // non-null when editing an existing game

        /// <summary>True when the page is in edit mode (pre-filled for an existing game).</summary>
        public bool IsEditMode => _editKey is not null;

        // ── Events ────────────────────────────────────────────────────────────────

        public event Action? Cancelled;
        public event Func<Task>? GameSaved; // shell handles re-init + library refresh

        // ── Constructor ───────────────────────────────────────────────────────────

        public AddGamePage(BackendCore core)
        {
            _core = core;
            InitializeComponent();

            _step1.ValidationChanged += Step1_ValidationChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void LoadForAdd()
        {
            _editKey = null;
            _profile = new CustomGameProfile();
            LoadAllSteps();
            UpdateHeader();
        }

        public void LoadForEdit(string key, CustomGameProfile profile)
        {
            _editKey = key;
            _profile = CloneProfile(profile);
            LoadAllSteps();
            UpdateHeader();
        }

        // ── Internal helpers ──────────────────────────────────────────────────────

        private void LoadAllSteps()
        {
            _step1.LoadProfile(_profile);
            _step2.LoadProfile(_profile);
            _step3.LoadProfile(_profile);
            _step4.LoadProfile(_profile);
            RefreshCreateButton();
            UpdateSummary();
            // Scroll back to top when loading a new game
            formScroller.ScrollToTop();
        }

        private void UpdateHeader()
        {
            txtPageMode.Text = _editKey is null ? "Add a Game" : "Edit Game";
            if (_editKey is not null)
            {
                txtEditLabel.Text       = $"Editing: {_profile.GameName}";
                txtEditLabel.Visibility = Visibility.Visible;
            }
            else
            {
                txtEditLabel.Visibility = Visibility.Collapsed;
            }
            btnCreate.Content = _editKey is null ? "Create" : "Save";
        }

        private void RefreshCreateButton()
        {
            btnCreate.IsEnabled = _step1.IsValid;
            // Essentials dot: accent when valid
            dotEssentials.Fill = _step1.IsValid
                ? (Brush)(Application.Current.Resources["AccentBrush"] ?? Brushes.Cyan)
                : (Brush)(Application.Current.Resources["SubTextBrush"] ?? Brushes.Gray);
        }

        private void UpdateSummary()
        {
            if (!_step1.IsValid)
            {
                txtLiveSummary.Text = "Fill in game name and a valid install directory to continue.";
                return;
            }

            // Save current step state into _profile to compute counts
            _step1.SaveProfile(_profile);
            _step2.SaveProfile(_profile);
            _step3.SaveProfile(_profile);

            int modTypeCount  = _profile.ModTypes.Count;
            int ruleCount     = _profile.RoutingRules.Count
                                + _profile.ModTypes.Sum(mt => mt.RoutingRules.Count);
            bool hasIntegrity = _profile.ExpectedExeBytes.HasValue
                                || _profile.AcceptedExeMd5s.Count > 0;

            txtLiveSummary.Text = $"Ready — {modTypeCount} mod type{(modTypeCount != 1 ? "s" : "")}, "
                                + $"{ruleCount} rule{(ruleCount != 1 ? "s" : "")}"
                                + (hasIntegrity ? ", integrity set" : "");
        }

        // ── Step1 validation ──────────────────────────────────────────────────────

        private void Step1_ValidationChanged(object? sender, EventArgs e)
        {
            RefreshCreateButton();
            UpdateSummary();
        }

        // ── Jump-rail ─────────────────────────────────────────────────────────────

        private void JumpToEssentials_Click(object sender, RoutedEventArgs e)
            => sectionEssentials.BringIntoView();

        private void JumpToModTypes_Click(object sender, RoutedEventArgs e)
            => sectionModTypes.BringIntoView();

        private void JumpToRouting_Click(object sender, RoutedEventArgs e)
            => sectionRouting.BringIntoView();

        private void JumpToReview_Click(object sender, RoutedEventArgs e)
        {
            // Refresh review with current state before scrolling to it
            _step1.SaveProfile(_profile);
            _step2.SaveProfile(_profile);
            _step3.SaveProfile(_profile);
            _step4.LoadProfile(_profile);
            sectionReview.BringIntoView();
        }

        // ── Footer buttons ────────────────────────────────────────────────────────

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            _step1.SaveProfile(_profile);
            _step2.SaveProfile(_profile);
            _step3.SaveProfile(_profile);
            _step4.SaveProfile(_profile);

            btnCreate.IsEnabled = false;
            try
            {
                if (_editKey is null)
                    await GameRegistry.Instance.AddCustomGameAsync(_profile);
                else
                    await GameRegistry.Instance.UpdateCustomGameAsync(_editKey, _profile);

                string actionWord = _editKey is null ? "added" : "updated";
                NotificationService.ShowSuccess($"'{_profile.GameName}' {actionWord}.", "GameRegistry");

                if (GameSaved is not null)
                    await GameSaved.Invoke();
            }
            catch (IOException ex)
            {
                Logger.Error($"Save game '{_profile.GameName}' failed (IO)", ex);
                NotificationService.ShowError($"Could not save game: {ex.Message}", "GameRegistry", "TMM-E012");
                btnCreate.IsEnabled = _step1.IsValid;
            }
            catch (Exception ex)
            {
                Logger.Error($"Save game '{_profile.GameName}' failed", ex);
                NotificationService.ShowError($"Could not save game: {ex.Message}", "GameRegistry", "TMM-E012");
                btnCreate.IsEnabled = _step1.IsValid;
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
            => Cancelled?.Invoke();

        // ── Profile clone ─────────────────────────────────────────────────────────

        private static CustomGameProfile CloneProfile(CustomGameProfile src) => new()
        {
            GameName          = src.GameName,
            ShortName         = src.ShortName,
            GameDirectory     = src.GameDirectory,
            ExePath           = src.ExePath,
            SteamAppId        = src.SteamAppId,
            NexusSlug         = src.NexusSlug,
            ModTypes          = new(src.ModTypes),
            RoutingRules      = new(src.RoutingRules),
            OverlayFolders    = new(src.OverlayFolders),
            CompanionSiblings = src.CompanionSiblings.ToDictionary(
                kvp => kvp.Key,
                kvp => new System.Collections.Generic.List<string>(kvp.Value)),
            SearchHints       = new(src.SearchHints),
            Robustness        = src.Robustness,
            ReleaseTag        = src.ReleaseTag,
            CustomTag         = src.CustomTag,
            IsNative          = src.IsNative,
            Version           = src.Version,
            Description       = src.Description,
            Author            = src.Author,
            ExpectedExeBytes  = src.ExpectedExeBytes,
            AcceptedExeMd5s   = new(src.AcceptedExeMd5s),
            GradientStartHex  = src.GradientStartHex,
            GradientEndHex    = src.GradientEndHex,
            LibraryStatus     = src.LibraryStatus,
            CustomArtFileName = src.CustomArtFileName,
        };
    }
}
