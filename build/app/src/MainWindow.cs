using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace AutoProcessTwin
{
    public class MainWindow : Window
    {
        public const string AppVersion = "0.5.2";

        private readonly RecorderProcess _recorder = new RecorderProcess();
        private readonly List<string> _logLines = new List<string>();

        private Border _statusDot;
        private TextBlock _statusText;
        private Button _toggleButton;
        private TextBlock _eventsTodayText, _capturedTodayText, _filteredTodayText, _lastActivityText;
        private ListBox _logListBox;
        private TextBlock _countdownText;
        private Border _onboardingBanner;
        private readonly DispatcherTimer _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private DateTime _nextTickAt = DateTime.MinValue;

        // רענון אוטומטי - במקום כפתורי "עדכן X" ידניים (פידבק: הניווט לא היה
        // ברור). שני טיימרים בקצב שונה כי RefreshPatterns כבד יותר.
        private readonly DispatcherTimer _statusAutoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        private readonly DispatcherTimer _patternsAutoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(3) };
        private TextBlock _autoRefreshStatusText;

        // Spotlight - התובנה הכי חשובה, מוצגת בבית בלי צורך לחפש בטאבים,
        // בהשראת "Daily Focus Coach" של RescueTime וה-signals ב-task mining.
        private Border _spotlightCard;
        private TextBlock _spotlightHeadline, _spotlightBody;
        private StackPanel _topPatternsPreview;

        // פרטיות
        private ListBox _excludedAppsList, _excludedKeywordsList;
        private TextBox _newAppBox, _newKeywordBox;
        private Slider _intervalSlider;
        private TextBlock _intervalValueLabel;
        private TextBox _retentionBox;
        private CheckBox _screenshotEnabledBox, _ocrEnabledBox;
        private Dictionary<string, object> _privacyConfig;

        // Guardrails
        private Slider _discountSlider;
        private TextBlock _discountValueLabel;
        private TextBox _maxCloseBox, _currencyBox;
        private ComboBox _executionModeCombo;
        private ListBox _decisionRulesList, _escalationList;
        private TextBox _ruleDescBox, _ruleCondBox, _ruleActionBox;
        private TextBox _newEscalationBox;
        private List<Dictionary<string, object>> _decisionRules;
        private Dictionary<string, object> _guardrailsConfig;

        // תדרוך
        private ListBox _reportsList;
        private RichTextBox _reportView;
        private TextBlock _reportEmptyHint;

        // General settings UI refs
        private ComboBox _languageCombo, _themeCombo;
        private CheckBox _startWithWindowsBox, _autoRecordBox;
        private Dictionary<string, object> _appConfig;

        // בדיקת עדכונים (0.5.3) - opt-out checkbox + תצוגת סטטוס בטאב עזרה.
        // ר' UpdateChecker.cs לחוזה ההתנהגות המלא (GET לא-מאומת, שקט לגמרי
        // בכשל, פעם אחת לכל הרצה, מושהה כדי לא להתחרות ב-startup).
        private CheckBox _checkUpdatesBox;
        private TextBlock _updateStatusText;
        private bool _updateCheckDoneThisSession;
        private UpdateChecker.UpdateInfo _pendingUpdate;

        // Tray (§12.1, §12.2 STANDARDS)
        private System.Windows.Forms.NotifyIcon _tray;
        private bool _firstTrayHide = true;
        private bool _exitRequested = false;

        // AI (אופציונלי, כבוי כברירת מחדל - ראה BuildAiSettingsTab)
        private CheckBox _aiEnabledBox;
        private TextBox _aiEndpointBox, _aiModelBox;
        private PasswordBox _aiKeyBox;
        private Dictionary<string, object> _aiConfig;

        // המלצות אוטומציה
        private StackPanel _patternsListPanel;
        private TextBlock _patternsSummaryText, _patternsEmptyHint;
        // פילוח לפי קטגוריה - "לאיזה סוג עבודה הולך הזמן בסה"כ" (report-only
        // rollup across ALL patterns, not just automation candidates - see
        // patterns.js categoryBreakdown). Populated in RefreshPatterns.
        private StackPanel _categoryBreakdownPanel;
        private StackPanel _ignoredPatternsPanel;
        private TextBlock _ignoredHeader;
        private ComboBox _patternsRangeCombo;
        private int _patternsRangeDays = 30;

        // סיכום מנהלים AI - כפתור ידני בכוונה (לא בתוך auto-refresh), כי זו
        // קריאת רשת חיצונית עם עלות/זמן תגובה, לא ניתוח מקומי חינמי כמו השאר.
        private TextBlock _aiSummaryText;
        private Button _aiSummaryBtn;

        public MainWindow()
        {
            try { _appConfig = ConfigStore.LoadApp(); }
            catch { _appConfig = new Dictionary<string, object>(); }
            if (_appConfig == null) _appConfig = new Dictionary<string, object>();
            var lang = Json.GetString(_appConfig, "language", "en");
            var themeName = Json.GetString(_appConfig, "theme", "dark");
            Strings.Language = lang;
            Theme.Load(themeName == "light" ? "Light" : "Dark");

            Title = "AutoProcess Twin";
            Width = 1040;
            Height = 720;
            MinWidth = 860;
            MinHeight = 560;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Theme.Get("BgBrush");
            FontFamily = new FontFamily("Segoe UI");
            FlowDirection = lang == "he" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Icon = LoadIcon();

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var header = BuildHeader();
            Grid.SetRow(header, 0);
            root.Children.Add(header);

            var tabs = BuildTabs();
            Grid.SetRow(tabs, 1);
            root.Children.Add(tabs);

            var footer = BuildFooter();
            Grid.SetRow(footer, 2);
            root.Children.Add(footer);

            Content = root;

            _recorder.OutputReceived += OnRecorderOutput;
            _recorder.Stopped += OnRecorderStopped;

            _countdownTimer.Tick += (s, e) => UpdateCountdownText();

            LoadPrivacyIntoUi();
            LoadGuardrailsIntoUi();
            LoadAiIntoUi();
            LoadAppConfigIntoUi();
            RefreshReportsList();
            RefreshStatus();
            RefreshPatterns();
            UpdateToggleButton();

            _statusAutoRefreshTimer.Tick += (s, e) => RefreshStatus();
            _patternsAutoRefreshTimer.Tick += (s, e) => RefreshPatterns();
            _statusAutoRefreshTimer.Start();
            _patternsAutoRefreshTimer.Start();

            // Tray (§12.1 §12.2) + Window state (§12.3)
            InitTray();
            RestoreWindowState();
            Closing += OnWindowClosing;

            Loaded += (s, e) =>
            {
                MaybeShowOnboarding();
                if (Json.GetBool(_appConfig, "auto_record", false) && !_recorder.IsRunning)
                    ToggleRecording();
                MaybeCheckForUpdates();
            };
        }

        // נדחה ~5 שניות כדי לא להתחרות בעומס האתחול (onboarding/recorder/
        // סטטוס ראשוני), async כדי לא לחסום את ה-UI thread, ופעם אחת בלבד
        // לכל הרצת אפליקציה (לא בכל 30ms auto-refresh, לא בכל פתיחת טאב).
        private async void MaybeCheckForUpdates()
        {
            if (_updateCheckDoneThisSession) return;
            if (!Json.GetBool(_appConfig, "check_for_updates", true)) return;
            _updateCheckDoneThisSession = true;

            try { await Task.Delay(5000); } catch { return; }

            UpdateChecker.UpdateInfo info = null;
            try
            {
                info = await Task.Run(new Func<UpdateChecker.UpdateInfo>(
                    () => UpdateChecker.CheckSync(AppVersion)));
            }
            catch { /* שקט - ר' UpdateChecker.CheckSync */ }

            if (info == null) return;
            _pendingUpdate = info;
            NotifyUpdateAvailable(info);
        }

        // התראה לא-חוסמת: balloon tip מה-tray (כבר קיים לכל הרצה, §12.1/12.2)
        // + עדכון הטקסט בטאב "עזרה". שום MessageBox/דיאלוג - המשתמש ממשיך
        // לעבוד בלי הפרעה, ובוחר מתי (אם בכלל) לפתוח את עמוד ההורדה.
        private void NotifyUpdateAvailable(UpdateChecker.UpdateInfo info)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (_updateStatusText != null)
                    {
                        bool isHe = Strings.Language == "he";
                        _updateStatusText.Text = isHe
                            ? ("גרסה חדשה זמינה: " + info.Version + " (יש לך " + AppVersion + ") - לחצו לפתיחת עמוד ההורדה")
                            : ("A new version is available: " + info.Version + " (you have " + AppVersion + ") - click to open the download page");
                        _updateStatusText.Cursor = System.Windows.Input.Cursors.Hand;
                        _updateStatusText.MouseLeftButtonUp += (s, e) => OpenUpdatePage();
                    }

                    if (_tray != null)
                    {
                        _tray.BalloonTipClicked += (s, e) => OpenUpdatePage();
                        _tray.ShowBalloonTip(6000,
                            "AutoProcess Twin",
                            Strings.Language == "he"
                                ? ("גרסה " + info.Version + " זמינה להורדה - לחצו כאן")
                                : ("Version " + info.Version + " is available - click here"),
                            System.Windows.Forms.ToolTipIcon.Info);
                    }
                }
                catch { }
            }));
        }

        // כפתור "בדוק עכשיו" ידני בטאב עזרה - בניגוד ל-MaybeCheckForUpdates
        // הזה לא מוגבל ל"פעם אחת בהרצה" ולא ממתין 5 שניות, כי המשתמש ביקש
        // אותו במפורש. עדיין async/לא-חוסם ועדיין שקט בכשל.
        private async void ManualCheckForUpdates()
        {
            if (_updateStatusText != null)
                _updateStatusText.Text = Strings.Language == "he" ? "בודק..." : "Checking...";

            UpdateChecker.UpdateInfo info = null;
            try
            {
                info = await Task.Run(new Func<UpdateChecker.UpdateInfo>(
                    () => UpdateChecker.CheckSync(AppVersion)));
            }
            catch { }

            if (info != null)
            {
                _pendingUpdate = info;
                NotifyUpdateAvailable(info);
            }
            else if (_updateStatusText != null)
            {
                _updateStatusText.Text = Strings.Language == "he"
                    ? ("אתם מעודכנים (v" + AppVersion + ").")
                    : ("You're up to date (v" + AppVersion + ").");
            }
        }

        private void OpenUpdatePage()
        {
            try
            {
                string url = _pendingUpdate != null && !string.IsNullOrEmpty(_pendingUpdate.HtmlUrl)
                    ? _pendingUpdate.HtmlUrl : UpdateChecker.ReleasesPageUrl;
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        // §12.1 §12.2 — Tray icon, minimize-to-tray on X
        private void InitTray()
        {
            try
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);

                string iconPath = System.IO.Path.Combine(AppPaths.Root, "AppIcon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    try { icon = new System.Drawing.Icon(iconPath); } catch { }
                }

                _tray = new System.Windows.Forms.NotifyIcon
                {
                    Icon = icon,
                    Text = Strings.TrayTooltip,
                    Visible = true,
                };

                // Left-click: show window (§12.2)
                _tray.Click += (s, e) =>
                {
                    var me = (System.Windows.Forms.MouseEventArgs)e;
                    if (me.Button == System.Windows.Forms.MouseButtons.Left)
                        ShowFromTray();
                };

                // Right-click menu (§12.2)
                var menu = new System.Windows.Forms.ContextMenuStrip();
                var recItem = new System.Windows.Forms.ToolStripMenuItem(Strings.TrayMenuStartRec);
                recItem.Click += (s, e) =>
                {
                    ShowFromTray();
                    ToggleRecording();
                };
                _recorder.Stopped += (exitCode) => UpdateTray(recItem);
                menu.Opening += (s, e) =>
                {
                    recItem.Text = _recorder.IsRunning ? Strings.TrayMenuStopRec : Strings.TrayMenuStartRec;
                };
                menu.Items.Add(recItem);
                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                var openItem = new System.Windows.Forms.ToolStripMenuItem(Strings.TrayMenuOpen);
                openItem.Click += (s, e) => ShowFromTray();
                menu.Items.Add(openItem);
                menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
                var exitItem = new System.Windows.Forms.ToolStripMenuItem(Strings.TrayMenuExit);
                exitItem.Click += (s, e) => RequestExit();
                menu.Items.Add(exitItem);
                _tray.ContextMenuStrip = menu;
            }
            catch { }
        }

        private void UpdateTray(System.Windows.Forms.ToolStripMenuItem recItem)
        {
            try
            {
                Dispatcher.Invoke(new Action(() =>
                {
                    if (_tray == null) return;
                    _tray.Text = _recorder.IsRunning ? Strings.TrayTooltipRecording : Strings.TrayTooltip;
                    recItem.Text = _recorder.IsRunning ? Strings.TrayMenuStopRec : Strings.TrayMenuStartRec;
                }));
            }
            catch { }
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void RequestExit()
        {
            _exitRequested = true;
            if (_recorder.IsRunning) _recorder.Stop();
            _statusAutoRefreshTimer.Stop();
            _patternsAutoRefreshTimer.Stop();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            Close();
        }

        // §12.1 — X minimizes to tray; exit only via tray menu
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_exitRequested)
            {
                e.Cancel = true;
                Hide();
                if (_firstTrayHide && _tray != null)
                {
                    _firstTrayHide = false;
                    _tray.ShowBalloonTip(4000, Strings.TrayBalloonTitle, Strings.TrayBalloonText,
                        System.Windows.Forms.ToolTipIcon.Info);
                }
                return;
            }
            // Real exit
            SaveWindowState();
        }

        // §12.3 — window state persistence
        private void RestoreWindowState()
        {
            try
            {
                double left = Json.GetNumber(_appConfig, "window_left", -1);
                double top = Json.GetNumber(_appConfig, "window_top", -1);
                double width = Json.GetNumber(_appConfig, "window_width", -1);
                double height = Json.GetNumber(_appConfig, "window_height", -1);
                bool maximized = Json.GetBool(_appConfig, "window_maximized", false);

                if (width > 0 && height > 0)
                {
                    Width = Math.Max(MinWidth, width);
                    Height = Math.Max(MinHeight, height);
                }
                if (left >= 0 && top >= 0)
                {
                    // Validate that position is on a visible screen
                    var screen = System.Windows.SystemParameters.WorkArea;
                    if (left < screen.Right - 100 && top < screen.Bottom - 100)
                    {
                        Left = left;
                        Top = top;
                    }
                }
                if (maximized)
                    WindowState = WindowState.Maximized;
            }
            catch { }
        }

        private void SaveWindowState()
        {
            try
            {
                if (_appConfig == null) _appConfig = new Dictionary<string, object>();
                _appConfig["window_left"] = Left;
                _appConfig["window_top"] = Top;
                _appConfig["window_width"] = Width;
                _appConfig["window_height"] = Height;
                _appConfig["window_maximized"] = WindowState == WindowState.Maximized;
                ConfigStore.SaveApp(_appConfig);
            }
            catch { }
        }

        private void MaybeShowOnboarding()
        {
            if (System.IO.File.Exists(AppPaths.OnboardingMarkerPath)) return;
            ShowOnboardingWizard();
        }

        private void ShowOnboardingWizard()
        {
            var wizard = new OnboardingWizard(this);
            wizard.ShowDialog();
            try
            {
                AppPaths.EnsureDataFolders();
                System.IO.File.WriteAllText(AppPaths.OnboardingMarkerPath, DateTime.Now.ToString("o"));
            }
            catch { }

            if (wizard.StartRecordingRequested && !_recorder.IsRunning)
            {
                ToggleRecording();
            }
        }

        private static BitmapImage LoadIcon()
        {
            try
            {
                string path = Path.Combine(AppPaths.Root, "AppIcon.ico");
                if (!File.Exists(path)) return null;
                var img = new BitmapImage();
                img.BeginInit();
                img.UriSource = new Uri(path);
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.EndInit();
                return img;
            }
            catch { return null; }
        }

        // ---------- Header ----------

        private Border BuildHeader()
        {
            var border = new Border { Background = Theme.Get("HeaderBgBrush"), Padding = new Thickness(24, 16, 24, 16) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var titleStack = new StackPanel { Orientation = Orientation.Vertical };
            titleStack.Children.Add(new TextBlock
            {
                Text = "AutoProcess Twin",
                Foreground = Theme.Get("HeaderTextBrush"),
                FontSize = 22,
                FontWeight = FontWeights.Bold,
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = Strings.HeaderSubtitle,
                Foreground = Theme.Get("HeaderSubTextBrush"),
                FontSize = 12,
                Margin = new Thickness(0, 4, 0, 0),
            });
            Grid.SetColumn(titleStack, 0);
            grid.Children.Add(titleStack);

            var statusStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            var statusInner = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            _statusDot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = Theme.Get("TextMutedBrush"), Margin = new Thickness(0, 0, 8, 0) };
            _statusText = new TextBlock { Text = "לא פעיל", Foreground = Theme.Get("HeaderTextBrush"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            statusInner.Children.Add(_statusDot);
            statusInner.Children.Add(_statusText);
            statusStack.Children.Add(statusInner);

            _countdownText = new TextBlock
            {
                Text = "",
                Foreground = Theme.Get("HeaderSubTextBrush"),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 16, 0),
            };
            statusStack.Children.Add(_countdownText);

            _toggleButton = new Button { Width = 160, Style = (Style)Theme.GetStyle("AccentButtonStyle") };
            _toggleButton.Click += (s, e) => ToggleRecording();
            statusStack.Children.Add(_toggleButton);

            Grid.SetColumn(statusStack, 1);
            grid.Children.Add(statusStack);

            border.Child = grid;
            return border;
        }

        private void ToggleRecording()
        {
            if (_recorder.IsRunning)
            {
                _recorder.Stop();
                _countdownTimer.Stop();
                _countdownText.Text = "";
            }
            else
            {
                AppendLog("--- מתחיל הקלטה ---");
                _recorder.Start();
                _nextTickAt = DateTime.Now.AddSeconds(_intervalSlider.Value);
                _countdownTimer.Start();
                UpdateCountdownText();
            }
            UpdateToggleButton();
        }

        private void UpdateCountdownText()
        {
            if (!_recorder.IsRunning)
            {
                _countdownText.Text = "";
                return;
            }
            var remaining = _nextTickAt - DateTime.Now;
            int secs = (int)Math.Max(0, Math.Ceiling(remaining.TotalSeconds));
            _countdownText.Text = secs > 0 ? ("התצפית הבאה בעוד " + secs + "s") : "מצלם עכשיו...";
        }

        private void UpdateToggleButton()
        {
            bool running = _recorder.IsRunning;
            _toggleButton.Content = running ? "⏹  עצור הקלטה" : "▶  התחל הקלטה";
            _toggleButton.Style = (Style)Theme.GetStyle(running ? "DangerButtonStyle" : "AccentButtonStyle");
            _statusDot.Background = Theme.Get(running ? "AccentBrush" : "TextMutedBrush");
            _statusText.Text = running ? "מקליט..." : "לא פעיל";
        }

        private void OnRecorderOutput(string line)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AppendLog(line);
                if (line.Contains("captured (") || line.Contains("filtered ("))
                {
                    RefreshStatus();
                    _nextTickAt = DateTime.Now.AddSeconds(_intervalSlider.Value);
                    UpdateCountdownText();
                }
            }));
        }

        private void OnRecorderStopped(int exitCode)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AppendLog("--- התהליך נעצר (exit " + exitCode + ") ---");
                _countdownTimer.Stop();
                _countdownText.Text = "";
                UpdateToggleButton();
            }));
        }

        private void AppendLog(string line)
        {
            _logLines.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + line);
            while (_logLines.Count > 300) _logLines.RemoveAt(0);
            if (_logListBox != null)
            {
                _logListBox.Items.Add(_logLines[_logLines.Count - 1]);
                while (_logListBox.Items.Count > 300) _logListBox.Items.RemoveAt(0);
                if (_logListBox.Items.Count > 0) _logListBox.ScrollIntoView(_logListBox.Items[_logListBox.Items.Count - 1]);
            }
        }

        // ---------- Tabs ----------

        private TabControl BuildTabs()
        {
            var tabs = new TabControl { Style = (Style)Theme.GetStyle("ModernTabControlStyle"), Margin = new Thickness(20, 16, 20, 0) };

            tabs.Items.Add(MakeTab(Strings.TabHome, BuildDashboardTab()));
            tabs.Items.Add(MakeTab(Strings.TabPatterns, BuildPatternsTab()));
            tabs.Items.Add(MakeTab(Strings.TabSettings, BuildSettingsTab()));
            tabs.Items.Add(MakeTab(Strings.TabHelp, BuildAboutTab()));

            return tabs;
        }

        private TabItem MakeTab(string header, UIElement content)
        {
            var item = new TabItem { Header = header, Style = (Style)Theme.GetStyle("ModernTabItemStyle") };
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = content, Padding = new Thickness(4, 16, 4, 16) };
            item.Content = scroll;
            return item;
        }

        // הגדרות הפרטיות ו-Guardrails נדירות (עורכים פעם, לא כל יום) - איחוד
        // לטאב אחד עם תת-טאבים במקום שני טאבים ראשיים, בעקבות פידבק שהניווט
        // הרגיש עמוס.
        private UIElement BuildSettingsTab()
        {
            var inner = new TabControl { Style = (Style)Theme.GetStyle("ModernTabControlStyle") };
            inner.Items.Add(MakeInnerTab(Strings.TabGeneral, BuildGeneralSettingsTab()));
            inner.Items.Add(MakeInnerTab(Strings.TabPrivacy, BuildPrivacyTab()));
            inner.Items.Add(MakeInnerTab(Strings.TabGuardrails, BuildGuardrailsTab()));
            inner.Items.Add(MakeInnerTab(Strings.TabAi, BuildAiSettingsTab()));
            return inner;
        }

        private TabItem MakeInnerTab(string header, UIElement content)
        {
            var item = new TabItem { Header = header, Style = (Style)Theme.GetStyle("ModernTabItemStyle") };
            item.Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = content, Padding = new Thickness(4, 16, 4, 4) };
            return item;
        }

        // ---------- General Settings Tab ----------

        private static Border MakeSettingsDivider()
        {
            return new Border { Height = 1, Background = Theme.Get("BorderColorBrush"), Margin = new Thickness(0, 16, 0, 16) };
        }

        private static TextBlock MakeSectionHeader(string text)
        {
            return new TextBlock
            {
                Text = text.ToUpperInvariant(),
                Foreground = Theme.Get("AccentBrush"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12),
            };
        }

        private static StackPanel MakeCheckRow(CheckBox chk, string desc)
        {
            var row = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 12) };
            chk.Foreground = Theme.Get("TextBrush");
            chk.Margin = new Thickness(0, 0, 0, 2);
            row.Children.Add(chk);
            if (!string.IsNullOrEmpty(desc))
                row.Children.Add(new TextBlock { Text = desc, Foreground = Theme.Get("TextMutedBrush"), FontSize = 11, Margin = new Thickness(20, 0, 0, 0) });
            return row;
        }

        private static StackPanel MakeComboRow(string label, ComboBox combo)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            row.Children.Add(new TextBlock { Text = label, Foreground = Theme.Get("TextBrush"), Width = 180, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(combo);
            return row;
        }

        private static TextBlock MakeSecurityBullet(string text, bool ok = true)
        {
            return new TextBlock
            {
                Text = (ok ? "✔  " : "⚠  ") + text,
                Foreground = ok ? Theme.Get("AccentBrush") : Theme.Get("WarningBrush"),
                FontSize = 12,
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private UIElement BuildGeneralSettingsTab()
        {
            var stack = new StackPanel { Margin = new Thickness(20, 12, 20, 20) };

            // ── Section: Appearance ──
            stack.Children.Add(MakeSectionHeader(Strings.LabelLanguage + " / " + Strings.LabelTheme));

            _languageCombo = new ComboBox { Width = 200, Style = (Style)Theme.GetStyle("ModernComboStyle") };
            _languageCombo.Items.Add(Strings.LangHe);
            _languageCombo.Items.Add(Strings.LangEn);
            stack.Children.Add(MakeComboRow(Strings.LabelLanguage, _languageCombo));

            _themeCombo = new ComboBox { Width = 200, Style = (Style)Theme.GetStyle("ModernComboStyle") };
            _themeCombo.Items.Add(Strings.ThemeDark);
            _themeCombo.Items.Add(Strings.ThemeLight);
            stack.Children.Add(MakeComboRow(Strings.LabelTheme, _themeCombo));

            stack.Children.Add(MakeSettingsDivider());

            // ── Section: Startup / Behavior ──
            stack.Children.Add(MakeSectionHeader(Strings.SectionBehavior));

            _startWithWindowsBox = new CheckBox { Content = Strings.LabelStartup };
            stack.Children.Add(MakeCheckRow(_startWithWindowsBox, Strings.StartupDesc));

            _autoRecordBox = new CheckBox { Content = Strings.LabelAutoRecord };
            stack.Children.Add(MakeCheckRow(_autoRecordBox, Strings.AutoRecordDesc));

            _checkUpdatesBox = new CheckBox { Content = Strings.LabelCheckUpdates };
            stack.Children.Add(MakeCheckRow(_checkUpdatesBox, Strings.CheckUpdatesDesc));

            // Save button
            var saveBtn = new Button
            {
                Content = Strings.BtnSave,
                Style = (Style)Theme.GetStyle("PrimaryButtonStyle"),
                HorizontalAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(28, 8, 28, 8),
                Margin = new Thickness(0, 4, 0, 0)
            };
            saveBtn.Click += (s, e) => SaveGeneralSettings();
            stack.Children.Add(saveBtn);

            stack.Children.Add(MakeSettingsDivider());

            // ── Section: Files & Folders ──
            stack.Children.Add(MakeSectionHeader(Strings.SectionData));

            var folderRow = new StackPanel { Orientation = Orientation.Horizontal };
            var btnReports = new Button { Content = Strings.BtnOpenReports, Style = (Style)Theme.GetStyle("GhostButtonStyle"), Margin = new Thickness(0, 0, 8, 0) };
            btnReports.Click += (s, e) => { try { System.Diagnostics.Process.Start(AppPaths.ReportsDir); } catch { } };
            var btnData = new Button { Content = Strings.BtnOpenData, Style = (Style)Theme.GetStyle("GhostButtonStyle") };
            btnData.Click += (s, e) => { try { System.Diagnostics.Process.Start(AppPaths.DataDir); } catch { } };
            folderRow.Children.Add(btnReports);
            folderRow.Children.Add(btnData);
            stack.Children.Add(folderRow);

            // Data path display
            stack.Children.Add(new TextBlock
            {
                Text = Strings.LabelDataDir + ": " + AppPaths.DataDir,
                Foreground = Theme.Get("TextMutedBrush"),
                FontSize = 11,
                Margin = new Thickness(0, 8, 0, 0),
                TextWrapping = TextWrapping.Wrap
            });

            stack.Children.Add(MakeSettingsDivider());

            // ── Section: Security Status ──
            stack.Children.Add(MakeSectionHeader(Strings.SectionSecurity));

            stack.Children.Add(MakeSecurityBullet(Strings.SecurityNoKeylog));
            stack.Children.Add(MakeSecurityBullet(Strings.SecurityNoAudio));
            stack.Children.Add(MakeSecurityBullet(Strings.SecurityLocalOnly));
            stack.Children.Add(MakeSecurityBullet(Strings.SecurityFilterFirst));
            stack.Children.Add(MakeSecurityBullet(Strings.SecurityApiKeyInfo));

            stack.Children.Add(MakeSettingsDivider());

            // ── Version ──
            stack.Children.Add(new TextBlock
            {
                Text = Strings.LabelVersion + ": " + AppVersion,
                Foreground = Theme.Get("TextMutedBrush"),
                FontSize = 11
            });

            return stack;
        }

        private void LoadAppConfigIntoUi()
        {
            if (_languageCombo == null || _themeCombo == null || _startWithWindowsBox == null) return;
            var lang = Json.GetString(_appConfig, "language", "he");
            var theme = Json.GetString(_appConfig, "theme", "dark");
            var startup = Json.GetBool(_appConfig, "start_with_windows", false);
            var autoRecord = Json.GetBool(_appConfig, "auto_record", false);
            var checkUpdates = Json.GetBool(_appConfig, "check_for_updates", true);

            _languageCombo.SelectedIndex = lang == "en" ? 1 : 0;
            _themeCombo.SelectedIndex = theme == "light" ? 1 : 0;
            _startWithWindowsBox.IsChecked = startup;
            if (_autoRecordBox != null) _autoRecordBox.IsChecked = autoRecord;
            if (_checkUpdatesBox != null) _checkUpdatesBox.IsChecked = checkUpdates;
        }

        private void SaveGeneralSettings()
        {
            var lang = _languageCombo.SelectedIndex == 1 ? "en" : "he";
            var theme = _themeCombo.SelectedIndex == 1 ? "light" : "dark";
            var startup = _startWithWindowsBox.IsChecked == true;
            var autoRecord = _autoRecordBox != null && _autoRecordBox.IsChecked == true;
            var checkUpdates = _checkUpdatesBox == null || _checkUpdatesBox.IsChecked == true;

            _appConfig["language"] = lang;
            _appConfig["theme"] = theme;
            _appConfig["start_with_windows"] = startup;
            _appConfig["auto_record"] = autoRecord;
            _appConfig["check_for_updates"] = checkUpdates;
            ConfigStore.SaveApp(_appConfig);

            SetStartWithWindows(startup);

            MessageBox.Show(Strings.MsgRestartNeeded, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private static void SetStartWithWindows(bool enable)
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
            const string valueName = "AutoProcessTwin";
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                {
                    if (key == null) return;
                    if (enable)
                    {
                        string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                        key.SetValue(valueName, "\"" + exePath + "\"");
                    }
                    else
                    {
                        if (key.GetValue(valueName) != null)
                            key.DeleteValue(valueName);
                    }
                }
            }
            catch { }
        }

        // ---------- Dashboard ----------

        private UIElement BuildDashboardTab()
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            _onboardingBanner = new Border
            {
                Background = Theme.Get("AccentLightBrush"),
                BorderBrush = Theme.Get("AccentBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 16),
            };
            var bannerStack = new StackPanel();
            bannerStack.Children.Add(new TextBlock
            {
                Text = "עדיין לא מוקלט כלום",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = Theme.Get("TextBrush"),
            });
            bannerStack.Children.Add(new TextBlock
            {
                Text = "לחץ על \"▶ התחל הקלטה\" למעלה מימין. תוך שנייה-שתיים תופיע תצפית ראשונה כאן למטה " +
                       "וב\"אירועים היום\". התצפית הבאה אחריה תגיע כעבור המרווח שהוגדר בטאב \"פרטיות\" (ברירת מחדל: 20 שניות) - " +
                       "זה לא תקוע, זה פשוט לא מצלם כל שנייה בכוונה.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = Theme.Get("TextMutedBrush"),
                FontSize = 12,
                Margin = new Thickness(0, 6, 0, 0),
            });
            _onboardingBanner.Child = bannerStack;
            stack.Children.Add(_onboardingBanner);

            // Spotlight - התובנה הכי חשובה קודם, לא רשימה שצריך לפענח.
            // מוסתר עד שיש ניתוח ראשון (ראה RefreshPatterns).
            _spotlightCard = new Border
            {
                Style = (Style)Theme.GetStyle("CardBorderStyle"),
                BorderBrush = Theme.Get("AccentBrush"),
                Margin = new Thickness(0, 0, 0, 16),
                Visibility = Visibility.Collapsed,
            };
            var spotlightStack = new StackPanel();
            spotlightStack.Children.Add(new TextBlock { Text = "💡 התובנה של היום", Style = (Style)Theme.GetStyle("HintLabelStyle"), FontWeight = FontWeights.Bold, Foreground = Theme.Get("AccentBrush"), Margin = new Thickness(0, 0, 0, 6) });
            _spotlightHeadline = new TextBlock { FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Theme.Get("TextBrush"), TextWrapping = TextWrapping.Wrap };
            _spotlightBody = new TextBlock { FontSize = 13, Foreground = Theme.Get("TextMutedBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
            spotlightStack.Children.Add(_spotlightHeadline);
            spotlightStack.Children.Add(_spotlightBody);
            _spotlightCard.Child = spotlightStack;
            stack.Children.Add(_spotlightCard);

            var cardsRow = new WrapPanel { Orientation = Orientation.Horizontal };
            cardsRow.Children.Add(StatCard("אירועים היום", out _eventsTodayText));
            cardsRow.Children.Add(StatCard("צולמו (לא סוננו)", out _capturedTodayText));
            cardsRow.Children.Add(StatCard("סוננו ע\"י Privacy Filter", out _filteredTodayText));
            stack.Children.Add(cardsRow);

            _lastActivityText = new TextBlock
            {
                Text = "",
                FontSize = 11.5,
                Foreground = Theme.Get("TextMutedBrush"),
                Margin = new Thickness(0, 2, 0, 0),
                FlowDirection = FlowDirection.RightToLeft,
            };
            stack.Children.Add(_lastActivityText);

            // בלי כפתורי "עדכן X" - הכל מתעדכן ברקע לבד (כמו RescueTime/Time
            // Doctor). כפתור רענון ידני נשאר כ-escape hatch למי שרוצה עכשיו.
            var refreshRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 16), VerticalAlignment = VerticalAlignment.Center };
            var manualRefreshBtn = new Button { Content = "🔄  רענן עכשיו", Width = 140, Style = (Style)Theme.GetStyle("GhostButtonStyle"), Margin = new Thickness(0, 0, 10, 0) };
            manualRefreshBtn.Click += (s, e) => { RefreshStatus(); RefreshPatterns(); };
            _autoRefreshStatusText = new TextBlock { Text = "מתעדכן אוטומטית", FontSize = 11.5, Foreground = Theme.Get("TextMutedBrush"), VerticalAlignment = VerticalAlignment.Center };
            refreshRow.Children.Add(manualRefreshBtn);
            refreshRow.Children.Add(_autoRefreshStatusText);
            stack.Children.Add(refreshRow);

            // תצוגה מקדימה - 3 הפעולות החוזרות המובילות, עם קישור לרשימה המלאה
            var previewCard = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle"), Margin = new Thickness(0, 0, 0, 16) };
            var previewStack = new StackPanel();
            var previewHeader = new DockPanel();
            previewHeader.Children.Add(new TextBlock { Text = "הכי בולט מבחינת פעולות חוזרות", Style = (Style)Theme.GetStyle("SectionLabelStyle"), Margin = new Thickness(0) });
            previewStack.Children.Add(previewHeader);
            _topPatternsPreview = new StackPanel();
            _topPatternsPreview.Children.Add(new TextBlock
            {
                Text = "עדיין אין ניתוח - צריך כמה ימים של הקלטה. זה יתמלא לבד.",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                TextWrapping = TextWrapping.Wrap,
            });
            previewStack.Children.Add(_topPatternsPreview);
            var seeAllBtn = new Button { Content = "לכל הפעולות החוזרות ←", Style = (Style)Theme.GetStyle("IconButtonStyle"), HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 0), FontSize = 12 };
            seeAllBtn.Click += (s, e) => SelectTabByHeader("פעולות חוזרות");
            previewStack.Children.Add(seeAllBtn);
            previewCard.Child = previewStack;
            stack.Children.Add(previewCard);

            var logCard = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle") };
            var logStack = new DockPanel();
            logStack.Children.Add(new TextBlock { Text = "יומן פעילות חי", Style = (Style)Theme.GetStyle("SectionLabelStyle"), Margin = new Thickness(0, 0, 0, 10) });
            DockPanel.SetDock(logStack.Children[0], Dock.Top);

            _logListBox = new ListBox
            {
                Style = (Style)Theme.GetStyle("ModernListBoxStyle"),
                Height = 220,
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                FlowDirection = FlowDirection.LeftToRight, // log lines are timestamps/paths - read better LTR even in an RTL window
            };
            logStack.Children.Add(_logListBox);
            logCard.Child = logStack;
            stack.Children.Add(logCard);

            return stack;
        }

        // מוצא את ה-TabControl הראשי ומעביר לטאב לפי הכותרת שלו - למשל
        // מהקישור "לכל הפעולות החוזרות" בתצוגה המקדימה בבית.
        private void SelectTabByHeader(string header)
        {
            var tabs = FindVisualChild<TabControl>(this);
            if (tabs == null) return;
            foreach (TabItem item in tabs.Items)
            {
                if ((string)item.Header == header) { tabs.SelectedItem = item; return; }
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                var typed = child as T;
                if (typed != null) return typed;
                var result = FindVisualChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        private Border StatCard(string label, out TextBlock valueText)
        {
            var card = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle"), Margin = new Thickness(0, 0, 12, 12), Width = 220 };
            var stack = new StackPanel();
            var value = new TextBlock { Text = "0", FontSize = 30, FontWeight = FontWeights.Bold, Foreground = Theme.Get("AccentBrush") };
            var caption = new TextBlock { Text = label, FontSize = 12, Foreground = Theme.Get("TextMutedBrush"), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
            stack.Children.Add(value);
            stack.Children.Add(caption);
            card.Child = stack;
            valueText = value;
            return card;
        }

        // async void בכוונה - זו נקראת מ-DispatcherTimer.Tick כל 30 שניות, וקודם
        // רצה סינכרונית על ה-UI thread (NodeRunner.RunSync חוסם עד שה-process
        // Node מסיים) - כלומר כל 30 שניות החלון פשוט קפא לרגע. Task.Run מזיז
        // את ההמתנה לתהליך לthread ברקע; הקוד שאחרי ה-await חוזר אוטומטית
        // ל-UI thread (SynchronizationContext של WPF) כך שעדכון הבקרות בטוח.
        // _statusRefreshInFlight מונע קריאה חופפת אם ה-Node process איטי במיוחד
        // וה-timer מספיק לתקתק שוב לפני שהקודמת הסתיימה.
        private bool _statusRefreshInFlight;

        private async void RefreshStatus()
        {
            if (_statusRefreshInFlight) return;
            _statusRefreshInFlight = true;
            try
            {
                string json = await Task.Run(new Func<string>(() => NodeRunner.RunSync(AppPaths.StatusScript, 10000)));
                var serializer = new JavaScriptSerializer();
                var data = (Dictionary<string, object>)serializer.DeserializeObject(json.Trim());
                double eventsToday = Json.GetNumber(data, "eventsToday", 0);
                _eventsTodayText.Text = eventsToday.ToString("0");
                _capturedTodayText.Text = Json.GetNumber(data, "capturedToday", 0).ToString("0");
                _filteredTodayText.Text = Json.GetNumber(data, "filteredToday", 0).ToString("0");
                if (_onboardingBanner != null) _onboardingBanner.Visibility = eventsToday > 0 ? Visibility.Collapsed : Visibility.Visible;

                // Show last-seen app + elapsed time so the user can tell the recorder
                // is alive even between captured-event stat refreshes.
                string lastApp = Json.GetString(data, "lastApp", null);
                double lastTs = Json.GetNumber(data, "lastTs", 0);
                if (!string.IsNullOrEmpty(lastApp) && lastTs > 0)
                {
                    double agoSec = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds - lastTs / 1000.0;
                    string agoLabel = agoSec < 90 ? "עכשיו" : agoSec < 3600 ? (int)(agoSec / 60) + " דק'" : (int)(agoSec / 3600) + " שע'";
                    bool wasFiltered = Json.GetBool(data, "lastWasFiltered", false);
                    string filterNote = wasFiltered ? " (סונן)" : "";
                    _lastActivityText.Text = "תצפית אחרונה: " + lastApp + filterNote + " · לפני " + agoLabel;
                }
                else
                {
                    _lastActivityText.Text = "";
                }
            }
            catch (Exception ex)
            {
                AppendLog("[status] שגיאה בקריאת סטטוס: " + ex.Message);
            }
            finally
            {
                _statusRefreshInFlight = false;
            }
        }

        private async void GenerateBriefingAndShow()
        {
            AppendLog("--- מעדכן יומן פעילות ---");
            try
            {
                string output = await Task.Run(new Func<string>(() => NodeRunner.RunSync(AppPaths.BriefingScript, 30000)));
                AppendLog(output.Trim());
                RefreshReportsList();
                if (_reportsList.Items.Count > 0)
                {
                    _reportsList.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "שגיאה בעדכון יומן הפעילות:\n" + ex.Message, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- המלצות אוטומציה (פעולות חוזרות) ----------
        //
        // זה הליבה של המטרה האמיתית של המערכת: לא "איפה היית", אלא "מה חוזר
        // על עצמו מספיק כדי שכדאי להשקיע באוטומציה שלו". agent/src/patterns.js
        // עושה את הזיהוי בפועל (keyword-based, לא ML - ראה ההערה שם); הטאב
        // הזה רק מציג את זה יפה ומריץ ידני.

        private UIElement BuildPatternsTab()
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            stack.Children.Add(new TextBlock
            {
                Text = "מטרת הטאב הזה: לזהות פעולות שחוזרות על עצמן - הזנת לקוחות ב-CRM, העברת קבצים, " +
                       "מילוי גיליונות - ולהציע במה אפשר להחליף אותן. לא ניתוח AI חכם - זיהוי לפי מילות מפתח " +
                       "ותדירות, בכנות. מתעדכן לבד ברקע; צריך כמה ימים של הקלטה כדי שיהיה מה לזהות.",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                Margin = new Thickness(0, 0, 0, 16),
            });

            var rangeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 14), VerticalAlignment = VerticalAlignment.Center };
            rangeRow.Children.Add(new TextBlock { Text = "טווח ניתוח:", Foreground = Theme.Get("TextMutedBrush"), FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            _patternsRangeCombo = new ComboBox { Style = (Style)Theme.GetStyle("ModernComboBoxStyle"), Width = 150 };
            _patternsRangeCombo.Items.Add("7 ימים אחרונים");
            _patternsRangeCombo.Items.Add("14 ימים אחרונים");
            _patternsRangeCombo.Items.Add("30 ימים אחרונים");
            _patternsRangeCombo.Items.Add("90 ימים אחרונים");
            _patternsRangeCombo.SelectedIndex = 2; // 30 יום - ברירת המחדל הקיימת, נבחר *לפני* חיבור ה-handler כדי שלא יירה על הבנייה הראשונית
            _patternsRangeCombo.SelectionChanged += (s, e) =>
            {
                int idx = _patternsRangeCombo.SelectedIndex;
                _patternsRangeDays = idx == 0 ? 7 : idx == 1 ? 14 : idx == 3 ? 90 : 30;
                RefreshPatterns();
            };
            rangeRow.Children.Add(_patternsRangeCombo);
            stack.Children.Add(rangeRow);

            _patternsSummaryText = new TextBlock
            {
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                Margin = new Thickness(0, 0, 0, 16),
                FontWeight = FontWeights.SemiBold,
            };
            stack.Children.Add(_patternsSummaryText);

            _patternsEmptyHint = new TextBlock
            {
                Text = "עדיין אין ניתוח. זה מתעדכן לבד ברקע - צריך כמה ימים של הקלטה כדי שיהיה מה לזהות.",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                TextWrapping = TextWrapping.Wrap,
            };
            stack.Children.Add(_patternsEmptyHint);

            // פילוח לפי קטגוריה - "לאן הזמן הולך בגדול" לפני פירוט הדפוסים
            // הבודדים. מוסתר עד שיש דאטה (ר' RefreshPatterns).
            _categoryBreakdownPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
            stack.Children.Add(_categoryBreakdownPanel);

            var exportBtn = new Button
            {
                Content = "📤  פתח דוח HTML לשיתוף",
                Style = (Style)Theme.GetStyle("GhostButtonStyle"),
                Width = 220,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 16),
            };
            exportBtn.Click += (s, e) => OpenLatestPatternsHtmlReport();
            stack.Children.Add(exportBtn);

            // CSV - הפורמט שמנהל בפועל מייבא לאקסל/שיטס כדי למיין/לסנן, לצד
            // ה-HTML לקריאה חד-פעמית (השראה: RescueTime/Timely מייצאים CSV
            // כברירת מחדל, לא רק דוח מעוצב).
            var csvExportBtn = new Button
            {
                Content = "📄  פתח CSV לאקסל",
                Style = (Style)Theme.GetStyle("GhostButtonStyle"),
                Width = 180,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 0, 0, 16),
            };
            csvExportBtn.Click += (s, e) => OpenLatestPatternsCsvReport();
            stack.Children.Add(csvExportBtn);

            var aiCard = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle"), Margin = new Thickness(0, 0, 0, 16) };
            var aiStack = new StackPanel();
            aiStack.Children.Add(new TextBlock { Text = "✨ סיכום מנהלים (AI)", Style = (Style)Theme.GetStyle("SectionLabelStyle") });
            _aiSummaryText = new TextBlock
            {
                Text = "אופציונלי וכבוי כברירת מחדל. כדי להפעיל: הגדרות ← AI (ניסיוני). כשמופעל, נשלחים רק נתונים מצטברים על הדפוסים - לעולם לא כותרות חלונות.",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 12),
            };
            aiStack.Children.Add(_aiSummaryText);
            _aiSummaryBtn = new Button { Content = "✨  צור סיכום עכשיו", Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 180, HorizontalAlignment = HorizontalAlignment.Left };
            _aiSummaryBtn.Click += (s, e) => GenerateAiSummary();
            aiStack.Children.Add(_aiSummaryBtn);
            aiCard.Child = aiStack;
            stack.Children.Add(aiCard);

            _patternsListPanel = new StackPanel();
            stack.Children.Add(_patternsListPanel);

            // דפוסים שהוסתרו - מוצג רק כשיש כאלה (ר' RefreshPatterns), עם
            // אפשרות "השב תצוגה" לכל אחד. בלי זה, "כבר טיפלתי בזה" היה כפתור
            // חד-כיווני בלי דרך חזרה מהממשק.
            _ignoredHeader = new TextBlock
            {
                Text = "דפוסים שהוסתרו",
                Style = (Style)Theme.GetStyle("SectionLabelStyle"),
                Margin = new Thickness(0, 20, 0, 8),
                Visibility = Visibility.Collapsed,
            };
            stack.Children.Add(_ignoredHeader);
            _ignoredPatternsPanel = new StackPanel { Visibility = Visibility.Collapsed };
            stack.Children.Add(_ignoredPatternsPanel);

            return stack;
        }

        private void OpenLatestPatternsHtmlReport()
        {
            try
            {
                if (!Directory.Exists(AppPaths.ReportsDir))
                {
                    MessageBox.Show(this, "עדיין אין דוח - זה נוצר לבד אחרי כמה ימים של הקלטה, או אפשר ללחוץ \"🔄 רענן עכשיו\" בבית.", "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var htmlFiles = Directory.GetFiles(AppPaths.ReportsDir, "automation-recommendations-*.html");
                if (htmlFiles.Length == 0)
                {
                    MessageBox.Show(this, "עדיין אין דוח - זה נוצר לבד אחרי כמה ימים של הקלטה, או אפשר ללחוץ \"🔄 רענן עכשיו\" בבית.", "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string latest = htmlFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                Process.Start(new ProcessStartInfo(latest) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "שגיאה בפתיחת הדוח:\n" + ex.Message, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // אותו רעיון בדיוק כמו OpenLatestPatternsHtmlReport, קובץ CSV במקום
        // HTML - patterns-report.js כותב את שניהם (וגם md) באותה ריצה.
        private void OpenLatestPatternsCsvReport()
        {
            try
            {
                if (!Directory.Exists(AppPaths.ReportsDir))
                {
                    MessageBox.Show(this, "עדיין אין דוח - זה נוצר לבד אחרי כמה ימים של הקלטה, או אפשר ללחוץ \"🔄 רענן עכשיו\" בבית.", "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                var csvFiles = Directory.GetFiles(AppPaths.ReportsDir, "automation-recommendations-*.csv");
                if (csvFiles.Length == 0)
                {
                    MessageBox.Show(this, "עדיין אין דוח - זה נוצר לבד אחרי כמה ימים של הקלטה, או אפשר ללחוץ \"🔄 רענן עכשיו\" בבית.", "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                string latest = csvFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                Process.Start(new ProcessStartInfo(latest) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "שגיאה בפתיחת ה-CSV:\n" + ex.Message, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // אותה סיבה בדיוק כמו RefreshStatus - זו רצה כל 3 דקות מה-timer, וה-Node
        // process שלה איטי יותר (מנתח את כל הדפוסים, לא רק שולף מספרים) - חסימת
        // ה-UI thread לזה הייתה קפיאה מורגשת ממש. אותו דפוס Task.Run + דגל
        // re-entrancy.
        private bool _patternsRefreshInFlight;

        private async void RefreshPatterns()
        {
            if (_patternsRefreshInFlight) return;
            _patternsRefreshInFlight = true;
            try
            {
                int rangeDays = _patternsRangeDays;
                string json = (await Task.Run(new Func<string>(() => NodeRunner.RunSync(AppPaths.PatternsScript, 30000, rangeDays.ToString())))).Trim();
                var serializer = new JavaScriptSerializer();
                var data = (Dictionary<string, object>)serializer.DeserializeObject(json);
                var patternsRaw = Json.AsList(data.ContainsKey("patterns") ? data["patterns"] : null);
                double totalMinutes = Json.GetNumber(data, "totalMinutesTracked", 0);
                double periodDays = Json.GetNumber(data, "periodDays", 30);

                _patternsSummaryText.Text = string.Format(
                    "{0} ימים אחרונים · {1} שעות ו-{2} דקות של פעילות מוקלטת בסך הכול",
                    (int)periodDays, (int)(totalMinutes / 60), (int)(totalMinutes % 60));

                var categoryBreakdownRaw = Json.AsList(data.ContainsKey("categoryBreakdown") ? data["categoryBreakdown"] : null)
                    .Select(Json.AsDict).ToList();
                RenderCategoryBreakdown(categoryBreakdownRaw);

                _patternsListPanel.Children.Clear();
                var candidates = patternsRaw.Select(Json.AsDict).Where(p => Json.GetBool(p, "isAutomationCandidate", false))
                    .OrderByDescending(p => Json.GetNumber(p, "minutes", 0)).ToList();

                if (candidates.Count == 0)
                {
                    _patternsEmptyHint.Text = "אין עדיין חזרות מובהקות מספיק כדי להמליץ על משהו. או שצריך עוד כמה ימים של הקלטה, או שפשוט אין הרבה פעולות חוזרות אצלך - זה דבר טוב.";
                    _patternsEmptyHint.Visibility = Visibility.Visible;
                }
                else
                {
                    _patternsEmptyHint.Visibility = Visibility.Collapsed;
                    double maxMinutes = candidates.Max(p => Json.GetNumber(p, "minutes", 0));
                    foreach (var p in candidates)
                    {
                        _patternsListPanel.Children.Add(BuildPatternCard(p, maxMinutes));
                    }
                }

                UpdateSpotlightAndPreview(candidates);

                var ignoredRaw = Json.AsList(data.ContainsKey("ignoredPatterns") ? data["ignoredPatterns"] : null).Select(Json.AsDict).ToList();
                _ignoredPatternsPanel.Children.Clear();
                if (ignoredRaw.Count == 0)
                {
                    _ignoredHeader.Visibility = Visibility.Collapsed;
                    _ignoredPatternsPanel.Visibility = Visibility.Collapsed;
                }
                else
                {
                    _ignoredHeader.Text = "דפוסים שהוסתרו (" + ignoredRaw.Count + ")";
                    _ignoredHeader.Visibility = Visibility.Visible;
                    _ignoredPatternsPanel.Visibility = Visibility.Visible;
                    foreach (var p in ignoredRaw)
                    {
                        _ignoredPatternsPanel.Children.Add(BuildIgnoredPatternRow(p));
                    }
                }

                AppendLog("[ניתוח] " + candidates.Count + " מועמדים לאוטומציה נמצאו.");
            }
            catch (Exception ex)
            {
                AppendLog("[ניתוח] שגיאה בניתוח פעולות חוזרות: " + ex.Message);
            }
            finally
            {
                _patternsRefreshInFlight = false;
            }
        }

        // פילוח לפי קטגוריה - "לאן הזמן הולך בגדול" (patterns.js:
        // categoryBreakdown, מבוסס על כל הדפוסים, לא רק מועמדים לאוטומציה,
        // כדי שהאחוזים יסתכמו ל-100% אמיתיים). מוצג כרשימת שורות עם פס יחסי,
        // בדיוק כמו כרטיסי הדפוסים למטה אבל ברמת קטגוריה. Report-only בלבד -
        // אין כאן שום פעולה, רק תצוגה.
        private void RenderCategoryBreakdown(List<Dictionary<string, object>> categories)
        {
            if (_categoryBreakdownPanel == null) return;
            _categoryBreakdownPanel.Children.Clear();
            if (categories == null || categories.Count == 0) return;

            _categoryBreakdownPanel.Children.Add(new TextBlock
            {
                Text = "לאן הזמן הולך — לפי קטגוריה",
                Style = (Style)Theme.GetStyle("SectionLabelStyle"),
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 10),
            });

            double maxMinutes = categories.Max(c => Json.GetNumber(c, "minutes", 0));
            var card = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle") };
            var listStack = new StackPanel();
            foreach (var c in categories)
            {
                string label = Json.GetString(c, "categoryLabel", "");
                double minutes = Json.GetNumber(c, "minutes", 0);
                int percent = (int)Json.GetNumber(c, "percent", 0);
                int patternCount = (int)Json.GetNumber(c, "patternCount", 0);

                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
                var head = new Grid();
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var nameText = new TextBlock { Text = label, Foreground = Theme.Get("TextBrush"), FontSize = 13, FontWeight = FontWeights.SemiBold };
                Grid.SetColumn(nameText, 0);
                head.Children.Add(nameText);
                var statsText = new TextBlock
                {
                    Text = string.Format("{0} דק' · {1}% · {2} דפוסים", (int)minutes, percent, patternCount),
                    Foreground = Theme.Get("TextMutedBrush"),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11.5,
                };
                Grid.SetColumn(statsText, 1);
                head.Children.Add(statsText);
                row.Children.Add(head);

                var barTrack = new Border { Background = Theme.Get("Panel2Brush"), Height = 5, CornerRadius = new CornerRadius(2.5), Margin = new Thickness(0, 6, 0, 0) };
                double fraction = maxMinutes > 0 ? Math.Max(0.02, minutes / maxMinutes) : 0.02;
                var barGrid = new Grid();
                barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(fraction, GridUnitType.Star) });
                barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - fraction, GridUnitType.Star) });
                var filled = new Border { Background = Theme.Get("AccentBrush"), CornerRadius = new CornerRadius(2.5) };
                Grid.SetColumn(filled, 0);
                barGrid.Children.Add(filled);
                barTrack.Child = barGrid;
                row.Children.Add(barTrack);

                listStack.Children.Add(row);
            }
            card.Child = listStack;
            _categoryBreakdownPanel.Children.Add(card);
        }

        private Border BuildIgnoredPatternRow(Dictionary<string, object> p)
        {
            string category = Json.GetString(p, "categoryLabel", null) ?? Json.GetString(p, "appName", "");
            string key = Json.GetString(p, "key", null);

            var row = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle"), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(14, 10, 14, 10) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock { Text = category, Foreground = Theme.Get("TextMutedBrush"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(nameText, 0);
            grid.Children.Add(nameText);

            var restoreBtn = new Button { Content = "↺  השב תצוגה", Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 120, FontSize = 11.5 };
            restoreBtn.Click += (s, e) => UnignorePattern(key);
            Grid.SetColumn(restoreBtn, 1);
            grid.Children.Add(restoreBtn);

            row.Child = grid;
            return row;
        }

        // התובנה של היום (בית) + תצוגה מקדימה של 3 הפעולות החוזרות המובילות -
        // מתעדכן לבד מתוך אותו ניתוח, בלי קריאה נפרדת.
        private void UpdateSpotlightAndPreview(List<Dictionary<string, object>> candidates)
        {
            if (_spotlightCard == null || _topPatternsPreview == null) return;

            if (candidates.Count == 0)
            {
                _spotlightCard.Visibility = Visibility.Collapsed;
                _topPatternsPreview.Children.Clear();
                _topPatternsPreview.Children.Add(new TextBlock
                {
                    Text = "עדיין אין ניתוח - צריך כמה ימים של הקלטה. זה יתמלא לבד.",
                    Style = (Style)Theme.GetStyle("HintLabelStyle"),
                    TextWrapping = TextWrapping.Wrap,
                });
                return;
            }

            var top = candidates[0];
            string category = Json.GetString(top, "categoryLabel", null) ?? Json.GetString(top, "appName", "");
            double minutes = Json.GetNumber(top, "minutes", 0);
            int occurrences = (int)Json.GetNumber(top, "occurrences", 0);
            string trendDirection = Json.GetString(top, "trendDirection", null);
            double trendPercent = Json.GetNumber(top, "trendPercent", 0);

            _spotlightHeadline.Text = string.Format("הכי הרבה זמן הולך על \"{0}\" — {1} שעות ו-{2} דקות, {3} פעמים.",
                category, (int)(minutes / 60), (int)(minutes % 60), occurrences);
            string trendText = FormatTrendSentence(trendDirection, trendPercent);
            _spotlightBody.Text = (Json.GetString(top, "recommendation", "") + (trendText != null ? "  " + trendText : "")).Trim();
            _spotlightCard.Visibility = Visibility.Visible;

            _topPatternsPreview.Children.Clear();
            foreach (var p in candidates.Take(3))
            {
                _topPatternsPreview.Children.Add(BuildCompactPatternRow(p));
            }
        }

        private static string FormatTrendSentence(string direction, double percent)
        {
            if (string.IsNullOrEmpty(direction) || direction == "stable") return null;
            if (direction == "new") return "(חדש בתקופה הזו)";
            string arrow = direction == "up" ? "↑" : "↓";
            string sign = percent > 0 ? "+" : "";
            return string.Format("מגמה: {0} {1}{2}% לעומת קודם.", arrow, sign, (int)percent);
        }

        private Border BuildCompactPatternRow(Dictionary<string, object> p)
        {
            string category = Json.GetString(p, "categoryLabel", null) ?? Json.GetString(p, "appName", "");
            double minutes = Json.GetNumber(p, "minutes", 0);
            int occurrences = (int)Json.GetNumber(p, "occurrences", 0);
            string trendDirection = Json.GetString(p, "trendDirection", null);
            double trendPercent = Json.GetNumber(p, "trendPercent", 0);

            var row = new Border { BorderBrush = Theme.Get("BorderColorBrush"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(0, 8, 0, 8) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock { Text = category, Foreground = Theme.Get("TextBrush"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(nameText, 0);
            grid.Children.Add(nameText);

            var statsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            string trendSuffix = "";
            if (!string.IsNullOrEmpty(trendDirection) && trendDirection != "stable" && trendDirection != "new")
            {
                trendSuffix = "  " + (trendDirection == "up" ? "↑" : "↓") + (trendPercent > 0 ? "+" : "") + (int)trendPercent + "%";
            }
            statsStack.Children.Add(new TextBlock
            {
                Text = string.Format("{0}ש' {1}דק' · {2}×{3}", (int)(minutes / 60), (int)(minutes % 60), occurrences, trendSuffix),
                Foreground = Theme.Get("TextMutedBrush"),
                FontSize = 12,
            });
            Grid.SetColumn(statsStack, 1);
            grid.Children.Add(statsStack);

            row.Child = grid;
            return row;
        }

        private Border BuildPatternCard(Dictionary<string, object> p, double maxMinutes)
        {
            double minutes = Json.GetNumber(p, "minutes", 0);
            int occurrences = (int)Json.GetNumber(p, "occurrences", 0);
            int distinctDays = (int)Json.GetNumber(p, "distinctDays", 0);
            string category = Json.GetString(p, "categoryLabel", null);
            string appName = Json.GetString(p, "appName", "");
            string titleSample = Json.GetString(p, "titleSample", "");
            string recommendation = Json.GetString(p, "recommendation", "");
            string trendDirection = Json.GetString(p, "trendDirection", null);
            double? trendPercent = p.ContainsKey("trendPercent") && p["trendPercent"] != null ? (double?)Json.GetNumber(p, "trendPercent", 0) : null;
            string confidenceLabel = Json.GetString(p, "confidenceLabel", null);
            double? confidenceScore = p.ContainsKey("confidenceScore") && p["confidenceScore"] != null ? (double?)Json.GetNumber(p, "confidenceScore", 0) : null;
            string patternKey = Json.GetString(p, "key", null);

            var card = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle"), Margin = new Thickness(0, 0, 0, 12) };
            var stack = new StackPanel();

            var headerRow = new Grid();
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var titleStack = new StackPanel();
            titleStack.Children.Add(new TextBlock
            {
                Text = category ?? appName,
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = Theme.Get("TextBrush"),
            });
            titleStack.Children.Add(new TextBlock
            {
                Text = appName + "  ·  דוגמה: " + titleSample,
                FontSize = 11.5,
                Foreground = Theme.Get("TextMutedBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 3, 0, 0),
            });
            Grid.SetColumn(titleStack, 0);
            headerRow.Children.Add(titleStack);

            var statsStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var confidenceBadge = BuildConfidenceBadge(confidenceLabel, confidenceScore);
            if (confidenceBadge != null)
            {
                confidenceBadge.Margin = new Thickness(0, 0, 8, 0);
                statsStack.Children.Add(confidenceBadge);
            }
            var trendBadge = BuildTrendBadge(trendDirection, trendPercent);
            if (trendBadge != null)
            {
                trendBadge.Margin = new Thickness(0, 0, 8, 0);
                statsStack.Children.Add(trendBadge);
            }
            statsStack.Children.Add(new TextBlock
            {
                Text = string.Format("{0} דק' · {1} פעמים · {2} ימים", (int)minutes, occurrences, distinctDays),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                Foreground = Theme.Get("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            });
            Grid.SetColumn(statsStack, 1);
            headerRow.Children.Add(statsStack);
            stack.Children.Add(headerRow);

            // בר יחסי - רוחב לפי הזמן ביחס לפריט הכי גדול ברשימה
            var barTrack = new Border { Background = Theme.Get("Panel2Brush"), Height = 6, CornerRadius = new CornerRadius(3), Margin = new Thickness(0, 10, 0, 12) };
            double fraction = maxMinutes > 0 ? Math.Max(0.03, minutes / maxMinutes) : 0.03;
            var barGrid = new Grid();
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(fraction, GridUnitType.Star) });
            barGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - fraction, GridUnitType.Star) });
            var filledPart = new Border { Background = Theme.Get("AccentBrush"), CornerRadius = new CornerRadius(3) };
            Grid.SetColumn(filledPart, 0);
            barGrid.Children.Add(filledPart);
            barTrack.Child = barGrid;
            stack.Children.Add(barTrack);

            stack.Children.Add(new TextBlock
            {
                Text = recommendation,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = Theme.Get("TextBrush"),
            });

            if (!string.IsNullOrEmpty(patternKey))
            {
                var dismissBtn = new Button
                {
                    Content = "✕  כבר טיפלתי בזה - אל תציג שוב",
                    Style = (Style)Theme.GetStyle("IconButtonStyle"),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(0, 10, 0, 0),
                    FontSize = 11.5,
                };
                dismissBtn.Click += (s, e) => DismissPattern(patternKey);
                stack.Children.Add(dismissBtn);
            }

            card.Child = stack;
            return card;
        }

        // עוטף Process.Start+WaitForExit של patterns-report.js --ignore/--unignore
        // ב-Task.Run - משותף לשני הכפתורים כדי לא לשכפל את ה-ProcessStartInfo.
        // בעבר זה חסם את ה-UI thread עד 10 שניות בלחיצה על הכפתור.
        private static Task RunPatternsCliAsync(string flag, string key)
        {
            return Task.Run(new Action(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = AppPaths.FindNodeExe(),
                    Arguments = NodeRunner.EscapeWindowsArg(AppPaths.PatternsScript) + " " + flag + " " + NodeRunner.EscapeWindowsArg(key),
                    WorkingDirectory = AppPaths.AgentDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit(10000);
                }
            }));
        }

        private async void DismissPattern(string key)
        {
            try
            {
                await RunPatternsCliAsync("--ignore", key);
                AppendLog("--- המלצה הוסתרה, לא תוצג יותר ---");
                RefreshPatterns();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "שגיאה בהסתרת ההמלצה:\n" + ex.Message, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ביטול "כבר טיפלתי בזה" - בלי זה, ההסתרה הייתה דלת חד-כיוונית: לחיצה
        // בטעות אחת מעלימה המלצה לצמיתות בלי שום דרך חזרה דרך הממשק (רק
        // עריכה ידנית של config/ignored-patterns.json).
        private async void UnignorePattern(string key)
        {
            try
            {
                await RunPatternsCliAsync("--unignore", key);
                AppendLog("--- הסתרה בוטלה, ההמלצה תוצג שוב ---");
                RefreshPatterns();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "שגיאה בביטול ההסתרה:\n" + ex.Message, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // מגמה: השוואה אמיתית בין מחצית ראשונה/שנייה של התקופה (patterns.js) -
        // לא הערכה. null כשאין מספיק היסטוריה כדי שההשוואה תהיה משמעותית.
        private Border BuildTrendBadge(string direction, double? percent)
        {
            if (string.IsNullOrEmpty(direction)) return null;

            string text;
            Brush bg, fg;
            if (direction == "new")
            {
                text = "חדש"; bg = Theme.Get("Panel2Brush"); fg = Theme.Get("TextMutedBrush");
            }
            else if (direction == "stable")
            {
                text = "יציב"; bg = Theme.Get("Panel2Brush"); fg = Theme.Get("TextMutedBrush");
            }
            else
            {
                int pct = percent.HasValue ? (int)percent.Value : 0;
                string sign = pct > 0 ? "+" : "";
                string arrow = direction == "up" ? "↑" : "↓";
                text = arrow + " " + sign + pct + "%";
                bg = direction == "up" ? Theme.Get("Panel2Brush") : Theme.Get("AccentLightBrush");
                fg = direction == "up" ? Theme.Get("WarningBrush") : Theme.Get("AccentBrush");
            }

            return new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                Child = new TextBlock { Text = text, Foreground = fg, FontFamily = new FontFamily("Consolas"), FontSize = 11, FontWeight = FontWeights.SemiBold },
            };
        }

        // ביטחון: כמה עקבי הדפוס על פני התקופה (כיסוי-ימים + צפיפות חזרות -
        // patterns.js: computeConfidence). לא ציון AI/ML - שילוב חשבוני פשוט,
        // מוצג ככה גם בטולטיפ כדי לא לתת רושם מוגזם של "המערכת חכמה יותר
        // ממה שהיא". רק למועמדים לאוטומציה (יש להם confidenceScore).
        private Border BuildConfidenceBadge(string label, double? score)
        {
            if (string.IsNullOrEmpty(label)) return null;

            string text;
            Brush bg, fg;
            if (label == "high") { text = "ביטחון גבוה"; bg = Theme.Get("AccentLightBrush"); fg = Theme.Get("AccentBrush"); }
            else if (label == "medium") { text = "ביטחון בינוני"; bg = Theme.Get("Panel2Brush"); fg = Theme.Get("WarningBrush"); }
            else { text = "ביטחון נמוך"; bg = Theme.Get("Panel2Brush"); fg = Theme.Get("TextMutedBrush"); }

            int scoreVal = score.HasValue ? (int)score.Value : 0;
            var badge = new Border
            {
                Background = bg,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2, 8, 2),
                ToolTip = "עד כמה עקבי הדפוס לאורך התקופה (כיסוי ימים + תדירות) - לא ניתוח AI",
                Child = new TextBlock { Text = text + " · " + scoreVal, Foreground = fg, FontFamily = new FontFamily("Consolas"), FontSize = 11, FontWeight = FontWeights.SemiBold },
            };
            return badge;
        }

        // ---------- Privacy ----------

        private UIElement BuildPrivacyTab()
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var hint = new TextBlock
            {
                Text = "כל אפליקציה או מילת מפתח כאן נבדקת *לפני* צילום מסך - אם יש התאמה, לא מצולם שום דבר ולא מריצים OCR. הוסף כאן כל בנק, מנהל סיסמאות, או לקוח רגיש.",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                Margin = new Thickness(0, 0, 0, 16),
            };
            stack.Children.Add(hint);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var appsCard = BuildStringListEditor("אפליקציות מוחרגות (לפי שם תהליך)", out _excludedAppsList, out _newAppBox);
            Grid.SetColumn(appsCard, 0);
            grid.Children.Add(appsCard);

            var keywordsCard = BuildStringListEditor("מילות מפתח מוחרגות (בכותרת החלון)", out _excludedKeywordsList, out _newKeywordBox);
            Grid.SetColumn(keywordsCard, 2);
            grid.Children.Add(keywordsCard);

            stack.Children.Add(grid);

            var settingsCard = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle"), Margin = new Thickness(0, 16, 0, 0) };
            var settingsStack = new StackPanel();
            settingsStack.Children.Add(new TextBlock { Text = "הגדרות הקלטה", Style = (Style)Theme.GetStyle("SectionLabelStyle") });

            settingsStack.Children.Add(new TextBlock { Text = "מרווח בין תצפיות (שניות)", Style = (Style)Theme.GetStyle("HintLabelStyle"), Margin = new Thickness(0, 4, 0, 4) });
            var intervalRow = new StackPanel { Orientation = Orientation.Horizontal };
            _intervalSlider = new Slider { Minimum = 15, Maximum = 300, Width = 400, Value = 60, TickFrequency = 15, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
            _intervalValueLabel = new TextBlock { Text = "60s", Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Get("TextBrush"), Width = 50 };
            _intervalSlider.ValueChanged += (s, e) => _intervalValueLabel.Text = ((int)_intervalSlider.Value) + "s";
            intervalRow.Children.Add(_intervalSlider);
            intervalRow.Children.Add(_intervalValueLabel);
            settingsStack.Children.Add(intervalRow);

            settingsStack.Children.Add(new TextBlock { Text = "שמירת נתונים (ימים) - קבצים ושורות ישנים יותר נמחקים אוטומטית", Style = (Style)Theme.GetStyle("HintLabelStyle"), Margin = new Thickness(0, 14, 0, 4) });
            _retentionBox = new TextBox { Style = (Style)Theme.GetStyle("ModernTextBoxStyle"), Width = 100, HorizontalAlignment = HorizontalAlignment.Left };
            settingsStack.Children.Add(_retentionBox);

            _screenshotEnabledBox = new CheckBox { Content = "צילום מסך מופעל", Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0, 14, 0, 4) };
            _ocrEnabledBox = new CheckBox { Content = "OCR מופעל (דורש tesseract מותקן)", Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0, 4, 0, 0) };
            settingsStack.Children.Add(_screenshotEnabledBox);
            settingsStack.Children.Add(_ocrEnabledBox);

            var saveBtn = new Button { Content = "שמור הגדרות פרטיות", Width = 200, Style = (Style)Theme.GetStyle("AccentButtonStyle"), Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            saveBtn.Click += (s, e) => SavePrivacyFromUi();
            settingsStack.Children.Add(saveBtn);

            settingsCard.Child = settingsStack;
            stack.Children.Add(settingsCard);

            return stack;
        }

        private Border BuildStringListEditor(string title, out ListBox listBox, out TextBox inputBox)
        {
            var card = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle") };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, Style = (Style)Theme.GetStyle("SectionLabelStyle") });

            var list = new ListBox { Style = (Style)Theme.GetStyle("ModernListBoxStyle"), Height = 180 };
            stack.Children.Add(list);

            var addRow = new Grid { Margin = new Thickness(0, 10, 0, 0) };
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            addRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var input = new TextBox { Style = (Style)Theme.GetStyle("ModernTextBoxStyle") };
            Grid.SetColumn(input, 0);
            addRow.Children.Add(input);

            var addBtn = new Button { Content = "הוסף", Width = 70, Margin = new Thickness(8, 0, 0, 0), Style = (Style)Theme.GetStyle("GhostButtonStyle") };
            addBtn.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(input.Text))
                {
                    list.Items.Add(input.Text.Trim());
                    input.Text = "";
                }
            };
            Grid.SetColumn(addBtn, 1);
            addRow.Children.Add(addBtn);

            var removeBtn = new Button { Content = "הסר נבחר", Width = 90, Margin = new Thickness(6, 0, 0, 0), Style = (Style)Theme.GetStyle("GhostButtonStyle") };
            removeBtn.Click += (s, e) =>
            {
                if (list.SelectedItem != null) list.Items.Remove(list.SelectedItem);
            };
            Grid.SetColumn(removeBtn, 2);
            addRow.Children.Add(removeBtn);

            stack.Children.Add(addRow);
            card.Child = stack;

            listBox = list;
            inputBox = input;
            return card;
        }

        private void LoadPrivacyIntoUi()
        {
            _privacyConfig = ConfigStore.LoadPrivacy();

            _excludedAppsList.Items.Clear();
            foreach (var app in Json.AsStringList(_privacyConfig.ContainsKey("excluded_apps") ? _privacyConfig["excluded_apps"] : null))
                _excludedAppsList.Items.Add(app);

            _excludedKeywordsList.Items.Clear();
            foreach (var kw in Json.AsStringList(_privacyConfig.ContainsKey("excluded_title_keywords") ? _privacyConfig["excluded_title_keywords"] : null))
                _excludedKeywordsList.Items.Add(kw);

            double intervalSeconds = Json.GetNumber(_privacyConfig, "capture_interval_ms", 60000) / 1000.0;
            _intervalSlider.Value = Math.Max(_intervalSlider.Minimum, Math.Min(_intervalSlider.Maximum, intervalSeconds));
            _intervalValueLabel.Text = ((int)_intervalSlider.Value) + "s";

            _retentionBox.Text = Json.GetNumber(_privacyConfig, "retention_days", 30).ToString("0");
            _screenshotEnabledBox.IsChecked = Json.GetBool(_privacyConfig, "screenshot_enabled", true);
            _ocrEnabledBox.IsChecked = Json.GetBool(_privacyConfig, "ocr_enabled", true);
        }

        private void SavePrivacyFromUi()
        {
            try
            {
                var data = new Dictionary<string, object>();
                data["_comment"] = Json.GetString(_privacyConfig, "_comment", "נערך דרך AutoProcess Twin GUI");
                data["excluded_apps"] = _excludedAppsList.Items.Cast<string>().ToList();
                data["excluded_title_keywords"] = _excludedKeywordsList.Items.Cast<string>().ToList();
                data["capture_interval_ms"] = (int)(_intervalSlider.Value * 1000);

                int retention;
                if (!int.TryParse(_retentionBox.Text, out retention) || retention < 1) retention = 30;
                data["retention_days"] = retention;

                data["screenshot_enabled"] = _screenshotEnabledBox.IsChecked == true;
                data["ocr_enabled"] = _ocrEnabledBox.IsChecked == true;

                ConfigStore.SavePrivacy(data);
                _privacyConfig = data;
                AppendLog("[privacy] הגדרות פרטיות נשמרו.");
                if (_recorder.IsRunning)
                {
                    AppendLog("[privacy] שים לב: ה-recorder כבר רץ - הגדרות חדשות ייכנסו לתוקף רק אחרי עצירה והפעלה מחדש.");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "שגיאה בשמירה:\n" + ex.Message, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- Guardrails ----------

        private UIElement BuildGuardrailsTab()
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var hint = new TextBlock
            {
                Text = "זו הסכימה של ה-Guardrail Dashboard מהאפיון המקורי. שים לב: זו טבלת קונפיג בלבד - שום דבר כאן לא אוכף שום פעולה אוטונומית בפועל בשלב הנוכחי (ראה README/FINDINGS בתיקיית הפרויקט).",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                Margin = new Thickness(0, 0, 0, 16),
            };
            stack.Children.Add(hint);

            var boundsCard = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle") };
            var boundsStack = new StackPanel();
            boundsStack.Children.Add(new TextBlock { Text = "גבולות תמחור (pricing_boundaries)", Style = (Style)Theme.GetStyle("SectionLabelStyle") });

            boundsStack.Children.Add(new TextBlock { Text = "הנחה אוטומטית מקסימלית (%)", Style = (Style)Theme.GetStyle("HintLabelStyle"), Margin = new Thickness(0, 4, 0, 4) });
            var discountRow = new StackPanel { Orientation = Orientation.Horizontal };
            _discountSlider = new Slider { Minimum = 0, Maximum = 50, Width = 400, Value = 5, TickFrequency = 1, IsSnapToTickEnabled = true, VerticalAlignment = VerticalAlignment.Center };
            _discountValueLabel = new TextBlock { Text = "5%", Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = Theme.Get("TextBrush"), Width = 50 };
            _discountSlider.ValueChanged += (s, e) => _discountValueLabel.Text = ((int)_discountSlider.Value) + "%";
            discountRow.Children.Add(_discountSlider);
            discountRow.Children.Add(_discountValueLabel);
            boundsStack.Children.Add(discountRow);

            var closeRow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            closeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            closeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            closeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var closeStack = new StackPanel();
            closeStack.Children.Add(new TextBlock { Text = "סגירת עסקה אוטומטית עד (סכום)", Style = (Style)Theme.GetStyle("HintLabelStyle") });
            _maxCloseBox = new TextBox { Style = (Style)Theme.GetStyle("ModernTextBoxStyle"), Width = 160, Margin = new Thickness(0, 4, 0, 0) };
            closeStack.Children.Add(_maxCloseBox);
            Grid.SetColumn(closeStack, 0);
            closeRow.Children.Add(closeStack);
            var currStack = new StackPanel();
            currStack.Children.Add(new TextBlock { Text = "מטבע", Style = (Style)Theme.GetStyle("HintLabelStyle") });
            _currencyBox = new TextBox { Style = (Style)Theme.GetStyle("ModernTextBoxStyle"), Width = 100, Margin = new Thickness(0, 4, 0, 0) };
            currStack.Children.Add(_currencyBox);
            Grid.SetColumn(currStack, 2);
            closeRow.Children.Add(currStack);
            boundsStack.Children.Add(closeRow);

            boundsStack.Children.Add(new TextBlock { Text = "מצב הרצה (execution_mode)", Style = (Style)Theme.GetStyle("HintLabelStyle"), Margin = new Thickness(0, 14, 0, 4) });
            _executionModeCombo = new ComboBox { Style = (Style)Theme.GetStyle("ModernComboBoxStyle"), Width = 220, HorizontalAlignment = HorizontalAlignment.Left };
            _executionModeCombo.Items.Add("manual_trigger");
            _executionModeCombo.Items.Add("live_assist");
            _executionModeCombo.Items.Add("full_autopilot");
            boundsStack.Children.Add(_executionModeCombo);
            boundsStack.Children.Add(new TextBlock
            {
                Text = "⚠ full_autopilot לא עושה כלום בפועל בגרסה הזו - אין חיבור אמיתי ל-Gmail/CRM. שינוי הבחירה כאן לא מפעיל שום שליחה אוטומטית.",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                Foreground = Theme.Get("WarningBrush"),
                Margin = new Thickness(0, 6, 0, 0),
            });

            var saveBoundsBtn = new Button { Content = "שמור Guardrails", Width = 180, Style = (Style)Theme.GetStyle("AccentButtonStyle"), Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            saveBoundsBtn.Click += (s, e) => SaveGuardrailsFromUi();
            boundsStack.Children.Add(saveBoundsBtn);

            boundsCard.Child = boundsStack;
            stack.Children.Add(boundsCard);

            // decision_rules
            var rulesCard = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle"), Margin = new Thickness(0, 16, 0, 0) };
            var rulesStack = new StackPanel();
            rulesStack.Children.Add(new TextBlock { Text = "decision_rules", Style = (Style)Theme.GetStyle("SectionLabelStyle") });

            _decisionRulesList = new ListBox { Style = (Style)Theme.GetStyle("ModernListBoxStyle"), Height = 120 };
            _decisionRulesList.SelectionChanged += (s, e) =>
            {
                int idx = _decisionRulesList.SelectedIndex;
                if (idx >= 0 && idx < _decisionRules.Count)
                {
                    var r = _decisionRules[idx];
                    _ruleDescBox.Text = Json.GetString(r, "description", "");
                    _ruleCondBox.Text = Json.GetString(r, "condition", "");
                    _ruleActionBox.Text = Json.GetString(r, "action", "");
                }
            };
            rulesStack.Children.Add(_decisionRulesList);

            _ruleDescBox = LabeledTextBox(rulesStack, "תיאור (description)");
            _ruleCondBox = LabeledTextBox(rulesStack, "תנאי (condition)");
            _ruleActionBox = LabeledTextBox(rulesStack, "פעולה (action, למשל draft_only / flag_for_review)");

            var ruleBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            var addRuleBtn = new Button { Content = "הוסף/עדכן חוק", Width = 130, Style = (Style)Theme.GetStyle("GhostButtonStyle"), Margin = new Thickness(0, 0, 8, 0) };
            addRuleBtn.Click += (s, e) => AddOrUpdateRule();
            var removeRuleBtn = new Button { Content = "מחק חוק נבחר", Width = 120, Style = (Style)Theme.GetStyle("GhostButtonStyle") };
            removeRuleBtn.Click += (s, e) =>
            {
                int idx = _decisionRulesList.SelectedIndex;
                if (idx >= 0 && idx < _decisionRules.Count)
                {
                    _decisionRules.RemoveAt(idx);
                    RefreshDecisionRulesList();
                }
            };
            ruleBtnRow.Children.Add(addRuleBtn);
            ruleBtnRow.Children.Add(removeRuleBtn);
            rulesStack.Children.Add(ruleBtnRow);

            rulesCard.Child = rulesStack;
            stack.Children.Add(rulesCard);

            // escalation_triggers
            var escCard = BuildStringListEditor("escalation_triggers", out _escalationList, out _newEscalationBox);
            escCard.Margin = new Thickness(0, 16, 0, 0);
            stack.Children.Add(escCard);

            return stack;
        }

        private TextBox LabeledTextBox(StackPanel parent, string label)
        {
            parent.Children.Add(new TextBlock { Text = label, Style = (Style)Theme.GetStyle("HintLabelStyle"), Margin = new Thickness(0, 10, 0, 4) });
            var box = new TextBox { Style = (Style)Theme.GetStyle("ModernTextBoxStyle") };
            parent.Children.Add(box);
            return box;
        }

        private void AddOrUpdateRule()
        {
            if (string.IsNullOrWhiteSpace(_ruleDescBox.Text)) return;
            var rule = new Dictionary<string, object>
            {
                { "id", _ruleDescBox.Text.Trim().Replace(" ", "-").ToLowerInvariant() },
                { "description", _ruleDescBox.Text.Trim() },
                { "condition", _ruleCondBox.Text.Trim() },
                { "action", _ruleActionBox.Text.Trim() },
            };

            int idx = _decisionRulesList.SelectedIndex;
            if (idx >= 0 && idx < _decisionRules.Count)
            {
                _decisionRules[idx] = rule;
            }
            else
            {
                _decisionRules.Add(rule);
            }
            RefreshDecisionRulesList();
            _ruleDescBox.Text = ""; _ruleCondBox.Text = ""; _ruleActionBox.Text = "";
        }

        private void RefreshDecisionRulesList()
        {
            _decisionRulesList.Items.Clear();
            foreach (var r in _decisionRules)
            {
                _decisionRulesList.Items.Add(Json.GetString(r, "description", "(ללא תיאור)") + "  →  " + Json.GetString(r, "action", ""));
            }
        }

        private void LoadGuardrailsIntoUi()
        {
            _guardrailsConfig = ConfigStore.LoadGuardrails();
            var bounds = Json.AsDict(_guardrailsConfig.ContainsKey("pricing_boundaries") ? _guardrailsConfig["pricing_boundaries"] : null);

            double discount = Json.GetNumber(bounds, "max_auto_discount_percent", 5);
            _discountSlider.Value = Math.Max(_discountSlider.Minimum, Math.Min(_discountSlider.Maximum, discount));
            _discountValueLabel.Text = ((int)_discountSlider.Value) + "%";

            _maxCloseBox.Text = Json.GetNumber(bounds, "max_auto_close_amount_ils", 5000).ToString("0");
            _currencyBox.Text = Json.GetString(bounds, "currency", "ILS");

            string mode = Json.GetString(_guardrailsConfig, "execution_mode_default", "live_assist");
            _executionModeCombo.SelectedItem = _executionModeCombo.Items.Cast<string>().FirstOrDefault(x => x == mode) ?? "live_assist";
            if (_executionModeCombo.SelectedItem == null) _executionModeCombo.SelectedIndex = 1;

            _decisionRules = new List<Dictionary<string, object>>();
            foreach (var r in Json.AsList(_guardrailsConfig.ContainsKey("decision_rules") ? _guardrailsConfig["decision_rules"] : null))
                _decisionRules.Add(Json.AsDict(r));
            RefreshDecisionRulesList();

            _escalationList.Items.Clear();
            foreach (var t in Json.AsStringList(_guardrailsConfig.ContainsKey("escalation_triggers") ? _guardrailsConfig["escalation_triggers"] : null))
                _escalationList.Items.Add(t);
        }

        private void SaveGuardrailsFromUi()
        {
            try
            {
                var data = new Dictionary<string, object>();
                data["_comment"] = Json.GetString(_guardrailsConfig, "_comment", "נערך דרך AutoProcess Twin GUI");
                data["profile_id"] = Json.GetString(_guardrailsConfig, "profile_id", "default");

                var bounds = new Dictionary<string, object>();
                bounds["max_auto_discount_percent"] = (int)_discountSlider.Value;
                double maxClose;
                if (!double.TryParse(_maxCloseBox.Text, out maxClose) || maxClose < 0) maxClose = 5000;
                bounds["max_auto_close_amount_ils"] = maxClose;
                bounds["currency"] = string.IsNullOrWhiteSpace(_currencyBox.Text) ? "ILS" : _currencyBox.Text.Trim();
                data["pricing_boundaries"] = bounds;

                data["decision_rules"] = _decisionRules.Cast<object>().ToList();
                data["escalation_triggers"] = _escalationList.Items.Cast<string>().ToList();
                data["execution_mode_default"] = _executionModeCombo.SelectedItem as string ?? "live_assist";
                data["_execution_mode_options"] = new List<object> { "manual_trigger", "live_assist", "full_autopilot" };
                data["_note_full_autopilot"] = "full_autopilot דורש חיבור אמיתי ל-Gmail/CRM עם הרשאות מפורשות, ואינו מחובר בשלב זה.";

                ConfigStore.SaveGuardrails(data);
                _guardrailsConfig = data;
                AppendLog("[guardrails] נשמר.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "שגיאה בשמירה:\n" + ex.Message, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ---------- AI (אופציונלי) ----------

        private UIElement BuildAiSettingsTab()
        {
            var stack = new StackPanel { Orientation = Orientation.Vertical };

            var hint = new TextBlock
            {
                Text = "אופציונלי ולגמרי כבוי כברירת מחדל. אם תפעיל, נשלחים לשירות שתגדיר כאן רק נתונים סטטיסטיים מצטברים על פעולות חוזרות (קטגוריה, שם אפליקציה, דקות, מספר חזרות, מגמה) - לעולם לא כותרות חלונות גולמיות, טקסט OCR או צילומי מסך. אפשר לכבות בכל רגע.",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                Margin = new Thickness(0, 0, 0, 16),
                TextWrapping = TextWrapping.Wrap,
            };
            stack.Children.Add(hint);

            var card = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle") };
            var cardStack = new StackPanel();
            cardStack.Children.Add(new TextBlock { Text = "סיכום מנהלים אוטומטי (AI)", Style = (Style)Theme.GetStyle("SectionLabelStyle") });

            _aiEnabledBox = new CheckBox { Content = "הפעל יצירת סיכום AI על בסיס הפעולות החוזרות", Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0, 4, 0, 14) };
            cardStack.Children.Add(_aiEnabledBox);

            cardStack.Children.Add(new TextBlock { Text = "Endpoint (תואם OpenAI Chat Completions)", Style = (Style)Theme.GetStyle("HintLabelStyle"), Margin = new Thickness(0, 0, 0, 4) });
            _aiEndpointBox = new TextBox { Style = (Style)Theme.GetStyle("ModernTextBoxStyle") };
            cardStack.Children.Add(_aiEndpointBox);

            cardStack.Children.Add(new TextBlock { Text = "מודל", Style = (Style)Theme.GetStyle("HintLabelStyle"), Margin = new Thickness(0, 12, 0, 4) });
            _aiModelBox = new TextBox { Style = (Style)Theme.GetStyle("ModernTextBoxStyle"), HorizontalAlignment = HorizontalAlignment.Left, Width = 260 };
            cardStack.Children.Add(_aiModelBox);

            cardStack.Children.Add(new TextBlock { Text = "מפתח API", Style = (Style)Theme.GetStyle("HintLabelStyle"), Margin = new Thickness(0, 12, 0, 4) });
            _aiKeyBox = new PasswordBox { Style = (Style)Theme.GetStyle("ModernPasswordBoxStyle") };
            cardStack.Children.Add(_aiKeyBox);

            var saveBtn = new Button { Content = "שמור הגדרות AI", Width = 160, Style = (Style)Theme.GetStyle("AccentButtonStyle"), Margin = new Thickness(0, 18, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
            saveBtn.Click += (s, e) => SaveAiFromUi();
            cardStack.Children.Add(saveBtn);

            card.Child = cardStack;
            stack.Children.Add(card);

            return stack;
        }

        private void LoadAiIntoUi()
        {
            _aiConfig = ConfigStore.LoadAi();
            _aiEnabledBox.IsChecked = Json.GetBool(_aiConfig, "enabled", false);
            _aiEndpointBox.Text = Json.GetString(_aiConfig, "endpoint", "https://api.openai.com/v1/chat/completions");
            _aiModelBox.Text = Json.GetString(_aiConfig, "model", "gpt-4o-mini");
            // DPAPI: stored value may be encrypted; fall back to plain text for older configs
            _aiKeyBox.Password = CryptoHelper.Decrypt(Json.GetString(_aiConfig, "apiKey", ""));
        }

        private void SaveAiFromUi()
        {
            try
            {
                var data = new Dictionary<string, object>();
                data["_comment"] = Json.GetString(_aiConfig, "_comment", "נערך דרך AutoProcess Twin GUI - נשלחים רק נתונים סטטיסטיים מצטברים, לעולם לא כותרות חלונות גולמיות");
                data["enabled"] = _aiEnabledBox.IsChecked == true;
                data["endpoint"] = string.IsNullOrWhiteSpace(_aiEndpointBox.Text) ? "https://api.openai.com/v1/chat/completions" : _aiEndpointBox.Text.Trim();
                data["model"] = string.IsNullOrWhiteSpace(_aiModelBox.Text) ? "gpt-4o-mini" : _aiModelBox.Text.Trim();
                // DPAPI: encrypt before writing to disk so the key is unusable outside this user session
                data["apiKey"] = CryptoHelper.Encrypt(_aiKeyBox.Password ?? "");

                ConfigStore.SaveAi(data);
                _aiConfig = data;
                AppendLog("[ai] הגדרות AI נשמרו.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "שגיאה בשמירה:\n" + ex.Message, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // קורא ל-agent/src/ai-insights.js ומציג את התוצאה בטאב "פעולות חוזרות".
        // רץ על thread ברקע (לא NodeRunner.RunSync ישיר על ה-UI thread כמו
        // שאר הקריאות) כי זו קריאת רשת חיצונית עם timeout של עד 20 שניות -
        // הקפאת החלון לכל הזמן הזה הייתה חוויה גרועה מדי בשביל פיצ'ר אופציונלי.
        private async void GenerateAiSummary()
        {
            _aiConfig = ConfigStore.LoadAi(); // ליתר ביטחון - ייתכן שנשמר מאז שהטאב הזה נבנה
            if (!Json.GetBool(_aiConfig, "enabled", false))
            {
                _aiSummaryText.Text = "AI כבוי כרגע. כדי להפעיל: הגדרות ← AI (ניסיוני) ← סמנו את התיבה, הזינו endpoint ומפתח, ושמרו.";
                return;
            }
            if (string.IsNullOrWhiteSpace(Json.GetString(_aiConfig, "apiKey", "")))
            {
                _aiSummaryText.Text = "AI מופעל אבל אין מפתח API מוגדר. הוסיפו אותו בהגדרות ← AI (ניסיוני).";
                return;
            }

            _aiSummaryBtn.IsEnabled = false;
            _aiSummaryBtn.Content = "⏳  מייצר סיכום...";
            _aiSummaryText.Text = "שולח נתונים מצטברים בלבד (קטגוריה, דקות, מספר חזרות, מגמה - בלי כותרות חלונות) ומחכה לתשובה...";

            try
            {
                // Decrypt the key in-memory and pass via env var — never written to disk in plain text
                string decryptedKey = CryptoHelper.Decrypt(Json.GetString(_aiConfig, "apiKey", ""));
                var envVars = new System.Collections.Generic.Dictionary<string, string> { { "APT_AI_KEY", decryptedKey } };
                string output = await Task.Run(new Func<string>(() => NodeRunner.RunSyncWithEnv(AppPaths.AiInsightsScript, 30000, envVars)));
                var serializer = new JavaScriptSerializer();
                var data = (Dictionary<string, object>)serializer.DeserializeObject(output.Trim());
                bool enabled = Json.GetBool(data, "enabled", false);
                string summary = Json.GetString(data, "summary", null);
                string reason = Json.GetString(data, "reason", null);
                string error = Json.GetString(data, "error", null);

                if (!enabled)
                {
                    _aiSummaryText.Text = reason == "no-key" ? "אין מפתח API מוגדר." : "AI כבוי.";
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    _aiSummaryText.Text = "שגיאה ביצירת הסיכום: " + error;
                }
                else if (reason == "no-candidates")
                {
                    _aiSummaryText.Text = "אין עדיין מספיק דפוסים לניתוח - נסו שוב אחרי כמה ימי הקלטה נוספים.";
                }
                else if (!string.IsNullOrEmpty(summary))
                {
                    _aiSummaryText.Text = summary;
                }
                else
                {
                    _aiSummaryText.Text = "לא התקבל סיכום מהשירות.";
                }
            }
            catch (Exception ex)
            {
                _aiSummaryText.Text = "שגיאה ביצירת הסיכום: " + ex.Message;
            }
            finally
            {
                _aiSummaryBtn.IsEnabled = true;
                _aiSummaryBtn.Content = "🔄  צור סיכום מחדש";
            }
        }

        // ---------- Briefing ----------

        private UIElement BuildBriefingTab()
        {
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(260) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var listCard = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle") };
            var listStack = new StackPanel();
            var headerRow = new DockPanel();
            headerRow.Children.Add(new TextBlock { Text = "דוחות", Style = (Style)Theme.GetStyle("SectionLabelStyle") });
            listStack.Children.Add(headerRow);

            var genBtn = new Button { Content = "צור דוח חדש", Style = (Style)Theme.GetStyle("GhostButtonStyle"), Margin = new Thickness(0, 0, 0, 6) };
            genBtn.Click += (s, e) => GenerateBriefingAndShow();
            listStack.Children.Add(genBtn);

            var refreshBtn = new Button { Content = "רענן רשימה", Style = (Style)Theme.GetStyle("GhostButtonStyle"), Margin = new Thickness(0, 0, 0, 10) };
            refreshBtn.Click += (s, e) => RefreshReportsList();
            listStack.Children.Add(refreshBtn);

            _reportsList = new ListBox { Style = (Style)Theme.GetStyle("ModernListBoxStyle"), Height = 420 };
            _reportsList.SelectionChanged += (s, e) => ShowSelectedReport();
            listStack.Children.Add(_reportsList);

            listCard.Child = listStack;
            Grid.SetColumn(listCard, 0);
            grid.Children.Add(listCard);

            var viewCard = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle") };
            var viewStack = new Grid();
            _reportEmptyHint = new TextBlock
            {
                Text = "אין עדיין דוחות. לחץ \"צור דוח חדש\" אחרי שהוקלטה פעילות.",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                TextWrapping = TextWrapping.Wrap,
            };
            viewStack.Children.Add(_reportEmptyHint);

            _reportView = new RichTextBox
            {
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = Brushes.Transparent,
                Visibility = Visibility.Collapsed,
            };
            viewStack.Children.Add(_reportView);
            viewCard.Child = viewStack;
            Grid.SetColumn(viewCard, 2);
            grid.Children.Add(viewCard);

            return grid;
        }

        private void RefreshReportsList()
        {
            _reportsList.Items.Clear();
            try
            {
                if (Directory.Exists(AppPaths.ReportsDir))
                {
                    var files = Directory.GetFiles(AppPaths.ReportsDir, "*.md").OrderByDescending(f => f).ToList();
                    foreach (var f in files) _reportsList.Items.Add(Path.GetFileName(f));
                }
            }
            catch { }
        }

        private void ShowSelectedReport()
        {
            if (_reportsList.SelectedItem == null)
            {
                _reportView.Visibility = Visibility.Collapsed;
                _reportEmptyHint.Visibility = Visibility.Visible;
                return;
            }
            try
            {
                string path = Path.Combine(AppPaths.ReportsDir, (string)_reportsList.SelectedItem);
                string content = File.ReadAllText(path);
                _reportView.Document = MarkdownView.Render(content);
                _reportView.Visibility = Visibility.Visible;
                _reportEmptyHint.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                AppendLog("[briefing] שגיאה בפתיחת דוח: " + ex.Message);
            }
        }

        // ---------- About ----------

        private UIElement BuildAboutTab()
        {
            var stack = new StackPanel();

            var card = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle") };
            var cardStack = new StackPanel();
            cardStack.Children.Add(new TextBlock { Text = "AutoProcess Twin — v" + AppVersion, Style = (Style)Theme.GetStyle("SectionLabelStyle") });
            cardStack.Children.Add(new TextBlock
            {
                Text = Strings.AppDescription,
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                Margin = new Thickness(0, 4, 0, 4),
            });
            // Copyright (§5 STANDARDS)
            cardStack.Children.Add(new TextBlock
            {
                Text = Strings.AppCopyright,
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                Margin = new Thickness(0, 0, 0, 16),
                FontStyle = FontStyles.Italic,
            });

            var wizardBtn = new Button { Content = Strings.BtnShowOnboarding, Style = (Style)Theme.GetStyle("AccentButtonStyle"), Margin = new Thickness(0, 0, 0, 16), HorizontalAlignment = HorizontalAlignment.Left, Width = 280 };
            wizardBtn.Click += (s, e) => ShowOnboardingWizard();
            cardStack.Children.Add(wizardBtn);

            AddDocButton(cardStack, Strings.BtnOpenSpec, "SPEC.md");
            AddDocButton(cardStack, Strings.BtnOpenReadme, "README.md");
            AddDocButton(cardStack, Strings.BtnOpenFindings, "FINDINGS.md");

            var openDataBtn = new Button { Content = Strings.BtnOpenDataFolder, Style = (Style)Theme.GetStyle("GhostButtonStyle"), Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 280 };
            openDataBtn.Click += (s, e) =>
            {
                try { AppPaths.EnsureDataFolders(); Process.Start("explorer.exe", AppPaths.DataDir); } catch { }
            };
            cardStack.Children.Add(openDataBtn);

            var openConfigBtn = new Button { Content = Strings.BtnOpenConfigFolder, Style = (Style)Theme.GetStyle("GhostButtonStyle"), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 280 };
            openConfigBtn.Click += (s, e) =>
            {
                try { Process.Start("explorer.exe", AppPaths.ConfigDir); } catch { }
            };
            cardStack.Children.Add(openConfigBtn);

            // בדיקת עדכונים ידנית - escape hatch, בנוסף לבדיקה האוטומטית
            // הפעם-אחת-לכל-הרצה ב-MaybeCheckForUpdates (Loaded). לא חוסם UI.
            var checkUpdatesBtn = new Button { Content = Strings.BtnCheckForUpdates, Style = (Style)Theme.GetStyle("GhostButtonStyle"), Margin = new Thickness(0, 8, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 280 };
            checkUpdatesBtn.Click += (s, e) => ManualCheckForUpdates();
            cardStack.Children.Add(checkUpdatesBtn);

            _updateStatusText = new TextBlock
            {
                Text = Strings.Language == "he" ? "לא נבדק עדיין בהרצה הזו." : "Not checked yet this run.",
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 6, 0, 0),
            };
            cardStack.Children.Add(_updateStatusText);
            if (_pendingUpdate != null) NotifyUpdateAvailable(_pendingUpdate);

            card.Child = cardStack;
            stack.Children.Add(card);

            var briefingHeaderCard = new Border { Style = (Style)Theme.GetStyle("CardBorderStyle"), Margin = new Thickness(0, 16, 0, 0) };
            var briefingHeaderStack = new StackPanel();
            briefingHeaderStack.Children.Add(new TextBlock { Text = Strings.BriefingTitle, Style = (Style)Theme.GetStyle("SectionLabelStyle") });
            briefingHeaderStack.Children.Add(new TextBlock
            {
                Text = Strings.BriefingDesc,
                Style = (Style)Theme.GetStyle("HintLabelStyle"),
                Margin = new Thickness(0, 0, 0, 12),
            });
            briefingHeaderStack.Children.Add(BuildBriefingTab());
            briefingHeaderCard.Child = briefingHeaderStack;
            stack.Children.Add(briefingHeaderCard);

            return stack;
        }

        private void AddDocButton(StackPanel parent, string label, string fileName)
        {
            var btn = new Button { Content = label, Style = (Style)Theme.GetStyle("GhostButtonStyle"), Margin = new Thickness(0, 4, 0, 0), HorizontalAlignment = HorizontalAlignment.Left, Width = 320 };
            btn.Click += (s, e) =>
            {
                try
                {
                    string path = Path.Combine(AppPaths.Root, fileName);
                    if (File.Exists(path)) Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    else MessageBox.Show(this, "לא נמצא: " + path, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "AutoProcess Twin", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            parent.Children.Add(btn);
        }

        // ---------- Footer ----------

        private Border BuildFooter()
        {
            var border = new Border { Background = Theme.Get("HeaderBgBrush"), Padding = new Thickness(20, 6, 20, 6) };
            var text = new TextBlock
            {
                Text = Strings.FooterText,
                Foreground = Theme.Get("HeaderSubTextBrush"),
                FontSize = 11,
            };
            border.Child = text;
            return border;
        }
    }
}
