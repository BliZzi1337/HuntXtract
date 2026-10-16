using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace Kopfgeld
{
    sealed class Settings
    {
        static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Kopfgeld.v1");
        static readonly string[] CountdownPositions = { "top-center", "top-left", "top-right", "bottom-center", "bottom-right" };
        static readonly string[] Themes = { "hunt", "modern", "pergament", "blutmond", "bayou", "nacht" };
        static readonly string[] UiFonts = { "auto", "serif", "modern", "western" };
        // How long matches are kept: all, the last days ("30d") or the last so many matches ("500").
        public static readonly string[] KeepChoices = { "all", "30d", "90d", "365d", "100", "250", "500", "1000" };
        static readonly Regex BackdropPattern = new Regex("^(auto|none|[a-z0-9-]{1,24})$");
        static readonly Regex ColorPattern = new Regex("^#[0-9a-fA-F]{6}$");
        static readonly Regex ChromePattern = new Regex("^[0-9a-f,]{0,80}$");

        // OBS
        public string ObsHost { get; set; }
        public int ObsPort { get; set; }
        public string ObsPasswordProtected { get; set; }  // DPAPI, current Windows user
        public string ObsPassword { get; set; }           // optional plain text for manual edits; encrypted on save
        public string SourceName { get; set; }

        // Where the picture comes from: "obs" or "screen" (straight from a monitor).
        public string CaptureSource { get; set; }
        public string ScreenDevice { get; set; }

        // Capture & detection
        public bool AutoDetect { get; set; }
        public int DetectFps { get; set; }
        public int DetectWidth { get; set; }
        public bool SaveScreenshots { get; set; }
        public int ScreenshotWidth { get; set; }
        // Tidying up: which matches are kept (KeepChoices) and what goes of the others – only their
        // pictures ("pictures", values and statistics stay) or the whole match ("matches").
        public string KeepMatches { get; set; }
        public string KeepWhat { get; set; }

        // Behaviour
        // "en" or "de". Not set in files from before 1.4.0 – those users keep German (see Load).
        public string Language { get; set; }
        // Own name in Hunt: tells HuntXtract which row in the Team Details is yours.
        public string PlayerName { get; set; }
        public bool Sound { get; set; }
        public int DayStartHour { get; set; }
        public string Hotkey { get; set; }
        public bool MinimizeToTray { get; set; }
        // The guided tour was finished or turned down with "Ich brauche keine Hilfe".
        public bool TourDone { get; set; }

        // Countdown window on top of the game
        public bool CountdownInGame { get; set; }
        public string CountdownPosition { get; set; }

        // Overlay
        public bool OverlayEnabled { get; set; }
        public int OverlayPort { get; set; }

        // Look of the program
        public string Theme { get; set; }           // hunt | modern | pergament | blutmond | bayou | nacht
        public string Backdrop { get; set; }        // "auto" (the design's own picture), "none" or a picture id
        public int BackdropStrength { get; set; }   // 0–100: how much of the picture shows
        public string UiFont { get; set; }          // western (the default) | auto (the design's own) | serif | modern
        public string Accent { get; set; }          // "" = the design's own, else #rrggbb
        // Window colours the UI reported for the current design ("bg,text,accent,muted,track,dark"),
        // so the title bar and the countdown window match before the UI has loaded.
        public string Chrome { get; set; }

        // Window
        public int WindowX { get; set; }
        public int WindowY { get; set; }
        public int WindowWidth { get; set; }
        public int WindowHeight { get; set; }
        public bool WindowMaximized { get; set; }

        public Settings()
        {
            ObsHost = "127.0.0.1";
            ObsPort = 4455;
            ObsPasswordProtected = "";
            SourceName = "";
            // New installs read straight from the screen: that works without any OBS at all.
            CaptureSource = "screen";
            ScreenDevice = "";
            AutoDetect = true;
            DetectFps = 4;
            DetectWidth = 640;
            SaveScreenshots = true;
            ScreenshotWidth = 1600;
            KeepMatches = "all";
            KeepWhat = "pictures";
            PlayerName = "";
            Sound = true;
            DayStartHour = 5;
            Hotkey = "Ctrl+Alt+K";
            MinimizeToTray = true;
            CountdownInGame = true;
            CountdownPosition = "top-center";
            OverlayEnabled = true;
            OverlayPort = 4466;
            Theme = "hunt";
            Backdrop = "auto";
            BackdropStrength = 55;
            UiFont = "western";
            Accent = "";
            Chrome = "";
        }

        [ScriptIgnore]
        public string Password
        {
            get
            {
                if (!string.IsNullOrEmpty(ObsPassword)) return ObsPassword;
                if (string.IsNullOrEmpty(ObsPasswordProtected)) return "";
                try
                {
                    return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(ObsPasswordProtected), Entropy, DataProtectionScope.CurrentUser));
                }
                catch
                {
                    return "";
                }
            }
            set
            {
                ObsPassword = null;
                ObsPasswordProtected = string.IsNullOrEmpty(value)
                    ? ""
                    : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), Entropy, DataProtectionScope.CurrentUser));
            }
        }

        [ScriptIgnore]
        public string OverlayUrl
        {
            get { return "http://127.0.0.1:" + OverlayPort + "/overlay"; }
        }

        public Settings Clone()
        {
            return (Settings)MemberwiseClone();
        }

        public void Normalize()
        {
            ObsHost = string.IsNullOrWhiteSpace(ObsHost) ? "127.0.0.1" : ObsHost.Trim();
            ObsPort = Clamp(ObsPort, 1, 65535, 4455);
            SourceName = (SourceName ?? "").Trim();
            if (CaptureSource != "screen") CaptureSource = "obs";
            ScreenDevice = (ScreenDevice ?? "").Trim();
            if (Language != "de") Language = "en";
            PlayerName = (PlayerName ?? "").Trim();
            if (PlayerName.Length > 40) PlayerName = PlayerName.Substring(0, 40);
            DetectFps = Clamp(DetectFps, 1, 10, 4);
            DetectWidth = Clamp(DetectWidth, 320, 1280, 640);
            ScreenshotWidth = Clamp(ScreenshotWidth, 640, 3840, 1600);
            if (Array.IndexOf(KeepChoices, KeepMatches) < 0) KeepMatches = "all";
            if (KeepWhat != "matches") KeepWhat = "pictures";
            DayStartHour = Math.Max(0, Math.Min(12, DayStartHour));
            if (string.IsNullOrWhiteSpace(Hotkey)) Hotkey = "Ctrl+Alt+K";
            if (Array.IndexOf(CountdownPositions, CountdownPosition) < 0) CountdownPosition = "top-center";
            OverlayPort = Clamp(OverlayPort, 1024, 65535, 4466);
            if (ObsPasswordProtected == null) ObsPasswordProtected = "";
            if (Array.IndexOf(Themes, Theme) < 0) Theme = "hunt";
            if (Backdrop == null || !BackdropPattern.IsMatch(Backdrop)) Backdrop = "auto";
            BackdropStrength = Math.Max(0, Math.Min(100, BackdropStrength));
            if (Array.IndexOf(UiFonts, UiFont) < 0) UiFont = "western";
            Accent = Accent != null && ColorPattern.IsMatch(Accent) ? Accent.ToLowerInvariant() : "";
            if (Chrome == null || !ChromePattern.IsMatch(Chrome)) Chrome = "";
        }

        static int Clamp(int value, int min, int max, int fallback)
        {
            if (value == 0) return fallback;
            return Math.Max(min, Math.Min(max, value));
        }

        // What the UI may see and edit. The password never leaves this process.
        public Dictionary<string, object> ToUi()
        {
            var d = new Dictionary<string, object>();
            d["obsHost"] = ObsHost;
            d["obsPort"] = ObsPort;
            d["hasPassword"] = Password.Length > 0;
            d["sourceName"] = SourceName;
            d["captureSource"] = CaptureSource;
            d["screenDevice"] = ScreenDevice;
            d["screenLabel"] = CaptureSource == "screen" ? ScreenCapture.Label(ScreenDevice) : "";
            d["autoDetect"] = AutoDetect;
            d["detectFps"] = DetectFps;
            d["saveScreenshots"] = SaveScreenshots;
            d["screenshotWidth"] = ScreenshotWidth;
            d["keepMatches"] = KeepMatches;
            d["keepWhat"] = KeepWhat;
            d["language"] = Language;
            d["playerName"] = PlayerName;
            d["sound"] = Sound;
            d["dayStartHour"] = DayStartHour;
            d["hotkey"] = Hotkey;
            d["minimizeToTray"] = MinimizeToTray;
            d["tourDone"] = TourDone;
            d["countdownInGame"] = CountdownInGame;
            d["countdownPosition"] = CountdownPosition;
            d["overlayEnabled"] = OverlayEnabled;
            d["overlayPort"] = OverlayPort;
            d["overlayUrl"] = OverlayUrl;
            d["theme"] = Theme;
            d["backdrop"] = Backdrop;
            d["backdropStrength"] = BackdropStrength;
            d["uiFont"] = UiFont;
            d["accent"] = Accent;
            return d;
        }

        public void ApplyFromUi(Dictionary<string, object> d)
        {
            if (d.ContainsKey("obsHost")) ObsHost = Json.Str(d, "obsHost");
            if (d.ContainsKey("obsPort")) ObsPort = (int)Json.Long(d, "obsPort", ObsPort);
            if (d.ContainsKey("password") && d["password"] != null) Password = Json.Str(d, "password");
            if (d.ContainsKey("sourceName")) SourceName = Json.Str(d, "sourceName");
            if (d.ContainsKey("captureSource")) CaptureSource = Json.Str(d, "captureSource");
            if (d.ContainsKey("screenDevice")) ScreenDevice = Json.Str(d, "screenDevice");
            if (d.ContainsKey("autoDetect")) AutoDetect = Json.Bool(d, "autoDetect");
            if (d.ContainsKey("detectFps")) DetectFps = (int)Json.Long(d, "detectFps", DetectFps);
            if (d.ContainsKey("saveScreenshots")) SaveScreenshots = Json.Bool(d, "saveScreenshots");
            if (d.ContainsKey("screenshotWidth")) ScreenshotWidth = (int)Json.Long(d, "screenshotWidth", ScreenshotWidth);
            if (d.ContainsKey("keepMatches")) KeepMatches = Json.Str(d, "keepMatches");
            if (d.ContainsKey("keepWhat")) KeepWhat = Json.Str(d, "keepWhat");
            if (d.ContainsKey("language")) Language = Json.Str(d, "language");
            if (d.ContainsKey("playerName")) PlayerName = Json.Str(d, "playerName");
            if (d.ContainsKey("sound")) Sound = Json.Bool(d, "sound");
            if (d.ContainsKey("dayStartHour")) DayStartHour = (int)Json.Long(d, "dayStartHour", DayStartHour);
            if (d.ContainsKey("hotkey")) Hotkey = Json.Str(d, "hotkey");
            if (d.ContainsKey("minimizeToTray")) MinimizeToTray = Json.Bool(d, "minimizeToTray");
            if (d.ContainsKey("tourDone")) TourDone = Json.Bool(d, "tourDone");
            if (d.ContainsKey("countdownInGame")) CountdownInGame = Json.Bool(d, "countdownInGame");
            if (d.ContainsKey("countdownPosition")) CountdownPosition = Json.Str(d, "countdownPosition");
            if (d.ContainsKey("overlayEnabled")) OverlayEnabled = Json.Bool(d, "overlayEnabled");
            if (d.ContainsKey("overlayPort")) OverlayPort = (int)Json.Long(d, "overlayPort", OverlayPort);
            if (d.ContainsKey("theme")) Theme = Json.Str(d, "theme");
            if (d.ContainsKey("backdrop")) Backdrop = Json.Str(d, "backdrop");
            if (d.ContainsKey("backdropStrength")) BackdropStrength = (int)Json.Long(d, "backdropStrength", BackdropStrength);
            if (d.ContainsKey("uiFont")) UiFont = Json.Str(d, "uiFont");
            if (d.ContainsKey("accent")) Accent = Json.Str(d, "accent");
            Normalize();
        }

        public static Settings Load(out bool existed)
        {
            existed = File.Exists(AppInfo.SettingsFile);
            Settings s = null;
            if (existed)
            {
                try
                {
                    s = Json.Deserialize<Settings>(File.ReadAllText(AppInfo.SettingsFile, Encoding.UTF8));
                }
                catch (Exception ex)
                {
                    Log.Write("settings.json konnte nicht gelesen werden, nutze Standardwerte: " + ex.Message);
                }
            }
            if (s == null) s = new Settings();
            // New installs start in English; settings from before the language choice existed
            // belong to someone who has used the program in German so far.
            if (string.IsNullOrEmpty(s.Language)) s.Language = existed ? "de" : "en";
            s.Normalize();
            return s;
        }

        public void Save()
        {
            Normalize();
            if (!string.IsNullOrEmpty(ObsPassword)) Password = ObsPassword;
            Files.WriteAllTextAtomic(AppInfo.SettingsFile, Json.Pretty(Json.Serialize(this)));
        }
    }

    static class Files
    {
        // Write to a temp file first, so a crash never leaves a half-written file behind.
        public static void WriteAllTextAtomic(string path, string text)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, text, new UTF8Encoding(false));
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }
    }
}
