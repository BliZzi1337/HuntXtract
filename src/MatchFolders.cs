using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Kopfgeld
{
    // Where a match's pictures and values live: one folder per day and in it one per match –
    // Daten\Matches\2026-09-27\19-08 Match 34\ with summary.jpg, team.jpg, karte.jpg,
    // karte-001.jpg … and stats.txt. Paths in matches.json are relative to Daten\Matches; a name
    // without a folder is a picture from before 1.6.0 in Daten\screenshots.
    static class MatchFolders
    {
        public const string Summary = "summary.jpg", Team = "team.jpg", Map = "karte.jpg", Stats = "stats.txt";
        static readonly Regex Segment = new Regex("^[A-Za-z0-9 ._-]+$");

        // "2026-09-27/19-08 Match 34"
        public static string Folder(Match m)
        {
            var time = m.LocalTime;
            return time.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "/" + time.ToString("HH-mm", CultureInfo.InvariantCulture)
                + " Match " + m.Id.ToString(CultureInfo.InvariantCulture);
        }

        // The map at the n-th point of the trail (from 1).
        public static string PointFile(int n)
        {
            return "karte-" + n.ToString("D3", CultureInfo.InvariantCulture) + ".jpg";
        }

        // Writes a file into the match's folder; returns its path for matches.json.
        public static string Write(Match m, string file, byte[] bytes)
        {
            string stored = Folder(m) + "/" + file;
            string full = FullPath(stored);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllBytes(full, bytes);
            return stored;
        }

        // The file for a path from matches.json – null if it isn't a safe one.
        public static string FullPath(string stored)
        {
            if (string.IsNullOrEmpty(stored)) return null;
            string[] parts = stored.Split('/');
            if (parts.Any(p => p == "." || p == ".." || !Segment.IsMatch(p))) return null;
            return parts.Length == 1 ? Path.Combine(AppInfo.ScreenshotDir, parts[0]) : Path.Combine(AppInfo.MatchesDir, Path.Combine(parts));
        }

        public static long Size(string stored)
        {
            string full = FullPath(stored);
            try
            {
                return full != null && File.Exists(full) ? new FileInfo(full).Length : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        public static void Delete(string stored)
        {
            string full = FullPath(stored);
            try
            {
                if (full != null && File.Exists(full)) File.Delete(full);
            }
            catch (Exception ex)
            {
                Log.Write("Bild konnte nicht gelöscht werden: " + ex.Message);
            }
        }

        // The whole folder of a match – and the day's folder, if that is empty then.
        public static void DeleteFolder(Match m)
        {
            try
            {
                string dir = Path.GetDirectoryName(FullPath(Folder(m) + "/" + Stats));
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                string day = Path.GetDirectoryName(dir);
                if (Directory.Exists(day) && !Directory.EnumerateFileSystemEntries(day).Any()) Directory.Delete(day);
            }
            catch (Exception ex)
            {
                Log.Write("Ordner des Matches konnte nicht gelöscht werden: " + ex.Message);
            }
        }

        // The values in plain text next to the pictures, to look at without HuntXtract.
        public static void WriteStats(Match m, Calibration calibration)
        {
            try
            {
                var culture = Lang.Culture;
                var sb = new StringBuilder();
                sb.AppendLine("HuntXtract – Match #" + m.Id);
                sb.AppendLine(m.LocalTime.ToString("f", culture));
                if (!string.IsNullOrEmpty(m.MapName)) sb.AppendLine(Lang.T("Karte: ", "Map: ") + m.MapName);
                string outcome = m.EffectiveOutcome(calibration.StatusField);
                if (outcome.Length > 0) sb.AppendLine(Lang.T("Ausgang: ", "Outcome: ") + (outcome == "extrahiert" ? "Extracted" : "Died"));
                if (m.Trail != null && m.Trail.Count > 0) sb.AppendLine(Lang.F("Laufspur: {0} Punkte", "Trail: {0} points", m.Trail.Count));
                AppendFields(sb, "Mission Summary", calibration.Fields, m);
                if (calibration.Team != null) AppendFields(sb, "Team Details", calibration.Team.Fields, m);
                if (!string.IsNullOrEmpty(m.Note))
                {
                    sb.AppendLine();
                    sb.AppendLine(Lang.T("Notiz: ", "Note: ") + m.Note);
                }
                string full = FullPath(Folder(m) + "/" + Stats);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, sb.ToString(), new UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                Log.Write("stats.txt konnte nicht geschrieben werden: " + ex.Message);
            }
        }

        static void AppendFields(StringBuilder sb, string title, List<FieldDef> fields, Match m)
        {
            var lines = (fields ?? new List<FieldDef>())
                .Where(f => f.Type != "status" && m.Text(f.Id).Length > 0)
                .Select(f => "  " + f.Label + ": " + m.Text(f.Id) + (f.Type == "sterne" ? " ★" : ""))
                .ToList();
            if (lines.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine(title);
            foreach (string line in lines) sb.AppendLine(line);
        }
    }
}
