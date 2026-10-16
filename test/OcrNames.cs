using System;
using System.Drawing;
using System.IO;

namespace Kopfgeld
{
    // Diagnostic for the name reading: shows what the blob analysis makes of a field before
    // the text recognition sees it. Usage: OcrNames.exe <image> <x> <y> <w> <h> [more boxes]
    static class OcrNames
    {
        static void Main(string[] args)
        {
            if (args.Length < 5)
            {
                Console.WriteLine("OcrNames.exe <bild> <x> <y> <breite> <hoehe> [<x> <y> <breite> <hoehe> ...]");
                return;
            }
            string error;
            var ocr = OcrService.TryCreate(out error);
            if (ocr == null)
            {
                Console.WriteLine(error);
                return;
            }
            using (var image = Imaging.Load(args[0]))
            {
                for (int i = 1; i + 3 < args.Length; i += 4)
                {
                    var rect = new Rectangle(int.Parse(args[i]), int.Parse(args[i + 1]), int.Parse(args[i + 2]), int.Parse(args[i + 3]));
                    using (var crop = Imaging.Crop(image, rect))
                    using (var glyphs = OcrService.Prepare(crop, false, true))
                    {
                        var field = new FieldDef
                        {
                            Id = "f",
                            Label = "f",
                            Type = "text",
                            Area = new Area { X = rect.X / (double)image.Width, Y = rect.Y / (double)image.Height, W = rect.Width / (double)image.Width, H = rect.Height / (double)image.Height },
                        };
                        var reading = ocr.ReadAsync(image, field, Calibration.CreateDefault()).Result;
                        Console.WriteLine(rect + ": gelesen \"" + reading.Value + "\", Glyphenhöhe " + glyphs.GlyphHeight +
                            ", Bild " + glyphs.Image.Width + "x" + glyphs.Image.Height + ", Punkt am Ende: " + glyphs.EndsWithPeriod);
                        string file = Path.Combine(Path.GetDirectoryName(args[0]), "glyphen-" + (i / 4) + ".png");
                        glyphs.Image.Save(file);
                    }
                }
            }
        }
    }
}
