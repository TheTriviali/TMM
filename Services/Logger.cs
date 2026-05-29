using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;

namespace TMM.Services
{
    /// <summary>
    /// Lightweight file logger with size-based rotation. Co-exists with the legacy
    /// <see cref="BackendCore.Log"/> path — both write to the same TMM.log so existing
    /// call sites keep working while new code can rotate cleanly.
    /// </summary>
    public static class Logger
    {
        public const long MaxBytes = 5L * 1024 * 1024;
        public const int KeepRotations = 3;

        private static string? _logPath;
        private static readonly object _gate = new();
        private static readonly ConcurrentQueue<string> _ring = new();
        private const int RingCapacity = 200;

        public static void Initialize(string appDataPath)
        {
            _logPath = Path.Combine(appDataPath, "TMM.log");
            try { RotateIfNeeded(); } catch { /* logging must never crash the app */ }
        }

        public static void Info(string message) => Write("INFO", message);
        public static void Warn(string message) => Write("WARN", message);
        public static void Error(string message, Exception? ex = null) =>
            Write("ERROR", ex is null ? message : $"{message}\n  {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");

        /// <summary>Returns the last ~N lines from the in-memory ring buffer (no disk read).</summary>
        public static string Tail(int maxLines = 50)
        {
            var arr = _ring.ToArray();
            int start = Math.Max(0, arr.Length - maxLines);
            return string.Join(Environment.NewLine, arr.Skip(start));
        }

        private static void Write(string level, string message)
        {
            string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {level} {message}";
            _ring.Enqueue(line);
            while (_ring.Count > RingCapacity) _ring.TryDequeue(out _);

            if (_logPath is null) return;
            try
            {
                lock (_gate)
                {
                    RotateIfNeeded();
                    File.AppendAllText(_logPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { /* swallow */ }
        }

        private static void RotateIfNeeded()
        {
            if (_logPath is null || !File.Exists(_logPath)) return;
            var info = new FileInfo(_logPath);
            if (info.Length < MaxBytes) return;

            string dir = info.DirectoryName ?? Path.GetTempPath();
            string baseName = Path.GetFileNameWithoutExtension(_logPath);

            for (int i = KeepRotations; i >= 1; i--)
            {
                string src = Path.Combine(dir, $"{baseName}.log.{i}");
                string dst = Path.Combine(dir, $"{baseName}.log.{i + 1}");
                if (File.Exists(src))
                {
                    if (i == KeepRotations) File.Delete(src);
                    else File.Move(src, dst, overwrite: true);
                }
            }

            File.Move(_logPath, Path.Combine(dir, $"{baseName}.log.1"), overwrite: true);
        }
    }
}
