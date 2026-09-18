using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace AutoProcessTwin
{
    // עטיפה דקה סביב JavaScriptSerializer (זמין ב-GAC של .NET Framework, בלי
    // צורך ב-NuGet) + pretty-printer עצמאי כדי שקבצי הקונפיג שה-GUI כותב
    // יישארו קריאים בעריכה ידנית - כמו JSON.stringify(x, null, 2) בצד ה-Node.
    public static class Json
    {
        public static Dictionary<string, object> LoadDict(string path)
        {
            var serializer = new JavaScriptSerializer();
            serializer.MaxJsonLength = int.MaxValue;
            string text = File.ReadAllText(path, Encoding.UTF8);
            return (Dictionary<string, object>)serializer.DeserializeObject(text);
        }

        public static void SaveDict(string path, Dictionary<string, object> data)
        {
            var sb = new StringBuilder();
            WriteValue(sb, data, 0);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        public static List<object> AsList(object value)
        {
            var result = new List<object>();
            if (value == null) return result;
            var enumerable = value as IEnumerable;
            if (enumerable != null)
            {
                foreach (var item in enumerable) result.Add(item);
            }
            return result;
        }

        public static List<string> AsStringList(object value)
        {
            var result = new List<string>();
            foreach (var item in AsList(value)) result.Add(Convert.ToString(item));
            return result;
        }

        public static Dictionary<string, object> AsDict(object value)
        {
            return value as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        public static string GetString(Dictionary<string, object> dict, string key, string fallback)
        {
            object v;
            if (dict != null && dict.TryGetValue(key, out v) && v != null) return Convert.ToString(v);
            return fallback;
        }

        public static double GetNumber(Dictionary<string, object> dict, string key, double fallback)
        {
            object v;
            if (dict != null && dict.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToDouble(v); } catch { return fallback; }
            }
            return fallback;
        }

        public static bool GetBool(Dictionary<string, object> dict, string key, bool fallback)
        {
            object v;
            if (dict != null && dict.TryGetValue(key, out v) && v != null)
            {
                try { return Convert.ToBoolean(v); } catch { return fallback; }
            }
            return fallback;
        }

        private static void WriteValue(StringBuilder sb, object value, int indent)
        {
            if (value == null) { sb.Append("null"); return; }

            var dict = value as Dictionary<string, object>;
            if (dict != null) { WriteObject(sb, dict, indent); return; }

            if (value is string) { WriteString(sb, (string)value); return; }
            if (value is bool) { sb.Append((bool)value ? "true" : "false"); return; }

            var enumerable = value as IEnumerable;
            if (enumerable != null) { WriteArray(sb, enumerable, indent); return; }

            // מספרים - JavaScriptSerializer מחזיר int/double/long בהתאם
            sb.Append(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture));
        }

        private static void WriteObject(StringBuilder sb, Dictionary<string, object> dict, int indent)
        {
            if (dict.Count == 0) { sb.Append("{}"); return; }
            sb.Append("{\n");
            string pad = new string(' ', (indent + 1) * 2);
            int i = 0;
            foreach (var kv in dict)
            {
                sb.Append(pad);
                WriteString(sb, kv.Key);
                sb.Append(": ");
                WriteValue(sb, kv.Value, indent + 1);
                if (++i < dict.Count) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append(new string(' ', indent * 2)).Append("}");
        }

        private static void WriteArray(StringBuilder sb, IEnumerable items, int indent)
        {
            var list = new List<object>();
            foreach (var item in items) list.Add(item);
            if (list.Count == 0) { sb.Append("[]"); return; }
            sb.Append("[\n");
            string pad = new string(' ', (indent + 1) * 2);
            for (int i = 0; i < list.Count; i++)
            {
                sb.Append(pad);
                WriteValue(sb, list[i], indent + 1);
                if (i < list.Count - 1) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append(new string(' ', indent * 2)).Append("]");
        }

        private static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }
    }
}
