// MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Windows.Forms;

namespace MapleExpTracker
{
    /// <summary>The always-on-top tracker panel (a per-pixel-alpha layered window).</summary>
    public sealed class OverlayForm : Form
    {
        static readonly float[] Sizes = { 0.75f, 1f, 1.25f, 1.5f, 1.75f, 2f };
        static readonly float[] Opacities = { 1f, 0.9f, 0.8f, 0.7f, 0.6f, 0.5f };
        static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(8);
        static readonly TimeSpan HelpAfter = TimeSpan.FromSeconds(12);

        readonly Settings settings;
        readonly ExpTracker tracker = new ExpTracker();
        readonly OcrWorker worker = new OcrWorker();
        readonly OverlayRenderer renderer = new OverlayRenderer();
        readonly NotifyIcon tray;
        readonly ContextMenuStrip menu;
        readonly System.Windows.Forms.Timer timer;
        ToolStripMenuItem pauseItem, clickThroughItem, visibleItem, sizeMenu, opacityMenu, themeMenu;
        OcrTestForm testForm;

        bool paused;
        DateTime pauseStartedUtc;
        TimeSpan pausedTotal;
        DateTime lastGoodUtc = DateTime.MinValue;
        DateTime regionSetUtc = DateTime.UtcNow;
        string ocrError;
        string lastRenderKey;
        string lastLoggedText;
        bool settingsHover;
        float renderScale = 1f;
        bool positionDirty;
        int ticks;

        public OverlayForm()
        {
            settings = Settings.Load();
            tracker.Level = settings.Level;
            renderer.Palette = Palette.ByName(settings.Theme);

            Text = "MapleStory EXP Tracker";
            FormBorderStyle = FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            TopMost = true;
            Icon = AppIcon.Create(32);
            Size = new Size(OverlayRenderer.BaseWidth, OverlayRenderer.BaseHeight);
            Location = InitialLocation();

            menu = BuildMenu();
            tray = new NotifyIcon();
            tray.Icon = Icon;
            tray.Text = "MapleStory EXP Tracker - Created by Aydrian";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.DoubleClick += delegate { ToggleOverlay(); };

            worker.IntervalMs = settings.IntervalMs;
            worker.FrameReady += OnWorkerFrame;
            worker.SetRegion(settings.Region);

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 1000;
            timer.Tick += OnTick;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= Native.WS_EX_LAYERED | Native.WS_EX_TOOLWINDOW | Native.WS_EX_NOACTIVATE;
                if (settings != null && settings.ClickThrough) cp.ExStyle |= Native.WS_EX_TRANSPARENT;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Render(true);
            timer.Start();
            if (!OcrService.TryInit(out ocrError))
            {
                Render(true);
                MessageBox.Show(ocrError, "MapleStory EXP Tracker", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            worker.Start();
            if (settings.Region.IsEmpty)
                tray.ShowBalloonTip(10000, "MapleStory EXP Tracker",
                    "Click the gear at the top right of the tracker, choose \"Select EXP Region\", then drag a box around the EXP numbers in game.", ToolTipIcon.Info);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            timer.Stop();
            worker.Dispose();
            settings.Save();
            tray.Visible = false;
            if (testForm != null && !testForm.IsDisposed) testForm.Close();
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tray.Dispose();
                menu.Dispose();
                timer.Dispose();
            }
            base.Dispose(disposing);
        }

        // ------------------------------------------------------------ tracking

        DateTime TrackerNow()
        {
            // The tracker's clock stops while paused, so breaks don't drag down EXP/h.
            return (paused ? pauseStartedUtc : DateTime.UtcNow) - pausedTotal;
        }

        void OnWorkerFrame(OcrFrame frame)
        {
            try
            {
                if (IsDisposed || !IsHandleCreated)
                {
                    frame.Dispose();
                    return;
                }
                BeginInvoke(new Action<OcrFrame>(HandleFrame), frame);
            }
            catch (InvalidOperationException)
            {
                frame.Dispose();
            }
        }

        void HandleFrame(OcrFrame frame)
        {
            if (paused || IsDisposed)
            {
                frame.Dispose();
                return;
            }
            int levelBefore = tracker.Level;
            FeedOutcome outcome = tracker.Feed(frame.Candidates, TrackerNow());
            switch (outcome)
            {
                case FeedOutcome.Started:
                case FeedOutcome.Unchanged:
                case FeedOutcome.Gained:
                case FeedOutcome.Jumped:
                case FeedOutcome.Lost:
                case FeedOutcome.LevelUp:
                case FeedOutcome.Resynced:
                    lastGoodUtc = DateTime.UtcNow;
                    break;
                case FeedOutcome.Rejected:
                    worker.TryOtherFilter();
                    break;
            }
            if (tracker.Level != levelBefore)
            {
                settings.Level = tracker.Level;
                settings.Save();
            }
            LogFrame(frame, outcome, levelBefore);
            if (testForm != null && !testForm.IsDisposed && testForm.Visible) testForm.ShowFrame(frame, tracker);
            else frame.Dispose();
            Render(false);
        }

        /// <summary>Notes level changes, re-syncs and ignored readings (each distinct text once).</summary>
        void LogFrame(OcrFrame frame, FeedOutcome outcome, int levelBefore)
        {
            bool notable = tracker.Level != levelBefore || outcome == FeedOutcome.Started || outcome == FeedOutcome.LevelUp
                || outcome == FeedOutcome.Resynced || outcome == FeedOutcome.Lost || outcome == FeedOutcome.Jumped;
            bool ignored = outcome == FeedOutcome.Rejected || outcome == FeedOutcome.Confirming;
            if (!notable && !(ignored && frame.Text != lastLoggedText)) return;
            lastLoggedText = frame.Text;
            string pct = double.IsNaN(tracker.DisplayPercent) ? "?" : tracker.DisplayPercent.ToString("0.00", CultureInfo.InvariantCulture);
            TrackerLog.Write(outcome + "  level " + levelBefore + (tracker.Level != levelBefore ? " -> " + tracker.Level : "")
                + "  now " + Fmt.Exp(tracker.Exp) + " [" + pct + "%]  read \"" + frame.Text + "\" via " + frame.Filter
                + "  (" + tracker.LastNote + ")");
        }

        void OnTick(object sender, EventArgs e)
        {
            ticks++;
            Render(false);
            if (Visible && ticks % 3 == 0)
                Native.SetWindowPos(Handle, Native.HWND_TOPMOST, 0, 0, 0, 0, Native.SWP_NOMOVE | Native.SWP_NOSIZE | Native.SWP_NOACTIVATE);
            if (positionDirty)
            {
                positionDirty = false;
                settings.Save();
            }
        }

        // ------------------------------------------------------------ drawing

        OverlayView BuildView()
        {
            DateTime now = TrackerNow();
            var v = new OverlayView();
            v.Name = settings.Name;
            v.Level = tracker.Level;
            v.EtaLabel = tracker.Level > 0 ? "TO LV. " + (tracker.Level + 1).ToString(CultureInfo.InvariantCulture) : "TO LEVEL UP";

            if (ocrError != null) v.Message = "Windows OCR unavailable";
            else if (settings.Region.IsEmpty) v.Message = "Click the gear → Select EXP Region";
            else if (!tracker.Started)
            {
                if (paused) v.Message = "Paused";
                else if (DateTime.UtcNow - regionSetUtc > HelpAfter) v.Message = "Can't see EXP — click the gear → Test OCR";
                else v.Message = "Reading EXP…";
            }

            if (tracker.Started)
            {
                v.HasExp = true;
                v.Exp = tracker.Exp;
                v.LevelTotal = (long)Math.Round(tracker.LevelSize);
                v.LevelTotalExact = tracker.UsesTable;
                v.Percent = tracker.DisplayPercent;
                long recent = tracker.RecentGain(now);
                v.Recent = Fmt.Signed(recent);
                v.RecentNegative = recent < 0;
                v.Session = Fmt.Signed(tracker.SessionGain);
                v.SessionNegative = tracker.SessionGain < 0;
                double rate;
                if (tracker.TryGetRate(now, out rate)) v.Rate = Fmt.Short(rate);
                if (tracker.TryGetRate(now, ExpTracker.LongRateWindow, out rate)) v.RateHour = Fmt.Short(rate);
                v.SessionTime = Fmt.Clock(tracker.SessionLength(now));
                TimeSpan eta;
                if (tracker.TryGetTimeToLevel(now, out eta)) v.Eta = Fmt.Duration(eta);
            }

            v.SettingsHover = settingsHover;
            if (paused) v.Status = OverlayStatus.Paused;
            else if (ocrError != null || settings.Region.IsEmpty) v.Status = OverlayStatus.NoRegion;
            else if (tracker.Started && DateTime.UtcNow - lastGoodUtc < StaleAfter) v.Status = OverlayStatus.Live;
            else v.Status = OverlayStatus.Waiting;
            return v;
        }

        void Render(bool force)
        {
            if (!IsHandleCreated || IsDisposed) return;
            OverlayView view = BuildView();
            float scale = settings.Scale * DpiScale();
            string key = view.Key() + "|" + scale.ToString(CultureInfo.InvariantCulture) + "|" + settings.Opacity.ToString(CultureInfo.InvariantCulture)
                + "|" + renderer.Palette.Name;
            if (!force && key == lastRenderKey) return;
            lastRenderKey = key;
            renderScale = scale;
            using (Bitmap bmp = renderer.Render(view, scale))
                ApplyBitmap(bmp, (byte)Math.Round(255 * settings.Opacity));
        }

        float DpiScale()
        {
            try
            {
                uint dpi = Native.GetDpiForWindow(Handle);
                return dpi > 0 ? dpi / 96f : 1f;
            }
            catch (EntryPointNotFoundException)
            {
                return 1f;
            }
        }

        void ApplyBitmap(Bitmap bmp, byte opacity)
        {
            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBitmap = bmp.GetHbitmap(Color.FromArgb(0));
            IntPtr old = Native.SelectObject(memDc, hBitmap);
            try
            {
                var size = new Native.SIZE(bmp.Width, bmp.Height);
                var source = new Native.POINT(0, 0);
                var target = new Native.POINT(Left, Top);
                var blend = new Native.BLENDFUNCTION
                {
                    BlendOp = Native.AC_SRC_OVER,
                    SourceConstantAlpha = opacity,
                    AlphaFormat = Native.AC_SRC_ALPHA
                };
                Native.UpdateLayeredWindow(Handle, screenDc, ref target, ref size, memDc, ref source, 0, ref blend, Native.ULW_ALPHA);
            }
            finally
            {
                Native.SelectObject(memDc, old);
                Native.DeleteObject(hBitmap);
                Native.DeleteDC(memDc);
                Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        // ------------------------------------------------------------ mouse and window

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (OverSettings(e.Location))
            {
                ShowSettingsMenu();
                return;
            }
            if (e.Clicks >= 2)
            {
                OnOverlayDoubleClick(e.Location);
                return;
            }
            // Let Windows move the borderless window as if its caption were dragged.
            Native.ReleaseCapture();
            Native.SendMessage(Handle, Native.WM_NCLBUTTONDOWN, (IntPtr)Native.HTCAPTION, IntPtr.Zero);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Right)
            {
                Native.SetForegroundWindow(Handle);
                menu.Show(Cursor.Position);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            SetSettingsHover(OverSettings(e.Location));
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            SetSettingsHover(false);
        }

        bool OverSettings(Point client)
        {
            return renderer.SettingsArea.Contains(client.X / renderScale, client.Y / renderScale);
        }

        void SetSettingsHover(bool hover)
        {
            if (hover == settingsHover) return;
            settingsHover = hover;
            Cursor = hover ? Cursors.Hand : Cursors.Default;
            Render(false);
        }

        /// <summary>Opens the settings menu just below the gear button, right-aligned with it.</summary>
        void ShowSettingsMenu()
        {
            RectangleF area = renderer.SettingsArea;
            var anchor = new Point((int)Math.Round(area.Right * renderScale), (int)Math.Round(area.Bottom * renderScale) + 2);
            Native.SetForegroundWindow(Handle);
            menu.Show(PointToScreen(anchor), ToolStripDropDownDirection.BelowLeft);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case Native.WM_MOUSEACTIVATE:
                    m.Result = (IntPtr)Native.MA_NOACTIVATE;
                    return;
                case Native.WM_NCLBUTTONDBLCLK:
                    // Don't let a double-click on the fake caption maximize the window.
                    OnOverlayDoubleClick(PointToClient(Cursor.Position));
                    return;
                case Native.WM_DPICHANGED:
                    base.WndProc(ref m);
                    BeginInvoke(new Action(() => Render(true)));
                    return;
            }
            base.WndProc(ref m);
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (settings == null) return;
            settings.X = Left;
            settings.Y = Top;
            settings.HasPosition = true;
            positionDirty = true;
        }

        void OnOverlayDoubleClick(Point client)
        {
            var p = new PointF(client.X / renderScale, client.Y / renderScale);
            if (renderer.LevelArea.Contains(p) || renderer.NameArea.Contains(p)) EditCharacter();
        }

        Point InitialLocation()
        {
            if (settings.HasPosition)
            {
                var p = new Point(settings.X, settings.Y);
                foreach (Screen s in Screen.AllScreens)
                    if (s.WorkingArea.Contains(new Point(p.X + 60, p.Y + 30))) return p;
            }
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            return new Point(wa.Left + 40, wa.Top + 40);
        }

        // ------------------------------------------------------------ menu actions

        ContextMenuStrip BuildMenu()
        {
            var m = new ContextMenuStrip();
            m.Items.Add("Select EXP Region…", null, delegate { SelectRegion(); });
            m.Items.Add("Test OCR…", null, delegate { ShowOcrTest(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Set Level / Name…", null, delegate { EditCharacter(); });
            m.Items.Add("Reset Session", null, delegate { ResetSession(); });
            pauseItem = new ToolStripMenuItem("Pause Tracking", null, delegate { TogglePause(); });
            m.Items.Add(pauseItem);
            m.Items.Add(new ToolStripSeparator());

            sizeMenu = new ToolStripMenuItem("Size");
            foreach (float s in Sizes)
            {
                float value = s;
                var item = new ToolStripMenuItem(Percent(value), null, delegate
                {
                    settings.Scale = value;
                    settings.Save();
                    Render(true);
                });
                item.Tag = value;
                sizeMenu.DropDownItems.Add(item);
            }
            m.Items.Add(sizeMenu);

            opacityMenu = new ToolStripMenuItem("Opacity");
            foreach (float o in Opacities)
            {
                float value = o;
                var item = new ToolStripMenuItem(Percent(value), null, delegate
                {
                    settings.Opacity = value;
                    settings.Save();
                    Render(true);
                });
                item.Tag = value;
                opacityMenu.DropDownItems.Add(item);
            }
            m.Items.Add(opacityMenu);

            themeMenu = new ToolStripMenuItem("Theme");
            foreach (Palette p in Palette.All)
            {
                Palette value = p;
                var item = new ToolStripMenuItem(p.DisplayName, OverlayRenderer.Swatch(p, 16), delegate
                {
                    settings.Theme = value.Name;
                    renderer.Palette = value;
                    settings.Save();
                    Render(true);
                });
                item.Tag = value;
                themeMenu.DropDownItems.Add(item);
            }
            m.Items.Add(themeMenu);

            clickThroughItem = new ToolStripMenuItem("Click-Through (undo from tray icon)", null, delegate { ToggleClickThrough(); });
            m.Items.Add(clickThroughItem);
            visibleItem = new ToolStripMenuItem("Hide Tracker", null, delegate { ToggleOverlay(); });
            m.Items.Add(visibleItem);
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add("Exit", null, delegate { Close(); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(new ToolStripMenuItem("Created by Aydrian") { Enabled = false });

            m.Opening += delegate
            {
                pauseItem.Checked = paused;
                clickThroughItem.Checked = settings.ClickThrough;
                visibleItem.Text = Visible ? "Hide Tracker" : "Show Tracker";
                foreach (ToolStripMenuItem item in sizeMenu.DropDownItems)
                    item.Checked = Math.Abs((float)item.Tag - settings.Scale) < 0.01f;
                foreach (ToolStripMenuItem item in opacityMenu.DropDownItems)
                    item.Checked = Math.Abs((float)item.Tag - settings.Opacity) < 0.01f;
                foreach (ToolStripMenuItem item in themeMenu.DropDownItems)
                    item.Checked = item.Tag == renderer.Palette;
            };
            return m;
        }

        static string Percent(float value)
        {
            return Math.Round(value * 100).ToString(CultureInfo.InvariantCulture) + "%";
        }

        void SelectRegion()
        {
            bool wasVisible = Visible;
            bool testVisible = testForm != null && !testForm.IsDisposed && testForm.Visible;
            Hide();
            if (testVisible) testForm.Hide();
            Application.DoEvents();
            Thread.Sleep(200);  // let the windows disappear before the screenshot

            Rectangle picked = Rectangle.Empty;
            using (var picker = new RegionSelectForm(settings.Region))
                if (picker.ShowDialog() == DialogResult.OK) picked = picker.Selection;

            if (wasVisible) Show();
            if (testVisible) testForm.Show();
            if (!picked.IsEmpty)
            {
                settings.Region = picked;
                settings.Save();
                tracker.BeginResync();
                regionSetUtc = DateTime.UtcNow;
                worker.SetRegion(picked);
                if (tracker.Level <= 0) EditCharacter();
            }
            Render(true);
        }

        void ShowOcrTest()
        {
            if (testForm == null || testForm.IsDisposed)
            {
                testForm = new OcrTestForm();
                testForm.ReselectRequested += delegate { SelectRegion(); };
                testForm.FormClosed += delegate { worker.DebugImages = false; };
            }
            worker.DebugImages = true;
            testForm.ShowWaiting(settings.Region, ocrError);
            testForm.Show();
            testForm.Activate();
        }

        void EditCharacter()
        {
            int level = tracker.Level;
            string name = settings.Name;
            if (!CharacterDialog.Ask(ref level, ref name)) return;
            tracker.Level = level;
            settings.Level = level;
            settings.Name = name;
            settings.Save();
            Render(true);
        }

        void ResetSession()
        {
            if (tracker.Started) tracker.ResetSession(TrackerNow());
            Render(true);
        }

        void TogglePause()
        {
            if (!paused)
            {
                paused = true;
                pauseStartedUtc = DateTime.UtcNow;
                worker.Paused = true;
            }
            else
            {
                pausedTotal += DateTime.UtcNow - pauseStartedUtc;
                paused = false;
                tracker.BeginResync();  // EXP gained while paused isn't counted
                worker.Paused = false;
            }
            Render(true);
        }

        void ToggleClickThrough()
        {
            settings.ClickThrough = !settings.ClickThrough;
            settings.Save();
            int ex = Native.GetWindowLong(Handle, Native.GWL_EXSTYLE);
            ex = settings.ClickThrough ? ex | Native.WS_EX_TRANSPARENT : ex & ~Native.WS_EX_TRANSPARENT;
            Native.SetWindowLong(Handle, Native.GWL_EXSTYLE, ex);
            if (settings.ClickThrough)
                tray.ShowBalloonTip(6000, "Click-Through is on",
                    "The tracker now ignores the mouse. Right-click the tray icon to turn Click-Through off.", ToolTipIcon.Info);
        }

        void ToggleOverlay()
        {
            if (Visible)
            {
                Hide();
            }
            else
            {
                Show();
                Render(true);
            }
        }
    }
}
