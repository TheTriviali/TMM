using System.Windows;
using System.Windows.Controls;

namespace TMM
{
    /// <summary>
    /// Base class for the three dashboard windows.
    /// Provides the shared toolbar-label visibility toggle helper.
    /// </summary>
    public class DashboardWindowBase : TmmWindow
    {
        protected void ApplyToolbarLabels(AppSettings settings, string[] names)
        {
            var vis = settings.ToolbarShowLabels ? Visibility.Visible : Visibility.Collapsed;
            foreach (var name in names)
                if (FindName(name) is TextBlock tb) tb.Visibility = vis;
        }
    }
}
