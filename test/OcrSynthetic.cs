using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Linq;

namespace Kopfgeld
{
    // Regression test for the OCR pipeline: draws many values in different fonts, sizes and
    // colours on a textured dark frame, puts loose boxes around them and checks what
    // OcrService reads. Usage: OcrSynthetic.exe
    static class OcrSynthetic
    {
        static readonly Random Rng = new Random(11);

        static void Main()
        {
            string error;
            var ocr = OcrService.TryCreate(out error);
            if (ocr == null)
            {
                Console.WriteLine(error);
                return;
            }
            var calibration = Calibration.CreateDefault();
            string[] values = { "+0", "+1", "+2", "+3", "+4", "+5", "+6", "+7", "+8", "+9", "+10", "+12", "+50", "+99", "+250", "+420", "+670", "+1,250", "+3480", "0", "1", "7", "12", "305", "12,345", "$ 1,250", "-5", "1,880", "+1,880", "1500", "+1500", "+7590", "+460", "+697" };
            string[] fonts = { "Georgia", "Segoe UI", "Times New Roman", "Trebuchet MS" };
            int[] sizes = { 14, 18, 24, 32 };
            Color[] colors = { Color.FromArgb(236, 230, 215), Color.FromArgb(214, 90, 74), Color.FromArgb(226, 190, 120), Color.FromArgb(150, 205, 140) };

            int total = 0, correct = 0;
            var failures = new List<string>();
            foreach (string font in fonts)
            {
                foreach (int size in sizes)
                {
                    using (var frame = new Bitmap(1920, 1080, PixelFormat.Format32bppArgb))
                    {
                        var fields = new List<Tuple<FieldDef, string>>();
                        using (var g = Graphics.FromImage(frame))
                        {
                            g.Clear(Color.FromArgb(20, 18, 16));
                            for (int i = 0; i < 400; i++)
                            {
                                int shade = Rng.Next(10, 42);
                                using (var b = new SolidBrush(Color.FromArgb(120, shade, shade, Math.Max(0, shade - 4))))
                                    g.FillEllipse(b, Rng.Next(0, 1920), Rng.Next(0, 1080), Rng.Next(8, 70), Rng.Next(6, 40));
                            }
                            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                            using (var f = new Font(font, size, FontStyle.Bold, GraphicsUnit.Pixel))
                            {
                                for (int i = 0; i < values.Length; i++)
                                {
                                    int col = i % 4, row = i / 4;
                                    float x = 80 + col * 450, y = 60 + row * 140;
                                    var color = colors[(i + size) % colors.Length];
                                    using (var brush = new SolidBrush(color)) g.DrawString(values[i], f, brush, x, y);
                                    var measured = g.MeasureString(values[i], f);
                                    // Loose box: lots of vertical slack, extra room on both sides.
                                    float bx = x - size * 1.2f, by = y - size * 0.9f, bw = measured.Width + size * 3.0f, bh = measured.Height + size * 1.6f;
                                    var area = new Area { X = bx / 1920, Y = by / 1080, W = bw / 1920, H = bh / 1080 };
                                    fields.Add(Tuple.Create(new FieldDef { Id = "f" + i, Label = values[i], Type = "number", Area = area }, values[i]));
                                }
                            }
                        }
                        foreach (var entry in fields)
                        {
                            var reading = ocr.ReadAsync(frame, entry.Item1, calibration).Result;
                            string expected = new string(entry.Item2.Where(char.IsDigit).ToArray()).TrimStart('0');
                            if (expected.Length == 0) expected = "0";
                            string got = reading.Value == null ? "null" : reading.Value.ToString();
                            total++;
                            if (got == expected) correct++;
                            else failures.Add(font + " " + size + "px '" + entry.Item2 + "' -> " + got + " (raw '" + reading.Raw + "')");
                        }
                    }
                }
            }
            Console.WriteLine("Zahlen richtig: " + correct + "/" + total);
            foreach (string f in failures) Console.WriteLine("  FEHLER " + f);

            // Status words through a status field.
            foreach (string word in new[] { "EXTRAHIERT", "GESTORBEN", "Extracted", "Died" })
            {
                using (var frame = new Bitmap(800, 200, PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(frame))
                    using (var f = new Font("Georgia", 26, FontStyle.Bold, GraphicsUnit.Pixel))
                    {
                        g.Clear(Color.FromArgb(22, 19, 16));
                        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                        g.DrawString(word, f, Brushes.WhiteSmoke, 120, 80);
                    }
                    var field = new FieldDef { Id = "s", Label = "Status", Type = "status", Area = new Area { X = 0.08, Y = 0.25, W = 0.6, H = 0.5 } };
                    var reading = ocr.ReadAsync(frame, field, calibration).Result;
                    Console.WriteLine("Status '" + word + "' -> " + reading.Value + " (raw '" + reading.Raw + "')");
                }
            }
        }
    }
}
