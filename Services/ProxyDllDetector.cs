using System;
using System.Collections.Generic;
using System.IO;

namespace TMM.Services
{
    /// <summary>
    /// Identifies "proxy DLLs" — script extenders / mod loaders that masquerade as
    /// a system DLL (dinput8, d3d9, dsound, etc.) and must sit beside the game's exe
    /// rather than in plugins/ or scripts/. Used by the install wizard to surface a hint
    /// and confirm the planned destination.
    /// </summary>
    public static class ProxyDllDetector
    {
        public static readonly HashSet<string> KnownProxies = new(StringComparer.OrdinalIgnoreCase)
        {
            "dinput8.dll", "dinput.dll",
            "d3d9.dll", "d3d10.dll", "d3d11.dll", "d3d12.dll",
            "dxgi.dll", "dsound.dll",
            "xinput1_3.dll", "xinput1_4.dll",
            "winmm.dll", "version.dll", "wininet.dll",
            "msvcr110.dll", "msacm32.dll", "vorbisfile.dll",
            // GTA/Rockstar ecosystem
            "scripthookv.dll", "scripthookvdotnet3.dll", "scripthookrdr2.dll", "asiloader.dll",
            // Bethesda
            "skse64_loader.exe", "f4se_loader.exe",
        };

        public sealed record Detection(string FileName, string FullPath, string Reason);

        /// <summary>Scan a mod's extracted folder for proxy DLLs at any depth.</summary>
        public static List<Detection> Scan(string folder)
        {
            var results = new List<Detection>();
            if (!Directory.Exists(folder)) return results;

            foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (KnownProxies.Contains(name))
                    results.Add(new Detection(name, file, ClassifyReason(name)));
            }
            return results;
        }

        public static bool IsKnownProxy(string fileName) => KnownProxies.Contains(fileName);

        private static string ClassifyReason(string name) => name.ToLowerInvariant() switch
        {
            "scripthookv.dll" => "GTA V Script Hook loader",
            "scripthookvdotnet3.dll" => "GTA V Script Hook .NET",
            "scripthookrdr2.dll" => "Red Dead 2 Script Hook",
            "skse64_loader.exe" => "Skyrim Script Extender loader",
            "f4se_loader.exe" => "Fallout 4 Script Extender loader",
            "asiloader.dll" => "ASI loader",
            _ => "Likely proxy DLL — masquerades as a system DLL to load mods",
        };
    }
}
