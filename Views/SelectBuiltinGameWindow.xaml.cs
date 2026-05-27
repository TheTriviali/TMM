using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace TMM
{
    public partial class SelectBuiltinGameWindow : TmmWindow
    {
        private readonly BackendCore _core;

        public SelectBuiltinGameWindow(BackendCore core)
        {
            InitializeComponent();
            _core = core;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Bind GTA III series
            rowIII.Bind(_core, GameProfile.III);
            rowVC.Bind(_core, GameProfile.VC);
            rowSA.Bind(_core, GameProfile.SA);

            // Bind GTA IV family
            rowIV.Bind(_core, GameProfile.IV);
            rowTLaD.Bind(_core, GameProfile.TLaD);
            rowTBoGT.Bind(_core, GameProfile.TBoGT);

            // When IV path changes, refresh siblings
            rowIV.LinkedPathsChanged += async (_, _) =>
            {
                await rowTLaD.RefreshAsync();
                await rowTBoGT.RefreshAsync();
                UpdateDoneButtonState();
            };

            // Track changes to enable Done button
            rowIII.PathChanged += (_, _) => UpdateDoneButtonState();
            rowVC.PathChanged += (_, _) => UpdateDoneButtonState();
            rowSA.PathChanged += (_, _) => UpdateDoneButtonState();
            rowIV.PathChanged += (_, _) => UpdateDoneButtonState();
            rowTLaD.PathChanged += (_, _) => UpdateDoneButtonState();
            rowTBoGT.PathChanged += (_, _) => UpdateDoneButtonState();

            // Pre-populate via quick scan
            await Task.Run(() => _core.QuickScan());
            await RefreshAllAsync();
        }

        private async Task RefreshAllAsync()
        {
            await rowIII.RefreshAsync();
            await rowVC.RefreshAsync();
            await rowSA.RefreshAsync();
            await rowIV.RefreshAsync();
            await rowTLaD.RefreshAsync();
            await rowTBoGT.RefreshAsync();
            UpdateDoneButtonState();
        }

        private void UpdateDoneButtonState()
        {
            bool anyReady = GameProfile.All.Any(_core.IsGameReady);
            BtnDone.IsEnabled = anyReady;
        }

        private new void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.ClickCount == 2) return;
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        }

        private new void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void BtnDone_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
