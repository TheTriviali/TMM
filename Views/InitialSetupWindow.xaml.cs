using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace TMM
{
    public partial class InitialSetupWindow : Window
    {
        private readonly BackendCore _core;

        public InitialSetupWindow(BackendCore core)
        {
            InitializeComponent();
            _core = core;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Bind each row to its profile. The old code ran the load logic
            // TWICE on launch (constructor Loaded+= AND XAML Loaded=) - this
            // unifies it into one place.
            rowIII.Bind(_core, GameProfile.III);
            rowVC.Bind(_core, GameProfile.VC);
            rowSA.Bind(_core, GameProfile.SA);

            // Pre-populate via quick scan for any unmapped paths.
            await Task.Run(() => _core.QuickScan());
            await RefreshAllAsync();
        }

        private async Task RefreshAllAsync()
        {
            await rowIII.RefreshAsync();
            await rowVC.RefreshAsync();
            await rowSA.RefreshAsync();
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            // Allow closing even during first launch, but warn if no games are mapped.
            bool anyReady = GameProfile.All.Any(_core.IsGameReady);
            if (!anyReady)
            {
                var result = MessageBox.Show(
                    "No games are mapped yet. TMM won't be able to manage mods until at least one game is located.\n\nClose anyway?",
                    "No Games Mapped", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
            }
            DialogResult = false;
            Close();
        }

        private void BtnFinish_Click(object sender, RoutedEventArgs e)
        {
            bool anyReady = GameProfile.All.Any(_core.IsGameReady);
            if (!anyReady)
            {
                MessageBox.Show(
                    "You must locate at least one game before finishing setup. " +
                    "TMM needs a path to know where to manage your mods.",
                    "Setup Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _core.Settings.FirstLaunch = false;
            _core.SaveSettings();
            DialogResult = true;
            Close();
        }
    }
}
