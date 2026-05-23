using System.Diagnostics;

namespace TMM
{
    internal static class ShellHelper
    {
        public static void OpenFolder(string path) =>
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true, Verb = "open" });

        public static void OpenUrl(string url) =>
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
