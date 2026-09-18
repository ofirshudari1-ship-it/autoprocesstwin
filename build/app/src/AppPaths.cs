using System;
using System.IO;
using System.Reflection;

namespace AutoProcessTwin
{
    // מאתר את שורש ההתקנה (התיקייה שמכילה agent/ + config/) יחסית ל-exe -
    // עובד גם כשה-exe רץ מתוך dist/AutoProcessTwin (פריסת התקנה: agent/config
    // לצד ה-exe) וגם בפיתוח, בלי לדרוש נתיב קבוע.
    //
    // מ-0.5.1: ברירת המחדל של המתקין היא Program Files (החלטת בעלים
    // מפורשת, דורסת את ההחלטה הקודמת "AppData כי אין צורך ב-UAC" - ראה
    // CHANGELOG). Program Files הוא read-only למשתמש לא-מורם אחרי ההתקנה,
    // אז Root משמש רק ל"payload" הקבוע (agent/, ה-exe עצמו) - כל דאטה
    // הניתנת לכתיבה (config/data/reports, כולל twin.db) עוברת ל-
    // %LOCALAPPDATA%\AutoProcessTwin, בדיוק כמו שצד ה-Node (agent/src/config.js)
    // מחשב עצמאית מ-process.env.LOCALAPPDATA - שתי הצדדים מגיעים לאותה תיקייה
    // פיזית בלי לתאם ביניהם ב-runtime.
    public static class AppPaths
    {
        public static readonly string Root = FindRoot();
        public static readonly string UserDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoProcessTwin");
        public static string AgentDir { get { return Path.Combine(Root, "agent"); } }
        public static string ConfigDir { get { return Path.Combine(UserDataRoot, "config"); } }
        public static string DataDir { get { return Path.Combine(UserDataRoot, "data"); } }
        public static string ReportsDir { get { return Path.Combine(UserDataRoot, "reports"); } }
        public static string NodeScript { get { return Path.Combine(AgentDir, "src", "index.js"); } }
        public static string BriefingScript { get { return Path.Combine(AgentDir, "src", "briefing.js"); } }
        public static string StatusScript { get { return Path.Combine(AgentDir, "src", "status.js"); } }
        public static string PatternsScript { get { return Path.Combine(AgentDir, "src", "patterns-report.js"); } }
        public static string OnboardingMarkerPath { get { return Path.Combine(DataDir, ".onboarded"); } }
        // *.example.json templates ship read-only next to the exe (inside the
        // installed payload); the *real*, user-editable config files live in
        // UserDataRoot (see ConfigDir above).
        public static string PrivacyConfigPath { get { return Path.Combine(ConfigDir, "privacy.json"); } }
        public static string PrivacyExamplePath { get { return Path.Combine(Root, "config", "privacy.example.json"); } }
        public static string GuardrailsConfigPath { get { return Path.Combine(ConfigDir, "guardrails.json"); } }
        public static string GuardrailsExamplePath { get { return Path.Combine(Root, "config", "guardrails.example.json"); } }
        public static string AiConfigPath { get { return Path.Combine(ConfigDir, "ai.json"); } }
        public static string AiExamplePath { get { return Path.Combine(Root, "config", "ai.example.json"); } }
        public static string AiInsightsScript { get { return Path.Combine(AgentDir, "src", "ai-insights.js"); } }
        public static string AppConfigPath { get { return Path.Combine(ConfigDir, "app.json"); } }
        public static string AppConfigExamplePath { get { return Path.Combine(Root, "config", "app.example.json"); } }

        private static string FindRoot()
        {
            string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            for (int i = 0; i < 4 && dir != null; i++)
            {
                if (Directory.Exists(Path.Combine(dir, "agent")) && Directory.Exists(Path.Combine(dir, "config")))
                {
                    return dir;
                }
                dir = Path.GetDirectoryName(dir);
            }
            // נופל חזרה לתיקיית ה-exe עצמה - EnsureLayout ידאג ליצור מה שחסר.
            return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        }

        public static void EnsureDataFolders()
        {
            Directory.CreateDirectory(ConfigDir);
            Directory.CreateDirectory(DataDir);
            Directory.CreateDirectory(Path.Combine(DataDir, "screenshots"));
            Directory.CreateDirectory(ReportsDir);
        }

        // מיגרציה חד-פעמית: משתמשים שהתקינו לפני 0.5.1 (או שמעדכנים בתוך אותה
        // תיקיית AppData\Local\Programs הישנה) עדיין יש להם config/data/reports
        // ממש ליד ה-exe (Root), לא ב-UserDataRoot. אם UserDataRoot ריק אבל יש
        // דאטה ליד ה-exe - מעתיקים (לא מעבירים: לא רוצים למחוק Root אם זו
        // עדיין התקנה חיה) לפני שמשהו אחר מנסה לקרוא/לכתוב מ-UserDataRoot.
        // אידמפוטנטי ובטוח-לכשל: כל שגיאה מדולגת בשקט, אף פעם לא חוסמת עלייה.
        public static void MigrateLegacyDataIfNeeded()
        {
            try
            {
                if (string.Equals(Path.GetFullPath(Root), Path.GetFullPath(UserDataRoot), StringComparison.OrdinalIgnoreCase))
                    return; // לא אמור לקרות, אבל למען הבטיחות

                bool userRootHasData = Directory.Exists(DataDir) || Directory.Exists(ConfigDir);
                if (userRootHasData) return;

                CopyLegacyDir(Path.Combine(Root, "config"), ConfigDir, skipExampleFiles: true);
                CopyLegacyDir(Path.Combine(Root, "data"), DataDir, skipExampleFiles: false);
                CopyLegacyDir(Path.Combine(Root, "reports"), ReportsDir, skipExampleFiles: false);
            }
            catch { }
        }

        private static void CopyLegacyDir(string sourceDir, string destDir, bool skipExampleFiles)
        {
            if (!Directory.Exists(sourceDir)) return;
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                if (skipExampleFiles && Path.GetFileName(file).IndexOf(".example.", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                string relative = file.Substring(sourceDir.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string dest = Path.Combine(destDir, relative);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    if (!File.Exists(dest)) File.Copy(file, dest);
                }
                catch { }
            }
        }

        public static string FindNodeExe()
        {
            string[] candidates = {
                Environment.GetEnvironmentVariable("ProgramFiles") + @"\nodejs\node.exe",
                Environment.GetEnvironmentVariable("ProgramFiles(x86)") + @"\nodejs\node.exe",
            };
            foreach (var c in candidates)
            {
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;
            }
            // fallback: להסתמך על PATH
            return "node";
        }
    }
}
