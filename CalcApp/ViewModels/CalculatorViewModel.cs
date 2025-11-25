using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Input;

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

        private string _display = "0";
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
            MemoryClearCommand = new RelayCommand(_ => ResetMemory());

            InitializeMemory();
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
                System.Diagnostics.Debug.WriteLine($"Error updating memory display: {ex}");
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
                System.Diagnostics.Debug.WriteLine($"Unexpected error in ExecuteOperation: {ex}");
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
            value /= 100.0;
            if (!IsFinite(value))
            {
                ShowError();
                return;
            }

            SetDisplayValue(value);
            if (Display == "Error") return;
            _shouldResetDisplay = true;
            RecordOperation($"{FormatNumber(originalValue)}%", value);
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
            if (!_leftOperand.HasValue || _pendingOperator is null) return;
            if (!TryGetDisplayValue(out var rightOperand)) return;

            ExecuteOperation(_leftOperand.Value, rightOperand, _pendingOperator);
            
            // After equals, we clear the pending operator and left operand effectively starting fresh with the result
            // But ExecuteOperation sets _leftOperand to result. 
            // Standard calculator behavior: 
            // 5 + 3 = 8. 
            // If I type + 2, it does 8 + 2.
            // If I type 5, it starts new.
            // So _leftOperand = result is correct for chaining, but for Equals we might want to clear _pendingOperator.
            
            _pendingOperator = null;
            _shouldResetDisplay = true;
            
            // Note: ExecuteOperation sets _leftOperand = result. 
            // If we want to support "Repeat last operation" (e.g. pressing = again), we'd need more state.
            // For now, we just match previous logic which cleared them.
            // Wait, previous logic:
            // _leftOperand = null;
            // _pendingOperator = null;
            
            // So I should reset _leftOperand to null if I want to match exactly, OR keep it as result.
            // Previous code:
            // _leftOperand = null;
            // _pendingOperator = null;
            
            // But ExecuteOperation sets _leftOperand = result.
            // Let's override it.
            _leftOperand = null;
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
                System.Diagnostics.Debug.WriteLine($"Error in unary function {operationName}: {ex}");
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
                System.Diagnostics.Debug.WriteLine($"Unexpected error in Factorial: {ex}");
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
                System.Diagnostics.Debug.WriteLine($"Error in MemoryAdd: {ex}");
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
                System.Diagnostics.Debug.WriteLine($"Error in MemorySubtract: {ex}");
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

        private static double Evaluate(double left, double right, string operatorSymbol)
        {
            if (!IsFinite(left) || !IsFinite(right))
            {
                throw new InvalidOperationException("Invalid operand values");
            }

            if (operatorSymbol == "+")
            {
                var result = left + right;
                if (!IsFinite(result)) throw new OverflowException("Addition overflow");
                return result;
            }
            if (operatorSymbol == "-")
            {
                var result = left - right;
                if (!IsFinite(result)) throw new OverflowException("Subtraction overflow");
                return result;
            }
            if (operatorSymbol == "*")
            {
                var result = left * right;
                if (!IsFinite(result)) throw new OverflowException("Multiplication overflow");
                return result;
            }
            if (operatorSymbol == "/")
            {
                if (Math.Abs(right) < double.Epsilon) throw new DivideByZeroException();
                var result = left / right;
                if (!IsFinite(result)) throw new OverflowException("Division overflow");
                return result;
            }
            
            throw new InvalidOperationException($"Unknown operator: {operatorSymbol}");
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
