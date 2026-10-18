// MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
using System;
using System.IO;

namespace MapleExpTracker
{
    /// <summary>
    /// tracker.log next to the exe: level changes, re-syncs and ignored readings, with the text that
    /// was read, so odd behaviour can be traced. Kept under ~512 KB (older lines go to tracker.old.log).
    /// </summary>
    static class TrackerLog
    {
        const long MaxBytes = 512 * 1024;
        static string path;

        public static void Write(string line)
        {
            try
            {
                if (path == null) path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tracker.log");
                var info = new FileInfo(path);
                if (info.Exists && info.Length > MaxBytes)
                {
                    string old = Path.Combine(info.DirectoryName, "tracker.old.log");
                    if (File.Exists(old)) File.Delete(old);
                    File.Move(path, old);
                }
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + line + "\r\n");
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
                // Read-only folder: fall back to %APPDATA%\MapleExpTracker.
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MapleExpTracker");
                string fallback = Path.Combine(dir, "tracker.log");
                if (path == fallback) return;
                try
                {
                    Directory.CreateDirectory(dir);
                    path = fallback;
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }
}
