using System;
using System.IO;

namespace ReShadeBridge
{
    internal readonly record struct Outcome(bool Ok, string Key, object[] Args);

    /// <summary>
    /// Puts ReShade where the engine loads it: the ReShade dll, named dxgi.dll (or d3d11 / d3d12), in the folder of the
    /// engine's exe - the usual way ReShade is installed for a program - plus a ReShade.ini that points at the user's
    /// shaders and preset and sets the overlay key.
    /// </summary>
    internal static class ReShadeInstaller
    {
        private static readonly string[] ProxyNames = { "dxgi", "d3d11", "d3d12" };

        public static string ProxyFile(BridgeSettings s) =>
            (Array.IndexOf(ProxyNames, s.Proxy) >= 0 ? s.Proxy : "dxgi") + ".dll";

        private static Outcome Fail(string key, params object[] args) => new(false, key, args);

        public static Outcome Install(BridgeSettings s, string? engineExe)
        {
            if (string.IsNullOrWhiteSpace(s.ReShadeDll) || !File.Exists(s.ReShadeDll)) return Fail("err_nodll");
            if (!PeInfo.LooksLikeReShade(s.ReShadeDll)) return Fail("err_notreshade");
            if (string.IsNullOrWhiteSpace(s.EngineFolder)) return Fail("err_noengine");
            if (!Directory.Exists(s.EngineFolder)) return Fail("err_nofolder", s.EngineFolder);

            var exe = engineExe != null && File.Exists(engineExe) ? engineExe : EngineLocator.FirstExeIn(s.EngineFolder);
            var dllArch = PeInfo.Machine(s.ReShadeDll);
            var exeArch = exe != null ? PeInfo.Machine(exe) : null;
            if (dllArch != null && exeArch != null && dllArch != exeArch) return Fail("err_arch", dllArch, exeArch);

            var target = Path.Combine(s.EngineFolder, ProxyFile(s));
            try
            {
                if (File.Exists(target) && !PeInfo.LooksLikeReShade(target)) return Fail("err_foreign", Path.GetFileName(target));

                // Only one ReShade may load: take away a proxy of ours that an earlier choice left under another name.
                foreach (var name in ProxyNames)
                {
                    var other = Path.Combine(s.EngineFolder, name + ".dll");
                    if (!string.Equals(other, target, StringComparison.OrdinalIgnoreCase) && File.Exists(other) && PeInfo.LooksLikeReShade(other))
                        File.Delete(other);
                }

                if (!SamePath(s.ReShadeDll, target)) File.Copy(s.ReShadeDll, target, true);
                WriteIni(s);
            }
            catch (UnauthorizedAccessException) { return Fail("err_access", s.EngineFolder); }
            catch (IOException ex) when ((ex.HResult & 0xFFFF) is 32 or 33) { return Fail("err_locked"); }
            catch (IOException ex) { return Fail("err_io", ex.Message); }

            return new Outcome(true, "ok_installed", new object[] { s.EngineFolder });
        }

        public static Outcome Remove(BridgeSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.EngineFolder)) return Fail("err_noengine");
            var removed = false;
            try
            {
                foreach (var name in ProxyNames)
                {
                    var file = Path.Combine(s.EngineFolder, name + ".dll");
                    if (!File.Exists(file)) continue;
                    if (!PeInfo.LooksLikeReShade(file)) continue;   // never touch a dll that is not ReShade
                    File.Delete(file);
                    removed = true;
                }
            }
            catch (UnauthorizedAccessException) { return Fail("err_access", s.EngineFolder); }
            catch (IOException ex) when ((ex.HResult & 0xFFFF) is 32 or 33) { return Fail("err_locked"); }
            catch (IOException ex) { return Fail("err_io", ex.Message); }

            return removed ? new Outcome(true, "ok_removed", new object[] { s.EngineFolder }) : Fail("info_nothing");
        }

        /// <summary>The version of the ReShade dll installed in the engine folder, or null when there is none.</summary>
        public static string? InstalledVersion(BridgeSettings s)
        {
            if (string.IsNullOrWhiteSpace(s.EngineFolder)) return null;
            try
            {
                var file = Path.Combine(s.EngineFolder, ProxyFile(s));
                return File.Exists(file) && PeInfo.LooksLikeReShade(file) ? PeInfo.Version(file) ?? "?" : null;
            }
            catch { return null; }
        }

        private static bool SamePath(string a, string b) =>
            string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);

        private static void WriteIni(BridgeSettings s)
        {
            var iniPath = Path.Combine(s.EngineFolder, "ReShade.ini");

            // The user's root for shaders, textures and the preset: the folder they chose, else the one beside the dll.
            var root = !string.IsNullOrWhiteSpace(s.ReShadeFolder) ? s.ReShadeFolder : (Path.GetDirectoryName(Path.GetFullPath(s.ReShadeDll)) ?? "");

            if (!File.Exists(iniPath))
            {
                // Start from the user's own ReShade.ini when there is one next to their ReShade.
                var seed = Path.Combine(root, "ReShade.ini");
                if (root.Length > 0 && File.Exists(seed) && !SamePath(seed, iniPath)) File.Copy(seed, iniPath);
            }

            var ini = IniFile.Load(iniPath);

            var shaders = FirstDir(root, new[] { "reshade-shaders", "Shaders" }, new[] { "Shaders" });
            var textures = FirstDir(root, new[] { "reshade-shaders", "Textures" }, new[] { "Textures" });
            // "\**" at the end means "and every folder inside", as ReShade's own default has it.
            if (shaders != null) ini.Set("GENERAL", "EffectSearchPaths", shaders + Path.DirectorySeparatorChar + "**");
            if (textures != null) ini.Set("GENERAL", "TextureSearchPaths", textures + Path.DirectorySeparatorChar + "**");

            var preset = Path.Combine(root, "ReShadePreset.ini");
            if (root.Length > 0 && (File.Exists(preset) || !string.IsNullOrWhiteSpace(s.ReShadeFolder)))
                ini.Set("GENERAL", "PresetPath", preset);

            ini.Set("INPUT", "KeyOverlay", s.OverlayKey + ",0,0,0");
            ini.Save(iniPath);
        }

        private static string? FirstDir(string root, params string[][] options)
        {
            if (root.Length == 0) return null;
            foreach (var parts in options)
            {
                var d = root;
                foreach (var part in parts) d = Path.Combine(d, part);
                if (Directory.Exists(d)) return d;
            }
            return null;
        }
    }
}
