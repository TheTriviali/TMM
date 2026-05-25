using System.Windows;
using System.Windows.Controls;

namespace TMM
{
    /// <summary>
    /// Minimal episode/game picker dialog — shows a prompt and a list of GameProfiles to choose from.
    /// Originally defined inside Gta4DashboardWindow; moved here when that window was deleted.
    /// </summary>
    internal sealed class EpisodePicker : Window
    {
        public GameProfile? SelectedProfile { get; private set; }

        public EpisodePicker(string prompt, IEnumerable<GameProfile> profiles)
        {
            Title           = "Select Target";
            Width           = 320;
            Height          = 200;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode      = ResizeMode.NoResize;
            WindowStyle     = WindowStyle.SingleBorderWindow;
            Background      = (System.Windows.Media.Brush)Application.Current.Resources["BgBrush"];

            var stack = new StackPanel { Margin = new Thickness(16) };

            stack.Children.Add(new TextBlock
            {
                Text         = prompt,
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(0, 0, 0, 10),
                Foreground   = (System.Windows.Media.Brush)Application.Current.Resources["FgBrush"]
            });

            foreach (var p in profiles)
            {
                var profile = p; // capture
                var btn = new Button
                {
                    Content = profile.DisplayName,
                    Tag     = profile,
                    Margin  = new Thickness(0, 0, 0, 6),
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                btn.Click += (_, _) =>
                {
                    SelectedProfile = profile;
                    DialogResult = true;
                };
                stack.Children.Add(btn);
            }

            var cancelBtn = new Button
            {
                Content             = "Cancel",
                Margin              = new Thickness(0, 4, 0, 0),
                Padding             = new Thickness(8, 4, 8, 4),
                HorizontalAlignment = HorizontalAlignment.Right,
                IsCancel            = true
            };
            cancelBtn.Click += (_, _) => { DialogResult = false; };
            stack.Children.Add(cancelBtn);

            Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
    }
}
