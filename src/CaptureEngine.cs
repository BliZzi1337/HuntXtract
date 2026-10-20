using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Kopfgeld
{
    enum ObsState { Connecting, Unreachable, AuthFailed, NoSource, SourceMissing, NoImage, Ready }

    enum DetectState { Off, NotCalibrated, Watching, Paused, Settling, Reading, Cooldown }

    sealed class EngineStatus
    {
        public ObsState Obs = ObsState.Connecting;
        public string ObsText = Lang.T("Starte …", "Starting …");
        public string ObsVersion;
        public string Source;
        public DetectState Detect = DetectState.Off;
        public string DetectText = "";
        public double Similarity = -1;
        public int Threshold;
        public double Fps;
        // While waiting for the Mission Summary to finish: time left until reading (-1 otherwise).
        public int SettleRemainingMs = -1;
        public int SettleElapsedMs;
        public bool Counting;
        // Changes whenever the trail of the running match does (the live map fetches it then).
        public int TrailVersion;
        // Straight from the monitor: why no picture is taken right now ("NotInFront", "NotRunning",
        // "OtherScreen") – only Hunt is ever read. Null while Hunt is to be seen, and with OBS.
        public string HuntHidden;

        public EngineStatus Copy()
        {
            return (EngineStatus)MemberwiseClone();
        }

        public Dictionary<string, object> ToUi()
        {
            var d = new Dictionary<string, object>();
            d["obs"] = Obs.ToString();
            d["obsText"] = ObsText;
            d["obsVersion"] = ObsVersion;
            d["source"] = Source;
            d["detect"] = Detect.ToString();
            d["detectText"] = DetectText;
            d["similarity"] = Similarity < 0 ? null : (object)Math.Round(Similarity, 1);
            d["threshold"] = Threshold;
            d["fps"] = Math.Round(Fps, 1);
            d["settleRemainingMs"] = SettleRemainingMs;
            d["settleElapsedMs"] = SettleElapsedMs;
            d["counting"] = Counting;
            d["trailVersion"] = TrailVersion;
            d["huntHidden"] = HuntHidden;
            return d;
        }
    }

    // Owns the connection to OBS. Pulls small frames of the game capture a few times per
    // second, compares the anchor area with the calibrated end screen and, once it shows,
    // waits until the counting-up values have settled, reads them and hands the match on.
    sealed class CaptureEngine
    {
        const int SettleMinMs = 12000;
        const int SettleQuietMs = 3000;
        const int SettleMaxMs = 40000;
        const double StableChange = 4.0;
        // A field showing something, not just the empty panel (see Imaging.InkContrast).
        const double ContentContrast = 60;
        // Straight from the monitor, with another program in front: how long reading waits for Hunt.
        const int HiddenWaitMs = 60000;
        public const int ReadNowWaitMs = 15000;
        // Back in Hunt: a moment until the game shows its own picture again.
        const int BackInFrontMs = 600;

        readonly Func<Settings> getSettings;
        readonly Func<Calibration> getCalibration;
        // Where Hunt is, from its own log (map of the match).
        readonly Func<HuntStatus> getHunt;
        readonly OcrService ocr;
        readonly Func<PendingMatch, Match> saveMatch;
        readonly Action<TeamReading> mergeTeam;
        readonly SemaphoreSlim wake = new SemaphoreSlim(0, 1);
        readonly SemaphoreSlim extractLock = new SemaphoreSlim(1, 1);
        readonly object sync = new object();
        // Positions from the map (Tab) during the running match.
        readonly TrailRecorder trail = new TrailRecorder();
        // The maps as the game opens them – how a look at the map counts without the red border.
        readonly MapMemory knownMaps = new MapMemory();
        // How much below the threshold the legend next to the map may stay.
        const int MapAnchorSlack = 10;
        // The map at the last look that counted (only the engine loop uses it).
        MapPrint lastLook;
        DateTime lastMapNote;
        CancellationTokenSource runCts;
        CancellationTokenSource sessionCts;
        volatile ObsClient client;
        volatile bool previewEnabled;
        volatile bool calibrating;
        volatile bool extractRequested;
        volatile bool restartRequested;
        // The last screen grab of GrabAsync found no Hunt to read (another program in front …).
        volatile bool screenHidden;
        // Start (Hunt's log) of the match the user cancelled by hand; MinValue: none.
        DateTime cancelledMatch = DateTime.MinValue;
        EngineStatus status = new EngineStatus();
        long lastPublish;

        public event Action<EngineStatus> StatusChanged;
        public event Action<string, double> Preview;
        public event Action<string, string> Notice;

        public CaptureEngine(Func<Settings> getSettings, Func<Calibration> getCalibration, Func<HuntStatus> getHunt, OcrService ocr, Func<PendingMatch, Match> saveMatch, Action<TeamReading> mergeTeam)
        {
            this.getSettings = getSettings;
            this.getCalibration = getCalibration;
            this.getHunt = getHunt;
            this.ocr = ocr;
            this.saveMatch = saveMatch;
            this.mergeTeam = mergeTeam;
            trail.Load();
            knownMaps.Load();
            status.TrailVersion = trail.Version;
        }

        public EngineStatus Status
        {
            get { lock (sync) return status.Copy(); }
        }

        // The trail of the running match and the map at the last look, for the live map.
        public LiveTrail LiveTrail()
        {
            return trail.Live();
        }

        public byte[] LiveMapPicture()
        {
            return trail.Picture;
        }

        // Hunt's log: a match began – its trail starts with it, on its map.
        public void HuntChanged(HuntStatus hunt)
        {
            // Out of the match (menu, loading, Hunt closed): a match cancelled by hand is over for good.
            if (hunt.Phase != HuntPhase.Match) lock (sync) cancelledMatch = DateTime.MinValue;
            if (hunt.Phase == HuntPhase.Match && hunt.MapName != null && !IsCancelled(hunt) && trail.BeginMatch(hunt.MapName, hunt.MatchStarted)) TrailChanged();
        }

        // By hand, when no Mission Summary will come (Hunt crashed, the connection dropped): the trail
        // of the running match is dropped, and while Hunt's log still reports this very match it no
        // longer counts as running. A look at the map in it brings it back; the next match starts as usual.
        public void CancelMatch()
        {
            var hunt = getHunt();
            bool inMatch = hunt.Phase == HuntPhase.Match;
            lock (sync) cancelledMatch = inMatch ? hunt.MatchStarted : DateTime.MinValue;
            int points = trail.Discard();
            Log.Write("Laufendes Match von Hand abgebrochen" + (inMatch && hunt.MapName != null ? " (" + hunt.MapName + ")" : "")
                + (points == 1 ? " – Laufspur mit 1 Punkt verworfen" : " – Laufspur mit " + points + " Punkten verworfen"));
            TrailChanged();
        }

        // Is the match Hunt's log reports right now one the user cancelled? A few seconds' leeway:
        // after reading the log anew its start comes from the log's own clock.
        public bool IsCancelled(HuntStatus hunt)
        {
            lock (sync)
            {
                return hunt.Phase == HuntPhase.Match && cancelledMatch != DateTime.MinValue
                    && Math.Abs((hunt.MatchStarted - cancelledMatch).TotalSeconds) < 5;
            }
        }

        // The map of the match that just ended, from Hunt's log – for a match without a trail (no look
        // at the map). Only if that match began no longer ago than a match lasts; null otherwise
        // (Hunt not running, no log, the shooting range …).
        string MatchMapFromLog()
        {
            var hunt = getHunt();
            if (hunt.MapName == null || hunt.MatchStarted == DateTime.MinValue) return null;
            if (hunt.Phase != HuntPhase.Match && hunt.Phase != HuntPhase.Summary && hunt.Phase != HuntPhase.Menu
                && hunt.Phase != HuntPhase.Searching && hunt.Phase != HuntPhase.Found) return null;
            double minutes = (DateTime.UtcNow - hunt.MatchStarted).TotalMinutes;
            return minutes >= 0 && minutes <= TrailRecorder.LongestMatchMinutes + 15 ? hunt.MapName : null;
        }

        public void Start()
        {
            runCts = new CancellationTokenSource();
            var token = runCts.Token;
            Task.Run(() => RunAsync(token));
        }

        public void Stop()
        {
            if (runCts != null) runCts.Cancel();
            CancelSession();
        }

        public void Restart()
        {
            restartRequested = true;
            CancelSession();
            Wake();
        }

        // While the setup page is open the engine keeps measuring the similarity for the
        // meter there, but must not save matches from the screens used for calibrating.
        public void SetPreview(bool enabled, bool calibrating)
        {
            previewEnabled = enabled;
            this.calibrating = calibrating;
            Wake();
        }

        public void RequestExtract()
        {
            extractRequested = true;
            Wake();
        }

        public void Wake()
        {
            try
            {
                if (wake.CurrentCount == 0) wake.Release();
            }
            catch (SemaphoreFullException)
            {
            }
        }

        void CancelSession()
        {
            var session = sessionCts;
            if (session == null) return;
            try
            {
                session.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        async Task SleepAsync(int ms, CancellationToken token)
        {
            try
            {
                await wake.WaitAsync(ms, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        // ------------------------------------------------------------ commands from the UI

        public async Task<Bitmap> GrabFullFrameAsync()
        {
            var c = client;
            var s = getSettings();
            if (s.CaptureSource == "screen")
            {
                HuntView view;
                var frame = ScreenCapture.Grab(s.ScreenDevice, out view);
                SetHuntView(view);
                if (view == HuntView.Shown) return frame;
                frame.Dispose();
                throw new UserError(view == HuntView.NotRunning
                    ? Lang.T("Hunt läuft nicht – HuntXtract nimmt nur Hunt auf. Starte Hunt und öffne die Seite, die du aufnehmen willst.",
                        "Hunt isn't running – HuntXtract only captures Hunt. Start Hunt and open the page you want to capture.")
                    : view == HuntView.OtherScreen
                    ? Lang.T("Hunt läuft auf einem anderen Bildschirm – wähle in Schritt 1 den Bildschirm, auf dem Hunt läuft.",
                        "Hunt runs on another screen – pick the screen Hunt runs on in step 1.")
                    : Lang.T("Hunt kam nicht in den Vordergrund – HuntXtract nimmt nur Hunt auf. Klick auf „Jetzt aufnehmen“ und wechsle innerhalb von 15 Sekunden ins Spiel.",
                        "Hunt didn't come to the front – HuntXtract only captures Hunt. Click “Capture now” and switch to the game within 15 seconds."));
            }
            if (c == null || !c.IsConnected) throw new ObsException(Lang.T("Keine Verbindung zu OBS", "No connection to OBS"));
            if (s.SourceName.Length == 0) throw new ObsException(Lang.T("Noch keine Quelle gewählt", "No source selected yet"));
            var request = new Dictionary<string, object>();
            request["sourceName"] = s.SourceName;
            request["imageFormat"] = "png";
            ObsResponse shot;
            try
            {
                shot = await c.RequestAsync("GetSourceScreenshot", request, 10000).ConfigureAwait(false);
            }
            catch (ObsRequestException ex)
            {
                throw new ObsException(Friendly(ex, s));
            }
            byte[] bytes = DecodeDataUrl(shot.ImageData);
            if (bytes == null) throw new ObsException(Lang.T("OBS hat kein lesbares Bild geliefert", "OBS delivered no readable picture"));
            return Imaging.Decode(bytes);
        }

        // A frame exactly like the ones detection compares (same size and JPEG quality).
        public async Task<byte[]> GrabDetectFrameAsync()
        {
            var c = client;
            var s = getSettings();
            if (s.CaptureSource == "screen")
            {
                HuntView view;
                byte[] picture = ScreenShot(s, DetectRequest(s), out view);
                SetHuntView(view);
                return picture;
            }
            if (c == null || !c.IsConnected || s.SourceName.Length == 0) return null;
            var shot = await c.RequestAsync("GetSourceScreenshot", DetectRequest(s), 10000).ConfigureAwait(false);
            return DecodeDataUrl(shot.ImageData);
        }

        // The same picture the OBS request would deliver, taken straight from the monitor – null
        // while Hunt isn't to be seen there.
        static byte[] ScreenShot(Settings s, Dictionary<string, object> request, out HuntView view)
        {
            object format, width, quality;
            request.TryGetValue("imageFormat", out format);
            request.TryGetValue("imageWidth", out width);
            request.TryGetValue("imageCompressionQuality", out quality);
            return ScreenCapture.Grab(s.ScreenDevice,
                Convert.ToString(format) == "png" ? "png" : "jpg",
                width == null ? 0 : Convert.ToInt32(width),
                quality == null ? 85 : Convert.ToInt32(quality),
                out view);
        }

        static Dictionary<string, object> DetectRequest(Settings s)
        {
            var request = new Dictionary<string, object>();
            request["sourceName"] = s.SourceName;
            request["imageFormat"] = "jpg";
            request["imageCompressionQuality"] = 80;
            request["imageWidth"] = s.DetectWidth;
            return request;
        }

        public async Task<List<Dictionary<string, object>>> ListInputsAsync()
        {
            var c = client;
            if (c == null || !c.IsConnected) throw new ObsException(Lang.T("Keine Verbindung zu OBS", "No connection to OBS"));
            var list = await c.RequestAsync("GetInputList", null, 5000).ConfigureAwait(false);
            return Json.List(list.Data, "inputs").Select(Json.Obj).Where(d => d != null).ToList();
        }

        // ------------------------------------------------------------ connection loop

        async Task RunAsync(CancellationToken runToken)
        {
            int retryMs = 1000;
            while (!runToken.IsCancellationRequested)
            {
                var s = getSettings();
                // OBS delivers its own source; only reading from the monitor waits for Hunt.
                if (s.CaptureSource != "screen") SetHuntView(HuntView.Shown);
                restartRequested = false;
                sessionCts = CancellationTokenSource.CreateLinkedTokenSource(runToken);
                var token = sessionCts.Token;
                ObsClient c = null;
                string failure = null;
                bool authFailed = false;
                try
                {
                    // Straight from a monitor: no OBS, no connection, the loop just reads frames.
                    if (s.CaptureSource == "screen")
                    {
                        SetObs(ObsState.Ready, ReadyText(s), null, s);
                        failure = await SessionAsync(null, s, null, new TaskCompletionSource<string>().Task, token).ConfigureAwait(false);
                    }
                    else
                    {
                        SetObs(ObsState.Connecting, Lang.F("Verbinde mit OBS ({0}:{1}) …", "Connecting to OBS ({0}:{1}) …", s.ObsHost, s.ObsPort), null, s);
                        c = new ObsClient();
                        var lost = new TaskCompletionSource<string>();
                        c.Disconnected += reason => lost.TrySetResult(reason);
                        await c.ConnectAsync(s.ObsHost, s.ObsPort, s.Password, token).ConfigureAwait(false);
                        var version = await c.RequestAsync("GetVersion", null, 5000).ConfigureAwait(false);
                        string obsVersion = Json.Str(version.Data, "obsVersion");
                        Log.Write("Mit OBS " + obsVersion + " verbunden (obs-websocket " + Json.Str(version.Data, "obsWebSocketVersion") + ")");
                        client = c;
                        retryMs = 1000;
                        failure = await SessionAsync(c, s, obsVersion, lost.Task, token).ConfigureAwait(false);
                    }
                }
                catch (ObsAuthException ex)
                {
                    failure = ex.Message;
                    authFailed = true;
                }
                catch (OperationCanceledException)
                {
                    failure = runToken.IsCancellationRequested || restartRequested ? null : Lang.T("Keine Antwort von OBS", "No answer from OBS");
                }
                catch (Exception ex)
                {
                    failure = Describe(ex);
                }
                client = null;
                if (c != null) c.Dispose();
                sessionCts.Dispose();
                sessionCts = null;
                if (runToken.IsCancellationRequested) break;
                if (restartRequested) continue;

                if (failure != null && s.CaptureSource == "screen")
                {
                    Log.Write("Bildschirmaufnahme: " + failure);
                    SetObs(ObsState.NoImage, failure, null, s);
                }
                else if (failure != null)
                {
                    Log.Write("OBS: " + failure);
                    int waitMs = authFailed ? 10000 : retryMs;
                    SetObs(authFailed ? ObsState.AuthFailed : ObsState.Unreachable,
                        failure + Lang.F(" – neuer Versuch in {0} s", " – trying again in {0} s", waitMs / 1000), null, s);
                    SetDetect(DetectState.Off, Lang.T("Keine Verbindung zu OBS", "No connection to OBS"), -1, 0);
                }
                await SleepAsync(authFailed ? 10000 : retryMs, runToken).ConfigureAwait(false);
                retryMs = Math.Min(retryMs * 2, 5000);
            }
        }

        static string ReadyText(Settings s)
        {
            return s.CaptureSource == "screen"
                ? Lang.T("Bildschirmaufnahme – der Bildschirm, auf dem Hunt läuft", "Screen capture – the screen Hunt runs on")
                : Lang.F("Verbunden – „{0}“ liefert Bild", "Connected – “{0}” delivers a picture", s.SourceName);
        }

        async Task<string> SessionAsync(ObsClient c, Settings s, string obsVersion, Task<string> lost, CancellationToken token)
        {
            if (s.CaptureSource != "screen" && s.SourceName.Length == 0)
            {
                SetObs(ObsState.NoSource, Lang.T("Mit OBS verbunden – wähle in der Einrichtung deine Spielaufnahme",
                    "Connected to OBS – pick your game capture in the setup"), obsVersion, s);
                SetDetect(DetectState.NotCalibrated, Lang.T("Noch keine Quelle gewählt", "No source selected yet"), -1, 0);
                while (!token.IsCancellationRequested && !lost.IsCompleted)
                {
                    if (extractRequested)
                    {
                        extractRequested = false;
                        Notify("warn", Lang.T("Bitte zuerst in der Einrichtung die Spielaufnahme als Quelle wählen.", "Please pick the game capture as the source in the setup first."));
                    }
                    await SleepAsync(1000, token).ConfigureAwait(false);
                }
                return lost.IsCompleted ? lost.Result : null;
            }

            var small = DetectRequest(s);

            var clock = Stopwatch.StartNew();
            var recent = new Queue<long>();
            int hits = 0, teamHits = 0;
            long belowSince = -1, teamBelowSince = -1;
            long lastPreview = -10000;
            long lastProbe = -10000;
            long lastMapRead = -10000;
            int mapMisses = 0;
            bool cooldown = false, teamCooldown = false;

            while (!token.IsCancellationRequested)
            {
                if (lost.IsCompleted) return lost.Result;
                var cal = getCalibration();
                bool detect = s.AutoDetect && cal.IsReady;

                if (extractRequested)
                {
                    extractRequested = false;
                    // Straight from the monitor, "Read now" outside the game waits until Hunt is in front.
                    if (s.CaptureSource == "screen" && !await WaitForHuntAsync(s, token).ConfigureAwait(false)) continue;
                    // Read what is on screen right now: the Mission Summary, or the Team Details
                    // if that page is showing.
                    if (await OnTeamPageAsync(c, s, cal, obsVersion).ConfigureAwait(false))
                    {
                        teamCooldown = await ReadTeamAsync(c, s, cal, true, token).ConfigureAwait(false);
                        teamBelowSince = -1;
                        teamHits = 0;
                    }
                    else if (await ExtractAsync(c, s, cal, true, token).ConfigureAwait(false))
                    {
                        cooldown = detect;
                    }
                    belowSince = -1;
                    hits = 0;
                    continue;
                }

                if (!detect)
                {
                    cooldown = false;
                    teamCooldown = false;
                    hits = 0;
                    teamHits = 0;
                    if (cal.IsReady)
                        SetDetect(DetectState.Off, Lang.T("Automatische Erkennung ist aus – „Jetzt auslesen“ oder Hotkey nutzen",
                            "Automatic detection is off – use “Read now” or the hotkey"), -1, cal.Threshold);
                    else
                        SetDetect(DetectState.NotCalibrated, Lang.T("Noch nicht eingerichtet – bitte die Einrichtung abschließen",
                            "Not set up yet – please finish the setup"), -1, cal.Threshold);
                    if (!previewEnabled)
                    {
                        if (clock.ElapsedMilliseconds - lastProbe > 3000)
                        {
                            lastProbe = clock.ElapsedMilliseconds;
                            await ProbeAsync(c, s, obsVersion).ConfigureAwait(false);
                        }
                        await SleepAsync(500, token).ConfigureAwait(false);
                        continue;
                    }
                }

                // Straight from the monitor, nothing is read while Hunt isn't the program in front.
                // A Mission Summary saved just before may still be open behind: its cooldown stays.
                if (s.CaptureSource == "screen")
                {
                    var view = ScreenCapture.View(s.ScreenDevice);
                    SetHuntView(view);
                    if (view != HuntView.Shown)
                    {
                        hits = 0;
                        teamHits = 0;
                        mapMisses = 0;
                        belowSince = -1;
                        teamBelowSince = -1;
                        if (detect && !calibrating && !cooldown) SetDetect(DetectState.Watching, WatchingText(), -1, cal.Threshold);
                        await SleepAsync(500, token).ConfigureAwait(false);
                        continue;
                    }
                }

                long started = clock.ElapsedMilliseconds;
                byte[] jpeg = await GrabAsync(c, small, s, obsVersion, 5000).ConfigureAwait(false);
                if (jpeg == null)
                {
                    hits = 0;
                    await SleepAsync(1000, token).ConfigureAwait(false);
                    continue;
                }
                SetObs(ObsState.Ready, ReadyText(s), obsVersion, s);
                recent.Enqueue(started);
                while (recent.Count > 0 && started - recent.Peek() > 3000) recent.Dequeue();
                double fps = recent.Count > 1 ? (recent.Count - 1) * 1000.0 / Math.Max(1, started - recent.Peek()) : 0;
                lock (sync) status.Fps = fps;

                double sim = -1, teamSim = -1, mapSim = -1;
                bool teamPage = cal.Team != null && cal.Team.IsReady;
                var mapPage = cal.Map != null && cal.Map.IsReady ? cal.Map : null;
                if (detect)
                {
                    using (var frame = Imaging.Decode(jpeg))
                    {
                        sim = cal.Similarity(frame);
                        if (teamPage) teamSim = cal.Team.Similarity(frame);
                        if (mapPage != null) mapSim = mapPage.Similarity(frame);
                    }
                    if (calibrating)
                    {
                        hits = 0;
                        teamHits = 0;
                        cooldown = false;
                        teamCooldown = false;
                        SetDetect(DetectState.Paused, Lang.T("Einrichtung ist offen – solange werden keine Matches gespeichert",
                            "The setup is open – no matches are saved meanwhile"), sim, cal.Threshold);
                    }
                    else if (!cooldown)
                    {
                        hits = sim >= cal.Threshold ? hits + 1 : 0;
                        SetDetect(DetectState.Watching, WatchingText(), sim, cal.Threshold);
                        if (hits >= 2)
                        {
                            hits = 0;
                            cooldown = await ExtractAsync(c, s, cal, false, token).ConfigureAwait(false);
                            belowSince = -1;
                            continue;
                        }
                    }
                    else
                    {
                        SetDetect(DetectState.Cooldown, Lang.T("Match gespeichert – wartet, bis die Mission Summary geschlossen ist",
                            "Match saved – waiting until the Mission Summary is closed"), sim, cal.Threshold);
                        if (sim < cal.Threshold - 5)
                        {
                            if (belowSince < 0) belowSince = clock.ElapsedMilliseconds;
                            else if (clock.ElapsedMilliseconds - belowSince > 2500) cooldown = false;
                        }
                        else
                        {
                            belowSince = -1;
                        }
                    }

                    // The Team Details tab of the same match: read once per visit and add to the match.
                    if (!calibrating && teamPage)
                    {
                        if (!teamCooldown)
                        {
                            teamHits = teamSim >= cal.Threshold ? teamHits + 1 : 0;
                            if (teamHits >= 2)
                            {
                                teamHits = 0;
                                teamCooldown = await ReadTeamAsync(c, s, cal, false, token).ConfigureAwait(false);
                                teamBelowSince = -1;
                                continue;
                            }
                        }
                        else if (teamSim < cal.Threshold - 5)
                        {
                            if (teamBelowSince < 0) teamBelowSince = clock.ElapsedMilliseconds;
                            else if (clock.ElapsedMilliseconds - teamBelowSince > 2500) teamCooldown = false;
                        }
                        else
                        {
                            teamBelowSince = -1;
                        }
                    }

                    // The map on Tab: where the player is, about once a second. The legend is
                    // see-through – the game behind it lowers the similarity –, so a little less
                    // is enough here; whether the map is really open, its own picture decides.
                    // A look that did not count (the map still fading in, say) is tried again
                    // soon, so a quick glance counts too.
                    if (!calibrating && mapPage != null && mapSim >= cal.Threshold - MapAnchorSlack)
                    {
                        if (clock.ElapsedMilliseconds - lastMapRead >= (mapMisses > 0 && mapMisses <= 2 ? 300 : 1000))
                        {
                            lastMapRead = clock.ElapsedMilliseconds;
                            mapMisses = await ReadMapAsync(c, s, mapPage, obsVersion).ConfigureAwait(false) ? 0 : mapMisses + 1;
                        }
                    }
                    else
                    {
                        mapMisses = 0;
                    }
                }

                if (previewEnabled && clock.ElapsedMilliseconds - lastPreview >= 450)
                {
                    lastPreview = clock.ElapsedMilliseconds;
                    var handler = Preview;
                    if (handler != null) handler(Imaging.DataUrl(jpeg), sim);
                }

                int wait = 1000 / s.DetectFps - (int)(clock.ElapsedMilliseconds - started);
                if (wait > 0) await SleepAsync(wait, token).ConfigureAwait(false);
            }
            return null;
        }

        // Is the Team Details tab the page on screen right now? Used for reading by hand.
        async Task<bool> OnTeamPageAsync(ObsClient c, Settings s, Calibration cal, string obsVersion)
        {
            if (cal.Team == null || !cal.Team.IsReady) return false;
            byte[] jpeg = await GrabAsync(c, DetectRequest(s), s, obsVersion, 5000).ConfigureAwait(false);
            if (jpeg == null) return false;
            using (var frame = Imaging.Decode(jpeg))
            {
                double team = cal.Team.Similarity(frame);
                double summary = cal.HasAnchor ? cal.Similarity(frame) : -1;
                return team >= cal.Threshold && team > summary;
            }
        }

        // Reads the Team Details tab and hands the values over to be added to the last match.
        // Returns true once the page has been read, so it is not read again while it stays open.
        async Task<bool> ReadTeamAsync(ObsClient c, Settings s, Calibration cal, bool manual, CancellationToken token)
        {
            if (ocr == null) return true;
            await extractLock.WaitAsync(token).ConfigureAwait(false);
            Bitmap frame = null;
            try
            {
                SetDetect(DetectState.Reading, Lang.T("Team Details erkannt – lese Namen und MMR …", "Team Details detected – reading names and MMR …"), -1, cal.Threshold);
                var request = new Dictionary<string, object>();
                request["sourceName"] = s.SourceName;
                request["imageFormat"] = "png";
                var fields = cal.Team.Fields.Where(f => f.HasArea && !f.Derived).ToList();
                float[][] previous = null;
                var clock = Stopwatch.StartNew();

                // The page builds up for a moment, so read it once nothing changes any more.
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    byte[] bytes = await GrabAsync(c, request, s, null, 10000).ConfigureAwait(false);
                    if (bytes == null)
                    {
                        // Hunt went out of front before anything was read: read it once it is back.
                        if (frame == null) return !screenHidden;
                        break;
                    }
                    var bmp = Imaging.Decode(bytes);
                    var prints = fields.Select(f => Imaging.GraySamples(bmp, f.Area, 48, 16)).ToArray();
                    bool changed = previous == null || prints.Select((p, i) => Imaging.Change(p, previous[i])).Any(change => change >= StableChange);
                    if (frame != null) frame.Dispose();
                    frame = bmp;
                    previous = prints;
                    if (!changed || clock.ElapsedMilliseconds > 6000) break;
                    await Task.Delay(700, token).ConfigureAwait(false);
                }

                var values = new Dictionary<string, object>();
                var raw = new Dictionary<string, string>();
                foreach (var field in fields)
                {
                    var reading = await ocr.ReadAsync(frame, field, cal).ConfigureAwait(false);
                    values[field.Id] = reading.Value;
                    raw[field.Id] = reading.Raw;
                }
                cal.Team.DeriveOwnMmr(values, s.PlayerName);
                byte[] screenshot = s.SaveScreenshots ? Imaging.EncodeJpeg(frame, 85, s.ScreenshotWidth) : null;
                var handler = mergeTeam;
                if (handler != null) handler(new TeamReading { Values = values, Raw = raw, Manual = manual, ScreenshotJpeg = screenshot });
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Write("Team Details konnten nicht gelesen werden: " + ex);
                return true;
            }
            finally
            {
                if (frame != null) frame.Dispose();
                extractLock.Release();
            }
        }

        // Returns true if a match was saved (or recognised as already saved).
        async Task<bool> ExtractAsync(ObsClient c, Settings s, Calibration cal, bool manual, CancellationToken token)
        {
            if (!cal.HasFields)
            {
                Notify("warn", Lang.T("Noch keine Felder markiert – bitte zuerst die Einrichtung abschließen.", "No fields marked yet – please finish the setup first."));
                return false;
            }
            if (ocr == null)
            {
                Notify("error", Lang.T("Die Windows-Texterkennung ist nicht verfügbar.", "Windows text recognition is not available."));
                return false;
            }

            await extractLock.WaitAsync(token).ConfigureAwait(false);
            Bitmap frame = null;
            string note = null;
            try
            {
                lock (sync)
                {
                    status.SettleElapsedMs = 0;
                    status.SettleRemainingMs = manual ? -1 : SettleMinMs;
                    status.Counting = !manual;
                }
                string settleText = manual ? Lang.T("Lese den aktuellen Bildschirm aus …", "Reading the current screen …")
                    : Lang.T("Mission Summary erkannt – warte, bis alle Punkte vergeben sind …", "Mission Summary detected – waiting until all points are awarded …");
                SetDetect(DetectState.Settling, settleText, -1, cal.Threshold);
                var full = new Dictionary<string, object>();
                full["sourceName"] = s.SourceName;
                full["imageFormat"] = "png";
                var fields = cal.Fields.Where(f => f.HasArea).ToList();
                var lastRow = LastRow(fields);
                float[][] previous = null;
                var clock = Stopwatch.StartNew();
                long lastChange = 0;
                long hiddenSince = -1;

                // Hunt builds the Mission Summary up and hands out the points over ~15 s, with
                // pauses between the steps. Read once at least SettleMinMs have passed and no
                // value has changed for SettleQuietMs. If the summary is closed earlier, read
                // the last frame it was still visible on.
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    byte[] bytes = await GrabAsync(c, full, s, null, 10000).ConfigureAwait(false);
                    // Straight from the monitor with another program in front: nothing to see, but
                    // the Mission Summary may still be open behind it – wait for Hunt a while.
                    if (bytes == null && screenHidden)
                    {
                        if (hiddenSince < 0) hiddenSince = clock.ElapsedMilliseconds;
                        if (clock.ElapsedMilliseconds - hiddenSince < HiddenWaitMs)
                        {
                            SetDetect(DetectState.Settling, Lang.T("Wartet, bis Hunt wieder im Vordergrund ist …", "Waiting until Hunt is in front again …"), -1, cal.Threshold);
                            await Task.Delay(400, token).ConfigureAwait(false);
                            continue;
                        }
                        if (frame == null)
                        {
                            Notify("warn", Lang.T("Nicht ausgelesen – Hunt war nicht im Vordergrund.", "Not read – Hunt wasn't in front."));
                            return false;
                        }
                        note = Lang.T("Hunt war zwischendurch nicht im Vordergrund – bitte Werte prüfen.", "Hunt wasn't in front for a while – please check the values.");
                        break;
                    }
                    if (bytes != null && hiddenSince >= 0)
                    {
                        // Back in Hunt: the first picture after the switch may still show the other program.
                        hiddenSince = -1;
                        SetDetect(DetectState.Settling, settleText, -1, cal.Threshold);
                        await Task.Delay(BackInFrontMs, token).ConfigureAwait(false);
                        continue;
                    }
                    if (bytes == null)
                    {
                        if (frame == null)
                        {
                            Notify("warn", Lang.T("Auslesen abgebrochen: OBS liefert gerade kein Bild.", "Reading stopped: OBS delivers no picture right now."));
                            return false;
                        }
                        note = Lang.T("OBS lieferte zwischendurch kein Bild – bitte Werte prüfen.", "OBS delivered no picture for a moment – please check the values.");
                        break;
                    }
                    var bmp = Imaging.Decode(bytes);
                    if (manual)
                    {
                        frame = bmp;
                        break;
                    }

                    double sim = cal.Similarity(bmp);
                    long elapsed = clock.ElapsedMilliseconds;
                    if (sim < cal.Threshold - 10)
                    {
                        bmp.Dispose();
                        if (frame == null)
                        {
                            SetDetect(DetectState.Watching, WatchingText(), sim, cal.Threshold);
                            return false;
                        }
                        if (elapsed < SettleMinMs || elapsed - lastChange < SettleQuietMs)
                        {
                            note = Lang.T("Die Mission Summary wurde geschlossen, bevor alle Punkte vergeben waren – bitte Werte prüfen.",
                                "The Mission Summary was closed before all points were awarded – please check the values.");
                            Log.Write("Mission Summary nach " + (elapsed / 1000) + " s geschlossen – lese den letzten sichtbaren Stand");
                        }
                        break;
                    }

                    var prints = fields.Select(f => Imaging.GraySamples(bmp, f.Area, 48, 16)).ToArray();
                    bool changed = previous == null || prints.Select((p, i) => Imaging.Change(p, previous[i])).Any(change => change >= StableChange);
                    if (changed) lastChange = elapsed;
                    if (frame != null) frame.Dispose();
                    frame = bmp;
                    previous = prints;
                    // Hunt fills in the bottom row (Total Rewards) last, sometimes after a pause of
                    // several seconds: until a field there shows something, the summary isn't done.
                    bool lastRowShown = lastRow.Count == 0 || lastRow.Any(i => Imaging.InkContrast(prints[i], 48, 16) >= ContentContrast);
                    long remaining = Math.Min(Math.Max(SettleMinMs - elapsed, SettleQuietMs - (elapsed - lastChange)), SettleMaxMs - elapsed);
                    if (!lastRowShown) remaining = Math.Max(remaining, Math.Min(SettleQuietMs, SettleMaxMs - elapsed));
                    SetSettle(elapsed, remaining, elapsed - lastChange < 1500);

                    if (elapsed >= SettleMinMs && elapsed - lastChange >= SettleQuietMs && lastRowShown) break;
                    if (elapsed >= SettleMaxMs)
                    {
                        Log.Write("Werte sind nicht zur Ruhe gekommen – lese nach " + (SettleMaxMs / 1000) + " s trotzdem aus");
                        break;
                    }
                    await Task.Delay(500, token).ConfigureAwait(false);
                }

                SetDetect(DetectState.Reading, Lang.T("Lese Werte aus …", "Reading values …"), -1, cal.Threshold);
                var values = new Dictionary<string, object>();
                var raw = new Dictionary<string, string>();
                foreach (var field in fields)
                {
                    var reading = await ocr.ReadAsync(frame, field, cal).ConfigureAwait(false);
                    values[field.Id] = reading.Value;
                    raw[field.Id] = reading.Raw;
                }
                byte[] screenshot = s.SaveScreenshots ? Imaging.EncodeJpeg(frame, 85, s.ScreenshotWidth) : null;
                // The trail from the map belongs to this match; the next one starts a new trail.
                var matchTrail = trail.Take(DateTime.UtcNow);
                TrailChanged();
                if (matchTrail != null && !s.SaveScreenshots)
                {
                    matchTrail.MapPicture = null;
                    matchTrail.PointPictures = null;
                }
                saveMatch(new PendingMatch { Values = values, Raw = raw, Manual = manual, ScreenshotJpeg = screenshot, Note = note, Trail = matchTrail, MapName = MatchMapFromLog() });
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (ObsException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Write("Auslesen fehlgeschlagen: " + ex);
                Notify("error", Lang.T("Auslesen fehlgeschlagen: ", "Reading failed: ") + ex.Message);
                return false;
            }
            finally
            {
                if (frame != null) frame.Dispose();
                extractLock.Release();
            }
        }

        // The fields of the Mission Summary's bottom row (Total Rewards in Hunt's layout), as
        // indexes into fields. Those only a surviving hunter has, and the status word, don't count.
        static List<int> LastRow(List<FieldDef> fields)
        {
            var candidates = fields.Select((f, i) => new { f, i }).Where(x => !x.f.SurvivorOnly && x.f.Type != "status").ToList();
            if (candidates.Count == 0) return new List<int>();
            double bottom = candidates.Max(x => x.f.Area.Y + x.f.Area.H / 2);
            return candidates.Where(x => x.f.Area.Y + x.f.Area.H / 2 >= bottom - 0.03).Select(x => x.i).ToList();
        }

        async Task<byte[]> GrabAsync(ObsClient c, Dictionary<string, object> request, Settings s, string obsVersion, int timeoutMs)
        {
            if (s.CaptureSource == "screen")
            {
                try
                {
                    HuntView view;
                    byte[] picture = ScreenShot(s, request, out view);
                    screenHidden = view != HuntView.Shown;
                    SetHuntView(view);
                    return picture;
                }
                catch (Exception ex)
                {
                    screenHidden = false;
                    SetObs(ObsState.NoImage, Lang.T("Der Bildschirm lässt sich nicht lesen: ", "The screen cannot be read: ") + ex.Message, null, s);
                    return null;
                }
            }
            ObsResponse shot = null;
            try
            {
                shot = await c.RequestAsync("GetSourceScreenshot", request, timeoutMs).ConfigureAwait(false);
            }
            catch (ObsRequestException ex)
            {
                ReportSourceProblem(ex, s, obsVersion);
            }
            if (shot == null) return null;
            byte[] bytes = DecodeDataUrl(shot.ImageData);
            if (bytes == null) SetObs(ObsState.NoImage, Lang.T("OBS hat ein unlesbares Bild geliefert", "OBS delivered an unreadable picture"), obsVersion, s);
            return bytes;
        }

        // "Read now" from outside the game (button, tray): straight from the monitor only Hunt is
        // read, so wait a while until it is the program in front. The hotkey in the game needs no wait.
        async Task<bool> WaitForHuntAsync(Settings s, CancellationToken token)
        {
            var view = ScreenCapture.View(s.ScreenDevice);
            SetHuntView(view);
            if (view == HuntView.Shown) return true;
            if (view == HuntView.NotRunning)
            {
                Notify("warn", Lang.T("Nicht ausgelesen – Hunt läuft nicht.", "Not read – Hunt isn't running."));
                return false;
            }
            if (view == HuntView.OtherScreen)
            {
                Notify("warn", Lang.T("Nicht ausgelesen – Hunt läuft auf einem anderen Bildschirm als dem in der Einrichtung gewählten.",
                    "Not read – Hunt runs on another screen than the one picked in the setup."));
                return false;
            }
            Notify("info", Lang.T("Wechsle zu Hunt – ausgelesen wird, sobald Hunt im Vordergrund ist.", "Switch to Hunt – it is read as soon as Hunt is in front."));
            if (await WaitUntilHuntShownAsync(ReadNowWaitMs, token).ConfigureAwait(false)) return true;
            Notify("warn", Lang.T("Nicht ausgelesen – Hunt kam nicht in den Vordergrund.", "Not read – Hunt didn't come to the front."));
            return false;
        }

        // Straight from the monitor: waits up to ms until Hunt is the program in front (then a moment
        // more, since the first picture after the switch may still show the other program).
        public async Task<bool> WaitUntilHuntShownAsync(int ms, CancellationToken token)
        {
            var s = getSettings();
            var clock = Stopwatch.StartNew();
            bool waited = false;
            while (true)
            {
                var view = ScreenCapture.View(s.ScreenDevice);
                SetHuntView(view);
                if (view == HuntView.Shown)
                {
                    if (waited) await Task.Delay(BackInFrontMs, token).ConfigureAwait(false);
                    return true;
                }
                if (view != HuntView.NotInFront || clock.ElapsedMilliseconds >= ms) return false;
                waited = true;
                await Task.Delay(250, token).ConfigureAwait(false);
            }
        }

        // Straight from the monitor: whether Hunt is to be seen, or why not – for the status bar.
        void SetHuntView(HuntView view)
        {
            string hidden = view == HuntView.Shown ? null : view.ToString();
            bool changed;
            lock (sync)
            {
                changed = status.HuntHidden != hidden;
                status.HuntHidden = hidden;
            }
            if (changed) Publish(true);
        }

        // ------------------------------------------------------------ map and trail

        // Status line while waiting, with the trail collected so far.
        string WatchingText()
        {
            int points = trail.Count;
            string trailText = points == 0 ? ""
                : points == 1 ? Lang.T(" · Laufspur: 1 Punkt", " · trail: 1 point")
                : Lang.F(" · Laufspur: {0} Punkte", " · trail: {0} points", points);
            return Lang.T("Wartet auf die Mission Summary", "Waiting for the Mission Summary") + trailText;
        }

        // The map is open: note where the player's marker is. Only the map as the game opens it
        // counts – zoomed in, the positions would not fit the others. That shows by the red border
        // around it (start of a round), by matching a map remembered from such a look, or by matching
        // the last look that counted – since that one only clues and markers have changed, while the
        // round changes the map a lot (clue shading, the boss lair). Returns whether the look counted.
        async Task<bool> ReadMapAsync(ObsClient c, Settings s, MapPage page, string obsVersion)
        {
            Bitmap frame = null;
            try
            {
                frame = await GrabMapFrameAsync(c, s, obsVersion).ConfigureAwait(false);
                if (frame == null) return false;
                var reading = MapReader.Locate(frame, page, trail.Last);
                double match;
                var known = knownMaps.Best(reading.Print, out match);
                double sinceLastLook = lastLook != null ? lastLook.Similarity(reading.Print) : 0;
                bool unzoomed = reading.BorderAround || match >= MapPrint.SameMap || sinceLastLook >= MapPrint.SameMap;
                string problem = reading.Covered
                    ? "Karte verdeckt oder abgedunkelt – Steam-Overlay offen? (Steam-Farben " + Math.Round(reading.SteamShare, 1) + " %, Papierrand " + Math.Round(reading.PaperLuma) + ") – keine Position"
                    : !unzoomed
                    ? "Karte erkannt, aber ohne roten Rand außen herum und keinem gemerkten Kartenbild ähnlich (Rand " + reading.EdgeText
                        + ", Kartenbild " + (known != null ? Math.Round(match * 100) + " %" : "keins")
                        + ", letzter Blick " + (lastLook != null ? Math.Round(sinceLastLook * 100) + " %" : "keiner") + ") – gezoomt oder Karte noch nicht gemerkt, keine Position"
                    : !reading.Found ? "Karte erkannt, aber kein gelber Punkt gefunden" : null;
                if (problem != null)
                {
                    // Worth knowing when the trail stays empty, but not every second – with the
                    // picture that was turned down.
                    if ((DateTime.UtcNow - lastMapNote).TotalMinutes >= 1)
                    {
                        lastMapNote = DateTime.UtcNow;
                        try
                        {
                            File.WriteAllBytes(AppInfo.RejectedMapFile, MapReader.Picture(frame, page));
                        }
                        catch (Exception ex)
                        {
                            Log.Write("Verworfenes Kartenbild konnte nicht gespeichert werden: " + ex.Message);
                        }
                        Log.Write(problem);
                    }
                    return false;
                }
                var now = DateTime.UtcNow;
                bool sameAsKnown = known != null && match >= MapPrint.SameMap;
                // The name of the map: for the match, and a different one means a new match. Hunt's
                // own log knows it for sure; else it is read now and then – and right away for a map
                // that is about to be remembered.
                var hunt = getHunt();
                string logMap = hunt.Phase == HuntPhase.Match || hunt.Phase == HuntPhase.Loading ? hunt.MapName : null;
                bool readName = logMap == null && (trail.NeedsName(now) || (reading.BorderAround && !sameAsKnown));
                string name = logMap ?? (readName ? (await MapReader.ReadNameAsync(ocr, frame, page).ConfigureAwait(false))[1] : null);
                // In between (or unreadable), the remembered map tells which one it is.
                if (name == null && sameAsKnown) name = known.Name;
                if (reading.BorderAround) knownMaps.Remember(reading.Print, name, now);
                else if (sameAsKnown) knownMaps.Name(known, name);
                if (trail.StartsNew(name, now)) Log.Write("Laufspur beginnt" + (name != null ? " auf " + name : ""));
                // With screenshots on, every new point keeps the map of its moment (teammates and all).
                Func<byte[]> pointPicture = null;
                if (s.SaveScreenshots) pointPicture = () => MapReader.PointPicture(frame, page);
                trail.Add(reading.X, reading.Y, name, MapReader.Picture(frame, page), pointPicture, now, readName);
                lastLook = reading.Print;
                // A look at the map in a match cancelled by hand: it is played on after all.
                bool resumed;
                lock (sync)
                {
                    resumed = cancelledMatch != DateTime.MinValue;
                    cancelledMatch = DateTime.MinValue;
                }
                if (resumed) Log.Write("Blick auf die Karte – das abgebrochene Match wird doch weiter verfolgt");
                TrailChanged();
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Log.Write("Karte konnte nicht gelesen werden: " + ex.Message);
                return false;
            }
            finally
            {
                if (frame != null) frame.Dispose();
            }
        }

        // The trail changed: tell the UI, whose live map then fetches it.
        void TrailChanged()
        {
            lock (sync) status.TrailVersion = trail.Version;
            Publish(true);
        }

        // A full-size frame for the map: straight from the monitor, or a good JPEG from OBS.
        async Task<Bitmap> GrabMapFrameAsync(ObsClient c, Settings s, string obsVersion)
        {
            if (s.CaptureSource == "screen")
            {
                HuntView view;
                var frame = ScreenCapture.Grab(s.ScreenDevice, out view);
                SetHuntView(view);
                if (view == HuntView.Shown) return frame;
                frame.Dispose();
                return null;
            }
            var request = new Dictionary<string, object>();
            request["sourceName"] = s.SourceName;
            request["imageFormat"] = "jpg";
            request["imageCompressionQuality"] = 92;
            byte[] bytes = await GrabAsync(c, request, s, obsVersion, 5000).ConfigureAwait(false);
            return bytes == null ? null : Imaging.Decode(bytes);
        }

        async Task ProbeAsync(ObsClient c, Settings s, string obsVersion)
        {
            var probe = new Dictionary<string, object>();
            probe["sourceName"] = s.SourceName;
            probe["imageFormat"] = "jpg";
            probe["imageWidth"] = 64;
            if (await GrabAsync(c, probe, s, obsVersion, 5000).ConfigureAwait(false) != null)
                SetObs(ObsState.Ready, ReadyText(s), obsVersion, s);
        }

        static byte[] DecodeDataUrl(string dataUrl)
        {
            if (string.IsNullOrEmpty(dataUrl)) return null;
            int comma = dataUrl.IndexOf(',');
            if (comma < 0) return null;
            try
            {
                return Convert.FromBase64String(dataUrl.Substring(comma + 1));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        // ------------------------------------------------------------ status

        void ReportSourceProblem(ObsRequestException ex, Settings s, string obsVersion)
        {
            SetObs(ex.Code == 600 ? ObsState.SourceMissing : ObsState.NoImage, Friendly(ex, s), obsVersion ?? Status.ObsVersion, s);
        }

        static string Friendly(ObsRequestException ex, Settings s)
        {
            // 600 = ResourceNotFound, 702 = RequestProcessingFailed ("Failed to render screenshot")
            if (ex.Code == 600)
                return Lang.F("Quelle „{0}“ gibt es in OBS nicht – bitte in der Einrichtung prüfen", "OBS has no source “{0}” – please check the setup", s.SourceName);
            if (ex.Code == 702)
                return Lang.F("„{0}“ liefert kein Bild – läuft Hunt und ist die Quelle in der aktiven Szene sichtbar?",
                    "“{0}” delivers no picture – is Hunt running and is the source visible in the active scene?", s.SourceName);
            return Lang.F("„{0}“: ", "“{0}”: ", s.SourceName) + ex.Message;
        }

        void SetObs(ObsState state, string text, string obsVersion, Settings s)
        {
            bool changed;
            lock (sync)
            {
                changed = status.Obs != state || status.ObsText != text;
                status.Obs = state;
                status.ObsText = text;
                if (obsVersion != null || state == ObsState.Connecting || state == ObsState.Unreachable || state == ObsState.AuthFailed)
                    status.ObsVersion = obsVersion;
                status.Source = s.SourceName;
            }
            if (changed) Publish(true);
        }

        void SetDetect(DetectState state, string text, double similarity, int threshold)
        {
            bool changed;
            lock (sync)
            {
                changed = status.Detect != state || status.DetectText != text;
                status.Detect = state;
                status.DetectText = text;
                status.Similarity = similarity;
                status.Threshold = threshold;
                if (state != DetectState.Settling) status.SettleRemainingMs = -1;
            }
            Publish(changed);
        }

        void SetSettle(long elapsed, long remaining, bool counting)
        {
            lock (sync)
            {
                status.SettleElapsedMs = (int)elapsed;
                status.SettleRemainingMs = (int)Math.Max(0, remaining);
                status.Counting = counting;
            }
            Publish(true);
        }

        void Publish(bool force)
        {
            long now = Environment.TickCount;
            if (!force && now - Interlocked.Read(ref lastPublish) < 500) return;
            Interlocked.Exchange(ref lastPublish, now);
            var handler = StatusChanged;
            if (handler != null) handler(Status);
        }

        void Notify(string kind, string text)
        {
            Log.Write(text);
            var handler = Notice;
            if (handler != null) handler(kind, text);
        }

        public static string Describe(Exception ex)
        {
            var agg = ex as AggregateException;
            if (agg != null && agg.InnerException != null) ex = agg.InnerException;
            if (ex is ObsException) return ex.Message;
            for (Exception e = ex; e != null; e = e.InnerException)
            {
                var socketError = e as SocketException;
                if (socketError != null && socketError.SocketErrorCode == SocketError.ConnectionRefused) return Unreachable();
            }
            if (ex is WebSocketException) return Unreachable();
            if (ex is OperationCanceledException) return Lang.T("Keine Antwort von OBS", "No answer from OBS");
            return ex.Message;
        }

        static string Unreachable()
        {
            return Lang.T("OBS nicht erreichbar – läuft OBS und ist der WebSocket-Server aktiviert?", "OBS not reachable – is OBS running and the WebSocket server enabled?");
        }
    }
}
