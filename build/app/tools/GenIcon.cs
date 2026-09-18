using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

// מחולל האייקון של AutoProcess Twin: ריבוע כהה מעוגל (תואם ל-Theme הכהה של
// האפליקציה עצמה) עם "עין" בצבע האקסנט - מסמלת מעקב/תצפית (מה שהמערכת עושה),
// לא מצלמה/מנעול - כדי לא להיראות כמו אייקון אבטחה גנרי.
class GenIcon
{
    static readonly Color Bg1 = Color.FromArgb(0x0F, 0x17, 0x2A);
    static readonly Color Bg2 = Color.FromArgb(0x1E, 0x29, 0x3B);
    static readonly Color Accent = Color.FromArgb(0x10, 0xB9, 0x81);
    static readonly Color AccentDark = Color.FromArgb(0x05, 0x96, 0x69);

    static Bitmap DrawFrame(int size)
    {
        var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float radius = size * 0.24f;
            var rect = new RectangleF(size * 0.03f, size * 0.03f, size * 0.94f, size * 0.94f);
            using (var path = RoundedRect(rect, radius))
            using (var brush = new LinearGradientBrush(rect, Bg2, Bg1, 45f))
            {
                g.FillPath(brush, path);
            }

            // עין: קווי המתאר (badge שקוף) + אישון בצבע האקסנט עם הילה עדינה.
            float cx = size * 0.5f, cy = size * 0.52f;
            float eyeW = size * 0.66f, eyeH = size * 0.36f;
            using (var eyePath = new GraphicsPath())
            {
                var eyeRect = new RectangleF(cx - eyeW / 2, cy - eyeH / 2, eyeW, eyeH);
                eyePath.AddEllipse(eyeRect);
                using (var pen = new Pen(Color.White, Math.Max(1.5f, size * 0.045f)))
                {
                    g.DrawEllipse(pen, eyeRect);
                }
            }

            float pupilR = size * 0.135f;
            using (var glow = new GraphicsPath())
            {
                glow.AddEllipse(cx - pupilR * 1.6f, cy - pupilR * 1.6f, pupilR * 3.2f, pupilR * 3.2f);
                using (var glowBrush = new PathGradientBrush(glow))
                {
                    glowBrush.CenterColor = Color.FromArgb(120, Accent);
                    glowBrush.SurroundColors = new[] { Color.FromArgb(0, Accent) };
                    g.FillPath(glowBrush, glow);
                }
            }
            using (var pupilBrush = new SolidBrush(Accent))
            {
                g.FillEllipse(pupilBrush, cx - pupilR, cy - pupilR, pupilR * 2, pupilR * 2);
            }
            using (var pupilPen = new Pen(AccentDark, Math.Max(1f, size * 0.02f)))
            {
                g.DrawEllipse(pupilPen, cx - pupilR, cy - pupilR, pupilR * 2, pupilR * 2);
            }
            float highlightR = pupilR * 0.32f;
            using (var hlBrush = new SolidBrush(Color.FromArgb(230, 255, 255, 255)))
            {
                g.FillEllipse(hlBrush, cx - pupilR * 0.45f, cy - pupilR * 0.55f, highlightR, highlightR);
            }
        }
        return bmp;
    }

    static GraphicsPath RoundedRect(RectangleF r, float radius)
    {
        var path = new GraphicsPath();
        float d = radius * 2;
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    static void SaveIco(int[] sizes, string outputPath)
    {
        using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
        using (var bw = new BinaryWriter(fs))
        {
            var pngs = new byte[sizes.Length][];
            for (int i = 0; i < sizes.Length; i++)
            {
                using (var bmp = DrawFrame(sizes[i]))
                using (var ms = new MemoryStream())
                {
                    bmp.Save(ms, ImageFormat.Png);
                    pngs[i] = ms.ToArray();
                }
            }

            bw.Write((short)0);
            bw.Write((short)1);
            bw.Write((short)sizes.Length);

            int offset = 6 + 16 * sizes.Length;
            for (int i = 0; i < sizes.Length; i++)
            {
                int s = sizes[i];
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)(s >= 256 ? 0 : s));
                bw.Write((byte)0);
                bw.Write((byte)0);
                bw.Write((short)1);
                bw.Write((short)32);
                bw.Write(pngs[i].Length);
                bw.Write(offset);
                offset += pngs[i].Length;
            }
            for (int i = 0; i < sizes.Length; i++)
            {
                bw.Write(pngs[i]);
            }
        }
    }

    static void Main(string[] args)
    {
        string outPath = args.Length > 0 ? args[0] : "AppIcon.ico";
        SaveIco(new[] { 16, 24, 32, 48, 64, 128, 256 }, outPath);
        Console.WriteLine("Saved " + outPath);
    }
}
