using System;
using System.Windows;

namespace TMM
{
    public partial class CrashReportWindow : TmmWindow
    {
        private readonly string _fullText;

        public CrashReportWindow(Exception ex)
        {
            InitializeComponent();
            _fullText = BuildReport(ex);
            txtMessage.Text = ex.Message;
            txtDetail.Text  = ex.StackTrace ?? "(no stack trace)";
        }

        private static string BuildReport(Exception ex)
            => $"Error: {ex.Message}\n\nType: {ex.GetType().FullName}\n\nStack Trace:\n{ex.StackTrace}";

        private void BtnCopy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(_fullText);
                btnCopy.Content = "Copied!";
            }
            catch { /* clipboard unavailable — silent */ }
        }

    }
}
