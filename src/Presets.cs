using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Kopfgeld
{
    // What Hunt keeps in its USER folder about its picture: the resolution and window mode it runs
    // in (game.cfg) and the language it loaded (Game.log). Only read, never written.
    sealed class HuntDisplay
    {
        static readonly Regex CfgLine = new Regex(@"^\s*(r_Width|r_Height|r_WindowType)\s*=\s*(-?\d+)", RegexOptions.IgnoreCase);

        // 0 × 0 if unknown.
        public int Width { get; private set; }
        public int Height { get; private set; }
        // 0 window, 1 borderless window, 2 borderless fullscreen, 3 exclusive fullscreen; -1 unknown.
        public int WindowType { get; private set; }
        // "english", "german" …; null if unknown.
        public string Language { get; private set; }

        public static HuntDisplay Read()
        {
            var d = new HuntDisplay { WindowType = -1 };
            string folder = HuntLog.UserFolder();
            string cfg = folder != null ? Path.Combine(folder, "game.cfg") : null;
            if (cfg != null && File.Exists(cfg))
            {
                try
                {
                    int width = 0, height = 0, type = -1;
                    using (var fs = new FileStream(cfg, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var reader = new StreamReader(fs, Encoding.UTF8))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            var m = CfgLine.Match(line);
                            int value;
                            if (!m.Success || !int.TryParse(m.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value)) continue;
                            switch (m.Groups[1].Value.ToLowerInvariant())
                            {
                                case "r_width": width = value; break;
                                case "r_height": height = value; break;
                                default: type = value; break;
                            }
                        }
                    }
                    if (width > 0 && height > 0)
                    {
                        d.Width = width;
                        d.Height = height;
                    }
                    d.WindowType = type;
                }
                catch (Exception)
                {
                }
            }
            d.Language = HuntLog.GameLanguage();
            return d;
        }

        public string Describe()
        {
            var parts = new List<string>();
            parts.Add(Width > 0 ? Width + "×" + Height : "Auflösung unbekannt");
            if (WindowType >= 0) parts.Add("r_WindowType " + WindowType);
            parts.Add(Language ?? "Sprache unbekannt");
            return string.Join(", ", parts);
        }
    }

    // The setup HuntXtract brings along: the frames of a finished setup and clean pictures of its three
    // pages – assets\vorgabe for Hunt in English, assets\vorgabe\<language> for another language once
    // there is one (built with tools\make-preset.ps1). The frames were drawn on 1920 × 1080, but Hunt
    // draws its menus for 16:9 and scales them evenly, so they sit at the same places in 2560 × 1440
    // or 3840 × 2160 too. New users whose game fits get it right away instead of drawing every frame
    // themselves; everyone else can load it on the setup page.
    static class Presets
    {
        public const string Name = "16:9";
        const int SampleWidth = 1920, SampleHeight = 1080;

        static string Root { get { return Path.Combine(AppInfo.AppDir, "assets", "vorgabe"); } }

        public static bool Available
        {
            get { return File.Exists(Path.Combine(Root, "kalibrierung.json")); }
        }

        // The preset for the language Hunt runs in (English if unknown), or null if there is none yet.
        static string Folder(string language)
        {
            if (language == null || language == "english") return Available ? Root : null;
            string folder = Path.Combine(Root, language);
            return File.Exists(Path.Combine(folder, "kalibrierung.json")) ? folder : null;
        }

        // For loading by hand: the language's own preset if there is one, else the English one.
        public static string FolderOrDefault(string language)
        {
            return Folder(language) ?? Root;
        }

        // 16:9 from 1920 × 1080 up – in smaller pictures the numbers get too small to read reliably.
        static bool Fits(int width, int height)
        {
            return width >= SampleWidth && width * 9 == height * 16;
        }

        // The size of Hunt's picture: the screen, which exclusive fullscreen switches to Hunt's own size.
        static Size GameSize(Settings settings, HuntDisplay hunt)
        {
            if (hunt.WindowType == 3 && hunt.Width > 0) return new Size(hunt.Width, hunt.Height);
            return ScreenCapture.ScreenSize(settings.ScreenDevice);
        }

        // The preset folder for this game and screen – or null, with the reason why none fits.
        public static string Find(Settings settings, HuntDisplay hunt, out string folder)
        {
            folder = null;
            if (!Available) return Lang.T("Die fertige Einrichtung fehlt im Programmordner.", "The ready-made setup is missing from the program folder.");
            if (hunt.Width > 0 && !Fits(hunt.Width, hunt.Height))
                return Lang.F("Hunt läuft bei dir in {0} × {1} – die fertige Einrichtung gibt es für 16:9 ab 1920 × 1080.",
                    "Hunt runs in {0} × {1} on your PC – the ready-made setup is for 16:9 from 1920 × 1080 up.", hunt.Width, hunt.Height);
            if (hunt.WindowType == 0)
                return Lang.T("Hunt läuft bei dir im Fenster.", "Hunt runs in a window on your PC.");
            var size = GameSize(settings, hunt);
            if (!Fits(size.Width, size.Height))
                return Lang.F("Dein Bildschirm hat {0} × {1} – die fertige Einrichtung gibt es für 16:9 ab 1920 × 1080.",
                    "Your screen is {0} × {1} – the ready-made setup is for 16:9 from 1920 × 1080 up.", size.Width, size.Height);
            // The pages are recognised by their headings, which are in the game's language.
            folder = Folder(hunt.Language);
            if (folder == null)
                return Lang.F("Für Hunt auf {0} gibt es noch keine fertige Einrichtung.", "There is no ready-made setup for Hunt in {0} yet.", LanguageName(hunt.Language));
            return null;
        }

        static string LanguageName(string language)
        {
            switch (language)
            {
                case "german": return Lang.T("Deutsch", "German");
                case "french": return Lang.T("Französisch", "French");
                case "spanish": return Lang.T("Spanisch", "Spanish");
                case "italian": return Lang.T("Italienisch", "Italian");
                case "polish": return Lang.T("Polnisch", "Polish");
                case "russian": return Lang.T("Russisch", "Russian");
                default: return string.IsNullOrEmpty(language) ? "?" : char.ToUpperInvariant(language[0]) + language.Substring(1);
            }
        }

        public static Dictionary<string, object> ToUi(Settings settings)
        {
            var d = new Dictionary<string, object>();
            d["available"] = Available;
            if (!Available) return d;
            var hunt = HuntDisplay.Read();
            string folder;
            string mismatch = Find(settings, hunt, out folder);
            var size = GameSize(settings, hunt);
            d["fits"] = mismatch == null;
            d["reason"] = mismatch;
            d["resolution"] = size.Width + " × " + size.Height;
            return d;
        }

        // First start: nothing set up yet and the game fits – take the preset. Returns true if it did.
        public static bool ApplyOnFirstStart(Settings settings)
        {
            if (!Available) return false;
            foreach (string file in new[] { AppInfo.CalibrationFile, AppInfo.ReferenceImage, AppInfo.TeamReferenceImage, AppInfo.MapReferenceImage })
            {
                if (File.Exists(file)) return false;
            }
            var hunt = HuntDisplay.Read();
            string folder;
            string mismatch = Find(settings, hunt, out folder);
            if (mismatch != null)
            {
                Log.Write("Keine fertige Einrichtung übernommen (" + hunt.Describe() + "): " + mismatch);
                return false;
            }
            try
            {
                Apply(folder);
                Log.Write("Fertige Einrichtung übernommen (" + hunt.Describe() + ", Vorgabe " + (folder == Root ? "english" : Path.GetFileName(folder)) + ")");
                return true;
            }
            catch (Exception ex)
            {
                Log.Write("Fertige Einrichtung konnte nicht übernommen werden: " + ex.Message);
                return false;
            }
        }

        static List<Calibration> variants;

        // The ready-made setups of all languages, read once.
        static List<Calibration> Variants()
        {
            if (variants != null) return variants;
            var list = new List<Calibration>();
            if (Available)
            {
                var folders = new List<string> { Root };
                folders.AddRange(Directory.GetDirectories(Root));
                foreach (string folder in folders)
                {
                    string file = Path.Combine(folder, "kalibrierung.json");
                    if (!File.Exists(file)) continue;
                    try
                    {
                        var calibration = Json.Deserialize<Calibration>(File.ReadAllText(file, Encoding.UTF8));
                        if (calibration != null) list.Add(calibration);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Vorgabe in " + folder + " nicht lesbar: " + ex.Message);
                    }
                }
            }
            variants = list;
            return list;
        }

        // Where a page's recognition area is the one of the ready-made setups, it also gets their
        // prints of that area – the page as it looks in each of Hunt's languages. Then switching the
        // game's language needs no new setup, for a ready-made setup and for one built on it.
        // Returns in how many languages the Mission Summary is recognised: one per ready-made setup
        // at its place (a setup saved again has a print of its own besides, in one of those languages).
        public static int AttachLanguagePrints(Calibration c)
        {
            var all = Variants();
            var summary = all.Select(v => Tuple.Create(v.Anchor, v.PrintWidth, v.PrintHeight, v.AnchorPrint)).ToList();
            c.LanguagePrints = OtherPrints(c.Anchor, c.PrintWidth, c.PrintHeight, c.AnchorPrint, summary);
            if (c.Team != null)
            {
                c.Team.LanguagePrints = OtherPrints(c.Team.Anchor, c.Team.PrintWidth, c.Team.PrintHeight, c.Team.AnchorPrint,
                    all.Where(v => v.Team != null).Select(v => Tuple.Create(v.Team.Anchor, v.Team.PrintWidth, v.Team.PrintHeight, v.Team.AnchorPrint)));
            }
            if (c.Map != null)
            {
                c.Map.LanguagePrints = OtherPrints(c.Map.Anchor, c.Map.PrintWidth, c.Map.PrintHeight, c.Map.AnchorPrint,
                    all.Where(v => v.Map != null).Select(v => Tuple.Create(v.Map.Anchor, v.Map.PrintWidth, v.Map.PrintHeight, v.Map.AnchorPrint)));
            }
            int languages = summary.Count(v => Serves(v, c.Anchor, c.PrintWidth, c.PrintHeight));
            return Math.Max(1, languages);
        }

        // A preset's print (area, size, print) that fits this recognition area.
        static bool Serves(Tuple<Area, int, int, List<float>> candidate, Area anchor, int width, int height)
        {
            if (anchor == null || !anchor.IsSet || width <= 0 || height <= 0) return false;
            if (candidate.Item1 == null || !SameArea(candidate.Item1, anchor) || candidate.Item2 != width || candidate.Item3 != height) return false;
            return candidate.Item4 != null && candidate.Item4.Count == width * height;
        }

        static List<List<float>> OtherPrints(Area anchor, int width, int height, List<float> own, IEnumerable<Tuple<Area, int, int, List<float>>> candidates)
        {
            var prints = new List<List<float>>();
            foreach (var candidate in candidates)
            {
                if (!Serves(candidate, anchor, width, height)) continue;
                var print = candidate.Item4;
                if ((own != null && own.SequenceEqual(print)) || prints.Any(p => p.SequenceEqual(print))) continue;
                prints.Add(print);
            }
            return prints;
        }

        static bool SameArea(Area a, Area b)
        {
            const double Near = 0.0005;
            return Math.Abs(a.X - b.X) < Near && Math.Abs(a.Y - b.Y) < Near && Math.Abs(a.W - b.W) < Near && Math.Abs(a.H - b.H) < Near;
        }

        // Replaces the setup with a preset: its calibration and the pictures of all three pages.
        // Everything is read and converted first, so a broken preset changes nothing.
        public static void Apply(string folder)
        {
            var calibration = Json.Deserialize<Calibration>(File.ReadAllText(Path.Combine(folder, "kalibrierung.json"), Encoding.UTF8));
            if (calibration == null || calibration.FrameWidth != SampleWidth || calibration.FrameHeight != SampleHeight)
                throw new InvalidDataException("kalibrierung.json der Vorgabe ist nicht in 1920 × 1080 aufgenommen");
            calibration.Preset = Name;
            AttachLanguagePrints(calibration);
            var pictures = new Dictionary<string, Bitmap>();
            try
            {
                pictures[AppInfo.ReferenceImage] = FullSize(folder, "summary.jpg");
                pictures[AppInfo.TeamReferenceImage] = FullSize(folder, "team.jpg");
                pictures[AppInfo.MapReferenceImage] = FullSize(folder, "karte.jpg");
                foreach (var picture in pictures) Imaging.SavePng(picture.Value, picture.Key);
            }
            finally
            {
                foreach (var picture in pictures.Values) picture.Dispose();
            }
            // Detection-sized frames belonged to the setup that is replaced; without them the prints
            // are taken from the pictures above, should the frames be saved again.
            foreach (string file in new[] { AppInfo.ReferenceDetectImage, AppInfo.TeamReferenceDetectImage, AppInfo.MapReferenceDetectImage })
            {
                if (File.Exists(file)) File.Delete(file);
            }
            calibration.Save();
        }

        // The pictures travel scaled down to keep the download small; the setup expects them in the
        // size they were taken in. Everything compared is relative, so they serve every 16:9 size.
        static Bitmap FullSize(string folder, string name)
        {
            using (var source = Imaging.Load(Path.Combine(folder, name)))
            {
                var full = new Bitmap(SampleWidth, SampleHeight, PixelFormat.Format24bppRgb);
                using (var g = Graphics.FromImage(full))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    using (var wrap = new ImageAttributes())
                    {
                        // No dark seam at the edges from sampling outside the picture.
                        wrap.SetWrapMode(WrapMode.TileFlipXY);
                        g.DrawImage(source, new Rectangle(0, 0, SampleWidth, SampleHeight), 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, wrap);
                    }
                }
                return full;
            }
        }
    }
}
