using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace CalcApp
{
    public partial class MainWindow : Window
    {
        private double? _leftOperand;
        private string? _pendingOperator;
        private bool _shouldResetDisplay;
        private readonly double[] _memoryValues = new double[10];
        private readonly List<string>[] _memoryOperations = new List<string>[10];
        private bool _useMaterialYou;
        private string? _lastOperationDescription;

        private TextBox? _display;
        private ToggleButton? _materialThemeToggle;
        private TextBlock? _memoryText;
        private ListBox? _memoryList;

        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        public MainWindow()
        {
            LoadComponentFromXaml();
            ApplyTheme();
            InitializeMemoryOperations();
            InitializeMemory();
        }

        private void InitializeMemoryOperations()
        {
            for (var i = 0; i < _memoryOperations.Length; i++)
            {
                _memoryOperations[i] = new List<string>();
                _memoryValues[i] = 0;
            }
        }

        private void LoadComponentFromXaml()
        {
            // Manually load the XAML to work around designer not seeing InitializeComponent.
            Application.LoadComponent(this, new Uri("/CalcApp;component/MainWindow.xaml", UriKind.Relative));
        }

        private TextBox DisplayBox => _display ??= FindRequiredControl<TextBox>("Display");

        private ToggleButton MaterialThemeToggleControl =>
            _materialThemeToggle ??= FindRequiredControl<ToggleButton>("MaterialThemeToggle");

        private TextBlock MemoryTextBlock => _memoryText ??= FindRequiredControl<TextBlock>("MemoryText");

        private ListBox MemoryListBox => _memoryList ??= FindRequiredControl<ListBox>("MemoryList");

        private T FindRequiredControl<T>(string name) where T : class
        {
            if (FindName(name) is T control)
            {
                return control;
            }

            throw new InvalidOperationException($"Could not find control '{name}'.");
        }

        private void Digit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            var digit = button.Content?.ToString() ?? string.Empty;

            _lastOperationDescription = null;

            if (_shouldResetDisplay || DisplayBox.Text == "0" || DisplayBox.Text == "Error")
            {
                DisplayBox.Text = digit;
            }
            else
            {
                DisplayBox.Text += digit;
            }

            _shouldResetDisplay = false;
        }

        private void Decimal_Click(object sender, RoutedEventArgs e)
        {
            if (_shouldResetDisplay || DisplayBox.Text == "Error")
            {
                DisplayBox.Text = "0.";
                _shouldResetDisplay = false;
                _lastOperationDescription = null;
                return;
            }

            if (!DisplayBox.Text.Contains('.', StringComparison.Ordinal))
            {
                DisplayBox.Text += ".";
                _lastOperationDescription = null;
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ResetCalculatorState();
        }

        private void Sign_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value))
            {
                return;
            }

            value *= -1;
            SetDisplayValue(value);
            _lastOperationDescription = null;
        }

        private void Percent_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value))
            {
                return;
            }

            var originalValue = value;
            value /= 100;
            SetDisplayValue(value);
            _shouldResetDisplay = true;
            RecordOperation($"{FormatNumber(originalValue)}%", value);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (_shouldResetDisplay || DisplayBox.Text == "Error")
            {
                DisplayBox.Text = "0";
                _shouldResetDisplay = false;
                _lastOperationDescription = null;
                return;
            }

            if (DisplayBox.Text.Length <= 1)
            {
                DisplayBox.Text = "0";
                _lastOperationDescription = null;
                return;
            }

            DisplayBox.Text = DisplayBox.Text[..^1];
            _lastOperationDescription = null;
        }

        private void Operator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            var operatorSymbol = button.Tag?.ToString() ?? button.Content?.ToString();
            if (string.IsNullOrWhiteSpace(operatorSymbol))
            {
                return;
            }

            if (!TryGetDisplayValue(out var currentValue))
            {
                return;
            }

            if (_leftOperand.HasValue && _pendingOperator is not null && !_shouldResetDisplay)
            {
                var leftOperand = _leftOperand.Value;
                var operatorSymbol = _pendingOperator;
                var result = Evaluate(leftOperand, currentValue, operatorSymbol);
                _leftOperand = result;
                SetDisplayValue(result);
                RecordOperation($"{FormatNumber(leftOperand)}{operatorSymbol}{FormatNumber(currentValue)}", result);
            }
            else
            {
                _leftOperand = currentValue;
            }

            _pendingOperator = operatorSymbol;
            _shouldResetDisplay = true;
        }

        private void Equals_Click(object sender, RoutedEventArgs e)
        {
            if (!_leftOperand.HasValue || _pendingOperator is null)
            {
                return;
            }

            if (!TryGetDisplayValue(out var rightOperand))
            {
                return;
            }

            try
            {
                var leftOperand = _leftOperand.Value;
                var operatorSymbol = _pendingOperator;
                var result = Evaluate(leftOperand, rightOperand, operatorSymbol);
                SetDisplayValue(result);
                RecordOperation($"{FormatNumber(leftOperand)}{operatorSymbol}{FormatNumber(rightOperand)}", result);
                _leftOperand = null;
                _pendingOperator = null;
                _shouldResetDisplay = true;
            }
            catch (DivideByZeroException)
            {
                ShowError();
            }
            catch (InvalidOperationException)
            {
                ShowError();
            }
        }

        private void Sin_Click(object sender, RoutedEventArgs e)
        {
            ApplyUnaryFunction(Math.Sin, "sin", degrees: true);
        }

        private void Cos_Click(object sender, RoutedEventArgs e)
        {
            ApplyUnaryFunction(Math.Cos, "cos", degrees: true);
        }

        private void Tan_Click(object sender, RoutedEventArgs e)
        {
            ApplyUnaryFunction(Math.Tan, "tan", degrees: true, validateTan: true);
        }

        private void Sqrt_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value))
            {
                return;
            }

            if (value < 0)
            {
                ShowError();
                return;
            }

            var result = Math.Sqrt(value);
            SetDisplayValue(result);
            _shouldResetDisplay = true;
            RecordOperation($"√({FormatNumber(value)})", result);
        }

        private void Factorial_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value))
            {
                return;
            }

            if (value < 0 || Math.Abs(value - Math.Round(value)) > double.Epsilon)
            {
                ShowError();
                return;
            }

            try
            {
                var roundedValue = (int)Math.Round(value);
                var result = Factorial(roundedValue);
                SetDisplayValue(result);
                _shouldResetDisplay = true;
                RecordOperation($"{FormatNumber(roundedValue)}!", result);
            }
            catch (OverflowException)
            {
                ShowError();
            }
        }

        private void MemoryAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value))
            {
                return;
            }

            var index = SelectedMemoryIndex;
            _memoryValues[index] += value;
            TrackMemoryOperation(index, value, true);
            _shouldResetDisplay = true;
        }

        private void MemorySubtract_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value))
            {
                return;
            }

            var index = SelectedMemoryIndex;
            _memoryValues[index] -= value;
            TrackMemoryOperation(index, value, false);
            _shouldResetDisplay = true;
        }

        private void MemoryRecall_Click(object sender, RoutedEventArgs e)
        {
            var index = SelectedMemoryIndex;
            SetDisplayValue(_memoryValues[index]);
            _shouldResetDisplay = true;
            _lastOperationDescription = null;
        }

        private void MemoryClear_Click(object sender, RoutedEventArgs e)
        {
            var index = SelectedMemoryIndex;
            _memoryValues[index] = 0;
            _memoryOperations[index].Clear();
            UpdateMemoryDisplay(index);
        }

        private void MemoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MemoryListBox.SelectedIndex < 0)
            {
                return;
            }

            UpdateMemoryText(MemoryListBox.SelectedIndex);
        }

        private void MaterialThemeToggle_OnChecked(object sender, RoutedEventArgs e)
        {
            _useMaterialYou = true;
            ApplyTheme();
        }

        private void MaterialThemeToggle_OnUnchecked(object sender, RoutedEventArgs e)
        {
            _useMaterialYou = false;
            ApplyTheme();
        }

        private void ApplyUnaryFunction(Func<double, double> func, string operationName, bool degrees = false, bool validateTan = false)
        {
            if (!TryGetDisplayValue(out var value))
            {
                return;
            }

            var originalValue = value;

            if (degrees)
            {
                value = value * Math.PI / 180.0;
            }

            if (validateTan)
            {
                var cosValue = Math.Cos(value);
                if (Math.Abs(cosValue) < 1e-12)
                {
                    ShowError();
                    return;
                }
            }

            var result = func(value);
            SetDisplayValue(result);
            _shouldResetDisplay = true;
            RecordOperation($"{operationName}({FormatNumber(originalValue)})", result);
        }

        private static double Factorial(int value)
        {
            double result = 1;
            for (var i = 2; i <= value; i++)
            {
                result *= i;
                if (double.IsInfinity(result) || double.IsNaN(result))
                {
                    throw new OverflowException();
                }
            }

            return result;
        }

        private void ApplyTheme()
        {
            if (_useMaterialYou)
            {
                Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(18, 17, 23));
                Resources["BorderBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(33, 31, 42));
                Resources["BorderForegroundBrush"] = new SolidColorBrush(Color.FromRgb(238, 233, 255));
                Resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(55, 51, 64));
                Resources["ButtonForegroundBrush"] = new SolidColorBrush(Color.FromRgb(238, 233, 255));
                Resources["AccentButtonBrush"] = new SolidColorBrush(Color.FromRgb(154, 139, 255));
            }
            else
            {
                Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                Resources["BorderBackgroundBrush"] = new SolidColorBrush(Colors.White);
                Resources["BorderForegroundBrush"] = new SolidColorBrush(Color.FromRgb(31, 31, 31));
                Resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(224, 224, 224));
                Resources["ButtonForegroundBrush"] = new SolidColorBrush(Color.FromRgb(31, 31, 31));
                Resources["AccentButtonBrush"] = new SolidColorBrush(Color.FromRgb(127, 180, 255));
            }

            MaterialThemeToggleControl.Content = _useMaterialYou ? "Sötét mód" : "Klasszikus nézet";
        }

        private void ResetCalculatorState()
        {
            DisplayBox.Text = "0";
            _leftOperand = null;
            _pendingOperator = null;
            _shouldResetDisplay = false;
            _lastOperationDescription = null;
        }

        private bool TryGetDisplayValue(out double value)
        {
            if (DisplayBox.Text == "Error")
            {
                value = 0;
                return false;
            }

            return double.TryParse(DisplayBox.Text, NumberStyles.Float, _culture, out value);
        }

        private void SetDisplayValue(double value)
        {
            DisplayBox.Text = FormatNumber(value);
        }

        private void ShowError()
        {
            DisplayBox.Text = "Error";
            _leftOperand = null;
            _pendingOperator = null;
            _shouldResetDisplay = true;
            _lastOperationDescription = null;
        }

        private void InitializeMemory()
        {
            UpdateMemoryDisplay(0);
        }

        private int SelectedMemoryIndex =>
            MemoryListBox.SelectedIndex >= 0 ? MemoryListBox.SelectedIndex : 0;

        private void UpdateMemoryDisplay(int? selectedIndexOverride = null)
        {
            var selectedIndex = selectedIndexOverride ?? SelectedMemoryIndex;

            MemoryListBox.Items.Clear();
            for (var i = 0; i < _memoryValues.Length; i++)
            {
                var value = FormatNumber(_memoryValues[i]);
                var operations = _memoryOperations[i];
                if (operations.Count == 0)
                {
                    MemoryListBox.Items.Add($"M{i + 1}: {value}");
                    continue;
                }

                var operationsText = string.Join("; ", operations);
                MemoryListBox.Items.Add($"M{i + 1}: {operationsText} (összesen: {value})");
            }

            if (selectedIndex < 0 || selectedIndex >= _memoryValues.Length)
            {
                selectedIndex = 0;
            }

            MemoryListBox.SelectedIndex = selectedIndex;
            UpdateMemoryText(selectedIndex);
        }

        private void UpdateMemoryText(int selectedIndex)
        {
            if (selectedIndex < 0 || selectedIndex >= _memoryValues.Length)
            {
                MemoryTextBlock.Text = string.Empty;
                return;
            }

            var value = _memoryValues[selectedIndex];

            if (Math.Abs(value) < double.Epsilon)
            {
                MemoryTextBlock.Text = $"Aktív memória: M{selectedIndex + 1} üres";
                return;
            }

            var formattedValue = FormatNumber(value);
            MemoryTextBlock.Text = $"Aktív memória: M{selectedIndex + 1} = {formattedValue}";
        }

        private void TrackMemoryOperation(int index, double value, bool isAddition)
        {
            var description = _lastOperationDescription ?? FormatNumber(value);

            if (_memoryOperations[index].Count == 0 && isAddition)
            {
                _memoryOperations[index].Add(description);
            }
            else
            {
                var sign = isAddition ? "+" : "-";
                _memoryOperations[index].Add($"{sign} {description}");
            }

            UpdateMemoryDisplay(index);
        }

        private void RecordOperation(string description, double result)
        {
            var formattedResult = FormatNumber(result);
            _lastOperationDescription = $"{description}={formattedResult}";
        }

        private string FormatNumber(double value)
        {
            var formatted = value.ToString("G12", _culture);
            if (formatted.Contains('E', StringComparison.Ordinal))
            {
                return formatted;
            }

            formatted = formatted.TrimEnd('0').TrimEnd('.');
            if (formatted == "-0")
            {
                formatted = "0";
            }

            return string.IsNullOrEmpty(formatted) ? "0" : formatted;
        }

        private static double Evaluate(double left, double right, string operatorSymbol)
        {
            return operatorSymbol switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" => right == 0 ? throw new DivideByZeroException() : left / right,
                _ => throw new InvalidOperationException($"Unknown operator: {operatorSymbol}")
            };
        }
    }
}
