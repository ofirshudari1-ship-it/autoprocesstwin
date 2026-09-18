using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using System.Windows.Forms;
using Microsoft.Win32;

namespace AutoProcessTwinSetup
{
    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(0x0F, 0x17, 0x2A);
        public static readonly Color Panel = Color.FromArgb(0x1E, 0x29, 0x3B);
        public static readonly Color Panel2 = Color.FromArgb(0x27, 0x33, 0x49);
        public static readonly Color HeaderBg = Color.FromArgb(0x0B, 0x12, 0x20);
        public static readonly Color HeaderText = Color.White;
        public static readonly Color HeaderSub = Color.FromArgb(0x94, 0xA3, 0xB8);
        public static readonly Color Accent = Color.FromArgb(0x10, 0xB9, 0x81);
        public static readonly Color AccentHover = Color.FromArgb(0x34, 0xD3, 0x99);
        public static readonly Color Text = Color.FromArgb(0xE2, 0xE8, 0xF0);
        public static readonly Color TextMuted = Color.FromArgb(0x94, 0xA3, 0xB8);
        public static readonly Color Warning = Color.FromArgb(0xFB, 0xBF, 0x24);
    }

    // Default is English for every tool (2026-09-14 decision). This installer had no
    // English option at all before - RightToLeft was hardcoded to Yes and every string
    // was a Hebrew literal. Toggle button in the header switches languages live.
    static class Loc
    {
        public static string Lang = "en";

        private static readonly System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>> T;

        static Loc()
        {
            var en = new System.Collections.Generic.Dictionary<string, string>();
            en.Add("toggle_lang", "עברית");
            en.Add("title_update", "Update ");
            en.Add("title_install", "Install ");
            en.Add("header_sub", "Automation Assistant + Digital Twin — Setup");
            en.Add("footer", "AutoProcess Twin — Phase 1 (local recording + briefing only, no autonomous actions)");
            en.Add("desc_same_version", "The installed version ({0}) is already up to date. You can reinstall to repair files.");
            en.Add("btn_reinstall", "Reinstall");
            en.Add("desc_update", "An existing installation was found (version {0}). Setup will update it to {1} in place, without deleting collected data (config/data/reports are kept).");
            en.Add("btn_update", "Update");
            en.Add("desc_fresh", "{0} version {1} — smart activity recording and a local morning briefing.");
            en.Add("btn_install", "Install");
            en.Add("bullet_patterns", "✔  Repeated-action detection — find what's worth automating");
            en.Add("bullet_briefing", "✔  Morning briefing — a daily summary of what happened");
            en.Add("bullet_local", "✔  100% local — nothing ever leaves your computer");
            en.Add("bullet_no_uac", "✔  Installs to Program Files, available to every user on this PC");
            en.Add("located_at", "Located at:");
            en.Add("will_install_at", "Will install to:");
            en.Add("chk_desktop", "Create a desktop shortcut");
            en.Add("chk_startmenu", "Create a Start Menu shortcut");
            en.Add("chk_startup", "Start with Windows (launch automatically at sign-in)");
            en.Add("node_warning", "⚠ Node.js was not found on this computer. Setup will continue, but recording won't work until you install Node.js (nodejs.org) - that's an external dependency, not bundled with this installer.");
            en.Add("appdata_note", "Installs to Program Files for all users - requires administrator rights (UAC). Your recordings, config and reports stay per-user under AppData\\Local, not in Program Files.");
            en.Add("btn_cancel", "Cancel");
            en.Add("installing", "Installing...");
            en.Add("finish_title_update", "Update complete");
            en.Add("finish_title_install", "Installation complete");
            en.Add("finish_desc", "{0} version {1} is installed and ready. You can remove it anytime from \"Settings > Apps\".");
            en.Add("chk_launch", "Launch {0} now");
            en.Add("btn_finish", "Finish");
            en.Add("install_error_title", "Setup Error");
            en.Add("install_error_body", "Setup error:\n{0}");
            en.Add("shortcut_desc", " - Automation Assistant and Digital Twin");
            en.Add("app_running_title", "AutoProcess Twin Setup");
            en.Add("app_running_body", "AutoProcess Twin is currently running.\n\nPlease close it before continuing the installation.");

            var he = new System.Collections.Generic.Dictionary<string, string>();
            he.Add("toggle_lang", "English");
            he.Add("title_update", "עדכון ");
            he.Add("title_install", "התקנת ");
            he.Add("header_sub", "סייען אוטומציה + כפיל דיגיטלי — התקנה");
            he.Add("footer", "AutoProcess Twin — Phase 1 (הקלטה מקומית + תדרוך, בלי פעולות אוטונומיות)");
            he.Add("desc_same_version", "הגרסה המותקנת ({0}) כבר עדכנית. אפשר להתקין מחדש כדי לתקן קבצים.");
            he.Add("btn_reinstall", "התקן מחדש");
            he.Add("desc_update", "נמצאה התקנה קיימת (גרסה {0}). ההתקנה תעדכן אותה לגרסה {1} במקום, בלי למחוק את הדאטה שנאסף (config/data/reports נשארים).");
            he.Add("btn_update", "עדכן");
            he.Add("desc_fresh", "{0} גרסה {1} — הקלטת פעילות חכמה ותדרוך בוקר מקומי.");
            he.Add("btn_install", "התקן");
            he.Add("bullet_patterns", "✔  זיהוי פעולות חוזרות — מציאת מה אפשר לאוטמט");
            he.Add("bullet_briefing", "✔  תדרוך בוקר — סיכום יומי של מה שקרה");
            he.Add("bullet_local", "✔  100% מקומי — שום דבר לא עוזב את המחשב");
            he.Add("bullet_no_uac", "✔  מותקן ב-Program Files, זמין לכל המשתמשים במחשב הזה");
            he.Add("located_at", "ממוקם ב:");
            he.Add("will_install_at", "יותקן ב:");
            he.Add("chk_desktop", "צור קיצור דרך בשולחן העבודה");
            he.Add("chk_startmenu", "צור קיצור דרך בתפריט התחל");
            he.Add("chk_startup", "הפעל עם Windows (הפעלה אוטומטית בכניסה)");
            he.Add("node_warning", "⚠ Node.js לא נמצא על המחשב הזה. ההתקנה תמשיך, אבל הקלטה לא תעבוד עד שתתקין Node.js (nodejs.org) - זה תלות חיצונית, לא נכלל בהתקנה הזו.");
            he.Add("appdata_note", "התקנה ב-Program Files עבור כל המשתמשים - דורשת הרשאות מנהל (UAC). ההקלטות, ההגדרות והדוחות שלך נשארים לפי משתמש תחת AppData\\Local, לא ב-Program Files.");
            he.Add("btn_cancel", "ביטול");
            he.Add("installing", "מתקין...");
            he.Add("finish_title_update", "העדכון הושלם");
            he.Add("finish_title_install", "ההתקנה הושלמה");
            he.Add("finish_desc", "{0} גרסה {1} מותקן ומוכן. אפשר להסיר אותו בכל עת מ-\"הגדרות > אפליקציות\".");
            he.Add("chk_launch", "הפעל את {0} עכשיו");
            he.Add("btn_finish", "סיום");
            he.Add("install_error_title", "שגיאה");
            he.Add("install_error_body", "שגיאת התקנה:\n{0}");
            he.Add("shortcut_desc", " - סייען אוטומציה וכפיל דיגיטלי");
            he.Add("app_running_title", "התקנת AutoProcess Twin");
            he.Add("app_running_body", "AutoProcess Twin פועל כרגע.\n\nיש לסגור אותו לפני שממשיכים בהתקנה.");

            T = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, string>>();
            T.Add("en", en);
            T.Add("he", he);
        }

        public static string S(string key) { return T[Lang][key]; }
        public static string F(string key, params object[] args) { return string.Format(T[Lang][key], args); }
    }

    static class UiHelpers
    {
        public static void RoundCorners(Control c, int radius)
        {
            if (c.Width <= 0 || c.Height <= 0) return;
            var path = new GraphicsPath();
            int d = Math.Min(radius * 2, Math.Min(c.Width, c.Height));
            path.AddArc(0, 0, d, d, 180, 90);
            path.AddArc(c.Width - d, 0, d, d, 270, 90);
            path.AddArc(c.Width - d, c.Height - d, d, d, 0, 90);
            path.AddArc(0, c.Height - d, d, d, 90, 90);
            path.CloseFigure();
            c.Region = new Region(path);
        }

        public static Button MakeButton(string text, Color back, Color fore, int width)
        {
            var btn = new Button
            {
                Text = text,
                Width = width,
                Height = 38,
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = fore,
                Font = new Font("Segoe UI", 10f),
                Cursor = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize = 0;
            var hover = ControlPaint.Light(back, 0.15f);
            btn.MouseEnter += (s, e) => btn.BackColor = hover;
            btn.MouseLeave += (s, e) => btn.BackColor = back;
            btn.Resize += (s, e) => RoundCorners(btn, 9);
            RoundCorners(btn, 9);
            return btn;
        }

        // Shared rounded-rect path builder for the owner-drawn theme controls below
        // (ThemedCheckBox, ThemedProgressBar) - same corner language as buttons/cards.
        public static GraphicsPath RoundedRectPath(Rectangle bounds, int radius)
        {
            int d = Math.Max(1, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(Math.Max(bounds.X, bounds.Right - d), bounds.Y, d, d, 270, 90);
            path.AddArc(Math.Max(bounds.X, bounds.Right - d), Math.Max(bounds.Y, bounds.Bottom - d), d, d, 0, 90);
            path.AddArc(bounds.X, Math.Max(bounds.Y, bounds.Bottom - d), d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // אייקון האפליקציה עצמה (ה"עין") מוטבע גם ב-setup.exe דרך -win32icon
        // בקומפילציה - נחלץ אותו מה-exe הנוכחי כדי שההתקנה תיראה עקבית.
        public static Icon GetBrandIcon()
        {
            try
            {
                var icon = Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                if (icon != null) return icon;
            }
            catch { }
            return SystemIcons.Application;
        }
    }

    // Native WinForms CheckBox/ProgressBar render with the OS visual style (a plain
    // white checkbox square, a system-blue marquee bar) no matter what BackColor is
    // set on the form - that's the "looks like generic Windows chrome" tell inside an
    // otherwise fully-themed dark/emerald window. These two owner-drawn replacements
    // use the exact same palette as Theme.cs / Setup.cs Theme so the whole installer
    // reads as one surface instead of a themed shell around default controls.
    public class ThemedCheckBox : CheckBox
    {
        public ThemedCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            AutoSize = false;
            Height = 22;
            Cursor = Cursors.Hand;
        }

        protected override void OnCheckedChanged(EventArgs e)
        {
            base.OnCheckedChanged(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var bg = new SolidBrush(Parent != null ? Parent.BackColor : Theme.Bg))
                g.FillRectangle(bg, ClientRectangle);

            const int box = 16;
            var boxRect = new Rectangle(0, (Height - box) / 2, box, box);
            using (var path = UiHelpers.RoundedRectPath(boxRect, 4))
            {
                using (var fill = new SolidBrush(Checked ? Theme.Accent : Theme.Panel2))
                    g.FillPath(fill, path);
                using (var pen = new Pen(Checked ? Theme.Accent : Theme.TextMuted, 1.3f))
                    g.DrawPath(pen, path);
            }
            if (Checked)
            {
                using (var checkPen = new Pen(Color.White, 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawLines(checkPen, new[]
                    {
                        new Point(boxRect.Left + 3, boxRect.Top + 8),
                        new Point(boxRect.Left + 6, boxRect.Top + 11),
                        new Point(boxRect.Left + 12, boxRect.Top + 4)
                    });
                }
            }

            var textRect = new Rectangle(box + 8, 0, Math.Max(0, Width - box - 8), Height);
            TextRenderer.DrawText(g, Text, Font, textRect, ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis);
        }
    }

    // Indeterminate progress indicator (setup work isn't measurably incremental) drawn
    // as a rounded accent-colored bar sliding across a themed track, replacing the
    // native ProgressBarStyle.Marquee control (always system blue, square corners).
    public class ThemedProgressBar : Control
    {
        private readonly Timer _timer;
        private float _pos = -0.35f;

        public ThemedProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 8;
            _timer = new Timer { Interval = 16 };
            _timer.Tick += (s, e) =>
            {
                _pos += 0.012f;
                if (_pos > 1.35f) _pos = -0.35f;
                Invalidate();
            };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            _timer.Start();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var track = new Rectangle(0, 0, Width, Height);
            int radius = Height / 2;

            using (var trackPath = UiHelpers.RoundedRectPath(track, radius))
            using (var trackBrush = new SolidBrush(Theme.Panel2))
                g.FillPath(trackBrush, trackPath);

            int barWidth = Math.Max(30, Width / 4);
            int x = (int)(_pos * (Width + barWidth)) - barWidth / 2;
            var barRect = new Rectangle(x, 0, barWidth, Height);

            var oldClip = g.Clip;
            using (var clipPath = UiHelpers.RoundedRectPath(track, radius))
            {
                g.SetClip(clipPath, CombineMode.Replace);
                using (var barPath = UiHelpers.RoundedRectPath(barRect, radius))
                using (var barBrush = new SolidBrush(Theme.Accent))
                    g.FillPath(barBrush, barPath);
            }
            g.Clip = oldClip;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _timer.Dispose();
            base.Dispose(disposing);
        }
    }

    public class SetupForm : Form
    {
        public const string AppName = "AutoProcess Twin";
        public const string AppVersion = "0.5.2";
        public const string ExeFileName = "AutoProcessTwin.exe";
        public const string ShortcutFileName = "AutoProcess Twin.lnk";
        private const string InstallDirName = "AutoProcessTwin";
        private const string PayloadResourceName = "AutoProcessTwinSetup.payload.zip";
        private const string UninstallKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + InstallDirName;

        private Panel _pageWelcome, _pageProgress, _pageFinish;
        private ThemedProgressBar _progressBar;
        private ThemedCheckBox _chkDesktop, _chkStartMenu, _chkLaunch, _chkStartWithWindows;
        private string _installDir;
        private bool _alreadyInstalled;
        private string _existingVersion;
        private bool _nodeFound;
        private string _legacyInstallDir;

        private Label _footer;

        public SetupForm()
        {
            _nodeFound = FindNode() != null;
            DetectExistingInstall();

            ClientSize = new Size(580, 520);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            Icon = UiHelpers.GetBrandIcon();
            Font = new Font("Segoe UI", 9.5f);

            Controls.Add(BuildHeader());

            _footer = new Label
            {
                ForeColor = Theme.TextMuted,
                Dock = DockStyle.Bottom,
                TextAlign = ContentAlignment.MiddleCenter,
                Height = 24,
                Font = new Font("Segoe UI", 8f)
            };
            Controls.Add(_footer);

            _pageWelcome = BuildWelcomePage();
            _pageProgress = BuildProgressPage();
            _pageFinish = BuildFinishPage();
            Controls.Add(_pageWelcome);
            Controls.Add(_pageProgress);
            Controls.Add(_pageFinish);
            _pageProgress.Visible = false;
            _pageFinish.Visible = false;

            ApplyLanguage();
        }

        /// <summary>(Re)applies RightToLeft layout and every static string for the
        /// currently selected Loc.Lang. Called once at startup and again whenever
        /// the language toggle button is clicked.</summary>
        private void ApplyLanguage()
        {
            RightToLeft = Loc.Lang == "he" ? RightToLeft.Yes : RightToLeft.No;
            RightToLeftLayout = Loc.Lang == "he";

            Text = (_alreadyInstalled ? Loc.S("title_update") : Loc.S("title_install")) + AppName;
            _footer.Text = Loc.S("footer");

            // Rebuild the welcome/finish pages in place - simplest way to keep every
            // string, layout mirroring (RTL bullet positions etc.) and button handler
            // wiring in sync after a language switch, without duplicating that logic.
            var oldWelcome = _pageWelcome;
            var oldFinish = _pageFinish;
            bool welcomeWasVisible = oldWelcome == null || oldWelcome.Visible;
            bool finishWasVisible = oldFinish != null && oldFinish.Visible;

            _pageWelcome = BuildWelcomePage();
            _pageFinish = BuildFinishPage();
            Controls.Add(_pageWelcome);
            Controls.Add(_pageFinish);
            if (oldWelcome != null) { Controls.Remove(oldWelcome); oldWelcome.Dispose(); }
            if (oldFinish != null) { Controls.Remove(oldFinish); oldFinish.Dispose(); }

            if (_pageProgress != null) _pageProgress.Controls[0].Text = Loc.S("installing");

            ShowPage(finishWasVisible ? _pageFinish : (welcomeWasVisible ? _pageWelcome : _pageProgress));
        }

        private static string FindNode()
        {
            string[] candidates =
            {
                Environment.GetEnvironmentVariable("ProgramFiles") + @"\nodejs\node.exe",
                Environment.GetEnvironmentVariable("ProgramFiles(x86)") + @"\nodejs\node.exe",
            };
            foreach (var c in candidates)
                if (!string.IsNullOrEmpty(c) && File.Exists(c)) return c;

            foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try
                {
                    var candidate = Path.Combine(dir, "node.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch { }
            }
            return null;
        }

        // נתיב HKCU הישן (<=0.5.0, לפני שההתקנה עברה ל-Program Files/HKLM
        // בעקבות החלטת בעלים מפורשת - ראה CHANGELOG 0.5.1). עדיין נקרא כדי
        // לזהות שדרוג מהתקנת AppData ישנה ולנקות אותה, כדי שלא יישארו שני
        // עותקים מותקנים במקביל.
        private static readonly string LegacyInstallDirDefault = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs", InstallDirName);

        private void DetectExistingInstall()
        {
            _installDir = SetupForm.DefaultInstallDir();
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(UninstallKeyPath))
                {
                    if (key != null)
                    {
                        _existingVersion = key.GetValue("DisplayVersion") as string;
                        var existingDir = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(existingDir)) _installDir = existingDir;
                        _alreadyInstalled = true;
                    }
                }
            }
            catch { }

            if (_alreadyInstalled) return;

            // אין התקנת Program Files - בודקים אם יש התקנת AppData ישנה
            // (<=0.5.0) כדי להציג "עדכון" נכון ולנקות אותה אחרי ההתקנה החדשה.
            try
            {
                using (var legacyKey = Registry.CurrentUser.OpenSubKey(UninstallKeyPath))
                {
                    if (legacyKey != null)
                    {
                        _existingVersion = legacyKey.GetValue("DisplayVersion") as string;
                        var existingDir = legacyKey.GetValue("InstallLocation") as string;
                        _legacyInstallDir = !string.IsNullOrEmpty(existingDir) ? existingDir : LegacyInstallDirDefault;
                        _alreadyInstalled = true;
                        // _installDir נשאר ב-Program Files (ברירת המחדל החדשה) -
                        // זו "התקנה מחדש" לוקיישן חדש, לא עדכון במקום.
                    }
                }
            }
            catch { }
        }

        private Control BuildHeader()
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 78, BackColor = Theme.HeaderBg };
            var logo = new PictureBox
            {
                Image = UiHelpers.GetBrandIcon().ToBitmap(),
                Size = new Size(42, 42),
                Location = new Point(22, 18),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            var title = new Label { Text = AppName, ForeColor = Theme.HeaderText, Font = new Font("Segoe UI", 15f, FontStyle.Bold), AutoSize = true, Location = new Point(74, 14) };
            var sub = new Label { Name = "HeaderSub", Text = Loc.S("header_sub"), ForeColor = Theme.HeaderSub, Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(74, 46) };
            var btnLang = UiHelpers.MakeButton(Loc.S("toggle_lang"), Theme.Panel2, Theme.Text, 80);
            btnLang.Height = 26;
            btnLang.Font = new Font("Segoe UI", 8f);
            btnLang.Location = new Point(478, 12);
            btnLang.Click += (s, e) =>
            {
                Loc.Lang = Loc.Lang == "he" ? "en" : "he";
                sub.Text = Loc.S("header_sub");
                btnLang.Text = Loc.S("toggle_lang");
                ApplyLanguage();
            };
            panel.Controls.Add(logo);
            panel.Controls.Add(title);
            panel.Controls.Add(sub);
            panel.Controls.Add(btnLang);
            return panel;
        }

        private Panel BuildWelcomePage()
        {
            var p = new Panel { Location = new Point(0, 78), Size = new Size(580, 520 - 78 - 24), BackColor = Theme.Bg };

            string descText, buttonText;
            bool sameVersion = _alreadyInstalled && _existingVersion == AppVersion;
            if (sameVersion)
            {
                descText = Loc.F("desc_same_version", _existingVersion);
                buttonText = Loc.S("btn_reinstall");
            }
            else if (_alreadyInstalled)
            {
                descText = Loc.F("desc_update", _existingVersion ?? "?", AppVersion);
                buttonText = Loc.S("btn_update");
            }
            else
            {
                descText = Loc.F("desc_fresh", AppName, AppVersion);
                buttonText = Loc.S("btn_install");
            }

            var desc = new Label { Text = descText, Location = new Point(24, 16), Size = new Size(530, 36), ForeColor = Theme.Text, Font = new Font("Segoe UI", 10f) };
            p.Controls.Add(desc);

            // Feature bullets (only for fresh install)
            if (!_alreadyInstalled)
            {
                string[] bullets = {
                    Loc.S("bullet_patterns"),
                    Loc.S("bullet_briefing"),
                    Loc.S("bullet_local"),
                    Loc.S("bullet_no_uac"),
                };
                int by = 56;
                foreach (var b in bullets)
                {
                    p.Controls.Add(new Label { Text = b, Location = new Point(32, by), AutoSize = true, ForeColor = Theme.TextMuted, Font = new Font("Segoe UI", 9f) });
                    by += 20;
                }
            }

            var lblPath = new Label { Text = (_alreadyInstalled ? Loc.S("located_at") : Loc.S("will_install_at")), Location = new Point(24, 140), AutoSize = true, ForeColor = Theme.Text };
            p.Controls.Add(lblPath);
            var txtPath = new TextBox { Text = _installDir, Location = new Point(24, 162), Width = 530, ReadOnly = true, BackColor = Theme.Panel2, ForeColor = Theme.Text, BorderStyle = BorderStyle.FixedSingle };
            p.Controls.Add(txtPath);

            _chkDesktop = new ThemedCheckBox { Text = Loc.S("chk_desktop"), Checked = true, Location = new Point(24, 200), Width = 500, ForeColor = Theme.Text };
            _chkStartMenu = new ThemedCheckBox { Text = Loc.S("chk_startmenu"), Checked = true, Location = new Point(24, 224), Width = 500, ForeColor = Theme.Text };
            _chkStartWithWindows = new ThemedCheckBox { Text = Loc.S("chk_startup"), Checked = false, Location = new Point(24, 248), Width = 500, ForeColor = Theme.Text };
            p.Controls.Add(_chkDesktop);
            p.Controls.Add(_chkStartMenu);
            p.Controls.Add(_chkStartWithWindows);

            int noteY = 280;
            if (!_nodeFound)
            {
                var warn = new Label
                {
                    Text = Loc.S("node_warning"),
                    Location = new Point(24, noteY),
                    Size = new Size(530, 40),
                    ForeColor = Theme.Warning
                };
                p.Controls.Add(warn);
                noteY += 46;
            }

            var note = new Label
            {
                Text = Loc.S("appdata_note"),
                Location = new Point(24, noteY),
                Size = new Size(530, 24),
                ForeColor = Theme.TextMuted
            };
            p.Controls.Add(note);

            var btnInstall = UiHelpers.MakeButton(buttonText, Theme.Accent, Color.White, 130);
            btnInstall.Location = new Point(24, noteY + 40);
            btnInstall.Click += async (s, e) => await DoInstall();
            p.Controls.Add(btnInstall);

            var btnCancel = UiHelpers.MakeButton(Loc.S("btn_cancel"), Theme.Panel2, Theme.Text, 100);
            btnCancel.Location = new Point(160, noteY + 40);
            btnCancel.Click += (s, e) => Close();
            p.Controls.Add(btnCancel);

            return p;
        }

        private Panel BuildProgressPage()
        {
            var p = new Panel { Location = new Point(0, 78), Size = new Size(580, 520 - 78 - 24), BackColor = Theme.Bg };
            var label = new Label { Text = Loc.S("installing"), Location = new Point(24, 130), AutoSize = true, ForeColor = Theme.Text, Font = new Font("Segoe UI", 10f) };
            _progressBar = new ThemedProgressBar { Location = new Point(24, 168), Size = new Size(530, 8) };
            p.Controls.Add(label);
            p.Controls.Add(_progressBar);
            return p;
        }

        private Panel BuildFinishPage()
        {
            var p = new Panel { Location = new Point(0, 78), Size = new Size(580, 520 - 78 - 24), BackColor = Theme.Bg };
            var title = new Label { Text = _alreadyInstalled ? Loc.S("finish_title_update") : Loc.S("finish_title_install"), Font = new Font("Segoe UI", 13f, FontStyle.Bold), ForeColor = Theme.Text, Location = new Point(24, 30), AutoSize = true };
            var desc = new Label
            {
                Text = Loc.F("finish_desc", AppName, AppVersion),
                Location = new Point(24, 66), Size = new Size(530, 40), ForeColor = Theme.TextMuted
            };
            _chkLaunch = new ThemedCheckBox { Text = Loc.F("chk_launch", AppName), Checked = true, Location = new Point(24, 116), Width = 500, ForeColor = Theme.Text };
            p.Controls.Add(title);
            p.Controls.Add(desc);
            p.Controls.Add(_chkLaunch);

            var btnFinish = UiHelpers.MakeButton(Loc.S("btn_finish"), Theme.Accent, Color.White, 130);
            btnFinish.Location = new Point(24, 170);
            btnFinish.Click += (s, e) =>
            {
                if (_chkLaunch.Checked)
                {
                    try { Process.Start(Path.Combine(_installDir, ExeFileName)); } catch { }
                }
                Close();
            };
            p.Controls.Add(btnFinish);
            return p;
        }

        private void ShowPage(Panel page)
        {
            _pageWelcome.Visible = page == _pageWelcome;
            _pageProgress.Visible = page == _pageProgress;
            _pageFinish.Visible = page == _pageFinish;
        }

        /// <summary>Blocks (with a retry/cancel prompt) until AutoProcessTwin isn't
        /// running, or the user cancels. Same pattern as OptiGuard/SnapAI's installers -
        /// prevents the update from failing partway through on a locked exe.</summary>
        private bool EnsureAppNotRunning()
        {
            while (Process.GetProcessesByName("AutoProcessTwin").Length > 0)
            {
                var result = MessageBox.Show(this,
                    Loc.S("app_running_body"), Loc.S("app_running_title"),
                    MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                if (result != DialogResult.Retry)
                    return false;
            }
            return true;
        }

        private async System.Threading.Tasks.Task DoInstall()
        {
            if (!EnsureAppNotRunning())
                return;

            ShowPage(_pageProgress);
            try
            {
                string installDir = _installDir;
                bool desktop = _chkDesktop.Checked, startMenu = _chkStartMenu.Checked;
                bool startWithWin = _chkStartWithWindows != null && _chkStartWithWindows.Checked;
                string exePath = await System.Threading.Tasks.Task.Run(() => PerformInstall(installDir, desktop, startMenu));
                if (startWithWin)
                {
                    try
                    {
                        using (var key = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                        {
                            if (key != null) key.SetValue("AutoProcessTwin", "\"" + exePath + "\"");
                        }
                    }
                    catch { }
                }
                if (!string.IsNullOrEmpty(_legacyInstallDir))
                {
                    CleanupLegacyInstall(_legacyInstallDir);
                }
                ShowPage(_pageFinish);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, Loc.F("install_error_body", ex.Message), Loc.S("install_error_title"), MessageBoxButtons.OK, MessageBoxIcon.Error);
                ShowPage(_pageWelcome);
            }
        }

        // מסירה את התקנת ה-AppData הישנה (<=0.5.0) אחרי שההתקנה החדשה
        // ב-Program Files הצליחה, כדי שלא יישארו שני קיצורי דרך/שני exe
        // מותקנים. חשוב: מעתיקים קודם config/data/reports (כולל twin.db) אל
        // %LOCALAPPDATA%\AutoProcessTwin ולפני שמוחקים את legacyDir - AppPaths.
        // MigrateLegacyDataIfNeeded בצד האפליקציה הוא רשת ביטחון נוספת, לא
        // הדרך היחידה, כי הוא רץ רק בהפעלה הבאה ולא לפני שהתיקייה הישנה נמחקת.
        // best-effort לגמרי - אף כישלון כאן לא חוסם/מפיל את ההתקנה.
        private static void CleanupLegacyInstall(string legacyDir)
        {
            try
            {
                string userRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AutoProcessTwin");
                CopyLegacyDataDir(Path.Combine(legacyDir, "data"), Path.Combine(userRoot, "data"));
                CopyLegacyDataDir(Path.Combine(legacyDir, "reports"), Path.Combine(userRoot, "reports"));
                CopyLegacyDataDir(Path.Combine(legacyDir, "config"), Path.Combine(userRoot, "config"));
            }
            catch { }

            try { Registry.CurrentUser.DeleteSubKeyTree(UninstallKeyPath, false); } catch { }
            try
            {
                using (var runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (runKey != null) runKey.DeleteValue("AutoProcessTwin", false);
                }
            }
            catch { }
            try { if (Directory.Exists(legacyDir)) Directory.Delete(legacyDir, true); } catch { }
        }

        private static void CopyLegacyDataDir(string sourceDir, string destDir)
        {
            if (!Directory.Exists(sourceDir)) return;
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                if (Path.GetFileName(file).IndexOf(".example.", StringComparison.OrdinalIgnoreCase) >= 0) continue;
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

        // הלוגיקה בפועל, בלי תלות ב-UI - כדי שגם --silent-install (בדיקה
        // אוטומטית, ראה Program.Main) וגם האשף הגרפי ישתמשו באותו קוד בדיוק.
        public static string PerformInstall(string installDir, bool desktopShortcut, bool startMenuShortcut)
        {
            Directory.CreateDirectory(installDir);
            ExtractPayload(installDir);

            string exePath = Path.Combine(installDir, ExeFileName);

            if (desktopShortcut)
            {
                CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), ShortcutFileName), exePath, installDir);
            }
            if (startMenuShortcut)
            {
                string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                Directory.CreateDirectory(startMenuDir);
                CreateShortcut(Path.Combine(startMenuDir, ShortcutFileName), exePath, installDir);
            }

            RegisterUninstall(installDir, exePath);
            AddFirewallRule(exePath);
            return exePath;
        }

        // §11.8 — best-effort firewall rule (requires admin; silently skipped if not elevated).
        // The app is local-only but Node.js may open a local IPC socket that Windows Firewall
        // could prompt about on first run; adding the rule here prevents that popup.
        private static void AddFirewallRule(string exePath)
        {
            try
            {
                var p = new Process();
                p.StartInfo.FileName = "netsh";
                p.StartInfo.Arguments = "advfirewall firewall add rule"
                    + " name=\"AutoProcess Twin\""
                    + " dir=in action=allow"
                    + " program=\"" + exePath + "\""
                    + " enable=yes profile=private,domain";
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.UseShellExecute = false;
                p.Start();
                p.WaitForExit(5000);
            }
            catch { }
        }

        // 0.5.1: ברירת מחדל היא Program Files (החלטת בעלים מפורשת - כל
        // הכלים בפורטפוליו מותקנים שם כברירת מחדל, ראה CHANGELOG). Program.Main
        // מוודא הרשאות מנהל לפני שהאשף בכלל נפתח.
        public static string DefaultInstallDir()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), InstallDirName);
        }

        // הערה: ניסינו כאן בעבר "לתקן" כשל התקנה עם קידומת \\?\ (עקיפת
        // MAX_PATH), בהנחה שגויה שמדובר בנתיב ארוך מדי. בפועל הנתיב הארוך
        // ביותר ב-node_modules הוא ~73 תווים - רחוק מ-260. הקידומת עצמה היא
        // שגרמה לכשל (ArgumentException דרך FileIOPermission legacy check
        // ב-.NET Framework, שלא תומך ב-\\?\ עם System.IO הרגיל) - גם על נתיב
        // קצר לגמרי כמו agent\package-lock.json. הוסרה. הקוד הפשוט למטה נבדק
        // ועובד.
        private static void ExtractPayload(string destDir)
        {
            var asm = Assembly.GetExecutingAssembly();
            using (var stream = asm.GetManifestResourceStream(PayloadResourceName))
            {
                if (stream == null) throw new Exception("Payload resource not found: " + PayloadResourceName);
                using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                {
                    foreach (var entry in archive.Entries)
                    {
                        try
                        {
                            string relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
                            if (string.IsNullOrEmpty(entry.Name) && relative.EndsWith(Path.DirectorySeparatorChar.ToString()))
                            {
                                Directory.CreateDirectory(Path.Combine(destDir, relative));
                                continue;
                            }
                            // לא דורסים config/*.json אמיתי שכבר קיים (רק *.example.json בפיילוד ממילא,
                            // אבל שומרים על הכלל הזה כרשת ביטחון לעדכונים עתידיים).
                            string destPath = Path.Combine(destDir, relative);
                            if (Path.GetFileName(destPath).Equals("privacy.json", StringComparison.OrdinalIgnoreCase) && File.Exists(destPath)) continue;
                            if (Path.GetFileName(destPath).Equals("guardrails.json", StringComparison.OrdinalIgnoreCase) && File.Exists(destPath)) continue;

                            Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                            entry.ExtractToFile(destPath, true);
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Entry failed: [" + entry.FullName + "] -> " + ex.GetType().Name + ": " + ex.Message, ex);
                        }
                    }
                }
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetExe, string workingDir)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
            Type scType = shortcut.GetType();
            scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe });
            scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
            scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { targetExe + ",0" });
            scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { SetupForm.AppName + Loc.S("shortcut_desc") });
            scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }

        // HKLM (לא HKCU) מ-0.5.1: Program Files היא התקנה per-machine - "הגדרות
        // > אפליקציות" מצפה לרשומה תחת HKLM\...\Uninstall עבור כלים שמותקנים
        // שם. דורש שהתהליך רץ מורם (ראה Program.Main).
        private static void RegisterUninstall(string installDir, string exePath)
        {
            using (var key = Registry.LocalMachine.CreateSubKey(UninstallKeyPath))
            {
                key.SetValue("DisplayName", SetupForm.AppName);
                key.SetValue("Publisher", "Ofir Shudari");
                key.SetValue("DisplayVersion", SetupForm.AppVersion);
                key.SetValue("InstallLocation", installDir);
                key.SetValue("DisplayIcon", exePath);
                key.SetValue("UninstallString", "\"" + exePath + "\" --self-uninstall");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", 51200, RegistryValueKind.DWord);
            }
        }
    }

    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--silent-install")
            {
                // מסלול לא-גרפי לבדיקה אוטומטית / פריסה מתוסרטת: אותה בדיוק
                // לוגיקת התקנה כמו האשף, בלי חלון. מדפיס תוצאה ל-stdout.
                // בכוונה לא מרימים UAC כאן - "silent" אומר בלי אינטראקציה עם
                // המשתמש בכלל, ו-UAC prompt הוא אינטראקציה. הקורא (CI/סקריפט
                // בדיקה) אחראי להריץ את עצמו מורם אם רוצה לבדוק התקנת
                // Program Files; אם לא - הכישלון (UnauthorizedAccessException)
                // נתפס ומודפס בבירור כ-ERROR, בלי ניסיון שקט/מטעה לעקוף אותו.
                try
                {
                    string dir = SetupForm.PerformInstall(SetupForm.DefaultInstallDir(), true, true);
                    Console.WriteLine("OK " + dir);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR " + ex.Message);
                    Environment.Exit(1);
                }
                return;
            }

            // האשף הגרפי כותב ל-Program Files ול-HKLM\...\Uninstall - מריצים
            // מורם. אותה שיטת relaunch-with-runas כמו במתקין של OptiGuard
            // (sibling tool בפורטפוליו): לא manifest, relaunch מפורש כי הבנייה
            // כאן היא csc.exe גולמי בלי embed של app.manifest.
            if (!IsAdmin())
            {
                try
                {
                    var psi = new ProcessStartInfo(Application.ExecutablePath) { Verb = "runas" };
                    Process.Start(psi);
                }
                catch { /* המשתמש ביטל את ה-UAC prompt */ }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }

        private static bool IsAdmin()
        {
            using (var id = WindowsIdentity.GetCurrent())
            {
                var p = new WindowsPrincipal(id);
                return p.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
