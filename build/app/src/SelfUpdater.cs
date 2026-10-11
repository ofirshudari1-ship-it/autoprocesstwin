using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;

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
        public enum DownloadResult { Ok, NoAssetUrl, NetworkError, SizeMismatch, DiskError, InsecureUrl, NotAnInstaller, HashMismatch }
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

        // 0.5.10: רק HTTPS ורק מארחי GitHub (github.com / *.github.com /
        // *.githubusercontent.com ל-redirect של ה-CDN). הקוד הזה מוריד ומריץ
        // קובץ הפעלה בלי שהמשתמש רואה - אסור שכתובת לא-מוצפנת או דומיין זר
        // (למשל מ-JSON שהוחלף בדרך) יגיעו לשם.
        public static bool IsTrustedDownloadUrl(string url)
        {
            Uri uri;
            if (string.IsNullOrEmpty(url) || !Uri.TryCreate(url, UriKind.Absolute, out uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps) return false;
            string host = uri.Host.ToLowerInvariant();
            return host == "github.com" || host.EndsWith(".github.com") ||
                   host == "githubusercontent.com" || host.EndsWith(".githubusercontent.com");
        }

        // אימות קובץ שהורד, לפי סדר: גודל (מול GitHub API), חתימת PE ("MZ" +
        // לפחות 1KB - תופס דף HTML של proxy/captive-portal במקום ה-installer),
        // ו-sha256 מול ה-digest ש-GitHub מחזיר (אם קיים). פונקציה טהורה, נבדקת
        // ב---update-selftest. מחזירה Ok או את סיבת הכשל.
        public static DownloadResult VerifyInstallerFile(string path, long expectedSize, string expectedSha256, out string detail)
        {
            detail = null;
            try
            {
                long actual = new FileInfo(path).Length;
                if (expectedSize > 0 && actual != expectedSize)
                {
                    detail = "Downloaded " + actual + " bytes, expected " + expectedSize + " bytes.";
                    return DownloadResult.SizeMismatch;
                }
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (fs.Length <= 1024 || fs.ReadByte() != 'M' || fs.ReadByte() != 'Z')
                    {
                        detail = "Downloaded file is not a Windows executable (" + actual + " bytes).";
                        return DownloadResult.NotAnInstaller;
                    }
                }
                if (!string.IsNullOrEmpty(expectedSha256))
                {
                    string actualHash;
                    using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (var sha = SHA256.Create())
                    {
                        var bytes = sha.ComputeHash(fs);
                        var sb = new System.Text.StringBuilder(bytes.Length * 2);
                        foreach (var b in bytes) sb.Append(b.ToString("x2"));
                        actualHash = sb.ToString();
                    }
                    if (!string.Equals(actualHash, expectedSha256, StringComparison.OrdinalIgnoreCase))
                    {
                        detail = "sha256 mismatch: got " + actualHash + ", expected " + expectedSha256;
                        return DownloadResult.HashMismatch;
                    }
                }
                return DownloadResult.Ok;
            }
            catch (Exception ex)
            {
                detail = ex.Message;
                return DownloadResult.DiskError;
            }
        }

        // מוריד את ה-installer שה-release מצביע עליו לתיקיית temp ייעודית,
        // ומאמת אותו (גודל + MZ + sha256, ר' VerifyInstallerFile). כל כשל נרשם
        // ל-update.log ומחזיר תוצאה ברורה - אף פעם לא מריצים קובץ שלא עבר אימות.
        public static DownloadOutcome DownloadInstaller(UpdateChecker.UpdateInfo info, int timeoutMs = 120000)
        {
            if (info == null || string.IsNullOrEmpty(info.AssetDownloadUrl))
            {
                return new DownloadOutcome { Result = DownloadResult.NoAssetUrl, ErrorDetail = "No installer asset found on the release." };
            }
            if (!IsTrustedDownloadUrl(info.AssetDownloadUrl))
            {
                UpdateLog.Write("download refused (untrusted URL): " + info.AssetDownloadUrl);
                return new DownloadOutcome { Result = DownloadResult.InsecureUrl, ErrorDetail = "Installer URL is not an https GitHub URL." };
            }

            string tempDir = Path.Combine(Path.GetTempPath(), "AutoProcessTwin-Update");
            string destPath;
            try
            {
                Directory.CreateDirectory(tempDir);
                // ניקוי installers יתומים מניסיונות קודמים (הורדה שנקטעה / עדכון שלא רץ).
                foreach (var old in Directory.GetFiles(tempDir, "AutoProcessTwin-Setup-*.exe"))
                {
                    try { File.Delete(old); } catch { }
                }
                string fileName = "AutoProcessTwin-Setup-" + info.Version + ".exe";
                destPath = Path.Combine(tempDir, fileName);
            }
            catch (Exception ex)
            {
                UpdateLog.Write("download setup failed: " + ex.Message);
                return new DownloadOutcome { Result = DownloadResult.DiskError, ErrorDetail = ex.Message };
            }

            try
            {
                UpdateChecker.EnsureModernTls();
                var req = (HttpWebRequest)WebRequest.Create(info.AssetDownloadUrl);
                req.UserAgent = "AutoProcessTwin-SelfUpdater/" + MainWindow.AppVersion;
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.Method = "GET";
                // GitHub redirects browser_download_url to a signed S3/Azure URL -
                // HttpWebRequest follows redirects by default (AllowAutoRedirect=true).

                using (var resp = (HttpWebResponse)req.GetResponse())
                {
                    // גם אחרי redirect: חייב להישאר HTTPS.
                    if (resp.ResponseUri == null || resp.ResponseUri.Scheme != Uri.UriSchemeHttps)
                        throw new WebException("redirected to a non-https URL");
                    using (var respStream = resp.GetResponseStream())
                    using (var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write))
                    {
                        respStream.CopyTo(fileStream, 81920);
                    }
                }
            }
            catch (Exception ex)
            {
                try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
                bool isDisk = ex is IOException || ex is UnauthorizedAccessException;
                UpdateLog.Write("download failed: " + ex.GetType().Name + ": " + ex.Message);
                return new DownloadOutcome
                {
                    Result = isDisk ? DownloadResult.DiskError : DownloadResult.NetworkError,
                    ErrorDetail = ex.Message
                };
            }

            // GitHub API לפעמים לא מדווח size (0) לassets ישנים/מוזרים - במקרה
            // הזה אין בסיס להשוואת גודל, אבל MZ (ו-sha256 אם קיים) עדיין נבדקים.
            string detail;
            var verify = VerifyInstallerFile(destPath, info.AssetSize, info.AssetSha256, out detail);
            if (verify != DownloadResult.Ok)
            {
                try { File.Delete(destPath); } catch { }
                UpdateLog.Write("download verification failed (" + verify + "): " + detail);
                return new DownloadOutcome { Result = verify, ErrorDetail = detail };
            }

            UpdateLog.Write("downloaded + verified " + Path.GetFileName(destPath) + " (sha256 " + (string.IsNullOrEmpty(info.AssetSha256) ? "not published" : "ok") + ")");
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
                var psi = new ProcessStartInfo(installerPath, "/VERYSILENT /RELAUNCH")
                {
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                UpdateLog.Write("launched silent installer: " + installerPath);
                return new LaunchOutcome { Result = LaunchResult.Ok };
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // Win32 error 1223 = ERROR_CANCELLED - המשתמש לחץ "לא" ב-UAC.
                bool declined = ex.NativeErrorCode == 1223;
                UpdateLog.Write("installer launch " + (declined ? "declined at UAC" : "failed") + ": " + ex.Message);
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
