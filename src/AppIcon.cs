using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace MapleExpTracker
{
    static class Shapes
    {
        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            var p = new GraphicsPath();
            if (d <= 0.1f)
            {
                p.AddRectangle(r);
                return p;
            }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        /// <summary>A vertical gradient; the rectangle is padded to avoid GDI+ edge wrap-around.</summary>
        public static LinearGradientBrush Vertical(RectangleF r, Color top, Color bottom)
        {
            return new LinearGradientBrush(new RectangleF(r.X, r.Y - 1, Math.Max(1, r.Width), r.Height + 2), top, bottom, LinearGradientMode.Vertical);
        }

        public static LinearGradientBrush Vertical(RectangleF r, Color top, Color mid, Color bottom, float midAt)
        {
            LinearGradientBrush b = Vertical(r, top, bottom);
            var blend = new ColorBlend(3);
            blend.Colors = new[] { top, mid, bottom };
            blend.Positions = new[] { 0f, midAt, 1f };
            b.InterpolationColors = blend;
            return b;
        }
    }

    /// <summary>Draws the app icon: an up arrow over a green EXP bar.</summary>
    static class AppIcon
    {
        public static Bitmap Draw(int size)
        {
            var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.ScaleTransform(size / 32f, size / 32f);

                var body = new RectangleF(1.5f, 1.5f, 29f, 29f);
                using (GraphicsPath p = Shapes.RoundRect(body, 7f))
                using (LinearGradientBrush b = Shapes.Vertical(body, Color.FromArgb(0x5A, 0x3E, 0x22), Color.FromArgb(0x1A, 0x15, 0x10)))
                using (var pen = new Pen(Color.FromArgb(0xD9, 0xA8, 0x5C), 2f))
                {
                    g.FillPath(b, p);
                    g.DrawPath(pen, p);
                }

                PointF[] arrow =
                {
                    new PointF(16f, 5f), new PointF(24.5f, 13.5f), new PointF(19.5f, 13.5f), new PointF(19.5f, 17.5f),
                    new PointF(12.5f, 17.5f), new PointF(12.5f, 13.5f), new PointF(7.5f, 13.5f)
                };
                var arrowBox = new RectangleF(7.5f, 5f, 17f, 12.5f);
                using (LinearGradientBrush b = Shapes.Vertical(arrowBox, Color.FromArgb(0xFF, 0xE0, 0x8A), Color.FromArgb(0xE8, 0x8A, 0x1E)))
                    g.FillPolygon(b, arrow);

                var track = new RectangleF(6f, 20.5f, 20f, 6f);
                using (GraphicsPath p = Shapes.RoundRect(track, 2f))
                using (var b = new SolidBrush(Color.FromArgb(0x0A, 0x09, 0x08)))
                    g.FillPath(b, p);
                var fill = new RectangleF(7f, 21.5f, 12.5f, 4f);
                using (GraphicsPath p = Shapes.RoundRect(fill, 1.5f))
                using (LinearGradientBrush b = Shapes.Vertical(fill, Color.FromArgb(0xD8, 0xFF, 0x86), Color.FromArgb(0x5C, 0xA2, 0x20)))
                    g.FillPath(b, p);
            }
            return bmp;
        }

        public static Icon Create(int size)
        {
            using (Bitmap bmp = Draw(size))
            {
                IntPtr handle = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(handle).Clone();
                }
                finally
                {
                    Native.DestroyIcon(handle);
                }
            }
        }

        /// <summary>Writes a multi-size .ico with PNG-compressed images (used for the exe icon).</summary>
        public static void WriteIco(string path)
        {
            int[] sizes = { 16, 20, 24, 32, 40, 48, 64, 128, 256 };
            var images = new List<byte[]>();
            foreach (int s in sizes)
            {
                using (Bitmap bmp = Draw(s))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    images.Add(ms.ToArray());
                }
            }
            using (var w = new BinaryWriter(File.Create(path)))
            {
                w.Write((ushort)0);
                w.Write((ushort)1);
                w.Write((ushort)sizes.Length);
                int offset = 6 + 16 * sizes.Length;
                for (int i = 0; i < sizes.Length; i++)
                {
                    byte dim = (byte)(sizes[i] >= 256 ? 0 : sizes[i]);
                    w.Write(dim);
                    w.Write(dim);
                    w.Write((byte)0);
                    w.Write((byte)0);
                    w.Write((ushort)1);
                    w.Write((ushort)32);
                    w.Write((uint)images[i].Length);
                    w.Write((uint)offset);
                    offset += images[i].Length;
                }
                foreach (byte[] png in images) w.Write(png);
            }
        }
    }
}
