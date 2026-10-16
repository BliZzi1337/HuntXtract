using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace Kopfgeld
{
    static class AppInfo
    {
        public const string Name = "HuntXtract";
        public const string Version = "2.0.0";

        public static string Subtitle
        {
            get { return Lang.T("Match-Tracker für Hunt: Showdown", "Match tracker for Hunt: Showdown"); }
        }
        public const string Author = "Chris aka BliZzi1337";
        public const string AuthorUrl = "https://steamcommunity.com/id/blizzi1337/";
        // Virtual origin for the built-in UI; ".example" never resolves, WebView2 serves it from resources.
        // Still named after "Bounty Ledger": the browser storage of the UI belongs to this origin.
        public const string UiOrigin = "https://bountyledger.example";
        public const string UiUrl = UiOrigin + "/app/index.html";

        // Chromium keeps its storage about 100 characters below the profile folder and fails
        // once the full path passes 260, so the data folder must stay reasonably short.
        const int MaxDataDirLength = 120;

        static readonly Dictionary<string, byte[]> ResourceCache = new Dictionary<string, byte[]>();

        public static string AppDir { get; private set; }
        public static string DataDir { get; private set; }

        // Why the data is not in the program folder: null, "deep" or "readonly".
        static string dataDirReason;
        static string fallbackName;

        public static string DataDirNote
        {
            get
            {
                if (dataDirReason == "deep")
                    return Lang.F("Der Programmordner liegt zu tief verschachtelt. Deine Daten liegen deshalb unter %LOCALAPPDATA%\\{0}. Tipp: den Ordner z. B. nach C:\\Portable Stuff\\ verschieben.",
                        "The program folder is nested too deeply, so your data is kept in %LOCALAPPDATA%\\{0}. Tip: move the folder to e.g. C:\\Portable Stuff\\.", fallbackName);
                if (dataDirReason == "readonly")
                    return Lang.F("Der Programmordner ist schreibgeschützt. Deine Daten liegen deshalb unter %LOCALAPPDATA%\\{0}.",
                        "The program folder is read-only, so your data is kept in %LOCALAPPDATA%\\{0}.", fallbackName);
                return null;
            }
        }

        public static string WebViewDataDir { get { return Path.Combine(DataDir, "WebView2"); } }
        public static string SettingsFile { get { return Path.Combine(DataDir, "settings.json"); } }
        public static string CalibrationFile { get { return Path.Combine(DataDir, "kalibrierung.json"); } }
        public static string ReferenceImage { get { return Path.Combine(DataDir, "referenz.png"); } }
        // The same Mission Summary as a detection-sized frame (small JPEG, like the live checks).
        public static string ReferenceDetectImage { get { return Path.Combine(DataDir, "referenz-erkennung.jpg"); } }
        // The second screen: Team Details.
        public static string TeamReferenceImage { get { return Path.Combine(DataDir, "referenz-team.png"); } }
        public static string TeamReferenceDetectImage { get { return Path.Combine(DataDir, "referenz-team-erkennung.jpg"); } }
        // The third: the map on Tab.
        public static string MapReferenceImage { get { return Path.Combine(DataDir, "referenz-karte.png"); } }
        public static string MapReferenceDetectImage { get { return Path.Combine(DataDir, "referenz-karte-erkennung.jpg"); } }
        // The trail of the match that is running, until its Mission Summary is saved.
        public static string TrailFile { get { return Path.Combine(DataDir, "laufspur.json"); } }
        public static string TrailMapFile { get { return Path.Combine(DataDir, "laufspur-karte.jpg"); } }
        // The map at each point of that trail (001.jpg, 002.jpg, …).
        public static string TrailPicturesDir { get { return Path.Combine(DataDir, "laufspur-bilder"); } }
        // The maps as the game opens them, remembered at the start of a round (MapMemory).
        public static string MapPrintsFile { get { return Path.Combine(DataDir, "kartenbilder.json"); } }
        // The last look at the map that did not count – to see why, when a trail stays short.
        public static string RejectedMapFile { get { return Path.Combine(DataDir, "laufspur-verworfen.jpg"); } }
        public static string MatchesFile { get { return Path.Combine(DataDir, "matches.json"); } }
        public static string DeletedFile { get { return Path.Combine(DataDir, "geloescht.json"); } }
        public static string OverlaysFile { get { return Path.Combine(DataDir, "overlays.json"); } }
        // Pictures from before 1.6.0, all in one folder; they move into MatchesDir on the first start.
        public static string ScreenshotDir { get { return Path.Combine(DataDir, "screenshots"); } }
        // One folder per day, in it one per match with its pictures and stats.txt.
        public static string MatchesDir { get { return Path.Combine(DataDir, "Matches"); } }
        public static string LogFile { get { return Path.Combine(DataDir, "protokoll.log"); } }

        public static void Init()
        {
            AppDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string portable = Path.Combine(AppDir, "Daten");
            // Data of the time the program was called "Bounty Ledger" stays where it is.
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string fallback = Directory.Exists(Path.Combine(local, "BountyLedger")) ? "BountyLedger" : "HuntXtract";
            if (portable.Length > MaxDataDirLength)
            {
                dataDirReason = "deep";
            }
            else if (IsWritable(portable))
            {
                DataDir = portable;
                Directory.CreateDirectory(MatchesDir);
                return;
            }
            else
            {
                dataDirReason = "readonly";
            }
            fallbackName = fallback;
            DataDir = Path.Combine(local, fallback);
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(MatchesDir);
        }

        static bool IsWritable(string dir)
        {
            try
            {
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".write-test");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsWebUri(string uri)
        {
            return uri != null &&
                (uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                 uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsUiUri(string uri)
        {
            return uri != null && uri.StartsWith(UiOrigin + "/", StringComparison.OrdinalIgnoreCase);
        }

        public static void OpenExternal(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Write("Konnte nicht öffnen: " + target + " (" + ex.Message + ")");
            }
        }

        // Embedded files are named by their path below web\, e.g. "app/index.html". Fonts and
        // pictures (fonts/…, img/…) are plain files in the assets folder next to the exe instead,
        // so the exe stays small. For UI development KOPFGELD_UI_DIR can point at the web folder
        // to serve everything from disk.
        public static byte[] Resource(string name)
        {
            string devDir = Environment.GetEnvironmentVariable("KOPFGELD_UI_DIR");
            if (!string.IsNullOrEmpty(devDir))
            {
                byte[] dev = FileBelow(devDir, name);
                if (dev != null) return dev;
            }
            lock (ResourceCache)
            {
                byte[] bytes;
                if (ResourceCache.TryGetValue(name, out bytes)) return bytes;
                if (name.StartsWith("fonts/", StringComparison.Ordinal) || name.StartsWith("img/", StringComparison.Ordinal))
                {
                    bytes = FileBelow(Path.Combine(AppDir, "assets"), name);
                    if (bytes == null) return null;
                }
                else
                {
                    using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
                    {
                        if (stream == null) return null;
                        using (var ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            bytes = ms.ToArray();
                        }
                    }
                }
                ResourceCache[name] = bytes;
                return bytes;
            }
        }

        // The file for a URL path below root – never outside of it.
        static byte[] FileBelow(string root, string name)
        {
            string full = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string file = Path.GetFullPath(Path.Combine(full, name.Replace('/', Path.DirectorySeparatorChar)));
            if (!file.StartsWith(full, StringComparison.OrdinalIgnoreCase) || !File.Exists(file)) return null;
            try
            {
                return File.ReadAllBytes(file);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        public static string ResourceText(string name)
        {
            byte[] bytes = Resource(name);
            return bytes == null ? null : Encoding.UTF8.GetString(bytes);
        }

        public static Stream OpenResource(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }

        public static string ContentType(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            switch (ext)
            {
                case ".html": return "text/html; charset=utf-8";
                case ".js": return "text/javascript; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".json": return "application/json; charset=utf-8";
                case ".svg": return "image/svg+xml";
                case ".png": return "image/png";
                case ".jpg": return "image/jpeg";
                case ".ico": return "image/x-icon";
                case ".woff2": return "font/woff2";
                case ".txt": return "text/plain; charset=utf-8";
                default: return "application/octet-stream";
            }
        }
    }

    static class Log
    {
        static readonly object Sync = new object();
        static string path;

        public static void Init()
        {
            path = AppInfo.LogFile;
            try
            {
                var info = new FileInfo(path);
                if (info.Exists && info.Length > 1024 * 1024)
                {
                    File.Copy(path, path + ".old", true);
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }

        public static void Write(string message)
        {
            if (path == null) return;
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine;
            lock (Sync)
            {
                try
                {
                    File.AppendAllText(path, line, Encoding.UTF8);
                }
                catch
                {
                }
            }
        }
    }
}
