using System.Windows.Media;

namespace TMM
{
    /// <summary>
    /// Centralized UI color definitions used across dashboard windows.
    /// </summary>
    public static class UiColors
    {
        public static readonly Color DisabledGray = Color.FromRgb(70, 70, 70);
        public static readonly Color ReadyGreen = Color.FromRgb(80, 200, 100);
        public static readonly Color NotReadyRed = Color.FromRgb(160, 60, 60);
        public static readonly Color PendingOrange = Color.FromRgb(200, 110, 20);

        public static SolidColorBrush DisabledBrush => new(DisabledGray);
        public static SolidColorBrush ReadyBrush => new(ReadyGreen);
        public static SolidColorBrush NotReadyBrush => new(NotReadyRed);
        public static SolidColorBrush PendingBrush => new(PendingOrange);
    }
}
