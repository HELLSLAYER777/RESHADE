using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ReShadeBridge
{
    [StructLayout(LayoutKind.Sequential)]
    internal struct RECT
    {
        public int Left, Top, Right, Bottom;
        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct PROCESSENTRY32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public UIntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szExeFile;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MOUSEINPUT
    {
        public int dx, dy;
        public uint mouseData, dwFlags, time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KEYBDINPUT
    {
        public ushort wVk, wScan;
        public uint dwFlags, time;
        public UIntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    internal readonly record struct ProcInfo(uint Pid, uint ParentPid, string Exe);

    /// <summary>The few Win32 calls the bridge needs (window styles, focus, key presses, process list).</summary>
    internal static class Native
    {
        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TRANSPARENT = 0x00000020L;
        public const long WS_EX_NOACTIVATE = 0x08000000L;
        public const uint SWP_NOSIZE = 0x0001, SWP_NOMOVE = 0x0002, SWP_NOZORDER = 0x0004, SWP_NOACTIVATE = 0x0010, SWP_FRAMECHANGED = 0x0020;
        public const uint WM_KEYDOWN = 0x0100, WM_KEYUP = 0x0101;
        public const byte VK_MENU = 0x12;
        public const uint KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2;
        public const int SW_RESTORE = 9;

        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc cb, IntPtr lParam);
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern void SwitchToThisWindow(IntPtr hWnd, bool altTab);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool attach);
        [DllImport("user32.dll")] public static extern short GetAsyncKeyState(int vk);
        [DllImport("user32.dll")] public static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern uint MapVirtualKeyW(uint code, uint mapType);
        [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);
        [DllImport("user32.dll", SetLastError = true)] public static extern uint SendInput(uint count, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")] private static extern int GetWindowLong32(IntPtr hWnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")] private static extern int SetWindowLong32(IntPtr hWnd, int index, int value);

        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint pid);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32FirstW(IntPtr snap, ref PROCESSENTRY32W entry);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern bool Process32NextW(IntPtr snap, ref PROCESSENTRY32W entry);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

        public static long GetExStyle(IntPtr hWnd) =>
            IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, GWL_EXSTYLE).ToInt64() : GetWindowLong32(hWnd, GWL_EXSTYLE);

        public static void SetExStyle(IntPtr hWnd, long style)
        {
            if (IntPtr.Size == 8) SetWindowLongPtr64(hWnd, GWL_EXSTYLE, new IntPtr(style));
            else SetWindowLong32(hWnd, GWL_EXSTYLE, unchecked((int)style));
        }

        /// <summary>Make a window re-read its extended style.</summary>
        public static void ApplyStyle(IntPtr hWnd) =>
            SetWindowPos(hWnd, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

        /// <summary>The visible top-level windows that belong to any of the given processes.</summary>
        public static List<IntPtr> VisibleWindowsOf(ICollection<uint> pids)
        {
            var result = new List<IntPtr>();
            EnumWindows((h, _) =>
            {
                GetWindowThreadProcessId(h, out var pid);
                if (pids.Contains(pid) && IsWindowVisible(h)) result.Add(h);
                return true;
            }, IntPtr.Zero);
            return result;
        }

        /// <summary>Every running process with its parent (a Toolhelp snapshot).</summary>
        public static List<ProcInfo> Snapshot()
        {
            var list = new List<ProcInfo>();
            var snap = CreateToolhelp32Snapshot(0x2, 0);
            if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return list;
            try
            {
                var e = new PROCESSENTRY32W { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32W>() };
                if (Process32FirstW(snap, ref e))
                {
                    do { list.Add(new ProcInfo(e.th32ProcessID, e.th32ParentProcessID, e.szExeFile ?? "")); }
                    while (Process32NextW(snap, ref e));
                }
            }
            finally { CloseHandle(snap); }
            return list;
        }

        /// <summary>Bring a window of another process to the front (Windows only allows this in certain cases, so it tries a few ways).</summary>
        public static bool ForceForeground(IntPtr target)
        {
            if (!IsWindow(target)) return false;
            if (IsIconic(target)) ShowWindow(target, SW_RESTORE);
            var fg = GetForegroundWindow();
            if (fg == target) return true;

            var me = GetCurrentThreadId();
            uint fgThread = fg != IntPtr.Zero ? GetWindowThreadProcessId(fg, out _) : 0;
            var attached = fgThread != 0 && fgThread != me && AttachThreadInput(me, fgThread, true);
            try
            {
                BringWindowToTop(target);
                SetForegroundWindow(target);
            }
            finally { if (attached) AttachThreadInput(me, fgThread, false); }
            if (GetForegroundWindow() == target) return true;

            // A tap of Alt lets SetForegroundWindow through when Windows refuses the first try.
            keybd_event(VK_MENU, 0x38, 0, UIntPtr.Zero);
            keybd_event(VK_MENU, 0x38, KEYEVENTF_KEYUP, UIntPtr.Zero);
            SetForegroundWindow(target);
            if (GetForegroundWindow() == target) return true;

            SwitchToThisWindow(target, true);
            return GetForegroundWindow() == target;
        }

        /// <summary>A real key press (down or up) sent to whatever window has the keyboard.</summary>
        public static bool SendKey(int vk, bool up, bool extended)
        {
            var scan = (ushort)MapVirtualKeyW((uint)vk, 0);
            var input = new INPUT { type = 1 };
            input.u.ki = new KEYBDINPUT
            {
                wVk = (ushort)vk,
                wScan = scan,
                dwFlags = (up ? KEYEVENTF_KEYUP : 0u) | (extended ? KEYEVENTF_EXTENDEDKEY : 0u),
                time = 0,
                dwExtraInfo = UIntPtr.Zero
            };
            return SendInput(1, new[] { input }, Marshal.SizeOf<INPUT>()) == 1;
        }

        /// <summary>The same key as a window message, for when the engine window could not be brought to the front.</summary>
        public static void PostKey(IntPtr hWnd, int vk, bool up, bool extended)
        {
            var scan = MapVirtualKeyW((uint)vk, 0);
            long lParam = 1 | ((long)scan << 16) | (extended ? 1L << 24 : 0);
            if (up) lParam |= (1L << 30) | (1L << 31);
            PostMessageW(hWnd, up ? WM_KEYUP : WM_KEYDOWN, new IntPtr(vk), new IntPtr(lParam));
        }
    }
}
