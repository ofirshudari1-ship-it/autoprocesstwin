using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace AutoProcessTwin
{
    // Splash screen — STANDARDS.md §19 splash-screen template:
    //  1) gradient background in the tool's own brand colors (dark-slate -> emerald tint)
    //  2) real logo as the large, dominant, centered element
    //  3) frameless, transparent, rounded-corner window (WPF equivalent of
    //     WinForms FormBorderStyle=None + region rounding: WindowStyle=None +
    //     AllowsTransparency=true + a rounded Border host)
    //  4) a subtle continuous spinner (rotating arc), not a fake/indeterminate progress bar
    //  5) minimum 800ms display enforced in code + an 8-second hard safety timeout
    //  6) MainWindow is constructed but never shown until the splash is done —
    //     app.ShutdownMode = OnLastWindowClose keeps the app alive meanwhile, no flicker.
    public class SplashWindow : Window
    {
        // Brand palette (assets/BRAND.md — dark-slate + emerald, the palette
        // actually shipped in Theme.cs/reports/landing page; the old purple
        // #6450DC used here previously was never the real brand color).
        private static readonly Color BgDark = Color.FromRgb(0x0F, 0x17, 0x2A);   // BG Dark
        private static readonly Color SurfaceDark = Color.FromRgb(0x1E, 0x29, 0x3B); // Surface Dark
        private static readonly Color Accent = Color.FromRgb(0x10, 0xB9, 0x81);   // Primary/Accent (emerald)
        private static readonly Color AccentHover = Color.FromRgb(0x34, 0xD3, 0x99); // Primary Hover (dark theme)
        private static readonly Color TextMuted = Color.FromRgb(0x94, 0xA3, 0xB8);   // Text Muted (dark theme)

        private const double MinDisplayMs = 1500; // within the 1.5–2.5s band, well above the 800ms floor
        private const double MaxDisplayMs = 8000; // hard safety timeout — never blocks the app longer than this

        private readonly DateTime _shownAt;
        private TextBlock _statusText;
        private RotateTransform _spinnerRotate;
        private DispatcherTimer _safetyTimer;
        private MainWindow _mainWindow;
        private bool _closed;

        public SplashWindow()
        {
            _shownAt = DateTime.Now;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;
            Width = 440;
            Height = 320;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;

            // (1) Gradient background in the app's own brand colors — a diagonal
            // dark-slate sweep with a faint emerald tint near the bottom-right,
            // never plain white/gray/framework default.
            var bgGradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            bgGradient.GradientStops.Add(new GradientStop(BgDark, 0.0));
            bgGradient.GradientStops.Add(new GradientStop(SurfaceDark, 0.55));
            bgGradient.GradientStops.Add(new GradientStop(Color.FromRgb(0x16, 0x2A, 0x24), 1.0)); // subtle emerald-tinted slate

            // (3) Frameless + transparent + rounded corners.
            var root = new Border
            {
                Background = bgGradient,
                CornerRadius = new CornerRadius(18),
                BorderBrush = new SolidColorBrush(Color.FromArgb(40, 0x10, 0xB9, 0x81)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(40, 40, 40, 32),
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            // (2) Real logo — large and dominant, not a small icon in the corner.
            var iconHost = new Border
            {
                Width = 108,
                Height = 108,
                CornerRadius = new CornerRadius(24),
                Background = new SolidColorBrush(Accent),
                Margin = new Thickness(0, 0, 0, 20),
                HorizontalAlignment = HorizontalAlignment.Center,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Accent,
                    Opacity = 0.35,
                    BlurRadius = 28,
                    ShadowDepth = 0,
                },
            };
            var iconText = new TextBlock
            {
                Text = "APT",
                Foreground = Brushes.White,
                FontSize = 30,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var iconGrid = new Grid();
            iconGrid.Children.Add(iconText);
            iconHost.Child = iconGrid;

            // Prefer the real brand logo when it's on disk (assets/icons ->
            // AppIcon.ico copied next to the exe at build time); fall back to
            // the emerald monogram tile above if it's missing for any reason.
            try
            {
                string iconPath = System.IO.Path.Combine(AppPaths.Root, "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    var img = new Image { Width = 96, Height = 96, HorizontalAlignment = HorizontalAlignment.Center, Stretch = Stretch.Uniform };
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(iconPath);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 96;
                    bmp.EndInit();
                    img.Source = bmp;
                    iconHost.Background = Brushes.Transparent;
                    iconHost.Child = img;
                }
            }
            catch { /* keep the monogram fallback */ }

            stack.Children.Add(iconHost);

            stack.Children.Add(new TextBlock
            {
                Text = "AutoProcess Twin",
                Foreground = Brushes.White,
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 4),
            });

            stack.Children.Add(new TextBlock
            {
                Text = "v" + MainWindow.AppVersion,
                Foreground = new SolidColorBrush(TextMuted),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 22),
            });

            // (4) Subtle continuous spinner — a rotating emerald arc, not an
            // indeterminate/marquee ProgressBar pretending to show progress.
            stack.Children.Add(BuildSpinner());

            _statusText = new TextBlock
            {
                Text = Strings.SplashLoading,
                Foreground = new SolidColorBrush(TextMuted),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 14, 0, 0),
            };
            stack.Children.Add(_statusText);

            root.Child = stack;
            Content = root;

            // Fade in
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            Opacity = 0;
            Loaded += (s, e) =>
            {
                BeginAnimation(OpacityProperty, fadeIn);
                StartSpinner();

                // (5) Hard safety timeout: whatever else happens (slow MainWindow
                // construction, a stalled dispatcher, etc.) the splash never blocks
                // the app for more than MaxDisplayMs.
                _safetyTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(MaxDisplayMs) };
                _safetyTimer.Tick += (ts, te) =>
                {
                    _safetyTimer.Stop();
                    ForceFinish();
                };
                _safetyTimer.Start();

                // Schedule MainWindow creation after a short render delay
                var startTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
                startTimer.Tick += (ts, te) =>
                {
                    startTimer.Stop();
                    CreateAndShowMain();
                };
                startTimer.Start();
            };
        }

        private UIElement BuildSpinner()
        {
            const double size = 30;
            var canvas = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center };

            // Faint full ring (track) so the moving arc reads as a spinner, not a stray mark.
            var track = new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = new SolidColorBrush(Color.FromArgb(35, 0xFF, 0xFF, 0xFF)),
                StrokeThickness = 3,
            };
            canvas.Children.Add(track);

            // Moving arc: a ring stroked with only ~30% of its length visible,
            // via a DashArray, then continuously rotated — a real spinner, no
            // percentage/determinate claim.
            var arc = new Ellipse
            {
                Width = size,
                Height = size,
                Stroke = new LinearGradientBrush(AccentHover, Accent, new Point(0, 0), new Point(1, 1)),
                StrokeThickness = 3,
                StrokeDashArray = new DoubleArray(2.6, 6.4).ToDoubleCollection(),
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
            };
            _spinnerRotate = new RotateTransform(0, size / 2, size / 2);
            arc.RenderTransform = _spinnerRotate;
            canvas.Children.Add(arc);

            return canvas;
        }

        private void StartSpinner()
        {
            if (_spinnerRotate == null) return;
            var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(1100))
            {
                RepeatBehavior = RepeatBehavior.Forever,
            };
            _spinnerRotate.BeginAnimation(RotateTransform.AngleProperty, spin);
        }

        // Small helper so the stroke-dash-array literal above stays readable.
        private class DoubleArray
        {
            private readonly double[] _values;
            public DoubleArray(params double[] values) { _values = values; }
            public DoubleCollection ToDoubleCollection() { return new DoubleCollection(_values); }
        }

        private void CreateAndShowMain()
        {
            _statusText.Text = Strings.SplashLoading;
            try
            {
                _mainWindow = new MainWindow();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Startup error: " + ex.Message, "AutoProcess Twin",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
                return;
            }

            _statusText.Text = Strings.SplashReady;

            var elapsed = (DateTime.Now - _shownAt).TotalMilliseconds;
            var remaining = Math.Max(0, MinDisplayMs - elapsed);

            var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(remaining) };
            closeTimer.Tick += (cs, ce) =>
            {
                closeTimer.Stop();
                FinishAndClose();
            };
            closeTimer.Start();
        }

        // (6) MainWindow was created hidden (never Shown) above; only here,
        // once the splash's minimum display time is up, does it become visible.
        private void FinishAndClose()
        {
            if (_closed) return;
            _closed = true;
            if (_safetyTimer != null) { _safetyTimer.Stop(); _safetyTimer = null; }

            _mainWindow.Show();
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            fadeOut.Completed += (fs, fe) => Close();
            BeginAnimation(OpacityProperty, fadeOut);
        }

        // Fired only if MaxDisplayMs elapses before the normal flow finishes
        // (e.g. MainWindow construction took unusually long). Falls back to a
        // best-effort show/close instead of hanging on screen indefinitely.
        private void ForceFinish()
        {
            if (_closed) return;
            try
            {
                if (_mainWindow == null) _mainWindow = new MainWindow();
                FinishAndClose();
            }
            catch (Exception ex)
            {
                _closed = true;
                MessageBox.Show("Startup error: " + ex.Message, "AutoProcess Twin",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown(1);
            }
        }
    }
}
