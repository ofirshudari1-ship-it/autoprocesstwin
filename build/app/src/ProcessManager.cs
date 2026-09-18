using System;
using System.Diagnostics;
using System.Text;

namespace AutoProcessTwin
{
    // מריץ ומנהל את ה-recorder של Node.js כתהליך-בן. עצירה היא Kill() ישיר -
    // לא graceful shutdown (SIGINT) - כי לא הצלחנו לאמת מסירת סיגנל אמינה
    // מ-.NET לתהליך קונסולה על Windows בלי טריקי GenerateConsoleCtrlEvent
    // מסובכים. ראה FINDINGS.md בשורש הפרויקט. אובדן הנתונים המקסימלי מהפסקה
    // כזו הוא שורת DB אחת שבתהליך כתיבה - SQLite לא מתכתם מזה.
    public class RecorderProcess
    {
        private Process _process;
        public event Action<string> OutputReceived;
        public event Action<int> Stopped;

        public bool IsRunning
        {
            get { return _process != null && !_process.HasExited; }
        }

        public void Start()
        {
            if (IsRunning) return;

            AppPaths.EnsureDataFolders();

            var psi = new ProcessStartInfo
            {
                FileName = AppPaths.FindNodeExe(),
                Arguments = "\"" + AppPaths.NodeScript + "\"",
                WorkingDirectory = AppPaths.AgentDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            _process = new Process { EnableRaisingEvents = true, StartInfo = psi };
            _process.OutputDataReceived += (s, e) => { if (e.Data != null) Raise(e.Data); };
            _process.ErrorDataReceived += (s, e) => { if (e.Data != null) Raise("[stderr] " + e.Data); };
            _process.Exited += (s, e) =>
            {
                int code = 0;
                try { code = _process.ExitCode; } catch { }
                if (Stopped != null) Stopped(code);
            };

            _process.Start();
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        public void Stop()
        {
            if (!IsRunning) return;
            try { _process.Kill(); } catch { }
        }

        private void Raise(string line)
        {
            if (OutputReceived != null) OutputReceived(line);
        }
    }

    public static class NodeRunner
    {
        // Windows command-line quoting per MSVC CRT rules: doubles backslashes
        // that precede a '"', then escapes the '"' itself. Never use simple
        // Replace("\"","\\\"") — it misses backslash-before-quote edge cases.
        public static string EscapeWindowsArg(string arg)
        {
            var sb = new StringBuilder();
            sb.Append('"');
            int bs = 0;
            foreach (char c in arg)
            {
                if (c == '\\') { bs++; }
                else if (c == '"') { for (int i = 0; i < bs * 2 + 1; i++) sb.Append('\\'); sb.Append('"'); bs = 0; }
                else { for (int i = 0; i < bs; i++) sb.Append('\\'); sb.Append(c); bs = 0; }
            }
            for (int i = 0; i < bs * 2; i++) sb.Append('\\');
            sb.Append('"');
            return sb.ToString();
        }

        // הרצה סינכרונית חד-פעמית של סקריפט Node (briefing.js וכד'), עם timeout.
        // מחזירה stdout+stderr משולבים. extraArgs (אופציונלי) מצורף אחרי נתיב
        // הסקריפט כמו שהוא - למשל "30" בשביל patterns-report.js שמצפה למספר ימים.
        public static string RunSync(string scriptPath, int timeoutMs = 30000, string extraArgs = null)
        {
            return RunSyncInternal(scriptPath, timeoutMs, extraArgs, null);
        }

        // כמו RunSync אבל מזריק משתני סביבה לתהליך-הבן - משמש להעברת מפתח API
        // מפוענח בזיכרון בלבד (DPAPI) מבלי לכתוב אותו בטקסט גלוי לדיסק.
        public static string RunSyncWithEnv(string scriptPath, int timeoutMs, System.Collections.Generic.Dictionary<string, string> envVars)
        {
            return RunSyncInternal(scriptPath, timeoutMs, null, envVars);
        }

        private static string RunSyncInternal(string scriptPath, int timeoutMs, string extraArgs, System.Collections.Generic.Dictionary<string, string> envVars)
        {
            var psi = new ProcessStartInfo
            {
                FileName = AppPaths.FindNodeExe(),
                Arguments = EscapeWindowsArg(scriptPath) + (string.IsNullOrEmpty(extraArgs) ? "" : " " + extraArgs),
                WorkingDirectory = AppPaths.AgentDir,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            if (envVars != null)
            {
                foreach (var kv in envVars)
                    psi.EnvironmentVariables[kv.Key] = kv.Value;
            }

            using (var p = Process.Start(psi))
            {
                string stdout = p.StandardOutput.ReadToEnd();
                string stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { }
                    return "Timeout after " + timeoutMs + "ms.\n" + stdout + stderr;
                }
                return stdout + stderr;
            }
        }
    }
}
