using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace TMM.Services
{
    /// <summary>Result of an exe integrity check against a profile's expected fingerprint.</summary>
    public enum IntegrityState
    {
        /// <summary>Profile declares no expected size or hashes — nothing to verify.</summary>
        NotConfigured,
        /// <summary>Expected fingerprint matches the actual file.</summary>
        Ok,
        /// <summary>File size differs from the expected value.</summary>
        SizeMismatch,
        /// <summary>File size matches (or wasn't checked) but the MD5 is not in the accepted list.</summary>
        Md5Mismatch,
        /// <summary>Exe file does not exist at the resolved path.</summary>
        FileMissing,
    }

    /// <summary>Outcome of an integrity check with a human-readable message.</summary>
    public readonly record struct IntegrityResult(IntegrityState State, string Message);

    /// <summary>
    /// Generic exe integrity verification for any game profile.
    /// Supports filesize-only checks (fast), MD5 checks (slower but stricter),
    /// or both. Independent of any specific game — driven entirely by the
    /// profile's <see cref="CustomGameProfile.ExpectedExeBytes"/> and
    /// <see cref="CustomGameProfile.AcceptedExeMd5s"/> fields.
    /// </summary>
    public static class IntegrityChecker
    {
        /// <summary>
        /// Compares the file at <paramref name="exePath"/> against the profile's
        /// expected fingerprint. Filesize is checked first (cheap); MD5 only runs
        /// if size matches (or no size was configured) AND hashes are configured.
        /// </summary>
        public static async Task<IntegrityResult> CheckAsync(string exePath, CustomGameProfile profile)
        {
            bool hasSize = profile.ExpectedExeBytes.HasValue;
            bool hasHash = profile.AcceptedExeMd5s.Count > 0;

            if (!hasSize && !hasHash)
                return new IntegrityResult(IntegrityState.NotConfigured, "No integrity check configured.");

            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                return new IntegrityResult(IntegrityState.FileMissing, $"Exe not found: {exePath}");

            if (hasSize)
            {
                long actual = new FileInfo(exePath).Length;
                long expected = profile.ExpectedExeBytes!.Value;
                if (actual != expected)
                    return new IntegrityResult(
                        IntegrityState.SizeMismatch,
                        $"Size mismatch: expected {expected:N0} bytes, got {actual:N0} bytes.");
            }

            if (hasHash)
            {
                string actual = await ComputeMd5Async(exePath);
                bool match = profile.AcceptedExeMd5s.Any(h =>
                    string.Equals(h.Trim(), actual, StringComparison.OrdinalIgnoreCase));
                if (!match)
                    return new IntegrityResult(
                        IntegrityState.Md5Mismatch,
                        $"MD5 mismatch: got {actual}, expected one of {profile.AcceptedExeMd5s.Count} accepted hash(es).");
            }

            return new IntegrityResult(IntegrityState.Ok, "Integrity verified.");
        }

        /// <summary>Returns the lowercase-hex MD5 of the file at <paramref name="filePath"/>.</summary>
        public static async Task<string> ComputeMd5Async(string filePath)
        {
            using var md5 = MD5.Create();
            await using var stream = File.OpenRead(filePath);
            byte[] hash = await md5.ComputeHashAsync(stream);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
