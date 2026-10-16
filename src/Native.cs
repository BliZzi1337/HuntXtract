using System;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Kopfgeld
{
    // Title bar in the colours of the current design (Windows 11 takes any colour, Windows 10 only
    // dark or light). Before a design is known it follows the Windows app theme (Settings →
    // Personalization → Colors). Windows only draws a dark caption for programs that ask for it.
    static class TitleBar
    {
        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        const int DWMWA_BORDER_COLOR = 34, DWMWA_CAPTION_COLOR = 35, DWMWA_TEXT_COLOR = 36;
        static string lastLogged;
        const int WM_SETTINGCHANGE = 0x001A;
        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);

        public static bool SystemUsesDarkTheme()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    object value = key == null ? null : key.GetValue("AppsUseLightTheme");
                    return value is int && (int)value == 0;
                }
            }
            catch
            {
                return false;
            }
        }

        // chrome == null: dark or light like Windows, with the system caption colours.
        public static void Apply(IntPtr window, WindowChrome chrome)
        {
            int dark = (chrome != null ? chrome.Dark : SystemUsesDarkTheme()) ? 1 : 0;
            try
            {
                int result = DwmSetWindowAttribute(window, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                if (result != 0) result = DwmSetWindowAttribute(window, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref dark, sizeof(int));
                // DWMWA_COLOR_DEFAULT (0xFFFFFFFF) hands the colours back to Windows.
                int caption = chrome != null ? ColorRef(chrome.Background) : -1;
                int text = chrome != null ? ColorRef(chrome.Text) : -1;
                int colored = DwmSetWindowAttribute(window, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
                DwmSetWindowAttribute(window, DWMWA_TEXT_COLOR, ref text, sizeof(int));
                DwmSetWindowAttribute(window, DWMWA_BORDER_COLOR, ref caption, sizeof(int));
                // Repaint the frame now instead of on the next activation.
                SetWindowPos(window, IntPtr.Zero, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                string line = "Titelleiste: " + (dark == 1 ? "dunkel" : "hell") +
                    (chrome == null ? " (Windows-Design)" : colored == 0 ? " in den Farben des Designs" : " (Farben erst ab Windows 11)") +
                    (result == 0 ? "" : ", Dunkelmodus nicht unterstützt: 0x" + result.ToString("X8"));
                if (line != lastLogged) Log.Write(line);
                lastLogged = line;
            }
            catch (Exception ex)
            {
                Log.Write("Titelleiste konnte nicht angepasst werden: " + ex.Message);
            }
        }

        static int ColorRef(System.Drawing.Color c)
        {
            return c.R | (c.G << 8) | (c.B << 16);
        }

        // True when Windows reports a change of the light/dark app theme.
        public static bool IsThemeChange(ref Message m)
        {
            return m.Msg == WM_SETTINGCHANGE && m.LParam != IntPtr.Zero && Marshal.PtrToStringUni(m.LParam) == "ImmersiveColorSet";
        }
    }

    // System-wide hotkey ("Jetzt auslesen"), works while the game has focus.
    static class Hotkeys
    {
        public const int WM_HOTKEY = 0x0312;
        const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8, MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public static bool TryParse(string text, out uint modifiers, out uint key)
        {
            modifiers = 0;
            key = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            foreach (string rawPart in text.Split('+'))
            {
                string part = rawPart.Trim().ToUpperInvariant();
                if (part.Length == 0) return false;
                if (part == "CTRL" || part == "STRG" || part == "CONTROL") modifiers |= MOD_CONTROL;
                else if (part == "ALT") modifiers |= MOD_ALT;
                else if (part == "SHIFT" || part == "UMSCHALT") modifiers |= MOD_SHIFT;
                else if (part == "WIN") modifiers |= MOD_WIN;
                else if (key != 0) return false;
                else if (part.Length == 1 && ((part[0] >= 'A' && part[0] <= 'Z') || (part[0] >= '0' && part[0] <= '9'))) key = part[0];
                else if (part.StartsWith("F") && part.Length <= 3)
                {
                    int n;
                    if (!int.TryParse(part.Substring(1), out n) || n < 1 || n > 24) return false;
                    key = (uint)(0x70 + n - 1);
                }
                else if (part.StartsWith("NUM") && part.Length == 4 && char.IsDigit(part[3])) key = (uint)(0x60 + (part[3] - '0'));
                else return false;
            }
            // A bare letter would fire while typing in chat; require a modifier unless it is an F-key.
            return key != 0 && (modifiers != 0 || (key >= 0x70 && key <= 0x87));
        }

        // Returns null on success, otherwise a message for the user.
        public static string Register(IntPtr window, int id, string text)
        {
            UnregisterHotKey(window, id);
            if (string.IsNullOrWhiteSpace(text)) return null;
            uint modifiers, key;
            if (!TryParse(text, out modifiers, out key))
                return Lang.F("Tastenkürzel „{0}“ ist ungültig (Beispiel: Ctrl+Alt+K oder F9).", "Shortcut “{0}” is not valid (example: Ctrl+Alt+K or F9).", text);
            if (!RegisterHotKey(window, id, modifiers | MOD_NOREPEAT, key))
                return Lang.F("Tastenkürzel „{0}“ ist schon von einem anderen Programm belegt.", "Shortcut “{0}” is already taken by another program.", text);
            return null;
        }

        public static void Unregister(IntPtr window, int id)
        {
            UnregisterHotKey(window, id);
        }
    }

    // A short two-tone chime, generated instead of shipped as a file.
    static class Chime
    {
        static SoundPlayer player;

        public static void Play()
        {
            try
            {
                if (player == null)
                {
                    player = new SoundPlayer(new MemoryStream(BuildWave()));
                    player.Load();
                }
                player.Play();
            }
            catch (Exception ex)
            {
                Log.Write("Ton konnte nicht abgespielt werden: " + ex.Message);
            }
        }

        static byte[] BuildWave()
        {
            const int rate = 44100;
            int samples = (int)(rate * 0.55);
            var pcm = new short[samples];
            AddTone(pcm, rate, 880.0, 0.00, 0.35, 0.22);
            AddTone(pcm, rate, 1318.5, 0.11, 0.44, 0.20);
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                int dataBytes = samples * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short)1);
                w.Write((short)1);
                w.Write(rate);
                w.Write(rate * 2);
                w.Write((short)2);
                w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (short s in pcm) w.Write(s);
                w.Flush();
                return ms.ToArray();
            }
        }

        static void AddTone(short[] pcm, int rate, double frequency, double start, double length, double volume)
        {
            int first = (int)(start * rate), count = (int)(length * rate);
            for (int i = 0; i < count && first + i < pcm.Length; i++)
            {
                double t = i / (double)rate;
                double attack = Math.Min(1.0, t / 0.008);
                double envelope = attack * Math.Exp(-t * 7.5);
                double sample = Math.Sin(2 * Math.PI * frequency * t) + 0.25 * Math.Sin(4 * Math.PI * frequency * t);
                int value = pcm[first + i] + (int)(sample * envelope * volume * short.MaxValue * 0.8);
                pcm[first + i] = (short)Math.Max(short.MinValue, Math.Min(short.MaxValue, value));
            }
        }
    }
}
