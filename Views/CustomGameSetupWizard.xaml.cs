using System;
using System.Windows;
using System.Windows.Media;

namespace TMM
{
    // ── IWizardStep ────────────────────────────────────────────────────────────

    /// <summary>Contract for each step page in the CustomGameSetupWizard.</summary>
    public interface IWizardStep
    {
        /// <summary>True when the step has enough valid input to proceed.</summary>
        bool IsValid { get; }

        /// <summary>Fires when IsValid may have changed.</summary>
        event EventHandler? ValidationChanged;

        /// <summary>Populate the step's UI fields from the shared profile.</summary>
        void LoadProfile(CustomGameProfile profile);

        /// <summary>Write the step's UI fields back into the shared profile.</summary>
        void SaveProfile(CustomGameProfile profile);
    }

    // ── CustomGameSetupWizard ──────────────────────────────────────────────────

    public partial class CustomGameSetupWizard : TmmWindow
    {
        private static readonly string[] StepTitles =
        {
            "Game Details",
            "Mod Types",
            "Routing Rules",
            "Review & Save",
        };
        private static readonly string[] StepLabels =
        {
            "Step 1 of 4",
            "Step 2 of 4",
            "Step 3 of 4",
            "Step 4 of 4",
        };

        private int _step = 0; // 0-based
        private readonly CustomGameProfile _profile;
        private readonly bool _isEdit;

        // Step pages (lazy-init)
        private readonly IWizardStep[] _steps;

        public CustomGameProfile? Result { get; private set; }

        public CustomGameSetupWizard(CustomGameProfile? existing = null)
        {
            _isEdit  = existing is not null;
            _profile = existing is not null
                ? CloneProfile(existing)
                : new CustomGameProfile();

            InitializeComponent();

            if (_isEdit) txtWindowTitle.Text = "Edit Game";

            _steps = new IWizardStep[]
            {
                new Step1_GameDetailsPage(),
                new Step2_ModTypesPage(),
                new Step3_RoutingRulesPage(),
                new Step4_ReviewPage(),
            };

            foreach (var step in _steps)
                step.ValidationChanged += (_, _) => RefreshNextButton();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ThemeEngine.ApplyTheme(((App)Application.Current).Core.Settings);
            GoToStep(0);
        }

        private void GoToStep(int index)
        {
            // Save current step's data before switching
            if (_step < _steps.Length && stepContent.Content is IWizardStep current)
                current.SaveProfile(_profile);

            _step = index;
            var page = _steps[_step];

            // Load the new step's data
            page.LoadProfile(_profile);

            // Update header
            txtStepLabel.Text = StepLabels[_step];
            txtStepTitle.Text = StepTitles[_step];

            // Update step dots
            var dots = new[] { dot1, dot2, dot3, dot4 };
            var accentBrush  = FindRes("AccentBrush");
            var dimBrush     = FindRes("ControlBgBrush");
            for (int i = 0; i < dots.Length; i++)
                dots[i].Fill = i <= _step ? accentBrush : dimBrush;

            // Update buttons
            btnBack.Visibility = _step > 0 ? Visibility.Visible : Visibility.Collapsed;
            btnNext.Content    = _step == _steps.Length - 1 ? "Save Profile" : "Next →";

            stepContent.Content = page;
            RefreshNextButton();
        }

        private void RefreshNextButton()
        {
            if (btnNext is null || _step >= _steps.Length) return;
            bool valid = _steps[_step].IsValid;
            btnNext.IsEnabled = valid;
            btnNext.Opacity   = valid ? 1.0 : 0.45;
            txtFooterMsg.Text = valid ? "" : GetStepHint();
        }

        private string GetStepHint() => _step switch
        {
            0 => "Enter a game name and a valid install directory to continue.",
            _ => ""
        };

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (stepContent.Content is IWizardStep s) s.SaveProfile(_profile);

            if (_step < _steps.Length - 1)
            {
                GoToStep(_step + 1);
            }
            else
            {
                SaveAndClose();
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (stepContent.Content is IWizardStep s) s.SaveProfile(_profile);
            if (_step > 0) GoToStep(_step - 1);
        }

        private void SaveAndClose()
        {
            if (stepContent.Content is IWizardStep s) s.SaveProfile(_profile);

            Result = _profile;
            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private new void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private Brush FindRes(string key)
        {
            if (Application.Current.Resources[key] is Brush b) return b;
            return Brushes.Gray;
        }

        private static CustomGameProfile CloneProfile(CustomGameProfile src) => new()
        {
            GameName      = src.GameName,
            GameDirectory = src.GameDirectory,
            ExePath       = src.ExePath,
            SteamAppId    = src.SteamAppId,
            ModTypes      = new(src.ModTypes),
            RoutingRules  = new(src.RoutingRules),
            Robustness    = src.Robustness,
            ReleaseTag    = src.ReleaseTag,
            CustomTag     = src.CustomTag,
            IsNative      = src.IsNative,
            Version       = src.Version,
            Description   = src.Description,
            Author        = src.Author,
        };
    }
}
