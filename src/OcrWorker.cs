using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace MapleExpTracker
{
    /// <summary>The result of reading the EXP region once.</summary>
    public sealed class OcrFrame : IDisposable
    {
        public DateTime TimeUtc;
        public Rectangle Region;
        public string Text = "";
        public string Filter = "";
        public List<ExpReading> Candidates = new List<ExpReading>();
        public long Milliseconds;
        public string Error;
        public Bitmap Raw;        // only kept while the OCR test window is open
        public Bitmap Processed;

        public void Dispose()
        {
            if (Raw != null) Raw.Dispose();
            if (Processed != null) Processed.Dispose();
            Raw = Processed = null;
        }
    }

    /// <summary>Background thread that captures and reads the EXP region on an interval.</summary>
    public sealed class OcrWorker : IDisposable
    {
        readonly object gate = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        Thread thread;
        Rectangle region;
        int preferredAttempt;
        volatile bool running;
        volatile bool paused;
        volatile bool debugImages;
        volatile bool rotateFilter;

        public int IntervalMs = 1000;

        /// <summary>Raised on the worker thread; the handler owns the frame.</summary>
        public event Action<OcrFrame> FrameReady;

        public bool Paused
        {
            get { return paused; }
            set { paused = value; wake.Set(); }
        }

        public bool DebugImages
        {
            get { return debugImages; }
            set { debugImages = value; }
        }

        public void SetRegion(Rectangle r)
        {
            lock (gate) region = r;
            wake.Set();
        }

        /// <summary>The last reading didn't fit; start with a different image filter next time.</summary>
        public void TryOtherFilter()
        {
            rotateFilter = true;
        }

        public void Start()
        {
            if (thread != null) return;
            running = true;
            thread = new Thread(Run);
            thread.IsBackground = true;
            thread.Name = "EXP OCR";
            thread.Start();
        }

        void Run()
        {
            while (running)
            {
                Stopwatch started = Stopwatch.StartNew();
                Rectangle r;
                lock (gate) r = region;
                if (!paused && r.Width > 0 && r.Height > 0)
                {
                    OcrFrame frame = ReadOnce(r);
                    Action<OcrFrame> handler = FrameReady;
                    if (handler != null) handler(frame);
                    else frame.Dispose();
                }
                wake.WaitOne(Math.Max(100, IntervalMs - (int)started.ElapsedMilliseconds));
            }
        }

        OcrFrame ReadOnce(Rectangle r)
        {
            var frame = new OcrFrame { TimeUtc = DateTime.UtcNow, Region = r };
            Bitmap raw = null;
            try
            {
                if (rotateFilter)
                {
                    rotateFilter = false;
                    preferredAttempt = (preferredAttempt + 1) % OcrService.AttemptCount;
                }
                raw = OcrService.Capture(r);
                bool keep = debugImages;
                OcrRead read = OcrService.Read(raw, ref preferredAttempt, keep);
                frame.Text = read.Text;
                frame.Filter = read.Filter;
                frame.Candidates = read.Candidates;
                frame.Milliseconds = read.Milliseconds;
                frame.Processed = read.Processed;
                if (keep)
                {
                    frame.Raw = raw;
                    raw = null;
                }
            }
            catch (Exception ex)
            {
                // e.g. the screen is locked, or a UAC prompt is showing
                frame.Error = ex.GetBaseException().Message;
            }
            finally
            {
                if (raw != null) raw.Dispose();
            }
            return frame;
        }

        public void Dispose()
        {
            running = false;
            wake.Set();
            if (thread != null && thread.IsAlive) thread.Join(3000);
        }
    }
}
