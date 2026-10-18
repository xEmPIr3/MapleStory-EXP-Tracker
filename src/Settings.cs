using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;

namespace MapleExpTracker
{
    /// <summary>settings.ini next to the exe (or in %APPDATA%\MapleExpTracker if that folder is read-only).</summary>
    public sealed class Settings
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public Rectangle Region = Rectangle.Empty;
        public int Level;
        public string Name = "";
        public bool HasPosition;
        public int X, Y;
        public float Scale = 1f;
        public float Opacity = 1f;
        public bool ClickThrough;
        public string Theme = "default";
        public int IntervalMs = 1000;

        static string LocalFile
        {
            get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"); }
        }

        static string RoamingFile
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MapleExpTracker", "settings.ini"); }
        }

        public static Settings Load()
        {
            var s = new Settings();
            string file = File.Exists(LocalFile) ? LocalFile : File.Exists(RoamingFile) ? RoamingFile : null;
            if (file == null) return s;
            try
            {
                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (string line in File.ReadAllLines(file))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0) values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
                int[] r = Ints(Get(values, "region"));
                if (r != null && r.Length == 4 && r[2] > 0 && r[3] > 0) s.Region = new Rectangle(r[0], r[1], r[2], r[3]);
                int[] p = Ints(Get(values, "position"));
                if (p != null && p.Length == 2)
                {
                    s.HasPosition = true;
                    s.X = p[0];
                    s.Y = p[1];
                }
                int.TryParse(Get(values, "level"), NumberStyles.Integer, Inv, out s.Level);
                s.Name = Get(values, "name") ?? "";
                s.Scale = Clamp(Float(Get(values, "scale"), 1f), 0.5f, 3f);
                s.Opacity = Clamp(Float(Get(values, "opacity"), 1f), 0.3f, 1f);
                s.ClickThrough = Get(values, "click_through") == "1";
                s.Theme = Get(values, "theme") ?? s.Theme;
                int interval;
                if (int.TryParse(Get(values, "interval_ms"), NumberStyles.Integer, Inv, out interval))
                    s.IntervalMs = Math.Max(250, Math.Min(10000, interval));
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            return s;
        }

        public void Save()
        {
            var lines = new List<string>();
            lines.Add("; MapleStory EXP Tracker settings");
            if (!Region.IsEmpty) lines.Add("region=" + Region.X + "," + Region.Y + "," + Region.Width + "," + Region.Height);
            if (HasPosition) lines.Add("position=" + X + "," + Y);
            lines.Add("level=" + Level.ToString(Inv));
            lines.Add("name=" + (Name ?? "").Replace("\r", "").Replace("\n", ""));
            lines.Add("scale=" + Scale.ToString(Inv));
            lines.Add("opacity=" + Opacity.ToString(Inv));
            lines.Add("click_through=" + (ClickThrough ? "1" : "0"));
            lines.Add("theme=" + Theme);
            lines.Add("; how often to read the EXP bar, in milliseconds");
            lines.Add("interval_ms=" + IntervalMs.ToString(Inv));
            string[] text = lines.ToArray();
            try
            {
                File.WriteAllLines(LocalFile, text);
                return;
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(RoamingFile));
                File.WriteAllLines(RoamingFile, text);
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (IOException)
            {
            }
        }

        static string Get(Dictionary<string, string> values, string key)
        {
            string v;
            return values.TryGetValue(key, out v) ? v : null;
        }

        static int[] Ints(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            string[] parts = text.Split(',');
            var result = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!int.TryParse(parts[i].Trim(), NumberStyles.Integer, Inv, out result[i])) return null;
            return result;
        }

        static float Float(string text, float fallback)
        {
            float v;
            return float.TryParse(text, NumberStyles.Float, Inv, out v) ? v : fallback;
        }

        static float Clamp(float v, float min, float max)
        {
            return v < min ? min : v > max ? max : v;
        }
    }
}
