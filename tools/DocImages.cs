// MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace MapleExpTracker.Tools
{
    /// <summary>
    /// Draws the images for the public download page with the app's own code.
    /// Usage: DocImages.exe outputDir fixturesDir      (build with "build.bat docs")
    /// </summary>
    static class DocImages
    {
        static string outDir, fixtures;

        [STAThread]
        static int Main(string[] args)
        {
            outDir = args[0];
            fixtures = args[1];
            Directory.CreateDirectory(outDir);
            var renderer = new OverlayRenderer();
            OverlayView live = Sample();

            renderer.Palette = Palette.Default;
            Save(renderer.Render(live, 2f), "hero.png");
            foreach (Palette p in Palette.All)
            {
                renderer.Palette = p;
                Save(renderer.Render(live, 2f), "theme-" + p.Name + ".png");
            }
            renderer.Palette = Palette.Default;
            HowItReads(renderer, live);
            OcrTestWindow();
            SettingsMenu(renderer, live);
            Console.WriteLine("Images written to " + outDir);
            return 0;
        }

        /// <summary>Numbers that match the real capture in tests\fixtures (level 41, 194,111 EXP).</summary>
        static OverlayView Sample()
        {
            long total = ExpTable.Required(41), exp = 194111;
            double rate = 148600;
            return new OverlayView
            {
                Name = "Aydrian", Level = 41, HasExp = true, Exp = exp, LevelTotal = total, LevelTotalExact = true,
                Percent = 59.87, Recent = Fmt.Signed(12800), Rate = Fmt.Short(rate), RateHour = Fmt.Short(151200),
                Session = Fmt.Signed(286400), SessionTime = Fmt.Clock(new TimeSpan(1, 54, 12)),
                Eta = Fmt.Duration(TimeSpan.FromHours((total - exp) / rate)), EtaLabel = "TO LV. 42", Status = OverlayStatus.Live
            };
        }

        static void Save(Bitmap bmp, string name)
        {
            using (bmp) bmp.Save(Path.Combine(outDir, name), ImageFormat.Png);
            Console.WriteLine("  " + name);
        }

        static readonly Color Card = Color.FromArgb(0x16, 0x1B, 0x22);
        static readonly Color Gold = Color.FromArgb(0xEB, 0xC4, 0x7C);
        static readonly Color Soft = Color.FromArgb(0xC9, 0xD1, 0xD9);

        /// <summary>The game's EXP bar (a real capture) and the tracker it turns into.</summary>
        static void HowItReads(OverlayRenderer renderer, OverlayView live)
        {
            using (var game = new Bitmap(Path.Combine(fixtures, "exp_194111_59.87.png")))
            using (Bitmap tracker = renderer.Render(live, 1.5f))
            {
                const int zoom = 2, pad = 36;
                int gameW = game.Width * zoom, gameH = game.Height * zoom;
                int width = Math.Max(gameW, tracker.Width) + pad * 2, height = pad + 30 + gameH + 30 + 90 + 30 + tracker.Height + pad;
                var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(bmp))
                using (var title = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var note = new Font("Segoe UI", 14f, FontStyle.Regular, GraphicsUnit.Pixel))
                using (var gold = new SolidBrush(Gold))
                using (var soft = new SolidBrush(Soft))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    using (GraphicsPath card = Shapes.RoundRect(new RectangleF(0, 0, width - 1, height - 1), 18f))
                    using (var b = new SolidBrush(Card))
                        g.FillPath(b, card);

                    int y = pad;
                    g.DrawString("1.  Your EXP bar in game", title, gold, pad, y);
                    y += 30;
                    int gx = (width - gameW) / 2;
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(game, new Rectangle(gx, y, gameW, gameH));
                    g.PixelOffsetMode = PixelOffsetMode.Default;
                    // The box you draw once with "Select EXP Region".
                    using (var pen = new Pen(Gold, 2.5f) { DashStyle = DashStyle.Dash })
                        g.DrawRectangle(pen, gx + 66 * zoom, y + 28 * zoom, 182 * zoom, 22 * zoom);
                    y += gameH + 8;
                    const string caption = "The dashed box is the region you select once. It's read every second.";
                    SizeF size = g.MeasureString(caption, note);
                    g.DrawString(caption, note, soft, (width - size.Width) / 2, y);
                    y += 42;

                    // Arrow
                    float ax = width / 2f;
                    using (var pen = new Pen(Gold, 4f) { EndCap = LineCap.ArrowAnchor })
                        g.DrawLine(pen, ax, y, ax, y + 46);
                    y += 70;

                    g.DrawString("2.  The tracker", title, gold, pad, y);
                    y += 30;
                    g.DrawImage(tracker, (width - tracker.Width) / 2, y);
                }
                Save(bmp, "how-it-reads.png");
            }
        }

        /// <summary>The "Test OCR" window, fed with the real capture through the real reading code.</summary>
        static void OcrTestWindow()
        {
            string error;
            OcrService.TryInit(out error);
            using (var raw = new Bitmap(Path.Combine(fixtures, "exp_194111_59.87.png")))
            using (var form = new OcrTestForm())
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(-3000, -3000);
                form.Show();
                var tracker = new ExpTracker();
                DateTime now = DateTime.UtcNow;
                int preferred = 0;
                OcrRead read = null;
                for (int i = 0; i < 2; i++)
                {
                    read = OcrService.Read(raw, ref preferred, true);
                    tracker.Feed(read.Candidates, now.AddSeconds(i));
                    if (i == 0) read.Processed.Dispose();
                }
                var frame = new OcrFrame
                {
                    Region = new Rectangle(1778, 1344, raw.Width, raw.Height), Text = read.Text, Filter = read.Filter,
                    Milliseconds = read.Milliseconds, Candidates = read.Candidates, Raw = new Bitmap(raw), Processed = read.Processed
                };
                form.ShowFrame(frame, tracker);
                Application.DoEvents();
                var bmp = new Bitmap(form.Width, form.Height);
                form.DrawToBitmap(bmp, new Rectangle(Point.Empty, form.Size));
                Save(bmp, "ocr-test.png");
                form.Close();
            }
        }

        [DllImport("gdi32.dll")]
        static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);

        /// <summary>
        /// The real settings menu, opened from the gear with the Theme list expanded. Shown over a plain
        /// backdrop window so nothing else on the screen ends up in the picture.
        /// </summary>
        static void SettingsMenu(OverlayRenderer renderer, OverlayView live)
        {
            BindingFlags any = BindingFlags.Instance | BindingFlags.NonPublic;
            using (Bitmap tracker = renderer.Render(live, 1f))
            using (var overlay = new OverlayForm())   // not shown; only its menu is used
            using (var backdrop = new Form())
            {
                var menu = (ContextMenuStrip)typeof(OverlayForm).GetField("menu", any).GetValue(overlay);
                var themes = (ToolStripMenuItem)typeof(OverlayForm).GetField("themeMenu", any).GetValue(overlay);
                var tray = (NotifyIcon)typeof(OverlayForm).GetField("tray", any).GetValue(overlay);
                tray.Visible = false;

                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                var area = new Rectangle(wa.Left + 80, wa.Top + 80, 700, 620);
                backdrop.FormBorderStyle = FormBorderStyle.None;
                backdrop.StartPosition = FormStartPosition.Manual;
                backdrop.ShowInTaskbar = false;
                backdrop.TopMost = true;
                backdrop.Bounds = area;
                backdrop.BackColor = Card;
                backdrop.Paint += (s, e) => e.Graphics.DrawImage(tracker, 10, 10);
                backdrop.Show();
                Pump(300);

                RectangleF gear = renderer.SettingsArea;
                var anchor = new Point(area.Left + 10 + (int)gear.Right, area.Top + 10 + (int)gear.Bottom + 2);
                menu.Show(anchor, ToolStripDropDownDirection.BelowLeft);
                // The overlay itself isn't shown here, so the menu thinks it's hidden.
                ((ToolStripMenuItem)typeof(OverlayForm).GetField("visibleItem", any).GetValue(overlay)).Text = "Hide Tracker";
                Pump(300);
                themes.ShowDropDown();
                Pump(600);

                var bmp = new Bitmap(area.Width, area.Height);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    IntPtr screen = GetDC(IntPtr.Zero), dst = g.GetHdc();
                    BitBlt(dst, 0, 0, area.Width, area.Height, screen, area.Left, area.Top, 0x00CC0020 | 0x40000000);
                    g.ReleaseHdc(dst);
                    ReleaseDC(IntPtr.Zero, screen);
                }
                menu.Close();
                backdrop.Close();
                Save(Trim(bmp, Card), "settings-menu.png");
            }
        }

        static void Pump(int ms)
        {
            for (int t = 0; t < ms; t += 20)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
        }

        /// <summary>Crops away backdrop-colored margins on the right and bottom.</summary>
        static Bitmap Trim(Bitmap bmp, Color background)
        {
            int right = 0, bottom = 0;
            for (int y = 0; y < bmp.Height; y++)
                for (int x = 0; x < bmp.Width; x++)
                {
                    Color c = bmp.GetPixel(x, y);
                    if (Math.Abs(c.R - background.R) + Math.Abs(c.G - background.G) + Math.Abs(c.B - background.B) > 12)
                    {
                        if (x > right) right = x;
                        if (y > bottom) bottom = y;
                    }
                }
            var rect = new Rectangle(0, 0, Math.Min(bmp.Width, right + 12), Math.Min(bmp.Height, bottom + 12));
            Bitmap cropped = bmp.Clone(rect, PixelFormat.Format32bppArgb);
            bmp.Dispose();
            return cropped;
        }
    }
}
