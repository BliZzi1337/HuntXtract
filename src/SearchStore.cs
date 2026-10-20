using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Kopfgeld
{
    // One matchmaking search that led to a match, from Hunt's log.
    sealed class SearchRecord
    {
        // UTC, on the log's own clock (whole seconds): the click on "Ready".
        public DateTime Start { get; set; }
        // Until the lobby that started was found (lobbies that broke up before included).
        public int Seconds { get; set; }
        // That lobby's wait until the map loaded; null for searches saved by 2.1.0, which stopped
        // at the lobby.
        public int? Lobby { get; set; }
        // Lobbies that broke up before.
        public int Dissolved { get; set; }
    }

    // The search times, kept in Daten\suchen.json. Hunt's log is read again at every start of
    // HuntXtract, so the same search arrives more than once – it is kept only once.
    sealed class SearchStore
    {
        // The same search seen live and later read from the log differs by a second at most.
        const int SameWithinSeconds = 3;
        const int MaxKept = 20000;

        readonly object sync = new object();
        List<SearchRecord> searches = new List<SearchRecord>();

        public void Load()
        {
            lock (sync)
            {
                try
                {
                    if (!File.Exists(AppInfo.SearchesFile)) return;
                    var loaded = Json.Deserialize<List<SearchRecord>>(File.ReadAllText(AppInfo.SearchesFile, Encoding.UTF8));
                    if (loaded == null) return;
                    foreach (var s in loaded) s.Start = s.Start.ToUniversalTime();
                    searches = loaded.OrderBy(s => s.Start).ToList();
                }
                catch (Exception ex)
                {
                    Log.Write("Suchzeiten konnten nicht gelesen werden: " + ex.Message);
                }
            }
        }

        // Returns whether anything changed: a new search, or a search saved without its lobby wait
        // (2.1.0) that now gets it.
        public bool Add(DateTime start, int seconds, int lobby, int dissolved)
        {
            lock (sync)
            {
                start = start.ToUniversalTime();
                var known = searches.FirstOrDefault(s => Math.Abs((s.Start - start).TotalSeconds) <= SameWithinSeconds);
                if (known != null)
                {
                    if (known.Lobby != null) return false;
                    known.Seconds = seconds;
                    known.Lobby = lobby;
                    known.Dissolved = dissolved;
                }
                else
                {
                    searches.Add(new SearchRecord { Start = start, Seconds = seconds, Lobby = lobby, Dissolved = dissolved });
                    searches.Sort((a, b) => a.Start.CompareTo(b.Start));
                    if (searches.Count > MaxKept) searches.RemoveRange(0, searches.Count - MaxKept);
                }
                try
                {
                    Files.WriteAllTextAtomic(AppInfo.SearchesFile, Json.Serialize(searches));
                }
                catch (Exception ex)
                {
                    Log.Write("Suchzeiten konnten nicht gespeichert werden: " + ex.Message);
                }
                return true;
            }
        }

        public List<SearchRecord> Snapshot()
        {
            lock (sync) return searches.ToList();
        }

        // Today (from the start of the game day), the last 10 and all searches; the whole wait also by
        // time of day: night 0–6, morning 6–12, afternoon 12–18, evening 18–24 (local time).
        public static Dictionary<string, object> Compute(List<SearchRecord> list, DateTime dayStart)
        {
            var ordered = list.OrderBy(s => s.Start).ToList();
            var d = new Dictionary<string, object>();
            d["today"] = Aggregate(ordered.Where(s => s.Start.ToLocalTime() >= dayStart).ToList());
            d["last10"] = Aggregate(ordered.Skip(Math.Max(0, ordered.Count - 10)).ToList());
            d["all"] = Aggregate(ordered);
            d["byTime"] = new[] { 0, 6, 12, 18 }.Select(from =>
            {
                var part = Aggregate(ordered.Where(s => s.Start.ToLocalTime().Hour >= from && s.Start.ToLocalTime().Hour < from + 6).ToList());
                part["from"] = from;
                return (object)part;
            }).ToList();
            var last = ordered.LastOrDefault();
            d["last"] = last == null ? null : last.Start.ToString("o", CultureInfo.InvariantCulture);
            return d;
        }

        // Search, lobby wait and the whole wait (click to map) – the last two only from searches that
        // know their lobby wait.
        static Dictionary<string, object> Aggregate(List<SearchRecord> list)
        {
            var full = list.Where(s => s.Lobby != null).ToList();
            var d = new Dictionary<string, object>();
            d["count"] = list.Count;
            d["avgSeconds"] = list.Count > 0 ? (object)Math.Round(list.Average(s => s.Seconds), 1) : null;
            d["longest"] = list.Count > 0 ? (object)list.Max(s => s.Seconds) : null;
            d["full"] = full.Count;
            d["avgLobby"] = full.Count > 0 ? (object)Math.Round(full.Average(s => s.Lobby.Value), 1) : null;
            d["avgTotal"] = full.Count > 0 ? (object)Math.Round(full.Average(s => s.Seconds + s.Lobby.Value), 1) : null;
            d["longestTotal"] = full.Count > 0 ? (object)full.Max(s => s.Seconds + s.Lobby.Value) : null;
            d["dissolved"] = list.Sum(s => s.Dissolved);
            return d;
        }
    }
}
