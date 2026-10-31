using System;

namespace ReShadeBridge
{
    /// <summary>The keys that can open the ReShade overlay (ReShade's own default is Home).</summary>
    internal static class OverlayKeys
    {
        public static readonly (string Name, int Vk)[] All =
        {
            ("Home", 0x24), ("End", 0x23), ("Insert", 0x2D), ("Delete", 0x2E),
            ("Page Up", 0x21), ("Page Down", 0x22), ("Pause", 0x13), ("Scroll Lock", 0x91),
            ("F9", 0x78), ("F10", 0x79), ("F11", 0x7A), ("F12", 0x7B)
        };

        public static int IndexOf(int vk)
        {
            for (var i = 0; i < All.Length; i++) if (All[i].Vk == vk) return i;
            return 0;
        }

        public static string NameOf(int vk) => All[IndexOf(vk)].Name;

        /// <summary>The navigation-cluster keys are "extended" keys for the keyboard driver.</summary>
        public static bool IsExtended(int vk) => vk is (>= 0x21 and <= 0x28) or 0x2D or 0x2E;
    }
}
