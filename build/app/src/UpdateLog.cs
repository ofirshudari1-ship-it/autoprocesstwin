using System;
using System.IO;
using System.Text;

namespace AutoProcessTwin
{
    // 0.5.10: לוג ייעודי למסלול העדכון (%APPDATA%\AutoProcessTwin\logs\update.log).
    // עד עכשיו כל כשל בבדיקת/הורדת עדכון נבלע בשקט לגמרי, ואי אפשר היה לדעת
    // למה "לא התעדכן". אף פעם לא זורק, מתרוקן אוטומטית ב-256KB (גיבוי אחד).
    public static class UpdateLog
    {
        // --update-selftest מכבה את הכתיבה כדי לא לגעת בלוג האמיתי של המשתמש.
        public static bool Disabled;

        private static readonly object Gate = new object();
        private const long MaxBytes = 256 * 1024;

        public static string LogPath
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AutoProcessTwin", "logs", "update.log");
            }
        }

        public static void Write(string message)
        {
            if (Disabled) return;
            try
            {
                lock (Gate)
                {
                    string path = LogPath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > MaxBytes)
                    {
                        try { File.Delete(path + ".old"); File.Move(path, path + ".old"); } catch { }
                    }
                    File.AppendAllText(path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + "\r\n", Encoding.UTF8);
                }
            }
            catch { /* לוג הוא best-effort - לעולם לא מפיל את מסלול העדכון */ }
        }
    }
}
