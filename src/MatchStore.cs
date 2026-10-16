using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace Kopfgeld
{
    sealed class Match
    {
        public int Id { get; set; }
        public string Time { get; set; } // ISO 8601 with offset
        public bool Manual { get; set; }
        public Dictionary<string, object> Values { get; set; }
        public Dictionary<string, string> Raw { get; set; }
        public string Screenshot { get; set; }
        // Picture of the Team Details page, when they were read for this match.
        public string TeamScreenshot { get; set; }
        // The trail from the map (Tab): the map it was on, a picture of it and the positions.
        public string MapName { get; set; }
        public string MapShot { get; set; }
        public List<TrailPoint> Trail { get; set; }
        public bool Edited { get; set; }
        public string Note { get; set; }
        // "extrahiert", "gestorben" or empty – set by hand when the end screen does not show it.
        public string Outcome { get; set; }

        [ScriptIgnore]
        public DateTime LocalTime
        {
            get
            {
                DateTimeOffset t;
                return DateTimeOffset.TryParse(Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out t) ? t.LocalDateTime : DateTime.MinValue;
            }
        }

        public long? Number(string fieldId)
        {
            object v;
            if (Values == null || !Values.TryGetValue(fieldId, out v) || v == null || v is string) return null;
            try
            {
                return Convert.ToInt64(v, CultureInfo.InvariantCulture);
            }
            catch
            {
                return null;
            }
        }

        public string Text(string fieldId)
        {
            object v;
            if (Values == null || !Values.TryGetValue(fieldId, out v) || v == null) return "";
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        // Extracted/died: the read status field if there is one, otherwise the manual outcome.
        public string EffectiveOutcome(FieldDef statusField)
        {
            string read = statusField != null ? Text(statusField.Id) : "";
            return read.Length > 0 ? read : Outcome ?? "";
        }
    }

    sealed class PendingMatch
    {
        public Dictionary<string, object> Values;
        public Dictionary<string, string> Raw;
        public bool Manual;
        public byte[] ScreenshotJpeg;
        public string Note;
        // The trail collected from the map during the match, if any.
        public Trail Trail;
        // The map of the match from Hunt's log – for a match without a trail, or one whose map name
        // could not be read.
        public string MapName;
    }

    // What the Team Details screen gave us; it belongs to the match saved just before.
    sealed class TeamReading
    {
        public Dictionary<string, object> Values;
        public Dictionary<string, string> Raw;
        // Asked for by hand ("Jetzt auslesen" or hotkey): then the match may be older.
        public bool Manual;
        public byte[] ScreenshotJpeg;
    }

    // Values of a match the user deleted, so a Mission Summary that is still open doesn't bring it back.
    sealed class DeletedMatch
    {
        public string Time { get; set; }
        public Dictionary<string, object> Values { get; set; }

        [ScriptIgnore]
        public DateTime LocalTime
        {
            get
            {
                DateTimeOffset t;
                return DateTimeOffset.TryParse(Time, CultureInfo.InvariantCulture, DateTimeStyles.None, out t) ? t.LocalDateTime : DateTime.MinValue;
            }
        }
    }

    enum AddResult { Saved, Duplicate, Deleted, Completed }

    // All matches in one JSON file next to the program, with a daily safety copy.
    sealed class MatchStore
    {
        readonly object sync = new object();
        List<Match> matches = new List<Match>();
        List<DeletedMatch> deleted = new List<DeletedMatch>();

        public void Load()
        {
            lock (sync)
            {
                LoadDeleted();
                if (!File.Exists(AppInfo.MatchesFile)) return;
                try
                {
                    matches = Json.Deserialize<List<Match>>(File.ReadAllText(AppInfo.MatchesFile, Encoding.UTF8)) ?? new List<Match>();
                    matches = matches.OrderBy(m => m.LocalTime).ToList();
                }
                catch (Exception ex)
                {
                    string broken = AppInfo.MatchesFile + ".defekt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                    Log.Write("matches.json konnte nicht gelesen werden (" + ex.Message + ") – gesichert als " + Path.GetFileName(broken));
                    try
                    {
                        File.Copy(AppInfo.MatchesFile, broken, true);
                    }
                    catch
                    {
                    }
                    matches = new List<Match>();
                }
            }
        }

        void SaveLocked()
        {
            BackupOncePerDay();
            Files.WriteAllTextAtomic(AppInfo.MatchesFile, Json.Serialize(matches));
        }

        void LoadDeleted()
        {
            try
            {
                if (File.Exists(AppInfo.DeletedFile))
                    deleted = Json.Deserialize<List<DeletedMatch>>(File.ReadAllText(AppInfo.DeletedFile, Encoding.UTF8)) ?? new List<DeletedMatch>();
            }
            catch (Exception ex)
            {
                Log.Write("geloescht.json konnte nicht gelesen werden: " + ex.Message);
                deleted = new List<DeletedMatch>();
            }
        }

        // Kept for a day – long enough for any Mission Summary that is still open.
        void SaveDeletedLocked()
        {
            var recent = deleted.Where(d => (DateTime.Now - d.LocalTime).TotalHours < 24).ToList();
            deleted = recent.Skip(Math.Max(0, recent.Count - 50)).ToList();
            try
            {
                Files.WriteAllTextAtomic(AppInfo.DeletedFile, Json.Serialize(deleted));
            }
            catch (Exception ex)
            {
                Log.Write("geloescht.json konnte nicht gespeichert werden: " + ex.Message);
            }
        }

        static void BackupOncePerDay()
        {
            try
            {
                if (!File.Exists(AppInfo.MatchesFile)) return;
                string dir = Path.Combine(AppInfo.DataDir, "sicherungen");
                Directory.CreateDirectory(dir);
                string today = Path.Combine(dir, "matches-" + DateTime.Now.ToString("yyyy-MM-dd") + ".json");
                if (File.Exists(today)) return;
                File.Copy(AppInfo.MatchesFile, today);
                foreach (var old in new DirectoryInfo(dir).GetFiles("matches-*.json").OrderByDescending(f => f.Name).Skip(14))
                    old.Delete();
            }
            catch (Exception ex)
            {
                Log.Write("Sicherung fehlgeschlagen: " + ex.Message);
            }
        }

        public List<Match> Snapshot()
        {
            lock (sync) return matches.ToList();
        }

        // Saves a new match unless it is the last one read again, or one the user deleted while the
        // Mission Summary was still open. Returns the existing match for a duplicate, and for the
        // last one read again with values that were still missing – those are filled in.
        public Match AddIfNew(PendingMatch pending, Calibration calibration, out AddResult result)
        {
            lock (sync)
            {
                var last = matches.LastOrDefault();
                if (last != null && SameSummary(last.Values, last.LocalTime, pending, calibration))
                {
                    result = AddResult.Duplicate;
                    return last;
                }
                int missing;
                if (last != null && SameSummaryPartly(last, pending, calibration, out missing))
                {
                    result = missing > 0 ? AddResult.Completed : AddResult.Duplicate;
                    if (missing > 0) CompleteLocked(last, pending, calibration);
                    return last;
                }
                // Reading by hand is a deliberate request, so it may bring a deleted match back.
                if (!pending.Manual && deleted.Any(d => (DateTime.Now - d.LocalTime).TotalHours < 24 && SameSummary(d.Values, d.LocalTime, pending, calibration)))
                {
                    result = AddResult.Deleted;
                    return null;
                }
                result = AddResult.Saved;

                var now = DateTimeOffset.Now;
                var match = new Match
                {
                    Id = matches.Count == 0 ? 1 : matches.Max(m => m.Id) + 1,
                    Time = now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture),
                    Manual = pending.Manual,
                    Values = pending.Values,
                    Raw = pending.Raw,
                    Note = pending.Note ?? "",
                    // Extracted or died, if the user marked fields that only a surviving hunter has.
                    Outcome = calibration.DeriveOutcome(pending.Values),
                };
                if (pending.ScreenshotJpeg != null)
                {
                    try
                    {
                        match.Screenshot = MatchFolders.Write(match, MatchFolders.Summary, pending.ScreenshotJpeg);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Screenshot konnte nicht gespeichert werden: " + ex.Message);
                    }
                }
                var trail = pending.Trail;
                if (trail != null && trail.Points != null && trail.Points.Count > 0)
                {
                    match.Trail = trail.Points;
                    match.MapName = trail.MapName;
                    if (trail.MapPicture != null)
                    {
                        try
                        {
                            match.MapShot = MatchFolders.Write(match, MatchFolders.Map, trail.MapPicture);
                        }
                        catch (Exception ex)
                        {
                            Log.Write("Kartenbild konnte nicht gespeichert werden: " + ex.Message);
                        }
                    }
                    // The map at each point, next to the match's other pictures.
                    for (int i = 0; i < trail.Points.Count; i++)
                    {
                        var bytes = trail.PointPictures != null && i < trail.PointPictures.Count ? trail.PointPictures[i] : null;
                        trail.Points[i].Shot = null;
                        if (bytes == null) continue;
                        try
                        {
                            trail.Points[i].Shot = MatchFolders.Write(match, MatchFolders.PointFile(i + 1), bytes);
                        }
                        catch (Exception ex)
                        {
                            Log.Write("Kartenbild zum Punkt konnte nicht gespeichert werden: " + ex.Message);
                            break;
                        }
                    }
                }
                if (string.IsNullOrEmpty(match.MapName)) match.MapName = pending.MapName;
                matches.Add(match);
                SaveLocked();
                MatchFolders.WriteStats(match, calibration);
                return match;
            }
        }

        // The same Mission Summary read again, e.g. after switching menu tabs or restarting HuntXtract
        // while it is still open. Three or more non-zero numbers are practically unique to one match,
        // so those count however old; sparse values (a quick death) only within 20 minutes, since two
        // real matches can look alike.
        static bool SameSummary(Dictionary<string, object> earlier, DateTime earlierTime, PendingMatch pending, Calibration calibration)
        {
            if (!SameValues(earlier, pending.Values, calibration)) return false;
            return (DateTime.Now - earlierTime).TotalMinutes < 20 || NonZeroNumbers(pending.Values, calibration) >= 3;
        }

        // The same Mission Summary read before it had finished (the game in the background, say) or
        // read once more with a value it could not make out: no value differs, at least three non-zero
        // numbers are the same – so it can't be another match – and the summary can still be open.
        // missing: values the match lacks and this reading has.
        static bool SameSummaryPartly(Match earlier, PendingMatch pending, Calibration calibration, out int missing)
        {
            missing = 0;
            if (pending.Values == null || (DateTime.Now - earlier.LocalTime).TotalMinutes >= 20) return false;
            int sameNonZero = 0;
            foreach (var field in calibration.Fields.Where(f => f.HasArea))
            {
                object v;
                string a = earlier.Text(field.Id);
                string b = pending.Values.TryGetValue(field.Id, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : "";
                if (a.Length == 0 || b.Length == 0)
                {
                    if (a.Length == 0 && b.Length > 0) missing++;
                    continue;
                }
                if (a != b) return false;
                long n;
                if (field.Type == "number" && long.TryParse(a, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n != 0) sameNonZero++;
            }
            return sameNonZero >= 3;
        }

        // Fills in what the match was missing; the complete summary also becomes its picture.
        void CompleteLocked(Match match, PendingMatch pending, Calibration calibration)
        {
            if (match.Values == null) match.Values = new Dictionary<string, object>();
            // Worked out from the values before – unless it was set by hand.
            bool derived = (match.Outcome ?? "") == calibration.DeriveOutcome(match.Values);
            foreach (var field in calibration.Fields.Where(f => f.HasArea))
            {
                object v;
                if (match.Text(field.Id).Length > 0 || !pending.Values.TryGetValue(field.Id, out v) || v == null
                    || Convert.ToString(v, CultureInfo.InvariantCulture).Length == 0) continue;
                match.Values[field.Id] = v;
                string raw;
                if (pending.Raw != null && pending.Raw.TryGetValue(field.Id, out raw))
                {
                    if (match.Raw == null) match.Raw = new Dictionary<string, string>();
                    match.Raw[field.Id] = raw;
                }
            }
            if (derived) match.Outcome = calibration.DeriveOutcome(match.Values);
            if (pending.ScreenshotJpeg != null)
            {
                try
                {
                    string earlier = match.Screenshot;
                    match.Screenshot = MatchFolders.Write(match, MatchFolders.Summary, pending.ScreenshotJpeg);
                    if (earlier != null && earlier != match.Screenshot) MatchFolders.Delete(earlier);
                }
                catch (Exception ex)
                {
                    Log.Write("Screenshot konnte nicht gespeichert werden: " + ex.Message);
                }
            }
            if (string.IsNullOrEmpty(match.MapName) && pending.MapName != null) match.MapName = pending.MapName;
            SaveLocked();
            MatchFolders.WriteStats(match, calibration);
        }

        static bool SameValues(Dictionary<string, object> earlier, Dictionary<string, object> values, Calibration calibration)
        {
            bool anyValue = false;
            foreach (var field in calibration.Fields.Where(f => f.HasArea))
            {
                object a = null, b = null;
                if (earlier != null) earlier.TryGetValue(field.Id, out a);
                values.TryGetValue(field.Id, out b);
                string sa = a == null ? "" : Convert.ToString(a, CultureInfo.InvariantCulture);
                string sb = b == null ? "" : Convert.ToString(b, CultureInfo.InvariantCulture);
                if (sa != sb) return false;
                if (sb.Length > 0) anyValue = true;
            }
            return anyValue;
        }

        static int NonZeroNumbers(Dictionary<string, object> values, Calibration calibration)
        {
            int count = 0;
            foreach (var field in calibration.Fields.Where(f => f.HasArea && f.Type == "number"))
            {
                object v;
                long n;
                if (values.TryGetValue(field.Id, out v) && v != null &&
                    long.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out n) && n != 0)
                    count++;
            }
            return count;
        }

        // Adds the values from the Team Details screen to the match that was saved just before.
        // Returns null if there is no recent match or nothing new to add (reason says which).
        public Match AddTeamValues(TeamReading reading, Calibration calibration, int withinMinutes, out string reason)
        {
            lock (sync)
            {
                var last = matches.LastOrDefault();
                if (last == null)
                {
                    reason = Lang.T("es gibt noch kein Match, zu dem sie gehören könnten", "there is no match yet they could belong to");
                    return null;
                }
                if ((DateTime.Now - last.LocalTime).TotalMinutes > withinMinutes)
                {
                    reason = withinMinutes >= 60
                        ? Lang.F("das letzte Match ist älter als {0} Stunden", "the last match is older than {0} hours", withinMinutes / 60)
                        : Lang.F("das letzte Match ist älter als {0} Minuten", "the last match is older than {0} minutes", withinMinutes);
                    return null;
                }
                if (last.Values == null) last.Values = new Dictionary<string, object>();
                if (last.Raw == null) last.Raw = new Dictionary<string, string>();
                bool changed = false;
                foreach (var field in calibration.Team.Fields)
                {
                    object read;
                    if (!reading.Values.TryGetValue(field.Id, out read)) continue;
                    object cleaned = CleanValue(read, field.Type);
                    object previous;
                    last.Values.TryGetValue(field.Id, out previous);
                    if (Convert.ToString(previous, CultureInfo.InvariantCulture) != Convert.ToString(cleaned, CultureInfo.InvariantCulture))
                    {
                        last.Values[field.Id] = cleaned;
                        changed = true;
                    }
                    string rawText;
                    if (reading.Raw != null && reading.Raw.TryGetValue(field.Id, out rawText)) last.Raw[field.Id] = rawText;
                }
                // The picture of the page goes with the values; a match read before this existed gets one too.
                if (reading.ScreenshotJpeg != null && (changed || string.IsNullOrEmpty(last.TeamScreenshot)))
                {
                    try
                    {
                        string earlier = last.TeamScreenshot;
                        last.TeamScreenshot = MatchFolders.Write(last, MatchFolders.Team, reading.ScreenshotJpeg);
                        if (earlier != null && earlier != last.TeamScreenshot) MatchFolders.Delete(earlier);
                        changed = true;
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Screenshot der Team Details konnte nicht gespeichert werden: " + ex.Message);
                    }
                }
                if (!changed)
                {
                    reason = Lang.T("dieselben Werte wie schon gespeichert", "the same values as already saved");
                    return null;
                }
                SaveLocked();
                MatchFolders.WriteStats(last, calibration);
                reason = null;
                return last;
            }
        }

        // Your own MMR for saved matches that don't have it yet, worked out from their Team Details
        // names with the player name as it is now – e.g. after entering it for the first time.
        // Values that are there already stay. Returns how many matches got one.
        public int FillOwnMmr(TeamPage team, string playerName)
        {
            var target = (team.Fields ?? new List<FieldDef>()).FirstOrDefault(f => f.Derived);
            if (target == null || string.IsNullOrWhiteSpace(playerName)) return 0;
            lock (sync)
            {
                int count = 0;
                foreach (var match in matches)
                {
                    if (match.Values == null || match.Number(target.Id).HasValue) continue;
                    var values = new Dictionary<string, object>(match.Values);
                    if (team.DeriveOwnMmr(values, playerName) == null) continue;
                    object stars;
                    if (!values.TryGetValue(target.Id, out stars) || stars == null) continue;
                    match.Values = values;
                    count++;
                }
                if (count > 0) SaveLocked();
                return count;
            }
        }

        public Match Update(int id, Dictionary<string, object> values, string note, string outcome, Calibration calibration)
        {
            lock (sync)
            {
                var match = matches.FirstOrDefault(m => m.Id == id);
                if (match == null) return null;
                if (outcome != null) match.Outcome = outcome == "extrahiert" || outcome == "gestorben" ? outcome : "";
                if (values != null)
                {
                    if (match.Values == null) match.Values = new Dictionary<string, object>();
                    foreach (var entry in values)
                    {
                        var field = calibration.AllFields.FirstOrDefault(f => f.Id == entry.Key);
                        string type = field != null ? field.Type : "text";
                        object cleaned = CleanValue(entry.Value, type);
                        object previous;
                        match.Values.TryGetValue(entry.Key, out previous);
                        if (Convert.ToString(previous, CultureInfo.InvariantCulture) != Convert.ToString(cleaned, CultureInfo.InvariantCulture))
                        {
                            match.Values[entry.Key] = cleaned;
                            match.Edited = true;
                        }
                    }
                }
                if (note != null) match.Note = note.Length > 500 ? note.Substring(0, 500) : note;
                SaveLocked();
                MatchFolders.WriteStats(match, calibration);
                return match;
            }
        }

        static object CleanValue(object value, string type)
        {
            if (value == null) return null;
            string s = Convert.ToString(value, CultureInfo.InvariantCulture).Trim();
            if (s.Length == 0) return null;
            if (type == "number" || type == "stand" || type == "sterne")
            {
                long n;
                string digits = new string(s.Where(char.IsDigit).ToArray());
                return digits.Length > 0 && digits.Length <= 12 && long.TryParse(digits, out n) ? (object)n : null;
            }
            if (type == "status")
            {
                string lower = s.ToLowerInvariant();
                return lower == "extrahiert" || lower == "gestorben" ? lower : "";
            }
            return s.Length > 100 ? s.Substring(0, 100) : s;
        }

        public bool Delete(int id)
        {
            lock (sync)
            {
                var match = matches.FirstOrDefault(m => m.Id == id);
                if (match == null) return false;
                matches.Remove(match);
                SaveLocked();
                if (match.Values != null)
                {
                    deleted.Add(new DeletedMatch { Time = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture), Values = match.Values });
                    SaveDeletedLocked();
                }
                DeletePictures(match);
                return true;
            }
        }

        // From 1.6.0 on each match has its own folder (MatchFolders). Pictures from before move there
        // once – matches.json is copied to sicherungen first – and every match gets its stats.txt.
        // Returns how many pictures moved.
        public int MigrateToFolders(Calibration calibration)
        {
            lock (sync)
            {
                if (matches.Any(m => Pictures(m).Any(p => p.IndexOf('/') < 0)))
                {
                    try
                    {
                        string dir = Path.Combine(AppInfo.DataDir, "sicherungen");
                        Directory.CreateDirectory(dir);
                        File.Copy(AppInfo.MatchesFile, Path.Combine(dir, "matches-vor-ordnern.json"), true);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Sicherung vor dem Umzug der Bilder fehlgeschlagen – sie bleiben, wo sie sind: " + ex.Message);
                        return 0;
                    }
                }
                int moved = 0;
                foreach (var m in matches)
                {
                    m.Screenshot = MoveInto(m, m.Screenshot, MatchFolders.Summary, ref moved);
                    m.TeamScreenshot = MoveInto(m, m.TeamScreenshot, MatchFolders.Team, ref moved);
                    m.MapShot = MoveInto(m, m.MapShot, MatchFolders.Map, ref moved);
                    if (m.Trail != null)
                        for (int i = 0; i < m.Trail.Count; i++) m.Trail[i].Shot = MoveInto(m, m.Trail[i].Shot, MatchFolders.PointFile(i + 1), ref moved);
                    if (!File.Exists(MatchFolders.FullPath(MatchFolders.Folder(m) + "/" + MatchFolders.Stats))) MatchFolders.WriteStats(m, calibration);
                }
                if (moved == 0) return 0;
                SaveLocked();
                Log.Write(moved + " Bilder in Ordner pro Tag und Match umgezogen (Daten\\Matches)");
                try
                {
                    if (Directory.Exists(AppInfo.ScreenshotDir) && !Directory.EnumerateFileSystemEntries(AppInfo.ScreenshotDir).Any()) Directory.Delete(AppInfo.ScreenshotDir);
                }
                catch (Exception)
                {
                }
                return moved;
            }
        }

        // A picture from before 1.6.0 (a name without folder) moves into the match's folder.
        static string MoveInto(Match m, string stored, string file, ref int moved)
        {
            if (string.IsNullOrEmpty(stored) || stored.IndexOf('/') >= 0) return stored;
            string from = MatchFolders.FullPath(stored);
            if (from == null || !File.Exists(from)) return stored;
            string target = MatchFolders.Folder(m) + "/" + file;
            string to = MatchFolders.FullPath(target);
            try
            {
                if (File.Exists(to)) return stored;
                Directory.CreateDirectory(Path.GetDirectoryName(to));
                File.Move(from, to);
                moved++;
                return target;
            }
            catch (Exception ex)
            {
                Log.Write("Bild konnte nicht umziehen (" + stored + "): " + ex.Message);
                return stored;
            }
        }

        // Tidying up (Settings, "Aufbewahren"): of the matches beyond what is kept, only the pictures
        // go ("pictures" – values and statistics stay) or the whole match ("matches"). With
        // apply = false it only counts. bytes: the space it frees.
        public int Cleanup(string keep, string what, bool apply, out long bytes)
        {
            bytes = 0;
            lock (sync)
            {
                int count = 0;
                foreach (var m in BeyondKeep(keep))
                {
                    var pictures = Pictures(m);
                    if (what != "matches" && pictures.Count == 0) continue;
                    count++;
                    bytes += pictures.Sum(p => MatchFolders.Size(p));
                    if (!apply) continue;
                    if (what == "matches")
                    {
                        DeletePictures(m);
                        matches.Remove(m);
                    }
                    else
                    {
                        foreach (string p in pictures) MatchFolders.Delete(p);
                        m.Screenshot = m.TeamScreenshot = m.MapShot = null;
                        if (m.Trail != null) foreach (var point in m.Trail) point.Shot = null;
                    }
                }
                if (apply && count > 0) SaveLocked();
                return count;
            }
        }

        // The matches beyond "keep": older than so many days ("30d"), or all but the newest so many ("500").
        List<Match> BeyondKeep(string keep)
        {
            int n;
            if (string.IsNullOrEmpty(keep) || keep == "all") return new List<Match>();
            if (keep.EndsWith("d", StringComparison.Ordinal) && int.TryParse(keep.Substring(0, keep.Length - 1), out n))
            {
                var limit = DateTime.Now.AddDays(-n);
                return matches.Where(m => m.LocalTime < limit).ToList();
            }
            return int.TryParse(keep, out n) ? matches.OrderByDescending(m => m.LocalTime).Skip(n).ToList() : new List<Match>();
        }

        // All picture files of a match: Mission Summary, Team Details, the map and the map at each point.
        static List<string> Pictures(Match match)
        {
            var files = new List<string> { match.Screenshot, match.TeamScreenshot, match.MapShot };
            if (match.Trail != null) files.AddRange(match.Trail.Select(p => p.Shot));
            return files.Where(f => !string.IsNullOrEmpty(f)).ToList();
        }

        // Everything of a match on disk: its pictures (also from before 1.6.0) and its folder.
        static void DeletePictures(Match match)
        {
            foreach (string shot in Pictures(match)) MatchFolders.Delete(shot);
            MatchFolders.DeleteFolder(match);
        }

        public void ExportCsv(string path, Calibration calibration)
        {
            var fields = calibration.AllFields.Where(f => f.Type != "status").ToList();
            var status = calibration.StatusField;
            var sb = new StringBuilder();
            string yes = Lang.T("ja", "yes"), no = Lang.T("nein", "no");
            sb.Append(Lang.T("Nr;Datum;Uhrzeit;Karte;Ausgang", "No;Date;Time;Map;Outcome"));
            foreach (var f in fields) sb.Append(';').Append(Csv(f.Label));
            sb.AppendLine(Lang.T(";Manuell;Bearbeitet;Notiz", ";Manual;Edited;Note"));
            foreach (var m in Snapshot())
            {
                var t = m.LocalTime;
                sb.Append(m.Id).Append(';').Append(t.ToString(Lang.T("dd.MM.yyyy", "yyyy-MM-dd"), CultureInfo.InvariantCulture))
                    .Append(';').Append(t.ToString("HH:mm", CultureInfo.InvariantCulture))
                    .Append(';').Append(Csv(m.MapName ?? ""));
                string outcome = m.EffectiveOutcome(status);
                sb.Append(';').Append(outcome == "extrahiert" ? "Extracted" : outcome == "gestorben" ? "Died" : "");
                foreach (var f in fields) sb.Append(';').Append(Csv(m.Text(f.Id)));
                sb.Append(';').Append(m.Manual ? yes : no).Append(';').Append(m.Edited ? yes : no).Append(';').Append(Csv(m.Note ?? ""));
                sb.AppendLine();
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        static string Csv(string value)
        {
            if (value.IndexOfAny(new[] { ';', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        public static Dictionary<string, object> ToUi(Match m)
        {
            var d = new Dictionary<string, object>();
            d["id"] = m.Id;
            d["time"] = m.Time;
            d["manual"] = m.Manual;
            d["values"] = m.Values ?? new Dictionary<string, object>();
            d["raw"] = m.Raw ?? new Dictionary<string, string>();
            d["screenshot"] = string.IsNullOrEmpty(m.Screenshot) ? null : AppInfo.UiOrigin + "/shots/" + Uri.EscapeDataString(m.Screenshot);
            d["teamScreenshot"] = string.IsNullOrEmpty(m.TeamScreenshot) ? null : AppInfo.UiOrigin + "/shots/" + Uri.EscapeDataString(m.TeamScreenshot);
            d["mapShot"] = string.IsNullOrEmpty(m.MapShot) ? null : AppInfo.UiOrigin + "/shots/" + Uri.EscapeDataString(m.MapShot);
            d["mapName"] = m.MapName ?? "";
            // [x, y, seconds, picture of that moment or null] – compact, a trail can have a few hundred points.
            d["trail"] = (m.Trail ?? new List<TrailPoint>()).Select(p => (object)new object[] { p.X, p.Y, p.S,
                string.IsNullOrEmpty(p.Shot) ? null : AppInfo.UiOrigin + "/shots/" + Uri.EscapeDataString(p.Shot) }).ToList();
            d["edited"] = m.Edited;
            d["note"] = m.Note ?? "";
            d["outcome"] = m.Outcome ?? "";
            return d;
        }
    }

    static class Stats
    {
        public static Dictionary<string, object> Compute(List<Match> matches, Calibration calibration, int dayStartHour)
        {
            var fields = calibration.AllFields;
            var numeric = fields.Where(f => f.Type == "number").ToList();
            // "Stand" and star fields (hunter level, MMR) are not added up – the newest value counts.
            var levels = fields.Where(f => f.Type == "stand" || f.Type == "sterne").ToList();
            // The MMR of the single player rows: one average instead of a card per row.
            var rowMmr = calibration.Team != null ? levels.Where(calibration.Team.IsRowMmr).ToList() : new List<FieldDef>();
            var status = calibration.StatusField;

            DateTime now = DateTime.Now;
            DateTime dayStart = now.Date.AddHours(dayStartHour);
            if (now < dayStart) dayStart = dayStart.AddDays(-1);

            var ordered = matches.OrderBy(m => m.LocalTime).ToList();
            var today = ordered.Where(m => m.LocalTime >= dayStart).ToList();
            var last10 = ordered.Skip(Math.Max(0, ordered.Count - 10)).ToList();

            var d = new Dictionary<string, object>();
            d["fields"] = fields.Select(f =>
            {
                var fd = new Dictionary<string, object>();
                fd["id"] = f.Id;
                fd["label"] = f.Label;
                fd["type"] = f.Type;
                fd["survivorOnly"] = f.SurvivorOnly;
                fd["rowMmr"] = rowMmr.Contains(f);
                return fd;
            }).ToList();
            d["hasOutcome"] = status != null || ordered.Any(m => m.EffectiveOutcome(status).Length > 0);
            d["dayStart"] = dayStart.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
            d["today"] = Aggregate(today, numeric, levels, rowMmr, status);
            d["last10"] = Aggregate(last10, numeric, levels, rowMmr, status);
            d["all"] = Aggregate(ordered, numeric, levels, rowMmr, status);
            var last = ordered.LastOrDefault();
            d["last"] = last == null ? null : MatchStore.ToUi(last);
            d["updated"] = DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture);
            return d;
        }

        static Dictionary<string, object> Aggregate(List<Match> list, List<FieldDef> numeric, List<FieldDef> levels, List<FieldDef> rowMmr, FieldDef status)
        {
            var d = new Dictionary<string, object>();
            int extracted = list.Count(m => m.EffectiveOutcome(status) == "extrahiert");
            int died = list.Count(m => m.EffectiveOutcome(status) == "gestorben");
            d["matches"] = list.Count;
            d["extracted"] = extracted;
            d["died"] = died;
            d["extractionRate"] = extracted + died > 0 ? (object)Math.Round(extracted / (double)(extracted + died), 3) : null;
            var sums = new Dictionary<string, object>();
            var avgs = new Dictionary<string, object>();
            var best = new Dictionary<string, object>();
            foreach (var f in numeric)
            {
                var values = list.Select(m => m.Number(f.Id)).Where(v => v.HasValue).Select(v => v.Value).ToList();
                sums[f.Id] = values.Sum();
                avgs[f.Id] = values.Count > 0 ? (object)Math.Round(values.Average(), 2) : null;
                best[f.Id] = values.Count > 0 ? (object)values.Max() : null;
            }
            d["sums"] = sums;
            d["avgs"] = avgs;
            d["best"] = best;
            // Newest reading of each "Stand" field in this period, e.g. the hunter level,
            // plus its average – useful for ratings that change from match to match.
            var lasts = new Dictionary<string, object>();
            var levelAvgs = new Dictionary<string, object>();
            foreach (var f in levels)
            {
                long? value = null;
                for (int i = list.Count - 1; i >= 0 && !value.HasValue; i--) value = list[i].Number(f.Id);
                lasts[f.Id] = value.HasValue ? (object)value.Value : null;
                var seen = list.Select(m => m.Number(f.Id)).Where(v => v.HasValue).Select(v => v.Value).ToList();
                levelAvgs[f.Id] = seen.Count > 0 ? (object)Math.Round(seen.Average(), 2) : null;
            }
            d["lasts"] = lasts;
            d["levelAvgs"] = levelAvgs;

            // All player rows together: the team's MMR in this period.
            var rows = list.SelectMany(m => rowMmr.Select(f => m.Number(f.Id))).Where(v => v.HasValue).Select(v => v.Value).ToList();
            d["rowMmrAvg"] = rows.Count > 0 ? (object)Math.Round(rows.Average(), 2) : null;
            d["rowMmrCount"] = rows.Count;
            return d;
        }
    }
}
