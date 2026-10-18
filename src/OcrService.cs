// MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace MapleExpTracker
{
    public sealed class OcrRead
    {
        public string Text = "";
        public string Filter = "";
        public List<ExpReading> Candidates = new List<ExpReading>();
        public Bitmap Processed;
        public long Milliseconds;
    }

    /// <summary>Screen capture plus the Windows built-in OCR engine (Windows.Media.Ocr).</summary>
    public static class OcrService
    {
        /// <summary>Image clean-up filters.</summary>
        public static readonly string[] Filters = { "color", "white text", "contrast" };

        /// <summary>Upscale factors relative to the default size (OCR is picky about text height).</summary>
        static readonly double[] ScaleSteps = { 1.0, 0.6, 1.5 };

        /// <summary>Number of filter/scale combinations Read() can try.</summary>
        public static int AttemptCount
        {
            get { return Filters.Length * ScaleSteps.Length; }
        }

        static readonly object gate = new object();
        static OcrEngine engine;

        public static bool TryInit(out string error)
        {
            lock (gate)
            {
                error = null;
                if (engine != null) return true;
                try
                {
                    var english = new Language("en-US");
                    if (OcrEngine.IsLanguageSupported(english)) engine = OcrEngine.TryCreateFromLanguage(english);
                    if (engine == null) engine = OcrEngine.TryCreateFromUserProfileLanguages();
                }
                catch (Exception ex)
                {
                    error = "Windows OCR could not start: " + ex.GetBaseException().Message;
                    return false;
                }
                if (engine == null)
                {
                    error = "Windows OCR has no language installed. Add English under Settings > Time & language > Language & region, then restart the tracker.";
                    return false;
                }
                return true;
            }
        }

        public static Bitmap Capture(Rectangle r)
        {
            var bmp = new Bitmap(r.Width, r.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
                g.CopyFromScreen(r.Left, r.Top, 0, 0, r.Size, CopyPixelOperation.SourceCopy);
            return bmp;
        }

        /// <summary>
        /// Reads the EXP text in <paramref name="raw"/>. Tries filter/scale combinations, starting with the
        /// one that worked last time, until two of them agree on a clean reading. Readings are returned
        /// most-agreed-on first.
        /// </summary>
        public static OcrRead Read(Bitmap raw, ref int preferred, bool keepImage)
        {
            var read = new OcrRead();
            Stopwatch sw = Stopwatch.StartNew();

            // The game's own bitmap font is read exactly by shape. When it's there, OCR is never
            // used: if the numbers can't be read cleanly (cursor or window over them) the frame is skipped.
            Bitmap fontImage;
            bool fontFound;
            string fontText = PixelFontReader.Read(raw, keepImage, out fontImage, out fontFound);
            if (fontFound)
            {
                List<ExpReading> found = fontText != null ? ExpParser.Parse(fontText) : new List<ExpReading>();
                // The game always shows "number [dd.dd%]"; anything else read from its font is a misread.
                found = found.FindAll(r => r.HasPercent && r.PercentStep <= 0.011 && r.Quality >= ExpReading.High);
                bool clean = found.Count > 0 && found[0].Quality >= ExpReading.High;
                read.Text = fontText ?? "(EXP numbers partly covered, skipped)";
                read.Filter = "MapleStory font";
                read.Candidates = clean ? found : new List<ExpReading>();
                read.Processed = fontImage;
                read.Milliseconds = sw.ElapsedMilliseconds;
                return read;
            }
            if (fontImage != null) fontImage.Dispose();

            var votes = new List<int>();
            var firstAttempt = new List<int>();
            int bestVotes = 0;
            for (int i = 0; i < AttemptCount && bestVotes < 4; i++)
            {
                int attempt = (preferred + i) % AttemptCount;
                int filter = attempt % Filters.Length;
                double scaleStep = ScaleSteps[attempt / Filters.Length];
                Bitmap img = Preprocess(raw, filter, scaleStep);
                string text;
                try
                {
                    text = Recognize(img);
                }
                catch
                {
                    img.Dispose();
                    throw;
                }
                List<ExpReading> found = ExpParser.Parse(text);
                bool clean = found.Count > 0 && found[0].Quality >= ExpReading.High;
                if (i == 0 || (clean && bestVotes < 2) || (read.Candidates.Count == 0 && found.Count > 0))
                {
                    read.Text = text;
                    read.Filter = Filters[filter] + (scaleStep == 1.0 ? "" : ", x" + scaleStep.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
                    if (keepImage)
                    {
                        if (read.Processed != null) read.Processed.Dispose();
                        read.Processed = img;
                        img = null;
                    }
                }
                if (img != null) img.Dispose();

                foreach (ExpReading r in found)
                {
                    int weight = r.Quality >= ExpReading.High ? 2 : 1;
                    int index = read.Candidates.FindIndex(x => ExpParser.SameReading(x, r));
                    if (index < 0)
                    {
                        read.Candidates.Add(r);
                        votes.Add(weight);
                        firstAttempt.Add(attempt);
                        index = read.Candidates.Count - 1;
                    }
                    else
                    {
                        votes[index] += weight;
                        if (r.Quality > read.Candidates[index].Quality) read.Candidates[index].Quality = r.Quality;
                    }
                    bestVotes = Math.Max(bestVotes, votes[index]);
                }
            }

            // MapleStory always shows the percentage with two decimals: a bare number or "87%" is a
            // partial or broken read.
            for (int i = read.Candidates.Count - 1; i >= 0; i--)
            {
                ExpReading r = read.Candidates[i];
                if ((r.HasPercent && r.PercentStep <= 0.011) || r.Total > 0) continue;
                read.Candidates.RemoveAt(i);
                votes.RemoveAt(i);
                firstAttempt.RemoveAt(i);
            }

            // Most votes first; ties keep discovery order.
            var order = new List<int>();
            for (int i = 0; i < read.Candidates.Count; i++) order.Add(i);
            order.Sort((a, b) => votes[a] != votes[b] ? votes[b].CompareTo(votes[a]) : a.CompareTo(b));
            var ranked = new List<ExpReading>();
            foreach (int i in order) ranked.Add(read.Candidates[i]);
            if (order.Count > 0 && read.Candidates[order[0]].Quality >= ExpReading.High) preferred = firstAttempt[order[0]];
            read.Candidates = ranked;
            read.Milliseconds = sw.ElapsedMilliseconds;
            return read;
        }

        /// <summary>
        /// Upscales the capture (OCR wants text ~30px tall), pads it, and optionally binarizes it.
        /// </summary>
        public static Bitmap Preprocess(Bitmap raw, int filter, double scaleStep)
        {
            double scale = Math.Max(2.0, Math.Min(6.0, 60.0 / Math.Max(1, raw.Height))) * scaleStep;
            scale = Math.Max(1.0, Math.Min(scale, 2400.0 / raw.Width));
            const int pad = 18;
            int sw = (int)Math.Round(raw.Width * scale), sh = (int)Math.Round(raw.Height * scale);

            var up = new Bitmap(sw + pad * 2, sh + pad * 2, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(up))
            using (var attrs = new ImageAttributes())
            {
                g.Clear(EdgeColor(raw));
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                attrs.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(raw, new Rectangle(pad, pad, sw, sh), 0, 0, raw.Width, raw.Height, GraphicsUnit.Pixel, attrs);
            }
            if (filter != 0) Binarize(up, filter == 1);
            return up;
        }

        static Color EdgeColor(Bitmap bmp)
        {
            long r = 0, g = 0, b = 0, n = 0;
            int w = bmp.Width, h = bmp.Height;
            for (int x = 0; x < w; x++)
            {
                Accumulate(bmp.GetPixel(x, 0), ref r, ref g, ref b, ref n);
                Accumulate(bmp.GetPixel(x, h - 1), ref r, ref g, ref b, ref n);
            }
            for (int y = 1; y < h - 1; y++)
            {
                Accumulate(bmp.GetPixel(0, y), ref r, ref g, ref b, ref n);
                Accumulate(bmp.GetPixel(w - 1, y), ref r, ref g, ref b, ref n);
            }
            if (n == 0) return Color.Black;
            return Color.FromArgb((int)(r / n), (int)(g / n), (int)(b / n));
        }

        static void Accumulate(Color c, ref long r, ref long g, ref long b, ref long n)
        {
            r += c.R;
            g += c.G;
            b += c.B;
            n++;
        }

        /// <summary>
        /// Otsu threshold on either "whiteness" (min of R,G,B — isolates white digits on a colored bar)
        /// or luminance. The minority class is taken to be text and drawn black on white.
        /// </summary>
        static void Binarize(Bitmap bmp, bool whiteness)
        {
            int w = bmp.Width, h = bmp.Height;
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                int stride = d.Stride;
                var px = new byte[stride * h];
                Marshal.Copy(d.Scan0, px, 0, px.Length);
                var values = new byte[w * h];
                var hist = new int[256];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int i = y * stride + x * 4;
                        int b = px[i], g = px[i + 1], r = px[i + 2];
                        int v = whiteness ? Math.Min(r, Math.Min(g, b)) : (r * 299 + g * 587 + b * 114) / 1000;
                        values[y * w + x] = (byte)v;
                        hist[v]++;
                    }
                }
                int t = Otsu(hist, w * h);
                int above = 0;
                for (int v = t + 1; v < 256; v++) above += hist[v];
                bool textIsBright = above <= w * h - above;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int v = values[y * w + x];
                        bool text = textIsBright ? v > t : v <= t;
                        byte o = text ? (byte)0 : (byte)255;
                        int i = y * stride + x * 4;
                        px[i] = px[i + 1] = px[i + 2] = o;
                        px[i + 3] = 255;
                    }
                }
                Marshal.Copy(px, 0, d.Scan0, px.Length);
            }
            finally
            {
                bmp.UnlockBits(d);
            }
        }

        static int Otsu(int[] hist, int total)
        {
            double sum = 0;
            for (int i = 0; i < 256; i++) sum += i * (double)hist[i];
            double sumB = 0, best = -1;
            int wB = 0, threshold = 127;
            for (int i = 0; i < 256; i++)
            {
                wB += hist[i];
                if (wB == 0) continue;
                int wF = total - wB;
                if (wF == 0) break;
                sumB += i * (double)hist[i];
                double mB = sumB / wB, mF = (sum - sumB) / wF;
                double between = (double)wB * wF * (mB - mF) * (mB - mF);
                if (between > best)
                {
                    best = between;
                    threshold = i;
                }
            }
            return threshold;
        }

        internal static string Recognize(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var px = new byte[w * h * 4];
            BitmapData d = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                for (int y = 0; y < h; y++)
                    Marshal.Copy(IntPtr.Add(d.Scan0, y * d.Stride), px, y * w * 4, w * 4);
            }
            finally
            {
                bmp.UnlockBits(d);
            }

            IBuffer buffer;
            using (var writer = new DataWriter())
            {
                writer.WriteBytes(px);
                buffer = writer.DetachBuffer();
            }
            using (SoftwareBitmap sb = SoftwareBitmap.CreateCopyFromBuffer(buffer, BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied))
            {
                OcrResult result;
                lock (gate)
                {
                    if (engine == null) throw new InvalidOperationException("Windows OCR is not available.");
                    result = Wait(engine.RecognizeAsync(sb));
                }
                var text = new StringBuilder();
                foreach (OcrLine line in result.Lines)
                {
                    if (text.Length > 0) text.Append(' ');
                    text.Append(line.Text);
                }
                return text.ToString();
            }
        }

        /// <summary>Blocks until a WinRT async operation finishes (we're on a worker thread).</summary>
        static T Wait<T>(Windows.Foundation.IAsyncOperation<T> op)
        {
            var done = new ManualResetEvent(false);
            op.Completed = delegate { done.Set(); };
            if (!done.WaitOne(10000))
            {
                op.Cancel();
                throw new TimeoutException("Windows OCR did not respond.");
            }
            if (op.Status != Windows.Foundation.AsyncStatus.Completed)
                throw new InvalidOperationException("Windows OCR failed: " + (op.ErrorCode != null ? op.ErrorCode.Message : op.Status.ToString()));
            return op.GetResults();
        }
    }
}
