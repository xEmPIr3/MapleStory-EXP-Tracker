using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace MapleExpTracker
{
    /// <summary>Full-screen snapshot where the user drags a box around the EXP numbers.</summary>
    public sealed class RegionSelectForm : Form
    {
        const int Zoom = 6, MagW = 40, MagH = 24;

        readonly Rectangle virtualBounds;
        readonly Rectangle previous;    // client coordinates of the current region, if any
        readonly Bitmap shot;
        readonly Bitmap dimmed;
        readonly Rectangle banner;
        Point anchor, cursor;
        bool dragging;
        Rectangle selection;

        public Rectangle Selection { get; private set; }   // screen coordinates

        public RegionSelectForm(Rectangle current)
        {
            virtualBounds = SystemInformation.VirtualScreen;
            shot = new Bitmap(virtualBounds.Width, virtualBounds.Height, PixelFormat.Format32bppPArgb);
            using (Graphics g = Graphics.FromImage(shot))
                g.CopyFromScreen(virtualBounds.Location, Point.Empty, virtualBounds.Size);
            dimmed = (Bitmap)shot.Clone();
            using (Graphics g = Graphics.FromImage(dimmed))
            using (var b = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
                g.FillRectangle(b, 0, 0, dimmed.Width, dimmed.Height);

            if (!current.IsEmpty) previous = new Rectangle(current.X - virtualBounds.X, current.Y - virtualBounds.Y, current.Width, current.Height);
            Point mouse = MousePosition;
            cursor = new Point(mouse.X - virtualBounds.X, mouse.Y - virtualBounds.Y);
            Rectangle screen = Screen.FromPoint(mouse).Bounds;
            banner = new Rectangle(screen.X - virtualBounds.X + screen.Width / 2 - 330, screen.Y - virtualBounds.Y + 28, 660, 62);

            Text = "Select EXP Region";
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            Bounds = virtualBounds;
            ShowInTaskbar = false;
            TopMost = true;
            DoubleBuffered = true;
            KeyPreview = true;
            Cursor = Cursors.Cross;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            Activate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Escape) DialogResult = DialogResult.Cancel;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Right)
            {
                DialogResult = DialogResult.Cancel;
                return;
            }
            if (e.Button != MouseButtons.Left) return;
            Rectangle before = DirtyArea();
            dragging = true;
            anchor = e.Location;
            selection = Rectangle.Empty;
            Invalidate(before);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Rectangle before = DirtyArea();
            cursor = e.Location;
            if (dragging)
                selection = Rectangle.FromLTRB(Math.Min(anchor.X, e.X), Math.Min(anchor.Y, e.Y), Math.Max(anchor.X, e.X), Math.Max(anchor.Y, e.Y));
            Invalidate(before);
            Invalidate(DirtyArea());
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button != MouseButtons.Left || !dragging) return;
            dragging = false;
            if (selection.Width >= 8 && selection.Height >= 6)
            {
                Selection = new Rectangle(selection.X + virtualBounds.X, selection.Y + virtualBounds.Y, selection.Width, selection.Height);
                DialogResult = DialogResult.OK;
                return;
            }
            Rectangle before = DirtyArea();
            selection = Rectangle.Empty;
            Invalidate(before);
        }

        Rectangle DirtyArea()
        {
            Rectangle mag = MagnifierRect();
            mag.Inflate(4, 4);
            if (selection.IsEmpty) return mag;
            Rectangle sel = selection;
            sel.Inflate(6, 6);
            sel.Height += 30;  // size label below the box
            return Rectangle.Union(sel, mag);
        }

        Rectangle MagnifierRect()
        {
            int w = MagW * Zoom, h = MagH * Zoom;
            int x = cursor.X + 28, y = cursor.Y + 28;
            if (x + w > ClientSize.Width) x = cursor.X - 28 - w;
            if (y + h > ClientSize.Height) y = cursor.Y - 28 - h;
            return new Rectangle(x, y, w, h);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Everything is painted in OnPaint.
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            Rectangle clip = e.ClipRectangle;
            g.DrawImage(dimmed, clip, clip, GraphicsUnit.Pixel);

            if (!previous.IsEmpty && selection.IsEmpty)
                using (var pen = new Pen(Color.FromArgb(200, 255, 255, 255), 1f) { DashStyle = DashStyle.Dash })
                    g.DrawRectangle(pen, previous.X - 1, previous.Y - 1, previous.Width + 1, previous.Height + 1);

            if (!selection.IsEmpty)
            {
                g.DrawImage(shot, selection, selection, GraphicsUnit.Pixel);
                using (var pen = new Pen(Color.FromArgb(0xFF, 0xEB, 0xBE, 0x64), 1f))
                    g.DrawRectangle(pen, selection.X - 1, selection.Y - 1, selection.Width + 1, selection.Height + 1);
                DrawSizeLabel(g);
            }
            DrawBanner(g);
            DrawMagnifier(g);
        }

        void DrawSizeLabel(Graphics g)
        {
            string text = selection.Width + " × " + selection.Height;
            using (var font = new Font("Segoe UI", 11f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                SizeF size = g.MeasureString(text, font);
                var box = new RectangleF(selection.X, selection.Bottom + 5, size.Width + 8, size.Height + 2);
                using (var b = new SolidBrush(Color.FromArgb(220, 20, 16, 12))) g.FillRectangle(b, box);
                using (var b = new SolidBrush(Color.FromArgb(0xEB, 0xC4, 0x7C))) g.DrawString(text, font, b, box.X + 4, box.Y + 1);
            }
        }

        void DrawBanner(Graphics g)
        {
            if (!banner.IntersectsWith(Rectangle.Round(g.ClipBounds))) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            using (var path = Shapes.RoundRect(banner, 10f))
            using (var b = new SolidBrush(Color.FromArgb(235, 28, 23, 18)))
            using (var pen = new Pen(Color.FromArgb(0xA2, 0x7C, 0x47), 1.5f))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
            using (var title = new Font("Segoe UI", 17f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var body = new Font("Segoe UI", 13f, FontStyle.Regular, GraphicsUnit.Pixel))
            using (var gold = new SolidBrush(Color.FromArgb(0xEB, 0xC4, 0x7C)))
            using (var cream = new SolidBrush(Color.FromArgb(0xE6, 0xDC, 0xCA)))
            using (var center = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString("Drag a box around your EXP numbers", title, gold, new RectangleF(banner.X, banner.Y + 9, banner.Width, 24), center);
                g.DrawString("Include the whole number and the percentage, e.g.  613128 [62.73%]     •     Esc or right-click to cancel",
                    body, cream, new RectangleF(banner.X, banner.Y + 36, banner.Width, 20), center);
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        void DrawMagnifier(Graphics g)
        {
            Rectangle dst = MagnifierRect();
            var src = new Rectangle(cursor.X - MagW / 2, cursor.Y - MagH / 2, MagW, MagH);
            using (var b = new SolidBrush(Color.Black)) g.FillRectangle(b, dst);
            g.DrawImage(shot, dst, src, GraphicsUnit.Pixel);
            using (var pen = new Pen(Color.FromArgb(160, 255, 80, 60), 1f))
                g.DrawRectangle(pen, dst.X + MagW / 2 * Zoom, dst.Y + MagH / 2 * Zoom, Zoom - 1, Zoom - 1);
            using (var pen = new Pen(Color.FromArgb(0xEB, 0xBE, 0x64), 2f))
                g.DrawRectangle(pen, dst);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                shot.Dispose();
                dimmed.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
