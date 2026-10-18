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
            public bool[] Px;   // 7 rows x Cols
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
            var t = new Template { Ch = ch, Cols = rows[0].Length, Px = new bool[rows.Length * rows[0].Length] };
            for (int y = 0; y < rows.Length; y++)
                for (int x = 0; x < t.Cols; x++)
                    t.Px[y * t.Cols + x] = rows[y][x] == '#';
            return t;
        }

        struct Box
        {
            public int X, Y, Right, Bottom;
            public int W { get { return Right - X; } }
            public int H { get { return Bottom - Y; } }
        }

        enum LineStatus { NotFound, Unreadable, Ok }

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
                LineStatus status = ReadLine(ink, w, h, out text, out glyphs);
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

        static LineStatus ReadLine(bool[] ink, int w, int h, out string result, out List<Box> glyphs)
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
            var line = new List<Box>();
            var others = new List<Box>();         // non-glyph shapes crossing the text line
            foreach (Box b in boxes)
            {
                // Taller labels like "EXP", bars, frames, the mouse cursor, windows.
                if (b.Y < top - p * 0.6 || b.Bottom > baseline + p * 0.6 || b.W > p * 9)
                {
                    if (b.Y < baseline && b.Bottom > top) others.Add(b);
                    continue;
                }
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

            var chars = new char[merged.Count];
            int known = 0, digits = 0;
            for (int i = 0; i < merged.Count; i++)
            {
                chars[i] = Classify(ink, w, merged[i], p, top, baseline);
                if (chars[i] != '?') known++;
                if (chars[i] >= '0' && chars[i] <= '9') digits++;
            }
            // Mostly unknown shapes: some other font, let OCR have a go.
            if (known < 3 || known * 10 < merged.Count * 6) return LineStatus.NotFound;

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
                    foreach (Box o in others)
                        if (o.Right > spanLeft && o.X < spanRight) return LineStatus.Unreadable;
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

        static char Classify(bool[] ink, int w, Box g, double p, double top, double baseline)
        {
            int cols = Math.Max(1, (int)Math.Round(g.W / p));
            int rows = Math.Max(1, (int)Math.Round(g.H / p));

            // Small marks on the baseline: "." is one font pixel, "," is two.
            if (rows <= 2 && cols <= 2)
            {
                if (g.Bottom < baseline - p) return '\'';
                return rows * cols <= 1 ? '.' : ',';
            }

            // Sample the glyph on the 7-row font grid of the line.
            var px = new bool[7 * cols];
            for (int y = 0; y < 7; y++)
            {
                int sy = (int)(top + (y + 0.5) * p);
                for (int x = 0; x < cols; x++)
                {
                    int sx = (int)(g.X + (x + 0.5) * g.W / (double)cols);
                    px[y * cols + x] = sy >= 0 && sy * w + sx < ink.Length && ink[sy * w + sx];
                }
            }

            Template best = null;
            int bestDist = int.MaxValue, secondDist = int.MaxValue;
            foreach (Template t in Templates)
            {
                if (t.Cols != cols) continue;
                int d = 0;
                for (int i = 0; i < px.Length; i++)
                    if (px[i] != t.Px[i]) d++;
                if (d < bestDist)
                {
                    secondDist = bestDist;
                    bestDist = d;
                    best = t;
                }
                else if (d < secondDist)
                {
                    secondDist = d;
                }
            }
            int allowed = Math.Max(1, px.Length / 10);
            if (best == null || bestDist > allowed || secondDist <= bestDist) return '?';
            return best.Ch;
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
