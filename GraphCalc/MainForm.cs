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
    private bool HasPendingOperation => _pendingValue.HasValue && _pendingOperator is not null;

    private sealed record MemoryEntry(string Operation, string Result);
    private sealed record UnaryOperation(Func<double, double> Evaluate, Func<string, string>? OperationLabelFactory);

    private static readonly IReadOnlyDictionary<string, UnaryOperation> UnaryOperations = new Dictionary<string, UnaryOperation>(StringComparer.Ordinal)
    {
        ["sin"] = new(Math.Sin, formatted => $"sin({formatted}) ="),
        ["cos"] = new(Math.Cos, formatted => $"cos({formatted}) ="),
        ["tan"] = new(Math.Tan, formatted => $"tan({formatted}) ="),
        ["sqrt"] = new(value => value < 0 ? double.NaN : Math.Sqrt(value), formatted => $"\u221A({formatted}) ="),
        ["fact"] = new(CalculateFactorial, formatted => $"n!({formatted}) =")
    };
    private readonly List<MemoryEntry> _memoryEntries = new();
    private int _memoryDisplayIndex = -1;

    public MainForm()
    {
        InitializeComponent();
        UpdateDisplay("0");
        ApplyTheme();
    }

    private void OnDigitClick(object? sender, EventArgs e)
    {
        if (sender is not Button { Text: var digit })
        {
            return;
        }

        if (_shouldResetDisplay || DisplayTextBox.Text == "0")
        {
            UpdateDisplay(digit);
            _shouldResetDisplay = false;
            return;
        }

        UpdateDisplay(DisplayTextBox.Text + digit);
    }

    private void OnDecimalClick(object? sender, EventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        var separator = button.Tag as string ?? button.Text;
        var display = DisplayTextBox.Text;

        if (_shouldResetDisplay)
        {
            UpdateDisplay("0" + separator);
            _shouldResetDisplay = false;
            return;
        }

        if (!display.Contains(DisplayDecimalSeparator))
        {
            UpdateDisplay(display + separator);
        }
    }

    private void OnOperatorClick(object? sender, EventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (!TryGetDisplayValue(out var current))
        {
            return;
        }

        if (HasPendingOperation && !_shouldResetDisplay)
        {
            current = ExecutePendingOperation(current);
            UpdateDisplayFromDouble(current);
        }

        _pendingValue = current;
        _pendingOperator = button.Tag as string ?? button.Text;
        _shouldResetDisplay = true;
    }

    private void OnEqualsClick(object? sender, EventArgs e)
    {
        if (!HasPendingOperation || !TryGetDisplayValue(out var current))
        {
            return;
        }

        var pendingValue = _pendingValue!.Value;
        var pendingOperator = _pendingOperator!;
        var result = Evaluate(pendingValue, current, pendingOperator);
        UpdateDisplayFromDouble(result);

        var operationText = $"{FormatDouble(pendingValue)} {GetDisplayOperator(pendingOperator)} {FormatDouble(current)} =";
        UpdateOperationLabel(operationText);
        StoreInMemory(operationText, result);

        ResetPendingOperation();
        _shouldResetDisplay = true;
    }

    private void OnClearEntryClick(object? sender, EventArgs e)
    {
        UpdateDisplay("0");
        _shouldResetDisplay = true;
        UpdateOperationLabel(string.Empty);
    }

    private void OnClearAllClick(object? sender, EventArgs e)
    {
        UpdateDisplay("0");
        _pendingValue = null;
        _pendingOperator = null;
        _shouldResetDisplay = false;
        UpdateOperationLabel(string.Empty);
    }

    private void OnBackspaceClick(object? sender, EventArgs e)
    {
        if (_shouldResetDisplay)
        {
            UpdateDisplay("0");
            _shouldResetDisplay = false;
            return;
        }

        var text = DisplayTextBox.Text;
        if (text.Length > 1 && text is not "0")
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
        var display = DisplayTextBox.Text;

        if (display is "0" or "")
        {
            return;
        }

        if (display.StartsWith("-", StringComparison.Ordinal))
        {
            UpdateDisplay(display[1..]);
        }
        else
        {
            UpdateDisplay("-" + display);
        }
    }

    private void OnUnaryOperationClick(object? sender, EventArgs e)
    {
        if (sender is not Button button)
        {
            return;
        }

        if (!TryGetDisplayValue(out var current))
        {
            return;
        }

        var operationKey = button.Tag as string ?? button.Text;

        if (!UnaryOperations.TryGetValue(operationKey, out var unaryOperation))
        {
            return;
        }

        var result = unaryOperation.Evaluate(current);
        UpdateDisplayFromDouble(result);

        var formattedOperand = FormatDouble(current);
        var operationText = unaryOperation.OperationLabelFactory?.Invoke(formattedOperand) ?? string.Empty;
        UpdateOperationLabel(operationText);

        StoreInMemory(operationText, result);

        _shouldResetDisplay = true;
    }

    private void OnPercentClick(object? sender, EventArgs e)
    {
        if (!TryGetDisplayValue(out var current))
        {
            return;
        }

        var result = HasPendingOperation
            ? _pendingValue!.Value * current / 100d
            : current / 100d;

        UpdateDisplayFromDouble(result);

        if (HasPendingOperation)
        {
            UpdateOperationLabel($"{FormatDouble(_pendingValue!.Value)} {GetDisplayOperator(_pendingOperator!)} {FormatDouble(result)}");
        }
        else
        {
            var operationText = $"{FormatDouble(current)}% =";
            UpdateOperationLabel(operationText);
            StoreInMemory(operationText, result);
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
            ? "Eredm\u00E9ny ="
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

        var indexToRemove = _memoryDisplayIndex >= 0 && _memoryDisplayIndex < _memoryEntries.Count
            ? _memoryDisplayIndex
            : 0;

        _memoryEntries.RemoveAt(indexToRemove);

        if (_memoryEntries.Count == 0)
        {
            _memoryDisplayIndex = -1;
        }
        else if (_memoryDisplayIndex >= _memoryEntries.Count)
        {
            _memoryDisplayIndex = _memoryEntries.Count - 1;
        }

        RefreshMemoryList();

        if (_memoryDisplayIndex != -1)
        {
            ShowMemoryEntry(_memoryDisplayIndex);
        }
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
        if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || Math.Abs(value - Math.Round(value)) > 1e-9)
        {
            return double.NaN;
        }

        var integer = (int)Math.Round(value);
        if (integer > 170)
        {
            return double.PositiveInfinity;
        }

        double result = 1d;
        for (var i = 2; i <= integer; i++)
        {
            result *= i;
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

        ApplyButtonThemeRecursive(LayoutPanel);
    }

    private void ApplyButtonThemeRecursive(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            if (control is Button button)
            {
                ApplyButtonTheme(button);
            }

            if (control.HasChildren)
            {
                ApplyButtonThemeRecursive(control);
            }
        }
    }

    private static bool IsOperatorTag(string tag) => tag is "+" or "-" or "*" or "/";
    private static bool IsClearTag(string tag) => tag is "CE" or "C" or "Backspace";
    private static bool IsFunctionTag(string tag) => tag is "sin" or "cos" or "tan" or "sqrt" or "fact" or "%";
    private static bool IsMemoryTag(string tag) => tag.StartsWith("memory-", StringComparison.Ordinal);

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
        var tag = button.Tag as string ?? button.Text;

        if (IsMemoryTag(tag))
        {
            background = memoryBackColor;
            foreColor = Color.White;
        }
        else if (tag == "=")
        {
            background = equalsBackColor;
            foreColor = Color.White;
        }
        else if (IsOperatorTag(tag))
        {
            background = operatorBackColor;
        }
        else if (IsClearTag(tag))
        {
            background = clearBackColor;
        }
        else if (IsFunctionTag(tag))
        {
            background = functionBackColor;
        }

        button.BackColor = background;
        button.ForeColor = foreColor;
        button.FlatAppearance.BorderColor = _isDarkMode ? Color.FromArgb(70, 70, 70) : Color.LightGray;
    }
    private static double Evaluate(double left, double right, string op) => op switch
    {
        "+" => left + right,
        "-" => left - right,
        "*" or "\u00D7" => left * right,
        "/" or "\u00F7" => right == 0 ? double.NaN : left / right,
        _ => right
    };

    private double ExecutePendingOperation(double current) => Evaluate(_pendingValue!.Value, current, _pendingOperator!);

    private void ResetPendingOperation()
    {
        _pendingValue = null;
        _pendingOperator = null;
    }
    private bool TryGetDisplayValue(out double value)
    {
        var sanitized = DisplayTextBox.Text.Replace(DisplayDecimalSeparator, '.');
        return double.TryParse(sanitized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private void UpdateDisplayFromDouble(double value)
    {
        UpdateDisplay(FormatDouble(value));
    }

    private void UpdateDisplay(string value)
    {
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
        "*" => "\u00D7",
        "/" => "\u00F7",
        _ => op
    };

    private void UpdateOperationLabel(string text)
    {
        OperationLabel.Text = text;
    }

    private void StoreInMemory(string operationText, double result)
    {
        if (string.IsNullOrWhiteSpace(operationText))
        {
            return;
        }

        AddToMemory(new MemoryEntry(operationText, FormatDouble(result)));
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

