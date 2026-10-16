using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kopfgeld
{
    // Small click-through window on top of the game: counts down while HuntXtract waits for the
    // Mission Summary to hand out all points, then confirms the saved match. It is an ordinary
    // topmost window – nothing touches the game – and it is left out of screen captures, so it
    // neither shows up on stream nor gets into the frames HuntXtract reads.
    sealed class GameOverlay : Form
    {
        const int WS_EX_TOPMOST = 0x8, WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000, WS_EX_NOACTIVATE = 0x08000000;
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33, DWMWCP_ROUND = 2;
        const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SetWindowDisplayAffinity(IntPtr hwnd, uint affinity);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        enum Mode { Hidden, Countdown, Message }

        // Colours of the current design (SetPalette).
        Color Gold = Color.FromArgb(226, 178, 92);
        Color TextColor = Color.FromArgb(237, 230, 218);
        Color Muted = Color.FromArgb(163, 153, 139);
        Color Track = Color.FromArgb(56, 48, 40);

        readonly float scale;
        readonly Font headingFont, mainFont, detailFont;
        readonly Timer ticker = new Timer { Interval = 100 };
        readonly Timer hideTimer = new Timer();
        readonly Timer previewTimer = new Timer();
        Mode mode = Mode.Hidden;
        bool followsStatus, previewing;
        DateTime started, deadline;
        bool counting;
        string mainText = "", detailText = "";
        double progress = -1;

        public string Position { get; set; }

        public GameOverlay(float scale)
        {
            this.scale = scale;
            Position = "top-center";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            BackColor = Color.FromArgb(22, 19, 16);
            Opacity = 0.94;
            DoubleBuffered = true;
            Size = new Size(S(360), S(78));
            headingFont = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            mainFont = new Font("Segoe UI Semibold", 13f);
            detailFont = new Font("Segoe UI", 8.5f);
            ticker.Tick += (s, e) => Invalidate();
            hideTimer.Tick += (s, e) => HideNow();
            previewTimer.Tick += (s, e) =>
            {
                previewTimer.Stop();
                ShowMessage(Lang.T("Match #12 gespeichert", "Match #12 saved"),
                    Lang.T("Vorschau – so bestätigt HuntXtract das gespeicherte Match", "Preview – this is how HuntXtract confirms the saved match"), 3500, 1);
            };
        }

        int S(int value)
        {
            return (int)Math.Round(value * scale);
        }

        public void SetPalette(WindowChrome chrome)
        {
            if (chrome == null) return;
            BackColor = chrome.Panel;
            Gold = chrome.Accent;
            TextColor = chrome.Text;
            Muted = chrome.Muted;
            Track = chrome.Track;
            Invalidate();
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        // Created topmost, click-through and never activated, so the game keeps the focus.
        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int round = DWMWCP_ROUND;
                DwmSetWindowAttribute(Handle, DWMWA_WINDOW_CORNER_PREFERENCE, ref round, sizeof(int));
            }
            catch
            {
            }
            if (!SetWindowDisplayAffinity(Handle, WDA_EXCLUDEFROMCAPTURE))
                Log.Write("Countdown-Fenster lässt sich nicht aus Aufnahmen ausblenden (Fehler " + Marshal.GetLastWin32Error() + ")");
        }

        // True while the window shows a state of the capture engine (and not a timed message).
        public bool FollowsStatus
        {
            get { return mode != Mode.Hidden && followsStatus; }
        }

        // remainingMs: time until reading; elapsedMs: time since the Mission Summary was recognised.
        public void ShowCountdown(int remainingMs, int elapsedMs, bool valuesCounting)
        {
            StopPreview();
            SetCountdown(remainingMs, elapsedMs, valuesCounting);
            followsStatus = true;
            Present();
        }

        void SetCountdown(int remainingMs, int elapsedMs, bool valuesCounting)
        {
            var now = DateTime.UtcNow;
            var newDeadline = now.AddMilliseconds(remainingMs);
            var newStart = now.AddMilliseconds(-elapsedMs);
            // The engine reports once per captured frame; small differences would make the seconds jump.
            if (mode != Mode.Countdown || Math.Abs((newDeadline - deadline).TotalMilliseconds) > 400) deadline = newDeadline;
            if (mode != Mode.Countdown || Math.Abs((newStart - started).TotalMilliseconds) > 1500) started = newStart;
            counting = valuesCounting;
            mode = Mode.Countdown;
            hideTimer.Stop();
            ticker.Start();
        }

        // hideAfterMs <= 0 keeps the message until the engine state changes. bar < 0 hides the bar.
        public void ShowMessage(string main, string detail, int hideAfterMs, double bar)
        {
            StopPreview();
            mode = Mode.Message;
            mainText = main ?? "";
            detailText = detail ?? "";
            progress = bar;
            followsStatus = hideAfterMs <= 0;
            ticker.Stop();
            hideTimer.Stop();
            if (hideAfterMs > 0)
            {
                hideTimer.Interval = hideAfterMs;
                hideTimer.Start();
            }
            Present();
        }

        // A short run of the countdown and the confirmation, for the settings page.
        public void Preview()
        {
            StopPreview();
            mode = Mode.Hidden;
            SetCountdown(6000, 6000, false);
            previewing = true;
            followsStatus = false;
            Present();
            previewTimer.Interval = 6300;
            previewTimer.Start();
        }

        public void HideNow()
        {
            StopPreview();
            mode = Mode.Hidden;
            followsStatus = false;
            ticker.Stop();
            hideTimer.Stop();
            if (Visible) Hide();
        }

        void StopPreview()
        {
            previewing = false;
            previewTimer.Stop();
        }

        void Present()
        {
            Height = S(DetailLines() > 1 ? 94 : 78);
            Place();
            if (!Visible) Show();
            // Games that are topmost themselves could cover the window; put it back on top without activating it.
            SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            Invalidate();
        }

        // Measured on the window itself: with ClearType, text is a few pixels wider on screen than in a bitmap.
        int DetailLines()
        {
            string detail = mode == Mode.Countdown ? CountdownDetail() : detailText;
            if (string.IsNullOrEmpty(detail)) return 1;
            using (var g = CreateGraphics())
            {
                var size = TextRenderer.MeasureText(g, detail, detailFont, new Size(Width - 2 * S(14), int.MaxValue), DetailFlags);
                return size.Height > detailFont.Height * 1.5 ? 2 : 1;
            }
        }

        // TextBoxControl: at most the lines that fit, the last one ends in "…".
        const TextFormatFlags DetailFlags = TextFormatFlags.NoPadding | TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.EndEllipsis;

        string CountdownHeading()
        {
            return AppInfo.Name.ToUpperInvariant() + (previewing ? Lang.T(" · VORSCHAU", " · PREVIEW") : Lang.T(" · MISSION SUMMARY ERKANNT", " · MISSION SUMMARY DETECTED"));
        }

        string CountdownDetail()
        {
            if (previewing) return Lang.T("So zählt HuntXtract bis zum Auslesen herunter", "This is how HuntXtract counts down to the reading");
            return counting ? Lang.T("Punkte werden noch vergeben …", "Points are still being awarded …")
                : Lang.T("Bitte die Mission Summary offen lassen", "Please keep the Mission Summary open");
        }

        // On the monitor of the active window (the game), at the chosen spot.
        void Place()
        {
            Screen screen;
            try
            {
                IntPtr foreground = GetForegroundWindow();
                screen = foreground != IntPtr.Zero ? Screen.FromHandle(foreground) : Screen.PrimaryScreen;
            }
            catch
            {
                screen = Screen.PrimaryScreen;
            }
            var b = screen.Bounds;
            int margin = S(24);
            int x = b.Left + (b.Width - Width) / 2, y = b.Top + margin;
            switch (Position)
            {
                case "top-left": x = b.Left + margin; break;
                case "top-right": x = b.Right - Width - margin; break;
                case "bottom-center": y = b.Bottom - Height - S(96); break;
                case "bottom-right": x = b.Right - Width - margin; y = b.Bottom - Height - S(96); break;
            }
            Location = new Point(x, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            string heading = AppInfo.Name.ToUpperInvariant(), main = mainText, detail = detailText;
            double bar = progress;
            if (mode == Mode.Countdown)
            {
                var now = DateTime.UtcNow;
                double left = Math.Max(0, (deadline - now).TotalMilliseconds);
                int seconds = (int)Math.Ceiling(left / 1000.0);
                heading = CountdownHeading();
                main = seconds > 0 ? Lang.F("Auslesen in {0} s", "Reading in {0} s", seconds) : Lang.T("Gleich fertig …", "Almost done …");
                detail = CountdownDetail();
                bar = (now - started).TotalMilliseconds / Math.Max(1, (deadline - started).TotalMilliseconds);
            }
            int pad = S(14);
            TextRenderer.DrawText(g, heading, headingFont, new Point(pad, S(9)), Gold, TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, main, mainFont, new Rectangle(pad, S(22), Width - 2 * pad, S(28)), TextColor,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            TextRenderer.DrawText(g, detail, detailFont, new Rectangle(pad, S(51), Width - 2 * pad, Height - S(51) - S(7)), Muted, DetailFlags);
            if (bar >= 0)
            {
                int h = Math.Max(2, S(3));
                using (var track = new SolidBrush(Track)) g.FillRectangle(track, 0, Height - h, Width, h);
                using (var fill = new SolidBrush(Gold)) g.FillRectangle(fill, 0, Height - h, (int)(Width * Math.Max(0, Math.Min(1, bar))), h);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ticker.Dispose();
                hideTimer.Dispose();
                previewTimer.Dispose();
                headingFont.Dispose();
                mainFont.Dispose();
                detailFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
