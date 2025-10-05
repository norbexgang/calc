using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;

namespace CalcApp;

public partial class MainWindow : Window
{
    private double? _leftOperand;
    private string? _pendingOperator;
    private bool _shouldResetDisplay;

    private readonly string _decimalSeparator = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Digit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var digit = button.Content?.ToString() ?? string.Empty;

        if (_shouldResetDisplay || Display.Text == "0")
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
        if (_shouldResetDisplay)
        {
            Display.Text = "0" + _decimalSeparator;
            _shouldResetDisplay = false;
            return;
        }

        if (!Display.Text.Contains(_decimalSeparator, StringComparison.Ordinal))
        {
            Display.Text += _decimalSeparator;
        }
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        Display.Text = "0";
        _leftOperand = null;
        _pendingOperator = null;
        _shouldResetDisplay = false;
    }

    private void Sign_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(Display.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            value *= -1;
            Display.Text = value.ToString(CultureInfo.CurrentCulture);
        }
    }

    private void Percent_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(Display.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value))
        {
            value /= 100;
            Display.Text = value.ToString(CultureInfo.CurrentCulture);
        }
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

        if (double.TryParse(Display.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var currentValue))
        {
            if (_leftOperand.HasValue && _pendingOperator is not null && !_shouldResetDisplay)
            {
                _leftOperand = Evaluate(_leftOperand.Value, currentValue, _pendingOperator);
                Display.Text = _leftOperand?.ToString(CultureInfo.CurrentCulture) ?? "0";
            }
            else
            {
                _leftOperand = currentValue;
            }

            _pendingOperator = operatorSymbol;
            _shouldResetDisplay = true;
        }
    }

    private void Equals_Click(object sender, RoutedEventArgs e)
    {
        if (_leftOperand.HasValue && _pendingOperator is not null && double.TryParse(Display.Text, NumberStyles.Float, CultureInfo.CurrentCulture, out var rightOperand))
        {
            try
            {
                var result = Evaluate(_leftOperand.Value, rightOperand, _pendingOperator);
                Display.Text = result.ToString(CultureInfo.CurrentCulture);
                _leftOperand = null;
                _pendingOperator = null;
                _shouldResetDisplay = true;
            }
            catch (DivideByZeroException)
            {
                Display.Text = "Hiba";
                _leftOperand = null;
                _pendingOperator = null;
                _shouldResetDisplay = true;
            }
        }
    }

    private static double Evaluate(double left, double right, string operatorSymbol)
    {
        return operatorSymbol switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" => right == 0 ? throw new DivideByZeroException() : left / right,
            _ => right
        };
    }
}
