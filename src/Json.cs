using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Web.Script.Serialization;

namespace Kopfgeld
{
    static class Json
    {
        [ThreadStatic]
        static JavaScriptSerializer serializer;

        static JavaScriptSerializer Serializer
        {
            get
            {
                if (serializer == null) serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
                return serializer;
            }
        }

        public static string Serialize(object value)
        {
            return Serializer.Serialize(value);
        }

        public static T Deserialize<T>(string json)
        {
            return Serializer.Deserialize<T>(json);
        }

        public static Dictionary<string, object> Parse(string json)
        {
            return Serializer.Deserialize<Dictionary<string, object>>(json);
        }

        public static Dictionary<string, object> Obj(object value)
        {
            return value as Dictionary<string, object>;
        }

        public static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return null;
            return v as string ?? Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static bool Bool(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) && v is bool && (bool)v;
        }

        public static long Long(Dictionary<string, object> d, string key, long fallback)
        {
            object v;
            if (d == null || !d.TryGetValue(key, out v) || v == null) return fallback;
            try
            {
                return Convert.ToInt64(v, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        public static List<object> List(Dictionary<string, object> d, string key)
        {
            object v;
            var result = new List<object>();
            if (d == null || !d.TryGetValue(key, out v)) return result;
            var items = v as IEnumerable;
            if (items == null || v is string) return result;
            foreach (object item in items) result.Add(item);
            return result;
        }

        // JavaScriptSerializer has no indented output; settings.json should stay readable.
        public static string Pretty(string json)
        {
            var sb = new StringBuilder();
            int indent = 0;
            bool inString = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < json.Length) sb.Append(json[++i]);
                    else if (c == '"') inString = false;
                    continue;
                }
                switch (c)
                {
                    case '"':
                        inString = true;
                        sb.Append(c);
                        break;
                    case '{':
                    case '[':
                        sb.Append(c);
                        NewLine(sb, ++indent);
                        break;
                    case '}':
                    case ']':
                        NewLine(sb, --indent);
                        sb.Append(c);
                        break;
                    case ',':
                        sb.Append(c);
                        NewLine(sb, indent);
                        break;
                    case ':':
                        sb.Append(": ");
                        break;
                    default:
                        if (!char.IsWhiteSpace(c)) sb.Append(c);
                        break;
                }
            }
            return sb.ToString();
        }

        static void NewLine(StringBuilder sb, int indent)
        {
            sb.Append(Environment.NewLine);
            sb.Append(' ', Math.Max(0, indent) * 2);
        }
    }
}
