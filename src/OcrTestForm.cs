// MapleStory Classic EXP Tracker. Copyright (c) 2026 Aydrian. All rights reserved. See LICENSE.
using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace MapleExpTracker
{
    /// <summary>Live view of what the tracker captures, what OCR reads, and what the tracker makes of it.</summary>
    public sealed class OcrTestForm : Form
    {
        static readonly Color Back = Color.FromArgb(28, 24, 20);
        static readonly Color Panel = Color.FromArgb(16, 14, 12);
        static readonly Color Text1 = Color.FromArgb(235, 225, 210);
        static readonly Color Gold = Color.FromArgb(0xEB, 0xC4, 0x7C);

        readonly PictureBox rawBox = new PictureBox();
        readonly PictureBox processedBox = new PictureBox();
        readonly TextBox info = new TextBox();

        public event EventHandler ReselectRequested;

        public OcrTestForm()
        {
            Text = "EXP Tracker – OCR Test";
            Icon = AppIcon.Create(32);
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(600, 470);
            MinimumSize = new Size(460, 380);
            TopMost = true;
            BackColor = Back;
            ForeColor = Text1;
            Font = new Font("Segoe UI", 9f);

            Controls.Add(MakeLabel("Captured from the screen:", 12, 10));
            SetupPicture(rawBox, 32);
            Controls.Add(MakeLabel("What the OCR read (after clean-up):", 12, 122));
            SetupPicture(processedBox, 144);

            info.Multiline = true;
            info.ReadOnly = true;
            info.ScrollBars = ScrollBars.Vertical;
            info.BorderStyle = BorderStyle.FixedSingle;
            info.BackColor = Panel;
            info.ForeColor = Text1;
            info.Font = new Font("Consolas", 9.5f);
            info.Location = new Point(12, 236);
            info.Size = new Size(576, 182);
            info.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(info);

            Button reselect = MakeButton("Reselect Region…", new Point(12, 428), 150);
            reselect.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            reselect.Click += delegate
            {
                EventHandler h = ReselectRequested;
                if (h != null) h(this, EventArgs.Empty);
            };
            Button close = MakeButton("Close", new Point(488, 428), 100);
            close.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            close.Click += delegate { Close(); };
            Controls.Add(reselect);
            Controls.Add(close);
        }

        public void ShowWaiting(Rectangle region, string ocrError)
        {
            if (ocrError != null) info.Text = ocrError;
            else if (region.IsEmpty) info.Text = "No EXP region selected yet. Click \"Reselect Region\" and drag a box around the EXP numbers.";
            else info.Text = "Waiting for the next capture…";
        }

        /// <summary>Shows a frame; takes ownership of it.</summary>
        public void ShowFrame(OcrFrame f, ExpTracker t)
        {
            SetImage(rawBox, f.Raw);
            SetImage(processedBox, f.Processed);
            f.Raw = f.Processed = null;

            CultureInfo inv = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            sb.AppendLine("Region     " + f.Region.X + ", " + f.Region.Y + "   (" + f.Region.Width + " x " + f.Region.Height + " px)");
            if (f.Error != null) sb.AppendLine("Error      " + f.Error);
            sb.AppendLine("OCR        " + f.Milliseconds + " ms, filter: " + f.Filter);
            sb.AppendLine("Read       \"" + f.Text + "\"");
            if (f.Candidates.Count == 0)
            {
                sb.AppendLine("Parsed     nothing that looks like EXP");
                sb.AppendLine();
                sb.AppendLine("Tip: box only the EXP numbers, including the percentage, with a");
                sb.AppendLine("little space around them. The game must be visible (not minimized)");
                sb.AppendLine("and in windowed or borderless mode.");
            }
            else
            {
                sb.AppendLine("Parsed     " + f.Candidates[0]);
                for (int i = 1; i < Math.Min(4, f.Candidates.Count); i++) sb.AppendLine("           or " + f.Candidates[i]);
            }
            sb.AppendLine();
            sb.AppendLine("Tracker    " + t.LastNote);
            if (t.Started)
            {
                string pct = double.IsNaN(t.DisplayPercent) ? "?" : t.DisplayPercent.ToString("0.00", inv) + "%";
                sb.AppendLine("Level      " + (t.Level > 0 ? t.Level.ToString(inv) : "not set") + "     EXP " + Fmt.Exp(t.Exp) + " (" + pct + ")");
                double size = t.LevelSize;
                if (t.UsesTable && size > 0) sb.AppendLine("Level size " + Fmt.Exp((long)size) + " EXP (EXP table)");
                else sb.AppendLine("Level size " + (size > 0 ? "~" + Fmt.Exp((long)Math.Round(size)) + " EXP" : "not known yet"));
                sb.AppendLine("Session    " + Fmt.Signed(t.SessionGain) + " EXP");
            }
            info.Text = sb.ToString();
            f.Dispose();
        }

        static void SetImage(PictureBox box, Image image)
        {
            Image old = box.Image;
            box.Image = image;
            if (old != null) old.Dispose();
        }

        void SetupPicture(PictureBox box, int top)
        {
            box.Location = new Point(12, top);
            box.Size = new Size(576, 82);
            box.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            box.SizeMode = PictureBoxSizeMode.Zoom;
            box.BackColor = Panel;
            box.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(box);
        }

        static Label MakeLabel(string text, int x, int y)
        {
            return new Label { Text = text, Location = new Point(x, y), AutoSize = true, ForeColor = Gold };
        }

        static Button MakeButton(string text, Point location, int width)
        {
            var b = new Button
            {
                Text = text,
                Location = location,
                Size = new Size(width, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(58, 44, 30),
                ForeColor = Text1
            };
            b.FlatAppearance.BorderColor = Color.FromArgb(0xA2, 0x7C, 0x47);
            return b;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SetImage(rawBox, null);
                SetImage(processedBox, null);
            }
            base.Dispose(disposing);
        }
    }
}
