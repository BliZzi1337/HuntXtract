using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kopfgeld
{
    // How much of Hunt a screen frame may show. Only Hunt's own window is ever taken from the
    // screen – the desktop and other programs never.
    enum HuntView { Shown, NotRunning, NotInFront, OtherScreen }

    // Reads the picture straight from a monitor, for people who do not want to run OBS just for
    // HuntXtract. Works while the game runs in a window or borderless; in exclusive fullscreen
    // Windows may hand out a black picture – then OBS is the better source.
    static class ScreenCapture
    {
        public static List<Dictionary<string, object>> List()
        {
            var screens = new List<Dictionary<string, object>>();
            int index = 0;
            foreach (var screen in Screen.AllScreens)
            {
                index++;
                var d = new Dictionary<string, object>();
                d["device"] = screen.DeviceName;
                d["label"] = Lang.F("Bildschirm {0} · {1}×{2}", "Screen {0} · {1}×{2}", index, screen.Bounds.Width, screen.Bounds.Height) +
                    (screen.Primary ? Lang.T(" (Hauptbildschirm)", " (main screen)") : "");
                d["width"] = screen.Bounds.Width;
                d["height"] = screen.Bounds.Height;
                d["primary"] = screen.Primary;
                screens.Add(d);
            }
            return screens;
        }

        public static Screen Find(string device)
        {
            if (!string.IsNullOrEmpty(device))
            {
                var wanted = Screen.AllScreens.FirstOrDefault(s => s.DeviceName == device);
                if (wanted != null) return wanted;
            }
            return Screen.PrimaryScreen;
        }

        public static string Label(string device)
        {
            var screen = Find(device);
            int index = Array.IndexOf(Screen.AllScreens, screen) + 1;
            return Lang.F("Bildschirm {0} ({1}×{2})", "Screen {0} ({1}×{2})", Math.Max(1, index), screen.Bounds.Width, screen.Bounds.Height);
        }

        // The size of the monitor Hunt runs on (the picked one while Hunt isn't found) – for tests the
        // size of the picture standing in for it.
        public static Size ScreenSize(string device)
        {
            string testScreen = Environment.GetEnvironmentVariable("KOPFGELD_TEST_SCREEN");
            if (!string.IsNullOrEmpty(testScreen))
            {
                try
                {
                    using (var picture = Imaging.Load(testScreen)) return picture.Size;
                }
                catch (Exception)
                {
                }
            }
            var hunt = HuntWindow.Monitor();
            return (hunt ?? Find(device)).Bounds.Size;
        }

        // Whether Hunt could be read right now.
        public static HuntView View(string device)
        {
            Rectangle bounds, area;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("KOPFGELD_TEST_SCREEN"))) return TestView(Size.Empty, out area);
            return View(device, out bounds, out area);
        }

        // Hunt's picture, relative to the monitor it is on: the part of Hunt's window there, while
        // Hunt is the program in front. The monitor is the one Hunt runs on, whichever is picked in
        // the setup – with several screens nothing needs choosing.
        static HuntView View(string device, out Rectangle bounds, out Rectangle area)
        {
            area = Rectangle.Empty;
            bounds = Find(device).Bounds;
            Rectangle window;
            var view = HuntWindow.Find(out window);
            if (view != HuntView.Shown) return view;
            bounds = Screen.FromRectangle(window).Bounds;
            area = Rectangle.Intersect(bounds, window);
            if (area.Width < 2 || area.Height < 2)
            {
                area = Rectangle.Empty;
                return HuntView.OtherScreen;
            }
            area.Offset(-bounds.X, -bounds.Y);
            return HuntView.Shown;
        }

        // One frame of the monitor Hunt runs on with nothing but Hunt in it: black around Hunt's
        // window, and all black – without reading the screen at all – while another program is in
        // front. HuntXtract's own countdown window is left out by Windows, so it never shows either.
        public static Bitmap Grab(string device, out HuntView view)
        {
            // For tests: a picture file stands in for the monitor (read anew every time).
            string testScreen = Environment.GetEnvironmentVariable("KOPFGELD_TEST_SCREEN");
            if (!string.IsNullOrEmpty(testScreen)) return TestFrame(testScreen, out view);
            Rectangle bounds, area;
            view = View(device, out bounds, out area);
            if (bounds.Width < 2 || bounds.Height < 2) throw new InvalidOperationException(Lang.T("Der Bildschirm liefert keine Größe.", "The screen reports no size."));
            var bmp = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            try
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Black);
                    if (view == HuntView.Shown)
                        g.CopyFromScreen(bounds.X + area.X, bounds.Y + area.Y, area.X, area.Y, area.Size, CopyPixelOperation.SourceCopy);
                }
            }
            catch
            {
                bmp.Dispose();
                throw;
            }
            return bmp;
        }

        // Same shape as the OBS screenshots: PNG in full size, or a small JPEG for detection.
        // Null while Hunt isn't to be seen.
        public static byte[] Grab(string device, string format, int width, int quality, out HuntView view)
        {
            using (var frame = Grab(device, out view))
            {
                if (view != HuntView.Shown) return null;
                return format == "png" ? Imaging.EncodePng(frame) : Imaging.EncodeJpeg(frame, quality, width);
            }
        }

        // For tests, KOPFGELD_TEST_HUNT names a file saying where the stand-in picture shows Hunt:
        // "front" (or no file), "back", "other", "none", or "x,y,w,h" for a window.
        static HuntView TestView(Size picture, out Rectangle area)
        {
            area = new Rectangle(Point.Empty, picture);
            string file = Environment.GetEnvironmentVariable("KOPFGELD_TEST_HUNT");
            string text = "front";
            try
            {
                if (!string.IsNullOrEmpty(file) && File.Exists(file)) text = File.ReadAllText(file).Trim().ToLowerInvariant();
            }
            catch (IOException)
            {
            }
            if (text == "back") return HuntView.NotInFront;
            if (text == "other") return HuntView.OtherScreen;
            if (text == "none") return HuntView.NotRunning;
            var parts = text.Split(',');
            int x, y, w, h;
            if (parts.Length == 4 && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out x)
                && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out y)
                && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out w)
                && int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out h))
            {
                area = picture.IsEmpty ? new Rectangle(x, y, w, h) : Rectangle.Intersect(area, new Rectangle(x, y, w, h));
            }
            return HuntView.Shown;
        }

        static Bitmap TestFrame(string file, out HuntView view)
        {
            var picture = Imaging.Load(file);
            Rectangle area;
            view = TestView(picture.Size, out area);
            if (view == HuntView.Shown && area.Size == picture.Size) return picture;
            using (var g = Graphics.FromImage(picture))
            using (var outside = new Region(new Rectangle(Point.Empty, picture.Size)))
            {
                if (view == HuntView.Shown) outside.Exclude(area);
                g.FillRegion(Brushes.Black, outside);
            }
            return picture;
        }
    }

    // Hunt's window, found by its process (HuntGame.exe).
    static class HuntWindow
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        static extern IntPtr GetAncestor(IntPtr window, uint flags);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

        const uint RootOwner = 3; // GA_ROOTOWNER

        [DllImport("user32.dll")]
        static extern bool GetClientRect(IntPtr window, out NativeRect rect);

        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

        [StructLayout(LayoutKind.Sequential)]
        struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct NativePoint
        {
            public int X, Y;
        }

        static readonly object sync = new object();
        static readonly Stopwatch clock = Stopwatch.StartNew();
        static int[] ids = new int[0];
        static long idsAt = -10000;

        // Shown with the picture area of Hunt's window on the screen, if it is the window in front.
        public static HuntView Find(out Rectangle window)
        {
            window = Rectangle.Empty;
            int[] hunt = HuntIds();
            if (hunt.Length == 0) return HuntView.NotRunning;
            IntPtr front = GetForegroundWindow();
            if (front == IntPtr.Zero) return HuntView.NotInFront;
            uint processId;
            GetWindowThreadProcessId(front, out processId);
            if (Array.IndexOf(hunt, (int)processId) < 0) return HuntView.NotInFront;
            // A little window of Hunt's own in front (a message, say): the picture is still its main window.
            IntPtr main = GetAncestor(front, RootOwner);
            if (main == IntPtr.Zero) main = front;
            if (IsIconic(main)) return HuntView.NotInFront;
            NativeRect client;
            var origin = new NativePoint();
            if (!GetClientRect(main, out client) || !ClientToScreen(main, ref origin)) return HuntView.NotInFront;
            window = new Rectangle(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
            return window.Width > 0 && window.Height > 0 ? HuntView.Shown : HuntView.NotInFront;
        }

        // The monitor Hunt's window is on while Hunt runs, in front or not; null otherwise.
        public static Screen Monitor()
        {
            foreach (int id in HuntIds())
            {
                try
                {
                    using (var process = Process.GetProcessById(id))
                    {
                        IntPtr main = process.MainWindowHandle;
                        NativeRect r;
                        if (main == IntPtr.Zero || IsIconic(main) || !GetWindowRect(main, out r)) continue;
                        return Screen.FromRectangle(new Rectangle(r.Left, r.Top, r.Right - r.Left, r.Bottom - r.Top));
                    }
                }
                catch (ArgumentException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }
            return null;
        }

        // Hunt's process ids, looked up anew every two seconds at most.
        static int[] HuntIds()
        {
            lock (sync)
            {
                if (clock.ElapsedMilliseconds - idsAt < 2000) return ids;
                var list = Process.GetProcessesByName("HuntGame");
                ids = list.Select(p => p.Id).ToArray();
                foreach (var p in list) p.Dispose();
                idsAt = clock.ElapsedMilliseconds;
                return ids;
            }
        }
    }
}
