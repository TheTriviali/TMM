using System;
using System.Diagnostics;
using System.Windows;

namespace TGTAMM
{
    /// <summary>
    /// Single point of contact for invoking the Steam protocol handler.
    /// Previously each window (MainDashboard, InitialSetup, Settings, DebugConsole)
    /// inlined its own <c>Process.Start("steam://...")</c> with slightly different
    /// error handling — this consolidates that.
    /// </summary>
    public static class SteamLauncher
    {
        /// <summary>Force Steam to verify game files (steam://validate/{appId}).</summary>
        public static bool Validate(GameProfile profile, Action<string>? log = null)
            => Invoke("validate", profile.SteamAppId, log);

        /// <summary>Prompt Steam to install the game (steam://install/{appId}).</summary>
        public static bool Install(GameProfile profile, Action<string>? log = null)
            => Invoke("install", profile.SteamAppId, log);

        /// <summary>Prompt Steam to uninstall the game (steam://uninstall/{appId}).</summary>
        public static bool Uninstall(GameProfile profile, Action<string>? log = null)
            => Invoke("uninstall", profile.SteamAppId, log);

        /// <summary>
        /// Generic Steam protocol invocation. <paramref name="action"/> is e.g. "validate", "install".
        /// Returns true if Steam was contacted; false if the protocol launch failed.
        /// </summary>
        public static bool Invoke(string action, string appId, Action<string>? log = null)
        {
            string url = $"steam://{action}/{appId}";
            log?.Invoke($"Triggering Steam Protocol: {url}");

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke($"Steam protocol launch failed: {ex.Message}");
                MessageBox.Show(
                    $"Could not contact Steam:\n{ex.Message}\n\nEnsure Steam is installed and running.",
                    "Steam Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
