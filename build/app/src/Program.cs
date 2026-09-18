using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Threading;
using System.Windows;
using Microsoft.Win32;

namespace AutoProcessTwin
{
    public static class Program
    {
        public const string RegistryKeyName = "AutoProcessTwin";
        public const string ShortcutName = "AutoProcess Twin.lnk";
        // "Global\" (לא "Local\"/ברירת המחדל) - נתקלנו בפועל בשני תהליכים
        // ששניהם קיבלו createdNew=true על אותו שם מוטקס בלי הקידומת, מה
        // שמעיד על בידוד namespace בין sessions בסביבה הזו. הפתרון הסטנדרטי.
        private const string MutexName = @"Global\AutoProcessTwin-SingleInstance-9F3C2A";

        // שדה static, לא משתנה מקומי ב-Main! זה היה הבאג האמיתי (סבב 6/8/11
        // תיעדו "שני תהליכים מקבלים createdNew=true" בלי לאתר סיבת שורש):
        // Mutex שמוחזק רק במשתנה מקומי הוא "מת" מבחינת ה-JIT ברגע שהוא לא
        // נקרא שוב בהמשך המתודה (השורה if (!createdNew) הייתה השימוש האחרון) -
        // ה-GC יכול לאסוף אותו וה-finalizer של WaitHandle סוגר את ה-handle
        // ומשחרר את המוטקס בפועל, גם בזמן שה-process עדיין רץ. תוכנית קונסולה
        // מינימלית לבדיקה לא ביצעה מספיק הקצאות כדי שה-GC ירוץ בין יצירת
        // המוטקס לסיום התהליך - ולכן "עבדה תקין בבידוד" בדיוק כפי שתועד קודם,
        // בעוד שה-WPF app האמיתי מקצה המון (בניית כל ה-UI) ונותן ל-GC הזדמנות
        // לתפוס את המוטקס "המת" לפני ש-app.Run() חוסם. שדה static לא נאסף
        // לעולם עד סיום התהליך - זה התיקון האמיתי, לא רק ה-Global\ prefix.
        private static Mutex _instanceMutex;

        private static bool IsAdmin()
        {
            using (var id = WindowsIdentity.GetCurrent())
            {
                var p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }

        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        private const int SW_RESTORE = 9;

        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--self-uninstall")
            {
                // מ-0.5.1 ההתקנה ב-Program Files (HKLM Uninstall key) - מחיקת
                // התיקייה והרישום דורשת הרשאות מנהל, שהתהליך הזה (רץ בלי
                // manifest של elevation) לא בהכרח מקבל אוטומטית כשה-uninstall
                // מופעל מ"הגדרות > אפליקציות". מרימים UAC אם צריך.
                if (!IsAdmin())
                {
                    try
                    {
                        var psi = new ProcessStartInfo(Assembly.GetExecutingAssembly().Location)
                        {
                            Arguments = "--self-uninstall",
                            Verb = "runas",
                            UseShellExecute = true
                        };
                        Process.Start(psi);
                    }
                    catch { /* המשתמש ביטל את ה-UAC prompt - שום דבר לא נמחק */ }
                    return 0;
                }
                SelfUninstall();
                return 0;
            }

            if (args.Length > 0 && args[0] == "--selftest")
            {
                return SelfTest.Run();
            }

            // מיגרציית דאטה חד-פעמית (config/data/reports מהתקנות ישנות ליד
            // ה-exe -> %LOCALAPPDATA%\AutoProcessTwin). בלי תלות ב-UI, מוקדם
            // ככל האפשר - MainWindow/ProcessManager כבר מצפים ל-UserDataRoot.
            AppPaths.MigrateLegacyDataIfNeeded();

            // מונע הרצה כפולה: נתקלנו בפועל בשני תהליכים+שני recorders במקביל
            // כי open_application/לחיצה חוזרת על הקיצור פשוט פותחת עוד עותק.
            // אם כבר רץ - מנסים להעלות את החלון הקיים לחזית במקום לפתוח שני.
            bool createdNew;
            _instanceMutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                TryFocusExistingWindow();
                return 0;
            }

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                WriteCrashLog(ex != null ? ex.ToString() : e.ExceptionObject.ToString());
            };

            var app = new Application();
            // Keep app alive while splash is open and until MainWindow shows (STANDARDS §5)
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            app.DispatcherUnhandledException += (s, e) =>
            {
                WriteCrashLog(e.Exception.ToString());
                MessageBox.Show("Unexpected error:\n" + e.Exception.Message, "AutoProcess Twin",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };

            try
            {
                var splash = new SplashWindow();
                app.Run(splash); // SplashWindow creates MainWindow internally after 1.5s
            }
            catch (Exception ex)
            {
                WriteCrashLog(ex.ToString());
                MessageBox.Show("Startup error:\n" + ex.Message, "AutoProcess Twin",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return 0;
        }

        private static void WriteCrashLog(string details)
        {
            try
            {
                string logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AutoProcessTwin", "logs");
                Directory.CreateDirectory(logDir);
                string logPath = Path.Combine(logDir, "crash.log");
                string entry = string.Format("[{0}]\r\n{1}\r\n\r\n", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), details);
                File.AppendAllText(logPath, entry, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        private static void TryFocusExistingWindow()
        {
            try
            {
                var hWnd = FindWindow(null, "AutoProcess Twin");
                if (hWnd == IntPtr.Zero) return;
                if (IsIconic(hWnd)) ShowWindow(hWnd, SW_RESTORE);
                SetForegroundWindow(hWnd);
            }
            catch { }
        }

        private static void SelfUninstall()
        {
            try
            {
                var installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\');

                var desktopShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutName);
                if (File.Exists(desktopShortcut)) File.Delete(desktopShortcut);

                var startMenuShortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), ShortcutName);
                if (File.Exists(startMenuShortcut)) File.Delete(startMenuShortcut);

                // 0.5.1+: ה-installer רושם ב-HKLM (Program Files = per-machine
                // install). עדיין מנקים גם HKCU כרשת ביטחון להתקנות מ-0.5.0
                // ומטה שעודכנו במקום (ראה MigrateLegacyDataIfNeeded).
                try { Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + RegistryKeyName, false); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + RegistryKeyName, false); } catch { }

                // מתזמן מחיקת התיקייה אחרי שהתהליך הזה משחרר את הנעילה על עצמו
                // (EXE רץ לא יכול למחוק את התיקייה שהוא נמצא בה מיד).
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c ping 127.0.0.1 -n 3 >nul & rmdir /s /q \"" + installDir + "\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psi);
            }
            catch { }
        }
    }
}
