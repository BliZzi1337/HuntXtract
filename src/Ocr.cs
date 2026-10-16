using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace Kopfgeld
{
    sealed class FieldReading
    {
        public string Raw;
        public object Value; // long for numbers, string for text/status, null if unreadable
        // Something is clearly drawn in the box, but it is not a number (e.g. the level-50 symbol).
        public bool Mark;
    }

    // Dark glyphs on white, cropped to the text, and the height of the tallest glyph.
    sealed class GlyphImage : IDisposable
    {
        public Bitmap Image;
        public int GlyphHeight;
        // The last mark is small, round and sits on the baseline: a full stop, not a dash.
        public bool EndsWithPeriod;
        // A shape that stands out clearly from the background – not just noise of an empty box.
        public bool Mark;

        public void Dispose()
        {
            Image.Dispose();
        }
    }

    // Reads the marked fields with the OCR engine that ships with Windows 10/11.
    // Windows OCR is unreliable on very short tokens such as "7" or "+0", so each crop is
    // cleaned up first: the actual glyphs are located inside the user's box (however loose
    // it is), leading +/- signs are removed, and the glyphs are scaled to a fixed height.
    // Numbers are then read framed by known words ("Wert … Ende"), which are stripped again,
    // with a few fallback variants for the rare cases the first attempts miss.
    sealed class OcrService
    {
        const string Before = "Wert";
        const string After = "Ende";
        const int TargetGlyph = 40;
        const byte InkThreshold = 150;
        static readonly Regex ContextWords = new Regex("(?i)\\b(wert|ende)\\b");
        static readonly Regex Spaces = new Regex("\\s+");
        // At the highest hunter level the Mission Summary shows a symbol instead of the number.
        const int MaxHunterLevel = 50;
        static readonly Regex LevelLabel = new Regex("(?i)level|lvl|stufe");
        // Brightness difference between a real shape and the background of the box (0–255).
        const int MarkContrast = 60;
        // Languages written in Cyrillic: with one of them installed, Kopfgeld can read names
        // like "Хантер" instead of turning them into gibberish.
        static readonly string[] CyrillicTags = { "ru", "uk", "bg", "sr", "mk", "be", "kk" };

        readonly OcrEngine engine;
        readonly OcrEngine cyrillicEngine;
        readonly object sync = new object();

        public string Language { get; private set; }
        public string CyrillicLanguage { get; private set; }

        OcrService(OcrEngine engine, OcrEngine cyrillicEngine)
        {
            this.engine = engine;
            this.cyrillicEngine = cyrillicEngine;
            Language = engine.RecognizerLanguage.LanguageTag;
            CyrillicLanguage = cyrillicEngine != null ? cyrillicEngine.RecognizerLanguage.LanguageTag : null;
        }

        public static OcrService TryCreate(out string error)
        {
            error = null;
            try
            {
                var engine = OcrEngine.TryCreateFromUserProfileLanguages();
                if (engine == null)
                {
                    var first = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault();
                    if (first != null) engine = OcrEngine.TryCreateFromLanguage(first);
                }
                if (engine == null)
                {
                    error = Lang.T("Windows-Texterkennung nicht verfügbar – in den Windows-Einstellungen unter Zeit & Sprache ein Sprachpaket mit Texterkennung installieren.",
                        "Windows text recognition is not available – install a language pack with optical character recognition in the Windows settings under Time & language.");
                    return null;
                }
                OcrEngine cyrillic = null;
                if (IsCyrillic(engine.RecognizerLanguage.LanguageTag))
                {
                    cyrillic = engine;
                }
                else
                {
                    var language = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(l => IsCyrillic(l.LanguageTag));
                    if (language != null) cyrillic = OcrEngine.TryCreateFromLanguage(language);
                }
                return new OcrService(engine, cyrillic);
            }
            catch (Exception ex)
            {
                error = Lang.T("Windows-Texterkennung nicht verfügbar: ", "Windows text recognition is not available: ") + ex.Message;
                return null;
            }
        }

        static bool IsCyrillic(string languageTag)
        {
            string tag = (languageTag ?? "").ToLowerInvariant();
            return CyrillicTags.Any(t => tag == t || tag.StartsWith(t + "-", StringComparison.Ordinal));
        }

        public static bool HasCyrillicLetters(string text)
        {
            return !string.IsNullOrEmpty(text) && text.Any(c => c >= 'Ѐ' && c <= 'ӿ');
        }

        // One printed line such as the name above the map, read a few ways – the caller picks the
        // reading that makes sense. Only the recognizer of the Windows language: the Cyrillic one
        // turns Latin capitals into lookalikes ("M a" → "М а").
        public async Task<List<string>> ReadLineCandidatesAsync(Bitmap frame, Area area)
        {
            var readings = new List<string>();
            using (var crop = Imaging.Crop(frame, area.ToPixels(frame.Width, frame.Height)))
            {
                readings.Add((await RecognizeAsync(Enlarge(Normalize(crop)))).Trim());
                using (var glyphs = Prepare(crop, false, true))
                {
                    readings.Add((await RecognizeAsync(Compose(glyphs, false, TargetGlyph))).Trim());
                    readings.Add((await RecognizeAsync(Compose(glyphs, false, 56))).Trim());
                    readings.Add((await RecognizeAsync(Compose(glyphs, false, 28))).Trim());
                }
            }
            return readings;
        }

        public async Task<FieldReading> ReadAsync(Bitmap frame, FieldDef field, Calibration calibration)
        {
            Rectangle rect = field.Area.ToPixels(frame.Width, frame.Height);
            using (var crop = Imaging.Crop(frame, rect))
            {
                // "stand" is a number too, only counted differently (a level is not summed up).
                if (field.Type == "number" || field.Type == "stand")
                {
                    var number = await ReadNumberAsync(crop);
                    // Level 50 shows a symbol instead of a number. After a death the box is empty,
                    // so a clear shape without digits in a level field means the highest level.
                    if (number.Value == null && number.Mark && LevelLabel.IsMatch(field.Label ?? ""))
                        return new FieldReading { Raw = Lang.T("Symbol – Level 50", "symbol – level 50"), Value = (long)MaxHunterLevel };
                    return number;
                }
                if (field.Type == "sterne") return CountStars(crop);
                if (field.Type == "status") return await ReadStatusAsync(crop, calibration);
                // Names are read twice: once as the whole box, which keeps the proportions and
                // tells small letters from capitals, and once cut to the glyphs, which is better
                // for small or noisy text. The longer reading wins, ties go to the whole box.
                string plain = (await RecognizeAsync(Enlarge(Normalize(crop)))).Trim();
                using (var glyphs = Prepare(crop, false, true))
                {
                    string text = (await RecognizeAsync(Compose(glyphs, false, TargetGlyph))).Trim();
                    if (text.Length == 0) text = StripContext(await RecognizeAsync(Compose(glyphs, true, TargetGlyph)));
                    if (plain.Length >= text.Length) text = plain;
                    // A full stop at the end is easily read as a dash or comma, or missed.
                    if (glyphs.EndsWithPeriod && text.Length > 0)
                    {
                        char last = text[text.Length - 1];
                        if (last == '-' || last == ',' || last == '_' || last == '·' || last == '\'') text = text.Substring(0, text.Length - 1) + ".";
                        else if (last != '.') text += ".";
                    }
                    // Player names are often written in Cyrillic; the German model only makes
                    // gibberish of those, so a Cyrillic model gets a say when one is installed.
                    if (cyrillicEngine != null && cyrillicEngine != engine)
                    {
                        string other = (await RecognizeAsync(Compose(glyphs, false, TargetGlyph), cyrillicEngine)).Trim();
                        if (HasCyrillicLetters(other) || text.Length == 0) text = other;
                    }
                    return new FieldReading { Raw = text, Value = text.Length > 0 ? text : null };
                }
            }
        }

        async Task<FieldReading> ReadNumberAsync(Bitmap crop)
        {
            using (var glyphs = Prepare(crop, true))
            {
                // Several reads (framed and plain, different glyph sizes); the digit string most
                // of them agree on wins. Ties go to the earlier, usually most reliable variant.
                var reads = new List<string>();
                reads.Add(StripContext(await RecognizeAsync(Compose(glyphs, true, TargetGlyph))));
                reads.Add((await RecognizeAsync(Compose(glyphs, false, TargetGlyph))).Trim());
                reads.Add(StripContext(await RecognizeAsync(Compose(glyphs, true, 56))));
                reads.Add(StripContext(await RecognizeAsync(Compose(glyphs, true, 28))));
                var candidates = reads.Select((text, index) => new { Text = text, Digits = Digits(text), Index = index }).Where(c => c.Digits.Length > 0).ToList();
                var winner = candidates
                    .GroupBy(c => c.Digits)
                    .OrderByDescending(g => g.Count())
                    .ThenBy(g => g.Min(c => c.Index))
                    .Select(g => g.OrderBy(c => c.Index).First())
                    .FirstOrDefault();
                string raw = winner != null ? winner.Text : reads[0];
                string digits = winner != null ? winner.Digits : "";

                // Last resort: the glyphs repeated three times ("0 0 0").
                if (digits.Length == 0)
                {
                    string repeated = (await RecognizeAsync(Repeat(glyphs, 3, TargetGlyph))).Trim();
                    string consensus = Consensus(repeated);
                    if (consensus.Length > 0)
                    {
                        raw = repeated;
                        digits = consensus;
                    }
                }

                long number;
                object value = digits.Length > 0 && digits.Length <= 12 && long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out number)
                    ? (object)number : null;
                return new FieldReading { Raw = raw, Value = value, Mark = value == null && glyphs.Mark };
            }
        }

        async Task<FieldReading> ReadStatusAsync(Bitmap crop, Calibration calibration)
        {
            using (var glyphs = Prepare(crop, false))
            {
                // Short words are sometimes misread; try a few variants until one matches a word list.
                string first = null;
                foreach (var variant in new[] { Tuple.Create(false, TargetGlyph), Tuple.Create(true, TargetGlyph), Tuple.Create(false, 56), Tuple.Create(false, 28) })
                {
                    string text = StripContext(await RecognizeAsync(Compose(glyphs, variant.Item1, variant.Item2)));
                    if (first == null && text.Length > 0) first = text;
                    string status = calibration.ClassifyStatus(text);
                    if (status.Length > 0) return new FieldReading { Raw = text, Value = status };
                }
                return new FieldReading { Raw = first ?? "", Value = "" };
            }
        }

        static string StripContext(string text)
        {
            return Spaces.Replace(ContextWords.Replace(text, " "), " ").Trim();
        }

        // The digit string most of the repeated copies agree on.
        static string Consensus(string text)
        {
            var groups = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(Digits).Where(d => d.Length > 0)
                .GroupBy(d => d).OrderByDescending(g => g.Count()).ToList();
            return groups.Count > 0 && groups[0].Count() >= 2 ? groups[0].Key : "";
        }

        // Keeps the digits, fixing the usual OCR mix-ups (O/0, l/1, S/5 ...).
        public static string Digits(string text)
        {
            text = text.Trim();
            // A leading currency sign ("$ 1,250") is often read as "S" – which is not a 5.
            if (text.StartsWith("$")) text = text.Substring(1);
            else if (text.Length > 2 && (text[0] == 'S' || text[0] == 's') && char.IsWhiteSpace(text[1])) text = text.Substring(2);
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                char d = c;
                if ("OoDQ°".IndexOf(c) >= 0) d = '0';
                else if ("lI|!ij".IndexOf(c) >= 0) d = '1';
                else if (c == 'S' || c == 's') d = '5';
                else if (c == 'B') d = '8';
                else if (c == 'Z' || c == 'z') d = '2';
                else if (c == 'g' || c == 'q') d = '9';
                if (d >= '0' && d <= '9') sb.Append(d);
            }
            return sb.ToString();
        }

        // ------------------------------------------------------------ preprocessing

        sealed class Blob
        {
            public int Id, MinX = int.MaxValue, MinY = int.MaxValue, MaxX = -1, MaxY = -1, Area;
            public int W { get { return MaxX - MinX + 1; } }
            public int H { get { return MaxY - MinY + 1; } }
        }

        // Normalized crop for debugging: the whole box as dark text on white.
        // Windows OCR wants a certain size; blow the box up to roughly 80 pixels high.
        static Bitmap Enlarge(Bitmap source)
        {
            using (source)
            {
                int factor = Math.Max(1, Math.Min(6, (int)Math.Round(140.0 / Math.Max(8, source.Height))));
                if (factor == 1) return new Bitmap(source);
                var bmp = new Bitmap(source.Width * factor, source.Height * factor, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(source, 0, 0, bmp.Width, bmp.Height);
                }
                return bmp;
            }
        }

        internal static Bitmap Normalize(Bitmap crop)
        {
            return ToBitmap(NormalizedGray(crop), crop.Width, new Rectangle(0, 0, crop.Width, crop.Height), null, null);
        }

        internal static GlyphImage Prepare(Bitmap crop, bool stripSigns)
        {
            return Prepare(crop, stripSigns, false);
        }

        // keepSmall: for names, small marks belong to the text – a full stop, the dot on an i,
        // an umlaut. For numbers they are only texture and are dropped.
        internal static GlyphImage Prepare(Bitmap crop, bool stripSigns, bool keepSmall)
        {
            int w = crop.Width, h = crop.Height;
            int contrast;
            byte[] gray = NormalizedGray(crop, out contrast);
            int[] labels;
            var blobs = FindBlobs(gray, w, h, out labels);

            // Ignore frame lines and boxes of the game UI, then specks and texture remnants.
            var candidates = blobs.Where(b => !IsLineOrFrame(b, w, h)).ToList();
            int tallest = candidates.Count > 0 ? candidates.Max(b => b.H) : 0;
            var tall = candidates.Where(b => b.H >= Math.Max(2, tallest * 0.3) && b.Area >= 3).ToList();
            var keep = tall;
            if (keepSmall && tall.Count > 0)
            {
                // Small marks count when they sit within the line the tall letters span.
                int top = tall.Min(b => b.MinY), bottom = tall.Max(b => b.MaxY);
                keep = candidates.Where(b => tall.Contains(b) || (b.Area >= 3 && b.MinY >= top && b.MaxY <= bottom)).ToList();
            }
            keep = keep.OrderBy(b => b.MinX).ToList();
            if (stripSigns)
            {
                // A leading "+" or "-" is recognised by its shape, not just its height: fonts
                // with old-style figures have short digits too (a "1" can be as short as a "+").
                // What stands before the sign is the end of the label next to the number – in
                // German "Gesammelte Trophäen" reaches into a box drawn for "Bounty Collected", and
                // left there the "+" was read as a "4" ("+175" became 4175) – so it goes too.
                int sign = keep.FindIndex(b => IsSign(b, tallest, labels, w));
                if (sign > 0 && sign < keep.Count - 1) keep.RemoveRange(0, sign);
                while (keep.Count > 1 && IsSign(keep[0], tallest, labels, w)) keep.RemoveAt(0);
            }
            if (keep.Count == 0)
                return new GlyphImage { Image = ToBitmap(gray, w, new Rectangle(0, 0, w, h), null, null), GlyphHeight = Math.Max(4, h) };

            int minX = keep.Min(b => b.MinX), minY = keep.Min(b => b.MinY), maxX = keep.Max(b => b.MaxX), maxY = keep.Max(b => b.MaxY);
            int glyph = Math.Max(4, keep.Max(b => b.H));
            int margin = Math.Max(3, glyph / 3);
            var region = Rectangle.FromLTRB(Math.Max(0, minX - margin), Math.Max(0, minY - margin), Math.Min(w, maxX + 1 + margin), Math.Min(h, maxY + 1 + margin));
            var kept = new HashSet<int>(keep.Select(b => b.Id));
            return new GlyphImage
            {
                Image = ToBitmap(gray, w, region, labels, kept),
                GlyphHeight = glyph,
                EndsWithPeriod = keepSmall && IsPeriod(keep[keep.Count - 1], tall),
                // Noise in an empty box stays close to the background and makes only small specks.
                Mark = contrast >= MarkContrast && glyph >= h * 0.25,
            };
        }

        // A full stop: much smaller than the letters, about as wide as tall, and it sits on
        // the baseline. A dash would be flat and float in the middle of the line.
        static bool IsPeriod(Blob last, List<Blob> tall)
        {
            if (last == null || tall.Count == 0) return false;
            int baseline = tall.Max(b => b.MaxY), height = tall.Max(b => b.H);
            if (last.H == height) return false;
            // A few pixels tall at most, roughly square (anti-aliasing flattens it) and down at
            // the baseline – a dash would be wider still and float in the middle of the line.
            double aspect = last.W / (double)Math.Max(1, last.H);
            return last.H <= height * 0.45 && aspect >= 0.5 && aspect <= 2.6 && last.MaxY >= baseline - Math.Max(1, height * 0.2);
        }

        // "-": a flat dash. "+": a full horizontal and a full vertical bar through the middle
        // with empty corners – which a "1" (even one with a wide foot serif) does not have.
        static bool IsSign(Blob b, int tallest, int[] labels, int width)
        {
            if (b.H > tallest * 0.85) return false;
            double aspect = b.W / (double)b.H;
            int cx = (b.MinX + b.MaxX) / 2, cy = (b.MinY + b.MaxY) / 2;
            double row = 0, col = 0;
            for (int y = Math.Max(b.MinY, cy - 1); y <= Math.Min(b.MaxY, cy + 1); y++)
            {
                int n = 0;
                for (int x = b.MinX; x <= b.MaxX; x++) if (labels[y * width + x] == b.Id) n++;
                row = Math.Max(row, n / (double)b.W);
            }
            if (aspect >= 1.8 && b.H <= tallest * 0.35) return row >= 0.8;
            if (aspect < 0.6 || aspect > 1.6) return false;
            for (int x = Math.Max(b.MinX, cx - 1); x <= Math.Min(b.MaxX, cx + 1); x++)
            {
                int n = 0;
                for (int y = b.MinY; y <= b.MaxY; y++) if (labels[y * width + x] == b.Id) n++;
                col = Math.Max(col, n / (double)b.H);
            }
            if (row < 0.8 || col < 0.8) return false;
            int cw = Math.Max(1, b.W / 4), ch = Math.Max(1, b.H / 4);
            return CornerEmpty(b, labels, width, b.MinX, b.MinY, cw, ch) && CornerEmpty(b, labels, width, b.MaxX - cw + 1, b.MinY, cw, ch) &&
                   CornerEmpty(b, labels, width, b.MinX, b.MaxY - ch + 1, cw, ch) && CornerEmpty(b, labels, width, b.MaxX - cw + 1, b.MaxY - ch + 1, cw, ch);
        }

        static bool CornerEmpty(Blob b, int[] labels, int width, int x0, int y0, int w, int h)
        {
            for (int y = y0; y < y0 + h; y++)
                for (int x = x0; x < x0 + w; x++)
                    if (labels[y * width + x] == b.Id) return false;
            return true;
        }

        static bool IsLineOrFrame(Blob b, int w, int h)
        {
            if (b.W >= w * 0.8 && b.H <= h * 0.3) return true;                                                  // horizontal line
            if (b.H >= h * 0.8 && b.W <= Math.Max(3, w * 0.1) && (b.MinX <= 1 || b.MaxX >= w - 2)) return true; // vertical edge
            if (b.W >= w * 0.8 && b.H >= h * 0.8 && b.Area < 0.35 * b.W * b.H) return true;                     // hollow box/diamond
            return false;
        }

        static List<Blob> FindBlobs(byte[] gray, int w, int h, out int[] labels)
        {
            labels = new int[w * h];
            for (int i = 0; i < labels.Length; i++) labels[i] = -1;
            var blobs = new List<Blob>();
            var stack = new Stack<int>();
            for (int start = 0; start < gray.Length; start++)
            {
                if (labels[start] >= 0 || gray[start] >= InkThreshold) continue;
                var blob = new Blob { Id = blobs.Count };
                labels[start] = blob.Id;
                stack.Push(start);
                while (stack.Count > 0)
                {
                    int p = stack.Pop();
                    int x = p % w, y = p / w;
                    blob.Area++;
                    if (x < blob.MinX) blob.MinX = x;
                    if (x > blob.MaxX) blob.MaxX = x;
                    if (y < blob.MinY) blob.MinY = y;
                    if (y > blob.MaxY) blob.MaxY = y;
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if ((dx == 0 && dy == 0) || nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                            int q = ny * w + nx;
                            if (labels[q] >= 0 || gray[q] >= InkThreshold) continue;
                            labels[q] = blob.Id;
                            stack.Push(q);
                        }
                    }
                }
                blobs.Add(blob);
            }
            return blobs;
        }

        // Grayscale mapped to dark text on white: the background level is the border median,
        // the text is whatever differs most from it. Works for light and dark text.
        // MMR rating: count the lit stars in the marked area. No text recognition needed – lit
        // stars are nearly white, empty ones dark grey, so the bright columns are counted as
        // groups. Every group at least three pixels wide is one star.
        static FieldReading CountStars(Bitmap crop)
        {
            int w = crop.Width, h = crop.Height;
            if (w < 6 || h < 3) return new FieldReading { Raw = "", Value = null };
            byte[] gray = Gray(crop);
            int brightest = 0;
            foreach (byte v in gray) if (v > brightest) brightest = v;
            if (brightest < 70) return new FieldReading { Raw = Lang.T("keine Sterne", "no stars"), Value = 0L };
            int threshold = Math.Max(90, (int)(brightest * 0.6));

            int stars = 0, run = 0, runPixels = 0;
            int minPixels = Math.Max(2, h / 4);
            for (int x = 0; x < w; x++)
            {
                int bright = 0;
                for (int y = 0; y < h; y++) if (gray[y * w + x] >= threshold) bright++;
                if (bright > 0)
                {
                    run++;
                    runPixels += bright;
                }
                else
                {
                    if (run >= 3 && runPixels >= minPixels) stars++;
                    run = 0;
                    runPixels = 0;
                }
            }
            if (run >= 3 && runPixels >= minPixels) stars++;
            return new FieldReading { Raw = stars == 1 ? Lang.T("1 Stern", "1 star") : Lang.F("{0} Sterne", "{0} stars", stars), Value = (long)stars };
        }

        static byte[] Gray(Bitmap crop)
        {
            int w = crop.Width, h = crop.Height;
            var data = crop.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new byte[data.Stride * h];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            int stride = data.Stride;
            crop.UnlockBits(data);
            var gray = new byte[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    gray[y * w + x] = (byte)((px[i + 2] * 299 + px[i + 1] * 587 + px[i] * 114) / 1000);
                }
            return gray;
        }

        static byte[] NormalizedGray(Bitmap crop)
        {
            int contrast;
            return NormalizedGray(crop, out contrast);
        }

        // contrast: how far the text level lies from the background (0–255).
        static byte[] NormalizedGray(Bitmap crop, out int contrast)
        {
            int w = crop.Width, h = crop.Height;
            var data = crop.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var px = new byte[data.Stride * h];
            Marshal.Copy(data.Scan0, px, 0, px.Length);
            int stride = data.Stride;
            crop.UnlockBits(data);

            var gray = new byte[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    gray[y * w + x] = (byte)((px[i + 2] * 299 + px[i + 1] * 587 + px[i] * 114) / 1000);
                }
            }

            var border = new List<byte>(2 * (w + h));
            for (int x = 0; x < w; x++)
            {
                border.Add(gray[x]);
                border.Add(gray[(h - 1) * w + x]);
            }
            for (int y = 0; y < h; y++)
            {
                border.Add(gray[y * w]);
                border.Add(gray[y * w + w - 1]);
            }
            border.Sort();
            int background = border[border.Count / 2];
            var sorted = (byte[])gray.Clone();
            Array.Sort(sorted);
            int low = sorted[(int)(sorted.Length * 0.02)];
            int high = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * 0.98))];
            bool lightText = (background - low) < (high - background);
            int textLevel = lightText ? high : low;
            contrast = Math.Abs(textLevel - background);
            double range = Math.Max(24, contrast);

            var output = new byte[w * h];
            for (int i = 0; i < w * h; i++)
            {
                double t = Math.Abs(gray[i] - background) / range;
                t = Math.Max(0, Math.Min(1, (t - 0.25) / 0.6));
                output[i] = (byte)(255 - t * 255);
            }
            return output;
        }

        // Copies a region of the gray image into a bitmap. Pixels of blobs that were not kept
        // (and their direct neighbours, the anti-aliased edge) are painted white.
        static Bitmap ToBitmap(byte[] gray, int width, Rectangle region, int[] labels, HashSet<int> kept)
        {
            int height = gray.Length / width;
            var bytes = new byte[region.Width * region.Height * 4];
            for (int y = 0; y < region.Height; y++)
            {
                for (int x = 0; x < region.Width; x++)
                {
                    int sx = region.X + x, sy = region.Y + y;
                    byte v = gray[sy * width + sx];
                    if (labels != null && TouchesDropped(labels, kept, width, height, sx, sy)) v = 255;
                    int o = (y * region.Width + x) * 4;
                    bytes[o] = v;
                    bytes[o + 1] = v;
                    bytes[o + 2] = v;
                    bytes[o + 3] = 255;
                }
            }
            var bmp = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
            var data = bmp.LockBits(new Rectangle(0, 0, region.Width, region.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            for (int y = 0; y < region.Height; y++) Marshal.Copy(bytes, y * region.Width * 4, data.Scan0 + y * data.Stride, region.Width * 4);
            bmp.UnlockBits(data);
            return bmp;
        }

        static bool TouchesDropped(int[] labels, HashSet<int> kept, int w, int h, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int label = labels[ny * w + nx];
                    if (label >= 0 && !kept.Contains(label)) return true;
                }
            }
            return false;
        }

        // Scales the glyphs to the target height and optionally frames them with the context words.
        internal static Bitmap Compose(GlyphImage glyphs, bool withContext, int targetGlyph)
        {
            double scale = Math.Max(0.5, Math.Min(8.0, targetGlyph / (double)glyphs.GlyphHeight));
            int sw = Math.Max(1, (int)Math.Round(glyphs.Image.Width * scale));
            int sh = Math.Max(1, (int)Math.Round(glyphs.Image.Height * scale));
            // Segoe UI capitals are ~70 % of the font size, so this matches the digit height.
            using (var font = new Font("Segoe UI", targetGlyph * 1.4f, FontStyle.Bold, GraphicsUnit.Pixel))
            {
                Size before = Size.Empty, after = Size.Empty;
                if (withContext)
                {
                    using (var probe = new Bitmap(1, 1))
                    using (var g = Graphics.FromImage(probe))
                    {
                        before = Size.Ceiling(g.MeasureString(Before, font));
                        after = Size.Ceiling(g.MeasureString(After, font));
                    }
                }
                int pad = Math.Max(10, targetGlyph / 2), gap = Math.Max(8, targetGlyph / 2);
                int content = withContext ? Math.Max(sh, before.Height) : sh;
                int width = withContext ? pad + before.Width + gap + sw + gap + after.Width + pad : pad + sw + pad;
                var canvas = new Bitmap(width, content + 2 * pad, PixelFormat.Format32bppArgb);
                using (var g = Graphics.FromImage(canvas))
                {
                    g.Clear(Color.White);
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    int x = pad;
                    if (withContext)
                    {
                        g.DrawString(Before, font, Brushes.Black, pad, pad + (content - before.Height) / 2);
                        x = pad + before.Width + gap;
                    }
                    g.DrawImage(glyphs.Image, new Rectangle(x, pad + (content - sh) / 2, sw, sh));
                    if (withContext) g.DrawString(After, font, Brushes.Black, x + sw + gap, pad + (content - after.Height) / 2);
                }
                return canvas;
            }
        }

        // The glyphs several times in a row, separated like words: "0 0 0".
        static Bitmap Repeat(GlyphImage glyphs, int count, int targetGlyph)
        {
            double scale = Math.Max(0.5, Math.Min(8.0, targetGlyph / (double)glyphs.GlyphHeight));
            int sw = Math.Max(1, (int)Math.Round(glyphs.Image.Width * scale));
            int sh = Math.Max(1, (int)Math.Round(glyphs.Image.Height * scale));
            int pad = Math.Max(10, targetGlyph / 2), gap = targetGlyph;
            var canvas = new Bitmap(pad * 2 + count * sw + (count - 1) * gap, sh + 2 * pad, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(canvas))
            {
                g.Clear(Color.White);
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                for (int i = 0; i < count; i++) g.DrawImage(glyphs.Image, new Rectangle(pad + i * (sw + gap), pad, sw, sh));
            }
            return canvas;
        }

        Task<string> RecognizeAsync(Bitmap bmp)
        {
            return RecognizeAsync(bmp, engine);
        }

        async Task<string> RecognizeAsync(Bitmap bmp, OcrEngine with)
        {
            SoftwareBitmap software;
            using (bmp)
            {
                var data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                var bytes = new byte[data.Stride * bmp.Height];
                Marshal.Copy(data.Scan0, bytes, 0, bytes.Length);
                bmp.UnlockBits(data);
                var writer = new DataWriter();
                writer.WriteBytes(bytes);
                software = SoftwareBitmap.CreateCopyFromBuffer(writer.DetachBuffer(), BitmapPixelFormat.Bgra8, bmp.Width, bmp.Height, BitmapAlphaMode.Premultiplied);
            }
            using (software)
            {
                IAsyncOperation<OcrResult> operation;
                lock (sync) operation = with.RecognizeAsync(software);
                var result = await AsTask(operation);
                return string.Join(" ", result.Lines.Select(l => l.Text));
            }
        }

        // The usual AsTask() extension lives in an assembly that does not match the
        // Windows metadata on disk, so await WinRT operations by hand.
        static Task<T> AsTask<T>(IAsyncOperation<T> operation)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            operation.Completed = (op, status) =>
            {
                if (status == AsyncStatus.Completed) tcs.TrySetResult(op.GetResults());
                else if (status == AsyncStatus.Error) tcs.TrySetException(op.ErrorCode);
                else tcs.TrySetCanceled();
            };
            return tcs.Task;
        }
    }
}
