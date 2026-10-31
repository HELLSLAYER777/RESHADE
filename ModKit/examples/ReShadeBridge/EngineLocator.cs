using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReShadeBridge
{
    internal sealed record EngineInfo(uint Pid, string ExePath)
    {
        public string Folder => Path.GetDirectoryName(ExePath) ?? "";
    }

    /// <summary>Finds the frame-generation engine: the program the app starts when START MOTION is pressed.</summary>
    internal static class EngineLocator
    {
        private static readonly HashSet<string> Ignore = new(StringComparer.OrdinalIgnoreCase)
        {
            "conhost.exe", "cmd.exe", "powershell.exe", "pwsh.exe", "werfault.exe", "wermgr.exe", "dllhost.exe",
            "msedgewebview2.exe", "wscript.exe", "cscript.exe", "OpenConsole.exe"
        };

        private static readonly Dictionary<uint, string?> pathCache = new();

        /// <summary>
        /// The engine processes now running: first the programs the app itself started; when there are none, programs
        /// that live in a folder with the app's name (<paramref name="guessed"/> is then true).
        /// </summary>
        public static List<EngineInfo> FindRunning(out bool guessed)
        {
            guessed = false;
            var snapshot = Native.Snapshot();
            var me = (uint)Environment.ProcessId;

            var descendants = new HashSet<uint> { me };
            var children = new List<ProcInfo>();
            bool added;
            do
            {
                added = false;
                foreach (var p in snapshot)
                {
                    if (descendants.Contains(p.Pid) || !descendants.Contains(p.ParentPid)) continue;
                    descendants.Add(p.Pid);
                    children.Add(p);
                    added = true;
                }
            } while (added);

            var found = new List<EngineInfo>();
            foreach (var p in children)
            {
                if (Ignore.Contains(p.Exe)) continue;
                var path = PathOf(p.Pid);
                if (path != null) found.Add(new EngineInfo(p.Pid, path));
            }

            if (found.Count == 0)
            {
                guessed = true;
                var live = new HashSet<uint>(snapshot.Select(s => s.Pid));
                foreach (var pid in pathCache.Keys.ToList()) if (!live.Contains(pid)) pathCache.Remove(pid);
                foreach (var p in snapshot)
                {
                    if (p.Pid == me || Ignore.Contains(p.Exe)) continue;
                    var name = p.Exe.ToLowerInvariant();
                    if (!(name.Contains("nvngx") || name.Contains("mfg") || name.Contains("supermotion") || name.Contains("super motion"))) continue;
                    var path = PathOf(p.Pid);
                    if (path != null && !string.Equals(path, Environment.ProcessPath, StringComparison.OrdinalIgnoreCase))
                        found.Add(new EngineInfo(p.Pid, path));
                }
            }

            return found
                .OrderByDescending(e => Rank(e.ExePath))
                .ToList();
        }

        private static int Rank(string exe)
        {
            var n = Path.GetFileName(exe).ToLowerInvariant();
            return (n.Contains("engine") ? 2 : 0) + (n.Contains("mfg") || n.Contains("nvngx") ? 1 : 0);
        }

        private static string? PathOf(uint pid)
        {
            if (pathCache.TryGetValue(pid, out var cached)) return cached;
            string? path = null;
            try
            {
                using var p = Process.GetProcessById((int)pid);
                path = p.MainModule?.FileName;
            }
            catch { /* gone already, or not ours to look at */ }
            pathCache[pid] = path;
            return path;
        }

        /// <summary>Where the engine probably lives when it is not running: a folder named Engine beside the app, or in its data folder.</summary>
        public static string? GuessFolderOnDisk()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Engine"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MFG SUPER MOTION", "Engine")
            };
            foreach (var c in candidates)
            {
                try
                {
                    if (Directory.Exists(c) && Directory.GetFiles(c, "*.exe").Length > 0) return c;
                }
                catch { }
            }
            return null;
        }

        public static string? FirstExeIn(string folder)
        {
            try { return Directory.Exists(folder) ? Directory.GetFiles(folder, "*.exe").OrderBy(f => f).FirstOrDefault() : null; }
            catch { return null; }
        }
    }
}
