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

namespace CalcApp.ViewModels
{
    /// <summary>
    /// A számológép nézetmodellje, amely a számológép logikáját és állapotát tartalmazza.
    /// </summary>
    public class CalculatorViewModel : BaseViewModel
    {
        private string _display = "0";
        private string _memoryHistoryText = string.Empty;
        private string? _lastOperationDescription = string.Empty;
        private double? _leftOperand = null;
        private string? _pendingOperator = null;
        private bool _shouldResetDisplay = true;
        private double _memoryValue = 0;
        private double? _lastRightOperand = null;
        private string? _lastOperator = null;
        private readonly Queue<(bool, string)> _memoryHistoryEntries = new();
        private readonly StringBuilder _memoryHistoryBuilder = new(256);

        /// <summary>
        /// Megadja, hogy a turbó mód engedélyezve van-e.
        /// </summary>
        public bool IsTurboEnabled { get; private set; }

        private readonly Func<string> _logPathProvider;
        private readonly Action<string> _logDirectoryOpener;

        /// <summary>
        /// A számológép kijelzőjének aktuális értéke.
        /// </summary>
        public string Display
        {
            get => _display;
            set
            {
                if (_display != value)
                {
                    _display = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// A memóriaelőzmények elemeit tartalmazó gyűjtemény.
        /// </summary>
        public ObservableCollection<string> MemoryItems { get; } = [];

        /// <summary> Parancs egy számjegy feldolgozásához. </summary>
        public ICommand DigitCommand { get; }
        /// <summary> Parancs egy operátor feldolgozásához. </summary>
        public ICommand OperatorCommand { get; }
        /// <summary> Parancs a tizedesjel feldolgozásához. </summary>
        public ICommand DecimalCommand { get; }
        /// <summary> Parancs a számológép törléséhez. </summary>
        public ICommand ClearCommand { get; }
        /// <summary> Parancs az előjel megváltoztatásához. </summary>
        public ICommand SignCommand { get; }
        /// <summary> Parancs a százalék feldolgozásához. </summary>
        public ICommand PercentCommand { get; }
        /// <summary> Parancs az utolsó karakter törléséhez. </summary>
        public ICommand DeleteCommand { get; }
        /// <summary> Parancs az egyenlőségjel feldolgozásához. </summary>
        public ICommand EqualsCommand { get; }
        /// <summary> Parancs a szinusz függvény alkalmazásához. </summary>
        public ICommand SinCommand { get; }
        /// <summary> Parancs a koszinusz függvény alkalmazásához. </summary>
        public ICommand CosCommand { get; }
        /// <summary> Parancs a tangens függvény alkalmazásához. </summary>
        public ICommand TanCommand { get; }
        /// <summary> Parancs a négyzetgyök függvény alkalmazásához. </summary>
        public ICommand SqrtCommand { get; }
        /// <summary> Parancs a faktoriális függvény alkalmazásához. </summary>
        public ICommand FactorialCommand { get; }
        /// <summary> Parancs a memória hozzáadásához. </summary>
        public ICommand MemoryAddCommand { get; }
        /// <summary> Parancs a memória kivonásához. </summary>
        public ICommand MemorySubtractCommand { get; }
        /// <summary> Parancs a memória előhívásához. </summary>
        public ICommand MemoryRecallCommand { get; }
        /// <summary> Parancs a memória törléséhez. </summary>
        public ICommand MemoryClearCommand { get; }
        /// <summary> Parancs a naplók megnyitásához. </summary>
        public ICommand OpenLogsCommand { get; }

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private static readonly double DegreesToRadians = Math.PI / 180.0;
        private const int MaxFactorial = 170; // 170! fits in double, 171! overflows
        private const int MaxDisplayLength = 64; // protect against extremely long input/overflow UI
        private const int MaxMemoryHistoryLength = 1024; // bound memory history to avoid unbounded growth
        private const int MemoryHistoryDisplayCount = 50; // number of items to show in history text
        private const double IntegerTolerance = 1e-7; // tolerance for treating values as integers
        private static readonly double[] _factorialCache = CreateFactorialCache();

        private static readonly Func<double, double> SinFunc = Math.Sin;
        private static readonly Func<double, double> CosFunc = Math.Cos;
        private static readonly Func<double, double> TanFunc = Math.Tan;

        /// <summary>
        /// Inicializálja a CalculatorViewModel új példányát.
        /// </summary>
        public CalculatorViewModel(Func<string>? logPathProvider = null, Action<string>? logDirectoryOpener = null)
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
            SinCommand = new RelayCommand(_ => ApplyUnaryFunction(SinFunc, "sin", degrees: true));
            CosCommand = new RelayCommand(_ => ApplyUnaryFunction(CosFunc, "cos", degrees: true));
            TanCommand = new RelayCommand(_ => ApplyUnaryFunction(TanFunc, "tan", degrees: true, validateTan: true));
            SqrtCommand = new RelayCommand(_ => ProcessSqrt());
            FactorialCommand = new RelayCommand(_ => ProcessFactorial());
            MemoryAddCommand = new RelayCommand(_ => ProcessMemoryAdd());
            MemorySubtractCommand = new RelayCommand(_ => ProcessMemorySubtract());
            MemoryRecallCommand = new RelayCommand(_ => ProcessMemoryRecall());
            MemoryClearCommand = new RelayCommand(_ => ResetMemory());
            OpenLogsCommand = new RelayCommand(_ => ProcessOpenLogs());

            InitializeMemory();
        }

        /// <summary>
        /// Inicializálja a memória állapotát.
        /// </summary>
        private void InitializeMemory()
        {
            UpdateMemoryDisplay();
        }

        /// <summary>
        /// Frissíti a memória kijelzőjét.
        /// </summary>
        private void UpdateMemoryDisplay()
        {
            try
            {
                var value = FormatNumber(_memoryValue);
                if (value == "Error")
                {
                    value = "0";
                }

                var historyText = _memoryHistoryText;
                var hasHistory = !string.IsNullOrEmpty(historyText);

                // Optimize: Update in place if possible to avoid UI flicker/layout cycles
                if (hasHistory)
                {
                    // We need 2 items: "Memory total: ..." and "History: ..."
                    if (MemoryItems.Count == 2)
                    {
                        if (MemoryItems[0] != $"Memory total: {value}") MemoryItems[0] = $"Memory total: {value}";
                        if (MemoryItems[1] != $"History: {historyText}") MemoryItems[1] = $"History: {historyText}";
                    }
                    else
                    {
                        MemoryItems.Clear();
                        MemoryItems.Add($"Memory total: {value}");
                        MemoryItems.Add($"History: {historyText}");
                    }
                }
                else
                {
                    // We need 1 item: "Memory: ..."
                    if (MemoryItems.Count == 1)
                    {
                        if (MemoryItems[0] != $"Memory: {value}") MemoryItems[0] = $"Memory: {value}";
                    }
                    else
                    {
                        MemoryItems.Clear();
                        MemoryItems.Add($"Memory: {value}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating memory display");
            }
        }

        /// <summary>
        /// Feldolgoz egy számjegyet.
        /// </summary>
        /// <param name="digit">A feldolgozandó számjegy.</param>
        private void ProcessDigit(string? digit)
        {
            if (string.IsNullOrEmpty(digit)) return;

            _lastOperationDescription = null;
            if (_shouldResetDisplay || Display is "0" or "Error")
            {
                Display = digit.Length <= MaxDisplayLength ? digit : digit[..MaxDisplayLength];
            }
            else
            {
                var newLength = Display.Length + digit.Length;
                if (newLength <= MaxDisplayLength)
                {
                    Display += digit;
                }
            }
            _shouldResetDisplay = false;
        }

        /// <summary>
        /// Feldolgoz egy operátort.
        /// </summary>
        /// <param name="operatorSymbol">A feldolgozandó operátor.</param>
        private void ProcessOperator(string? operatorSymbol)
        {
            if (string.IsNullOrWhiteSpace(operatorSymbol) ||
                (operatorSymbol != "+" && operatorSymbol != "-" && operatorSymbol != "*" && operatorSymbol != "/" && operatorSymbol != "^"))
            {
                return;
            }

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

        /// <summary>
        /// Végrehajt egy műveletet.
        /// </summary>
        /// <param name="left">A bal oldali operandus.</param>
        /// <param name="right">A jobb oldali operandus.</param>
        /// <param name="op">Az operátor.</param>
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
                if (Display == "Error") return;
                RecordOperation($"{FormatNumber(left)}{op}{FormatNumber(right)}", result);
            }
            catch (DivideByZeroException)
            {
                ShowError();
            }
            catch (InvalidOperationException)
            {
                ShowError();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in ExecuteOperation");
                ShowError();
            }
        }

        /// <summary>
        /// Feldolgozza a tizedesjelet.
        /// </summary>
        private void ProcessDecimal()
        {
            if (_shouldResetDisplay || Display == "Error")
            {
                Display = "0.";
                _shouldResetDisplay = false;
                _lastOperationDescription = null;
                return;
            }

            if (!Display.Contains('.') && Display.Length + 1 <= MaxDisplayLength)
            {
                Display += ".";
                _lastOperationDescription = null;
            }
        }

        /// <summary>
        /// Visszaállítja a számológép állapotát.
        /// </summary>
        private void ResetCalculatorState()
        {
            Display = "0";
            _leftOperand = null;
            _pendingOperator = null;
            _shouldResetDisplay = false;
            _lastOperationDescription = null;
        }

        /// <summary>
        /// Beállítja a turbó módot.
        /// </summary>
        /// <param name="enabled">Engedélyezve legyen-e a turbó mód.</param>
        public void SetTurboMode(bool enabled)
        {
            if (IsTurboEnabled != enabled)
            {
                IsTurboEnabled = enabled;
                OnPropertyChanged(nameof(IsTurboEnabled));
            }
        }

        /// <summary>
        /// Feldolgozza az előjelváltást.
        /// </summary>
        private void ProcessSign()
        {
            if (!TryGetDisplayValue(out var value)) return;

            value = -value;
            SetDisplayValue(value);
            _lastOperationDescription = null;
        }

        /// <summary>
        /// Feldolgozza a százalékszámítást.
        /// </summary>
        private void ProcessPercent()
        {
            if (!TryGetDisplayValue(out var value)) return;

            var originalValue = value;
            double result;

            if (_leftOperand.HasValue && !string.IsNullOrEmpty(_pendingOperator))
            {
                if (_pendingOperator == "+" || _pendingOperator == "-")
                {
                    result = _leftOperand.Value * value / 100.0;
                }
                else // Assume * or /
                {
                    result = value / 100.0;
                }
            }
            else
            {
                result = value / 100.0;
            }

            if (!IsFinite(result))
            {
                ShowError();
                return;
            }

            SetDisplayValue(result);
            if (Display == "Error") return;
            _shouldResetDisplay = true;

            // Only record the operation if it's a standalone percent calculation
            if (!_leftOperand.HasValue)
            {
                RecordOperation($"{FormatNumber(originalValue)}%", result);
            }
        }

        /// <summary>
        /// Feldolgozza a törlés műveletet.
        /// </summary>
        private void ProcessDelete()
        {
            if (_shouldResetDisplay || Display == "Error")
            {
                Display = "0";
                _shouldResetDisplay = false;
                _lastOperationDescription = null;
                return;
            }
            Display = Display.Length <= 1 ? "0" : Display[..^1];
            _lastOperationDescription = null;
        }

        /// <summary>
        /// Feldolgozza az egyenlőségjel műveletet.
        /// </summary>
        private void ProcessEquals()
        {
            if (_pendingOperator != null)
            {
                if (!_leftOperand.HasValue) return;
                if (!TryGetDisplayValue(out var rightOperand)) return;

                _lastRightOperand = rightOperand;
                _lastOperator = _pendingOperator;

                ExecuteOperation(_leftOperand.Value, rightOperand, _pendingOperator);

                _pendingOperator = null;
                _shouldResetDisplay = true;
                // Reset left operand so next operator press picks up the display value
                _leftOperand = null;
            }
            else if (_lastOperator != null && _lastRightOperand.HasValue)
            {
                // Repeat last operation
                if (!TryGetDisplayValue(out var currentDisplay)) return;

                ExecuteOperation(currentDisplay, _lastRightOperand.Value, _lastOperator);

                _shouldResetDisplay = true;
                _leftOperand = null;
            }
        }

        /// <summary>
        /// Egyváltozós függvényt alkalmaz a kijelzőn lévő értékre.
        /// </summary>
        /// <param name="func">Az alkalmazandó függvény.</param>
        /// <param name="operationName">A művelet neve.</param>
        /// <param name="degrees">Fokokban számoljon-e.</param>
        /// <param name="validateTan">Érvényesítse-e a tangens függvényt.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void ApplyUnaryFunction(Func<double, double> func, string operationName, bool degrees = false, bool validateTan = false)
        {
            if (func == null || string.IsNullOrWhiteSpace(operationName)) return;
            if (!TryGetDisplayValue(out var value)) return;

            var originalValue = value;

            if (degrees)
            {
                value *= DegreesToRadians;
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

            try
            {
                var result = func(value);

                if (!IsFinite(result))
                {
                    ShowError();
                    return;
                }

                SetDisplayValue(result);
                if (Display == "Error") return;
                _shouldResetDisplay = true;
                RecordOperation($"{operationName}({FormatNumber(originalValue)})", result);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in unary function {OperationName}", operationName);
                ShowError();
            }
        }

        /// <summary>
        /// Feldolgozza a négyzetgyök műveletet.
        /// </summary>
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
            if (Display == "Error") return;
            _shouldResetDisplay = true;
            RecordOperation($"sqrt({FormatNumber(value)})", result);
        }

        /// <summary>
        /// Feldolgozza a faktoriális műveletet.
        /// </summary>
        private void ProcessFactorial()
        {
            if (!TryGetDisplayValue(out var value)) return;

            if (value < 0 || value > MaxFactorial)
            {
                ShowError();
                return;
            }

            if (!IsApproximatelyInteger(value))
            {
                ShowError();
                return;
            }

            try
            {
                var roundedValue = (int)Math.Round(value);

                if (roundedValue < 0 || roundedValue > MaxFactorial)
                {
                    ShowError();
                    return;
                }

                var result = Factorial(roundedValue);
                SetDisplayValue(result);
                if (Display == "Error") return;
                _shouldResetDisplay = true;
                RecordOperation($"{FormatNumber(roundedValue)}!", result);
            }
            catch (OverflowException)
            {
                ShowError();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error in Factorial");
                ShowError();
            }
        }

        /// <summary>
        /// Feldolgozza a memória hozzáadás műveletet.
        /// </summary>
        private void ProcessMemoryAdd()
        {
            if (!TryGetDisplayValue(out var value)) return;

            try
            {
                var newValue = _memoryValue + value;
                if (!IsFinite(newValue))
                {
                    ResetMemory();
                    ShowError();
                    return;
                }

                _memoryValue = newValue;
                TrackMemoryOperation(value, isAddition: true);
                _shouldResetDisplay = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in MemoryAdd");
                ResetMemory();
                ShowError();
            }
        }

        /// <summary>
        /// Feldolgozza a memória kivonás műveletet.
        /// </summary>
        private void ProcessMemorySubtract()
        {
            if (!TryGetDisplayValue(out var value)) return;

            try
            {
                var newValue = _memoryValue - value;
                if (!IsFinite(newValue))
                {
                    ResetMemory();
                    ShowError();
                    return;
                }

                _memoryValue = newValue;
                TrackMemoryOperation(value, isAddition: false);
                _shouldResetDisplay = true;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in MemorySubtract");
                ResetMemory();
                ShowError();
            }
        }

        /// <summary>
        /// Feldolgozza a memória előhívás műveletet.
        /// </summary>
        private void ProcessMemoryRecall()
        {
            SetDisplayValue(_memoryValue);
            if (Display == "Error")
            {
                ResetMemory();
                return;
            }

            _shouldResetDisplay = true;
            _lastOperationDescription = null;
        }

        /// <summary>
        /// Visszaállítja a memória állapotát.
        /// </summary>
        private void ResetMemory()
        {
            _memoryValue = 0;
            _memoryHistoryEntries.Clear();
            _memoryHistoryText = string.Empty;
            UpdateMemoryDisplay();
        }

        /// <summary>
        /// Megnyitja a naplófájlok mappáját.
        /// </summary>
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
                Log.Error(ex, "Failed to open logs folder");
            }
        }

        private static string DefaultLogPathProvider() => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");

        private static void DefaultLogDirectoryOpener(string logPath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = logPath,
                UseShellExecute = true,
                Verb = "open"
            });
        }

        /// <summary>
        /// Megpróbálja lekérni a kijelzőn lévő értéket.
        /// </summary>
        /// <param name="value">A kijelzőn lévő érték.</param>
        /// <returns>Igaz, ha a lekérés sikeres, egyébként hamis.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetDisplayValue(out double value)
        {
            var text = Display;

            if (string.IsNullOrEmpty(text) || text.Length > MaxDisplayLength || text == "Error")
            {
                value = 0;
                return false;
            }

            if (text == "-")
            {
                value = 0;
                return true;
            }

            if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, Culture, out value))
            {
                value = 0;
                return false;
            }

            if (!IsFinite(value))
            {
                value = 0;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Beállítja a kijelzőn lévő értéket.
        /// </summary>
        /// <param name="value">A beállítandó érték.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetDisplayValue(double value)
        {
            var formatted = FormatNumber(value);
            if (formatted == "Error") { ShowError(); return; }

            Display = formatted;
        }

        /// <summary>
        /// Hibát jelez a kijelzőn.
        /// </summary>
        private void ShowError()
        {
            Display = "Error";
            _leftOperand = null;
            _pendingOperator = null;
            _shouldResetDisplay = true;
            _lastOperationDescription = null;
        }

        /// <summary>
        /// Nyomon követi a memóriaműveleteket.
        /// </summary>
        /// <param name="value">A műveletben szereplő érték.</param>
        /// <param name="isAddition">Igaz, ha hozzáadásról van szó, egyébként hamis.</param>
        private void TrackMemoryOperation(double value, bool isAddition)
        {
            var description = _lastOperationDescription ?? FormatNumber(value);
            if (description == "Error")
            {
                description = "0";
            }

            _memoryHistoryEntries.Enqueue((isAddition, description));

            while (_memoryHistoryEntries.Count > MaxMemoryHistoryLength)
            {
                _memoryHistoryEntries.Dequeue();
            }

            UpdateMemoryHistoryText();
            UpdateMemoryDisplay();
        }

        /// <summary>
        /// Frissíti a memóriaelőzmények szövegét.
        /// </summary>
        private void UpdateMemoryHistoryText()
        {
            if (_memoryHistoryEntries.Count == 0)
            {
                _memoryHistoryText = string.Empty;
                return;
            }

            var entryCount = _memoryHistoryEntries.Count;
            var skip = entryCount > MemoryHistoryDisplayCount ? entryCount - MemoryHistoryDisplayCount : 0;
            var expectedItems = entryCount - skip;

            _memoryHistoryBuilder.Clear();
            _memoryHistoryBuilder.EnsureCapacity(Math.Max(_memoryHistoryBuilder.Capacity, expectedItems * 8));

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

        /// <summary>
        /// Rögzíti a végrehajtott műveletet.
        /// </summary>
        /// <param name="description">A művelet leírása.</param>
        /// <param name="result">A művelet eredménye.</param>
        private void RecordOperation(string description, double result)
        {
            var formattedResult = FormatNumber(result);
            if (formattedResult == "Error")
            {
                _lastOperationDescription = null;
                return;
            }

            _lastOperationDescription = $"{description}={formattedResult}";
        }

        /// <summary>
        /// Formáz egy számot a kijelzőn való megjelenítéshez.
        /// </summary>
        /// <param name="value">A formázandó szám.</param>
        /// <returns>A formázott szám.</returns>
        private static string FormatNumber(double value)
        {
            if (!IsFinite(value)) return "Error";
            if (value == 0.0 || Math.Abs(value) < double.Epsilon) return "0";

            Span<char> buffer = stackalloc char[64];
            if (value.TryFormat(buffer, out int written, "G12", Culture))
            {
                var s = new string(buffer[..written]);
                if (s.Contains('E'))
                    return s.Length <= MaxDisplayLength ? s : value.ToString("E6", Culture);

                if (s.Contains('.'))
                {
                    s = s.TrimEnd('0').TrimEnd('.');
                }
                if (s == "-0") return "0";
                if (s.Length > MaxDisplayLength) s = value.ToString("E6", Culture);
                return s.Length > 0 ? s : "0";
            }

            var formatted = value.ToString("G12", Culture);
            if (formatted.Contains('E'))
                return formatted.Length <= MaxDisplayLength ? formatted : value.ToString("E6", Culture);

            if (formatted.Contains('.'))
            {
                formatted = formatted.TrimEnd('0').TrimEnd('.');
            }
            if (formatted == "-0") return "0";
            if (formatted.Length > MaxDisplayLength) formatted = value.ToString("E6", Culture);
            return formatted.Length > 0 ? formatted : "0";
        }

        /// <summary>
        /// Ellenőrzi, hogy egy szám véges-e.
        /// </summary>
        /// <param name="value">Az ellenőrizendő szám.</param>
        /// <returns>Igaz, ha a szám véges, egyébként hamis.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        /// <summary>
        /// Ellen�'rzi, hogy egy �crt�ck k�zel �cszathet�' eg�csnek tekinthet�'-e (lebeg�cszaj toleranci�val).
        /// </summary>
        /// <param name="value">Az ellen�'rizend�' �crt�ck.</param>
        /// <returns>Igaz, ha eg�csnek tekinthet�', egy�cbk�cnt hamis.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsApproximatelyInteger(double value)
        {
            if (!IsFinite(value)) return false;
            var rounded = Math.Round(value);
            return Math.Abs(value - rounded) <= IntegerTolerance;
        }

        /// <summary>
        /// Kiértékel egy műveletet.
        /// </summary>
        /// <param name="left">A bal oldali operandus.</param>
        /// <param name="right">A jobb oldali operandus.</param>
        /// <param name="operatorSymbol">Az operátor.</param>
        /// <returns>A művelet eredménye.</returns>
        private static double Evaluate(double left, double right, string operatorSymbol)
        {
            if (!IsFinite(left) || !IsFinite(right))
            {
                throw new InvalidOperationException("Invalid operand values");
            }

            double result = operatorSymbol switch
            {
                "+" => left + right,
                "-" => left - right,
                "*" => left * right,
                "/" when Math.Abs(right) >= double.Epsilon => left / right,
                "/" => throw new DivideByZeroException(),
                "^" => Math.Pow(left, right),
                _ => throw new InvalidOperationException($"Unknown operator: {operatorSymbol}"),
            };

            if (!IsFinite(result))
            {
                throw new OverflowException("Operation resulted in overflow");
            }
            return result;
        }

        /// <summary>
        /// Létrehozza a faktoriális gyorsítótárat.
        /// </summary>
        /// <returns>A faktoriális gyorsítótár.</returns>
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

        /// <summary>
        /// Kiszámítja egy szám faktoriálisát.
        /// </summary>
        /// <param name="value">A szám, amelynek a faktoriálisát ki kell számítani.</param>
        /// <returns>A faktoriális eredménye.</returns>
        private static double Factorial(int value)
        {
            if (value < 0) throw new OverflowException("Factorial is not defined for negative numbers");
            if (value > MaxFactorial) throw new OverflowException($"Factorial overflow: maximum supported value is {MaxFactorial}");

            var result = _factorialCache[value];
            if (!IsFinite(result))
            {
                throw new OverflowException($"Factorial overflow at {value}!");
            }

            return result;
        }
    }
}