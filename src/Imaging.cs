using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;

namespace Kopfgeld
{
    // A rectangle in relative frame coordinates (0..1), so calibrations survive
    // resolution changes as long as the aspect ratio stays the same.
    sealed class Area
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double W { get; set; }
        public double H { get; set; }

        [ScriptIgnore]
        public bool IsSet
        {
            get { return W > 0.002 && H > 0.002; }
        }

        public Rectangle ToPixels(int width, int height)
        {
            int x = (int)Math.Round(X * width), y = (int)Math.Round(Y * height);
            int w = (int)Math.Round(W * width), h = (int)Math.Round(H * height);
            x = Math.Max(0, Math.Min(width - 1, x));
            y = Math.Max(0, Math.Min(height - 1, y));
            w = Math.Max(1, Math.Min(width - x, w));
            h = Math.Max(1, Math.Min(height - y, h));
            return new Rectangle(x, y, w, h);
        }

        public static Area FromUi(object value)
        {
            var d = value as Dictionary<string, object>;
            if (d == null) return null;
            var a = new Area
            {
                X = Clamp01(ToDouble(d, "x")),
                Y = Clamp01(ToDouble(d, "y")),
                W = Clamp01(ToDouble(d, "w")),
                H = Clamp01(ToDouble(d, "h")),
            };
            if (a.X + a.W > 1) a.W = 1 - a.X;
            if (a.Y + a.H > 1) a.H = 1 - a.Y;
            return a.IsSet ? a : null;
        }

        public Dictionary<string, object> ToUi()
        {
            var d = new Dictionary<string, object>();
            d["x"] = X;
            d["y"] = Y;
            d["w"] = W;
            d["h"] = H;
            return d;
        }

        static double ToDouble(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return 0;
            try
            {
                return Convert.ToDouble(v, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return 0;
            }
        }

        static double Clamp01(double v)
        {
            return Math.Max(0, Math.Min(1, v));
        }
    }

    static class Imaging
    {
        public static Bitmap Decode(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes))
            using (var image = Image.FromStream(ms))
            {
                var bmp = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp)) g.DrawImage(image, 0, 0, image.Width, image.Height);
                return bmp;
            }
        }

        public static Bitmap Load(string path)
        {
            return Decode(File.ReadAllBytes(path));
        }

        public static Bitmap Crop(Bitmap source, Rectangle rect)
        {
            return source.Clone(rect, PixelFormat.Format32bppArgb);
        }

        public static byte[] EncodeJpeg(Bitmap source, long quality, int maxWidth)
        {
            Bitmap scaled = null;
            try
            {
                Bitmap target = source;
                if (maxWidth > 0 && source.Width > maxWidth)
                {
                    int h = (int)Math.Round(source.Height * (maxWidth / (double)source.Width));
                    scaled = new Bitmap(maxWidth, h, PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(scaled))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.DrawImage(source, 0, 0, maxWidth, h);
                    }
                    target = scaled;
                }
                var codec = ImageCodecInfo.GetImageEncoders().First(c => c.MimeType == "image/jpeg");
                using (var parameters = new EncoderParameters(1))
                using (var ms = new MemoryStream())
                {
                    parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                    target.Save(ms, codec, parameters);
                    return ms.ToArray();
                }
            }
            finally
            {
                if (scaled != null) scaled.Dispose();
            }
        }

        // Approximates what OBS returns for the small detection frames: plain bilinear
        // downscaling (as the GPU does) and a JPEG round trip at quality 80.
        public static Bitmap SimulateDetectFrame(Bitmap source, int width)
        {
            int height = (int)Math.Round(source.Height * (width / (double)source.Width));
            using (var small = new Bitmap(width, height, PixelFormat.Format24bppRgb))
            {
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.PixelOffsetMode = PixelOffsetMode.Half;
                    g.DrawImage(source, 0, 0, width, height);
                }
                return Decode(EncodeJpeg(small, 80, 0));
            }
        }

        public static byte[] EncodePng(Bitmap source)
        {
            using (var ms = new MemoryStream())
            {
                source.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }

        public static void SavePng(Bitmap source, string path)
        {
            string tmp = path + ".tmp";
            source.Save(tmp, ImageFormat.Png);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }

        public static string DataUrl(byte[] jpeg)
        {
            return "data:image/jpeg;base64," + Convert.ToBase64String(jpeg);
        }

        // Gray values of an area, scaled to a small fixed size.
        public static float[] GraySamples(Bitmap source, Area area, int width, int height)
        {
            Rectangle rect = area.ToPixels(source.Width, source.Height);
            using (var small = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(source, new Rectangle(0, 0, width, height), rect, GraphicsUnit.Pixel);
                }
                var data = small.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var bytes = new byte[data.Stride * height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                small.UnlockBits(data);
                var gray = new float[width * height];
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int i = y * data.Stride + x * 4;
                        gray[y * width + x] = (bytes[i + 2] * 0.299f + bytes[i + 1] * 0.587f + bytes[i] * 0.114f);
                    }
                }
                return gray;
            }
        }

        // Brightness/contrast-independent fingerprint of an area: z-scored gray samples.
        public static float[] Fingerprint(Bitmap source, Area area, int width, int height, out double contrast)
        {
            float[] gray = GraySamples(source, area, width, height);
            double mean = gray.Average();
            double variance = gray.Sum(v => (v - mean) * (v - mean)) / gray.Length;
            contrast = Math.Sqrt(variance);
            double std = Math.Max(4.0, contrast);
            var result = new float[gray.Length];
            for (int i = 0; i < gray.Length; i++) result[i] = (float)((gray[i] - mean) / std);
            return result;
        }

        // 100 = identical, around 0-30 = unrelated.
        // The best of several prints of the same area – a page as it looks in each of Hunt's languages.
        public static double BestSimilarity(float[] print, float[] main, float[][] others)
        {
            double best = Similarity(print, main);
            if (others != null)
            {
                foreach (var other in others) best = Math.Max(best, Similarity(print, other));
            }
            return best;
        }

        public static double Similarity(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0) return 0;
            double sum = 0;
            for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);
            double mad = sum / a.Length;
            return Math.Max(0, Math.Min(100, 100 * (1 - mad / 1.1)));
        }

        // Mean absolute gray difference (0..255) between two sample sets.
        // How far the text in gray samples of a field stands out from their edge – an empty field
        // stays near its background (measured: blank reward slot 12–13, "+375" 141–146).
        public static double InkContrast(float[] gray, int width, int height)
        {
            if (gray == null || gray.Length != width * height || gray.Length == 0) return 0;
            var border = new List<float>(2 * (width + height));
            for (int x = 0; x < width; x++)
            {
                border.Add(gray[x]);
                border.Add(gray[(height - 1) * width + x]);
            }
            for (int y = 0; y < height; y++)
            {
                border.Add(gray[y * width]);
                border.Add(gray[y * width + width - 1]);
            }
            border.Sort();
            float background = border[border.Count / 2];
            var sorted = (float[])gray.Clone();
            Array.Sort(sorted);
            float low = sorted[(int)(sorted.Length * 0.02)];
            float high = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.98))];
            return Math.Max(background - low, high - background);
        }

        public static double Change(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length || a.Length == 0) return 255;
            double sum = 0;
            for (int i = 0; i < a.Length; i++) sum += Math.Abs(a[i] - b[i]);
            return sum / a.Length;
        }

        public static void ThumbSize(Area area, int frameWidth, int frameHeight, out int width, out int height)
        {
            double aspect = (area.H * frameHeight) / Math.Max(1e-6, area.W * frameWidth);
            width = 64;
            height = Math.Max(8, Math.Min(64, (int)Math.Round(64 * aspect)));
        }
    }
}
