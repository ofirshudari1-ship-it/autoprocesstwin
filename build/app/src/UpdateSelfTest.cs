using System;
using System.IO;
using System.Text;

namespace AutoProcessTwin
{
    // 0.5.10: בדיקות יחידה למסלול העדכון האוטומטי - בלי רשת, בלי UI, בלי
    // נגיעה בקונפיג/בלוג של המשתמש (UpdateLog מושבת). מריצים:
    //   AutoProcessTwin.exe --update-selftest > out.txt   (קוד יציאה 0 = הכל עבר)
    public static class UpdateSelfTest
    {
        private static int _pass, _fail;

        private static void Check(string name, bool ok)
        {
            if (ok) { _pass++; Console.WriteLine("PASS " + name); }
            else { _fail++; Console.WriteLine("FAIL " + name); }
        }

        private const string Hex64 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        public static int Run()
        {
            UpdateLog.Disabled = true;
            string tmp = Path.Combine(Path.GetTempPath(), "apt-update-selftest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tmp);
            try
            {
                // --- IsNewer ---
                Check("IsNewer 0.5.10 > 0.5.9 (numeric, not lexical)", UpdateChecker.IsNewer("0.5.10", "0.5.9"));
                Check("IsNewer same version is false", !UpdateChecker.IsNewer("0.5.9", "0.5.9"));
                Check("IsNewer older is false", !UpdateChecker.IsNewer("0.5.8", "0.5.9"));
                Check("IsNewer 1.0.0 > 0.9.99", UpdateChecker.IsNewer("1.0.0", "0.9.99"));
                Check("IsNewer garbage is false", !UpdateChecker.IsNewer("abc", "0.5.9"));

                // --- ParseDigest ---
                Check("ParseDigest accepts sha256:<64 hex>", UpdateChecker.ParseDigest("sha256:" + Hex64.ToUpperInvariant()) == Hex64);
                Check("ParseDigest rejects null/empty", UpdateChecker.ParseDigest(null) == null && UpdateChecker.ParseDigest("") == null);
                Check("ParseDigest rejects md5 prefix", UpdateChecker.ParseDigest("md5:" + Hex64) == null);
                Check("ParseDigest rejects wrong length", UpdateChecker.ParseDigest("sha256:abcd") == null);
                Check("ParseDigest rejects non-hex", UpdateChecker.ParseDigest("sha256:" + new string('z', 64)) == null);

                // --- ParseReleaseJson ---
                string json = "{\"tag_name\":\"v0.5.10\",\"html_url\":\"https://github.com/ofirshudari1-ship-it/autoprocesstwin/releases/tag/v0.5.10\",\"draft\":false,\"prerelease\":false,"
                    + "\"assets\":[{\"name\":\"source.zip\",\"size\":5,\"browser_download_url\":\"https://github.com/x/source.zip\"},"
                    + "{\"name\":\"AutoProcessTwin-Setup-0.5.10.exe\",\"size\":12345,\"digest\":\"sha256:" + Hex64 + "\","
                    + "\"browser_download_url\":\"https://github.com/ofirshudari1-ship-it/autoprocesstwin/releases/download/v0.5.10/AutoProcessTwin-Setup-0.5.10.exe\"}]}";
                var info = UpdateChecker.ParseReleaseJson(json, "0.5.9");
                Check("ParseReleaseJson finds newer release", info != null && info.Version == "0.5.10");
                Check("ParseReleaseJson picks installer asset (not source.zip)", info != null && info.AssetDownloadUrl != null && info.AssetDownloadUrl.EndsWith("AutoProcessTwin-Setup-0.5.10.exe"));
                Check("ParseReleaseJson reads size + sha256 digest", info != null && info.AssetSize == 12345 && info.AssetSha256 == Hex64);
                Check("ParseReleaseJson null when already current", UpdateChecker.ParseReleaseJson(json, "0.5.10") == null);
                Check("ParseReleaseJson ignores draft", UpdateChecker.ParseReleaseJson(json.Replace("\"draft\":false", "\"draft\":true"), "0.5.9") == null);
                Check("ParseReleaseJson ignores prerelease", UpdateChecker.ParseReleaseJson(json.Replace("\"prerelease\":false", "\"prerelease\":true"), "0.5.9") == null);
                var noAsset = UpdateChecker.ParseReleaseJson("{\"tag_name\":\"v0.6.0\",\"assets\":[]}", "0.5.9");
                Check("ParseReleaseJson with no assets -> update but no URL", noAsset != null && noAsset.AssetDownloadUrl == null);

                // --- IsTrustedDownloadUrl ---
                Check("Trusted: github.com https", SelfUpdater.IsTrustedDownloadUrl("https://github.com/a/b/releases/download/v1/x.exe"));
                Check("Trusted: objects.githubusercontent.com https", SelfUpdater.IsTrustedDownloadUrl("https://objects.githubusercontent.com/x"));
                Check("Untrusted: http", !SelfUpdater.IsTrustedDownloadUrl("http://github.com/a/b/x.exe"));
                Check("Untrusted: lookalike host", !SelfUpdater.IsTrustedDownloadUrl("https://github.com.evil.example/x.exe"));
                Check("Untrusted: notgithub.com suffix trick", !SelfUpdater.IsTrustedDownloadUrl("https://notgithub.com/x.exe"));
                Check("Untrusted: empty / garbage", !SelfUpdater.IsTrustedDownloadUrl("") && !SelfUpdater.IsTrustedDownloadUrl("not a url"));

                // --- VerifyInstallerFile ---
                byte[] good = new byte[4096];
                good[0] = (byte)'M'; good[1] = (byte)'Z';
                for (int i = 2; i < good.Length; i++) good[i] = (byte)(i * 7);
                string goodPath = Path.Combine(tmp, "good.exe");
                File.WriteAllBytes(goodPath, good);
                string goodSha;
                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    var h = sha.ComputeHash(good);
                    var sb = new StringBuilder();
                    foreach (var b in h) sb.Append(b.ToString("x2"));
                    goodSha = sb.ToString();
                }
                string d;
                Check("Verify OK: size + MZ + sha256", SelfUpdater.VerifyInstallerFile(goodPath, 4096, goodSha, out d) == SelfUpdater.DownloadResult.Ok);
                Check("Verify OK: no size, no digest (old release)", SelfUpdater.VerifyInstallerFile(goodPath, 0, null, out d) == SelfUpdater.DownloadResult.Ok);
                Check("Verify OK: digest upper-case", SelfUpdater.VerifyInstallerFile(goodPath, 4096, goodSha.ToUpperInvariant(), out d) == SelfUpdater.DownloadResult.Ok);
                Check("Verify rejects size mismatch", SelfUpdater.VerifyInstallerFile(goodPath, 4095, goodSha, out d) == SelfUpdater.DownloadResult.SizeMismatch);
                Check("Verify rejects wrong sha256", SelfUpdater.VerifyInstallerFile(goodPath, 4096, Hex64, out d) == SelfUpdater.DownloadResult.HashMismatch);

                string htmlPath = Path.Combine(tmp, "page.exe");
                File.WriteAllBytes(htmlPath, Encoding.UTF8.GetBytes("<html>" + new string('x', 3000) + "</html>"));
                Check("Verify rejects HTML masquerading as installer (MZ check)", SelfUpdater.VerifyInstallerFile(htmlPath, 0, null, out d) == SelfUpdater.DownloadResult.NotAnInstaller);

                string tinyPath = Path.Combine(tmp, "tiny.exe");
                File.WriteAllBytes(tinyPath, new byte[] { (byte)'M', (byte)'Z', 0, 0 });
                Check("Verify rejects tiny MZ stub (<1KB)", SelfUpdater.VerifyInstallerFile(tinyPath, 0, null, out d) == SelfUpdater.DownloadResult.NotAnInstaller);
                Check("Verify handles missing file without throwing", SelfUpdater.VerifyInstallerFile(Path.Combine(tmp, "nope.exe"), 0, null, out d) == SelfUpdater.DownloadResult.DiskError);

                // --- DownloadInstaller guards (no network reached) ---
                var noUrl = SelfUpdater.DownloadInstaller(new UpdateChecker.UpdateInfo { Version = "9.9.9" });
                Check("Download with no asset URL -> NoAssetUrl", noUrl.Result == SelfUpdater.DownloadResult.NoAssetUrl);
                var httpUrl = SelfUpdater.DownloadInstaller(new UpdateChecker.UpdateInfo { Version = "9.9.9", AssetDownloadUrl = "http://github.com/x.exe" });
                Check("Download refuses http:// URL", httpUrl.Result == SelfUpdater.DownloadResult.InsecureUrl);
                var foreign = SelfUpdater.DownloadInstaller(new UpdateChecker.UpdateInfo { Version = "9.9.9", AssetDownloadUrl = "https://example.com/x.exe" });
                Check("Download refuses non-GitHub host", foreign.Result == SelfUpdater.DownloadResult.InsecureUrl);

                // --- UpdateGate ---
                DateTime now = DateTime.UtcNow;
                Check("Gate: idle 5 min -> safe", UpdateGate.IsSafeToInstall(300, false, false, now, DateTime.MinValue));
                Check("Gate: active user (10s idle) -> not safe", !UpdateGate.IsSafeToInstall(10, false, false, now, DateTime.MinValue));
                Check("Gate: Windows session ending -> never safe", !UpdateGate.IsSafeToInstall(9999, true, false, now, DateTime.MinValue));
                Check("Gate: install already in progress -> not safe", !UpdateGate.IsSafeToInstall(9999, false, true, now, DateTime.MinValue));
                Check("Gate: inside 24h back-off after UAC decline -> not safe", !UpdateGate.IsSafeToInstall(9999, false, false, now, now.AddHours(24)));
                Check("Gate: back-off expired -> safe", UpdateGate.IsSafeToInstall(9999, false, false, now, now.AddSeconds(-1)));
                Check("Gate: check interval is 6 hours", UpdateGate.CheckInterval == TimeSpan.FromHours(6));
                Check("Gate: GetIdleSeconds does not throw and is >= 0", UpdateGate.GetIdleSeconds() >= 0);
            }
            catch (Exception ex)
            {
                _fail++;
                Console.WriteLine("FAIL unexpected exception: " + ex);
            }
            finally
            {
                try { Directory.Delete(tmp, true); } catch { }
            }

            Console.WriteLine("=== update self-test: " + _pass + " passed, " + _fail + " failed ===");
            return _fail == 0 ? 0 : 1;
        }
    }
}
