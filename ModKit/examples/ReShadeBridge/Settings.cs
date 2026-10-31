using System;
using System.IO;
using System.Text.Json;

namespace ReShadeBridge
{
    /// <summary>What the mod remembers, in its own data folder (reshade-bridge.json).</summary>
    internal sealed class BridgeSettings
    {
        public bool Enabled { get; set; } = true;
        public string ReShadeDll { get; set; } = "";
        public string ReShadeFolder { get; set; } = "";
        public string EngineFolder { get; set; } = "";
        public string Proxy { get; set; } = "dxgi";
        public int OverlayKey { get; set; } = 0x24;
        public bool OnlyWhenGameInFront { get; set; } = true;

        private const string FileName = "reshade-bridge.json";

        public static BridgeSettings Load(string folder)
        {
            try
            {
                var path = Path.Combine(folder, FileName);
                if (File.Exists(path))
                    return JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(path)) ?? new BridgeSettings();
            }
            catch { /* a damaged file: start again with the defaults */ }
            return new BridgeSettings();
        }

        public void Save(string folder)
        {
            try
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, FileName), JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* settings are a convenience; never stop the app for them */ }
        }
    }
}
