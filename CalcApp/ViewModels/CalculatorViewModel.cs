using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Serilog;

namespace CalcApp.ViewModels
{
    public class CalculatorViewModel : BaseViewModel
    {
        private double? _leftOperand;
        private string? _pendingOperator;
        private bool _shouldResetDisplay;
        private double _memoryValue;
        private readonly Queue<(bool IsAddition, string Description)> _memoryHistoryEntries = new();
        private string _memoryHistoryText = string.Empty;

        private string? _lastOperationDescription;
        private double? _lastRightOperand;
        private string? _lastOperator;

        private string _display = "0";
        public bool IsTurboEnabled { get; private set; }

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

        public ObservableCollection<string> MemoryItems { get; } = new();

        public ICommand DigitCommand { get; }
        public ICommand OperatorCommand { get; }
        public ICommand DecimalCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand SignCommand { get; }
        public ICommand PercentCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand EqualsCommand { get; }
        public ICommand SinCommand { get; }
        public ICommand CosCommand { get; }
        public ICommand TanCommand { get; }
        public ICommand SqrtCommand { get; }
        public ICommand FactorialCommand { get; }
        public ICommand MemoryAddCommand { get; }
        public ICommand MemorySubtractCommand { get; }
        public ICommand MemoryRecallCommand { get; }
        public ICommand MemoryClearCommand { get; }
        public ICommand OpenLogsCommand { get; }

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private static readonly double DegreesToRadians = Math.PI / 180.0;
        private const int MaxFactorial = 170; // 170! fits in double, 171! overflows
        private const int MaxDisplayLength = 64; // protect against extremely long input/overflow UI
        private const int MaxMemoryHistoryLength = 1024; // bound memory history to avoid unbounded growth
        private static readonly double[] _factorialCache = CreateFactorialCache();

        private static readonly Func<double, double> SinFunc = Math.Sin;
        private static readonly Func<double, double> CosFunc = Math.Cos;
        private static readonly Func<double, double> TanFunc = Math.Tan;

        public CalculatorViewModel()
        {
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
            MemoryRecallCommand = new RelayCommand(_ => ProcessMemoryRecall());
            MemoryClearCommand = new RelayCommand(_ => ResetMemory());
            OpenLogsCommand = new RelayCommand(_ => ProcessOpenLogs());

            InitializeMemory();
        }

        public void SetTurboMode(bool isEnabled)
        {
            IsTurboEnabled = isEnabled;
        }

        private void InitializeMemory()
        {
            UpdateMemoryDisplay();
        }

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

                MemoryItems.Clear();
                if (string.IsNullOrEmpty(historyText))
                {
                    MemoryItems.Add($"Memory: {value}");
                }
                else
                {
                    MemoryItems.Add($"Memory total: {value}");
                    MemoryItems.Add($"History: {historyText}");
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error updating memory display");
            }
        }

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

        private void ProcessOperator(string? operatorSymbol)
        {
            if (string.IsNullOrWhiteSpace(operatorSymbol) ||
                (operatorSymbol != "+" && operatorSymbol != "-" && operatorSymbol != "*" && operatorSymbol != "/"))
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

        private void ExecuteOperation(double left, double right, string op)
        {
            try
            {
                var result = IsTurboEnabled ? TryEvaluate(left, right, op) : Evaluate(left, right, op);
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

        private void ResetCalculatorState()
        {
            Display = "0";
            _leftOperand = null;
            _pendingOperator = null;
            _shouldResetDisplay = false;
            _lastOperationDescription = null;
        }

        private void ProcessSign()
        {
            if (!TryGetDisplayValue(out var value)) return;

            value = -value;
            SetDisplayValue(value);
            _lastOperationDescription = null;
        }

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

        private void ProcessFactorial()
        {
            if (!TryGetDisplayValue(out var value)) return;

            if (value < 0 || value > MaxFactorial)
            {
                ShowError();
                return;
            }

            if (Math.Abs(value - Math.Round(value)) > double.Epsilon)
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

        private void ResetMemory()
        {
            _memoryValue = 0;
            _memoryHistoryEntries.Clear();
            _memoryHistoryText = string.Empty;
            UpdateMemoryDisplay();
        }

        private void ProcessOpenLogs()
        {
            try
            {
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                if (!Directory.Exists(logPath))
                {
                    Directory.CreateDirectory(logPath);
                }

                Process.Start(new ProcessStartInfo
                {
                    FileName = logPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to open logs folder");
            }
        }

        private bool TryGetDisplayValue(out double value)
        {
            var text = Display;

            if (string.IsNullOrEmpty(text) || text.Length > MaxDisplayLength || text == "Error")
            {
                value = 0;
                return false;
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

        private void SetDisplayValue(double value)
        {
            var formatted = FormatNumber(value);
            if (formatted == "Error") { ShowError(); return; }

            Display = formatted;
        }

        private void ShowError()
        {
            Display = "Error";
            _leftOperand = null;
            _pendingOperator = null;
            _shouldResetDisplay = true;
            _lastOperationDescription = null;
        }

        private void TrackMemoryOperation(double value, bool isAddition)
        {
            var description = _lastOperationDescription ?? FormatNumber(value);
            if (description == "Error")
            {
                description = "0";
            }

            _memoryHistoryEntries.Enqueue((isAddition, description));
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

            // Simplified logic: Just show the last N items that fit, or just the last few.
            // The previous logic was very complex trying to fit exactly MaxMemoryHistoryLength characters.
            // Let's simplify to just taking the last 20 entries or so, or building it up until it's too long.

            var builder = new StringBuilder();
            var entries = _memoryHistoryEntries.Reverse().Take(50).Reverse().ToList(); // Take last 50

            bool isFirst = true;
            foreach (var entry in entries)
            {
                if (!isFirst) builder.Append("; ");
                if (entry.IsAddition) builder.Append("+ "); else builder.Append("- ");
                builder.Append(entry.Description);
                isFirst = false;
            }

            if (_memoryHistoryEntries.Count > 50)
            {
                builder.Insert(0, "...; ");
            }

            _memoryHistoryText = builder.ToString();
        }

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

        private static string FormatNumber(double value)
        {
            if (!IsFinite(value)) return "Error";
            if (value == 0.0 || Math.Abs(value) < double.Epsilon) return "0";

            Span<char> buffer = stackalloc char[32];
            if (value.TryFormat(buffer, out int written, "G12", Culture))
            {
                var s = new string(buffer[..written]);
                if (s.IndexOf('E') >= 0)
                    return s.Length <= MaxDisplayLength ? s : value.ToString("E6", Culture);

                s = s.TrimEnd('0').TrimEnd('.');
                if (s == "-0") return "0";
                if (s.Length > MaxDisplayLength) s = value.ToString("E6", Culture);
                return s.Length > 0 ? s : "0";
            }

            var formatted = value.ToString("G12", Culture);
            if (formatted.IndexOf('E') >= 0)
                return formatted.Length <= MaxDisplayLength ? formatted : value.ToString("E6", Culture);
            formatted = formatted.TrimEnd('0').TrimEnd('.');
            if (formatted == "-0") return "0";
            if (formatted.Length > MaxDisplayLength) formatted = value.ToString("E6", Culture);
            return formatted.Length > 0 ? formatted : "0";
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double TryEvaluate(double left, double right, string operatorSymbol)
        {
            try
            {
                return Evaluate(left, right, operatorSymbol);
            }
            catch (DivideByZeroException)
            {
                return double.NaN;
            }
            catch (InvalidOperationException)
            {
                return double.NaN;
            }
            catch (OverflowException)
            {
                return double.NaN;
            }
        }

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
                _ => throw new InvalidOperationException($"Unknown operator: {operatorSymbol}"),
            };

            if (!IsFinite(result))
            {
                throw new OverflowException("Operation resulted in overflow");
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
