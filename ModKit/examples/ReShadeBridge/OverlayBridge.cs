using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ReShadeBridge
{
    /// <summary>
    /// Makes the ReShade overlay usable on the engine's output window.
    ///
    /// The engine's window is made to let clicks and keys through to the game, so ReShade (which lives inside the engine)
    /// never sees the overlay key or the mouse. This bridge watches the key from outside: when it is pressed over the
    /// game it makes the engine window take the keyboard and mouse, and gives ReShade the key so its overlay opens;
    /// when the key is pressed again the overlay closes and the engine window goes back to letting everything through
    /// to the game.
    ///
    /// Call <see cref="Tick"/> about every 30 ms on the window's thread.
    /// </summary>
    internal sealed class OverlayBridge
    {
        private static readonly string[] ProxyNames = { "dxgi.dll", "d3d11.dll", "d3d12.dll" };

        private readonly Func<bool> motionRunning;
        private readonly Action<string, object[]> notice;
        private readonly Action<string> log;

        public OverlayBridge(Func<bool> motionRunning, Action<string, object[]> notice, Action<string> log)
        {
            this.motionRunning = motionRunning;
            this.notice = notice;
            this.log = log;
        }

        public BridgeSettings Settings { get; set; } = new();

        /// <summary>The engine was seen running from this folder.</summary>
        public event Action<string>? EngineFolderSeen;

        public EngineInfo? Engine { get; private set; }
        public bool HasWindow => engineWnd != IntPtr.Zero;
        public bool? ReShadeLoaded { get; private set; }
        public bool Interactive => interactive;

        private HashSet<uint> pids = new();
        private IntPtr engineWnd;
        private DateTime nextScan = DateTime.MinValue;
        private DateTime nextLoadedCheck = DateTime.MinValue;

        private bool keyWasDown;
        private bool interactive;
        private long originalStyle;
        private IntPtr previousForeground;
        private int step;                                   // 0 nothing to send, 1 key down due, 2 key up due
        private DateTime stepAt = DateTime.MaxValue;
        private DateTime exitAt = DateTime.MaxValue;
        private DateTime lostForegroundSince = DateTime.MaxValue;
        private DateTime ignoreKeyUntil = DateTime.MinValue;

        // ------------------------------------------------------------------ the tick

        public void Tick()
        {
            var now = DateTime.UtcNow;

            var down = (Native.GetAsyncKeyState(Settings.OverlayKey) & 0x8000) != 0;
            var pressed = down && !keyWasDown && now >= ignoreKeyUntil;
            keyWasDown = down;

            if (!Settings.Enabled && !interactive) return;

            Scan(false);

            if (interactive)
            {
                if (engineWnd == IntPtr.Zero || !Native.IsWindow(engineWnd) || !motionRunning()) { End(false); return; }

                if (step == 1 && now >= stepAt)
                {
                    SendOverlayKey(false);
                    step = 2;
                    stepAt = now.AddMilliseconds(40);
                }
                else if (step == 2 && now >= stepAt)
                {
                    SendOverlayKey(true);
                    step = 0;
                    ignoreKeyUntil = now.AddMilliseconds(250);   // our own key press must not count as the user's
                }

                // The user pressed the key again: the engine window has the keyboard, so ReShade got it; close up just after.
                if (pressed && step == 0) exitAt = now.AddMilliseconds(150);
                if (now >= exitAt && step == 0) { End(true); return; }

                // The user switched away on their own: stop holding the engine window open.
                if (Native.GetForegroundWindow() != engineWnd)
                {
                    if (lostForegroundSince == DateTime.MaxValue) lostForegroundSince = now;
                    else if ((now - lostForegroundSince).TotalMilliseconds > 900) End(false);
                }
                else lostForegroundSince = DateTime.MaxValue;
                return;
            }

            if (pressed && Settings.Enabled && motionRunning()) Begin(false);
        }

        /// <summary>For the "Open overlay now" button: open it if closed, close it if open.</summary>
        public void RequestToggle()
        {
            if (interactive)
            {
                if (step == 0)
                {
                    step = 1;
                    stepAt = DateTime.UtcNow;
                    exitAt = DateTime.UtcNow.AddMilliseconds(250);
                }
                return;
            }
            Begin(true);
        }

        public void Shutdown() => End(false);

        // ------------------------------------------------------------------ open / close

        private void Begin(bool manual)
        {
            Scan(true);
            if (Engine == null || engineWnd == IntPtr.Zero) { notice("info_nowindow_block", Array.Empty<object>()); return; }

            ReShadeLoaded = CheckLoaded();
            if (ReShadeLoaded == false) { notice("info_notloaded_block", Array.Empty<object>()); return; }

            if (!manual && Settings.OnlyWhenGameInFront && !GameInFront(engineWnd)) return;

            previousForeground = Native.GetForegroundWindow();
            originalStyle = Native.GetExStyle(engineWnd);
            var open = originalStyle & ~(Native.WS_EX_TRANSPARENT | Native.WS_EX_NOACTIVATE);
            if (open != originalStyle)
            {
                Native.SetExStyle(engineWnd, open);
                Native.ApplyStyle(engineWnd);
            }

            interactive = true;
            exitAt = DateTime.MaxValue;
            lostForegroundSince = DateTime.MaxValue;

            if (previousForeground == engineWnd)
            {
                // It already has the keyboard: the user's own key press went to ReShade, nothing to send.
                step = 0;
            }
            else
            {
                if (!Native.ForceForeground(engineWnd)) notice("info_focus", Array.Empty<object>());
                step = 1;
                stepAt = DateTime.UtcNow.AddMilliseconds(80);
            }
            log("overlay: engine window opened for ReShade");
        }

        private void End(bool refocusGame)
        {
            if (!interactive) return;
            interactive = false;
            step = 0;
            exitAt = DateTime.MaxValue;
            lostForegroundSince = DateTime.MaxValue;

            if (engineWnd != IntPtr.Zero && Native.IsWindow(engineWnd))
            {
                Native.SetExStyle(engineWnd, originalStyle);
                Native.ApplyStyle(engineWnd);
            }
            if (refocusGame && previousForeground != IntPtr.Zero && previousForeground != engineWnd && Native.IsWindow(previousForeground))
                Native.ForceForeground(previousForeground);

            log("overlay: engine window handed back to the game");
        }

        /// <summary>The overlay key, as a real key press when the engine window has the keyboard, else as a window message.</summary>
        private void SendOverlayKey(bool up)
        {
            var vk = Settings.OverlayKey;
            var extended = OverlayKeys.IsExtended(vk);
            var hasKeyboard = Native.GetForegroundWindow() == engineWnd;
            if (hasKeyboard) { if (Native.SendKey(vk, up, extended)) return; }
            Native.PostKey(engineWnd, vk, up, extended);
        }

        // ------------------------------------------------------------------ finding the engine

        private void Scan(bool force)
        {
            var now = DateTime.UtcNow;
            if (!force && now < nextScan) return;
            nextScan = now.AddMilliseconds(700);

            if (!motionRunning() && !interactive)
            {
                Engine = null;
                engineWnd = IntPtr.Zero;
                pids.Clear();
                ReShadeLoaded = null;
                return;
            }

            var list = EngineLocator.FindRunning(out _);
            var first = list.FirstOrDefault();
            if (first != null && (Engine == null || Engine.Pid != first.Pid))
            {
                ReShadeLoaded = null;
                nextLoadedCheck = DateTime.MinValue;
                if (!string.IsNullOrEmpty(first.Folder)) EngineFolderSeen?.Invoke(first.Folder);
            }
            Engine = first;
            pids = new HashSet<uint>(list.Select(e => e.Pid));

            if (!(interactive && engineWnd != IntPtr.Zero && Native.IsWindow(engineWnd)))
                engineWnd = pids.Count == 0 ? IntPtr.Zero : LargestWindow(pids);

            if (Engine != null && now >= nextLoadedCheck)
            {
                nextLoadedCheck = now.AddSeconds(3);
                ReShadeLoaded = CheckLoaded();
            }
        }

        private static IntPtr LargestWindow(HashSet<uint> pids)
        {
            IntPtr best = IntPtr.Zero;
            long bestArea = 200L * 150L;
            foreach (var h in Native.VisibleWindowsOf(pids))
            {
                if (!Native.GetWindowRect(h, out var r)) continue;
                var area = (long)r.Width * r.Height;
                if (area >= bestArea) { best = h; bestArea = area; }
            }
            return best;
        }

        /// <summary>Is ReShade's dll inside the engine process? (true / false; null when Windows will not tell.)</summary>
        private bool? CheckLoaded()
        {
            var e = Engine;
            if (e == null) return null;
            try
            {
                using var p = Process.GetProcessById((int)e.Pid);
                foreach (ProcessModule m in p.Modules)
                {
                    var file = m.FileName;
                    if (file == null) continue;
                    var name = Path.GetFileName(file);
                    if (Array.IndexOf(ProxyNames, name.ToLowerInvariant()) < 0) continue;
                    var dir = Path.GetDirectoryName(file) ?? "";
                    if (SameDir(dir, e.Folder) || SameDir(dir, Settings.EngineFolder)) return true;
                }
                return false;
            }
            catch { return null; }
        }

        private static bool SameDir(string a, string b) =>
            a.Length > 0 && b.Length > 0 &&
            string.Equals(a.TrimEnd('\\', '/'), b.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);

        /// <summary>The window in front covers at least half of the engine window (the game), and is not this app.</summary>
        private bool GameInFront(IntPtr wnd)
        {
            var fg = Native.GetForegroundWindow();
            if (fg == IntPtr.Zero) return false;
            if (fg == wnd) return true;
            Native.GetWindowThreadProcessId(fg, out var fgPid);
            if (fgPid == (uint)Environment.ProcessId) return false;
            if (!Native.GetWindowRect(fg, out var a) || !Native.GetWindowRect(wnd, out var b)) return false;
            long ix = Math.Max(0, Math.Min(a.Right, b.Right) - Math.Max(a.Left, b.Left));
            long iy = Math.Max(0, Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Top, b.Top));
            long area = (long)b.Width * b.Height;
            return area > 0 && ix * iy * 2 >= area;
        }
    }
}
