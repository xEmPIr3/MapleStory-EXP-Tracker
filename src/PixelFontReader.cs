using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;

namespace MapleExpTracker
{
    /// <summary>
    /// Reads the crisp 5x7 bitmap font MapleStory Classic uses for its HP / MP / EXP numbers by
    /// matching glyph shapes. General OCR misreads this font (e.g. "59.87" as "ss.87"); shape
    /// matching is exact. Works at any whole or fractional game scale.
    /// </summary>
    public static class PixelFontReader
    {
        sealed class Template
        {
            public char Ch;
            public int Cols;
            public bool[] Px;           // 7 rows x Cols
            public int InkTop, InkBottom; // rows that hold ink (InkBottom exclusive)
        }

        // Shapes taken from in-game captures. Every glyph is 7 font pixels tall.
        static readonly Template[] Templates =
        {
            T('0', ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###."),
            T('1', ".#", "##", ".#", ".#", ".#", ".#", ".#"),
            T('2', ".###.", "#...#", "....#", "...#.", "..#..", ".#...", "#####"),
            T('3', ".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###."),
            T('4', "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#."),
            T('5', "#####", "#....", "#....", ".###.", "....#", "#...#", ".###."),
            T('6', ".###.", "#...#", "#....", "####.", "#...#", "#...#", ".###."),
            T('7', "#####", "....#", "....#", "....#", "...#.", "..#..", "..#.."),
            T('8', ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###."),
            T('9', ".###.", "#...#", "#...#", ".####", "....#", "#...#", ".###."),
            T('[', "##", "#.", "#.", "#.", "#.", "#.", "##"),
            T(']', "##", ".#", ".#", ".#", ".#", ".#", "##"),
            T('/', "...", "..#", "..#", ".#.", ".#.", "#..", "#.."),
            T('%', ".#....#", "#.#..#.", ".#..#..", "...#...", "..#..#.", ".#..#.#", "#....#."),
        };

        // Brightness cut-offs tried in turn (text is light on the dark status bar).
        static readonly int[] Thresholds = { 150, 120, 185 };

        static Template T(char ch, params string[] rows)
        {
            var t = new Template { Ch = ch, Cols = rows[0].Length, Px = new bool[rows.Length * rows[0].Length], InkTop = -1 };
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < t.Cols; x++)
                {
                    t.Px[y * t.Cols + x] = rows[y][x] == '#';
                    if (rows[y][x] != '#') continue;
                    if (t.InkTop < 0) t.InkTop = y;
                    t.InkBottom = y + 1;
                }
            return t;
        }

        struct Box
        {
            public int X, Y, Right, Bottom;
            public int W { get { return Right - X; } }
            public int H { get { return Bottom - Y; } }
        }

        enum LineStatus { NotFound, Unreadable, Ok }

        static readonly double[] Phases = { 0, 0.2, 0.4, 0.6, 0.8 };

        /// <summary>When set, the reader explains its decisions here (for tests and troubleshooting).</summary>
#pragma warning disable 649
        internal static StringBuilder DebugLog;
#pragma warning restore 649

        /// <summary>
        /// Returns the text of the digit line in <paramref name="bmp"/>, e.g. "194,111[59.87%]", or null.
        /// <paramref name="fontFound"/> tells the two nulls apart: true means the game's font is there but
        /// can't be read right now (part of it is covered, e.g. by the mouse cursor), so it's better to
        /// skip this frame than to guess. <paramref name="image"/> receives a black-on-white picture of
        /// what was read (only when <paramref name="wantImage"/> is set).
        /// </summary>
        public static string Read(Bitmap bmp, bool wantImage, out Bitmap image, out bool fontFound)
        {
            image = null;
            fontFound = false;
            int w = bmp.Width, h = bmp.Height;
            byte[] bright = Brightness(bmp);
            bool[] lastInk = null;
            foreach (int threshold in Thresholds)
            {
                var ink = new bool[w * h];
                for (int i = 0; i < ink.Length; i++) ink[i] = bright[i] >= threshold;
                List<Box> glyphs;
                string text;
                LineStatus status = ReadLine(ink, bright, w, h, out text, out glyphs);
                if (status == LineStatus.Ok)
                {
                    fontFound = true;
                    if (wantImage) image = Draw(ink, w, h, glyphs);
                    return text;
                }
                if (status == LineStatus.Unreadable && !fontFound)
                {
                    fontFound = true;
                    lastInk = ink;
                }
            }
            if (wantImage && lastInk != null) image = Draw(lastInk, w, h, null);
            return null;
        }

        public static string Read(Bitmap bmp)
        {
            Bitmap unused;
            bool found;
            return Read(bmp, false, out unused, out found);
        }

        static LineStatus ReadLine(bool[] ink, byte[] bright, int w, int h, out string result, out List<Box> glyphs)
        {
            result = null;
            glyphs = null;
            List<Box> boxes = Components(ink, w, h);

            // The digit line: the most common glyph height and baseline among character-sized blobs.
            Box anchor = new Box();
            int bestCount = 0;
            foreach (Box b in boxes)
            {
                if (b.H < 7 || b.H > 120 || b.W > b.H) continue;
                int count = 0;
                foreach (Box o in boxes)
                    if (Math.Abs(o.H - b.H) <= 1 && Math.Abs(o.Bottom - b.Bottom) <= 1) count++;
                if (count > bestCount || (count == bestCount && b.H > anchor.H))
                {
                    bestCount = count;
                    anchor = b;
                }
            }
            if (bestCount < 3) return LineStatus.NotFound;

            double p = anchor.H / 7.0;            // screen pixels per font pixel
            double top = anchor.Bottom - anchor.H, baseline = anchor.Bottom;
            if (DebugLog != null) DebugLog.AppendLine("line: height " + anchor.H + ", p " + p.ToString("0.00") + ", top " + top + ", count " + bestCount);
            var line = new List<Box>();
            foreach (Box b in boxes)
            {
                // Skip taller labels like "EXP", bars, frames, the mouse cursor, windows.
                if (b.Y < top - p * 0.6 || b.Bottom > baseline + p * 0.6 || b.W > p * 9) continue;
                line.Add(b);
            }
            line.Sort((a, b) => a.X.CompareTo(b.X));

            // Join pieces of one glyph ("%" is three blobs).
            var merged = new List<Box>();
            foreach (Box b in line)
            {
                if (merged.Count > 0 && b.X < merged[merged.Count - 1].Right)
                {
                    Box m = merged[merged.Count - 1];
                    m.X = Math.Min(m.X, b.X);
                    m.Y = Math.Min(m.Y, b.Y);
                    m.Right = Math.Max(m.Right, b.Right);
                    m.Bottom = Math.Max(m.Bottom, b.Bottom);
                    merged[merged.Count - 1] = m;
                }
                else
                {
                    merged.Add(b);
                }
            }

            float[] cover = Coverage(bright, w, h, merged, top, baseline);
            var chars = new char[merged.Count];
            int known = 0, digits = 0, exact = 0, shapes = 0;
            for (int i = 0; i < merged.Count; i++)
            {
                double fit;
                chars[i] = Classify(cover, w, h, merged[i], p, top, baseline, out fit);
                if (chars[i] != '?') known++;
                if (chars[i] >= '0' && chars[i] <= '9') digits++;
                if (fit < 0) continue;   // "." and ","
                shapes++;
                if (fit <= 0.12) exact++;
            }
            // Not clearly the game's font: most characters must match it almost perfectly (ordinary
            // fonts match a few digits closely, never most of them). Otherwise let OCR try.
            if (exact < 3 || exact * 10 < shapes * 6 || known * 10 < merged.Count * 6) return LineStatus.NotFound;

            // The game's font, but something we don't know sits in the line, or a shape crosses the
            // numbers (cursor, window): don't guess from what's left visible.
            if (digits < 2 || Array.IndexOf(chars, '?') >= 0) return LineStatus.Unreadable;
            // Check each group of characters (split at wide gaps, where labels like "MP" sit) from its
            // first digit or bracket to its last character.
            int start = 0;
            for (int i = 1; i <= merged.Count; i++)
            {
                if (i < merged.Count && merged[i].X - merged[i - 1].Right <= p * 6) continue;
                int from = -1;
                for (int k = start; k < i && from < 0; k++)
                    if ((chars[k] >= '0' && chars[k] <= '9') || chars[k] == '[') from = k;
                if (from >= 0)
                {
                    int spanLeft = merged[from].X, spanRight = merged[i - 1].Right;
                    int stray = StrayInk(ink, w, merged, spanLeft, spanRight, (int)top, (int)Math.Ceiling(baseline));
                    if (stray > p * p * 2)
                    {
                        if (DebugLog != null) DebugLog.AppendLine("  covered: " + stray + " bright pixels between x" + spanLeft + "-" + spanRight);
                        return LineStatus.Unreadable;
                    }
                }
                start = i;
            }

            var text = new StringBuilder();
            for (int i = 0; i < merged.Count; i++)
            {
                if (i > 0 && merged[i].X - merged[i - 1].Right > p * 6) text.Append(' ');
                text.Append(chars[i]);
            }
            glyphs = merged;
            result = text.ToString();
            return LineStatus.Ok;
        }

        /// <summary>
        /// Which pixels are ink (1) or background (0), cut halfway between the background and text
        /// brightness of the line. For text the game stretched with smoothing this puts the blurred edges
        /// back where they were, so the glyphs can be compared with the crisp font.
        /// </summary>
        static float[] Coverage(byte[] bright, int w, int h, List<Box> glyphs, double top, double baseline)
        {
            var hist = new int[256];
            int y0 = Math.Max(0, (int)top), y1 = Math.Min(h, (int)Math.Ceiling(baseline));
            int x0 = glyphs.Count > 0 ? glyphs[0].X : 0, x1 = glyphs.Count > 0 ? glyphs[glyphs.Count - 1].Right : w;
            int n = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    hist[bright[y * w + x]]++;
                    n++;
                }
            int lo = Percentile(hist, n, 0.2), hi = Percentile(hist, n, 0.98);
            if (hi - lo < 40) lo = Math.Max(0, hi - 40);
            int mid = (lo + hi) / 2;
            var cover = new float[w * h];
            for (int i = 0; i < cover.Length; i++)
                cover[i] = bright[i] >= mid ? 1f : 0f;
            return cover;
        }

        static int Percentile(int[] hist, int total, double share)
        {
            int acc = 0;
            for (int i = 0; i < 256; i++)
            {
                acc += hist[i];
                if (acc >= total * share) return i;
            }
            return 255;
        }

        /// <summary>
        /// Matches a glyph against the font. Each template is laid over the glyph's box (one cell per font
        /// pixel, on the line's 7-row grid) and compared with how much ink each cell holds, so fractional
        /// game scales and smoothed text still match. The best template must win clearly.
        /// </summary>
        static char Classify(float[] cover, int w, int h, Box g, double p, double top, double baseline, out double fit)
        {
            fit = -1;
            // Small marks on the baseline: "." is one font pixel, "," is two.
            if (g.H <= p * 2.5 + 1 && g.W <= p * 2.5 + 1)
            {
                if (g.Bottom < baseline - p * 1.5) return '\'';
                return Math.Max(g.W, g.H) < p * 1.5 + 0.25 ? '.' : ',';
            }

            Template best = null, second = null;
            double[] bestInk = null, secondInk = null;
            double bestErr = double.MaxValue, secondErr = double.MaxValue;
            foreach (Template t in Templates)
            {
                // The sizes must fit the line's font size.
                int rowsSpan = t.InkBottom - t.InkTop;
                if (Math.Abs(t.Cols * p - g.W) > p * 1.2 + 1 || Math.Abs(rowsSpan * p - g.H) > p * 1.2 + 1) continue;
                double[] ink;
                double err = Fit(cover, w, h, g, t, out ink);
                if (err < bestErr)
                {
                    second = best;
                    secondErr = bestErr;
                    secondInk = bestInk;
                    best = t;
                    bestErr = err;
                    bestInk = ink;
                }
                else if (err < secondErr)
                {
                    second = t;
                    secondErr = err;
                    secondInk = ink;
                }
            }
            // Blurry thin strokes (smoothed text) fit less well overall, so the limit is a little looser when
            // the pixels that tell the two closest characters apart clearly agree with the best one.
            fit = bestErr;
            bool sure;
            if (best == null) sure = false;
            else if (second == null) sure = bestErr <= 0.3;
            else if (second.Cols == best.Cols) sure = bestErr <= 0.35 && Decisive(best, bestInk, second, secondInk);
            else sure = bestErr <= 0.3 && secondErr - bestErr >= 0.04 && secondErr >= bestErr * 1.2;
            if (DebugLog != null)
                DebugLog.AppendLine("  glyph x" + g.X + " " + g.W + "x" + g.H + ": best " + (best == null ? "-" : best.Ch.ToString())
                    + " " + bestErr.ToString("0.000") + ", second " + (second == null ? "-" : second.Ch.ToString())
                    + " " + (second == null ? "" : secondErr.ToString("0.000")) + (sure ? "" : "  -> unsure"));
            return sure ? best.Ch : '?';
        }

        /// <summary>
        /// Lays the template's ink box over the glyph's ink box (so the game's scale doesn't matter) and
        /// returns the average mismatch per font pixel; <paramref name="ink"/> gets the glyph's ink per font
        /// pixel. At fractional scales each font pixel becomes 1 or 2 screen pixels in a repeating pattern
        /// that depends on where the text sits, so every pattern is tried and the best fit kept.
        /// </summary>
        static double Fit(float[] cover, int w, int h, Box g, Template t, out double[] ink)
        {
            int rowsSpan = t.InkBottom - t.InkTop;
            double cellW = g.W / (double)t.Cols, cellH = g.H / (double)rowsSpan, best = double.MaxValue;
            ink = null;
            var cells = new double[7 * t.Cols];
            foreach (double phaseX in Phases)
                foreach (double phaseY in Phases)
                {
                    double e = 0;
                    for (int y = 0; y < 7; y++)
                    {
                        int y0 = g.Y + (int)Math.Floor((y - t.InkTop) * cellH + phaseY);
                        int y1 = g.Y + (int)Math.Floor((y + 1 - t.InkTop) * cellH + phaseY);
                        for (int x = 0; x < t.Cols; x++)
                        {
                            int x0 = g.X + (int)Math.Floor(x * cellW + phaseX);
                            int x1 = g.X + (int)Math.Floor((x + 1) * cellW + phaseX);
                            double v = CellInk(cover, w, h, x0, y0, x1, y1);
                            cells[y * t.Cols + x] = v;
                            e += Math.Abs(v - (t.Px[y * t.Cols + x] ? 1 : 0));
                        }
                    }
                    e /= cells.Length;
                    if (e < best)
                    {
                        best = e;
                        ink = (double[])cells.Clone();
                    }
                }
            return best;
        }

        /// <summary>
        /// For two close candidates (e.g. 8 and 9, which differ in 2 font pixels), look only at the font
        /// pixels where they differ: blur at the edges doesn't count there, the real difference does.
        /// </summary>
        static bool Decisive(Template best, double[] bestInk, Template second, double[] secondInk)
        {
            double sb = 0, ss = 0;
            int n = 0;
            for (int i = 0; i < best.Px.Length; i++)
            {
                if (best.Px[i] == second.Px[i]) continue;
                sb += Math.Abs(bestInk[i] - (best.Px[i] ? 1 : 0));
                ss += Math.Abs(secondInk[i] - (second.Px[i] ? 1 : 0));
                n++;
            }
            if (n == 0) return false;
            sb /= n;
            ss /= n;
            return sb <= 0.4 && ss - sb >= 0.25;
        }

        /// <summary>
        /// Bright pixels on the text line that belong to no character: something (the mouse cursor, a
        /// window) is over the numbers. Frames and labels around the text don't count.
        /// </summary>
        static int StrayInk(bool[] ink, int w, List<Box> glyphs, int left, int right, int top, int bottom)
        {
            int count = 0;
            for (int y = Math.Max(0, top); y < bottom && y * w < ink.Length; y++)
                for (int x = left; x < right; x++)
                {
                    if (!ink[y * w + x]) continue;
                    bool inGlyph = false;
                    foreach (Box g in glyphs)
                        if (x >= g.X - 1 && x <= g.Right && y >= g.Y - 1 && y <= g.Bottom)
                        {
                            inGlyph = true;
                            break;
                        }
                    if (!inGlyph) count++;
                }
            return count;
        }

        /// <summary>Average ink over the screen pixels [x0, x1) x [y0, y1) (at least one pixel).</summary>
        static double CellInk(float[] cover, int w, int h, int x0, int y0, int x1, int y1)
        {
            if (x1 <= x0) x1 = x0 + 1;
            if (y1 <= y0) y1 = y0 + 1;
            double sum = 0;
            int n = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    n++;
                    if (x >= 0 && y >= 0 && x < w && y < h) sum += cover[y * w + x];
                }
            return sum / n;
        }

        static byte[] Brightness(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var result = new byte[w * h];
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                var row = new byte[w * 4];
                for (int y = 0; y < h; y++)
                {
                    Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), row, 0, row.Length);
                    for (int x = 0; x < w; x++)
                    {
                        int i = x * 4;
                        result[y * w + x] = Math.Max(row[i], Math.Max(row[i + 1], row[i + 2]));
                    }
                }
            }
            finally
            {
                bmp.UnlockBits(d);
            }
            return result;
        }

        static List<Box> Components(bool[] ink, int w, int h)
        {
            var seen = new bool[ink.Length];
            var boxes = new List<Box>();
            var stack = new Stack<int>();
            for (int start = 0; start < ink.Length; start++)
            {
                if (!ink[start] || seen[start]) continue;
                var b = new Box { X = w, Y = h, Right = -1, Bottom = -1 };
                seen[start] = true;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int i = stack.Pop();
                    int x = i % w, y = i / w;
                    if (x < b.X) b.X = x;
                    if (y < b.Y) b.Y = y;
                    if (x + 1 > b.Right) b.Right = x + 1;
                    if (y + 1 > b.Bottom) b.Bottom = y + 1;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        int ny = y + dy;
                        if (ny < 0 || ny >= h) continue;
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx;
                            if (nx < 0 || nx >= w) continue;
                            int j = ny * w + nx;
                            if (ink[j] && !seen[j])
                            {
                                seen[j] = true;
                                stack.Push(j);
                            }
                        }
                    }
                }
                boxes.Add(b);
            }
            return boxes;
        }

        static Bitmap Draw(bool[] ink, int w, int h, List<Box> glyphs)
        {
            const int zoom = 3;
            var bmp = new Bitmap(w * zoom, h * zoom, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                if (glyphs == null)
                {
                    // Everything, so you can see what's covering the numbers.
                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                            if (ink[y * w + x]) g.FillRectangle(Brushes.Black, x * zoom, y * zoom, zoom, zoom);
                }
                else
                {
                    foreach (Box b in glyphs)
                        for (int y = b.Y; y < b.Bottom; y++)
                            for (int x = b.X; x < b.Right; x++)
                                if (ink[y * w + x]) g.FillRectangle(Brushes.Black, x * zoom, y * zoom, zoom, zoom);
                }
            }
            return bmp;
        }
    }
}
