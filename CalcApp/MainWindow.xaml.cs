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
using System.Windows.Media.Effects;
using System.Linq;
using CalcApp.ViewModels;
using Serilog;

namespace CalcApp
{
    /// <summary>
    /// A MainWindow.xaml interakciós logikája.
    /// </summary>
    public partial class MainWindow : Window
    {
        // Theme toggle removed
        // Theme toggle removed
        private readonly bool _animationsEnabled = true;

        private int _themeDictionaryIndex = -1;

        private const string ExperimentalDarkThemePath = "Themes/ExperimentalDark.xaml";

        private readonly ResourceDictionary _experimentalDarkThemeDictionary = CreateThemeDictionary(ExperimentalDarkThemePath);

        private bool _isTurbo = false;

        private Storyboard? _cachedButtonClickStoryboard;
        private Storyboard? _cachedFadeStoryboard;
        private SpeechControl? _speech;
        private bool _speechEnabled = true;

        private readonly DropShadowEffect _defaultWindowShadow = new() { Color = Colors.Black, Opacity = 0.35, BlurRadius = 8, ShadowDepth = 3, Direction = 270 };
        private readonly DropShadowEffect _defaultButtonShadow = new() { Color = Color.FromRgb(209, 196, 233), Opacity = 0.4, BlurRadius = 12, ShadowDepth = 4, Direction = 270 };
        private readonly DropShadowEffect _defaultButtonHoverShadow = new() { Color = Color.FromRgb(209, 196, 233), Opacity = 0.6, BlurRadius = 16, ShadowDepth = 4, Direction = 270 };

        /// <summary>
        /// Inicializálja a MainWindow új példányát.
        /// </summary>
        public MainWindow()
        {
            LoadComponentFromXaml();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            InitializeTheme();
            UpdateShadowResources();
            FreezeResourceDictionaries();
            InitializeKeyMappings();

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
                    // System.Diagnostics.Debug.WriteLine("No Hungarian speech recognizer installed; speech control disabled.");
                }
            }
            catch (Exception)
            {
                // System.Diagnostics.Debug.WriteLine($"Speech init failed in MainWindow ctor: {ex}");
            }
        }

        /// <summary>
        /// Az ablak betöltésekor lefutó eseménykezelő.
        /// </summary>
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Theme toggle removed
            // Experimental toggle removed
            if (FindName("TurboToggle") is ToggleButton turboBtn)
            {
                turboBtn.IsChecked = _isTurbo;
            }
        }

        /// <summary>
        /// Az ablak bezárásakor lefutó eseménykezelő.
        /// </summary>
        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;

#if DEBUG
            // Debug cleanup if needed
#endif
            try { _speech?.Dispose(); } catch { }
            _speech = null;
        }

        /// <summary>
        /// Betölti a komponenst a XAML-ből.
        /// </summary>
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
                Log.Fatal(ex, "CRITICAL: Failed to load main window XAML");

                try
                {
                    MessageBox.Show(
                        "Failed to initialize application UI. The application will exit.",
                        "Initialization Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch (Exception logEx)
                {
                    Log.Error(logEx, "Failed to show error message box");
                }

                Application.Current?.Shutdown();
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// A beszédvezérlés bekapcsolásakor lefutó eseménykezelő.
        /// </summary>
        private void SpeechToggle_Checked(object sender, RoutedEventArgs e)
        {
            EnableSpeech(true);
            if (sender is ToggleButton tb) tb.Content = "🎤 Beszéd: Be";
        }

        /// <summary>
        /// A beszédvezérlés kikapcsolásakor lefutó eseménykezelő.
        /// </summary>
        private void SpeechToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            EnableSpeech(false);
            if (sender is ToggleButton tb) tb.Content = "🎤 Beszéd: Ki";
        }

        /// <summary>
        /// Engedélyezi vagy letiltja a beszédvezérlést.
        /// </summary>
        /// <param name="enable">Igaz, ha engedélyezni kell, egyébként hamis.</param>
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
                    Log.Error(ex, "Failed to start speech control");
                    MessageBox.Show("A beszédvezérlés indítása nem sikerült.", "Beszédvezérlés hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                try { _speech?.Dispose(); } catch { }
                _speech = null;
            }
        }

        private static bool? _hasHungarianRecognizer;
        /// <summary>
        /// Ellenőrzi, hogy van-e telepítve magyar beszédfelismerő.
        /// </summary>
        /// <returns>Igaz, ha van, egyébként hamis.</returns>
        private static bool HasHungarianRecognizer()
        {
            if (_hasHungarianRecognizer.HasValue) return _hasHungarianRecognizer.Value;

            try
            {
                var culture = new System.Globalization.CultureInfo("hu-HU");
                var recognizerInfo = SpeechRecognitionEngine.InstalledRecognizers()
                    .FirstOrDefault(r => r.Culture.Equals(culture));
                _hasHungarianRecognizer = recognizerInfo != null;
            }
            catch
            {
                _hasHungarianRecognizer = false;
            }
            return _hasHungarianRecognizer.Value;
        }


        /// <summary>
        /// "Befagyasztja" az erőforrás-szótárakat a teljesítmény javítása érdekében.
        /// </summary>
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
                Log.Error(ex, "Freeze resources failed");
            }
        }

        /// <summary>
        /// Megkeres egy kötelező vezérlőt a név alapján.
        /// </summary>
        /// <typeparam name="T">A vezérlő típusa.</typeparam>
        /// <param name="name">A vezérlő neve.</param>
        /// <returns>A megtalált vezérlő.</returns>
        private T FindRequiredControl<T>(string name) where T : class
        {
            if (FindName(name) is T control)
            {
                return control;
            }

            throw new InvalidOperationException($"Could not find control '{name}'.");
        }

        /// <summary>
        /// Inicializálja a témát.
        /// </summary>
        private void InitializeTheme()
        {
            ApplyTheme();
        }

        /// <summary>
        /// Alkalmazza a kiválasztott témát.
        /// </summary>
        private void ApplyTheme()
        {
            var dictionaries = Resources.MergedDictionaries;
            ResourceDictionary targetDictionary;

            // Always use experimental themes
            // Always use experimental dark theme
            targetDictionary = _experimentalDarkThemeDictionary;

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

        // ExperimentalToggle_Click removed

        /// <summary>
        /// A turbó mód váltó gomb kattintásakor lefutó eseménykezelő.
        /// </summary>
        private void TurboToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
            {
                _isTurbo = tb.IsChecked == true;
                if (DataContext is CalculatorViewModel vm)
                {
                    vm.SetTurboMode(_isTurbo);
                }
                UpdateShadowResources();
            }
        }

        /// <summary>
        /// Frissíti az árnyék erőforrásokat.
        /// </summary>
        private void UpdateShadowResources()
        {
            if (_isTurbo)
            {
                Resources["WindowShadowEffect"] = null;
                Resources["ButtonShadowEffect"] = null;
                Resources["ButtonHoverShadowEffect"] = null;
            }
            else
            {
                Resources["WindowShadowEffect"] = _defaultWindowShadow;
                Resources["ButtonShadowEffect"] = _defaultButtonShadow;
                Resources["ButtonHoverShadowEffect"] = _defaultButtonHoverShadow;
            }
        }

        private readonly Dictionary<Key, Action<CalculatorViewModel>> _keyMappings = [];

        /// <summary>
        /// Inicializálja a billentyűleképezéseket.
        /// </summary>
        private void InitializeKeyMappings()
        {
            // Digits 0-9
            for (var k = Key.D0; k <= Key.D9; k++)
            {
                var digit = (char)('0' + (k - Key.D0));
                _keyMappings[k] = vm => vm.DigitCommand.Execute(digit.ToString());
            }
            for (var k = Key.NumPad0; k <= Key.NumPad9; k++)
            {
                var digit = (char)('0' + (k - Key.NumPad0));
                _keyMappings[k] = vm => vm.DigitCommand.Execute(digit.ToString());
            }

            // Operators
            _keyMappings[Key.Add] = vm => vm.OperatorCommand.Execute("+");
            _keyMappings[Key.OemPlus] = vm => vm.OperatorCommand.Execute("+");
            _keyMappings[Key.Subtract] = vm => vm.OperatorCommand.Execute("-");
            _keyMappings[Key.OemMinus] = vm => vm.OperatorCommand.Execute("-");
            _keyMappings[Key.Multiply] = vm => vm.OperatorCommand.Execute("*");
            _keyMappings[Key.Divide] = vm => vm.OperatorCommand.Execute("/");
            _keyMappings[Key.Oem2] = vm => vm.OperatorCommand.Execute("/"); // Question mark / slash

            // Others
            _keyMappings[Key.Decimal] = vm => vm.DecimalCommand.Execute(null);
            _keyMappings[Key.OemPeriod] = vm => vm.DecimalCommand.Execute(null);
            _keyMappings[Key.Return] = vm => vm.EqualsCommand.Execute(null);
            _keyMappings[Key.Enter] = vm => vm.EqualsCommand.Execute(null);
            _keyMappings[Key.Back] = vm => vm.DeleteCommand.Execute(null);
            _keyMappings[Key.Escape] = vm => vm.ClearCommand.Execute(null);
            _keyMappings[Key.Oem5] = vm => vm.PercentCommand.Execute(null); // Backslash / Pipe often used for percent in some layouts or just mapped
        }

        /// <summary>
        /// A billentyűlenyomások kezelése.
        /// </summary>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e == null) return;

            try
            {
                if (DataContext is CalculatorViewModel viewModel)
                {
                    var key = e.Key;
                    var modifiers = Keyboard.Modifiers;

                    // Handle special combinations first
                    if (modifiers == ModifierKeys.Control)
                    {
                        if (key == Key.C)
                        {
                            viewModel.ClearCommand.Execute(null);
                            e.Handled = true;
                            return;
                        }
                        if (key == Key.M)
                        {
                            viewModel.MemoryClearCommand.Execute(null);
                            e.Handled = true;
                            return;
                        }
                    }

                    if (modifiers == ModifierKeys.None || modifiers == ModifierKeys.Shift) // Shift often used for symbols
                    {
                        if (_keyMappings.TryGetValue(key, out var action))
                        {
                            // Special check for OemPlus (Shift+= is +) vs (= is usually unshifted for equals, but here we treat OemPlus as +)
                            // Let's stick to the original logic's intent but cleaner.
                            // Original: Key.Add || (Key.OemPlus && NoModifiers) -> +

                            // Refined check for OemPlus to match original logic strictly if needed, 
                            // but usually OemPlus is + or =. 
                            // The original code: if (key == Key.Add || (key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.None))

                            if (key == Key.OemPlus && modifiers != ModifierKeys.None)
                            {
                                // If shift is pressed on OemPlus, it might be + on some layouts, or just + on others.
                                // Original logic only allowed OemPlus with NO modifiers for +. 
                                // Wait, standard US layout: = is unshifted, + is shifted.
                                // Original code: (key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.None) -> Execute("+")
                                // This seems backwards for US layout (+ is shift+=), but maybe it's for numpad +? No, Key.Add is numpad.
                                // Let's assume the user wants the original behavior.

                                // Actually, let's just use the map.
                            }

                            // Execute mapped action
                            action(viewModel);
                            e.Handled = true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in keyboard handler");
            }
        }

        /// <summary>
        /// Biztosítja, hogy a gombkattintás animáció gyorsítótárazva legyen.
        /// </summary>
        /// <param name="scaleTransform">A skálázási transzformáció.</param>
        /// <returns>A storyboard.</returns>
        private Storyboard EnsureCachedButtonClickStoryboard(ScaleTransform scaleTransform)
        {
            if (_cachedButtonClickStoryboard != null) return _cachedButtonClickStoryboard;

            var storyboard = new Storyboard();
            // Smoother easing: QuinticEase for premium feel
            var easing = new QuinticEase { EasingMode = EasingMode.EaseInOut };

            var scaleXDown = new DoubleAnimation(1.0, 0.90, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing };
            var scaleYDown = new DoubleAnimation(1.0, 0.90, TimeSpan.FromMilliseconds(200)) { EasingFunction = easing };
            var scaleXUp = new DoubleAnimation(0.90, 1.0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(200), EasingFunction = easing };
            var scaleYUp = new DoubleAnimation(0.90, 1.0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(200), EasingFunction = easing };

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

        /// <summary>
        /// Elhalványítja az ablakot.
        /// </summary>
        private async Task FadeOutWindow()
        {
            if (!_animationsEnabled || _isTurbo) return;
            // Smoother fade out
            await FadeOpacity(1.0, 0.0, TimeSpan.FromMilliseconds(400), new QuinticEase { EasingMode = EasingMode.EaseOut });
        }

        /// <summary>
        /// Beúsztatja az ablakot.
        /// </summary>
        private async Task FadeInWindow()
        {
            if (_isTurbo) return;
            // Smoother fade in
            await FadeOpacity(0.0, 1.0, TimeSpan.FromMilliseconds(400), new QuinticEase { EasingMode = EasingMode.EaseIn });
        }

        /// <summary>
        /// Elhalványítja az ablakot egy adott átlátszóságra.
        /// </summary>
        /// <param name="from">A kiinduló átlátszóság.</param>
        /// <param name="to">A cél átlátszóság.</param>
        /// <param name="duration">Az animáció időtartama.</param>
        /// <param name="easing">A gyorsítási függvény.</param>
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

        /// <summary>
        /// Létrehoz egy téma erőforrás-szótárat.
        /// </summary>
        /// <param name="relativePath">A relatív elérési út.</param>
        /// <returns>Az erőforrás-szótár.</returns>
        private static ResourceDictionary CreateThemeDictionary(string relativePath)
        {
            try
            {
                return new ResourceDictionary { Source = new Uri(relativePath, UriKind.Relative) };
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to load theme dictionary '{RelativePath}'", relativePath);
                return new ResourceDictionary();
            }
        }

        /// <summary>
        /// Megkeresi a téma erőforrás-szótár indexét.
        /// </summary>
        /// <param name="dictionaries">Az erőforrás-szótárak listája.</param>
        /// <returns>Az index, vagy -1, ha nem található.</returns>
        private static int FindThemeDictionaryIndex(System.Collections.ObjectModel.Collection<ResourceDictionary> dictionaries)
        {
            for (var i = 0; i < dictionaries.Count; i++)
            {
                var source = dictionaries[i].Source?.OriginalString;
                if (string.Equals(source, ExperimentalDarkThemePath, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
