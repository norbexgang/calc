using System;
using System.Globalization;
using System.Windows.Forms;

namespace GraphCalc;

public partial class MainForm : Form
{
    private const char DisplayDecimalSeparator = ',';

    private double? _pendingValue;
    private string? _pendingOperator;
    private bool _shouldResetDisplay;

    // Magyar komment: az ablak inicializálása és a kijelző alaphelyzetbe állítása
    public MainForm()
    {
        InitializeComponent();
        UpdateDisplay("0");
    }

    private void OnDigitClick(object? sender, EventArgs e)
    {
        // Magyar komment: a gombból kiolvassuk a számjegyet és hozzáadjuk a kijelzőhöz
        if (sender is not Button button)
        {
            return;
        }

        if (_shouldResetDisplay || DisplayTextBox.Text == "0")
        {
            UpdateDisplay(button.Text);
            _shouldResetDisplay = false;
        }
        else
        {
            UpdateDisplay(DisplayTextBox.Text + button.Text);
        }
    }

    private void OnDecimalClick(object? sender, EventArgs e)
    {
        // Magyar komment: a tizedespont beszúrását csak egyszer engedjük
        if (sender is not Button button)
        {
            return;
        }

        var separator = button.Tag as string ?? button.Text;

        if (_shouldResetDisplay)
        {
            UpdateDisplay("0" + separator);
            _shouldResetDisplay = false;
            return;
        }

        if (!DisplayTextBox.Text.Contains(DisplayDecimalSeparator))
        {
            UpdateDisplay(DisplayTextBox.Text + separator);
        }
    }

    private void OnOperatorClick(object? sender, EventArgs e)
    {
        // Magyar komment: művelet kiválasztásakor eltároljuk az előző értéket és az operátort
        if (sender is not Button button)
        {
            return;
        }

        if (TryGetDisplayValue(out var current))
        {
            if (_pendingValue.HasValue && _pendingOperator is not null && !_shouldResetDisplay)
            {
                current = Evaluate(_pendingValue.Value, current, _pendingOperator);
                UpdateDisplayFromDouble(current);
            }

            _pendingValue = current;
            _pendingOperator = button.Tag as string ?? button.Text;
            _shouldResetDisplay = true;
        }
    }

    private void OnEqualsClick(object? sender, EventArgs e)
    {
        // Magyar komment: az egyenlőség gomb kiértékeli az aktuális kifejezést
        if (_pendingValue.HasValue && _pendingOperator is not null &&
            TryGetDisplayValue(out var current))
        {
            var result = Evaluate(_pendingValue.Value, current, _pendingOperator);
            UpdateDisplayFromDouble(result);
            _pendingValue = null;
            _pendingOperator = null;
            _shouldResetDisplay = true;
        }
    }

    private void OnClearEntryClick(object? sender, EventArgs e)
    {
        // Magyar komment: csak az aktuális bevitelt töröljük
        UpdateDisplay("0");
        _shouldResetDisplay = true;
    }

    private void OnClearAllClick(object? sender, EventArgs e)
    {
        // Magyar komment: teljes kalkulátor állapotot visszaállítjuk
        UpdateDisplay("0");
        _pendingValue = null;
        _pendingOperator = null;
        _shouldResetDisplay = false;
    }

    private void OnBackspaceClick(object? sender, EventArgs e)
    {
        // Magyar komment: egy karakterrel visszalépünk a beviteli mezőben
        if (_shouldResetDisplay)
        {
            UpdateDisplay("0");
            _shouldResetDisplay = false;
            return;
        }

        var text = DisplayTextBox.Text;
        if (text.Length > 1)
        {
            UpdateDisplay(text[..^1]);
        }
        else
        {
            UpdateDisplay("0");
        }
    }

    private void OnToggleSignClick(object? sender, EventArgs e)
    {
        // Magyar komment: előjelet váltunk a kijelzett számon
        if (DisplayTextBox.Text is "0" or "0." or "")
        {
            return;
        }

        if (DisplayTextBox.Text.StartsWith("-", StringComparison.Ordinal))
        {
            UpdateDisplay(DisplayTextBox.Text[1..]);
        }
        else
        {
            UpdateDisplay("-" + DisplayTextBox.Text);
        }
    }

    private static double Evaluate(double left, double right, string op) => op switch
    {
        "+" => left + right,
        "-" => left - right,
        "×" => left * right,
        "*" => left * right,
        "÷" => right == 0 ? double.NaN : left / right,
        "/" => right == 0 ? double.NaN : left / right,
        _ => right
    };

    private bool TryGetDisplayValue(out double value)
    {
        // Magyar komment: a kijelző szövegét számmá alakítjuk a számításokhoz
        var sanitized = DisplayTextBox.Text.Replace(DisplayDecimalSeparator, '.');
        return double.TryParse(sanitized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void UpdateDisplayFromDouble(double value)
    {
        // Magyar komment: a lebegőpontos eredményt rendezett formátumban jelenítjük meg
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            UpdateDisplay(value.ToString(CultureInfo.InvariantCulture));
            return;
        }

        var formatted = value.ToString("G15", CultureInfo.InvariantCulture);
        formatted = formatted.TrimEnd('0').TrimEnd('.');
        if (string.IsNullOrEmpty(formatted))
        {
            formatted = "0";
        }

        UpdateDisplay(formatted);
    }

    private void UpdateDisplay(string value)
    {
        // Magyar komment: a kijelzőt frissítjük, és a pontot vesszőre cseréljük
        DisplayTextBox.Text = value.Replace('.', DisplayDecimalSeparator);
    }
}
