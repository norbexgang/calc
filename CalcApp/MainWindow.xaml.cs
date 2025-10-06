using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace CalcApp
{
    public partial class MainWindow : Window
    {
        private double? _leftOperand;
        private string? _pendingOperator;
        private bool _shouldResetDisplay;
        private double _memoryValue;
        private readonly StringBuilder _memoryHistoryBuilder = new();
        private bool _useMaterialYou;
        private string? _lastOperationDescription;

        private ResourceDictionary? _currentThemeDictionary;

        private static readonly Uri ClassicThemeUri = new("/CalcApp;component/Themes/ClassicTheme.xaml", UriKind.Relative);
        private static readonly Uri MaterialThemeUri = new("/CalcApp;component/Themes/MaterialTheme.xaml", UriKind.Relative);
        private static ResourceDictionary? s_classicTheme;
        private static ResourceDictionary? s_materialTheme;

        private TextBox? _display;
        private ToggleButton? _materialThemeToggle;
        private TextBlock? _memoryText;
        private ListBox? _memoryList;

        private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

        public MainWindow()
        {
            LoadComponentFromXaml();
            InitializeThemeTracking();
            ApplyTheme();
            InitializeMemory();
        }

        private void LoadComponentFromXaml()
        {
            // Manually load the XAML to work around designer not seeing InitializeComponent.
            Application.LoadComponent(this, new Uri("/CalcApp;component/MainWindow.xaml", UriKind.Relative));
        }

        private void InitializeThemeTracking()
        {
            if (_currentThemeDictionary is not null)
            {
                return;
            }

            var existingTheme = Resources.MergedDictionaries.FirstOrDefault();
            if (existingTheme is not null)
            {
                _currentThemeDictionary = existingTheme;
                s_classicTheme ??= existingTheme;
            }
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

            _memoryValue += value;
            TrackMemoryOperation(value, true);
            _shouldResetDisplay = true;
        }

        private void MemorySubtract_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value))
            {
                return;
            }

            _memoryValue -= value;
            TrackMemoryOperation(value, false);
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
            var themeDictionary = GetThemeDictionary(_useMaterialYou);
            if (ReferenceEquals(_currentThemeDictionary, themeDictionary))
            {
                UpdateThemeToggleContent();
                return;
            }

            var mergedDictionaries = Resources.MergedDictionaries;
            if (_currentThemeDictionary is not null)
            {
                mergedDictionaries.Remove(_currentThemeDictionary);
            }

            if (!mergedDictionaries.Contains(themeDictionary))
            {
                mergedDictionaries.Add(themeDictionary);
            }

            _currentThemeDictionary = themeDictionary;
            UpdateThemeToggleContent();
        }

        private ResourceDictionary GetThemeDictionary(bool useMaterialYou)
        {
            return useMaterialYou
                ? s_materialTheme ??= LoadThemeDictionary(MaterialThemeUri)
                : s_classicTheme ??= LoadThemeDictionary(ClassicThemeUri);
        }

        private static ResourceDictionary LoadThemeDictionary(Uri source)
        {
            return (ResourceDictionary)Application.LoadComponent(source);
        }

        private void UpdateThemeToggleContent()
        {
            MaterialThemeToggleControl.Content = _useMaterialYou ? "Dark Mode" : "Klasszikus nézet";
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
            UpdateMemoryDisplay();
        }

        private void UpdateMemoryDisplay()
        {
            MemoryListBox.Items.Clear();
            var value = FormatNumber(_memoryValue);
            if (_memoryHistoryBuilder.Length == 0)
            {
                MemoryListBox.Items.Add($"Memória: {value}");
            }
            else
            {
                MemoryListBox.Items.Add($"Memória: {_memoryHistoryBuilder} (összesen: {value})");
            }

            UpdateMemoryText();
        }

        private void UpdateMemoryText()
        {
            if (Math.Abs(_memoryValue) < double.Epsilon)
            {
                MemoryTextBlock.Text = "Aktív memória: üres";
                return;
            }

            var formattedValue = FormatNumber(_memoryValue);
            MemoryTextBlock.Text = $"Aktív memória: {formattedValue}";
        }

        private void TrackMemoryOperation(double value, bool isAddition)
        {
            var description = _lastOperationDescription ?? FormatNumber(value);

            if (_memoryHistoryBuilder.Length == 0)
            {
                if (isAddition)
                {
                    _memoryHistoryBuilder.Append(description);
                }
                else
                {
                    _memoryHistoryBuilder.Append("- ");
                    _memoryHistoryBuilder.Append(description);
                }
            }
            else
            {
                _memoryHistoryBuilder.Append("; ");
                _memoryHistoryBuilder.Append(isAddition ? "+ " : "- ");
                _memoryHistoryBuilder.Append(description);
            }

            UpdateMemoryDisplay();
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
