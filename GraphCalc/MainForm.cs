using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace GraphCalc;

public partial class MainForm : Form
{
    private const char DisplayDecimalSeparator = ',';

    private const int HistoryCapacity = 10;

    private double? _pendingValue;
    private string? _pendingOperator;
    private bool _shouldResetDisplay;
    private bool _isDarkMode;

    private sealed record MemoryEntry(string Operation, string Result);

    private readonly List<MemoryEntry> _memoryEntries = new();
    private int _memoryDisplayIndex = -1;

    // Magyar komment: az ablak inicializálása és a kijelző alaphelyzetbe állítása
    public MainForm()
    {
        InitializeComponent();
        UpdateDisplay("0");
        ApplyTheme();
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
            var operationText = $"{FormatDouble(_pendingValue.Value)} {GetDisplayOperator(_pendingOperator)} {FormatDouble(current)} =";
            var resultText = FormatDouble(result);
            UpdateOperationLabel(operationText);
            AddToMemory(new MemoryEntry(operationText, resultText));
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
        UpdateOperationLabel(string.Empty);
    }

    private void OnClearAllClick(object? sender, EventArgs e)
    {
        // Magyar komment: teljes kalkulátor állapotot visszaállítjuk
        UpdateDisplay("0");
        _pendingValue = null;
        _pendingOperator = null;
        _shouldResetDisplay = false;
        UpdateOperationLabel(string.Empty);
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
    
    private void OnUnaryOperationClick(object? sender, EventArgs e)
    {
        // Magyar komment: egylépéses műveleteket (szinusz, koszinusz, négyzetgyök, faktoriális) számolunk
        if (sender is not Button button)
        {
            return;
        }

        if (!TryGetDisplayValue(out var current))
        {
            return;
        }

        var operation = button.Tag as string ?? button.Text;
        double result = operation switch
        {
            "sin" => Math.Sin(current),
            "cos" => Math.Cos(current),
            "tan" => Math.Tan(current),
            "sqrt" => current < 0 ? double.NaN : Math.Sqrt(current),
            "fact" => CalculateFactorial(current),
            _ => current
        };

        UpdateDisplayFromDouble(result);
        var operationText = operation switch
        {
            "sin" => $"sin({FormatDouble(current)}) =",
            "cos" => $"cos({FormatDouble(current)}) =",
            "tan" => $"tan({FormatDouble(current)}) =",
            "sqrt" => $"√({FormatDouble(current)}) =",
            "fact" => $"n!({FormatDouble(current)}) =",
            _ => string.Empty
        };

        UpdateOperationLabel(operationText);

        if (!string.IsNullOrEmpty(operationText))
        {
            AddToMemory(new MemoryEntry(operationText, FormatDouble(result)));
        }

        _shouldResetDisplay = true;
    }

    private void OnPercentClick(object? sender, EventArgs e)
    {
        if (!TryGetDisplayValue(out var current))
        {
            return;
        }

        double result;

        if (_pendingValue.HasValue && _pendingOperator is not null)
        {
            result = _pendingValue.Value * current / 100d;
            UpdateDisplayFromDouble(result);
            UpdateOperationLabel($"{FormatDouble(_pendingValue.Value)} {GetDisplayOperator(_pendingOperator)} {FormatDouble(result)}");
        }
        else
        {
            result = current / 100d;
            UpdateDisplayFromDouble(result);
            var operationText = $"{FormatDouble(current)}% =";
            UpdateOperationLabel(operationText);
            AddToMemory(new MemoryEntry(operationText, FormatDouble(result)));
        }

        _shouldResetDisplay = true;
    }

    private void OnMemoryRecallClick(object? sender, EventArgs e)
    {
        if (_memoryEntries.Count == 0)
        {
            return;
        }

        if (_memoryDisplayIndex < 0)
        {
            _memoryDisplayIndex = 0;
        }
        else if (_memoryDisplayIndex < _memoryEntries.Count - 1)
        {
            _memoryDisplayIndex++;
        }

        ShowMemoryEntry(_memoryDisplayIndex);
    }

    private void OnMemoryStoreClick(object? sender, EventArgs e)
    {
        if (!TryGetDisplayValue(out var value))
        {
            return;
        }

        var operationText = string.IsNullOrWhiteSpace(OperationLabel.Text)
            ? "Eredmény ="
            : OperationLabel.Text.Trim();
        var resultText = FormatDouble(value);
        AddToMemory(new MemoryEntry(operationText, resultText));
    }

    private void OnMemoryDeleteClick(object? sender, EventArgs e)
    {
        if (_memoryEntries.Count == 0)
        {
            return;
        }

        var wasViewingMemory = _memoryDisplayIndex >= 0;
        var index = wasViewingMemory && _memoryDisplayIndex < _memoryEntries.Count
            ? _memoryDisplayIndex
            : 0;

        _memoryEntries.RemoveAt(index);

        if (_memoryEntries.Count == 0)
        {
            _memoryDisplayIndex = -1;
            RefreshMemoryList();
            return;
        }

        if (!wasViewingMemory)
        {
            _memoryDisplayIndex = -1;
            RefreshMemoryList();
            return;
        }

        if (index >= _memoryEntries.Count)
        {
            index = _memoryEntries.Count - 1;
        }

        _memoryDisplayIndex = index;
        RefreshMemoryList();
        ShowMemoryEntry(_memoryDisplayIndex);
    }

    private void OnMemoryClearClick(object? sender, EventArgs e)
    {
        if (_memoryEntries.Count == 0)
        {
            return;
        }

        _memoryEntries.Clear();
        _memoryDisplayIndex = -1;
        RefreshMemoryList();
    }

    private void OnHistorySelectedIndexChanged(object? sender, EventArgs e)
    {
        var selectedIndex = HistoryListBox.SelectedIndex;

        if (selectedIndex >= 0 && selectedIndex < _memoryEntries.Count)
        {
            if (_memoryDisplayIndex != selectedIndex)
            {
                ShowMemoryEntry(selectedIndex);
            }

            return;
        }

        _memoryDisplayIndex = -1;
    }

    private static double CalculateFactorial(double value)
    {
        // Magyar komment: a faktoriálist csak nemnegatív egész számokra értelmezzük
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return double.NaN;
        }

        var rounded = Math.Round(value);
        if (rounded < 0 || Math.Abs(value - rounded) > 1e-9)
        {
            return double.NaN;
        }

        if (rounded > 170)
        {
            return double.PositiveInfinity;
        }

        var integer = (int)rounded;
        double result = 1d;
        for (var i = 2; i <= integer; i++)
        {
            result *= i;
            if (double.IsInfinity(result))
            {
                return double.PositiveInfinity;
            }
        }

        return result;
    }


    private void OnThemeToggleCheckedChanged(object? sender, EventArgs e)
    {
        _isDarkMode = ThemeToggleCheckBox.Checked;
        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var formBackColor = _isDarkMode ? Color.FromArgb(24, 24, 24) : Color.White;
        var panelBackColor = _isDarkMode ? Color.FromArgb(36, 36, 36) : Color.FromArgb(245, 245, 245);
        var displayBackColor = _isDarkMode ? Color.FromArgb(24, 24, 24) : Color.White;
        var displayForeColor = _isDarkMode ? Color.White : Color.Black;

        BackColor = formBackColor;
        LayoutPanel.BackColor = panelBackColor;
        DisplayTextBox.BackColor = displayBackColor;
        DisplayTextBox.ForeColor = displayForeColor;
        OperationLabel.ForeColor = _isDarkMode ? Color.Gainsboro : Color.DimGray;

        HistoryLabel.ForeColor = _isDarkMode ? Color.White : Color.Black;
        HistoryLabel.BackColor = Color.Transparent;
        HistoryListBox.BackColor = _isDarkMode ? Color.FromArgb(24, 24, 24) : Color.White;
        HistoryListBox.ForeColor = _isDarkMode ? Color.White : Color.Black;
        HistoryListBox.BorderStyle = BorderStyle.FixedSingle;

        ThemeToggleCheckBox.ForeColor = _isDarkMode ? Color.White : Color.Black;
        ThemeToggleCheckBox.BackColor = Color.Transparent;

        foreach (Control control in LayoutPanel.Controls)
        {
            if (control is Button button)
            {
                ApplyButtonTheme(button);
            }
        }
    }

    private void ApplyButtonTheme(Button button)
    {
        var generalBackColor = _isDarkMode ? Color.FromArgb(48, 48, 48) : Color.WhiteSmoke;
        var operatorBackColor = _isDarkMode ? Color.FromArgb(60, 80, 130) : Color.FromArgb(225, 230, 246);
        var clearBackColor = _isDarkMode ? Color.FromArgb(120, 90, 35) : Color.FromArgb(255, 236, 179);
        var functionBackColor = _isDarkMode ? Color.FromArgb(50, 90, 60) : Color.FromArgb(232, 245, 233);
        var equalsBackColor = _isDarkMode ? Color.FromArgb(46, 132, 50) : Color.FromArgb(76, 175, 80);
        var memoryBackColor = _isDarkMode ? Color.FromArgb(180, 40, 40) : Color.FromArgb(255, 138, 128);

        var background = generalBackColor;
        var foreColor = _isDarkMode ? Color.White : Color.Black;

        if (button.Text is "÷" or "×" or "-" or "+")
        {
            background = operatorBackColor;
        }
        else if (button.Text is "CE" or "C" or "⌫")
        {
            background = clearBackColor;
        }
        else if (button.Text is "sin" or "cos" or "tan" or "√" or "n!" or "%")
        {
            background = functionBackColor;
        }
        else if (button.Text == "=")
        {
            background = equalsBackColor;
            foreColor = Color.White;
        }

        if (button.Tag is string tag && tag.StartsWith("memory-", StringComparison.Ordinal))
        {
            background = memoryBackColor;
            foreColor = Color.White;
        }

        button.BackColor = background;
        button.ForeColor = foreColor;

        button.FlatAppearance.BorderColor = _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.LightGray;
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
        return double.TryParse((string?)sanitized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void UpdateDisplayFromDouble(double value)
    {
        // Magyar komment: a lebegőpontos eredményt rendezett formátumban jelenítjük meg
        UpdateDisplay(FormatDouble(value));
    }

    private void UpdateDisplay(string value)
    {
        // Magyar komment: a kijelzőt frissítjük, és a pontot vesszőre cseréljük
        DisplayTextBox.Text = value.Replace('.', DisplayDecimalSeparator);
    }

    private string FormatDouble(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        var formatted = value.ToString("G15", CultureInfo.InvariantCulture);
        formatted = formatted.TrimEnd('0').TrimEnd('.');
        if (string.IsNullOrEmpty(formatted))
        {
            formatted = "0";
        }

        return formatted.Replace('.', DisplayDecimalSeparator);
    }

    private static string GetDisplayOperator(string op) => op switch
    {
        "*" => "×",
        "/" => "÷",
        _ => op
    };

    private void UpdateOperationLabel(string text)
    {
        OperationLabel.Text = text;
    }

    private void AddToMemory(MemoryEntry entry)
    {
        _memoryEntries.Insert(0, entry);
        if (_memoryEntries.Count > HistoryCapacity)
        {
            _memoryEntries.RemoveAt(_memoryEntries.Count - 1);
        }

        _memoryDisplayIndex = -1;
        RefreshMemoryList();
    }

    private void ShowMemoryEntry(int index)
    {
        if (index < 0 || index >= _memoryEntries.Count)
        {
            return;
        }

        var entry = _memoryEntries[index];
        UpdateOperationLabel(entry.Operation);
        UpdateDisplay(entry.Result);
        _pendingValue = null;
        _pendingOperator = null;
        _shouldResetDisplay = true;

        _memoryDisplayIndex = index;
        HistoryListBox.SelectedIndex = index;
    }

    private void RefreshMemoryList()
    {
        HistoryListBox.BeginUpdate();
        HistoryListBox.Items.Clear();
        foreach (var entry in _memoryEntries)
        {
            HistoryListBox.Items.Add($"{entry.Operation} {entry.Result}".Trim());
        }
        HistoryListBox.EndUpdate();

        if (_memoryDisplayIndex >= 0 && _memoryDisplayIndex < _memoryEntries.Count)
        {
            HistoryListBox.SelectedIndex = _memoryDisplayIndex;
        }
        else
        {
            HistoryListBox.ClearSelected();
        }
    }
}
