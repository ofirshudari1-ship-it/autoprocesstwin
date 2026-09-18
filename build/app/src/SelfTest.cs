using System;
using System.Threading;
using System.Web.Script.Serialization;

namespace AutoProcessTwin
{
    // מסלול אבחון לא-גרפי: מריץ בדיוק את אותה לוגיקה שכפתור "התחל הקלטה"
    // מפעיל ב-MainWindow (אותו RecorderProcess), בלי צורך בגישה למסך.
    // נוסף אחרי שהתגלה שהגרסה המותקנת לא תפסה אף אירוע - כדי לוודא מהצד הזה
    // (לא רק מבדיקת node ישירה מ-Bash) שהחיווט בפועל ב-C# עובד.
    public static class SelfTest
    {
        public static int Run()
        {
            Console.WriteLine("=== AutoProcess Twin self-test ===");
            Console.WriteLine("Root: " + AppPaths.Root);
            Console.WriteLine("Agent dir exists: " + System.IO.Directory.Exists(AppPaths.AgentDir));
            Console.WriteLine("Node exe: " + AppPaths.FindNodeExe());

            var recorder = new RecorderProcess();
            var gotOutput = false;
            recorder.OutputReceived += line => { gotOutput = true; Console.WriteLine("[recorder] " + line); };
            recorder.Stopped += code => Console.WriteLine("[recorder] stopped, exit=" + code);

            Console.WriteLine("Starting recorder...");
            try
            {
                recorder.Start();
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: Start() threw: " + ex);
                return 1;
            }

            Thread.Sleep(8000);

            Console.WriteLine("Stopping recorder...");
            recorder.Stop();
            Thread.Sleep(1000);

            if (!gotOutput)
            {
                Console.WriteLine("FAIL: no output at all from the recorder process (didn't even print its startup line).");
                return 1;
            }

            string statusJson;
            try
            {
                statusJson = NodeRunner.RunSync(AppPaths.StatusScript, 10000).Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: status query threw: " + ex.Message);
                return 1;
            }

            Console.WriteLine("Status: " + statusJson);
            var data = (System.Collections.Generic.Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(statusJson);
            double eventsToday = Json.GetNumber(data, "eventsToday", 0);

            if (eventsToday < 1)
            {
                Console.WriteLine("FAIL: eventsToday = " + eventsToday + " (expected >= 1).");
                return 1;
            }

            Console.WriteLine("PASS (recording): eventsToday = " + eventsToday);

            // --- Briefing: same call GenerateBriefingAndShow() makes ---
            Console.WriteLine("Generating briefing...");
            try
            {
                string output = NodeRunner.RunSync(AppPaths.BriefingScript, 30000);
                Console.WriteLine("[briefing] " + output.Trim());
                var reportFiles = System.IO.Directory.GetFiles(AppPaths.ReportsDir, "*.md");
                if (reportFiles.Length == 0)
                {
                    Console.WriteLine("FAIL: briefing ran but no .md file appeared in " + AppPaths.ReportsDir);
                    return 1;
                }
                string content = System.IO.File.ReadAllText(reportFiles[0]);
                string lastApp = Json.GetString(data, "lastApp", null);
                if (!string.IsNullOrEmpty(lastApp) && !content.Contains(lastApp))
                {
                    Console.WriteLine("WARN: report doesn't mention '" + lastApp + "' (the actually-captured app) - check it manually: " + reportFiles[0]);
                }
                Console.WriteLine("PASS (briefing): " + reportFiles[0] + " (" + content.Length + " chars)");
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: briefing generation threw: " + ex.Message);
                return 1;
            }

            // --- Config round-trip: same calls the Privacy/Guardrails Save buttons make ---
            Console.WriteLine("Testing config save/load round-trip...");
            try
            {
                var privacy = ConfigStore.LoadPrivacy();
                privacy["_selftest_marker"] = "ok-" + DateTime.Now.Ticks;
                ConfigStore.SavePrivacy(privacy);
                var reloaded = ConfigStore.LoadPrivacy();
                if (!reloaded.ContainsKey("_selftest_marker"))
                {
                    Console.WriteLine("FAIL: privacy.json round-trip lost data.");
                    return 1;
                }
                privacy.Remove("_selftest_marker");
                ConfigStore.SavePrivacy(privacy);

                var guardrails = ConfigStore.LoadGuardrails();
                Console.WriteLine("PASS (config): privacy+guardrails load/save round-trip OK. execution_mode_default=" +
                    Json.GetString(guardrails, "execution_mode_default", "?"));
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: config round-trip threw: " + ex.Message);
                return 1;
            }

            // --- Node-side logic that needs deterministic inputs: privacy filter
            // (known app/title, not whatever's actually on screen) and retention
            // purge (a fake old row, not real data). See agent/src/selftest.js. ---
            Console.WriteLine("Running Node-side logic tests (privacy filter, retention purge)...");
            try
            {
                string script = System.IO.Path.Combine(AppPaths.AgentDir, "src", "selftest.js");
                string output = NodeRunner.RunSync(script, 15000);
                Console.WriteLine(output.Trim());
                if (!output.Contains("SELFTEST ALL PASS"))
                {
                    Console.WriteLine("FAIL: agent/src/selftest.js reported a failure (see output above).");
                    return 1;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL: Node-side selftest threw: " + ex.Message);
                return 1;
            }

            Console.WriteLine("=== ALL PASS ===");
            return 0;
        }
    }
}
