using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace Kopfgeld
{
    // What Steam keeps on this PC: the Steam name of the account signed in last, and where Hunt is
    // installed. Only read, never written.
    static class SteamFiles
    {
        const string HuntAppId = "594650";

        public static string SteamFolder()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    string path = key != null ? key.GetValue("SteamPath") as string : null;
                    if (string.IsNullOrEmpty(path)) return null;
                    path = path.Replace('/', '\\');
                    return Directory.Exists(path) ? path : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        // The display name of the account that signed in to Steam last – the name Hunt shows.
        public static string PersonaName()
        {
            string steam = SteamFolder();
            if (steam == null) return null;
            string file = Path.Combine(steam, @"config\loginusers.vdf");
            try
            {
                if (!File.Exists(file)) return null;
                var users = Vdf.Parse(File.ReadAllText(file, Encoding.UTF8)).Child("users");
                if (users == null) return null;
                Vdf best = null;
                long bestTime = -1;
                foreach (var user in users.Children.Values)
                {
                    long time;
                    long.TryParse(user.Value("Timestamp") ?? "0", out time);
                    if (user.Value("MostRecent") == "1") time = long.MaxValue;
                    if (time <= bestTime) continue;
                    bestTime = time;
                    best = user;
                }
                string name = best != null ? best.Value("PersonaName") : null;
                return string.IsNullOrWhiteSpace(name) ? null : name.Trim();
            }
            catch (Exception ex)
            {
                Log.Write("Steam-Name konnte nicht gelesen werden: " + ex.Message);
                return null;
            }
        }

        // Hunt's folder in one of the Steam libraries, or null.
        public static string HuntFolder()
        {
            string steam = SteamFolder();
            if (steam == null) return null;
            var libraries = new List<string> { steam };
            try
            {
                string list = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                if (File.Exists(list))
                {
                    var folders = Vdf.Parse(File.ReadAllText(list, Encoding.UTF8)).Child("libraryfolders");
                    if (folders != null)
                        libraries.AddRange(folders.Children.Values.Select(l => l.Value("path")).Where(p => !string.IsNullOrEmpty(p)));
                }
            }
            catch (Exception)
            {
            }
            foreach (string library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    string manifest = Path.Combine(library, @"steamapps\appmanifest_" + HuntAppId + ".acf");
                    if (!File.Exists(manifest)) continue;
                    var app = Vdf.Parse(File.ReadAllText(manifest, Encoding.UTF8)).Child("AppState");
                    string dir = app != null ? app.Value("installdir") : null;
                    if (string.IsNullOrEmpty(dir)) continue;
                    string folder = Path.Combine(library, "steamapps", "common", dir);
                    if (Directory.Exists(folder)) return folder;
                }
                catch (Exception)
                {
                }
            }
            return null;
        }
    }

    // Valve's KeyValues text files: "key" "value" and "key" { … }.
    sealed class Vdf
    {
        public readonly Dictionary<string, Vdf> Children = new Dictionary<string, Vdf>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public Vdf Child(string key)
        {
            Vdf child;
            return Children.TryGetValue(key, out child) ? child : null;
        }

        public string Value(string key)
        {
            string value;
            return Values.TryGetValue(key, out value) ? value : null;
        }

        public static Vdf Parse(string text)
        {
            int i = 0;
            var root = new Vdf();
            Fill(root, text, ref i);
            return root;
        }

        static void Fill(Vdf node, string text, ref int i)
        {
            while (true)
            {
                string key = Token(text, ref i);
                if (key == null || key == "}") return;
                string next = Token(text, ref i);
                if (next == null) return;
                if (next == "{")
                {
                    var child = new Vdf();
                    Fill(child, text, ref i);
                    node.Children[key] = child;
                }
                else
                {
                    node.Values[key] = next;
                }
            }
        }

        // The next quoted string (with its escapes resolved), "{" or "}" – null at the end.
        static string Token(string text, ref int i)
        {
            while (i < text.Length)
            {
                char c = text[i];
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }
                if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
                {
                    while (i < text.Length && text[i] != '\n') i++;
                    continue;
                }
                if (c == '{' || c == '}')
                {
                    i++;
                    return c.ToString();
                }
                var sb = new StringBuilder();
                if (c == '"')
                {
                    for (i++; i < text.Length && text[i] != '"'; i++)
                    {
                        if (text[i] == '\\' && i + 1 < text.Length)
                        {
                            char e = text[++i];
                            sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
                        }
                        else
                        {
                            sb.Append(text[i]);
                        }
                    }
                    i++;
                    return sb.ToString();
                }
                for (; i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] != '{' && text[i] != '}' && text[i] != '"'; i++) sb.Append(text[i]);
                return sb.ToString();
            }
            return null;
        }
    }

    // Range: a level without a mission is loaded – the shooting range.
    enum HuntPhase { None, Menu, Loading, Match, Summary, Range }

    // Where Hunt is right now, from its own log.
    sealed class HuntStatus
    {
        public HuntPhase Phase;
        // Hunt's name of the level ("civilwar") and the map it is (null for other levels).
        public string Level;
        public string MapName;
        // UTC: since when in this phase, and when the last match began.
        public DateTime Since, MatchStarted;

        public HuntStatus Copy()
        {
            return (HuntStatus)MemberwiseClone();
        }

        public Dictionary<string, object> ToUi()
        {
            var d = new Dictionary<string, object>();
            d["phase"] = Phase.ToString();
            d["map"] = MapName;
            d["level"] = Level;
            d["since"] = Since == DateTime.MinValue ? null : Since.ToString("o");
            return d;
        }
    }

    // Follows Hunt's own log (USER\Game.log): the menu, a match on which map, the Mission Summary.
    // It only reads; a new Game.log (Hunt started again) is picked up by itself.
    sealed class HuntLog
    {
        // Hunt's level names of the maps.
        static readonly Dictionary<string, string> Maps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "cemetery", "Stillwater Bayou" },
            { "civilwar", "Lawson Delta" },
            { "creek", "DeSalle" },
            { "colorado", "Mammon's Gulch" },
        };

        static readonly Regex TimeRx = new Regex(@"^<(\d\d):(\d\d):(\d\d)>");
        static readonly Regex LevelRx = new Regex(@"PrepareLevel '([^']+)'");
        static readonly Regex BagRx = new Regex(@"CMetaMissionBag state: (\w+)");
        static readonly Regex LoadedRx = new Regex(@"\*LOADING: Level levels/(\w+) loading time");

        readonly object sync = new object();
        readonly object pollLock = new object();
        HuntStatus status = new HuntStatus();
        Timer timer;
        string path;
        DateTime nextSearch, nextProcessCheck;
        long position;
        Decoder decoder = Encoding.UTF8.GetDecoder();
        readonly StringBuilder partial = new StringBuilder();
        // Reading what was written before HuntXtract started: no events, the times come from the log.
        bool catchingUp;
        bool huntRunning;

        public event Action<HuntStatus> Changed;

        public HuntStatus Status
        {
            get { lock (sync) return status.Copy(); }
        }

        public void Start()
        {
            timer = new Timer(_ => Poll(), null, 500, 1000);
        }

        public void Stop()
        {
            if (timer != null) timer.Dispose();
        }

        void Poll()
        {
            if (!Monitor.TryEnter(pollLock)) return;
            try
            {
                Read();
            }
            catch (Exception ex)
            {
                Log.Write("Hunt-Protokoll konnte nicht gelesen werden: " + ex.Message);
                path = null;
            }
            finally
            {
                Monitor.Exit(pollLock);
            }
        }

        void Read()
        {
            var now = DateTime.UtcNow;
            if (now >= nextProcessCheck)
            {
                nextProcessCheck = now.AddSeconds(5);
                bool was = huntRunning;
                huntRunning = HuntRuns();
                if (was && !huntRunning) Set(HuntPhase.None, null, now);
            }
            if (path == null || !File.Exists(path))
            {
                if (now < nextSearch) return;
                nextSearch = now.AddSeconds(30);
                path = FindLog();
                if (path == null) return;
                Log.Write("Hunt-Protokoll: " + path);
                position = 0;
                catchingUp = true;
            }
            long length = new FileInfo(path).Length;
            if (length < position)
            {
                // Hunt started again: a new log.
                position = 0;
                decoder = Encoding.UTF8.GetDecoder();
                partial.Clear();
                catchingUp = false;
                huntRunning = true;
            }
            if (length == position) return;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                fs.Seek(position, SeekOrigin.Begin);
                var buffer = new byte[(int)Math.Min(length - position, 8 << 20)];
                int read = fs.Read(buffer, 0, buffer.Length);
                position += read;
                var chars = new char[decoder.GetCharCount(buffer, 0, read)];
                decoder.GetChars(buffer, 0, read, chars, 0);
                partial.Append(chars);
            }
            string text = partial.ToString();
            int end = text.LastIndexOf('\n');
            if (end < 0) return;
            partial.Clear();
            partial.Append(text, end + 1, text.Length - end - 1);
            foreach (string line in text.Substring(0, end).Split('\n')) Handle(line.TrimEnd('\r'), now);
            if (catchingUp)
            {
                // An old log of a Hunt that is not running any more says nothing about now.
                if (!huntRunning) Set(HuntPhase.None, null, now);
                catchingUp = false;
                HuntStatus copy = Status;
                Log.Write("Hunt: " + Describe(copy));
                var handler = Changed;
                if (handler != null) handler(copy);
            }
        }

        void Handle(string line, DateTime now)
        {
            var time = TimeRx.Match(line);
            if (!time.Success) return;
            // Live, the moment of reading is exact enough; for the past the log's own time (UTC).
            DateTime at = catchingUp ? LogTime(time, now) : now;
            var level = LevelRx.Match(line);
            if (level.Success)
            {
                string name = level.Groups[1].Value;
                if (name == "menu") Set(HuntPhase.Menu, null, at);
                else if (name.StartsWith("levels/", StringComparison.Ordinal)) Set(HuntPhase.Loading, name.Substring(7), at);
                return;
            }
            var bag = BagRx.Match(line);
            if (bag.Success)
            {
                string state = bag.Groups[1].Value;
                if (state == "MissionStarted") Set(HuntPhase.Match, null, at);
                else if (state == "ContentsDumped") Set(HuntPhase.Menu, null, at);
                else if (state == "Empty" && Status.Phase == HuntPhase.Summary) Set(HuntPhase.Menu, null, at);
                return;
            }
            // A level done loading with no mission begun (in a match the bag starts before this line):
            // the shooting range. A map's level stays "loading" until its mission starts.
            var loaded = LoadedRx.Match(line);
            if (loaded.Success)
            {
                if (Status.Phase == HuntPhase.Loading && !Maps.ContainsKey(loaded.Groups[1].Value)) Set(HuntPhase.Range, loaded.Groups[1].Value, at);
                return;
            }
            if (line.IndexOf("Starting UI state machine 'post_match_summary/", StringComparison.Ordinal) >= 0) Set(HuntPhase.Summary, null, at);
        }

        void Set(HuntPhase phase, string level, DateTime at)
        {
            HuntStatus copy;
            lock (sync)
            {
                if (level != null)
                {
                    string map;
                    status.Level = level;
                    status.MapName = Maps.TryGetValue(level, out map) ? map : null;
                }
                // The mission bag reports "started" every minute during a match.
                if (phase == status.Phase && level == null) return;
                if (phase == HuntPhase.Match) status.MatchStarted = at;
                status.Phase = phase;
                status.Since = at;
                copy = status.Copy();
            }
            if (catchingUp) return;
            if (phase == HuntPhase.Match) Log.Write("Hunt: Match beginnt" + (copy.MapName != null ? " auf " + copy.MapName : ""));
            var handler = Changed;
            if (handler != null) handler(copy);
        }

        static string Describe(HuntStatus s)
        {
            switch (s.Phase)
            {
                case HuntPhase.Menu: return "im Menü";
                case HuntPhase.Loading: return "lädt " + (s.MapName ?? s.Level);
                case HuntPhase.Match: return "im Match" + (s.MapName != null ? " auf " + s.MapName : "");
                case HuntPhase.Summary: return "Mission Summary";
                case HuntPhase.Range: return s.Level == "shooting" ? "auf dem Schießstand" : "im Level " + s.Level;
                default: return "läuft nicht";
            }
        }

        static DateTime LogTime(System.Text.RegularExpressions.Match m, DateTime now)
        {
            var at = now.Date + new TimeSpan(int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
            return at > now.AddMinutes(5) ? at.AddDays(-1) : at;
        }

        // The language of the texts Hunt loaded at its last start ("english", "german" …), or null.
        // It says so in the first few hundred lines, so the start of the log is enough.
        public static string GameLanguage()
        {
            string file = FindLog();
            if (file == null) return null;
            try
            {
                string text;
                using (var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var reader = new StreamReader(fs, Encoding.UTF8))
                {
                    var buffer = new char[512 * 1024];
                    text = new string(buffer, 0, reader.ReadBlock(buffer, 0, buffer.Length));
                }
                var found = Regex.Matches(text, @"/localization/([a-z]+?)(?:_xml)?\.pak");
                if (found.Count == 0) found = Regex.Matches(text, @"user preferred language '(\w+)'");
                return found.Count > 0 ? found[found.Count - 1].Groups[1].Value.ToLowerInvariant() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // Hunt's USER folder with its log and game.cfg; for tests the folder of the stand-in log.
        public static string UserFolder()
        {
            string test = Environment.GetEnvironmentVariable("KOPFGELD_TEST_HUNTLOG");
            if (!string.IsNullOrEmpty(test)) return Path.GetDirectoryName(Path.GetFullPath(test));
            string hunt = SteamFiles.HuntFolder();
            return hunt != null ? Path.Combine(hunt, "USER") : null;
        }

        static string FindLog()
        {
            // For tests: a file stands in for Hunt's log.
            string test = Environment.GetEnvironmentVariable("KOPFGELD_TEST_HUNTLOG");
            if (!string.IsNullOrEmpty(test)) return File.Exists(test) ? test : null;
            string folder = UserFolder();
            if (folder == null) return null;
            string file = Path.Combine(folder, "Game.log");
            return File.Exists(file) ? file : null;
        }

        static bool HuntRuns()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KOPFGELD_TEST_HUNTLOG"))) return true;
            var list = Process.GetProcessesByName("HuntGame");
            foreach (var p in list) p.Dispose();
            return list.Length > 0;
        }
    }
}
