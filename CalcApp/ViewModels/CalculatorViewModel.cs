using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows.Input;
using Serilog;

namespace CalcApp.ViewModels;

/// <summary>
/// A számológép nézetmodellje, amely a számológép logikáját és állapotát kezeli.
/// </summary>
public sealed class CalculatorViewModel : BaseViewModel
{
    #region Constants

    private const int MaxFactorial = 170;
    private const int MaxDisplayLength = 64;
    private const int MaxMemoryHistoryLength = 1024;
    private const int MemoryHistoryDisplayCount = 50;
    private const double IntegerTolerance = 1e-7;
    private const double DegreesToRadians = Math.PI / 180.0;
    private const string AppDataFolderName = "CalcApp";
    private const string LogsFolderName = "logs";

    private const string ErrorString = "Error";
    private const string ZeroString = "0";
    private const string MinusZeroString = "-0";
    private const string DecimalPointString = ".";
    private const string MinusString = "-";
    private const string MinusZeroDecimalString = "-0.";
    private const string ZeroDecimalString = "0.";
    private const string MemoryPrefix = "Memory: ";
    private const string MemoryTotalPrefix = "Memory total: ";
    private const string HistoryPrefix = "History: ";

    #endregion

    #region Static Fields

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
    private static readonly double[] FactorialCache = CreateFactorialCache();

    private static readonly Func<double, double> SinFunc = Math.Sin;
    private static readonly Func<double, double> CosFunc = Math.Cos;
    private static readonly Func<double, double> TanFunc = Math.Tan;

    #endregion

    #region Fields

    private readonly Func<string> _logPathProvider;
    private readonly Action<string> _logDirectoryOpener;
    private readonly Queue<(bool IsAddition, string Description)> _memoryHistoryEntries = new();
    private readonly StringBuilder _memoryHistoryBuilder = new(256);

    private string _display = ZeroString;
    private string _memoryHistoryText = string.Empty;
    private string? _lastOperationDescription;
    private double? _leftOperand;
    private string? _pendingOperator;
    private bool _shouldResetDisplay = true;
    private double _memoryValue;
    private double? _lastRightOperand;
    private string? _lastOperator;

    #endregion

    #region Properties

    /// <summary>
    /// Megadja, hogy a turbó mód engedélyezve van-e.
    /// </summary>
    public bool IsTurboEnabled { get; private set; }

    /// <summary>
    /// A számológép kijelzőjének aktuális értéke.
    /// </summary>
    public string Display
    {
        get => _display;
        set
        {
            if (_display == value) return;

            _display = value;
            ScheduleDisplayUpdate();
        }
    }

    /// <summary>
    /// A memóriaelőzmények elemeit tartalmazó gyűjtemény.
    /// </summary>
    public ObservableCollection<string> MemoryItems { get; } = [];

    #endregion

    #region Commands

    /// <summary>Parancs egy számjegy feldolgozásához.</summary>
    public ICommand DigitCommand { get; }

    /// <summary>Parancs egy operátor feldolgozásához.</summary>
    public ICommand OperatorCommand { get; }

    /// <summary>Parancs a tizedesjel feldolgozásához.</summary>
    public ICommand DecimalCommand { get; }

    /// <summary>Parancs a számológép törléséhez.</summary>
    public ICommand ClearCommand { get; }

    /// <summary>Parancs az előjel megváltoztatásához.</summary>
    public ICommand SignCommand { get; }

    /// <summary>Parancs a százalék feldolgozásához.</summary>
    public ICommand PercentCommand { get; }

    /// <summary>Parancs az utolsó karakter törléséhez.</summary>
    public ICommand DeleteCommand { get; }

    /// <summary>Parancs az egyenlőségjel feldolgozásához.</summary>
    public ICommand EqualsCommand { get; }

    /// <summary>Parancs a szinusz függvény alkalmazásához.</summary>
    public ICommand SinCommand { get; }

    /// <summary>Parancs a koszinusz függvény alkalmazásához.</summary>
    public ICommand CosCommand { get; }

    /// <summary>Parancs a tangens függvény alkalmazásához.</summary>
    public ICommand TanCommand { get; }

    /// <summary>Parancs a négyzetgyök függvény alkalmazásához.</summary>
    public ICommand SqrtCommand { get; }

    /// <summary>Parancs a faktoriális függvény alkalmazásához.</summary>
    public ICommand FactorialCommand { get; }

    /// <summary>Parancs a memória hozzáadásához.</summary>
    public ICommand MemoryAddCommand { get; }

    /// <summary>Parancs a memória kivonásához.</summary>
    public ICommand MemorySubtractCommand { get; }

    /// <summary>Parancs a memória előhívásához.</summary>
    public ICommand MemoryRecallCommand { get; }

    /// <summary>Parancs a memória törléséhez.</summary>
    public ICommand MemoryClearCommand { get; }

    /// <summary>Parancs a naplók megnyitásához.</summary>
    public ICommand OpenLogsCommand { get; }

    #endregion

    #region Constructors

    /// <summary>
    /// Inicializálja a CalculatorViewModel új példányát alapértelmezett beállításokkal.
    /// </summary>
    public CalculatorViewModel()
        : this(logPathProvider: null, logDirectoryOpener: null)
    {
    }

    /// <summary>
    /// Inicializálja a CalculatorViewModel új példányát.
    /// </summary>
    /// <param name="logPathProvider">A naplófájlok elérési útját biztosító függvény.</param>
    /// <param name="logDirectoryOpener">A naplómappát megnyitó függvény.</param>
    public CalculatorViewModel(Func<string>? logPathProvider, Action<string>? logDirectoryOpener)
    {
        _logPathProvider = logPathProvider ?? DefaultLogPathProvider;
        _logDirectoryOpener = logDirectoryOpener ?? DefaultLogDirectoryOpener;

        DigitCommand = new RelayCommand(p => ProcessDigit(p?.ToString()));
        OperatorCommand = new RelayCommand(p => ProcessOperator(p?.ToString()));
        DecimalCommand = new RelayCommand(_ => ProcessDecimal());
        ClearCommand = new RelayCommand(_ => ResetCalculatorState());
        SignCommand = new RelayCommand(_ => ProcessSign());
        PercentCommand = new RelayCommand(_ => ProcessPercent());
        DeleteCommand = new RelayCommand(_ => ProcessDelete());
        EqualsCommand = new RelayCommand(_ => ProcessEquals());
        SinCommand = new RelayCommand(_ => ApplyTrigonometricFunction(SinFunc, "sin"));
        CosCommand = new RelayCommand(_ => ApplyTrigonometricFunction(CosFunc, "cos"));
        TanCommand = new RelayCommand(_ => ApplyTrigonometricFunction(TanFunc, "tan", validateTan: true));
        SqrtCommand = new RelayCommand(_ => ProcessSqrt());
        FactorialCommand = new RelayCommand(_ => ProcessFactorial());
        MemoryAddCommand = new RelayCommand(_ => ProcessMemoryAdd());
        MemorySubtractCommand = new RelayCommand(_ => ProcessMemorySubtract());
        MemoryRecallCommand = new RelayCommand(_ => ProcessMemoryRecall());
        MemoryClearCommand = new RelayCommand(_ => ResetMemory());
        OpenLogsCommand = new RelayCommand(_ => ProcessOpenLogs());

        UpdateMemoryDisplay();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Beállítja a turbó módot.
    /// </summary>
    /// <param name="enabled">Engedélyezve legyen-e a turbó mód.</param>
    public void SetTurboMode(bool enabled)
    {
        if (IsTurboEnabled == enabled) return;

        IsTurboEnabled = enabled;
        OnPropertyChanged(nameof(IsTurboEnabled));
    }

    #endregion

    #region Private Methods - Display

    private void ScheduleDisplayUpdate()
    {
        OnPropertyChanged(nameof(Display));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetDisplayValue(out double value)
    {
        var text = Display;

        if (string.IsNullOrEmpty(text) || text.Length > MaxDisplayLength || text == ErrorString)
        {
            value = 0;
            return false;
        }

        if (text == MinusString)
        {
            value = 0;
            return true;
        }

        if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, Culture, out value))
        {
            value = 0;
            return false;
        }

        return IsFinite(value);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetDisplayValue(double value)
    {
        var formatted = FormatNumber(value);
        if (formatted == ErrorString)
        {
            ShowError();
            return;
        }

        Display = formatted;
    }

    private void ShowError()
    {
        Display = ErrorString;
        _leftOperand = null;
        _pendingOperator = null;
        _shouldResetDisplay = true;
        _lastOperationDescription = null;
        _lastOperator = null;
        _lastRightOperand = null;
    }

    #endregion

    #region Private Methods - Digit Processing

    private void ProcessDigit(string? digit)
    {
        if (string.IsNullOrEmpty(digit)) return;

        _lastOperationDescription = null;

        if (_shouldResetDisplay || Display is ZeroString or ErrorString)
        {
            Display = digit.Length <= MaxDisplayLength ? digit : digit[..MaxDisplayLength];
        }
        else if (Display.Length + digit.Length <= MaxDisplayLength)
        {
            Display = string.Concat(Display, digit);
        }

        _shouldResetDisplay = false;
    }

    private void ProcessDecimal()
    {
        if (_shouldResetDisplay || Display == ErrorString)
        {
            Display = ZeroDecimalString;
            _shouldResetDisplay = false;
            _lastOperationDescription = null;
            return;
        }

        if (Display.Contains('.') || Display.Length + 1 > MaxDisplayLength) return;

        Display = Display == MinusString
            ? MinusZeroDecimalString
            : string.Concat(Display, DecimalPointString);

        _lastOperationDescription = null;
    }

    private void ProcessDelete()
    {
        if (_shouldResetDisplay || Display == ErrorString)
        {
            ResetCalculatorState();
            return;
        }

        Display = Display.Length <= 1 || (Display.Length == 2 && Display.StartsWith('-'))
            ? ZeroString
            : Display[..^1];

        _lastOperationDescription = null;
    }

    private void ProcessSign()
    {
        if (Display == ErrorString) return;

        if (_shouldResetDisplay)
        {
            Display = MinusString;
            _shouldResetDisplay = false;
            _lastOperationDescription = null;
            return;
        }

        if (Display == MinusString)
        {
            Display = ZeroString;
            _lastOperationDescription = null;
            return;
        }

        if (Display.StartsWith('-'))
        {
            Display = Display[1..];
        }
        else
        {
            if (Display.Length + 1 > MaxDisplayLength)
            {
                if (TryGetDisplayValue(out var value))
                {
                    SetDisplayValue(-value);
                }
                return;
            }

            Display = Display == ZeroString
                ? MinusString
                : string.Concat(MinusString, Display);
        }

        _lastOperationDescription = null;
    }

    #endregion

    #region Private Methods - Operations

    private void ProcessOperator(string? operatorSymbol)
    {
        if (!IsValidOperator(operatorSymbol)) return;
        if (!TryGetDisplayValue(out var currentValue)) return;

        if (_leftOperand.HasValue && _pendingOperator is not null && !_shouldResetDisplay)
        {
            ExecuteOperation(_leftOperand.Value, currentValue, _pendingOperator);
        }
        else
        {
            _leftOperand = currentValue;
        }

        _pendingOperator = operatorSymbol;
        _shouldResetDisplay = true;
    }

    private static bool IsValidOperator(string? op)
        => op is "+" or "-" or "*" or "/" or "^";

    private void ExecuteOperation(double left, double right, string op)
    {
        try
        {
            var result = Evaluate(left, right, op);
            if (!IsFinite(result))
            {
                ShowError();
                return;
            }

            _leftOperand = result;
            SetDisplayValue(result);

            if (Display != ErrorString)
            {
                RecordOperation($"{FormatNumber(left)}{op}{FormatNumber(right)}", result);
            }
        }
        catch (Exception ex) when (ex is DivideByZeroException or InvalidOperationException)
        {
            ShowError();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Váratlan hiba az ExecuteOperation-ben");
            ShowError();
        }
    }

    private void ProcessEquals()
    {
        if (_pendingOperator != null)
        {
            ProcessPendingOperation();
        }
        else if (_lastOperator != null && _lastRightOperand.HasValue)
        {
            RepeatLastOperation();
        }
    }

    private void ProcessPendingOperation()
    {
        if (!_leftOperand.HasValue) return;
        if (!TryGetDisplayValue(out var rightOperand)) return;

        _lastRightOperand = rightOperand;
        _lastOperator = _pendingOperator;

        ExecuteOperation(_leftOperand.Value, rightOperand, _pendingOperator!);

        _pendingOperator = null;
        _shouldResetDisplay = true;
        _leftOperand = null;
    }

    private void RepeatLastOperation()
    {
        if (!TryGetDisplayValue(out var currentDisplay)) return;

        ExecuteOperation(currentDisplay, _lastRightOperand!.Value, _lastOperator!);

        _shouldResetDisplay = true;
        _leftOperand = null;
    }

    private void ProcessPercent()
    {
        if (!TryGetDisplayValue(out var value)) return;

        var originalValue = value;
        var result = CalculatePercentage(value);

        if (!IsFinite(result))
        {
            ShowError();
            return;
        }

        SetDisplayValue(result);
        if (Display == ErrorString) return;

        _shouldResetDisplay = true;

        if (!_leftOperand.HasValue)
        {
            RecordOperation($"{FormatNumber(originalValue)}%", result);
        }
    }

    private double CalculatePercentage(double value)
    {
        if (!_leftOperand.HasValue || string.IsNullOrEmpty(_pendingOperator))
            return value / 100.0;

        return _pendingOperator is "+" or "-"
            ? _leftOperand.Value * value / 100.0
            : value / 100.0;
    }

    private void ResetCalculatorState()
    {
        Display = ZeroString;
        _leftOperand = null;
        _pendingOperator = null;
        _shouldResetDisplay = false;
        _lastOperationDescription = null;
        _lastOperator = null;
        _lastRightOperand = null;
    }

    #endregion

    #region Private Methods - Trigonometric Functions

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyTrigonometricFunction(
        Func<double, double> func,
        string operationName,
        bool validateTan = false)
    {
        if (!TryGetDisplayValue(out var value)) return;

        var originalValue = value;
        var radians = value * DegreesToRadians;

        if (validateTan && Math.Abs(Math.Cos(radians)) < 1e-12)
        {
            ShowError();
            return;
        }

        try
        {
            var result = func(radians);

            if (!IsFinite(result))
            {
                ShowError();
                return;
            }

            SetDisplayValue(result);
            if (Display == ErrorString) return;

            _shouldResetDisplay = true;
            RecordOperation($"{operationName}({FormatNumber(originalValue)})", result);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Hiba a {OperationName} függvényben", operationName);
            ShowError();
        }
    }

    private void ProcessSqrt()
    {
        if (!TryGetDisplayValue(out var value)) return;

        if (value < 0)
        {
            ShowError();
            return;
        }

        var result = Math.Sqrt(value);
        if (!IsFinite(result))
        {
            ShowError();
            return;
        }

        SetDisplayValue(result);
        if (Display == ErrorString) return;

        _shouldResetDisplay = true;
        RecordOperation($"sqrt({FormatNumber(value)})", result);
    }

    private void ProcessFactorial()
    {
        if (!TryGetDisplayValue(out var value)) return;

        if (value < 0 || value > MaxFactorial || !IsApproximatelyInteger(value))
        {
            ShowError();
            return;
        }

        try
        {
            var n = (int)Math.Round(value);
            var result = Factorial(n);

            SetDisplayValue(result);
            if (Display == ErrorString) return;

            _shouldResetDisplay = true;
            RecordOperation($"{n}!", result);
        }
        catch (OverflowException)
        {
            ShowError();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Váratlan hiba a Factorial-ban");
            ShowError();
        }
    }

    #endregion

    #region Private Methods - Memory

    private void ProcessMemoryAdd()
    {
        ProcessMemoryOperation(isAddition: true);
    }

    private void ProcessMemorySubtract()
    {
        ProcessMemoryOperation(isAddition: false);
    }

    private void ProcessMemoryOperation(bool isAddition)
    {
        if (!TryGetDisplayValue(out var value)) return;

        try
        {
            var newValue = isAddition
                ? _memoryValue + value
                : _memoryValue - value;

            if (!IsFinite(newValue))
            {
                ResetMemory();
                ShowError();
                return;
            }

            _memoryValue = newValue;
            TrackMemoryOperation(value, isAddition);
            _shouldResetDisplay = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Hiba a memóriaműveletben");
            ResetMemory();
            ShowError();
        }
    }

    private void ProcessMemoryRecall()
    {
        SetDisplayValue(_memoryValue);
        if (Display == ErrorString)
        {
            ResetMemory();
            return;
        }

        _shouldResetDisplay = true;
        _lastOperationDescription = null;
    }

    private void ResetMemory()
    {
        _memoryValue = 0;
        _memoryHistoryEntries.Clear();
        _memoryHistoryText = string.Empty;
        _lastOperationDescription = null;
        UpdateMemoryDisplay();
    }

    private void TrackMemoryOperation(double value, bool isAddition)
    {
        var description = _lastOperationDescription ?? FormatNumber(value);
        if (description == ErrorString)
        {
            description = ZeroString;
        }

        _memoryHistoryEntries.Enqueue((isAddition, description));

        while (_memoryHistoryEntries.Count > MaxMemoryHistoryLength)
        {
            _memoryHistoryEntries.Dequeue();
        }

        UpdateMemoryHistoryText();
        UpdateMemoryDisplay();
    }

    private void UpdateMemoryHistoryText()
    {
        if (_memoryHistoryEntries.Count == 0)
        {
            _memoryHistoryText = string.Empty;
            return;
        }

        var entryCount = _memoryHistoryEntries.Count;
        var skip = Math.Max(0, entryCount - MemoryHistoryDisplayCount);

        _memoryHistoryBuilder.Clear();
        _memoryHistoryBuilder.EnsureCapacity((entryCount - skip) * 8);

        var index = 0;
        var hasContent = false;

        foreach (var (isAddition, description) in _memoryHistoryEntries)
        {
            if (index++ < skip) continue;

            if (hasContent) _memoryHistoryBuilder.Append("; ");
            _memoryHistoryBuilder.Append(isAddition ? "+ " : "- ");
            _memoryHistoryBuilder.Append(description);
            hasContent = true;
        }

        if (skip > 0)
        {
            _memoryHistoryBuilder.Insert(0, "...; ");
        }

        _memoryHistoryText = _memoryHistoryBuilder.ToString();
    }

    private void UpdateMemoryDisplay()
    {
        try
        {
            var value = FormatNumber(_memoryValue);
            if (value == ErrorString)
            {
                value = ZeroString;
            }

            var hasHistory = !string.IsNullOrEmpty(_memoryHistoryText);

            if (hasHistory)
            {
                UpdateMemoryItemsWithHistory(value);
            }
            else
            {
                UpdateMemoryItemsSimple(value);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Hiba a memória kijelző frissítésekor");
        }
    }

    private void UpdateMemoryItemsWithHistory(string value)
    {
        var total = string.Concat(MemoryTotalPrefix, value);
        var history = string.Concat(HistoryPrefix, _memoryHistoryText);

        if (MemoryItems.Count == 2)
        {
            if (MemoryItems[0] != total) MemoryItems[0] = total;
            if (MemoryItems[1] != history) MemoryItems[1] = history;
        }
        else
        {
            MemoryItems.Clear();
            MemoryItems.Add(total);
            MemoryItems.Add(history);
        }
    }

    private void UpdateMemoryItemsSimple(string value)
    {
        var mem = string.Concat(MemoryPrefix, value);

        if (MemoryItems.Count == 1)
        {
            if (MemoryItems[0] != mem) MemoryItems[0] = mem;
        }
        else
        {
            MemoryItems.Clear();
            MemoryItems.Add(mem);
        }
    }

    #endregion

    #region Private Methods - Logging

    private void ProcessOpenLogs()
    {
        try
        {
            var logPath = _logPathProvider();
            if (!Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            _logDirectoryOpener(logPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Nem sikerült megnyitni a naplók mappáját");
        }
    }

    private static string DefaultLogPathProvider()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolderName,
            LogsFolderName);

    private static void DefaultLogDirectoryOpener(string logPath)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = logPath,
            UseShellExecute = true,
            Verb = "open"
        });
    }

    private void RecordOperation(string description, double result)
    {
        var formattedResult = FormatNumber(result);
        if (formattedResult == ErrorString)
        {
            _lastOperationDescription = null;
            return;
        }

        _lastOperationDescription = $"{description}={formattedResult}";
    }

    #endregion

    #region Private Methods - Utilities

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsFinite(double value)
        => !double.IsNaN(value) && !double.IsInfinity(value);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsApproximatelyInteger(double value)
    {
        if (!IsFinite(value)) return false;
        return Math.Abs(value - Math.Round(value)) <= IntegerTolerance;
    }

    private static string FormatNumber(double value)
    {
        if (!IsFinite(value)) return ErrorString;
        if (value == 0.0 || Math.Abs(value) < double.Epsilon) return ZeroString;

        if (IsApproximatelyInteger(value) && value is >= -1e14 and <= 1e14)
        {
            var s = Math.Round(value).ToString("G", Culture);
            if (s == MinusZeroString) return ZeroString;
            return s.Length > MaxDisplayLength ? value.ToString("E6", Culture) : s;
        }

        Span<char> buffer = stackalloc char[64];
        if (value.TryFormat(buffer, out int charsWritten, "G12", Culture))
        {
            var formatted = buffer[..charsWritten];

            var dotIndex = formatted.IndexOf('.');
            if (dotIndex != -1 && formatted.IndexOf('E') == -1)
            {
                int end = formatted.Length - 1;
                while (end > dotIndex && formatted[end] == '0')
                {
                    end--;
                }
                if (end == dotIndex) end--;
                formatted = formatted[..(end + 1)];
            }

            var result = formatted.ToString();
            if (result == MinusZeroString) return ZeroString;
            return result.Length > MaxDisplayLength ? value.ToString("E6", Culture) : (result.Length > 0 ? result : ZeroString);
        }

        var fallback = value.ToString("G12", Culture);
        return fallback.Length > MaxDisplayLength ? value.ToString("E6", Culture) : fallback;
    }

    private static double Evaluate(double left, double right, string operatorSymbol)
    {
        if (!IsFinite(left) || !IsFinite(right))
        {
            throw new InvalidOperationException("Érvénytelen operandus értékek");
        }

        var result = operatorSymbol switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" when Math.Abs(right) >= double.Epsilon => left / right,
            "/" => throw new DivideByZeroException(),
            "^" => Math.Pow(left, right),
            _ => throw new InvalidOperationException($"Ismeretlen operátor: {operatorSymbol}"),
        };

        if (!IsFinite(result))
        {
            throw new OverflowException("A művelet túlcsordulást eredményezett");
        }

        return result;
    }

    private static double[] CreateFactorialCache()
    {
        var cache = new double[MaxFactorial + 1];
        cache[0] = 1.0;

        for (var i = 1; i <= MaxFactorial; i++)
        {
            var next = cache[i - 1] * i;

            if (!IsFinite(next))
            {
                cache[i] = double.PositiveInfinity;
                break;
            }

            cache[i] = next;
        }

        return cache;
    }

    private static double Factorial(int value)
    {
        if (value < 0)
            throw new OverflowException("A faktoriális nem értelmezett negatív számokra");

        if (value > MaxFactorial)
            throw new OverflowException($"Faktoriális túlcsordulás: a maximálisan támogatott érték {MaxFactorial}");

        var result = FactorialCache[value];
        if (!IsFinite(result))
        {
            throw new OverflowException($"Faktoriális túlcsordulás: {value}!");
        }

        return result;
    }

    #endregion
}
