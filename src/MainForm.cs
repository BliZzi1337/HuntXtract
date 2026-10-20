using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

namespace Kopfgeld
{
    sealed class UserError : Exception
    {
        public UserError(string message) : base(message) { }
    }

    sealed class MainForm : Form
    {
        // Keep the UI and its timers alive while minimized, in the tray or behind the game.
        const string BrowserArguments =
            "--disable-background-timer-throttling --disable-renderer-backgrounding " +
            "--disable-backgrounding-occluded-windows --disable-features=CalculateNativeWinOcclusion";
        const int HotkeyId = 0x4B47;
        static readonly Regex SafeFileName = new Regex("^[A-Za-z0-9._-]+$");

        readonly EventWaitHandle showSignal;
        readonly bool firstRun;
        readonly Panel webHost;
        readonly NotifyIcon tray;
        readonly MatchStore store = new MatchStore();
        // How long matchmaking took, from Hunt's log.
        readonly SearchStore searches = new SearchStore();
        readonly OverlayProfiles overlays = new OverlayProfiles();
        readonly OcrService ocr;
        readonly string ocrError;
        readonly CaptureEngine engine;
        // Hunt's own log: menu or match, on which map.
        readonly HuntLog huntLog = new HuntLog();
        // The Steam name of this PC's account (null if Steam isn't found).
        readonly string steamName;
        readonly System.Windows.Forms.Timer minuteTimer;
        readonly float scale;

        volatile Settings settings;
        volatile Calibration calibration;
        volatile string statsJson = "{}";
        volatile string statusJson = "{}";
        OverlayServer overlayServer;
        GameOverlay gameOverlay;
        // Colours of the chosen design; null until the UI has reported one (then Windows decides).
        WindowChrome chrome;
        string overlayError;
        string hotkeyError;
        CoreWebView2Environment environment;
        CoreWebView2Controller view;
        RegisteredWaitHandle showWait;
        // Previews of the pictures taken in the setup, by page ("summary", "team", "map").
        readonly Dictionary<string, byte[]> referencePreviews = new Dictionary<string, byte[]>();
        long referenceVersion;
        bool wasMaximized;
        bool trayHintShown;

        public MainForm(Settings settings, bool firstRun, EventWaitHandle showSignal)
        {
            this.settings = settings;
            this.firstRun = firstRun;
            this.showSignal = showSignal;
            chrome = WindowChrome.Parse(settings.Chrome);

            Text = AppInfo.Name + " – " + AppInfo.Subtitle;
            Font = SystemFonts.MessageBoxFont;
            BackColor = WindowBackground;
            MinimumSize = new Size(960, 620);
            Icon = LoadIcon(0);
            scale = DeviceDpi / 96f;
            RestoreWindowBounds();

            webHost = new Panel { Dock = DockStyle.Fill, BackColor = WindowBackground };
            webHost.Resize += (s, e) => UpdateViewBounds();
            Controls.Add(webHost);

            tray = new NotifyIcon { Icon = LoadIcon(SystemInformation.SmallIconSize.Width), Text = AppInfo.Name, ContextMenuStrip = TrayMenu(), Visible = true };
            tray.DoubleClick += (s, e) => RestoreWindow();

            store.Load();
            searches.Load();
            overlays.Load();
            // New here and Hunt runs in 1920 × 1080: the setup that comes with HuntXtract, ready to use.
            Presets.ApplyOnFirstStart(settings);
            calibration = Calibration.Load();
            LocalizeFieldLabels();
            UpgradeAnchorPrint();
            // Pages at the places of the ready-made setup are recognised in each of its languages.
            int languages = Presets.AttachLanguagePrints(calibration);
            if (languages > 1) Log.Write("Wiedererkennung in " + languages + " Spielsprachen");
            // One folder per day and match (pictures from before move there once), then tidy up.
            store.MigrateToFolders(calibration);
            RunCleanup(false);
            ocr = OcrService.TryCreate(out ocrError);
            if (ocr != null) Log.Write("Texterkennung: Windows OCR (" + ocr.Language + ")");
            else Log.Write(ocrError);

            // Without a name yet, the one Hunt shows is the Steam name of this PC's account.
            steamName = SteamFiles.PersonaName();
            if (this.settings.PlayerName.Length == 0 && steamName != null)
            {
                this.settings.PlayerName = steamName.Length > 40 ? steamName.Substring(0, 40) : steamName;
                try
                {
                    this.settings.Save();
                    Log.Write("Name in Hunt aus Steam übernommen: " + this.settings.PlayerName);
                    if (calibration.Team != null && store.FillOwnMmr(calibration.Team, this.settings.PlayerName) > 0) Log.Write("Eigene MMR nachgetragen");
                }
                catch (Exception ex)
                {
                    Log.Write("Einstellungen konnten nicht gespeichert werden: " + ex.Message);
                }
            }

            engine = new CaptureEngine(() => this.settings, () => calibration, () => huntLog.Status, ocr, SaveMatch, MergeTeam);
            huntLog.Changed += hunt =>
            {
                engine.HuntChanged(hunt);
                RunOnUi(() => PostEvent("status", StatusPayload(engine.Status)));
            };
            huntLog.SearchFinished += (start, seconds, lobby, dissolved) =>
            {
                if (!searches.Add(start, seconds, lobby, dissolved)) return;
                Log.Write("Matchsuche: " + seconds + " s Suche + " + lobby + " s Lobby" + (dissolved > 0 ? ", " + dissolved + " Lobby aufgelöst" : ""));
                RunOnUi(() => PublishStats(false));
            };
            engine.StatusChanged += st =>
            {
                statusJson = Json.Serialize(StatusPayload(st));
                RunOnUi(() =>
                {
                    PostEvent("status", StatusPayload(st));
                    UpdateGameOverlay(st);
                });
            };
            engine.Preview += (image, similarity) => RunOnUi(() =>
            {
                var d = new Dictionary<string, object>();
                d["image"] = image;
                d["similarity"] = similarity < 0 ? null : (object)Math.Round(similarity, 1);
                PostEvent("preview", d);
            });
            // The engine only reports problems while reading, e.g. after the hotkey – worth seeing in the game too.
            engine.Notice += (kind, text) => RunOnUi(() =>
            {
                Notice(kind, text);
                if (kind == "warn" || kind == "error") ShowInGame(Lang.T("Nicht gespeichert", "Not saved"), text, 6000, -1);
            });

            minuteTimer = new System.Windows.Forms.Timer { Interval = 60000 };
            minuteTimer.Tick += (s, e) => PublishStats(false);
            RefreshStats();
            statusJson = Json.Serialize(StatusPayload(engine.Status));
        }

        // Built again when the language changes.
        ContextMenuStrip TrayMenu()
        {
            var menu = new ContextMenuStrip { Renderer = new DarkRenderer() };
            menu.Items.Add(MenuItem(Lang.T("Öffnen", "Open"), (s, e) => RestoreWindow(), true));
            menu.Items.Add(MenuItem(Lang.T("Jetzt auslesen", "Read now"), (s, e) => engine.RequestExtract(), false));
            menu.Items.Add(MenuItem(Lang.T("Overlay-Adresse kopieren", "Copy overlay address"),
                (s, e) => CopyText(settings.OverlayUrl, Lang.T("Overlay-Adresse kopiert", "Overlay address copied")), false));
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(MenuItem(Lang.T("Beenden", "Exit"), (s, e) => Close(), false));
            return menu;
        }

        static ToolStripMenuItem MenuItem(string text, EventHandler onClick, bool bold)
        {
            var item = new ToolStripMenuItem(text, null, onClick) { ForeColor = Theme.Text };
            if (bold) item.Font = new Font(item.Font, FontStyle.Bold);
            return item;
        }

        // Field names HuntXtract gave itself follow the language; names you typed stay as they are.
        // Returns true if a name changed (the calibration is then saved already).
        bool LocalizeFieldLabels()
        {
            var updated = calibration.Clone();
            if (!updated.LocalizeLabels()) return false;
            calibration = updated;
            if (!File.Exists(AppInfo.CalibrationFile)) return true;
            try
            {
                updated.Save();
            }
            catch (Exception ex)
            {
                Log.Write("Feldnamen konnten nicht gespeichert werden: " + ex.Message);
            }
            return true;
        }

        static Icon LoadIcon(int size)
        {
            using (var stream = AppInfo.OpenResource("app.ico"))
            {
                return size > 0 ? new Icon(stream, size, size) : new Icon(stream);
            }
        }

        // ---------------------------------------------------------------- startup

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            showWait = ThreadPool.RegisterWaitForSingleObject(showSignal, (state, timedOut) => RunOnUi(RestoreWindow), null, -1, false);
            RegisterHotkey();
            StartOverlayServer();
            engine.Start();
            huntLog.Start();
            minuteTimer.Start();
            InitializeView();
        }

        async void InitializeView()
        {
            try
            {
                string args = BrowserArguments;
                string debugPort = Environment.GetEnvironmentVariable("KOPFGELD_DEBUG_PORT");
                if (!string.IsNullOrEmpty(debugPort)) args += " --remote-debugging-port=" + debugPort + " --remote-allow-origins=*";
                environment = await CoreWebView2Environment.CreateAsync(null, AppInfo.WebViewDataDir, new CoreWebView2EnvironmentOptions(args));
                Log.Write("WebView2-Runtime " + environment.BrowserVersionString);

                var controller = await environment.CreateCoreWebView2ControllerAsync(webHost.Handle);
                view = controller;
                controller.DefaultBackgroundColor = WindowBackground;
                controller.IsVisible = true;
                UpdateViewBounds();

                var core = controller.CoreWebView2;
                core.Settings.IsStatusBarEnabled = false;
                core.Settings.IsGeneralAutofillEnabled = false;
                core.Settings.IsPasswordAutosaveEnabled = false;
                core.Settings.IsSwipeNavigationEnabled = false;
                core.Settings.IsZoomControlEnabled = false;
                // No right-click menu, no DevTools and no browser shortcuts (reload, find, print) – in
                // test runs neither: the test tools reach the page through the remote debugging port.
                core.Settings.AreDevToolsEnabled = false;
                core.Settings.AreDefaultContextMenusEnabled = false;
                core.Settings.AreBrowserAcceleratorKeysEnabled = false;
                core.AddWebResourceRequestedFilter(AppInfo.UiOrigin + "/*", CoreWebView2WebResourceContext.All, CoreWebView2WebResourceRequestSourceKinds.All);
                core.WebResourceRequested += View_WebResourceRequested;
                core.WebMessageReceived += View_WebMessageReceived;
                core.NavigationStarting += (s, e) =>
                {
                    if (AppInfo.IsUiUri(e.Uri) || e.Uri == "about:blank") return;
                    e.Cancel = true;
                    if (AppInfo.IsWebUri(e.Uri)) AppInfo.OpenExternal(e.Uri);
                };
                core.NewWindowRequested += (s, e) =>
                {
                    e.Handled = true;
                    if (AppInfo.IsWebUri(e.Uri) && !AppInfo.IsUiUri(e.Uri)) AppInfo.OpenExternal(e.Uri);
                };
                core.ProcessFailed += (s, e) =>
                {
                    Log.Write("WebView2-Prozess ausgefallen: " + e.ProcessFailedKind + " (" + e.Reason + ")");
                    if (e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessExited ||
                        e.ProcessFailedKind == CoreWebView2ProcessFailedKind.RenderProcessUnresponsive)
                        RunOnUi(() => core.Navigate(AppInfo.UiUrl));
                };
                core.Navigate(AppInfo.UiUrl);
                controller.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
            }
            catch (WebView2RuntimeNotFoundException)
            {
                Log.Write("WebView2-Runtime nicht gefunden");
                MessageBox.Show(this,
                    Lang.T("Die Microsoft Edge WebView2-Runtime fehlt auf diesem PC.\n\nSie ist bei Windows 11 normalerweise dabei. Die Download-Seite wird jetzt geöffnet.",
                        "The Microsoft Edge WebView2 Runtime is missing on this PC.\n\nWindows 11 normally includes it. The download page opens now."),
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                AppInfo.OpenExternal("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                Close();
            }
            catch (Exception ex)
            {
                Log.Write("Oberfläche konnte nicht starten: " + ex);
                MessageBox.Show(this, Lang.T("Die Oberfläche konnte nicht starten:\n", "The interface could not start:\n") + ex.Message,
                    AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ---------------------------------------------------------------- UI files

        void View_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            Uri uri;
            if (!Uri.TryCreate(e.Request.Uri, UriKind.Absolute, out uri)) return;
            string path = Uri.UnescapeDataString(uri.AbsolutePath).TrimStart('/');
            byte[] body = null;
            string type = null;
            bool dynamic = false;

            if (path.StartsWith("shots/", StringComparison.Ordinal))
            {
                // A picture of a match: in its folder (day/match/file), or from before 1.6.0.
                string file = MatchFolders.FullPath(path.Substring(6));
                if (file != null && File.Exists(file))
                {
                    body = File.ReadAllBytes(file);
                    type = "image/jpeg";
                }
            }
            else if (path == "reference.jpg" || path == "reference-team.jpg" || path == "reference-map.jpg")
            {
                body = ReferencePreview(path == "reference-team.jpg" ? "team" : path == "reference-map.jpg" ? "map" : "summary");
                type = "image/jpeg";
                dynamic = true;
            }
            else if (path == "live-map.jpg")
            {
                body = engine.LiveMapPicture();
                type = "image/jpeg";
                dynamic = true;
            }
            else if (path.StartsWith("live-point/", StringComparison.Ordinal))
            {
                // The map at one point of the running match's trail.
                string name = path.Substring(11);
                if (SafeFileName.IsMatch(name) && !name.Contains("..")) body = TrailRecorder.ReadPointPicture(name);
                type = "image/jpeg";
                dynamic = true;
            }
            else
            {
                body = AppInfo.Resource(path);
                type = AppInfo.ContentType(path);
            }

            if (body == null)
            {
                e.Response = environment.CreateWebResourceResponse(null, 404, "Not Found", "Content-Type: text/plain");
                return;
            }
            e.Response = environment.CreateWebResourceResponse(new MemoryStream(body, false), 200, "OK",
                "Content-Type: " + type + "\r\nCache-Control: " + (dynamic ? "no-store" : "no-cache"));
        }

        // The picture a setup page was calibrated on, and the same as a detection-sized frame.
        static string ReferenceFile(string page)
        {
            return page == "team" ? AppInfo.TeamReferenceImage : page == "map" ? AppInfo.MapReferenceImage : AppInfo.ReferenceImage;
        }

        static string ReferenceDetectFile(string page)
        {
            return page == "team" ? AppInfo.TeamReferenceDetectImage : page == "map" ? AppInfo.MapReferenceDetectImage : AppInfo.ReferenceDetectImage;
        }

        static string PageOf(Dictionary<string, object> args)
        {
            string page = Json.Str(args, "page");
            return page == "team" || page == "map" ? page : "summary";
        }

        byte[] ReferencePreview(string page)
        {
            byte[] cached;
            referencePreviews.TryGetValue(page, out cached);
            string file = ReferenceFile(page);
            if (cached == null && File.Exists(file))
            {
                try
                {
                    using (var bmp = Imaging.Load(file)) cached = Imaging.EncodeJpeg(bmp, 90, 1600);
                }
                catch (Exception ex)
                {
                    Log.Write("Referenzbild nicht lesbar: " + ex.Message);
                }
                referencePreviews[page] = cached;
            }
            return cached;
        }

        string ReferenceUrl(string page)
        {
            string name = page == "team" ? "reference-team.jpg" : page == "map" ? "reference-map.jpg" : "reference.jpg";
            return File.Exists(ReferenceFile(page)) ? AppInfo.UiOrigin + "/" + name + "?v=" + referenceVersion : null;
        }

        // ---------------------------------------------------------------- UI messages

        async void View_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            if (!AppInfo.IsUiUri(e.Source)) return;
            Dictionary<string, object> msg;
            try
            {
                msg = Json.Parse(e.WebMessageAsJson);
            }
            catch
            {
                return;
            }
            string cmd = Json.Str(msg, "cmd");
            object args;
            msg.TryGetValue("args", out args);
            var reply = new Dictionary<string, object>();
            reply["reply"] = msg.ContainsKey("id") ? msg["id"] : null;
            try
            {
                reply["result"] = await HandleAsync(cmd, Json.Obj(args) ?? new Dictionary<string, object>());
                reply["ok"] = true;
            }
            catch (Exception ex)
            {
                reply["ok"] = false;
                reply["error"] = ex is ObsException || ex is UserError ? ex.Message : Lang.T("Fehler: ", "Error: ") + ex.Message;
                if (!(ex is UserError)) Log.Write("Befehl „" + cmd + "“ fehlgeschlagen: " + ex.Message);
            }
            Post(reply);
        }

        async Task<object> HandleAsync(string cmd, Dictionary<string, object> args)
        {
            switch (cmd)
            {
                case "init":
                    return InitPayload();

                case "saveSettings":
                {
                    var old = settings;
                    var updated = old.Clone();
                    updated.ApplyFromUi(args);
                    settings = updated;
                    SaveSettings();
                    ApplySettings(old, updated);
                    return updated.ToUi();
                }

                case "listScreens":
                    return ScreenCapture.List();

                case "obsTest":
                {
                    string password = args.ContainsKey("password") && args["password"] != null ? Json.Str(args, "password") : settings.Password;
                    return await TestObsAsync(Json.Str(args, "host") ?? settings.ObsHost, (int)Json.Long(args, "port", settings.ObsPort), password);
                }

                case "setPreview":
                    engine.SetPreview(Json.Bool(args, "enabled"), Json.Bool(args, "calibrating"));
                    return null;

                case "testSound":
                    Chime.Play();
                    return null;

                case "captureReference":
                {
                    string page = PageOf(args);
                    // Straight from the monitor only Hunt is taken: clicked here, wait until the game is in front.
                    if (settings.CaptureSource == "screen") await engine.WaitUntilHuntShownAsync(CaptureEngine.ReadNowWaitMs, CancellationToken.None);
                    Bitmap frame = await engine.GrabFullFrameAsync();
                    byte[] detectFrame = null;
                    try
                    {
                        detectFrame = await engine.GrabDetectFrameAsync();
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Erkennungsbild der Referenz fehlt: " + ex.Message);
                    }
                    string image = ReferenceFile(page);
                    string detectImage = ReferenceDetectFile(page);
                    using (frame)
                    {
                        Imaging.SavePng(frame, image);
                        if (detectFrame != null) File.WriteAllBytes(detectImage, detectFrame);
                        else if (File.Exists(detectImage)) File.Delete(detectImage);
                        referencePreviews[page] = Imaging.EncodeJpeg(frame, 90, 1600);
                        referenceVersion++;
                        Log.Write((page == "team" ? "Team-Details-Bild" : page == "map" ? "Kartenbild" : "Referenzbild") + " aufgenommen (" + frame.Width + "×" + frame.Height + ")");
                        var d = new Dictionary<string, object>();
                        d["url"] = ReferenceUrl(page);
                        d["width"] = frame.Width;
                        d["height"] = frame.Height;
                        return d;
                    }
                }

                case "saveCalibration":
                    return SaveCalibration(args);

                case "resetCalibration":
                {
                    var fresh = Calibration.CreateDefault();
                    fresh.Save();
                    calibration = fresh;
                    try
                    {
                        foreach (string page in new[] { "summary", "team", "map" })
                        {
                            if (File.Exists(ReferenceFile(page))) File.Delete(ReferenceFile(page));
                            if (File.Exists(ReferenceDetectFile(page))) File.Delete(ReferenceDetectFile(page));
                        }
                    }
                    catch
                    {
                    }
                    referencePreviews.Clear();
                    referenceVersion++;
                    engine.Wake();
                    PublishStats(true);
                    return CalibrationPayload();
                }

                case "applyPreset":
                {
                    if (!Presets.Available) throw new UserError(Lang.T("Die fertige Einrichtung fehlt im Programmordner.", "The ready-made setup is missing from the program folder."));
                    string language = HuntDisplay.Read().Language;
                    Presets.Apply(Presets.FolderOrDefault(language));
                    calibration = Calibration.Load();
                    LocalizeFieldLabels();
                    referencePreviews.Clear();
                    referenceVersion++;
                    Log.Write("Fertige Einrichtung geladen (Hunt-Sprache " + (language ?? "unbekannt") + ")");
                    engine.Wake();
                    PublishStats(true);
                    return CalibrationPayload();
                }

                case "testOcr":
                    return await TestOcrAsync(args);

                case "liveTrail":
                    return LiveTrailPayload();

                case "cancelMatch":
                    engine.CancelMatch();
                    return null;

                case "cleanupPreview":
                {
                    // What a new "Aufbewahren" setting would remove right away.
                    long bytes;
                    int count = store.Cleanup(Json.Str(args, "keep"), Json.Str(args, "what"), false, out bytes);
                    var d = new Dictionary<string, object>();
                    d["count"] = count;
                    d["megabytes"] = Math.Round(bytes / 1048576.0, 1);
                    return d;
                }

                case "storageInfo":
                {
                    var d = new Dictionary<string, object>();
                    d["megabytes"] = Math.Round((FolderBytes(AppInfo.MatchesDir) + FolderBytes(AppInfo.ScreenshotDir)) / 1048576.0, 1);
                    d["matches"] = store.Snapshot().Count;
                    return d;
                }

                case "openMatchesFolder":
                    AppInfo.OpenExternal(AppInfo.MatchesDir);
                    return null;

                case "extractNow":
                    engine.RequestExtract();
                    return null;

                case "previewCountdown":
                    InGame().Preview();
                    return null;

                case "updateMatch":
                {
                    var values = Json.Obj(args.ContainsKey("values") ? args["values"] : null);
                    string note = args.ContainsKey("note") ? Json.Str(args, "note") ?? "" : null;
                    string outcome = args.ContainsKey("outcome") ? Json.Str(args, "outcome") ?? "" : null;
                    var match = store.Update((int)Json.Long(args, "id", 0), values, note, outcome, calibration);
                    if (match == null) throw new UserError(Lang.T("Match nicht gefunden.", "Match not found."));
                    PublishStats(true);
                    return MatchStore.ToUi(match);
                }

                case "deleteMatch":
                    if (!store.Delete((int)Json.Long(args, "id", 0))) throw new UserError(Lang.T("Match nicht gefunden.", "Match not found."));
                    PublishStats(true);
                    return null;

                case "exportCsv":
                    return ExportCsv();

                case "openDataFolder":
                    AppInfo.OpenExternal(AppInfo.DataDir);
                    return null;

                case "openLog":
                    AppInfo.OpenExternal(AppInfo.LogFile);
                    return null;

                case "openUrl":
                {
                    string url = Json.Str(args, "url");
                    if (AppInfo.IsWebUri(url)) AppInfo.OpenExternal(url);
                    return null;
                }

                case "copy":
                    CopyText(Json.Str(args, "text") ?? "", null);
                    return null;

                case "saveOverlays":
                    try
                    {
                        overlays.Replace(args.ContainsKey("profiles") ? args["profiles"] : null);
                    }
                    catch (IOException ex)
                    {
                        throw new UserError(Lang.T("Die Overlay-Einstellungen konnten nicht gespeichert werden: ", "The overlay settings could not be saved: ") + ex.Message);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        throw new UserError(Lang.T("Die Overlay-Einstellungen konnten nicht gespeichert werden: ", "The overlay settings could not be saved: ") + ex.Message);
                    }
                    return overlays.ToUi();

                case "setChrome":
                    SetChrome(Json.Str(args, "chrome"));
                    return null;

                default:
                    throw new UserError(Lang.T("Unbekannter Befehl: ", "Unknown command: ") + cmd);
            }
        }

        Dictionary<string, object> InitPayload()
        {
            var app = new Dictionary<string, object>();
            app["name"] = AppInfo.Name;
            app["version"] = AppInfo.Version;
            app["author"] = AppInfo.Author;
            app["authorUrl"] = AppInfo.AuthorUrl;
            app["dataDir"] = AppInfo.DataDir;
            app["dataDirNote"] = AppInfo.DataDirNote;
            app["ocrLanguage"] = ocr != null ? ocr.Language : null;
            app["ocrCyrillic"] = ocr != null ? ocr.CyrillicLanguage : null;
            app["ocrError"] = ocrError;
            app["firstRun"] = firstRun;
            app["steamName"] = steamName;
            app["preset"] = Presets.ToUi(settings);

            var d = new Dictionary<string, object>();
            d["app"] = app;
            d["settings"] = settings.ToUi();
            d["calibration"] = CalibrationPayload();
            d["matches"] = store.Snapshot().Select(MatchStore.ToUi).ToList();
            d["stats"] = Json.Parse(statsJson);
            d["status"] = StatusPayload(engine.Status);
            d["overlays"] = overlays.ToUi();
            return d;
        }

        Dictionary<string, object> CalibrationPayload()
        {
            var d = calibration.ToUi();
            d["referenceUrl"] = ReferenceUrl("summary");
            var team = d["team"] as Dictionary<string, object>;
            if (team != null) team["referenceUrl"] = ReferenceUrl("team");
            var map = d["map"] as Dictionary<string, object>;
            if (map != null) map["referenceUrl"] = ReferenceUrl("map");
            return d;
        }

        // The trail of the match that is running – the live map on the overview.
        Dictionary<string, object> LiveTrailPayload()
        {
            var live = engine.LiveTrail();
            bool any = live.Points.Count > 0;
            var d = new Dictionary<string, object>();
            d["version"] = live.Version;
            d["mapName"] = live.MapName;
            d["started"] = any ? live.Started.ToString("o") : null;
            d["lastSeen"] = any ? live.LastSeen.ToString("o") : null;
            // As in the match view: [x, y, seconds, the map at that moment]. The version keeps a
            // picture of an earlier match with the same number out of the cache.
            d["points"] = live.Points.Select(p => (object)new object[] { p.X, p.Y, p.S,
                string.IsNullOrEmpty(p.Shot) ? null : AppInfo.UiOrigin + "/live-point/" + p.Shot + "?v=" + live.Version }).ToList();
            d["pictureUrl"] = any && live.HasPicture ? AppInfo.UiOrigin + "/live-map.jpg?v=" + live.Version : null;
            return d;
        }

        Dictionary<string, object> StatusPayload(EngineStatus st)
        {
            var d = st.ToUi();
            d["overlayUrl"] = settings.OverlayUrl;
            d["overlayRunning"] = overlayServer != null;
            d["overlayError"] = overlayError;
            d["hotkey"] = settings.Hotkey;
            d["hotkeyError"] = hotkeyError;
            d["calibrated"] = calibration.IsReady;
            var hunt = huntLog.Status;
            d["hunt"] = hunt.ToUi();
            // The match Hunt's log reports was cancelled by hand (no Mission Summary will come).
            d["matchCancelled"] = engine.IsCancelled(hunt);
            return d;
        }

        async Task<object> TestObsAsync(string host, int port, string password)
        {
            using (var client = new ObsClient())
            {
                await client.ConnectAsync(host, port, password, CancellationToken.None);
                var version = await client.RequestAsync("GetVersion", null, 5000);
                var list = await client.RequestAsync("GetInputList", null, 5000);
                var sources = Json.List(list.Data, "inputs").Select(Json.Obj).Where(i => i != null)
                    .Select(i => new { Name = Json.Str(i, "inputName"), Kind = Json.Str(i, "inputKind") ?? "" })
                    .Where(i => i.Name != null && !IsAudioKind(i.Kind))
                    .OrderBy(i => KindRank(i.Kind)).ThenBy(i => i.Name, StringComparer.CurrentCultureIgnoreCase)
                    .Select(i =>
                    {
                        var s = new Dictionary<string, object>();
                        s["name"] = i.Name;
                        s["kind"] = i.Kind;
                        s["kindLabel"] = KindLabel(i.Kind);
                        return s;
                    }).ToList();
                var d = new Dictionary<string, object>();
                d["obsVersion"] = Json.Str(version.Data, "obsVersion");
                d["sources"] = sources;
                return d;
            }
        }

        static bool IsAudioKind(string kind)
        {
            return kind.Contains("wasapi") || kind.Contains("audio") || kind.Contains("pulse") || kind.Contains("alsa") || kind.Contains("jack");
        }

        static int KindRank(string kind)
        {
            switch (kind)
            {
                case "game_capture": return 0;
                case "window_capture": return 1;
                case "monitor_capture": return 2;
                default: return 3;
            }
        }

        // As OBS names the kinds of sources.
        static string KindLabel(string kind)
        {
            switch (kind)
            {
                case "game_capture": return Lang.T("Spielaufnahme", "Game Capture");
                case "window_capture": return Lang.T("Fensteraufnahme", "Window Capture");
                case "monitor_capture": return Lang.T("Bildschirmaufnahme", "Display Capture");
                case "dshow_input": return Lang.T("Videoaufnahmegerät", "Video Capture Device");
                case "browser_source": return "Browser";
                case "image_source": return Lang.T("Bild", "Image");
                case "ffmpeg_source": return Lang.T("Medienquelle", "Media Source");
                default: return kind;
            }
        }

        object SaveCalibration(Dictionary<string, object> args)
        {
            var updated = calibration.Clone();
            updated.ApplyFromUi(args);
            // Saved by the user, it is their own setup from now on.
            updated.Preset = null;
            if (updated.Anchor != null)
            {
                if (!File.Exists(AppInfo.ReferenceImage)) throw new UserError(Lang.T("Bitte zuerst die Mission Summary aufnehmen.", "Please capture the Mission Summary first."));
                ComputeAnchorPrint(updated);
            }
            else
            {
                updated.AnchorPrint = null;
            }
            if (updated.Team.Anchor != null)
            {
                if (!File.Exists(AppInfo.TeamReferenceImage)) throw new UserError(Lang.T("Bitte zuerst die Team Details aufnehmen.", "Please capture the Team Details first."));
                ComputePagePrint(updated.Team, "team");
            }
            else
            {
                updated.Team.AnchorPrint = null;
            }
            // The map is only recognised once it was captured; until then its areas are suggestions.
            if (updated.Map == null) updated.Map = MapPage.CreateDefault();
            if (updated.Map.Anchor != null && File.Exists(AppInfo.MapReferenceImage)) ComputePagePrint(updated.Map, "map");
            else updated.Map.AnchorPrint = null;
            // Still at the places of the ready-made setup: still recognised in each of its languages.
            Presets.AttachLanguagePrints(updated);
            updated.Save();
            calibration = updated;
            Log.Write("Kalibrierung gespeichert: " + updated.Fields.Count(f => f.HasArea) + " Felder, Schwelle " + updated.Threshold + " %");
            engine.Wake();
            PublishStats(true);
            var d = CalibrationPayload();
            if (updated.HasAnchor && updated.AnchorContrast < 8)
                d["warning"] = Lang.T("Der Wiedererkennungs-Bereich ist sehr gleichmäßig. Wähle besser einen Bereich mit Schrift oder klaren Kanten, z. B. die Überschrift „Mission Summary“.",
                    "The recognition area is very even. Better pick an area with lettering or clear edges, e.g. the heading “Mission Summary”.");
            return d;
        }

        void ComputePagePrint(AnchoredPage target, string page)
        {
            using (var reference = Imaging.Load(ReferenceFile(page)))
            {
                Bitmap detect = null;
                try
                {
                    if (File.Exists(ReferenceDetectFile(page))) detect = Imaging.Load(ReferenceDetectFile(page));
                    target.ComputePrint(reference, detect, settings.DetectWidth);
                }
                finally
                {
                    if (detect != null) detect.Dispose();
                }
            }
        }

        void ComputeAnchorPrint(Calibration target)
        {
            using (var reference = Imaging.Load(AppInfo.ReferenceImage))
            {
                Bitmap detect = null;
                try
                {
                    if (File.Exists(AppInfo.ReferenceDetectImage)) detect = Imaging.Load(AppInfo.ReferenceDetectImage);
                    target.ComputeAnchorPrint(reference, detect, settings.DetectWidth);
                }
                finally
                {
                    if (detect != null) detect.Dispose();
                }
            }
        }

        // Calibrations from before PrintVersion 2 compared full-size prints with small frames,
        // which lowers the similarity; recompute them once from the saved reference.
        void UpgradeAnchorPrint()
        {
            if (!calibration.HasAnchor || calibration.PrintVersion >= 2 || !File.Exists(AppInfo.ReferenceImage)) return;
            try
            {
                var upgraded = calibration.Clone();
                ComputeAnchorPrint(upgraded);
                upgraded.Save();
                calibration = upgraded;
                Log.Write("Wiedererkennung an die Erkennungsbilder angepasst");
            }
            catch (Exception ex)
            {
                Log.Write("Wiedererkennung konnte nicht angepasst werden: " + ex.Message);
            }
        }

        // The map in the setup: what HuntXtract finds on the captured picture.
        async Task<object> TestMapAsync(Dictionary<string, object> args)
        {
            if (!File.Exists(AppInfo.MapReferenceImage)) throw new UserError(Lang.T("Bitte zuerst die Karte aufnehmen.", "Please capture the map first."));
            var test = calibration.Clone();
            test.ApplyFromUi(args);
            var map = test.Map;
            if (map.MapArea == null || !map.MapArea.IsSet) throw new UserError(Lang.T("Bitte zuerst den Kartenausschnitt markieren.", "Please mark the map area first."));
            var d = new Dictionary<string, object>();
            using (var reference = Imaging.Load(AppInfo.MapReferenceImage))
            {
                var reading = MapReader.Locate(reference, map, null);
                var name = await MapReader.ReadNameAsync(ocr, reference, map);
                d["unzoomed"] = reading.BorderAround;
                d["edges"] = reading.Edges.Select(e => (object)Math.Round(e * 100)).ToList();
                d["found"] = reading.Found;
                d["x"] = Math.Round(reading.X, 4);
                d["y"] = Math.Round(reading.Y, 4);
                d["candidates"] = reading.Candidates;
                d["nameRaw"] = name[0];
                d["name"] = name[1];
            }
            return d;
        }

        async Task<object> TestOcrAsync(Dictionary<string, object> args)
        {
            if (ocr == null) throw new UserError(ocrError);
            if (Json.Str(args, "page") == "map") return await TestMapAsync(args);
            bool team = Json.Str(args, "page") == "team";
            string file = team ? AppInfo.TeamReferenceImage : AppInfo.ReferenceImage;
            if (!File.Exists(file))
            {
                throw new UserError(team ? Lang.T("Bitte zuerst die Team Details aufnehmen.", "Please capture the Team Details first.")
                    : Lang.T("Bitte zuerst die Mission Summary aufnehmen.", "Please capture the Mission Summary first."));
            }
            var test = calibration.Clone();
            test.ApplyFromUi(args);
            var results = new Dictionary<string, object>();
            using (var reference = Imaging.Load(file))
            {
                var values = new Dictionary<string, object>();
                foreach (var field in (team ? test.Team.Fields : test.Fields).Where(f => f.HasArea && !f.Derived))
                {
                    var reading = await ocr.ReadAsync(reference, field, test);
                    var r = new Dictionary<string, object>();
                    r["raw"] = reading.Raw;
                    r["value"] = reading.Value;
                    results[field.Id] = r;
                    values[field.Id] = reading.Value;
                }
                // Show what HuntXtract makes of it: which row is the player's own.
                if (team)
                {
                    var matched = test.Team.DeriveOwnMmr(values, settings.PlayerName);
                    // The matched row keeps your spelling, so show that in the test as well.
                    if (matched != null && results.ContainsKey(matched.Id))
                    {
                        var row = (Dictionary<string, object>)results[matched.Id];
                        row["value"] = values[matched.Id];
                        row["raw"] = Lang.F("deine Zeile, gelesen als „{0}“", "your row, read as “{0}”", row["raw"]);
                    }
                    foreach (var field in test.Team.Fields.Where(f => f.Derived))
                    {
                        object v;
                        values.TryGetValue(field.Id, out v);
                        var r = new Dictionary<string, object>();
                        r["raw"] = settings.PlayerName.Length == 0 ? Lang.T("trag oben deinen Namen in Hunt ein", "enter your name in Hunt above")
                            : matched != null ? Lang.F("aus der Zeile „{0}“", "from the row “{0}”", matched.Label)
                            : Lang.F("keine Zeile passt zu „{0}“", "no row matches “{0}”", settings.PlayerName);
                        r["value"] = v;
                        results[field.Id] = r;
                    }
                }
            }
            return results;
        }

        object ExportCsv()
        {
            using (var dialog = new SaveFileDialog
            {
                Title = Lang.T("Matches als CSV exportieren", "Export matches as CSV"),
                Filter = Lang.T("CSV-Datei (*.csv)|*.csv", "CSV file (*.csv)|*.csv"),
                FileName = "huntxtract-matches-" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return null;
                store.ExportCsv(dialog.FileName, calibration);
                return dialog.FileName;
            }
        }

        // ---------------------------------------------------------------- matches & stats

        // Called by the capture engine (background thread) after reading a summary.
        Match SaveMatch(PendingMatch pending)
        {
            AddResult result;
            var cal = calibration;
            var match = store.AddIfNew(pending, cal, out result);
            if (result == AddResult.Deleted)
            {
                Log.Write("Übersprungen: gleiche Werte wie ein gelöschtes Match (die Mission Summary ist noch offen)");
                RunOnUi(() =>
                {
                    Notice("info", Lang.T("Diese Mission Summary hattest du gelöscht – sie wird nicht erneut gespeichert.",
                        "You deleted this Mission Summary – it is not saved again."));
                    ShowInGame(Lang.T("Nicht übernommen", "Not taken"), Lang.T("Diese Mission Summary hattest du gelöscht.", "You deleted this Mission Summary."), 4000, -1);
                });
                return null;
            }
            if (result == AddResult.Duplicate)
            {
                Log.Write("Übersprungen: gleiche Werte wie Match #" + match.Id + " (dieselbe Mission Summary nochmal)");
                RunOnUi(() =>
                {
                    Notice("info", Lang.T("Dieses Match ist schon gespeichert – nicht doppelt übernommen.", "This match is already saved – not taken twice."));
                    ShowInGame(Lang.T("Schon gespeichert", "Already saved"),
                        Lang.F("Diese Mission Summary ist bereits Match #{0}.", "This Mission Summary is already match #{0}.", match.Id), 4000, -1);
                });
                return null;
            }
            bool completed = result == AddResult.Completed;
            Log.Write("Match #" + match.Id + (completed ? " ergänzt (dieselbe Mission Summary, jetzt vollständig): " : " gespeichert: ")
                + string.Join(", ", cal.Fields.Where(f => f.HasArea).Select(f => f.Label + "=" + match.Text(f.Id))));
            // One more match: the oldest may be beyond what is kept now.
            if (!completed) RunCleanup(false);
            RefreshStats();
            RunOnUi(() =>
            {
                var d = new Dictionary<string, object>();
                d["match"] = MatchStore.ToUi(match);
                d["completed"] = completed;
                PostEvent("matchSaved", d);
                PublishStats(true);
                if (settings.Sound) Chime.Play();
                ShowInGame(completed ? Lang.F("Match #{0} ergänzt", "Match #{0} completed", match.Id) : Lang.F("Match #{0} gespeichert", "Match #{0} saved", match.Id),
                    pending.Note ?? ValueSummary(match, cal), 4500, 1);
                if (!Visible && !settings.CountdownInGame)
                    tray.ShowBalloonTip(2500, AppInfo.Name, completed ? Lang.T("Match ergänzt.", "Match completed.") : Lang.T("Match gespeichert.", "Match saved."), ToolTipIcon.Info);
            });
            return match;
        }

        // Called by the capture engine after reading the Team Details screen.
        void MergeTeam(TeamReading reading)
        {
            var cal = calibration;
            string reason;
            // By hand it is a deliberate request, so the match may be older than the usual window.
            var match = store.AddTeamValues(reading, cal, reading.Manual ? 24 * 60 : 20, out reason);
            if (match == null)
            {
                Log.Write("Team Details nicht übernommen: " + reason);
                if (reading.Manual) RunOnUi(() =>
                {
                    Notice("info", Lang.F("Team Details nicht übernommen – {0}.", "Team Details not taken – {0}.", reason));
                    ShowInGame(Lang.T("Nicht übernommen", "Not taken"), reason, 4000, -1);
                });
                return;
            }
            var shown = cal.Team.Fields.Where(f => match.Text(f.Id).Length > 0).ToList();
            Log.Write("Team Details zu Match #" + match.Id + ": " + string.Join(", ", shown.Select(f => f.Label + "=" + match.Text(f.Id))));
            RefreshStats();
            RunOnUi(() =>
            {
                PublishStats(true);
                Notice("ok", Lang.F("Team Details zu Match #{0} ergänzt.", "Team Details added to match #{0}.", match.Id));
                ShowInGame(Lang.T("Team Details ergänzt", "Team Details added"), string.Join(" · ", shown.Take(4).Select(f => f.Label + " " + match.Text(f.Id))), 4000, 1);
            });
        }

        // "Extracted · Hunters Killed 2 · Monsters Killed 14 …" for the confirmation in the game.
        static string ValueSummary(Match match, Calibration cal)
        {
            var parts = new List<string>();
            string outcome = match.EffectiveOutcome(cal.StatusField);
            if (outcome == "extrahiert") parts.Add("Extracted");
            else if (outcome == "gestorben") parts.Add("Died");
            foreach (var field in cal.AllFields.Where(f => f.HasArea && f.Type != "status"))
            {
                string text = match.Text(field.Id);
                if (text.Length == 0) continue;
                long number;
                if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out number)) text = number.ToString("N0", Lang.Culture);
                parts.Add(field.Label + " " + text);
            }
            return string.Join(" · ", parts);
        }

        // ---------------------------------------------------------------- countdown in the game

        GameOverlay InGame()
        {
            if (gameOverlay == null || gameOverlay.IsDisposed)
            {
                gameOverlay = new GameOverlay(scale);
                gameOverlay.SetPalette(chrome);
            }
            gameOverlay.Position = settings.CountdownPosition;
            return gameOverlay;
        }

        void ShowInGame(string main, string detail, int hideAfterMs, double bar)
        {
            if (settings.CountdownInGame) InGame().ShowMessage(main, detail, hideAfterMs, bar);
        }

        // Mirrors the capture engine in the small window on top of the game.
        void UpdateGameOverlay(EngineStatus st)
        {
            bool settling = st.Detect == DetectState.Settling, reading = st.Detect == DetectState.Reading;
            if (!settings.CountdownInGame || (!settling && !reading))
            {
                if (gameOverlay != null && gameOverlay.FollowsStatus) gameOverlay.HideNow();
                return;
            }
            if (settling && st.SettleRemainingMs >= 0) InGame().ShowCountdown(st.SettleRemainingMs, st.SettleElapsedMs, st.Counting);
            else InGame().ShowMessage(Lang.T("Lese Werte aus …", "Reading values …"),
                settling ? Lang.T("Der aktuelle Bildschirm wird gelesen", "Reading the current screen") : Lang.T("Gleich fertig", "Almost done"), 0, -1);
        }

        void RefreshStats()
        {
            statsJson = Json.Serialize(Stats.Compute(store.Snapshot(), calibration, settings.DayStartHour, searches.Snapshot()));
        }

        void PublishStats(bool withMatches)
        {
            RefreshStats();
            var d = new Dictionary<string, object>();
            d["stats"] = Json.Parse(statsJson);
            if (withMatches) d["matches"] = store.Snapshot().Select(MatchStore.ToUi).ToList();
            PostEvent("data", d);
        }

        // ---------------------------------------------------------------- settings

        void ApplySettings(Settings old, Settings now)
        {
            bool captureChanged = old.ObsHost != now.ObsHost || old.ObsPort != now.ObsPort || old.Password != now.Password ||
                old.SourceName != now.SourceName || old.AutoDetect != now.AutoDetect || old.DetectFps != now.DetectFps ||
                old.CaptureSource != now.CaptureSource || old.ScreenDevice != now.ScreenDevice;
            if (captureChanged)
            {
                Log.Write("Einstellungen geändert – Bildquelle: " + (now.CaptureSource == "screen" ? ScreenCapture.Label(now.ScreenDevice) : "OBS, Quelle „" + now.SourceName + "“"));
                engine.Restart();
            }
            if (old.Hotkey != now.Hotkey) RegisterHotkey();
            if (old.OverlayEnabled != now.OverlayEnabled || old.OverlayPort != now.OverlayPort || overlayError != null)
            {
                StopOverlayServer();
                StartOverlayServer();
            }
            if (old.DayStartHour != now.DayStartHour) PublishStats(false);
            if (gameOverlay != null)
            {
                gameOverlay.Position = now.CountdownPosition;
                if (!now.CountdownInGame) gameOverlay.HideNow();
            }
            if (old.Language != now.Language) ApplyLanguage(now.Language);
            if (old.PlayerName != now.PlayerName) FillOwnMmr(now.PlayerName);
            if (old.KeepMatches != now.KeepMatches || old.KeepWhat != now.KeepWhat) RunCleanup(true);
            PostEvent("status", StatusPayload(engine.Status));
        }

        // Old matches or their pictures go, as set under "Aufbewahren".
        void RunCleanup(bool publish)
        {
            long bytes;
            int count = store.Cleanup(settings.KeepMatches, settings.KeepWhat, true, out bytes);
            if (count == 0) return;
            Log.Write((settings.KeepWhat == "matches" ? "Aufgeräumt: " + count + " alte Matches gelöscht" : "Aufgeräumt: Bilder von " + count + " alten Matches gelöscht")
                + " (" + Math.Round(bytes / 1048576.0, 1).ToString(CultureInfo.InvariantCulture) + " MB)");
            if (!publish) return;
            RefreshStats();
            PublishStats(true);
        }

        // What the pictures and values of all matches take on disk.
        static long FolderBytes(string dir)
        {
            try
            {
                return Directory.Exists(dir) ? new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;
            }
            catch (Exception)
            {
                return 0;
            }
        }

        // Tray menu, window title, countdown and the names of the Team Details fields follow the
        // language; the capture engine writes its next status line in it too.
        void ApplyLanguage(string language)
        {
            Lang.Set(language);
            Log.Write("Sprache: " + (Lang.English ? "Englisch" : "Deutsch"));
            Text = AppInfo.Name + " – " + AppInfo.Subtitle;
            var oldMenu = tray.ContextMenuStrip;
            tray.ContextMenuStrip = TrayMenu();
            if (oldMenu != null) oldMenu.Dispose();
            if (gameOverlay != null && !gameOverlay.IsDisposed) gameOverlay.Invalidate();
            if (LocalizeFieldLabels()) PublishStats(true);
            engine.Wake();
        }

        // Your own MMR for matches read before your name was set (or while it was spelled
        // differently): the names of the Team Details are kept with each match.
        void FillOwnMmr(string playerName)
        {
            if (calibration.Team == null) return;
            int count = store.FillOwnMmr(calibration.Team, playerName);
            if (count == 0) return;
            Log.Write("Eigene MMR bei " + count + " Matches nachgetragen");
            PublishStats(true);
            Notice("ok", count == 1 ? Lang.T("Eigene MMR bei 1 Match nachgetragen.", "Own MMR filled in for 1 match.")
                : Lang.F("Eigene MMR bei {0} Matches nachgetragen.", "Own MMR filled in for {0} matches.", count));
        }

        void SaveSettings()
        {
            try
            {
                settings.Save();
            }
            catch (Exception ex)
            {
                Log.Write("Einstellungen konnten nicht gespeichert werden: " + ex.Message);
                throw new UserError(Lang.T("Die Einstellungen konnten nicht gespeichert werden: ", "The settings could not be saved: ") + ex.Message);
            }
        }

        void RegisterHotkey()
        {
            hotkeyError = Hotkeys.Register(Handle, HotkeyId, settings.Hotkey);
            if (hotkeyError != null) Log.Write(hotkeyError);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            TitleBar.Apply(Handle, chrome);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Hotkeys.WM_HOTKEY && (int)m.WParam == HotkeyId)
            {
                Log.Write("Hotkey gedrückt – lese aus");
                engine.RequestExtract();
                return;
            }
            // Once a design is chosen, the title bar follows the design instead of Windows.
            if (TitleBar.IsThemeChange(ref m) && chrome == null) TitleBar.Apply(Handle, null);
            base.WndProc(ref m);
        }

        Color WindowBackground
        {
            get { return (chrome ?? WindowChrome.Default).Background; }
        }

        // The UI reports the colours of its design: title bar, window and countdown follow them,
        // and the next start uses them before the UI has loaded.
        void SetChrome(string text)
        {
            var parsed = WindowChrome.Parse(text);
            if (parsed == null) throw new UserError(Lang.T("Ungültige Fensterfarben.", "Invalid window colours."));
            chrome = parsed;
            BackColor = parsed.Background;
            webHost.BackColor = parsed.Background;
            if (view != null) view.DefaultBackgroundColor = parsed.Background;
            if (IsHandleCreated) TitleBar.Apply(Handle, parsed);
            if (gameOverlay != null && !gameOverlay.IsDisposed) gameOverlay.SetPalette(parsed);
            if (settings.Chrome == text) return;
            var updated = settings.Clone();
            updated.Chrome = text;
            settings = updated;
            SaveSettings();
        }

        void StartOverlayServer()
        {
            overlayError = null;
            if (!settings.OverlayEnabled) return;
            var server = new OverlayServer(() => statsJson, () => statusJson, () => Json.Serialize(huntLog.Status.ToUi()), overlays.ConfigJson);
            string error;
            if (server.Start(settings.OverlayPort, out error))
            {
                overlayServer = server;
            }
            else
            {
                overlayError = error;
                Log.Write(error);
            }
        }

        void StopOverlayServer()
        {
            if (overlayServer != null) overlayServer.Dispose();
            overlayServer = null;
        }

        // ---------------------------------------------------------------- helpers

        void Post(Dictionary<string, object> message)
        {
            if (view == null) return;
            try
            {
                view.CoreWebView2.PostWebMessageAsJson(Json.Serialize(message));
            }
            catch (Exception ex)
            {
                Log.Write("Nachricht an die Oberfläche fehlgeschlagen: " + ex.Message);
            }
        }

        void PostEvent(string name, object data)
        {
            var d = new Dictionary<string, object>();
            d["event"] = name;
            d["data"] = data;
            Post(d);
        }

        void Notice(string kind, string text)
        {
            var d = new Dictionary<string, object>();
            d["kind"] = kind;
            d["text"] = text;
            PostEvent("notice", d);
        }

        void CopyText(string text, string balloon)
        {
            try
            {
                Clipboard.SetText(text);
                if (balloon != null) tray.ShowBalloonTip(2000, AppInfo.Name, balloon, ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Log.Write("Zwischenablage nicht verfügbar: " + ex.Message);
            }
        }

        void RunOnUi(Action action)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke(action);
            }
            catch (InvalidOperationException)
            {
            }
        }

        // ---------------------------------------------------------------- window

        void UpdateViewBounds()
        {
            if (view == null || WindowState == FormWindowState.Minimized) return;
            if (webHost.ClientSize.Width <= 0 || webHost.ClientSize.Height <= 0) return;
            view.Bounds = webHost.ClientRectangle;
        }

        protected override void OnMove(EventArgs e)
        {
            base.OnMove(e);
            if (view != null) view.NotifyParentWindowPositionChanged();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            if (view != null) view.MoveFocus(CoreWebView2MoveFocusReason.Programmatic);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized)
            {
                if (settings.MinimizeToTray && Visible)
                {
                    Hide();
                    if (!trayHintShown)
                    {
                        trayHintShown = true;
                        tray.ShowBalloonTip(3000, AppInfo.Name, Lang.T("Läuft im Infobereich weiter und zählt deine Matches. Doppelklick auf das Symbol öffnet das Fenster.",
                            "Keeps running in the notification area and counts your matches. Double-click the icon to open the window."), ToolTipIcon.Info);
                    }
                }
            }
            else
            {
                wasMaximized = WindowState == FormWindowState.Maximized;
            }
        }

        void RestoreWindow()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = wasMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
            Activate();
        }

        void RestoreWindowBounds()
        {
            var s = settings;
            var wanted = new Rectangle(s.WindowX, s.WindowY, s.WindowWidth, s.WindowHeight);
            bool visible = s.WindowWidth >= 400 && s.WindowHeight >= 300 && Screen.AllScreens.Any(screen => screen.WorkingArea.IntersectsWith(wanted));
            if (visible)
            {
                StartPosition = FormStartPosition.Manual;
                Bounds = wanted;
                if (s.WindowMaximized) WindowState = FormWindowState.Maximized;
            }
            else
            {
                var area = Screen.PrimaryScreen.WorkingArea;
                StartPosition = FormStartPosition.CenterScreen;
                Size = new Size(Math.Min((int)(1320 * scale), (int)(area.Width * 0.92)), Math.Min((int)(880 * scale), (int)(area.Height * 0.92)));
            }
            wasMaximized = s.WindowMaximized;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Visible)
            {
                var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
                settings.WindowX = bounds.X;
                settings.WindowY = bounds.Y;
                settings.WindowWidth = bounds.Width;
                settings.WindowHeight = bounds.Height;
                settings.WindowMaximized = WindowState == FormWindowState.Maximized;
            }
            try
            {
                settings.Save();
            }
            catch (Exception ex)
            {
                Log.Write("Einstellungen konnten nicht gespeichert werden: " + ex.Message);
            }
            Log.Write("Programm wird beendet");
            if (showWait != null) showWait.Unregister(null);
            Hotkeys.Unregister(Handle, HotkeyId);
            minuteTimer.Stop();
            huntLog.Stop();
            engine.Stop();
            StopOverlayServer();
            if (gameOverlay != null) gameOverlay.Dispose();
            tray.Visible = false;
            base.OnFormClosing(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                tray.Dispose();
                minuteTimer.Dispose();
                if (view != null)
                {
                    try
                    {
                        view.Close();
                    }
                    catch
                    {
                    }
                }
            }
            base.Dispose(disposing);
        }
    }
}
