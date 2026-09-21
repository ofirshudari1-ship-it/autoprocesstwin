using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace AutoProcessTwin
{
    // בדיקת עדכונים מול GitHub Releases (0.5.3) - ר' CHANGELOG.md.
    //
    // חוזה התנהגות מכוון:
    //  - GET לא-מאומת (אין טוקן בקוד! אסור - הריפו ציבורי, זה בכוונה לא
    //    דורש אימות) ל-releases/latest, עם User-Agent (חובה לפי GitHub API,
    //    בקשה בלי זה מקבלת 403).
    //  - השוואת semver פשוטה (major.minor.patch), בלי תלות חיצונית.
    //  - נכשל בשקט לגמרי - אין רשת/GitHub down/rate-limit/עדיין אין release
    //    בכלל לא אמורים להפריע לאף משתמש. אף פעם לא זורק כלפי מעלה.
    //  - לא חוסם UI: תמיד נקרא מ-Task.Run/async, אף פעם לא סינכרוני על ה-UI thread.
    //  - נדחה (MainWindow קורא עם delay) כדי לא להתחרות בעומס ה-startup.
    //  - פעם אחת לכל הרצת אפליקציה (MainWindow אחראי לדגל ה-"once per session").
    //  - opt-out דרך app.json["check_for_updates"] (ברירת מחדל: true).
    public static class UpdateChecker
    {
        public const string ApiUrl = "https://api.github.com/repos/ofirshudari1-ship-it/autoprocesstwin/releases/latest";
        public const string ReleasesPageUrl = "https://github.com/ofirshudari1-ship-it/autoprocesstwin/releases";

        public class UpdateInfo
        {
            public string Version;
            public string HtmlUrl;
            // 0.5.6: מסלול self-update (ר' SelfUpdater.cs) - שם/URL/גודל של
            // ה-asset שהוא ה-installer (AutoProcessTwin-Setup-X.Y.Z.exe) מתוך
            // release ה-GitHub, אם נמצא כזה. AssetSize משמש לאימות הורדה
            // (השוואה מול גודל הקובץ שהתקבל בפועל - לא checksum, אבל מספיק
            // כדי לתפוס הורדה חתוכה/נכשלת).
            public string AssetDownloadUrl;
            public long AssetSize;
        }

        // סינכרוני בכוונה - הקורא (MainWindow) עוטף ב-Task.Run, בדיוק כמו
        // NodeRunner.RunSync לשאר קריאות ה-agent, כדי לשמור על אותו דפוס.
        public static UpdateInfo CheckSync(string currentVersion, int timeoutMs = 8000)
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(ApiUrl);
                // GitHub REST API דוחה בקשות בלי User-Agent (403) - זו לא
                // בקשה מאומתת, רק header תקני.
                req.UserAgent = "AutoProcessTwin-UpdateChecker/" + currentVersion;
                req.Accept = "application/vnd.github+json";
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.Method = "GET";

                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var stream = resp.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string json = reader.ReadToEnd();
                    var serializer = new JavaScriptSerializer();
                    serializer.MaxJsonLength = int.MaxValue;
                    var data = serializer.DeserializeObject(json) as Dictionary<string, object>;
                    if (data == null) return null;

                    string tag = Json.GetString(data, "tag_name", null);
                    if (string.IsNullOrEmpty(tag)) return null;

                    string htmlUrl = Json.GetString(data, "html_url", ReleasesPageUrl);
                    string latest = tag.TrimStart('v', 'V');

                    if (!IsNewer(latest, currentVersion)) return null;

                    string assetUrl = null;
                    long assetSize = 0;
                    foreach (var item in Json.AsList(data.ContainsKey("assets") ? data["assets"] : null))
                    {
                        var asset = item as Dictionary<string, object>;
                        if (asset == null) continue;
                        string name = Json.GetString(asset, "name", "");
                        // ה-installer בלבד - לא source zip/tarball שGitHub מצרף אוטומטית.
                        if (name.StartsWith("AutoProcessTwin-Setup-", StringComparison.OrdinalIgnoreCase) &&
                            name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        {
                            assetUrl = Json.GetString(asset, "browser_download_url", null);
                            assetSize = (long)Json.GetNumber(asset, "size", 0);
                            break;
                        }
                    }

                    return new UpdateInfo
                    {
                        Version = latest,
                        HtmlUrl = htmlUrl,
                        AssetDownloadUrl = assetUrl,
                        AssetSize = assetSize
                    };
                }
            }
            catch
            {
                // שקט לגמרי בכוונה: אין אינטרנט, GitHub API למטה, rate-limit
                // (60 בקשות/שעה ל-IP לא-מאומת), או שעדיין אין release - אף
                // אחד מהם לא באג ואף אחד לא צריך לבלבל את המשתמש.
                return null;
            }
        }

        // major.minor.patch בלבד (בלי pre-release tags) - מספיק לצרכי הפרויקט.
        public static bool IsNewer(string latestVersion, string currentVersion)
        {
            try
            {
                var latest = ParseVersion(latestVersion);
                var current = ParseVersion(currentVersion);
                for (int i = 0; i < 3; i++)
                {
                    if (latest[i] != current[i]) return latest[i] > current[i];
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static int[] ParseVersion(string v)
        {
            var result = new int[3];
            if (string.IsNullOrEmpty(v)) return result;
            var parts = v.Split('.');
            for (int i = 0; i < 3 && i < parts.Length; i++)
            {
                int n;
                result[i] = int.TryParse(parts[i], out n) ? n : 0;
            }
            return result;
        }
    }
}
