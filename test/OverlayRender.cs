using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Kopfgeld
{
    // Renders the in-game countdown window in its states to PNG files without showing it
    // (the window is left out of screen captures, so screenshots can't show it).
    // Usage: OverlayRender.exe <output folder>
    static class OverlayRender
    {
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string dir = args.Length > 0 ? args[0] : ".";
            Directory.CreateDirectory(dir);
            float scale;
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) scale = g.DpiX / 96f;
            Console.WriteLine("Skalierung " + scale);

            using (var o = new GameOverlay(scale))
            {
                Countdown(o, 9000, 12000, false, false);
                Save(o, dir, "1-countdown");
                Countdown(o, 2600, 15000, true, false);
                Save(o, dir, "2-counting");
                Countdown(o, 0, 16000, false, false);
                Save(o, dir, "3-gleich");
                Message(o, "Lese Werte aus …", "Gleich fertig", -1);
                Save(o, dir, "4-lesen");
                Message(o, "Match #5 gespeichert", "Hunters Killed 2 · Monsters Killed 9 · Bounty Collected 2 · Hunt Dollars 870 · Hunter Progress 2.950 · Bloodline Progress 1.200 · Bounty Token 3", 1);
                Save(o, dir, "5-gespeichert");
                Message(o, "Match #6 gespeichert", "Die Mission Summary wurde geschlossen, bevor alle Punkte vergeben waren – bitte Werte prüfen.", 1);
                Save(o, dir, "6-hinweis");
                Message(o, "Nicht gespeichert", "Noch keine Felder markiert – bitte zuerst die Einrichtung abschließen.", -1);
                Save(o, dir, "7-fehler");
                Countdown(o, 4000, 6000, false, true);
                Save(o, dir, "8-vorschau");

                // Further arguments "name=bg,panel,text,accent,muted,track,dark": the colours of a design.
                for (int i = 1; i < args.Length; i++)
                {
                    int eq = args[i].IndexOf('=');
                    var chrome = eq > 0 ? WindowChrome.Parse(args[i].Substring(eq + 1)) : null;
                    if (chrome == null) continue;
                    string name = args[i].Substring(0, eq);
                    o.SetPalette(chrome);
                    Countdown(o, 9000, 12000, false, false);
                    Save(o, dir, name + "-countdown");
                    Message(o, "Match #5 gespeichert", "Hunters Killed 2 · Monsters Killed 9 · Bounty Collected 2 · Hunt Dollars 870", 1);
                    Save(o, dir, name + "-gespeichert");
                }
            }
        }

        static void Countdown(GameOverlay o, int remainingMs, int totalMs, bool counting, bool preview)
        {
            var now = DateTime.UtcNow;
            SetMode(o, "Countdown");
            Set(o, "deadline", now.AddMilliseconds(remainingMs + 150));
            Set(o, "started", now.AddMilliseconds(remainingMs + 150 - totalMs));
            Set(o, "counting", counting);
            Set(o, "previewing", preview);
        }

        static void Message(GameOverlay o, string main, string detail, double bar)
        {
            SetMode(o, "Message");
            Set(o, "previewing", false);
            Set(o, "mainText", main);
            Set(o, "detailText", detail);
            Set(o, "progress", bar);
        }

        static void Save(GameOverlay o, string dir, string name)
        {
            // Same height rule as GameOverlay.Present().
            float scale = (float)typeof(GameOverlay).GetField("scale", Private).GetValue(o);
            int lines = (int)typeof(GameOverlay).GetMethod("DetailLines", Private).Invoke(o, null);
            o.Height = (int)Math.Round((lines > 1 ? 94 : 78) * scale);
            using (var bmp = new Bitmap(o.Width, o.Height, PixelFormat.Format32bppArgb))
            {
                o.DrawToBitmap(bmp, new Rectangle(0, 0, o.Width, o.Height));
                string file = Path.Combine(dir, "overlay-" + name + ".png");
                bmp.Save(file, ImageFormat.Png);
                Console.WriteLine(file + " (" + o.Width + "x" + o.Height + ", " + lines + " Zeile(n) Detail)");
            }
        }

        static void SetMode(GameOverlay o, string mode)
        {
            var type = typeof(GameOverlay).GetNestedType("Mode", BindingFlags.NonPublic);
            Set(o, "mode", Enum.Parse(type, mode));
        }

        static void Set(object o, string field, object value)
        {
            typeof(GameOverlay).GetField(field, Private).SetValue(o, value);
        }
    }
}
