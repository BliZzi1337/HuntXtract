using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Kopfgeld
{
    // The overlay designs for OBS (Daten\overlays.json). The UI owns their format and fills in
    // defaults; the host only keeps the list, checks its shape and hands one profile to the
    // browser source by id – so a change in the program shows up in OBS without a new address.
    sealed class OverlayProfiles
    {
        const int MaxProfiles = 24;
        const int MaxBytes = 256 * 1024;
        static readonly Regex IdPattern = new Regex("^[a-z0-9-]{1,32}$");

        readonly object sync = new object();
        List<Dictionary<string, object>> profiles = new List<Dictionary<string, object>>();
        Dictionary<string, string> jsonById = new Dictionary<string, string>();
        string firstJson = "null";

        public void Load()
        {
            if (!File.Exists(AppInfo.OverlaysFile)) return;
            try
            {
                var root = Json.Parse(File.ReadAllText(AppInfo.OverlaysFile, Encoding.UTF8));
                Set(Check(Json.List(root, "profiles")));
            }
            catch (Exception ex)
            {
                Log.Write("overlays.json konnte nicht gelesen werden: " + ex.Message);
            }
        }

        public List<Dictionary<string, object>> ToUi()
        {
            lock (sync) return new List<Dictionary<string, object>>(profiles);
        }

        // Replaces the whole list (the UI always sends all profiles) and writes the file.
        public void Replace(object list)
        {
            var items = list as System.Collections.IEnumerable;
            if (items == null || list is string) throw new UserError(Lang.T("Ungültige Overlay-Liste.", "Invalid overlay list."));
            var raw = new List<object>();
            foreach (object item in items) raw.Add(item);
            var checkedList = Check(raw);
            var root = new Dictionary<string, object>();
            root["version"] = 1;
            root["profiles"] = checkedList;
            string json = Json.Serialize(root);
            if (json.Length > MaxBytes) throw new UserError(Lang.T("Die Overlay-Einstellungen sind zu groß.", "The overlay settings are too large."));
            Files.WriteAllTextAtomic(AppInfo.OverlaysFile, Json.Pretty(json));
            Set(checkedList);
        }

        // The profile for the browser source: by id, else the first one ("null" when there is none).
        public string ConfigJson(string id)
        {
            lock (sync)
            {
                string json;
                if (id != null && jsonById.TryGetValue(id, out json)) return json;
                return firstJson;
            }
        }

        static List<Dictionary<string, object>> Check(List<object> raw)
        {
            var result = new List<Dictionary<string, object>>();
            var seen = new HashSet<string>();
            foreach (object item in raw)
            {
                var profile = Json.Obj(item);
                string id = Json.Str(profile, "id");
                if (profile == null || id == null || !IdPattern.IsMatch(id) || !seen.Add(id)) continue;
                result.Add(profile);
                if (result.Count == MaxProfiles) break;
            }
            return result;
        }

        void Set(List<Dictionary<string, object>> list)
        {
            var byId = new Dictionary<string, string>();
            foreach (var profile in list) byId[Json.Str(profile, "id")] = Json.Serialize(profile);
            lock (sync)
            {
                profiles = list;
                jsonById = byId;
                firstJson = list.Count > 0 ? byId[Json.Str(list[0], "id")] : "null";
            }
        }
    }
}
