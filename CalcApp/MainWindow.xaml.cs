using System;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CalcApp
{
    public partial class MainWindow : Window
    {
        private double? _leftOperand;
        private string? _pendingOperator;
        private bool _shouldResetDisplay;
        private double _memoryValue;
        private readonly StringBuilder _memoryHistoryBuilder = new();
        private string? _lastOperationDescription;

        private TextBox? _display;
        private ListBox? _memoryList;
        private Button? _themeToggle;
        private bool _isDarkMode = true; // Start with dark mode

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

        public MainWindow()
        {
            LoadComponentFromXaml();
            InitializeMemory();
            InitializeTheme();
        }

        private void LoadComponentFromXaml()
        {
            // Manually load the XAML to work around designer not seeing InitializeComponent.
            var uri = new Uri("/CalcApp;component/MainWindow.xaml", UriKind.Relative);
            Application.LoadComponent(this, uri);
        }

        private TextBox DisplayBox => _display ??= FindRequiredControl<TextBox>("Display");

        private ListBox MemoryListBox => _memoryList ??= FindRequiredControl<ListBox>("MemoryList");

        private Button ThemeToggleButton => _themeToggle ??= FindRequiredControl<Button>("ThemeToggle");

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
            if (sender is not Button button) return;

            var digit = button.Content?.ToString() ?? string.Empty;
            _lastOperationDescription = null;

            var currentText = DisplayBox.Text;
            if (_shouldResetDisplay || currentText is "0" or "Error")
            {
                DisplayBox.Text = digit;
            }
            else
            {
                DisplayBox.Text = currentText + digit;
            }

            _shouldResetDisplay = false;
        }

        private void Decimal_Click(object sender, RoutedEventArgs e)
        {
            var currentText = DisplayBox.Text;
            
            if (_shouldResetDisplay || currentText == "Error")
            {
                DisplayBox.Text = "0.";
                _shouldResetDisplay = false;
                _lastOperationDescription = null;
                return;
            }

            if (!currentText.Contains('.', StringComparison.Ordinal))
            {
                DisplayBox.Text = currentText + ".";
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
            var currentText = DisplayBox.Text;
            
            if (_shouldResetDisplay || currentText == "Error")
            {
                DisplayBox.Text = "0";
                _shouldResetDisplay = false;
                _lastOperationDescription = null;
                return;
            }

            DisplayBox.Text = currentText.Length <= 1 ? "0" : currentText[..^1];
            _lastOperationDescription = null;
        }

        private void Operator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            var operatorSymbol = button.Tag?.ToString() ?? button.Content?.ToString();
            if (string.IsNullOrWhiteSpace(operatorSymbol)) return;

            if (!TryGetDisplayValue(out var currentValue)) return;

            if (_leftOperand.HasValue && _pendingOperator is not null && !_shouldResetDisplay)
            {
                var leftOperand = _leftOperand.Value;
                var pendingOp = _pendingOperator;
                var result = Evaluate(leftOperand, currentValue, pendingOp);
                _leftOperand = result;
                SetDisplayValue(result);
                RecordOperation($"{FormatNumber(leftOperand)}{pendingOp}{FormatNumber(currentValue)}", result);
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
            if (!_leftOperand.HasValue || _pendingOperator is null) return;
            if (!TryGetDisplayValue(out var rightOperand)) return;

            try
            {
                var leftOperand = _leftOperand.Value;
                var pendingOperator = _pendingOperator!;
                var result = Evaluate(leftOperand, rightOperand, pendingOperator);
                SetDisplayValue(result);
                RecordOperation($"{FormatNumber(leftOperand)}{pendingOperator}{FormatNumber(rightOperand)}", result);
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
            if (!TryGetDisplayValue(out var value)) return;

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
            if (!TryGetDisplayValue(out var value)) return;

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
            if (!TryGetDisplayValue(out var value)) return;

            _memoryValue += value;
            TrackMemoryOperation(value, isAddition: true);
            _shouldResetDisplay = true;
        }

        private void MemorySubtract_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value)) return;

            _memoryValue -= value;
            TrackMemoryOperation(value, isAddition: false);
            _shouldResetDisplay = true;
        }

        private void MemoryRecall_Click(object sender, RoutedEventArgs e)
        {
            SetDisplayValue(_memoryValue);
            _shouldResetDisplay = true;
            _lastOperationDescription = null;
        }

        private void MemoryClear_Click(object sender, RoutedEventArgs e)
        {
            _memoryValue = 0;
            _memoryHistoryBuilder.Clear();
            UpdateMemoryDisplay();
        }

        private void ApplyUnaryFunction(Func<double, double> func, string operationName, bool degrees = false, bool validateTan = false)
        {
            if (!TryGetDisplayValue(out var value)) return;

            var originalValue = value;

            if (degrees)
            {
                value *= Math.PI / 180.0;
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

            return double.TryParse(DisplayBox.Text, NumberStyles.Float, Culture, out value);
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
            UpdateMemoryDisplay();
        }

        private void UpdateMemoryDisplay()
        {
            var items = MemoryListBox.Items;
            items.Clear();
            
            var value = FormatNumber(_memoryValue);
            var displayText = _memoryHistoryBuilder.Length == 0 
                ? $"Memória: {value}"
                : $"Memória: {_memoryHistoryBuilder} (összesen: {value})";
                
            items.Add(displayText);
        }

        private void TrackMemoryOperation(double value, bool isAddition)
        {
            var description = _lastOperationDescription ?? FormatNumber(value);
            var builder = _memoryHistoryBuilder;

            if (builder.Length == 0)
            {
                if (!isAddition) builder.Append("- ");
                builder.Append(description);
            }
            else
            {
                builder.Append("; ");
                builder.Append(isAddition ? "+ " : "- ");
                builder.Append(description);
            }

            UpdateMemoryDisplay();
        }

        private void RecordOperation(string description, double result)
        {
            var formattedResult = FormatNumber(result);
            _lastOperationDescription = $"{description}={formattedResult}";
        }

        private static string FormatNumber(double value)
        {
            var formatted = value.ToString("G12", Culture);
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

        private void InitializeTheme()
        {
            UpdateThemeToggleButton();
        }

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDarkMode = !_isDarkMode;
            ApplyTheme();
            UpdateThemeToggleButton();
        }

        private void ApplyTheme()
        {
            var resources = Resources.MergedDictionaries;
            resources.Clear();

            var themeUri = _isDarkMode 
                ? new Uri("Themes/MaterialTheme.xaml", UriKind.Relative)
                : new Uri("Themes/ClassicTheme.xaml", UriKind.Relative);

            var themeDict = new ResourceDictionary { Source = themeUri };
            resources.Add(themeDict);
        }

        private void UpdateThemeToggleButton()
        {
            var button = ThemeToggleButton;
            if (_isDarkMode)
            {
                button.Content = "☀️ Light";
            }
            else
            {
                button.Content = "🌙 Dark";
            }
        }
    }
}
