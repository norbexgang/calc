using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Speech.Recognition;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Linq;
using CalcApp.ViewModels;

namespace CalcApp
{
    public partial class MainWindow : Window
    {
        private Button? _themeToggle;
        private bool _animationsEnabled = true;
        private bool _isAnimating = false;
        private bool _isDarkMode = false;
        private readonly ResourceDictionary _darkThemeDictionary = CreateThemeDictionary(DarkThemePath);
        private readonly ResourceDictionary _lightThemeDictionary = CreateThemeDictionary(LightThemePath);
        private int _themeDictionaryIndex = -1;

        private const string DarkThemePath = "Themes/MaterialTheme.xaml";
        private const string LightThemePath = "Themes/ClassicTheme.xaml";

        private Storyboard? _cachedButtonClickStoryboard;
        private Storyboard? _cachedFadeStoryboard;
        private SpeechControl? _speech;
        private bool _speechEnabled = true;

        public MainWindow()
        {
            LoadComponentFromXaml();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            InitializeTheme();
            FreezeResourceDictionaries();

            try
            {
                var hasRecognizer = HasHungarianRecognizer();
                if (FindName("SpeechToggle") is ToggleButton tb)
                {
                    tb.IsChecked = _speechEnabled && hasRecognizer;
                    tb.Content = tb.IsChecked == true ? "🎤 Beszéd: Be" : "🎤 Beszéd: Ki";
                }

                if (_speechEnabled && hasRecognizer)
                {
                    if (DataContext is CalculatorViewModel viewModel)
                    {
                        _speech = new SpeechControl(viewModel);
                    }
                }
                else if (!hasRecognizer)
                {
                    System.Diagnostics.Debug.WriteLine("No Hungarian speech recognizer installed; speech control disabled.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Speech init failed in MainWindow ctor: {ex}");
            }
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (_themeToggle == null && FindName("ThemeToggle") is Button btn) _themeToggle = btn;
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;

#if DEBUG
            if (_themeToggle != null && FindName("ThemeToggle") is Button btn)
            {
                _themeToggle = btn;
                _cachedButtonClickStoryboard ??= TryFindResource("ButtonClickAnimation") as Storyboard;
                _cachedFadeStoryboard ??= TryFindResource("FadeOutAnimation") as Storyboard;

                try
                {
                    _themeToggle.Click -= ThemeToggle_Click;
                }
                catch { }
            }
#endif
            try { _speech?.Dispose(); } catch { }
            _speech = null;
            _themeToggle = null;
        }

        private void LoadComponentFromXaml()
        {
            try
            {
                var uri = new Uri("/CalcApp;component/MainWindow.xaml", UriKind.Relative);
                Application.LoadComponent(this, uri);

                var viewModel = new CalculatorViewModel();
                DataContext = viewModel;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"CRITICAL: Failed to load main window XAML: {ex}");

                try
                {
                    MessageBox.Show(
                        "Failed to initialize application UI. The application will exit.",
                        "Initialization Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    System.Diagnostics.Debug.WriteLine("Failed to show error message box");
                }

                Application.Current?.Shutdown();
                Environment.Exit(1);
            }
        }

        private void SpeechToggle_Checked(object sender, RoutedEventArgs e)
        {
            EnableSpeech(true);
            if (sender is ToggleButton tb) tb.Content = "🎤 Beszéd: Be";
        }

        private void SpeechToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            EnableSpeech(false);
            if (sender is ToggleButton tb) tb.Content = "🎤 Beszéd: Ki";
        }

        private void EnableSpeech(bool enable)
        {
            _speechEnabled = enable;
            if (enable)
            {
                if (_speech != null) return;
                if (!HasHungarianRecognizer())
                {
                    MessageBox.Show("Nincs telepítve magyar beszédfelismerő; a beszédvezérlés nem elérhető.", "Beszédvezérlés", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (FindName("SpeechToggle") is ToggleButton tb) { tb.IsChecked = false; tb.Content = "🎤 Beszéd: Ki"; }
                    _speechEnabled = false;
                    return;
                }

                try
                {
                    if (DataContext is CalculatorViewModel viewModel)
                    {
                        _speech = new SpeechControl(viewModel);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start speech control: {ex}");
                    MessageBox.Show("A beszédvezérlés indítása nem sikerült.", "Beszédvezérlés hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                try { _speech?.Dispose(); } catch { }
                _speech = null;
            }
        }

        private static bool HasHungarianRecognizer()
        {
            try
            {
                var culture = new System.Globalization.CultureInfo("hu-HU");
                var recognizerInfo = SpeechRecognitionEngine.InstalledRecognizers()
                    .FirstOrDefault(r => r.Culture.Equals(culture));
                return recognizerInfo != null;
            }
            catch
            {
                return false;
            }
        }

        private Button ThemeToggleButton => _themeToggle ??= FindRequiredControl<Button>("ThemeToggle");

        private void FreezeResourceDictionaries()
        {
            try
            {
                foreach (var dict in Resources.MergedDictionaries)
                {
                    foreach (var key in dict.Keys)
                    {
                        if (dict[key] is System.Windows.Freezable f && f.CanFreeze)
                        {
                            f.Freeze();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Freeze resources failed: {ex}");
            }
        }

        private T FindRequiredControl<T>(string name) where T : class
        {
            if (FindName(name) is T control)
            {
                return control;
            }

            throw new InvalidOperationException($"Could not find control '{name}'.");
        }

        private void InitializeTheme()
        {
            UpdateThemeToggleButton();
        }

        private async void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnimating) return;

            _isAnimating = true;
            var previousMode = _isDarkMode;
            var nextMode = !previousMode;

            try
            {
                await AnimateButtonClick().ConfigureAwait(true);
                await FadeOutWindow().ConfigureAwait(true);

                _isDarkMode = nextMode;
                ApplyTheme();
                UpdateThemeToggleButton();

                await FadeInWindow().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                _isDarkMode = previousMode;
                try
                {
                    ApplyTheme();
                    UpdateThemeToggleButton();
                }
                catch { }
                Opacity = 1.0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Theme toggle failed: {ex}");
                _isDarkMode = previousMode;
                try
                {
                    ApplyTheme();
                    UpdateThemeToggleButton();
                }
                catch (Exception innerEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to restore previous theme: {innerEx}");
                }

                Opacity = 1.0;
                MessageBox.Show("Theme switch failed. The previous theme has been restored.", "Theme Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _isAnimating = false;
                Opacity = 1.0;
            }
        }

        private void ApplyTheme()
        {
            var dictionaries = Resources.MergedDictionaries;
            var targetDictionary = _isDarkMode ? _darkThemeDictionary : _lightThemeDictionary;

            if (_themeDictionaryIndex < 0 || _themeDictionaryIndex >= dictionaries.Count)
            {
                _themeDictionaryIndex = FindThemeDictionaryIndex(dictionaries);
                if (_themeDictionaryIndex < 0)
                {
                    dictionaries.Add(targetDictionary);
                    _themeDictionaryIndex = dictionaries.Count - 1;
                    return;
                }
            }

            if (!ReferenceEquals(dictionaries[_themeDictionaryIndex], targetDictionary))
            {
                dictionaries[_themeDictionaryIndex] = targetDictionary;
            }
        }

        private void UpdateThemeToggleButton()
        {
            var button = ThemeToggleButton;
            button.Content = _isDarkMode ? "Light mode" : "Dark mode";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e == null) return;

            try
            {
                if (DataContext is CalculatorViewModel viewModel)
                {
                    var key = e.Key;
                    var modifiers = Keyboard.Modifiers;

                    if (modifiers == ModifierKeys.None && key >= Key.D0 && key <= Key.D9)
                    {
                        var digit = (char)('0' + (e.Key - Key.D0));
                        viewModel.DigitCommand.Execute(digit.ToString());
                        e.Handled = true;
                    }
                    else if (key >= Key.NumPad0 && key <= Key.NumPad9)
                    {
                        var digit = (char)('0' + (key - Key.NumPad0));
                        viewModel.DigitCommand.Execute(digit.ToString());
                        e.Handled = true;
                    }
                    else
                    {
                        switch (key)
                        {
                            case Key.Add:
                            case Key.OemPlus when Keyboard.Modifiers == ModifierKeys.None:
                                viewModel.OperatorCommand.Execute("+");
                                e.Handled = true;
                                break;
                            case Key.Subtract:
                            case Key.OemMinus:
                                viewModel.OperatorCommand.Execute("-");
                                e.Handled = true;
                                break;
                            case Key.Multiply:
                                viewModel.OperatorCommand.Execute("*");
                                e.Handled = true;
                                break;
                            case Key.Divide:
                            case Key.Oem2:
                                viewModel.OperatorCommand.Execute("/");
                                e.Handled = true;
                                break;
                            case Key.Decimal:
                            case Key.OemPeriod:
                                viewModel.DecimalCommand.Execute(null);
                                e.Handled = true;
                                break;
                            case Key.Return:
                            case Key.Enter:
                                viewModel.EqualsCommand.Execute(null);
                                e.Handled = true;
                                break;
                            case Key.Back:
                                viewModel.DeleteCommand.Execute(null);
                                e.Handled = true;
                                break;
                            case Key.Escape:
                                viewModel.ClearCommand.Execute(null);
                                e.Handled = true;
                                break;
                            case Key.C when modifiers == ModifierKeys.Control:
                                viewModel.ClearCommand.Execute(null);
                                e.Handled = true;
                                break;
                            case Key.M when modifiers == ModifierKeys.Control:
                                viewModel.MemoryClearCommand.Execute(null);
                                e.Handled = true;
                                break;
                            case Key.Oem5:
                                viewModel.PercentCommand.Execute(null);
                                e.Handled = true;
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in keyboard handler: {ex}");
            }
        }

        private Storyboard EnsureCachedButtonClickStoryboard(ScaleTransform scaleTransform)
        {
            if (_cachedButtonClickStoryboard != null) return _cachedButtonClickStoryboard;

            var storyboard = new Storyboard();
            var scaleXDown = new DoubleAnimation(1.0, 0.95, TimeSpan.FromMilliseconds(100));
            var scaleYDown = new DoubleAnimation(1.0, 0.95, TimeSpan.FromMilliseconds(100));
            var scaleXUp = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(100)) { BeginTime = TimeSpan.FromMilliseconds(100) };
            var scaleYUp = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(100)) { BeginTime = TimeSpan.FromMilliseconds(100) };

            Storyboard.SetTarget(scaleXDown, scaleTransform);
            Storyboard.SetTargetProperty(scaleXDown, new PropertyPath("ScaleX"));
            Storyboard.SetTarget(scaleYDown, scaleTransform);
            Storyboard.SetTargetProperty(scaleYDown, new PropertyPath("ScaleY"));
            Storyboard.SetTarget(scaleXUp, scaleTransform);
            Storyboard.SetTargetProperty(scaleXUp, new PropertyPath("ScaleX"));
            Storyboard.SetTarget(scaleYUp, scaleTransform);
            Storyboard.SetTargetProperty(scaleYUp, new PropertyPath("ScaleY"));

            storyboard.Children.Add(scaleXDown);
            storyboard.Children.Add(scaleYDown);
            storyboard.Children.Add(scaleXUp);
            storyboard.Children.Add(scaleYUp);
            _cachedButtonClickStoryboard = storyboard;
            return storyboard;
        }

        private async Task AnimateButtonClick()
        {
            if (!_animationsEnabled) return;
            var button = ThemeToggleButton;

            if (button.RenderTransform is not System.Windows.Media.ScaleTransform scaleTransform)
            {
                scaleTransform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                button.RenderTransform = scaleTransform;
            }

            var storyboard = EnsureCachedButtonClickStoryboard(scaleTransform);

            var tcs = new TaskCompletionSource<bool>();
            EventHandler handler = null!;
            handler = (s, e) =>
            {
                try { storyboard.Completed -= handler; } catch { }
                tcs.TrySetResult(true);
            };

            storyboard.Completed += handler;

            storyboard.Begin();
            await tcs.Task.ConfigureAwait(true);
        }

        private async Task FadeOutWindow()
        {
            if (!_animationsEnabled) return;
            await FadeOpacity(1.0, 0.3, TimeSpan.FromMilliseconds(250), new QuadraticEase { EasingMode = EasingMode.EaseOut });
        }

        private async Task FadeInWindow()
        {
            await FadeOpacity(0.3, 1.0, TimeSpan.FromMilliseconds(250), new QuadraticEase { EasingMode = EasingMode.EaseIn });
        }

        private async Task FadeOpacity(double from, double to, TimeSpan duration, IEasingFunction? easing = null)
        {
            var animation = new DoubleAnimation(from, to, duration) { EasingFunction = easing };
            var storyboard = _cachedFadeStoryboard ??= new Storyboard();
            storyboard.Children.Clear();
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
            storyboard.Children.Add(animation);

            var tcs = new TaskCompletionSource<bool>();
            EventHandler handler = null!;
            handler = (s, e) =>
            {
                try { storyboard.Completed -= handler; } catch { }
                tcs.TrySetResult(true);
            };

            storyboard.Completed += handler;
            storyboard.Begin();
            await tcs.Task.ConfigureAwait(true);
        }

        private static ResourceDictionary CreateThemeDictionary(string relativePath)
        {
            try
            {
                return new ResourceDictionary { Source = new Uri(relativePath, UriKind.Relative) };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load theme dictionary '{relativePath}': {ex}");
                return new ResourceDictionary();
            }
        }

        private static int FindThemeDictionaryIndex(IList<ResourceDictionary> dictionaries)
        {
            for (var i = 0; i < dictionaries.Count; i++)
            {
                var source = dictionaries[i].Source?.OriginalString;
                if (string.Equals(source, DarkThemePath, StringComparison.Ordinal) ||
                    string.Equals(source, LightThemePath, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}

