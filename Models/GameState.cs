using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;

namespace TGTAMM
{
    /// <summary>
    /// Detected state of a game executable.
    /// Unknown = not found / unrecognised hash.
    /// Vanilla = Steam DRM exe present (cannot run in virtual mode).
    /// Downgraded = confirmed 1.0 build via MD5 (safe to deploy and launch).
    /// </summary>
    public enum ExeStatus { Unknown, Vanilla, Downgraded }

    /// <summary>
    /// Immutable per-game snapshot. Re-created on each scan.
    /// The UI reads ButtonColor to tint the play buttons and IsReady to gate deployment.
    /// </summary>
    public sealed record GameDetectionState(
        GameProfile Profile,
        ExeStatus Status,
        string? ExePath = null)
    {
        public bool IsReady => Status == ExeStatus.Downgraded;

        /// <summary>Green = ready, Amber = vanilla, Gray = unknown.</summary>
        public Color ButtonColor => Status switch
        {
            ExeStatus.Downgraded => Color.FromRgb(0x4C, 0xAF, 0x50),
            ExeStatus.Vanilla => Color.FromRgb(0xFF, 0x98, 0x00),
            _ => Color.FromRgb(0x75, 0x75, 0x75),
        };

        public string StatusLabel => Status switch
        {
            ExeStatus.Downgraded => "Ready (1.0)",
            ExeStatus.Vanilla => "Vanilla — downgrade required",
            _ => "Not detected",
        };
    }

    /// <summary>
    /// Singleton detection manager. Call ScanAll on startup/refresh;
    /// call ScanGame when a single path changes.
    /// Subscribe to StateChanged to update UI reactively.
    /// </summary>
    public sealed class GameStateManager
    {
        public static readonly GameStateManager Instance = new();

        private readonly Dictionary<string, GameDetectionState> _states = new();
        public IReadOnlyDictionary<string, GameDetectionState> States => _states;

        /// <summary>Raised when any game's status changes. Args: gameKey, new state.</summary>
        public event Action<string, GameDetectionState>? StateChanged;

        public void ScanAll(AppSettings settings, Action<string>? log = null)
        {
            foreach (var p in GameProfile.All)
                ScanGame(p, settings.GamePaths.GetValueOrDefault(p.Key), log);
        }

        public GameDetectionState ScanGame(GameProfile profile, string? folder, Action<string>? log = null)
        {
            var state = Detect(profile, folder, log);
            bool changed = !_states.TryGetValue(profile.Key, out var prev) || prev != state;
            _states[profile.Key] = state;
            if (changed) StateChanged?.Invoke(profile.Key, state);
            return state;
        }

        public GameDetectionState For(string key)
        {
            if (_states.TryGetValue(key, out var s)) return s;
            var p = GameProfile.ByKey(key) ?? throw new ArgumentException($"Unknown key: {key}");
            return new GameDetectionState(p, ExeStatus.Unknown);
        }

        private static GameDetectionState Detect(GameProfile profile, string? folder, Action<string>? log)
        {
            if (string.IsNullOrWhiteSpace(folder))
            {
                log?.Invoke($"[{profile.Key}] No path configured.");
                return new GameDetectionState(profile, ExeStatus.Unknown);
            }

            string exePath = Path.Combine(folder, profile.ExeName);
            if (!File.Exists(exePath))
            {
                log?.Invoke($"[{profile.Key}] Exe not found: {exePath}");
                return new GameDetectionState(profile, ExeStatus.Unknown);
            }

            string md5 = ComputeMd5(exePath);
            log?.Invoke($"[{profile.Key}] MD5: {md5}");

            if (profile.IsValidMd5(md5))
            {
                log?.Invoke($"[{profile.Key}] Downgraded 1.0 confirmed.");
                return new GameDetectionState(profile, ExeStatus.Downgraded, exePath);
            }

            log?.Invoke($"[{profile.Key}] Unrecognised hash — treating as Vanilla.");
            return new GameDetectionState(profile, ExeStatus.Vanilla, exePath);
        }

        private static string ComputeMd5(string path)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(path);
            return BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }

    /// <summary>Progress payload for long-running deploy/clone operations.</summary>

    // ── Back-compat shim ─────────────────────────────────────────────────────
    // BackendCore still uses the name 'GameState'. Keeps it compiling until
    // the rename is done properly.
    public static class GameState
    {
        public static GameStateManager Instance => GameStateManager.Instance;
        public static void ScanAll(AppSettings s, Action<string>? log = null)
            => GameStateManager.Instance.ScanAll(s, log);
        public static GameDetectionState ScanGame(GameProfile p, string? folder, Action<string>? log = null)
            => GameStateManager.Instance.ScanGame(p, folder, log);
        public static GameDetectionState For(string key)
            => GameStateManager.Instance.For(key);
    }
}
