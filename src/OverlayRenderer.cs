using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;

namespace MapleExpTracker
{
    public enum OverlayStatus { Live, Waiting, NoRegion, Paused }

    /// <summary>Everything the overlay shows, already formatted.</summary>
    public sealed class OverlayView
    {
        public const string Dash = "—";

        public string Name = "";
        public int Level;               // 0 = unknown
        public bool HasExp;
        public long Exp;
        public long LevelTotal;         // EXP the level needs, 0 = unknown
        public bool LevelTotalExact;    // from the EXP table rather than estimated
        public double Percent = double.NaN;
        public string Message;          // shown in the EXP bar instead of the numbers
        public string Recent = Dash, Rate = Dash, RateHour = Dash, Session = Dash, SessionTime = Dash, Eta = Dash;
        public bool RecentNegative, SessionNegative;
        public string EtaLabel = "TO LEVEL UP";
        public OverlayStatus Status = OverlayStatus.Waiting;
        public bool SettingsHover;      // mouse over the settings button

        public string Key()
        {
            return string.Join("|", new[]
            {
                Name, Level.ToString(CultureInfo.InvariantCulture), HasExp ? Exp.ToString(CultureInfo.InvariantCulture) : "-",
                LevelTotal.ToString(CultureInfo.InvariantCulture) + (LevelTotalExact ? "" : "~"),
                Percent.ToString("0.0000", CultureInfo.InvariantCulture), Message ?? "", Recent, Rate, RateHour, Session,
                SessionTime, Eta, EtaLabel, Status.ToString(), RecentNegative ? "1" : "0", SessionNegative ? "1" : "0",
                SettingsHover ? "1" : "0"
            });
        }
    }

    /// <summary>A color theme. All themes are dark; they differ in their accent color.</summary>
    public sealed class Palette
    {
        public string Name, DisplayName;
        public Color Border, PanelTop, PanelBottom;
        public Color HeaderTop, HeaderBottom, HeaderSheen, HeaderLine, Title, NameText;
        public Color BadgeTop, BadgeBottom, BadgeBorder, BadgeText;
        public Color TrackTop, TrackBottom, TrackBorder;
        public Color Label, Value, ValueShadow, Negative, Missing, Divider;
        public Color TileTop, TileMid, TileBottom, TileBorder, TileDigitOutline;   // level digit tiles
        public Color FillTop, FillMid, FillBottom, FillEdge;                      // EXP bar fill
        public Color CoinTop, CoinBottom, CoinRim;                                // header icon

        static Color Rgb(int rgb)
        {
            return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }

        static Color Rgba(int alpha, int rgb)
        {
            return Color.FromArgb(alpha, Rgb(rgb));
        }

        /// <summary>The original look: brown and gold with a green EXP bar.</summary>
        public static readonly Palette Default = new Palette
        {
            Name = "default", DisplayName = "Default (Gold)",
            Border = Rgb(0xA27C47), PanelTop = Rgb(0x26211B), PanelBottom = Rgb(0x13110E),
            HeaderTop = Rgb(0x5B3F23), HeaderBottom = Rgb(0x2F2215), HeaderSheen = Rgba(55, 0xFFE1AA),
            HeaderLine = Rgb(0x0B0907), Title = Rgb(0xEBC47C), NameText = Rgb(0xF6ECDA),
            BadgeTop = Rgb(0x0F0D0B), BadgeBottom = Rgb(0x0F0D0B), BadgeBorder = Rgb(0x6A5334), BadgeText = Rgb(0xEFE4D0),
            TrackTop = Rgb(0x0A0908), TrackBottom = Rgb(0x0A0908), TrackBorder = Rgb(0x624E33),
            Label = Rgb(0xA8977D), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x8C8070), Divider = Rgb(0x3A3026),
            TileTop = Rgb(0xFFC46A), TileMid = Rgb(0xF99E44), TileBottom = Rgb(0xE07E22), TileBorder = Rgb(0x794C0E), TileDigitOutline = Rgb(0x7A3A06),
            FillTop = Rgb(0xECF885), FillMid = Rgb(0xC5E72E), FillBottom = Rgb(0x93C214), FillEdge = Rgb(0xFAFFC8),
            CoinTop = Rgb(0xFFDA74), CoinBottom = Rgb(0xE8801A), CoinRim = Rgb(0x6E3508)
        };

        public static readonly Palette Pink = new Palette
        {
            Name = "pink", DisplayName = "Pink",
            Border = Rgb(0xE07AB0), PanelTop = Rgb(0x2A1B24), PanelBottom = Rgb(0x150D12),
            HeaderTop = Rgb(0x6E2A50), HeaderBottom = Rgb(0x3A1529), HeaderSheen = Rgba(60, 0xFFC0E2),
            HeaderLine = Rgb(0x0D0609), Title = Rgb(0xFFB3DA), NameText = Rgb(0xFCE8F3),
            BadgeTop = Rgb(0x120A0E), BadgeBottom = Rgb(0x120A0E), BadgeBorder = Rgb(0x7A3A5C), BadgeText = Rgb(0xF8E1EE),
            TrackTop = Rgb(0x0C0709), TrackBottom = Rgb(0x0C0709), TrackBorder = Rgb(0x7A3A5C),
            Label = Rgb(0xC995B0), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF9A7A),
            Missing = Rgb(0x8E6F80), Divider = Rgb(0x432634),
            TileTop = Rgb(0xFFC2E2), TileMid = Rgb(0xFF6EB4), TileBottom = Rgb(0xD63A8A), TileBorder = Rgb(0x6E1442), TileDigitOutline = Rgb(0x6E1442),
            FillTop = Rgb(0xFFD1EA), FillMid = Rgb(0xFF7AC0), FillBottom = Rgb(0xD9418F), FillEdge = Rgb(0xFFE6F3),
            CoinTop = Rgb(0xFFD0E8), CoinBottom = Rgb(0xE85AA2), CoinRim = Rgb(0x6E1442)
        };

        public static readonly Palette Red = new Palette
        {
            Name = "red", DisplayName = "Red",
            Border = Rgb(0xD9534A), PanelTop = Rgb(0x2A1716), PanelBottom = Rgb(0x140B0A),
            HeaderTop = Rgb(0x701F1A), HeaderBottom = Rgb(0x3A100D), HeaderSheen = Rgba(60, 0xFFB4AA),
            HeaderLine = Rgb(0x0D0505), Title = Rgb(0xFF9C8F), NameText = Rgb(0xFBE9E6),
            BadgeTop = Rgb(0x120909), BadgeBottom = Rgb(0x120909), BadgeBorder = Rgb(0x7A2E28), BadgeText = Rgb(0xF6E0DC),
            TrackTop = Rgb(0x0C0606), TrackBottom = Rgb(0x0C0606), TrackBorder = Rgb(0x7A2E28),
            Label = Rgb(0xC8948C), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFFB36B),
            Missing = Rgb(0x8E6E6A), Divider = Rgb(0x45221F),
            TileTop = Rgb(0xFFB0A0), TileMid = Rgb(0xF2483A), TileBottom = Rgb(0xB81F17), TileBorder = Rgb(0x5E0C08), TileDigitOutline = Rgb(0x5E0C08),
            FillTop = Rgb(0xFFB4A8), FillMid = Rgb(0xF2564A), FillBottom = Rgb(0xB5231A), FillEdge = Rgb(0xFFDAD4),
            CoinTop = Rgb(0xFFB5A8), CoinBottom = Rgb(0xD9362B), CoinRim = Rgb(0x5E0C08)
        };

        public static readonly Palette Black = new Palette
        {
            Name = "black", DisplayName = "Black",
            Border = Rgb(0x8A8A8A), PanelTop = Rgb(0x171717), PanelBottom = Rgb(0x050505),
            HeaderTop = Rgb(0x303030), HeaderBottom = Rgb(0x121212), HeaderSheen = Rgba(45, 0xFFFFFF),
            HeaderLine = Rgb(0x000000), Title = Rgb(0xE6E6E6), NameText = Rgb(0xFFFFFF),
            BadgeTop = Rgb(0x0A0A0A), BadgeBottom = Rgb(0x0A0A0A), BadgeBorder = Rgb(0x4A4A4A), BadgeText = Rgb(0xEDEDED),
            TrackTop = Rgb(0x000000), TrackBottom = Rgb(0x000000), TrackBorder = Rgb(0x4A4A4A),
            Label = Rgb(0x9A9A9A), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF7A6A),
            Missing = Rgb(0x6A6A6A), Divider = Rgb(0x2C2C2C),
            TileTop = Rgb(0xFFFFFF), TileMid = Rgb(0xCFCFCF), TileBottom = Rgb(0x8C8C8C), TileBorder = Rgb(0x2A2A2A), TileDigitOutline = Rgb(0x1A1A1A),
            FillTop = Rgb(0xFFFFFF), FillMid = Rgb(0xC4C4C4), FillBottom = Rgb(0x808080), FillEdge = Rgb(0xFFFFFF),
            CoinTop = Rgb(0xF0F0F0), CoinBottom = Rgb(0x9A9A9A), CoinRim = Rgb(0x2A2A2A)
        };

        public static readonly Palette Orange = new Palette
        {
            Name = "orange", DisplayName = "Orange",
            Border = Rgb(0xE8923A), PanelTop = Rgb(0x2A1E14), PanelBottom = Rgb(0x140D08),
            HeaderTop = Rgb(0x7A4115), HeaderBottom = Rgb(0x3D1F0A), HeaderSheen = Rgba(60, 0xFFD2A0),
            HeaderLine = Rgb(0x0D0703), Title = Rgb(0xFFC27A), NameText = Rgb(0xFFF0E0),
            BadgeTop = Rgb(0x120C07), BadgeBottom = Rgb(0x120C07), BadgeBorder = Rgb(0x7A4A1E), BadgeText = Rgb(0xF9E6D2),
            TrackTop = Rgb(0x0C0805), TrackBottom = Rgb(0x0C0805), TrackBorder = Rgb(0x7A4A1E),
            Label = Rgb(0xC9A27F), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x8E7A68), Divider = Rgb(0x45301E),
            TileTop = Rgb(0xFFC46A), TileMid = Rgb(0xF99E44), TileBottom = Rgb(0xE07E22), TileBorder = Rgb(0x794C0E), TileDigitOutline = Rgb(0x7A3A06),
            FillTop = Rgb(0xFFD58A), FillMid = Rgb(0xFFA53A), FillBottom = Rgb(0xD9700F), FillEdge = Rgb(0xFFEACC),
            CoinTop = Rgb(0xFFDA74), CoinBottom = Rgb(0xE8801A), CoinRim = Rgb(0x6E3508)
        };

        public static readonly Palette Blue = new Palette
        {
            Name = "blue", DisplayName = "Blue",
            Border = Rgb(0x4A9BE0), PanelTop = Rgb(0x151D2A), PanelBottom = Rgb(0x0A0F17),
            HeaderTop = Rgb(0x1F3F6E), HeaderBottom = Rgb(0x0F203A), HeaderSheen = Rgba(60, 0xAAD2FF),
            HeaderLine = Rgb(0x04070D), Title = Rgb(0x8FC8FF), NameText = Rgb(0xE6F2FF),
            BadgeTop = Rgb(0x080C12), BadgeBottom = Rgb(0x080C12), BadgeBorder = Rgb(0x2E5280), BadgeText = Rgb(0xDDEBFA),
            TrackTop = Rgb(0x05080D), TrackBottom = Rgb(0x05080D), TrackBorder = Rgb(0x2E5280),
            Label = Rgb(0x8FA8C8), Value = Rgb(0xFFFFFF), ValueShadow = Rgba(150, 0x000000), Negative = Rgb(0xFF8672),
            Missing = Rgb(0x6A7A8E), Divider = Rgb(0x22324A),
            TileTop = Rgb(0xA8DBFF), TileMid = Rgb(0x4AA3F2), TileBottom = Rgb(0x1E6CC4), TileBorder = Rgb(0x0C2E5E), TileDigitOutline = Rgb(0x0C2E5E),
            FillTop = Rgb(0xB8F0FF), FillMid = Rgb(0x4FC3F7), FillBottom = Rgb(0x1A7FC4), FillEdge = Rgb(0xDDF6FF),
            CoinTop = Rgb(0xBFE4FF), CoinBottom = Rgb(0x3A8EE0), CoinRim = Rgb(0x0C2E5E)
        };

        public static readonly Palette[] All = { Default, Pink, Red, Black, Orange, Blue };

        public static Palette ByName(string name)
        {
            foreach (Palette p in All)
                if (string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) return p;
            return Default;
        }
    }

    /// <summary>Draws the tracker panel into a transparent bitmap for a per-pixel-alpha window.</summary>
    public sealed class OverlayRenderer
    {
        public const int BaseWidth = 384;
        public const int BaseHeight = 178;
        const float Margin = 6f;         // room for the drop shadow
        const float PanelW = 372f;
        const float PanelH = 166f;
        const float HeaderH = 26f;
        const float RowTop = 35f;
        const float RowH = 21f;

        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string SansName = PickFamily(new[] { "Segoe UI", "Tahoma", "Arial" });
        static readonly string HeavyName = PickFamily(new[] { "Segoe UI Black", "Arial Black", SansName });
        static readonly FontStyle HeavyStyle = HeavyName == SansName ? FontStyle.Bold : FontStyle.Regular;
        static readonly FontFamily Sans = new FontFamily(SansName);
        static readonly FontFamily Heavy = new FontFamily(HeavyName);
        static readonly StringFormat Typo = MakeTypo();

        static readonly Font HeaderFont = new Font(Sans, 10.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font NameFont = new Font(Sans, 11.5f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font LvFont = new Font(Sans, 10.5f, FontStyle.Bold | FontStyle.Italic, GraphicsUnit.Pixel);
        static readonly Font LabelFont = new Font(Sans, 9f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font ValueFont = new Font(Sans, 17f, FontStyle.Bold, GraphicsUnit.Pixel);
        static readonly Font CreditFont = new Font(Sans, 9f, FontStyle.Regular, GraphicsUnit.Pixel);
        static readonly Font CreditNameFont = new Font(Sans, 9f, FontStyle.Bold, GraphicsUnit.Pixel);

        Palette pal = Palette.Default;

        public Palette Palette
        {
            get { return pal; }
            set { pal = value ?? Palette.Default; }
        }

        /// <summary>Clickable areas from the last render, in unscaled window coordinates.</summary>
        public RectangleF LevelArea { get; private set; }
        public RectangleF NameArea { get; private set; }
        public RectangleF SettingsArea { get; private set; }

        public Bitmap Render(OverlayView v, float scale)
        {
            int w = (int)Math.Ceiling(BaseWidth * scale), h = (int)Math.Ceiling(BaseHeight * scale);
            var bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.ScaleTransform(scale, scale);
                DrawShadow(g);
                g.TranslateTransform(Margin, Margin);
                DrawPanel(g);
                DrawHeader(g, v);
                float barLeft = DrawLevel(g, v);
                DrawBar(g, v, barLeft);
                DrawStats(g, v);
                DrawCredit(g);
            }
            return bmp;
        }

        static void DrawShadow(Graphics g)
        {
            using (var b = new SolidBrush(Color.FromArgb(16, 0, 0, 0)))
            {
                for (int i = 6; i >= 1; i--)
                {
                    var r = new RectangleF(Margin - i + 0.5f, Margin - i + 2f, PanelW + 2 * i - 1, PanelH + 2 * i - 2);
                    using (GraphicsPath p = Shapes.RoundRect(r, 9f + i)) g.FillPath(b, p);
                }
            }
        }

        void DrawPanel(Graphics g)
        {
            var panel = new RectangleF(0, 0, PanelW, PanelH);
            using (GraphicsPath shape = Shapes.RoundRect(panel, 8f))
            {
                using (LinearGradientBrush b = Shapes.Vertical(panel, pal.PanelTop, pal.PanelBottom))
                    g.FillPath(b, shape);

                GraphicsState state = g.Save();
                g.SetClip(shape, CombineMode.Intersect);
                var header = new RectangleF(0, 0, PanelW, HeaderH);
                using (LinearGradientBrush b = Shapes.Vertical(header, pal.HeaderTop, pal.HeaderBottom))
                    g.FillRectangle(b, header);
                using (var p = new Pen(pal.HeaderSheen, 1f)) g.DrawLine(p, 0, 1.5f, PanelW, 1.5f);
                using (var p = new Pen(pal.HeaderLine, 1.5f)) g.DrawLine(p, 0, HeaderH, PanelW, HeaderH);
                g.Restore(state);

                using (var p = new Pen(pal.Border, 1.6f)) g.DrawPath(p, shape);
            }
        }

        void DrawHeader(Graphics g, OverlayView v)
        {
            float cy = HeaderH / 2;
            DrawCoin(g, 14f, cy, 5.5f);
            using (var b = new SolidBrush(pal.Title)) DrawSpaced(g, "EXP TRACKER", HeaderFont, b, 25f, cy, 1.1f);

            // Settings button in the top-right corner.
            var button = new RectangleF(PanelW - 29f, 3f, 22f, 20f);
            DrawGear(g, button, v.SettingsHover);
            SettingsArea = new RectangleF(button.X + Margin, button.Y + Margin, button.Width, button.Height);

            float right = button.X - 7f;
            string name = Fit(g, v.Name ?? "", NameFont, 140f);
            float nameW = name.Length > 0 ? Measure(g, name, NameFont) : 0f;
            if (name.Length > 0)
                using (var b = new SolidBrush(pal.NameText)) g.DrawString(name, NameFont, b, right - nameW, TextTop(NameFont, cy), Typo);

            float dotX = right - nameW - (name.Length > 0 ? 9f : 3f);
            DrawStatusDot(g, dotX, cy, v.Status);
            float left = Math.Min(dotX - 8f, right - 70f);
            NameArea = new RectangleF(Margin + left, Margin, right - left + 4f, HeaderH);
        }

        void DrawGear(Graphics g, RectangleF button, bool hover)
        {
            using (GraphicsPath p = Shapes.RoundRect(button, 4f))
            {
                using (var b = new SolidBrush(hover ? Color.FromArgb(70, pal.Title) : Color.FromArgb(40, 0, 0, 0))) g.FillPath(b, p);
                using (var pen = new Pen(hover ? pal.Title : Color.FromArgb(110, pal.Title), 1f)) g.DrawPath(pen, p);
            }
            float cx = button.X + button.Width / 2, cy = button.Y + button.Height / 2;
            const int teeth = 8;
            const float outer = 6.4f, inner = 4.7f, hole = 2f;
            var points = new PointF[teeth * 4];
            for (int i = 0; i < teeth; i++)
            {
                double a = 2 * Math.PI * i / teeth, step = 2 * Math.PI / teeth;
                double[] angles = { a - step * 0.30, a - step * 0.14, a + step * 0.14, a + step * 0.30 };
                float[] radii = { inner, outer, outer, inner };
                for (int k = 0; k < 4; k++)
                    points[i * 4 + k] = new PointF(cx + (float)(Math.Cos(angles[k]) * radii[k]), cy + (float)(Math.Sin(angles[k]) * radii[k]));
            }
            using (var gear = new GraphicsPath(FillMode.Alternate))
            {
                gear.AddPolygon(points);
                gear.AddEllipse(cx - hole, cy - hole, hole * 2, hole * 2);
                using (var pen = new Pen(Color.FromArgb(200, 20, 14, 8), 1.6f) { LineJoin = LineJoin.Round }) g.DrawPath(pen, gear);
                using (var b = new SolidBrush(hover ? Lighten(pal.Title, 0.45f) : pal.Title)) g.FillPath(b, gear);
            }
        }

        float DrawLevel(Graphics g, OverlayView v)
        {
            const float tileW = 13f, tileH = 17f, gap = 2f;
            string digits = v.Level > 0 ? v.Level.ToString(Inv) : "?";
            float lvW = Measure(g, "LV.", LvFont);
            float width = 7f + lvW + 5f + digits.Length * tileW + (digits.Length - 1) * gap + 4f;
            var badge = new RectangleF(10f, RowTop, width, RowH);

            using (GraphicsPath p = Shapes.RoundRect(badge, 4f))
            {
                using (LinearGradientBrush b = Shapes.Vertical(badge, pal.BadgeTop, pal.BadgeBottom)) g.FillPath(b, p);
                using (var pen = new Pen(pal.BadgeBorder, 1f)) g.DrawPath(pen, p);
            }
            using (var b = new SolidBrush(pal.BadgeText))
                g.DrawString("LV.", LvFont, b, badge.X + 7f, TextTop(LvFont, RowTop + RowH / 2), Typo);

            float x = badge.X + 7f + lvW + 5f, y = RowTop + (RowH - tileH) / 2;
            foreach (char d in digits)
            {
                DrawTile(g, new RectangleF(x, y, tileW, tileH), d.ToString());
                x += tileW + gap;
            }
            LevelArea = new RectangleF(badge.X + Margin, badge.Y + Margin, badge.Width, badge.Height);
            return badge.Right;
        }

        void DrawTile(Graphics g, RectangleF r, string digit)
        {
            using (GraphicsPath p = Shapes.RoundRect(r, 3f))
            {
                using (LinearGradientBrush b = Shapes.Vertical(r, pal.TileTop, pal.TileMid, pal.TileBottom, 0.5f))
                    g.FillPath(b, p);
                using (var pen = new Pen(pal.TileBorder, 1f)) g.DrawPath(pen, p);
            }
            var gloss = new RectangleF(r.X + 1.5f, r.Y + 1.2f, r.Width - 3f, r.Height * 0.42f);
            using (GraphicsPath p = Shapes.RoundRect(gloss, 2f))
            using (var b = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
                g.FillPath(b, p);
            DrawOutlined(g, digit, Heavy, HeavyStyle, 13.5f, r.X + r.Width / 2, r.Y + r.Height / 2 + 0.3f,
                Color.FromArgb(0xFF, 0xFC, 0xED), pal.TileDigitOutline, 2.2f);
        }

        void DrawBar(Graphics g, OverlayView v, float left)
        {
            float x0 = left + 7f, x1 = PanelW - 10f;
            var track = new RectangleF(x0, RowTop, x1 - x0, RowH);
            using (GraphicsPath p = Shapes.RoundRect(track, 4f))
            using (LinearGradientBrush b = Shapes.Vertical(track, pal.TrackTop, pal.TrackBottom))
                g.FillPath(b, p);

            double pct = v.Percent;
            if (v.Message == null && !double.IsNaN(pct) && pct > 0)
            {
                float inner = track.Width - 4f;
                float fw = Math.Max(3f, (float)(inner * Math.Min(1.0, pct / 100.0)));
                var fill = new RectangleF(x0 + 2f, RowTop + 2f, fw, RowH - 4f);
                using (GraphicsPath fp = Shapes.RoundRect(fill, 2.5f))
                {
                    using (LinearGradientBrush b = Shapes.Vertical(fill, pal.FillTop, pal.FillMid, pal.FillBottom, 0.4f))
                        g.FillPath(b, fp);
                    GraphicsState state = g.Save();
                    g.SetClip(fp, CombineMode.Intersect);
                    var gloss = new RectangleF(fill.X, fill.Y, fill.Width, fill.Height * 0.45f);
                    using (LinearGradientBrush b = Shapes.Vertical(gloss, Color.FromArgb(110, 255, 255, 255), Color.FromArgb(10, 255, 255, 255)))
                        g.FillRectangle(b, gloss);
                    using (var pen = new Pen(Color.FromArgb(150, pal.FillEdge), 1f))
                        g.DrawLine(pen, fill.Right - 0.5f, fill.Y, fill.Right - 0.5f, fill.Bottom);
                    g.Restore(state);
                    using (var pen = new Pen(Color.FromArgb(90, 0, 0, 0), 1f)) g.DrawPath(pen, fp);
                }
            }
            using (GraphicsPath p = Shapes.RoundRect(track, 4f))
            using (var pen = new Pen(pal.TrackBorder, 1f))
                g.DrawPath(pen, p);

            float cx = track.X + track.Width / 2, cy = track.Y + track.Height / 2;
            Color outline = Color.FromArgb(235, 20, 16, 12);
            if (v.Message != null)
            {
                DrawOutlined(g, v.Message, Sans, FontStyle.Bold, 10.5f, cx, cy, Color.FromArgb(0xF6, 0xF0, 0xE4), outline, 2.4f);
            }
            else if (v.HasExp)
            {
                // "104,741 / 356,916 [29.35%]", shortened to "4.52B / 9.48B [47.62%]" when it doesn't fit.
                string percent = double.IsNaN(pct) ? "" : " [" + pct.ToString("0.00", Inv) + "%]";
                string approx = v.LevelTotalExact ? "" : "~";
                var options = new System.Collections.Generic.List<string>();
                if (v.LevelTotal > 0)
                {
                    options.Add(v.Exp.ToString("N0", Inv) + " / " + approx + v.LevelTotal.ToString("N0", Inv) + percent);
                    options.Add(Fmt.Short(v.Exp) + " / " + approx + Fmt.Short(v.LevelTotal) + percent);
                }
                options.Add(v.Exp.ToString("N0", Inv) + percent);
                DrawExpText(g, options, cx, cy, track.Width - 12f, outline);
            }
        }

        /// <summary>Draws "EXP" and the first of <paramref name="options"/> that fits in <paramref name="maxWidth"/>.</summary>
        static void DrawExpText(Graphics g, System.Collections.Generic.List<string> options, float cx, float cy, float maxWidth, Color outline)
        {
            using (var label = new GraphicsPath())
            using (var number = new GraphicsPath())
            {
                const float gap = 5f;
                label.AddString("EXP", Sans, (int)(FontStyle.Bold | FontStyle.Italic), 10.5f, PointF.Empty, Typo);
                RectangleF lb = label.GetBounds(), nb = RectangleF.Empty;
                foreach (string value in options)
                {
                    number.Reset();
                    number.AddString(value, Sans, (int)FontStyle.Bold, 11f, PointF.Empty, Typo);
                    nb = number.GetBounds();
                    if (lb.Width + gap + nb.Width <= maxWidth) break;
                }
                float left = cx - (lb.Width + gap + nb.Width) / 2;
                float dy = cy - (lb.Y + lb.Height / 2);
                using (var m = new Matrix())
                {
                    m.Translate(left - lb.X, dy);
                    label.Transform(m);
                }
                using (var m = new Matrix())
                {
                    m.Translate(left + lb.Width + gap - nb.X, dy);
                    number.Transform(m);
                }
                using (var pen = new Pen(outline, 2.6f) { LineJoin = LineJoin.Round })
                {
                    g.DrawPath(pen, label);
                    g.DrawPath(pen, number);
                }
                using (var b = new SolidBrush(Color.FromArgb(0xFF, 0xF4, 0xD2))) g.FillPath(b, label);
                using (var b = new SolidBrush(Color.White)) g.FillPath(b, number);
            }
        }

        /// <summary>Two rows of three: rates on top, session and level progress below.</summary>
        void DrawStats(Graphics g, OverlayView v)
        {
            string[] labels = { "LAST 5 MIN", "EXP/H - 30M", "EXP/H - 1H", "THIS SESSION", "SESSION TIME", v.EtaLabel };
            string[] values = { v.Recent, v.Rate, v.RateHour, v.Session, v.SessionTime, v.Eta };
            bool[] negative = { v.RecentNegative, false, false, v.SessionNegative, false, false };
            const float rowGap = 40f;
            float colW = (PanelW - 12f) / 3f;
            using (var sep = new Pen(pal.Divider, 1f))
            using (var labelBrush = new SolidBrush(pal.Label))
            using (var shadow = new SolidBrush(pal.ValueShadow))
            {
                g.DrawLine(sep, 8f, 64.5f, PanelW - 8f, 64.5f);
                g.DrawLine(sep, 14f, 64.5f + rowGap, PanelW - 14f, 64.5f + rowGap);
                for (int i = 0; i < 6; i++)
                {
                    float x = 6f + (i % 3) * colW, dy = (i / 3) * rowGap;
                    if (i % 3 > 0) g.DrawLine(sep, x + 0.5f, 72f + dy, x + 0.5f, 100f + dy);
                    DrawSpaced(g, labels[i], LabelFont, labelBrush, x + 10f, 76f + dy, 0.5f);

                    Color c = negative[i] ? pal.Negative : values[i] == OverlayView.Dash ? pal.Missing : pal.Value;
                    float top = TextTop(ValueFont, 93f + dy);
                    g.DrawString(values[i], ValueFont, shadow, x + 10f, top + 1.2f, Typo);
                    using (var b = new SolidBrush(c)) g.DrawString(values[i], ValueFont, b, x + 9f, top, Typo);
                }
            }
        }

        void DrawCredit(Graphics g)
        {
            const float y = PanelH - 10.5f;
            using (var pen = new Pen(pal.Divider, 1f)) g.DrawLine(pen, 8f, PanelH - 20.5f, PanelW - 8f, PanelH - 20.5f);
            const string by = "Created by ", name = "Aydrian";
            float w1 = Measure(g, by, CreditFont), w2 = Measure(g, name, CreditNameFont);
            float x = (PanelW - w1 - w2) / 2;
            using (var b = new SolidBrush(pal.Missing)) g.DrawString(by, CreditFont, b, x, TextTop(CreditFont, y), Typo);
            using (var b = new SolidBrush(pal.Title)) g.DrawString(name, CreditNameFont, b, x + w1, TextTop(CreditNameFont, y), Typo);
        }

        static Color Lighten(Color c, float amount)
        {
            return Color.FromArgb(c.A, c.R + (int)((255 - c.R) * amount), c.G + (int)((255 - c.G) * amount), c.B + (int)((255 - c.B) * amount));
        }

        void DrawCoin(Graphics g, float cx, float cy, float r)
        {
            var rect = new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
            using (LinearGradientBrush b = Shapes.Vertical(rect, pal.CoinTop, pal.CoinBottom))
                g.FillEllipse(b, rect);
            using (var pen = new Pen(pal.CoinRim, 1f)) g.DrawEllipse(pen, rect);
            RectangleF inner = RectangleF.Inflate(rect, -2.2f, -2.2f);
            using (var pen = new Pen(Color.FromArgb(150, 255, 244, 200), 0.8f)) g.DrawEllipse(pen, inner);
        }

        static void DrawStatusDot(Graphics g, float cx, float cy, OverlayStatus status)
        {
            Color c;
            switch (status)
            {
                case OverlayStatus.Live: c = Color.FromArgb(0x86, 0xE0, 0x4E); break;
                case OverlayStatus.NoRegion: c = Color.FromArgb(0xE0, 0x56, 0x3E); break;
                case OverlayStatus.Paused: c = Color.FromArgb(0x9E, 0x98, 0x90); break;
                default: c = Color.FromArgb(0xF0, 0xB0, 0x38); break;
            }
            using (var b = new SolidBrush(Color.FromArgb(80, 0, 0, 0))) g.FillEllipse(b, cx - 4f, cy - 4f, 8f, 8f);
            using (var b = new SolidBrush(c)) g.FillEllipse(b, cx - 3f, cy - 3f, 6f, 6f);
            using (var b = new SolidBrush(Color.FromArgb(150, 255, 255, 255))) g.FillEllipse(b, cx - 1.8f, cy - 2.2f, 2.2f, 2.2f);
        }

        static void DrawOutlined(Graphics g, string text, FontFamily family, FontStyle style, float size,
            float cx, float cy, Color fill, Color outline, float width)
        {
            using (var path = new GraphicsPath())
            {
                path.AddString(text, family, (int)style, size, PointF.Empty, Typo);
                RectangleF b = path.GetBounds();
                using (var m = new Matrix())
                {
                    m.Translate(cx - (b.X + b.Width / 2), cy - (b.Y + b.Height / 2));
                    path.Transform(m);
                }
                using (var pen = new Pen(outline, width) { LineJoin = LineJoin.Round }) g.DrawPath(pen, path);
                using (var brush = new SolidBrush(fill)) g.FillPath(brush, path);
            }
        }

        /// <summary>Draws text with extra letter spacing, vertically centred on the cap height.</summary>
        static void DrawSpaced(Graphics g, string text, Font font, Brush brush, float x, float centerY, float spacing)
        {
            float y = TextTop(font, centerY);
            foreach (char ch in text)
            {
                string s = ch.ToString();
                g.DrawString(s, font, brush, x, y, Typo);
                x += Measure(g, s, font) + spacing;
            }
        }

        /// <summary>The y to draw at so that capitals and digits are centred on <paramref name="centerY"/>.</summary>
        static float TextTop(Font font, float centerY)
        {
            FontFamily f = font.FontFamily;
            float ascent = font.Size * f.GetCellAscent(font.Style) / f.GetEmHeight(font.Style);
            return centerY - (ascent - font.Size * 0.36f);
        }

        static float Measure(Graphics g, string text, Font font)
        {
            return g.MeasureString(text, font, PointF.Empty, Typo).Width;
        }

        static string Fit(Graphics g, string text, Font font, float maxWidth)
        {
            if (Measure(g, text, font) <= maxWidth) return text;
            while (text.Length > 1 && Measure(g, text + "…", font) > maxWidth) text = text.Substring(0, text.Length - 1);
            return text + "…";
        }

        static StringFormat MakeTypo()
        {
            var sf = (StringFormat)StringFormat.GenericTypographic.Clone();
            sf.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            return sf;
        }

        static string PickFamily(string[] names)
        {
            using (var installed = new InstalledFontCollection())
            {
                foreach (string name in names)
                    foreach (FontFamily f in installed.Families)
                        if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)) return f.Name;
            }
            return FontFamily.GenericSansSerif.Name;
        }
    }
}
