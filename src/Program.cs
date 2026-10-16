using System;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Kopfgeld
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try
            {
                SetProcessDPIAware();
            }
            catch
            {
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            AppInfo.Init();

            // One instance per data folder: two would record every match twice. The prefix is still
            // the old name, so an older build started on the same data can't run alongside.
            string id = "BountyLedger-" + ShortHash(AppInfo.DataDir);
            bool firstInstance;
            using (var mutex = new Mutex(true, "Local\\" + id, out firstInstance))
            using (var showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\" + id + "-show"))
            {
                if (!firstInstance)
                {
                    showSignal.Set();
                    return;
                }

                Log.Init();
                Log.Write("==== " + AppInfo.Name + " " + AppInfo.Version + " startet (Daten: " + AppInfo.DataDir + ")");
                Application.ThreadException += (s, e) => ReportCrash(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => ReportCrash(e.ExceptionObject as Exception);
                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    Log.Write("Unbeobachteter Fehler: " + e.Exception);
                    e.SetObserved();
                };

                bool existed;
                var settings = Settings.Load(out existed);
                Lang.Set(settings.Language);
                if (AppInfo.DataDirNote != null) Log.Write(AppInfo.DataDirNote);
                Application.Run(new MainForm(settings, !existed, showSignal));
                Log.Write("Beendet");
                GC.KeepAlive(mutex);
            }
        }

        static string ShortHash(string text)
        {
            using (var sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToLowerInvariant()));
                return BitConverter.ToString(hash, 0, 6).Replace("-", "");
            }
        }

        static void ReportCrash(Exception ex)
        {
            Log.Write("Unerwarteter Fehler: " + ex);
            MessageBox.Show(
                Lang.T("Unerwarteter Fehler:\n", "Unexpected error:\n") + (ex != null ? ex.Message : Lang.T("unbekannt", "unknown")) +
                Lang.T("\n\nDetails stehen im Protokoll (Daten\\protokoll.log).", "\n\nDetails are in the log (Daten\\protokoll.log)."),
                AppInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
