using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace TMM
{
    /// <summary>
    /// Base class for all TMM windows. Provides shared chrome handlers:
    /// drag-to-move titlebar, minimize, maximize/restore, close, and edge resize.
    /// </summary>
    public class TmmWindow : Window
    {
        // Resize border thickness in device-independent pixels.
        private const double ResizeBorder = 8;

        private const int WM_NCHITTEST   = 0x0084;
        private const int HTTOPLEFT      = 13;
        private const int HTTOPRIGHT     = 14;
        private const int HTBOTTOMLEFT  = 16;
        private const int HTBOTTOMRIGHT = 17;
        private const int HTLEFT        = 10;
        private const int HTRIGHT       = 11;
        private const int HTTOP         = 12;
        private const int HTBOTTOM      = 15;

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            source?.AddHook(ResizeHook);
        }

        private IntPtr ResizeHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg != WM_NCHITTEST || WindowState == WindowState.Maximized)
                return IntPtr.Zero;

            int raw = lParam.ToInt32();
            int screenX = unchecked((short)(raw & 0xFFFF));
            int screenY = unchecked((short)(raw >> 16));

            var pt = PointFromScreen(new Point(screenX, screenY));
            double w = ActualWidth;
            double h = ActualHeight;
            bool l = pt.X < ResizeBorder;
            bool r = pt.X > w - ResizeBorder;
            bool t = pt.Y < ResizeBorder;
            bool b = pt.Y > h - ResizeBorder;

            int hit =
                t && l ? HTTOPLEFT  :
                t && r ? HTTOPRIGHT :
                b && l ? HTBOTTOMLEFT  :
                b && r ? HTBOTTOMRIGHT :
                l      ? HTLEFT  :
                r      ? HTRIGHT :
                t      ? HTTOP   :
                b      ? HTBOTTOM : 0;

            if (hit != 0) { handled = true; return new IntPtr(hit); }
            return IntPtr.Zero;
        }

        protected void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        protected void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        protected void BtnMinimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        protected void BtnMaximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }
}
