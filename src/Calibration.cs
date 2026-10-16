using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Kopfgeld
{
    sealed class FieldDef
    {
        public string Id { get; set; }
        public string Label { get; set; }
        public string Type { get; set; } // number | stand | sterne | status | text
        public Area Area { get; set; }
        // The Mission Summary shows this only when the hunter got out, e.g. Hunter Level.
        public bool SurvivorOnly { get; set; }
        // Not read from the screen: HuntXtract works this one out itself (own MMR).
        public bool Derived { get; set; }

        [ScriptIgnore]
        public bool HasArea
        {
            get { return Area != null && Area.IsSet; }
        }
    }

    // A screen HuntXtract recognises by a marked area (its anchor), compared with the small
    // detection frames: the Team Details tab and the map.
    abstract class AnchoredPage
    {
        public Area Anchor { get; set; }
        public List<float> AnchorPrint { get; set; }
        public int PrintWidth { get; set; }
        public int PrintHeight { get; set; }
        public double AnchorContrast { get; set; }

        float[] printCache;
        List<List<float>> languagePrints;
        float[][] languageCache;

        [ScriptIgnore]
        public float[] Print
        {
            get
            {
                if (printCache == null && AnchorPrint != null) printCache = AnchorPrint.ToArray();
                return printCache;
            }
        }

        // The same area as the page shows it in Hunt's other languages (from the ready-made setups
        // whose area here is the same one): the page counts in each of them, so switching the
        // game's language needs no new setup.
        public List<List<float>> LanguagePrints
        {
            get { return languagePrints; }
            set { languagePrints = value; languageCache = null; }
        }

        [ScriptIgnore]
        public float[][] LanguagePrintArrays
        {
            get
            {
                if (languageCache == null && languagePrints != null) languageCache = languagePrints.Where(p => p != null).Select(p => p.ToArray()).ToArray();
                return languageCache;
            }
        }

        [ScriptIgnore]
        public bool HasAnchor
        {
            get { return Anchor != null && Anchor.IsSet && AnchorPrint != null && AnchorPrint.Count == PrintWidth * PrintHeight && AnchorPrint.Count > 0; }
        }

        public void ComputePrint(Bitmap reference, Bitmap detectFrame, int detectWidth)
        {
            int w, h;
            Imaging.ThumbSize(Anchor, reference.Width, reference.Height, out w, out h);
            PrintWidth = w;
            PrintHeight = h;
            double contrast;
            if (detectFrame != null)
            {
                AnchorPrint = Imaging.Fingerprint(detectFrame, Anchor, w, h, out contrast).ToList();
            }
            else
            {
                using (var simulated = Imaging.SimulateDetectFrame(reference, detectWidth))
                    AnchorPrint = Imaging.Fingerprint(simulated, Anchor, w, h, out contrast).ToList();
            }
            AnchorContrast = contrast;
            printCache = null;
        }

        // How much a detection frame shows this screen (0–100), in whichever language.
        public double Similarity(Bitmap frame)
        {
            double contrast;
            return Imaging.BestSimilarity(Imaging.Fingerprint(frame, Anchor, PrintWidth, PrintHeight, out contrast), Print, LanguagePrintArrays);
        }
    }

    // The second screen at the end of a match: the "Team Details" tab with the player names and
    // their MMR stars. HuntXtract recognises it on its own and adds the values to the match that
    // was just saved from the Mission Summary.
    sealed class TeamPage : AnchoredPage
    {
        public List<FieldDef> Fields { get; set; }

        [ScriptIgnore]
        public bool IsReady
        {
            get { return HasAnchor && Fields != null && Fields.Any(f => f.HasArea); }
        }

        public static TeamPage CreateDefault()
        {
            return new TeamPage { Fields = DefaultFields() };
        }

        public static List<FieldDef> DefaultFields()
        {
            var fields = new List<FieldDef>
            {
                new FieldDef { Id = "matchMmr", Type = "sterne" },
                new FieldDef { Id = "player1", Type = "text" },
                new FieldDef { Id = "player1Mmr", Type = "sterne" },
                new FieldDef { Id = "player2", Type = "text" },
                new FieldDef { Id = "player2Mmr", Type = "sterne" },
                new FieldDef { Id = "player3", Type = "text" },
                new FieldDef { Id = "player3Mmr", Type = "sterne" },
                new FieldDef { Id = "ownMmr", Type = "sterne", Derived = true },
            };
            foreach (var field in fields) field.Label = Lang.T(DefaultLabels[field.Id][0], DefaultLabels[field.Id][1]);
            return fields;
        }

        // The names HuntXtract gives these fields, in German and English. A field that still has
        // one of them follows the language; a name you typed yourself stays.
        static readonly Dictionary<string, string[]> DefaultLabels = new Dictionary<string, string[]>
        {
            { "matchMmr", new[] { "Match MMR", "Match MMR" } },
            { "player1", new[] { "Spieler 1", "Player 1" } },
            { "player1Mmr", new[] { "Spieler 1 MMR", "Player 1 MMR" } },
            { "player2", new[] { "Spieler 2", "Player 2" } },
            { "player2Mmr", new[] { "Spieler 2 MMR", "Player 2 MMR" } },
            { "player3", new[] { "Spieler 3", "Player 3" } },
            { "player3Mmr", new[] { "Spieler 3 MMR", "Player 3 MMR" } },
            { "ownMmr", new[] { "Eigene MMR", "Own MMR" } },
        };

        public bool LocalizeLabels()
        {
            bool changed = false;
            foreach (var field in Fields ?? new List<FieldDef>())
            {
                string[] names;
                if (field.Id == null || !DefaultLabels.TryGetValue(field.Id, out names)) continue;
                if (field.Label != names[0] && field.Label != names[1]) continue;
                string wanted = Lang.T(names[0], names[1]);
                if (field.Label == wanted) continue;
                field.Label = wanted;
                changed = true;
            }
            return changed;
        }

        // Which row is mine? The one whose name matches the player name from the settings – the
        // rows swap places between matches. Its stars go into the derived "Eigene MMR" field.
        // Returns the name field that matched, or null.
        public FieldDef DeriveOwnMmr(Dictionary<string, object> values, string playerName)
        {
            var target = (Fields ?? new List<FieldDef>()).FirstOrDefault(f => f.Derived);
            if (target == null) return null;
            values[target.Id] = null;
            string wanted = Simplify(playerName ?? "");
            if (wanted.Length < 3) return null;
            FieldDef best = null;
            int bestDistance = int.MaxValue, runnerUp = int.MaxValue;
            foreach (var name in Fields.Where(f => f.Type == "text" && f.HasArea))
            {
                object read;
                if (!values.TryGetValue(name.Id, out read) || read == null) continue;
                string got = Simplify(Convert.ToString(read, CultureInfo.InvariantCulture));
                if (got.Length == 0) continue;
                int distance = got == wanted ? 0
                    : got.Length >= 4 && (got.Contains(wanted) || wanted.Contains(got)) ? 1
                    : Distance(got, wanted);
                if (distance < bestDistance)
                {
                    runnerUp = bestDistance;
                    bestDistance = distance;
                    best = name;
                }
                else if (distance < runnerUp)
                {
                    runnerUp = distance;
                }
            }
            // Names come from text recognition, so a few wrong characters are normal. The row is
            // only taken when it is the closest one by a clear margin.
            int allowed = Math.Max(2, wanted.Length * 2 / 5);
            if (best == null || bestDistance > allowed || bestDistance + 2 > runnerUp) return null;
            // The row is mine, so the spelling from the settings beats whatever OCR made of it.
            values[best.Id] = (playerName ?? "").Trim();
            var partner = RowPartner(best);
            object stars;
            if (partner == null || !values.TryGetValue(partner.Id, out stars)) return best;
            values[target.Id] = stars;
            return best;
        }

        // A star field that belongs to a player row (a name sits next to it) – those are the
        // MMRs of the team, which the overview shows as one average instead of one card each.
        public bool IsRowMmr(FieldDef stars)
        {
            if (stars == null || stars.Type != "sterne" || stars.Derived || !stars.HasArea || Fields == null) return false;
            double center = stars.Area.Y + stars.Area.H / 2;
            return Fields.Any(f => f.Type == "text" && f.HasArea && center >= f.Area.Y - f.Area.H * 0.5 && center <= f.Area.Y + f.Area.H * 1.5);
        }

        // The star field of the same row: its box lies at the same height as the name box.
        FieldDef RowPartner(FieldDef name)
        {
            double center = name.Area.Y + name.Area.H / 2;
            return Fields.Where(f => f.Type == "sterne" && !f.Derived && f.HasArea)
                .Where(f => center >= f.Area.Y - f.Area.H * 0.5 && center <= f.Area.Y + f.Area.H * 1.5)
                .OrderBy(f => Math.Abs(f.Area.Y + f.Area.H / 2 - center))
                .FirstOrDefault();
        }

        internal static int Distance(string a, string b)
        {
            var row = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) row[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                int previous = row[0];
                row[0] = i;
                for (int j = 1; j <= b.Length; j++)
                {
                    int old = row[j];
                    row[j] = Math.Min(Math.Min(row[j] + 1, row[j - 1] + 1), previous + (a[i - 1] == b[j - 1] ? 0 : 1));
                    previous = old;
                }
            }
            return row[b.Length];
        }

        static string Simplify(string text)
        {
            var sb = new StringBuilder();
            foreach (char c in (text ?? "").ToLowerInvariant())
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }
    }

    // What the user taught the app about the end-of-match screen: a distinctive "anchor"
    // area to recognise it by, and the boxes of the values to read.
    sealed class Calibration
    {
        static readonly Regex SafeId = new Regex("^[A-Za-z][A-Za-z0-9_]{0,31}$");

        public int Version { get; set; }
        public int FrameWidth { get; set; }
        public int FrameHeight { get; set; }
        // Set while this is the ready-made setup the program brings along ("1920x1080"); gone as soon
        // as the user saves their own.
        public string Preset { get; set; }
        public Area Anchor { get; set; }
        public List<float> AnchorPrint { get; set; }
        public int PrintWidth { get; set; }
        public int PrintHeight { get; set; }
        public double AnchorContrast { get; set; }
        public int Threshold { get; set; }
        // 2 = anchor print taken from a detection-sized frame (see ComputeAnchorPrint).
        public int PrintVersion { get; set; }
        public List<FieldDef> Fields { get; set; }
        // Optional second screen: the Team Details tab of the same match.
        public TeamPage Team { get; set; }
        // Optional: the map on Tab, for the trail of the match.
        public MapPage Map { get; set; }
        public List<string> ExtractedWords { get; set; }
        public List<string> DiedWords { get; set; }

        // Everything that can end up in a match, from both screens.
        [ScriptIgnore]
        public List<FieldDef> AllFields
        {
            get
            {
                var all = new List<FieldDef>(Fields ?? new List<FieldDef>());
                if (Team != null && Team.Fields != null) all.AddRange(Team.Fields);
                return all;
            }
        }

        [ScriptIgnore]
        public bool HasAnchor
        {
            get { return Anchor != null && Anchor.IsSet && AnchorPrint != null && AnchorPrint.Count == PrintWidth * PrintHeight && AnchorPrint.Count > 0; }
        }

        [ScriptIgnore]
        public bool HasFields
        {
            get { return Fields != null && Fields.Any(f => f.HasArea); }
        }

        [ScriptIgnore]
        public bool IsReady
        {
            get { return HasAnchor && HasFields; }
        }

        [ScriptIgnore]
        public FieldDef StatusField
        {
            get { return Fields == null ? null : Fields.FirstOrDefault(f => f.Type == "status"); }
        }

        [ScriptIgnore]
        public List<FieldDef> SurvivorFields
        {
            get { return Fields == null ? new List<FieldDef>() : Fields.Where(f => f.SurvivorOnly && f.HasArea && f.Type != "status").ToList(); }
        }

        // Extracted or died, from fields the Mission Summary only shows for a hunter who got out
        // (Hunter Level, Hunter Progress …). Empty when the user marked no such field.
        public string DeriveOutcome(Dictionary<string, object> values)
        {
            var markers = SurvivorFields;
            if (markers.Count == 0 || values == null) return "";
            foreach (var field in markers)
            {
                object v;
                if (values.TryGetValue(field.Id, out v) && v != null && Convert.ToString(v, CultureInfo.InvariantCulture).Trim().Length > 0)
                    return "extrahiert";
            }
            return "gestorben";
        }

        float[] printCache;
        List<List<float>> languagePrints;
        float[][] languageCache;

        [ScriptIgnore]
        public float[] Print
        {
            get
            {
                if (printCache == null && AnchorPrint != null) printCache = AnchorPrint.ToArray();
                return printCache;
            }
        }

        // The Mission Summary's anchor as it looks in Hunt's other languages (see AnchoredPage).
        public List<List<float>> LanguagePrints
        {
            get { return languagePrints; }
            set { languagePrints = value; languageCache = null; }
        }

        [ScriptIgnore]
        public float[][] LanguagePrintArrays
        {
            get
            {
                if (languageCache == null && languagePrints != null) languageCache = languagePrints.Where(p => p != null).Select(p => p.ToArray()).ToArray();
                return languageCache;
            }
        }

        // How much a detection frame shows the Mission Summary (0–100), in whichever language.
        public double Similarity(Bitmap frame)
        {
            double contrast;
            return Imaging.BestSimilarity(Imaging.Fingerprint(frame, Anchor, PrintWidth, PrintHeight, out contrast), Print, LanguagePrintArrays);
        }

        public static Calibration CreateDefault()
        {
            return new Calibration
            {
                Version = 2,
                Threshold = 80,
                Fields = DefaultFields(),
                Team = TeamPage.CreateDefault(),
                Map = MapPage.CreateDefault(),
                ExtractedWords = new List<string> { "EXTRAHIERT", "EXTRACTED", "ÜBERLEBT", "SURVIVED", "ENTKOMMEN", "ESCAPED" },
                DiedWords = new List<string> { "GESTORBEN", "TOT", "DIED", "DEAD", "KILLED", "GEFALLEN" },
            };
        }

        // Labels as the game's Mission Summary shows them.
        public static List<FieldDef> DefaultFields()
        {
            return new List<FieldDef>
            {
                new FieldDef { Id = "status", Label = "Status", Type = "status" },
                new FieldDef { Id = "hunterKills", Label = "Hunters Killed", Type = "number" },
                new FieldDef { Id = "monsterKills", Label = "Monsters Killed", Type = "number" },
                new FieldDef { Id = "bounty", Label = "Bounty Collected", Type = "number" },
                new FieldDef { Id = "huntDollars", Label = "Hunt Dollars", Type = "number" },
                new FieldDef { Id = "xp", Label = "Hunter Progress", Type = "number", SurvivorOnly = true },
            };
        }

        // A level is a state, not something to add up: switch those fields to the "stand" type once.
        static readonly HashSet<string> LevelLabels = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase)
        {
            "Hunter Level", "Hunter-Level", "Level", "Hunter Rank", "Rang",
        };

        // Values only a surviving hunter has – the Mission Summary leaves them out after a death.
        static readonly HashSet<string> SurvivorLabels = new HashSet<string>(StringComparer.CurrentCultureIgnoreCase)
        {
            "Hunter Level", "Hunter Progress", "Hunter-Level", "Hunter Rang", "Erfahrung (XP)",
        };

        // Earlier default labels (German) → official English terms. Labels the user changed stay.
        static readonly Dictionary<string, string[]> LabelRenames = new Dictionary<string, string[]>
        {
            { "hunterKills", new[] { "Hunter getötet", "Hunters Killed" } },
            { "monsterKills", new[] { "Monster getötet", "Monsters Killed" } },
            { "bounty", new[] { "Kopfgeld", "Bounty Collected" } },
            { "xp", new[] { "Erfahrung (XP)", "Hunter Progress" } },
        };

        // "extrahiert", "gestorben" or "" if the text matches neither word list.
        public string ClassifyStatus(string text)
        {
            string t = Simplify(text);
            if (t.Length == 0) return "";
            foreach (string w in DiedWords ?? new List<string>())
                if (w.Length > 0 && t.Contains(Simplify(w))) return "gestorben";
            foreach (string w in ExtractedWords ?? new List<string>())
                if (w.Length > 0 && t.Contains(Simplify(w))) return "extrahiert";
            return "";
        }

        static string Simplify(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string upper = text.ToUpperInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in upper)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetter(c)) sb.Append(c);
            }
            return sb.ToString();
        }

        // The anchor print is compared against the small JPEG frames of the live detection, so
        // it is taken from such a frame: either one grabbed from OBS together with the
        // reference, or the full-size reference scaled down the same way.
        public void ComputeAnchorPrint(Bitmap reference, Bitmap detectFrame, int detectWidth)
        {
            FrameWidth = reference.Width;
            FrameHeight = reference.Height;
            int w, h;
            Imaging.ThumbSize(Anchor, reference.Width, reference.Height, out w, out h);
            PrintWidth = w;
            PrintHeight = h;
            double contrast;
            if (detectFrame != null)
            {
                AnchorPrint = Imaging.Fingerprint(detectFrame, Anchor, w, h, out contrast).ToList();
            }
            else
            {
                using (var simulated = Imaging.SimulateDetectFrame(reference, detectWidth))
                    AnchorPrint = Imaging.Fingerprint(simulated, Anchor, w, h, out contrast).ToList();
            }
            AnchorContrast = contrast;
            PrintVersion = 2;
            printCache = null;
        }

        public Dictionary<string, object> ToUi()
        {
            var d = new Dictionary<string, object>();
            d["ready"] = IsReady;
            d["hasAnchor"] = HasAnchor;
            d["hasFields"] = HasFields;
            d["hasReference"] = File.Exists(AppInfo.ReferenceImage);
            d["anchor"] = Anchor != null ? Anchor.ToUi() : null;
            d["anchorContrast"] = Math.Round(AnchorContrast, 1);
            d["threshold"] = Threshold;
            d["frameWidth"] = FrameWidth;
            d["frameHeight"] = FrameHeight;
            d["preset"] = Preset;
            d["fields"] = FieldsToUi(Fields);
            var team = new Dictionary<string, object>();
            team["ready"] = Team != null && Team.IsReady;
            team["hasAnchor"] = Team != null && Team.HasAnchor;
            team["hasReference"] = File.Exists(AppInfo.TeamReferenceImage);
            team["anchor"] = Team != null && Team.Anchor != null ? Team.Anchor.ToUi() : null;
            team["anchorContrast"] = Team != null ? Math.Round(Team.AnchorContrast, 1) : 0;
            team["fields"] = FieldsToUi(Team != null ? Team.Fields : null);
            d["team"] = team;
            var map = Map ?? MapPage.CreateDefault();
            var m = new Dictionary<string, object>();
            m["ready"] = map.IsReady;
            m["hasAnchor"] = map.HasAnchor;
            m["hasReference"] = File.Exists(AppInfo.MapReferenceImage);
            m["anchor"] = map.Anchor != null ? map.Anchor.ToUi() : null;
            m["mapArea"] = map.MapArea != null ? map.MapArea.ToUi() : null;
            m["nameArea"] = map.NameArea != null ? map.NameArea.ToUi() : null;
            d["map"] = m;
            d["extractedWords"] = ExtractedWords ?? new List<string>();
            d["diedWords"] = DiedWords ?? new List<string>();
            return d;
        }

        static List<object> FieldsToUi(List<FieldDef> fields)
        {
            return (fields ?? new List<FieldDef>()).Select(f =>
            {
                var fd = new Dictionary<string, object>();
                fd["id"] = f.Id;
                fd["label"] = f.Label;
                fd["type"] = f.Type;
                fd["survivorOnly"] = f.SurvivorOnly;
                fd["derived"] = f.Derived;
                fd["area"] = f.HasArea ? f.Area.ToUi() : null;
                return (object)fd;
            }).ToList();
        }

        public void ApplyFromUi(Dictionary<string, object> d)
        {
            if (d.ContainsKey("anchor")) Anchor = Area.FromUi(d["anchor"]);
            if (d.ContainsKey("threshold")) Threshold = (int)Math.Max(40, Math.Min(99, Json.Long(d, "threshold", Threshold)));
            if (d.ContainsKey("fields")) Fields = FieldsFromUi(Json.List(d, "fields"));
            if (d.ContainsKey("team"))
            {
                var t = Json.Obj(d["team"]) ?? new Dictionary<string, object>();
                if (Team == null) Team = TeamPage.CreateDefault();
                if (t.ContainsKey("anchor")) Team.Anchor = Area.FromUi(t["anchor"]);
                if (t.ContainsKey("fields")) Team.Fields = FieldsFromUi(Json.List(t, "fields"));
            }
            if (d.ContainsKey("map"))
            {
                var m = Json.Obj(d["map"]) ?? new Dictionary<string, object>();
                if (Map == null) Map = MapPage.CreateDefault();
                if (m.ContainsKey("anchor")) Map.Anchor = Area.FromUi(m["anchor"]);
                if (m.ContainsKey("mapArea")) Map.MapArea = Area.FromUi(m["mapArea"]);
                if (m.ContainsKey("nameArea")) Map.NameArea = Area.FromUi(m["nameArea"]);
            }
            if (d.ContainsKey("extractedWords")) ExtractedWords = Words(Json.List(d, "extractedWords"));
            if (d.ContainsKey("diedWords")) DiedWords = Words(Json.List(d, "diedWords"));
        }

        static List<FieldDef> FieldsFromUi(List<object> items)
        {
            var fields = new List<FieldDef>();
            var seen = new HashSet<string>();
            foreach (object item in items)
            {
                var fd = Json.Obj(item);
                if (fd == null) continue;
                string id = Json.Str(fd, "id") ?? "";
                if (!SafeId.IsMatch(id) || !seen.Add(id)) continue;
                string type = Json.Str(fd, "type");
                if (type != "number" && type != "stand" && type != "sterne" && type != "status" && type != "text") type = "number";
                string label = (Json.Str(fd, "label") ?? id).Trim();
                if (label.Length == 0) label = id;
                if (label.Length > 40) label = label.Substring(0, 40);
                fields.Add(new FieldDef
                {
                    Id = id,
                    Label = label,
                    Type = type,
                    SurvivorOnly = type != "status" && Json.Bool(fd, "survivorOnly"),
                    Derived = Json.Bool(fd, "derived"),
                    Area = fd.ContainsKey("area") ? Area.FromUi(fd["area"]) : null,
                });
            }
            return fields;
        }

        static List<string> Words(List<object> items)
        {
            return items.OfType<string>().Select(s => s.Trim()).Where(s => s.Length > 0 && s.Length <= 30).Distinct().Take(20).ToList();
        }

        public static Calibration Load()
        {
            Calibration c = null;
            if (File.Exists(AppInfo.CalibrationFile))
            {
                try
                {
                    c = Json.Deserialize<Calibration>(File.ReadAllText(AppInfo.CalibrationFile, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    Log.Write("kalibrierung.json konnte nicht gelesen werden: " + ex.Message);
                }
            }
            if (c == null) c = CreateDefault();
            if (c.Fields == null) c.Fields = DefaultFields();
            if (c.Team == null) c.Team = TeamPage.CreateDefault();
            if (c.Team.Fields == null) c.Team.Fields = TeamPage.DefaultFields();
            if (c.Map == null) c.Map = MapPage.CreateDefault();
            bool renamed = false;
            foreach (var field in c.Fields)
            {
                string[] rename;
                if (field.Id != null && LabelRenames.TryGetValue(field.Id, out rename) && field.Label == rename[0])
                {
                    field.Label = rename[1];
                    renamed = true;
                }
            }
            // The Mission Summary shows these only for a hunter who got out, so they tell
            // extracted from died. Set once for calibrations made before this existed.
            bool levelled = false;
            if (c.Version < 2)
            {
                foreach (var field in c.Fields.Where(f => f.Type == "number" && LevelLabels.Contains((f.Label ?? "").Trim())))
                {
                    field.Type = "stand";
                    levelled = true;
                }
                c.Version = 2;
            }
            bool marked = false;
            if (c.Fields.Count > 0 && !c.Fields.Any(f => f.SurvivorOnly))
            {
                foreach (var field in c.Fields.Where(f => f.Type != "status" && SurvivorLabels.Contains((f.Label ?? "").Trim())))
                {
                    field.SurvivorOnly = true;
                    marked = true;
                }
            }
            if ((renamed || marked || levelled) && File.Exists(AppInfo.CalibrationFile))
            {
                try
                {
                    c.Save();
                    if (renamed) Log.Write("Feldnamen auf die offiziellen Hunt-Begriffe umgestellt");
                    if (marked) Log.Write("Ausgang wird jetzt aus „" + string.Join("“, „", c.SurvivorFields.Select(f => f.Label)) + "“ abgeleitet");
                    if (levelled) Log.Write("„" + string.Join("“, „", c.Fields.Where(f => f.Type == "stand").Select(f => f.Label)) + "“ zählt jetzt als Stand statt als Summe");
                }
                catch (Exception ex)
                {
                    Log.Write("Kalibrierung konnte nicht gespeichert werden: " + ex.Message);
                }
            }
            if (c.ExtractedWords == null || c.DiedWords == null)
            {
                var d = CreateDefault();
                if (c.ExtractedWords == null) c.ExtractedWords = d.ExtractedWords;
                if (c.DiedWords == null) c.DiedWords = d.DiedWords;
            }
            if (c.Threshold == 0) c.Threshold = 80;
            return c;
        }

        public void Save()
        {
            Files.WriteAllTextAtomic(AppInfo.CalibrationFile, Json.Serialize(this));
        }

        // Names HuntXtract gave the Team Details fields, in the current language.
        public bool LocalizeLabels()
        {
            return Team != null && Team.LocalizeLabels();
        }

        public Calibration Clone()
        {
            return Json.Deserialize<Calibration>(Json.Serialize(this));
        }
    }
}
