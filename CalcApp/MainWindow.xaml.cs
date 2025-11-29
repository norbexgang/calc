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
        private bool _isTurbo = false;
        private Storyboard? _cachedButtonClickStoryboard;
        private SpeechControl? _speech;
        private bool _speechEnabled = true;

        // Shadow resources cached to avoid reallocation
        private readonly DropShadowEffect _defaultWindowShadow = new() { Color = Colors.Black, Opacity = 0.35, BlurRadius = 8, ShadowDepth = 3, Direction = 270, RenderingBias = RenderingBias.Performance };
        private readonly DropShadowEffect _defaultButtonShadow = new() { Color = Color.FromRgb(209, 196, 233), Opacity = 0.4, BlurRadius = 12, ShadowDepth = 4, Direction = 270, RenderingBias = RenderingBias.Performance };
        private readonly DropShadowEffect _defaultButtonHoverShadow = new() { Color = Color.FromRgb(209, 196, 233), Opacity = 0.6, BlurRadius = 16, ShadowDepth = 4, Direction = 270, RenderingBias = RenderingBias.Performance };

        public MainWindow()
        {
            LoadComponentFromXaml();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            UpdateShadowResources();
            FreezeResourceDictionaries();
            InitializeKeyMappings();
        }

        private async void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (FindName("TurboToggle") is ToggleButton turboBtn)
            {
                turboBtn.IsChecked = _isTurbo;
            }

            // Fire and forget speech init but on background thread
            await InitializeSpeechAsync();
        }

        private async Task InitializeSpeechAsync()
        {
            try
            {
                var speechToggle = FindName("SpeechToggle") as ToggleButton;
                if (speechToggle != null)
                {
                    speechToggle.IsEnabled = false;
                    speechToggle.Content = "🎤 Betöltés...";
                }

                bool hasRecognizer = false;
                await Task.Run(() => { hasRecognizer = HasHungarianRecognizer(); });

                if (speechToggle != null)
                {
                    speechToggle.IsEnabled = true;
                    speechToggle.IsChecked = _speechEnabled && hasRecognizer;
                    speechToggle.Content = speechToggle.IsChecked == true ? "🎤 Beszéd: Be" : "🎤 Beszéd: Ki";
                }

                if (_speechEnabled && hasRecognizer)
                {
                    if (DataContext is CalculatorViewModel viewModel)
                    {
                        await Task.Run(() =>
                        {
                            try
                            {
                                // SpeechControl creation is relatively heavy, keep off UI thread
                                _speech = new SpeechControl(viewModel);
                            }
                            catch (Exception ex)
                            {
                                Log.Error(ex, "Failed to initialize SpeechControl in background");
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during async speech initialization");
            }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;
            try { _speech?.Dispose(); } catch { }
            _speech = null;
        }

        private void LoadComponentFromXaml()
        {
            try
            {
                var uri = new Uri("/CalcApp;component/MainWindow.xaml", UriKind.Relative);
                Application.LoadComponent(this, uri);
                DataContext = new CalculatorViewModel();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "CRITICAL: Failed to load main window XAML");
                Application.Current?.Shutdown();
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
                    MessageBox.Show("Nincs telepítve magyar beszédfelismerő.", "Beszédvezérlés", MessageBoxButton.OK, MessageBoxImage.Information);
                    if (FindName("SpeechToggle") is ToggleButton tb) { tb.IsChecked = false; tb.Content = "🎤 Beszéd: Ki"; }
                    _speechEnabled = false;
                    return;
                }

                try
                {
                    if (DataContext is CalculatorViewModel viewModel)
                    {
                        Task.Run(() => _speech = new SpeechControl(viewModel));
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to start speech control");
                }
            }
            else
            {
                var s = _speech;
                _speech = null;
                Task.Run(() => s?.Dispose());
            }
        }

        private static bool? _hasHungarianRecognizer;
        private static bool HasHungarianRecognizer()
        {
            if (_hasHungarianRecognizer.HasValue) return _hasHungarianRecognizer.Value;
            try
            {
                var culture = new System.Globalization.CultureInfo("hu-HU");
                _hasHungarianRecognizer = SpeechRecognitionEngine.InstalledRecognizers().Any(r => r.Culture.Equals(culture));
            }
            catch
            {
                _hasHungarianRecognizer = false;
            }
            return _hasHungarianRecognizer.Value;
        }

        private void FreezeResourceDictionaries()
        {
            foreach (var dict in Resources.MergedDictionaries)
            {
                foreach (var key in dict.Keys)
                {
                    if (dict[key] is System.Windows.Freezable f && f.CanFreeze) f.Freeze();
                }
            }
        }

        private void TurboToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
            {
                _isTurbo = tb.IsChecked == true;
                if (DataContext is CalculatorViewModel vm) vm.SetTurboMode(_isTurbo);
                UpdateShadowResources();
            }
        }

        private void UpdateShadowResources()
        {
            // Direct dictionary manipulation is faster than reloading XAML
            if (_isTurbo)
            {
                Resources["WindowShadowEffect"] = null;
                Resources["ButtonShadowEffect"] = null;
                Resources["ButtonHoverShadowEffect"] = null;
                Resources["NeonBorderEffect"] = null;
                Resources["NeonTextEffect"] = null;
            }
            else
            {
                Resources["WindowShadowEffect"] = _defaultWindowShadow;
                Resources["ButtonShadowEffect"] = _defaultButtonShadow;
                Resources["ButtonHoverShadowEffect"] = _defaultButtonHoverShadow;
                Resources["NeonBorderEffect"] = FindResource("NeonBorderEffectDefault"); // Needs to be in resource dict or handled
                Resources["NeonTextEffect"] = FindResource("NeonTextEffectDefault");
            }
        }

        private readonly Dictionary<Key, Action<CalculatorViewModel>> _keyMappings = [];

        private void InitializeKeyMappings()
        {
            void Map(Key k, Action<CalculatorViewModel> a) => _keyMappings[k] = a;

            for (var k = Key.D0; k <= Key.D9; k++) Map(k, vm => vm.DigitCommand.Execute(((char)('0' + (k - Key.D0))).ToString()));
            for (var k = Key.NumPad0; k <= Key.NumPad9; k++) Map(k, vm => vm.DigitCommand.Execute(((char)('0' + (k - Key.NumPad0))).ToString()));

            Map(Key.Add, vm => vm.OperatorCommand.Execute("+"));
            Map(Key.OemPlus, vm => vm.OperatorCommand.Execute("+"));
            Map(Key.Subtract, vm => vm.OperatorCommand.Execute("-"));
            Map(Key.OemMinus, vm => vm.OperatorCommand.Execute("-"));
            Map(Key.Multiply, vm => vm.OperatorCommand.Execute("*"));
            Map(Key.Divide, vm => vm.OperatorCommand.Execute("/"));
            Map(Key.Oem2, vm => vm.OperatorCommand.Execute("/"));
            Map(Key.Decimal, vm => vm.DecimalCommand.Execute(null));
            Map(Key.OemPeriod, vm => vm.DecimalCommand.Execute(null));
            Map(Key.Return, vm => vm.EqualsCommand.Execute(null));
            Map(Key.Enter, vm => vm.EqualsCommand.Execute(null));
            Map(Key.Back, vm => vm.DeleteCommand.Execute(null));
            Map(Key.Escape, vm => vm.ClearCommand.Execute(null));
            Map(Key.Oem5, vm => vm.PercentCommand.Execute(null));
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not CalculatorViewModel viewModel) return;

            var key = e.Key;
            var modifiers = Keyboard.Modifiers;

            if (modifiers == ModifierKeys.Control)
            {
                if (key == Key.C) { viewModel.ClearCommand.Execute(null); e.Handled = true; return; }
                if (key == Key.M) { viewModel.MemoryClearCommand.Execute(null); e.Handled = true; return; }
            }

            if ((modifiers == ModifierKeys.None || modifiers == ModifierKeys.Shift) && _keyMappings.TryGetValue(key, out var action))
            {
                action(viewModel);
                e.Handled = true;
            }
        }
    }
}