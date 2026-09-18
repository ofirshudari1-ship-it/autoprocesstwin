using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace AutoProcessTwin
{
    // אשף פתיחה ראשוני - מוצג פעם אחת (או לפי דרישה, דרך "הצג שוב" בטאב אודות).
    // המטרה: לענות במפורש על "מה זה עושה" ו"מה זה לא עושה" לפני שהמשתמש מגיע
    // ללוח בקרה ריק ומרגיש אבוד. לא נוגע בשום קונפיג בעצמו - רק מסביר ומציע
    // להתחיל הקלטה בסוף.
    public class OnboardingWizard : Window
    {
        public bool StartRecordingRequested { get; private set; }

        private readonly List<Border> _pages = new List<Border>();
        private readonly List<Border> _dots = new List<Border>();
        private int _current = 0;
        private Button _backBtn, _nextBtn;
        private CheckBox _startNowCheck;

        public OnboardingWizard(Window owner)
        {
            Owner = owner;
            Title = Strings.OnboardingTitle;
            Width = 620;
            Height = 560;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = Theme.Get("BgBrush");
            FontFamily = new FontFamily("Segoe UI");
            FlowDirection = Strings.Language == "he" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Icon = owner != null ? owner.Icon : null;

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // dots
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // page content
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // nav buttons

            var dotsPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 20, 0, 10) };
            Grid.SetRow(dotsPanel, 0);
            root.Children.Add(dotsPanel);

            var pageHost = new Grid { Margin = new Thickness(32, 0, 32, 0) };
            Grid.SetRow(pageHost, 1);
            root.Children.Add(pageHost);

            AddPage(pageHost, dotsPanel, BuildWelcomePage());
            AddPage(pageHost, dotsPanel, BuildPrivacyPage());
            AddPage(pageHost, dotsPanel, BuildHowItWorksPage());
            AddPage(pageHost, dotsPanel, BuildFinishPage());

            var nav = BuildNav();
            Grid.SetRow(nav, 2);
            root.Children.Add(nav);

            Content = root;
            ShowPage(0);
        }

        private void AddPage(Grid host, StackPanel dotsPanel, Border page)
        {
            page.Visibility = Visibility.Collapsed;
            host.Children.Add(page);
            _pages.Add(page);

            var dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(4),
                Background = Theme.Get("BorderColorBrush"),
                Margin = new Thickness(4, 0, 4, 0),
            };
            dotsPanel.Children.Add(dot);
            _dots.Add(dot);
        }

        private void ShowPage(int index)
        {
            _current = index;
            for (int i = 0; i < _pages.Count; i++)
            {
                _pages[i].Visibility = i == index ? Visibility.Visible : Visibility.Collapsed;
                _dots[i].Background = i == index ? Theme.Get("AccentBrush") : Theme.Get("BorderColorBrush");
            }
            _backBtn.Visibility = index == 0 ? Visibility.Hidden : Visibility.Visible;
            _nextBtn.Content = index == _pages.Count - 1 ? Strings.BtnFinish : Strings.BtnNext;
        }

        private Border BuildNav()
        {
            var border = new Border { Padding = new Thickness(32, 16, 32, 24) };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var skipBtn = new Button { Content = Strings.BtnSkip, Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 90 };
            skipBtn.Click += (s, e) => { StartRecordingRequested = false; DialogResult = true; };
            Grid.SetColumn(skipBtn, 0);
            grid.Children.Add(skipBtn);

            var rightStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
            _backBtn = new Button { Content = Strings.BtnBack, Style = (Style)Theme.GetStyle("GhostButtonStyle"), Width = 90, Margin = new Thickness(0, 0, 8, 0) };
            _backBtn.Click += (s, e) => { if (_current > 0) ShowPage(_current - 1); };
            _nextBtn = new Button { Content = Strings.BtnNext, Style = (Style)Theme.GetStyle("AccentButtonStyle"), Width = 110 };
            _nextBtn.Click += (s, e) =>
            {
                if (_current < _pages.Count - 1) ShowPage(_current + 1);
                else
                {
                    StartRecordingRequested = _startNowCheck != null && _startNowCheck.IsChecked == true;
                    DialogResult = true;
                }
            };
            rightStack.Children.Add(_backBtn);
            rightStack.Children.Add(_nextBtn);
            Grid.SetColumn(rightStack, 2);
            grid.Children.Add(rightStack);

            border.Child = grid;
            return border;
        }

        private static TextBlock PageTitle(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = Theme.Get("TextBrush"),
                Margin = new Thickness(0, 0, 0, 14),
                TextWrapping = TextWrapping.Wrap,
            };
        }

        private static TextBlock Body(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 14,
                Foreground = Theme.Get("TextBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12),
                LineHeight = 22,
            };
        }

        private static Border Card(UIElement content, Brush accent = null)
        {
            var b = new Border
            {
                Style = (Style)Theme.GetStyle("CardBorderStyle"),
                Margin = new Thickness(0, 0, 0, 12),
            };
            if (accent != null) b.BorderBrush = accent;
            b.Child = content;
            return b;
        }

        // ---------- Page 1: מה זה כן עושה, מה זה לא עושה ----------
        private Border BuildWelcomePage()
        {
            var stack = new StackPanel();
            stack.Children.Add(PageTitle("ברוך הבא ל-AutoProcess Twin 👋"));
            stack.Children.Add(Body(
                "זה סייען שמקליט אילו אפליקציות אתה עובד איתן במהלך היום, ומכין לך " +
                "כל בוקר סיכום קצר - \"תדרוך בוקר\" - שמראה איפה עבר הזמן שלך. הכל " +
                "רץ מקומית על המחשב הזה. שום דבר לא נשלח לענן, לאף אחד."));

            var doStack = new StackPanel();
            doStack.Children.Add(new TextBlock { Text = "מה זה כן עושה עכשיו", FontWeight = FontWeights.Bold, Foreground = Theme.Get("AccentBrush"), Margin = new Thickness(0, 0, 0, 8) });
            foreach (var line in new[] {
                "רושם כל כמה שניות איזו אפליקציה פעילה + צילום מסך",
                "מזהה טקסט בצילום (OCR) כדי לתת הקשר",
                "מכין דוח יומי שמראה איפה עבר הזמן",
                "מוחק אוטומטית נתונים ישנים (ברירת מחדל: אחרי 30 יום)",
            })
            {
                doStack.Children.Add(new TextBlock { Text = "•  " + line, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap });
            }
            stack.Children.Add(Card(doStack, Theme.Get("AccentBrush")));

            var dontStack = new StackPanel();
            dontStack.Children.Add(new TextBlock { Text = "🚫 מה זה לא עושה (בכוונה, לא זמנית)", FontWeight = FontWeights.Bold, Foreground = Theme.Get("WarningBrush"), Margin = new Thickness(0, 0, 0, 8) });
            foreach (var line in new[] {
                "לא שולח מיילים ולא סוגר עסקאות לבד",
                "לא מקליד/שומר מה שאתה מקליד במקלדת",
                "לא מקליט שמע",
                "לא עושה שום פעולה בלי שאתה תלחץ בעצמך",
            })
            {
                dontStack.Children.Add(new TextBlock { Text = "•  " + line, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0, 0, 0, 4), TextWrapping = TextWrapping.Wrap });
            }
            stack.Children.Add(Card(dontStack, Theme.Get("WarningBrush")));

            return WrapPage(stack);
        }

        // ---------- Page 2: פרטיות ----------
        private Border BuildPrivacyPage()
        {
            var stack = new StackPanel();
            stack.Children.Add(PageTitle("פרטיות קודם כל 🔒"));
            stack.Children.Add(Body(
                "לפני שמצלמים משהו, המערכת בודקת אם האפליקציה הפעילה ברשימת ההחרגה. " +
                "אם כן - שום צילום מסך לא נלקח בכלל, ורק נרשם \"סונן\" בלי שום תוכן."));

            Dictionary<string, object> privacy = null;
            try { privacy = ConfigStore.LoadPrivacy(); } catch { }
            var apps = privacy != null ? Json.AsStringList(privacy.ContainsKey("excluded_apps") ? privacy["excluded_apps"] : null) : new List<string>();

            var listStack = new StackPanel();
            listStack.Children.Add(new TextBlock { Text = "כבר מוחרג כברירת מחדל:", FontWeight = FontWeights.Bold, Foreground = Theme.Get("TextBrush"), Margin = new Thickness(0, 0, 0, 8) });
            var wrap = new WrapPanel();
            foreach (var app in apps)
            {
                wrap.Children.Add(new Border
                {
                    Background = Theme.Get("Panel2Brush"),
                    CornerRadius = new CornerRadius(14),
                    Padding = new Thickness(12, 5, 12, 5),
                    Margin = new Thickness(0, 0, 8, 8),
                    Child = new TextBlock { Text = app, Foreground = Theme.Get("TextBrush"), FontSize = 12 },
                });
            }
            listStack.Children.Add(wrap);
            stack.Children.Add(Card(listStack));

            stack.Children.Add(Body(
                "אפשר להוסיף עוד (למשל לקוח ספציפי, אפליקציה פנימית) בטאב \"פרטיות\" " +
                "אחרי שהאשף הזה נסגר - שם יש רשימה עם כפתור הוספה/הסרה, לא JSON."));

            return WrapPage(stack);
        }

        // ---------- Page 3: איך זה עובד ----------
        private Border BuildHowItWorksPage()
        {
            var stack = new StackPanel();
            stack.Children.Add(PageTitle("איך זה עובד ביום-יום 🛠"));

            var steps = new string[,]
            {
                { "▶ / ⏹ למעלה מימין", "מתחיל או עוצר הקלטה. כשמוקלט, רואים שם גם ספירה לאחור לתצפית הבאה." },
                { "לוח בקרה", "כמה תצפיות נרשמו היום ויומן חי של מה שקורה." },
                { "פרטיות", "מה מוחרג מהקלטה, ומרווח הזמן בין תצפית לתצפית." },
                { "Guardrails", "טבלת גבולות (הנחה מקסימלית, סכום עסקה) שמוכנה לשלב הבא - היום היא לא אוכפת שום דבר אוטומטית, רק קונפיג." },
                { "תדרוך בוקר", "לוחצים \"צור תדרוך בוקר\" בלוח הבקרה, ורואים כאן דוח: איפה עבר הזמן, ומה בדיוק סונן ולמה." },
            };
            for (int i = 0; i < steps.GetLength(0); i++)
            {
                string label = steps[i, 0];
                string desc = steps[i, 1];
                var row = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
                row.Children.Add(new TextBlock { Text = label, FontWeight = FontWeights.Bold, Foreground = Theme.Get("AccentBrush"), FontSize = 14 });
                row.Children.Add(new TextBlock { Text = desc, Foreground = Theme.Get("TextBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) });
                stack.Children.Add(row);
            }

            return WrapPage(stack);
        }

        // ---------- Page 4: סיום ----------
        private Border BuildFinishPage()
        {
            var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stack.Children.Add(PageTitle("מוכן להתחיל"));
            stack.Children.Add(Body("אפשר להתחיל להקליט מיד, או לפתוח קודם את הטאבים ולהתאים הגדרות. אפשר לחזור למדריך הזה בכל רגע דרך טאב \"אודות\"."));

            _startNowCheck = new CheckBox
            {
                Content = "התחל הקלטה מיד",
                IsChecked = true,
                Foreground = Theme.Get("TextBrush"),
                Margin = new Thickness(0, 12, 0, 0),
                FontSize = 14,
            };
            stack.Children.Add(_startNowCheck);

            return WrapPage(stack);
        }

        private Border WrapPage(StackPanel content)
        {
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Content = content };
            return new Border { Child = scroll };
        }
    }
}
