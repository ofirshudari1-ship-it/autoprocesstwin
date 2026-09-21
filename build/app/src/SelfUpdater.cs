using System;
using System.Diagnostics;
using System.IO;
using System.Net;

namespace AutoProcessTwin
{
    // מימוש בפועל של "one-click update" (0.5.6) - ר' UpdateChecker.cs לבדיקת
    // הגרסה עצמה. המחלקה הזו אחראית על מה שקורה *אחרי* שנמצא עדכון: הורדת
    // ה-installer, אימות שההורדה הושלמה, הרצתו במצב שקט (/VERYSILENT, ר'
    // build/installer/Setup.cs) ויציאה נקייה מהאפליקציה כדי שההתקנה תוכל
    // לדרוס את הקבצים הרצים.
    //
    // חוזה התנהגות מכוון (סימטרי ל-UpdateChecker):
    //  - כל שלב יכול להיכשל (רשת/דיסק מלא/UAC נדחה) - הקורא (MainWindow)
    //    אחראי ליפול חזרה בעדינות להתראה הידנית הקיימת (NotifyUpdateAvailable),
    //    אף פעם לא משאיר את המשתמש בלי דרך קדימה.
    //  - לא נוגעת ב-UI/Dispatcher בעצמה - כל המתודות כאן סינכרוניות ונועדו
    //    להיקרא מתוך Task.Run בצד הקורא, בדיוק כמו UpdateChecker.CheckSync.
    //  - "שקט" = בלי אינטראקציה עם המשתמש מצד ה-installer עצמו. UAC prompt
    //    (elevation) הוא בלתי נמנע כי ההתקנה כותבת ל-Program Files/HKLM -
    //    זה לא "כשל", זו הסכמה נדרשת של Windows, בדיוק כמו באשף הגרפי.
    public static class SelfUpdater
    {
        public enum DownloadResult { Ok, NoAssetUrl, NetworkError, SizeMismatch, DiskError }
        public enum LaunchResult { Ok, LaunchFailed, ElevationDeclined }

        public class DownloadOutcome
        {
            public DownloadResult Result;
            public string FilePath;
            public string ErrorDetail;
        }

        public class LaunchOutcome
        {
            public LaunchResult Result;
            public string ErrorDetail;
        }

        // מוריד את ה-installer שה-release מצביע עליו לתיקיית temp ייעודית,
        // ומאמת שההורדה הושלמה במלואה ע"י השוואת גודל הקובץ שהתקבל מול
        // הגודל שדיווח GitHub API (UpdateInfo.AssetSize) - לא checksum, אבל
        // תופס בבירור הורדה חתוכה (ניתוק רשת באמצע, timeout וכו').
        public static DownloadOutcome DownloadInstaller(UpdateChecker.UpdateInfo info, int timeoutMs = 120000)
        {
            if (info == null || string.IsNullOrEmpty(info.AssetDownloadUrl))
            {
                return new DownloadOutcome { Result = DownloadResult.NoAssetUrl, ErrorDetail = "No installer asset found on the release." };
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "AutoProcessTwin-Update");
            string destPath;
            try
            {
                Directory.CreateDirectory(tempDir);
                string fileName = "AutoProcessTwin-Setup-" + info.Version + ".exe";
                destPath = Path.Combine(tempDir, fileName);
                if (File.Exists(destPath)) { try { File.Delete(destPath); } catch { } }
            }
            catch (Exception ex)
            {
                return new DownloadOutcome { Result = DownloadResult.DiskError, ErrorDetail = ex.Message };
            }

            try
            {
                var req = (HttpWebRequest)WebRequest.Create(info.AssetDownloadUrl);
                req.UserAgent = "AutoProcessTwin-SelfUpdater/" + MainWindow.AppVersion;
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.Method = "GET";
                // GitHub redirects browser_download_url to a signed S3/Azure URL -
                // HttpWebRequest follows redirects by default (AllowAutoRedirect=true).

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var respStream = resp.GetResponseStream())
                using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                {
                    respStream.CopyTo(fileStream, 81920);
                }
            }
            catch (Exception ex)
            {
                try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
                bool isDisk = ex is IOException || ex is UnauthorizedAccessException;
                return new DownloadOutcome
                {
                    Result = isDisk ? DownloadResult.DiskError : DownloadResult.NetworkError,
                    ErrorDetail = ex.Message
                };
            }

            long actualSize;
            try { actualSize = new FileInfo(destPath).Length; }
            catch (Exception ex)
            {
                return new DownloadOutcome { Result = DownloadResult.DiskError, ErrorDetail = ex.Message };
            }

            // GitHub API לפעמים לא מדווח size (0) לassets ישנים/מוזרים - במקרה
            // הזה אין בסיס להשוואה, אז מוותרים על הבדיקה במקום לדחות הורדה
            // תקינה. כשיש size מדווח, כל אי-התאמה = כשל ברור.
            if (info.AssetSize > 0 && actualSize != info.AssetSize)
            {
                try { File.Delete(destPath); } catch { }
                return new DownloadOutcome
                {
                    Result = DownloadResult.SizeMismatch,
                    ErrorDetail = "Downloaded " + actualSize + " bytes, expected " + info.AssetSize + " bytes."
                };
            }

            return new DownloadOutcome { Result = DownloadResult.Ok, FilePath = destPath };
        }

        // מריץ את ה-installer שהורד עם /VERYSILENT. ה-installer עצמו לא מנסה
        // להתרומם (ר' Setup.cs Program.Main) - אנחנו מבקשים את ה-UAC prompt
        // כאן דרך Verb="runas", בדיוק כמו שהאשף הגרפי עושה relaunch לעצמו.
        // לא מחכים ל-exit code כאן בכוונה: ProcessStartInfo.Verb=runas + UAC
        // מייצר תהליך נפרד לגמרי (consent.exe מתווך), וממילא ה-installer צריך
        // שהאפליקציה הזו תסגר כדי לדרוס את ה-exe הרץ - אז אין "לחכות ולבדוק
        // exit code" אפשרי מתוך התהליך שעומד להיסגר. הכשל הריאלי היחיד שאפשר
        // לתפוס פה הוא דחיית UAC/כשל בהפעלת התהליך עצמו (Win32Exception).
        public static LaunchOutcome LaunchSilentInstaller(string installerPath)
        {
            try
            {
                var psi = new ProcessStartInfo(installerPath, "/VERYSILENT")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                return new LaunchOutcome { Result = LaunchResult.Ok };
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Win32 error 1223 = ERROR_CANCELLED - המשתמש לחץ "לא" ב-UAC.
                bool declined = ex.NativeErrorCode == 1223;
                return new LaunchOutcome
                {
                    Result = declined ? LaunchResult.ElevationDeclined : LaunchResult.LaunchFailed,
                    ErrorDetail = ex.Message
                };
            }
            catch (Exception ex)
            {
                return new LaunchOutcome { Result = LaunchResult.LaunchFailed, ErrorDetail = ex.Message };
            }
        }
    }
}
