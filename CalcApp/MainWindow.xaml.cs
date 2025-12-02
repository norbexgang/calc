using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows;
using CalcApp.ViewModels;
using Serilog;

namespace CalcApp
{
    /// <summary>
    /// A MainWindow.xaml interakciós logikája.
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            LoadComponentFromXaml();
            FreezeResourceDictionaries();
            InitializeKeyMappings();
        }

        private void LoadComponentFromXaml()
        {
            try
            {
                var uri = new Uri("/CalcApp;component/MainWindow.xaml", UriKind.Relative);
                Application.LoadComponent(this, uri);
                DataContext ??= new CalculatorViewModel();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "CRITICAL: Failed to load main window XAML");
                Application.Current?.Shutdown();
            }
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

        private readonly Dictionary<Key, Action<CalculatorViewModel>> _keyMappings = [];

        private void InitializeKeyMappings()
        {
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
