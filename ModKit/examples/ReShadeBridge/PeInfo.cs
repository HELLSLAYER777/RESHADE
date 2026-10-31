using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ReShadeBridge
{
    /// <summary>Facts about a dll or exe on disk: its CPU type, and whether a dll is ReShade.</summary>
    internal static class PeInfo
    {
        private static readonly Dictionary<string, (long Length, DateTime Written, bool IsReShade)> cache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>"x64", "x86" or "arm64"; null when the file is not a readable PE file.</summary>
        public static string? Machine(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                using var br = new BinaryReader(fs);
                if (fs.Length < 0x40) return null;
                fs.Position = 0x3C;
                var pe = br.ReadInt32();
                if (pe <= 0 || pe + 6 > fs.Length) return null;
                fs.Position = pe;
                if (br.ReadUInt32() != 0x00004550) return null;   // "PE\0\0"
                return br.ReadUInt16() switch { 0x8664 => "x64", 0x14C => "x86", 0xAA64 => "arm64", _ => null };
            }
            catch { return null; }
        }

        /// <summary>True when the file is a ReShade dll (by its version information, or the name inside it). Remembered per file version.</summary>
        public static bool LooksLikeReShade(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return false;
                if (cache.TryGetValue(path, out var c) && c.Length == info.Length && c.Written == info.LastWriteTimeUtc) return c.IsReShade;

                var result = HasReShadeVersionInfo(path) || ContainsName(path);
                cache[path] = (info.Length, info.LastWriteTimeUtc, result);
                return result;
            }
            catch { return false; }
        }

        public static string? Version(string path)
        {
            try
            {
                var v = FileVersionInfo.GetVersionInfo(path);
                var text = v.ProductVersion ?? v.FileVersion;
                return string.IsNullOrWhiteSpace(text) ? null : text!.Trim();
            }
            catch { return null; }
        }

        private static bool HasReShadeVersionInfo(string path)
        {
            try
            {
                var v = FileVersionInfo.GetVersionInfo(path);
                foreach (var s in new[] { v.ProductName, v.FileDescription, v.InternalName, v.OriginalFilename })
                    if (s != null && s.Contains("reshade", StringComparison.OrdinalIgnoreCase)) return true;
            }
            catch { }
            return false;
        }

        private static bool ContainsName(string path)
        {
            var bytes = File.ReadAllBytes(path);
            ReadOnlySpan<byte> ascii = Encoding.ASCII.GetBytes("ReShade.ini");
            ReadOnlySpan<byte> wide = Encoding.Unicode.GetBytes("ReShade.ini");
            return bytes.AsSpan().IndexOf(ascii) >= 0 || bytes.AsSpan().IndexOf(wide) >= 0;
        }
    }
}
