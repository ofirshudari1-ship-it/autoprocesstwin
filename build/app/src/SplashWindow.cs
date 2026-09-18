using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace AutoProcessTwin
{
    // Splash screen — shown for minimum 1.5 seconds (STANDARDS §5).
    // After the timer fires it creates MainWindow, shows it, and closes itself.
    // ShutdownMode.OnLastWindowClose means the app keeps running because
    // MainWindow is already visible when we close here.
    public class SplashWindow : Window
    {
        private const double MinDisplayMs = 1500;
        private readonly DateTime _shownAt;
        private TextBlock _statusText;
        private ProgressBar _progress;

        public SplashWindow()
        {
            _shownAt = DateTime.Now;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            ResizeMode = ResizeMode.NoResize;
            Width = 400;
            Height = 260;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brushes.Transparent;
            Topmost = true;

            var root = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(18, 18, 26)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(40, 36, 40, 36),
            };

            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };

            // Logo / icon
            var iconHost = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromRgb(100, 80, 220)),
                Margin = new Thickness(0, 0, 0, 18),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var iconText = new TextBlock
            {
                Text = "APT",
                Foreground = Brushes.White,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            };
            var iconGrid = new Grid();
            iconGrid.Children.Add(iconText);
            iconHost.Child = iconGrid;

            // Try loading real icon
            try
            {
                string iconPath = Path.Combine(AppPaths.Root, "AppIcon.ico");
                if (File.Exists(iconPath))
                {
                    var img = new Image { Width = 48, Height = 48, HorizontalAlignment = HorizontalAlignment.Center };
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(iconPath);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    img.Source = bmp;
                    iconHost.Child = img;
                }
            }
            catch { }

            stack.Children.Add(iconHost);

            stack.Children.Add(new TextBlock
            {
                Text = "AutoProcess Twin",
                Foreground = Brushes.White,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6),
            });

            stack.Children.Add(new TextBlock
            {
                Text = "v" + MainWindow.AppVersion,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 130, 180)),
                FontSize = 13,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 24),
            });

            _progress = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 3,
                Margin = new Thickness(0, 0, 0, 10),
                Background = new SolidColorBrush(Color.FromRgb(40, 38, 60)),
                Foreground = new SolidColorBrush(Color.FromRgb(100, 80, 220)),
                BorderThickness = new Thickness(0),
            };
            stack.Children.Add(_progress);

            _statusText = new TextBlock
            {
                Text = Strings.SplashLoading,
                Foreground = new SolidColorBrush(Color.FromRgb(140, 130, 180)),
                FontSize = 12,
                FontFamily = new FontFamily("Segoe UI"),
                HorizontalAlignment = HorizontalAlignment.Center,
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

        private void CreateAndShowMain()
        {
            _statusText.Text = Strings.SplashLoading;
            MainWindow win;
            try
            {
                win = new MainWindow();
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
                win.Show();
                // Fade out
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
                fadeOut.Completed += (fs, fe) => Close();
                BeginAnimation(OpacityProperty, fadeOut);
            };
            closeTimer.Start();
        }
    }
}
