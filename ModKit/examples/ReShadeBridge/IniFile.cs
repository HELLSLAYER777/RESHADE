using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ReShadeBridge
{
    /// <summary>A small reader/writer for ReShade.ini that changes only the keys asked for and keeps everything else as it was.</summary>
    internal sealed class IniFile
    {
        private readonly List<string> lines = new();

        public static IniFile Load(string path)
        {
            var ini = new IniFile();
            if (File.Exists(path)) ini.lines.AddRange(File.ReadAllLines(path, new UTF8Encoding(false)));
            return ini;
        }

        private static bool IsSection(string line, out string name)
        {
            var t = line.Trim();
            if (t.Length >= 2 && t[0] == '[' && t[^1] == ']') { name = t.Substring(1, t.Length - 2); return true; }
            name = "";
            return false;
        }

        public string? Get(string section, string key)
        {
            var inSection = false;
            foreach (var line in lines)
            {
                if (IsSection(line, out var name)) { inSection = string.Equals(name, section, StringComparison.OrdinalIgnoreCase); continue; }
                if (!inSection) continue;
                var eq = line.IndexOf('=');
                if (eq > 0 && string.Equals(line.Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase)) return line.Substring(eq + 1);
            }
            return null;
        }

        public void Set(string section, string key, string value)
        {
            int start = -1, end = lines.Count;
            for (var i = 0; i < lines.Count; i++)
            {
                if (!IsSection(lines[i], out var name)) continue;
                if (start >= 0) { end = i; break; }
                if (string.Equals(name, section, StringComparison.OrdinalIgnoreCase)) start = i;
            }

            if (start < 0)
            {
                if (lines.Count > 0 && lines[^1].Trim().Length > 0) lines.Add("");
                lines.Add("[" + section + "]");
                lines.Add(key + "=" + value);
                return;
            }

            for (var i = start + 1; i < end; i++)
            {
                var eq = lines[i].IndexOf('=');
                if (eq > 0 && string.Equals(lines[i].Substring(0, eq).Trim(), key, StringComparison.OrdinalIgnoreCase))
                {
                    lines[i] = key + "=" + value;
                    return;
                }
            }

            var at = end;
            while (at > start + 1 && lines[at - 1].Trim().Length == 0) at--;
            lines.Insert(at, key + "=" + value);
        }

        public void Save(string path) =>
            File.WriteAllText(path, string.Join("\r\n", lines) + "\r\n", new UTF8Encoding(false));
    }
}
