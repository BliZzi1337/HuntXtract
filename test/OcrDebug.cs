using System;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace Kopfgeld
{
    // Debug tool: runs Kopfgeld's OCR on a reference image with a saved calibration and
    // writes the intermediate images (crop, normalized, with context words) per field.
    // Usage: OcrDebug.exe referenz.png kalibrierung.json outDir
    static class OcrDebug
    {
        static void Main(string[] args)
        {
            var frame = Imaging.Load(args[0]);
            var calibration = Json.Deserialize<Calibration>(File.ReadAllText(args[1], Encoding.UTF8));
            string outDir = args[2];
            Directory.CreateDirectory(outDir);
            string error;
            var ocr = OcrService.TryCreate(out error);
            if (ocr == null)
            {
                Console.WriteLine(error);
                return;
            }
            foreach (var field in calibration.Fields.Where(f => f.HasArea))
            {
                var rect = field.Area.ToPixels(frame.Width, frame.Height);
                using (var crop = Imaging.Crop(frame, rect))
                {
                    crop.Save(Path.Combine(outDir, field.Id + "-1-crop.png"), ImageFormat.Png);
                    using (var clean = OcrService.Normalize(crop))
                        clean.Save(Path.Combine(outDir, field.Id + "-2-clean.png"), ImageFormat.Png);
                    using (var glyphs = OcrService.Prepare(crop, field.Type == "number"))
                    using (var composed = OcrService.Compose(glyphs, true, 40))
                        composed.Save(Path.Combine(outDir, field.Id + "-3-context.png"), ImageFormat.Png);
                }
                var reading = ocr.ReadAsync(frame, field, calibration).Result;
                Console.WriteLine(field.Label.PadRight(20) + " raw='" + reading.Raw + "' value=" + (reading.Value ?? "null") + "  " + rect);
            }
        }
    }
}
