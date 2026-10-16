using System.Globalization;

namespace Kopfgeld
{
    // The language of everything the program itself says: the countdown in the game, the tray
    // menu, notices and error messages. The UI (web/app/i18n.js) follows the same setting.
    // Texts stand in both languages right where they are used: Lang.T("Deutsch", "English").
    // The log file stays German.
    static class Lang
    {
        static volatile bool english = true;

        public static bool English
        {
            get { return english; }
        }

        public static string Id
        {
            get { return english ? "en" : "de"; }
        }

        // Numbers in the countdown and in notices: 1.234 in German, 1,234 in English.
        public static CultureInfo Culture
        {
            get { return CultureInfo.GetCultureInfo(english ? "en-GB" : "de-DE"); }
        }

        public static void Set(string id)
        {
            english = id != "de";
        }

        public static string T(string german, string englishText)
        {
            return english ? englishText : german;
        }

        // T with placeholders {0}, {1} … filled in.
        public static string F(string german, string englishText, params object[] args)
        {
            return string.Format(Culture, T(german, englishText), args);
        }
    }
}
