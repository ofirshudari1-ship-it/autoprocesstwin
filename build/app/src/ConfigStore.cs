using System.Collections.Generic;
using System.IO;

namespace AutoProcessTwin
{
    // אותה לוגיקת נפילה ל-.example.json כמו agent/src/config.js בצד Node -
    // כדי ששני הצדדים (GUI ו-recorder) יסכימו על אותו קובץ אמיתי.
    public static class ConfigStore
    {
        public static Dictionary<string, object> LoadPrivacy()
        {
            return LoadWithFallback(AppPaths.PrivacyConfigPath, AppPaths.PrivacyExamplePath);
        }

        public static void SavePrivacy(Dictionary<string, object> data)
        {
            Json.SaveDict(AppPaths.PrivacyConfigPath, data);
        }

        public static Dictionary<string, object> LoadGuardrails()
        {
            return LoadWithFallback(AppPaths.GuardrailsConfigPath, AppPaths.GuardrailsExamplePath);
        }

        public static void SaveGuardrails(Dictionary<string, object> data)
        {
            Json.SaveDict(AppPaths.GuardrailsConfigPath, data);
        }

        public static Dictionary<string, object> LoadAi()
        {
            return LoadWithFallback(AppPaths.AiConfigPath, AppPaths.AiExamplePath);
        }

        public static void SaveAi(Dictionary<string, object> data)
        {
            Json.SaveDict(AppPaths.AiConfigPath, data);
        }

        public static Dictionary<string, object> LoadApp()
        {
            return LoadWithFallback(AppPaths.AppConfigPath, AppPaths.AppConfigExamplePath);
        }

        public static void SaveApp(Dictionary<string, object> data)
        {
            Json.SaveDict(AppPaths.AppConfigPath, data);
        }

        private static Dictionary<string, object> LoadWithFallback(string realPath, string examplePath)
        {
            string path = File.Exists(realPath) ? realPath
                        : File.Exists(examplePath) ? examplePath
                        : null;
            if (path == null) return new Dictionary<string, object>();
            Dictionary<string, object> data;
            try { data = Json.LoadDict(path); }
            catch { data = new Dictionary<string, object>(); }
            if (data == null) data = new Dictionary<string, object>();
            if (path == examplePath)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(realPath));
                    Json.SaveDict(realPath, data);
                }
                catch { }
            }
            return data;
        }
    }
}
