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


        // Shadow resources cached to avoid reallocation
        private readonly DropShadowEffect _defaultWindowShadow = new() { Color = Colors.Black, Opacity = 0.35, BlurRadius = 8, ShadowDepth = 3, Direction = 270, RenderingBias = RenderingBias.Performance };
        private readonly DropShadowEffect _defaultButtonShadow = new() { Color = Color.FromRgb(209, 196, 233), Opacity = 0.4, BlurRadius = 12, ShadowDepth = 4, Direction = 270, RenderingBias = RenderingBias.Performance };
        private readonly DropShadowEffect _defaultButtonHoverShadow = new() { Color = Color.FromRgb(209, 196, 233), Opacity = 0.6, BlurRadius = 16, ShadowDepth = 4, Direction = 270, RenderingBias = RenderingBias.Performance };
        private readonly DropShadowEffect? _neonBorderEffectDefault;
        private readonly DropShadowEffect? _neonTextEffectDefault;

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
        private readonly Dictionary<Key, Action<CalculatorViewModel>> _keyMappings = [];

        private void InitializeKeyMappings()
        {
            // Inline helper with AggressiveInlining for best performance
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            void Map(Key k, Action<CalculatorViewModel> a) => _keyMappings[k] = a;

            for (var k = Key.D0; k <= Key.D9; k++) Map(k, vm => vm.DigitCommand.Execute(((char)('0' + (k - Key.D0))).ToString()));
            for (var k = Key.NumPad0; k <= Key.NumPad9; k++) Map(k, vm => vm.DigitCommand.Execute(((char)('0' + (k - Key.NumPad0))).ToString()));

            Map(Key.Add, vm => vm.OperatorCommand.Execute("+"));
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
