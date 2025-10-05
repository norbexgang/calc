using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CalcApp;

public partial class MainWindow : Window
{
    private double? _leftOperand;
    private string? _pendingOperator;
    private bool _shouldResetDisplay;
    private double _memory;
    private bool _useMaterialYou;

    private readonly CultureInfo _culture = CultureInfo.InvariantCulture;

    public MainWindow()
    {
        InitializeComponent();
        ApplyTheme();
    }

    private void Digit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var digit = button.Content?.ToString() ?? string.Empty;

        if (_shouldResetDisplay || Display.Text == "0" || Display.Text == "Error")
        {
            Display.Text = digit;
        }
        else
        {
            Display.Text += digit;
        }

        _shouldResetDisplay = false;
    }

    private void Decimal_Click(object sender, RoutedEventArgs e)
    {
        if (_shouldResetDisplay || Display.Text == "Error")
        {
            Display.Text = "0.";
            _shouldResetDisplay = false;
            return;
        }

        if (!Display.Text.Contains('.', StringComparison.Ordinal))
        {
            Display.Text += ".";
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
    }

    private void Percent_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDisplayValue(out var value))
        {
            return;
        }

        value /= 100;
        SetDisplayValue(value);
        _shouldResetDisplay = true;
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_shouldResetDisplay || Display.Text == "Error")
        {
            Display.Text = "0";
            _shouldResetDisplay = false;
            return;
        }

        if (Display.Text.Length <= 1)
        {
            Display.Text = "0";
            return;
        }

        Display.Text = Display.Text[..^1];
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
            _leftOperand = Evaluate(_leftOperand.Value, currentValue, _pendingOperator);
            SetDisplayValue(_leftOperand.Value);
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
            var result = Evaluate(_leftOperand.Value, rightOperand, _pendingOperator);
            SetDisplayValue(result);
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
        ApplyUnaryFunction(Math.Sin, degrees: true);
    }

    private void Cos_Click(object sender, RoutedEventArgs e)
    {
        ApplyUnaryFunction(Math.Cos, degrees: true);
    }

    private void Tan_Click(object sender, RoutedEventArgs e)
    {
        ApplyUnaryFunction(Math.Tan, degrees: true, validateTan: true);
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

        SetDisplayValue(Math.Sqrt(value));
        _shouldResetDisplay = true;
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
            var result = Factorial((int)Math.Round(value));
            SetDisplayValue(result);
            _shouldResetDisplay = true;
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

        _memory += value;
        UpdateMemoryDisplay();
        _shouldResetDisplay = true;
    }

    private void MemorySubtract_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetDisplayValue(out var value))
        {
            return;
        }

        _memory -= value;
        UpdateMemoryDisplay();
        _shouldResetDisplay = true;
    }

    private void MemoryRecall_Click(object sender, RoutedEventArgs e)
    {
        SetDisplayValue(_memory);
        _shouldResetDisplay = true;
    }

    private void MemoryClear_Click(object sender, RoutedEventArgs e)
    {
        _memory = 0;
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

    private void ApplyUnaryFunction(Func<double, double> func, bool degrees = false, bool validateTan = false)
    {
        if (!TryGetDisplayValue(out var value))
        {
            return;
        }

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
            Resources["WindowBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(18, 18, 18));
            Resources["BorderBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(31, 31, 31));
            Resources["BorderForegroundBrush"] = new SolidColorBrush(Color.FromRgb(242, 242, 242));
            Resources["ButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(42, 42, 42));
            Resources["ButtonForegroundBrush"] = new SolidColorBrush(Color.FromRgb(242, 242, 242));
            Resources["AccentButtonBrush"] = new SolidColorBrush(Color.FromRgb(61, 125, 255));
        }

        MaterialThemeToggle.Content = _useMaterialYou ? "Material You nézet" : "Klasszikus nézet";
    }

    private void ResetCalculatorState()
    {
        Display.Text = "0";
        _leftOperand = null;
        _pendingOperator = null;
        _shouldResetDisplay = false;
    }

    private bool TryGetDisplayValue(out double value)
    {
        if (Display.Text == "Error")
        {
            value = 0;
            return false;
        }

        return double.TryParse(Display.Text, NumberStyles.Float, _culture, out value);
    }

    private void SetDisplayValue(double value)
    {
        var formatted = value.ToString("G12", _culture);
        if (formatted.Contains('E', StringComparison.Ordinal))
        {
            Display.Text = formatted;
            return;
        }

        formatted = formatted.TrimEnd('0').TrimEnd('.');
        if (formatted == "-0")
        {
            formatted = "0";
        }

        Display.Text = string.IsNullOrEmpty(formatted) ? "0" : formatted;
    }

    private void ShowError()
    {
        Display.Text = "Error";
        _leftOperand = null;
        _pendingOperator = null;
        _shouldResetDisplay = true;
    }

    private void UpdateMemoryDisplay()
    {
        if (Math.Abs(_memory) < double.Epsilon)
        {
            MemoryText.Text = string.Empty;
            return;
        }

        MemoryText.Text = $"Memory: {_memory.ToString("G12", _culture)}";
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
