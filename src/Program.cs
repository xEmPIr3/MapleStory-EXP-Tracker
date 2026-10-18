// MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace MapleExpTracker
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            // Developer helpers: render the overlay to a PNG, or write the exe icon.
            if (args.Length >= 2 && args[0] == "--preview")
            {
                RenderPreview(args[1]);
                return 0;
            }
            if (args.Length >= 2 && args[0] == "--write-icon")
            {
                AppIcon.WriteIco(args[1]);
                return 0;
            }

            bool created;
            using (var mutex = new Mutex(true, "MapleExpTracker.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("The EXP Tracker is already running. Look for its icon in the system tray.",
                        "MapleStory EXP Tracker", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 1;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += (s, e) => ReportError(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportError(e.ExceptionObject as Exception);
                Application.Run(new OverlayForm());
            }
            return 0;
        }

        static void ReportError(Exception ex)
        {
            if (ex == null) return;
            string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
            try
            {
                File.AppendAllText(log, DateTime.Now + "\r\n" + ex + "\r\n\r\n");
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            MessageBox.Show("Something went wrong: " + ex.Message + "\n\nDetails were written to error.log.",
                "MapleStory EXP Tracker", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        static void RenderPreview(string path)
        {
            var renderer = new OverlayRenderer();
            var live = new OverlayView
            {
                Name = "Aydrian", Level = 42, HasExp = true, Exp = 104741, LevelTotal = ExpTable.Required(42), LevelTotalExact = true,
                Percent = 29.35, Recent = "+13.5K", Rate = "151.8K", RateHour = "148.2K", Session = "+316.3K",
                SessionTime = "2:07:41", Eta = "1h 41m", EtaLabel = "TO LV. 43", Status = OverlayStatus.Live
            };
            var high = new OverlayView
            {
                Name = "Aydrian", Level = 99, HasExp = true, Exp = 4515315023, LevelTotal = ExpTable.Required(99), LevelTotalExact = true,
                Percent = 47.62, Recent = "+1.25M", Rate = "14.9M", RateHour = "15.2M", Session = "+82.3M",
                SessionTime = "5:31:02", Eta = "12d 21h", EtaLabel = "TO LV. 100", Status = OverlayStatus.Live, SettingsHover = true
            };
            // Every theme, two per row; the second column shows other states.
            OverlayView[] second =
            {
                high,
                new OverlayView { Message = "Click the gear → Select EXP Region", Status = OverlayStatus.NoRegion },
                new OverlayView { Level = 42, Status = OverlayStatus.Waiting, Message = "Reading EXP…" }
            };
            int cellW = OverlayRenderer.BaseWidth + 20, cellH = OverlayRenderer.BaseHeight + 34;
            int rows = Palette.All.Length;
            using (var canvas = new Bitmap(cellW * 2 + 20, cellH * rows + 10, PixelFormat.Format32bppArgb))
            using (var label = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    using (var bg = new LinearGradientBrush(new Rectangle(0, 0, canvas.Width, canvas.Height),
                        Color.FromArgb(120, 180, 230), Color.FromArgb(70, 130, 60), 90f))
                        g.FillRectangle(bg, 0, 0, canvas.Width, canvas.Height);
                    for (int i = 0; i < rows; i++)
                    {
                        renderer.Palette = Palette.All[i];
                        int y = 10 + i * cellH;
                        g.DrawString(Palette.All[i].DisplayName, label, Brushes.White, 24, y);
                        using (Bitmap one = renderer.Render(live, 1f)) g.DrawImage(one, 20, y + 18);
                        using (Bitmap two = renderer.Render(second[i % second.Length], 1f)) g.DrawImage(two, 20 + cellW, y + 18);
                    }
                }
                canvas.Save(path, ImageFormat.Png);
            }
        }
    }
}
