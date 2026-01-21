using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;
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


        // Shadow resources cached and frozen to avoid reallocation and enable GPU caching
        private static readonly DropShadowEffect _defaultWindowShadow;
        private static readonly DropShadowEffect _defaultButtonShadow;
        private static readonly DropShadowEffect _defaultButtonHoverShadow;
        private readonly DropShadowEffect? _neonBorderEffectDefault;
        private readonly DropShadowEffect? _neonTextEffectDefault;

        static MainWindow()
        {
            _defaultWindowShadow = new DropShadowEffect { Color = Colors.Black, Opacity = 0.35, BlurRadius = 8, ShadowDepth = 3, Direction = 270, RenderingBias = RenderingBias.Performance };
            _defaultWindowShadow.Freeze();
            _defaultButtonShadow = new DropShadowEffect { Color = Color.FromRgb(209, 196, 233), Opacity = 0.4, BlurRadius = 12, ShadowDepth = 4, Direction = 270, RenderingBias = RenderingBias.Performance };
            _defaultButtonShadow.Freeze();
            _defaultButtonHoverShadow = new DropShadowEffect { Color = Color.FromRgb(209, 196, 233), Opacity = 0.6, BlurRadius = 16, ShadowDepth = 4, Direction = 270, RenderingBias = RenderingBias.Performance };
            _defaultButtonHoverShadow.Freeze();
        }

        public MainWindow()
        {
            LoadComponentFromXaml();

            _neonBorderEffectDefault = TryFindResource("NeonBorderEffectDefault") as DropShadowEffect;
            _neonTextEffectDefault = TryFindResource("NeonTextEffectDefault") as DropShadowEffect;
            UpdateShadowResources();
            // FreezeResourceDictionaries(); // Removed: Resources are already frozen in XAML
            InitializeKeyMappings();
        }

        private void LoadComponentFromXaml()
        {
            try
            {
                // Direct InitializeComponent() is faster than LoadComponent with URI
                InitializeComponent();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "CRITICAL: Failed to load main window XAML");
                Application.Current?.Shutdown();
            }
        }

        private void TurboToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton tb)
            {
                var newTurboState = tb.IsChecked == true;
                if (_isTurbo == newTurboState) return;

                _isTurbo = newTurboState;
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
                if (_neonBorderEffectDefault != null) Resources["NeonBorderEffect"] = _neonBorderEffectDefault;
                if (_neonTextEffectDefault != null) Resources["NeonTextEffect"] = _neonTextEffectDefault;
            }
        }
        private static readonly Dictionary<Key, Action<CalculatorViewModel>> _keyMappings = new(30);

        private static void InitializeKeyMappings()
        {
            if (_keyMappings.Count > 0) return; // Already initialized

            // Pre-cached digit strings to avoid allocations
            var digits = new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" };

            for (var i = 0; i <= 9; i++)
            {
                var digit = digits[i];
                _keyMappings[Key.D0 + i] = vm => vm.DigitCommand.Execute(digit);
                _keyMappings[Key.NumPad0 + i] = vm => vm.DigitCommand.Execute(digit);
            }

            _keyMappings[Key.Add] = vm => vm.OperatorCommand.Execute("+");
            _keyMappings[Key.Subtract] = vm => vm.OperatorCommand.Execute("-");
            _keyMappings[Key.OemMinus] = vm => vm.OperatorCommand.Execute("-");
            _keyMappings[Key.Multiply] = vm => vm.OperatorCommand.Execute("*");
            _keyMappings[Key.Divide] = vm => vm.OperatorCommand.Execute("/");
            _keyMappings[Key.Oem2] = vm => vm.OperatorCommand.Execute("/");
            _keyMappings[Key.Decimal] = vm => vm.DecimalCommand.Execute(null);
            _keyMappings[Key.OemPeriod] = vm => vm.DecimalCommand.Execute(null);
            _keyMappings[Key.Return] = vm => vm.EqualsCommand.Execute(null);
            _keyMappings[Key.Enter] = vm => vm.EqualsCommand.Execute(null);
            _keyMappings[Key.Back] = vm => vm.DeleteCommand.Execute(null);
            _keyMappings[Key.Escape] = vm => vm.ClearCommand.Execute(null);
            _keyMappings[Key.Oem5] = vm => vm.PercentCommand.Execute(null);
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

            if (key == Key.OemPlus)
            {
                if (modifiers == ModifierKeys.Shift)
                {
                    viewModel.OperatorCommand.Execute("+");
                }
                else if (modifiers == ModifierKeys.None)
                {
                    viewModel.EqualsCommand.Execute(null);
                }
                e.Handled = true;
                return;
            }

            if ((modifiers == ModifierKeys.None || modifiers == ModifierKeys.Shift) && _keyMappings.TryGetValue(key, out var action))
            {
                action(viewModel);
                e.Handled = true;
            }
        }
    }
}
