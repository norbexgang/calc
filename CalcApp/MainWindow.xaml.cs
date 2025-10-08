using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace CalcApp
{
    public partial class MainWindow : Window
    {
        private double? _leftOperand;
        private string? _pendingOperator;
        private bool _shouldResetDisplay;
        private double _memoryValue;
        private readonly StringBuilder _memoryHistoryBuilder = new();
        private string? _lastOperationDescription;

        private TextBox? _display;
        private ListBox? _memoryList;
        private Button? _themeToggle;
        private bool _isDarkMode = true; // Start with dark mode
        private bool _isAnimating = false;
        private readonly ResourceDictionary _darkThemeDictionary = CreateThemeDictionary(DarkThemePath);
        private readonly ResourceDictionary _lightThemeDictionary = CreateThemeDictionary(LightThemePath);
        private int _themeDictionaryIndex = -1;
        private readonly Stack<(double? LeftOperand, string? PendingOperator)> _operationStack = new();

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private static readonly double DegreesToRadians = Math.PI / 180.0;
    private const int MaxFactorial = 170; // 170! fits in double, 171! overflows
    private const int MaxDisplayLength = 64; // protect against extremely long input/overflow UI
    private const int MaxMemoryHistoryLength = 1024; // bound memory history to avoid unbounded growth
        private const string DarkThemePath = "Themes/MaterialTheme.xaml";
        private const string LightThemePath = "Themes/ClassicTheme.xaml";
        private static int _maxComputedFactorial;
        private static readonly double[] _factorialCache = new double[MaxFactorial + 1];
        private static readonly object _factorialLock = new();

        static MainWindow()
        {
            // initialize factorial cache with sentinel (-1)
            for (var i = 0; i <= MaxFactorial; i++) _factorialCache[i] = -1.0;
            _factorialCache[0] = 1.0;
            _factorialCache[1] = 1.0;
            _maxComputedFactorial = 1;
        }

        public MainWindow()
        {
            LoadComponentFromXaml();
            // cache frequently used controls once window is loaded to avoid repeated FindName lookups
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            InitializeMemory();
            InitializeTheme();
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Try to cache controls to avoid repeated runtime lookups which allocate
            if (_display == null && FindName("Display") is TextBox tb) _display = tb;
            if (_memoryList == null && FindName("MemoryList") is ListBox lb) _memoryList = lb;
            if (_themeToggle == null && FindName("ThemeToggle") is Button btn) _themeToggle = btn;
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            // Clear references so the Window can be fully collected and to avoid holding UI elements longer than needed
            Loaded -= OnLoaded;
            Unloaded -= OnUnloaded;

            if (_themeToggle != null)
            {
                // Try to safely detach known handlers if they were attached from code
                try
                {
                    _themeToggle.Click -= ThemeToggle_Click;
                }
                catch { }
            }

            _display = null;
            _memoryList = null;
            _themeToggle = null;
        }

        private void LoadComponentFromXaml()
        {
            // Manually load the XAML to work around designer not seeing InitializeComponent.
            try
            {
                var uri = new Uri("/CalcApp;component/MainWindow.xaml", UriKind.Relative);
                Application.LoadComponent(this, uri);
            }
            catch (Exception ex)
            {
                // Fail fast on UI load error - show message and stop application to avoid running in an inconsistent state
                System.Diagnostics.Debug.WriteLine($"Failed to load main window XAML: {ex}");
                MessageBox.Show("Failed to initialize application UI. The application will exit.", "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current?.Shutdown();
            }
        }

        private TextBox DisplayBox => _display ??= FindRequiredControl<TextBox>("Display");

        private ListBox MemoryListBox => _memoryList ??= FindRequiredControl<ListBox>("MemoryList");

        private Button ThemeToggleButton => _themeToggle ??= FindRequiredControl<Button>("ThemeToggle");

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
            if (sender is not Button button) return;
            var digit = button.Content?.ToString() ?? string.Empty;
            ProcessDigit(digit);
        }

        private void Decimal_Click(object sender, RoutedEventArgs e)
        {
            ProcessDecimal();
        }

        private void OpenParenthesis_Click(object sender, RoutedEventArgs e)
        {
            ProcessOpenParenthesis();
        }

        private void CloseParenthesis_Click(object sender, RoutedEventArgs e)
        {
            ProcessCloseParenthesis();
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            ResetCalculatorState();
        }

        private void Sign_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value)) return;

            value = -value;
            SetDisplayValue(value);
            _lastOperationDescription = null;
        }

        private void Percent_Click(object sender, RoutedEventArgs e)
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
            if (DisplayBox.Text == "Error") return;
            _shouldResetDisplay = true;
            RecordOperation($"{FormatNumber(originalValue)}%", value);
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            ProcessDelete();
        }

        private void Operator_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var operatorSymbol = button.Tag?.ToString() ?? button.Content?.ToString();
            if (string.IsNullOrWhiteSpace(operatorSymbol)) return;
            ProcessOperator(operatorSymbol);
        }

        private void Equals_Click(object sender, RoutedEventArgs e)
        {
            ProcessEquals();
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
            if (DisplayBox.Text == "Error") return;
            _shouldResetDisplay = true;
            RecordOperation($"sqrt({FormatNumber(value)})", result);
        }

        private void Factorial_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value)) return;

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
                if (DisplayBox.Text == "Error") return;
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
            if (!TryGetDisplayValue(out var value)) return;

            _memoryValue += value;
            if (!IsFinite(_memoryValue))
            {
                ResetMemory();
                ShowError();
                return;
            }

            TrackMemoryOperation(value, isAddition: true);
            _shouldResetDisplay = true;
        }

        private void MemorySubtract_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value)) return;

            _memoryValue -= value;
            if (!IsFinite(_memoryValue))
            {
                ResetMemory();
                ShowError();
                return;
            }

            TrackMemoryOperation(value, isAddition: false);
            _shouldResetDisplay = true;
        }

        private void MemoryRecall_Click(object sender, RoutedEventArgs e)
        {
            SetDisplayValue(_memoryValue);
            if (DisplayBox.Text == "Error")
            {
                ResetMemory();
                return;
            }

            _shouldResetDisplay = true;
            _lastOperationDescription = null;
        }

        private void MemoryClear_Click(object sender, RoutedEventArgs e)
        {
            ResetMemory();
        }

        private void ResetMemory()
        {
            _memoryValue = 0;
            _memoryHistoryBuilder.Clear();
            UpdateMemoryDisplay();
        }

        private void ApplyUnaryFunction(Func<double, double> func, string operationName, bool degrees = false, bool validateTan = false)
        {
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

            var result = func(value);
            if (!IsFinite(result))
            {
                ShowError();
                return;
            }

            SetDisplayValue(result);
            if (DisplayBox.Text == "Error") return;
            _shouldResetDisplay = true;
            RecordOperation($"{operationName}({FormatNumber(originalValue)})", result);
        }

        private static double Factorial(int value)
        {
            if (value < 0) throw new OverflowException();
            if (value > MaxFactorial) throw new OverflowException();

            var cache = _factorialCache;
            var cached = cache[value];
            if (cached >= 0) return cached;

            lock (_factorialLock)
            {
                // double-check
                cached = cache[value];
                if (cached >= 0) return cached;

                var start = _maxComputedFactorial;
                if (start > value)
                {
                    start = value;
                }

                double result = cache[start];
                for (var i = start + 1; i <= value; i++)
                {
                    result *= i;
                    if (double.IsInfinity(result) || double.IsNaN(result))
                    {
                        throw new OverflowException();
                    }

                    cache[i] = result;
                }

                if (value > _maxComputedFactorial)
                {
                    _maxComputedFactorial = value;
                }

                return cache[value];
            }
        }

        private void ResetCalculatorState()
        {
            DisplayBox.Text = "0";
            _leftOperand = null;
            _pendingOperator = null;
            _shouldResetDisplay = false;
            _lastOperationDescription = null;
            _operationStack.Clear();
        }

        private bool TryGetDisplayValue(out double value)
        {
            var text = DisplayBox.Text;
            if (text == "Error")
            {
                value = 0;
                return false;
            }

            if (!double.TryParse(text, NumberStyles.Float, Culture, out value))
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
            // FormatNumber performs the finite check and returns "Error" for invalid values.
            var formatted = FormatNumber(value);
            if (formatted == "Error") { ShowError(); return; }

            DisplayBox.Text = formatted;
        }

        private void ShowError()
        {
            var d = DisplayBox;
            d.Text = "Error";
            _leftOperand = null;
            _pendingOperator = null;
            _shouldResetDisplay = true;
            _lastOperationDescription = null;
            _operationStack.Clear();
        }

        private void InitializeMemory()
        {
            UpdateMemoryDisplay();
        }

        private void UpdateMemoryDisplay()
        {
            var items = MemoryListBox.Items;
            var value = FormatNumber(_memoryValue);
            if (value == "Error")
            {
                value = "0";
            }
            // ensure memory value and history are bounded (operate in-place on StringBuilder to avoid extra allocations)
            if (_memoryHistoryBuilder.Length > MaxMemoryHistoryLength)
            {
                _memoryHistoryBuilder.Remove(0, _memoryHistoryBuilder.Length - MaxMemoryHistoryLength);
            }

            var history = _memoryHistoryBuilder.Length == 0 ? string.Empty : _memoryHistoryBuilder.ToString();
            var displayText = history.Length == 0 ? $"Memory: {value}" : $"Memory: {history} (Total: {value})";

            if (items.Count == 0) items.Add(displayText); else items[0] = displayText;
        }

        private void TrackMemoryOperation(double value, bool isAddition)
        {
            var description = _lastOperationDescription ?? FormatNumber(value);
            if (description == "Error")
            {
                description = "0";
            }

            var builder = _memoryHistoryBuilder;

            if (builder.Length == 0)
            {
                if (!isAddition) builder.Append("- ");
                builder.Append(description);
            }
            else
            {
                builder.Append("; ");
                builder.Append(isAddition ? "+ " : "- ");
                builder.Append(description);
            }

            // Bound the history to avoid unbounded memory growth - trim from the start in-place
            if (builder.Length > MaxMemoryHistoryLength)
            {
                builder.Remove(0, builder.Length - MaxMemoryHistoryLength);
            }

            UpdateMemoryDisplay();
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
            if (!IsFinite(value))
            {
                return "Error";
            }

            var formatted = value.ToString("G12", Culture);
            if (formatted.Contains('E', StringComparison.Ordinal))
            {
                return formatted.Length <= MaxDisplayLength ? formatted : value.ToString("E6", Culture);
            }

            formatted = formatted.TrimEnd('0').TrimEnd('.');
            if (formatted == "-0")
            {
                formatted = "0";
            }

            if (formatted.Length > MaxDisplayLength)
            {
                formatted = value.ToString("E6", Culture);
            }

            return string.IsNullOrEmpty(formatted) ? "0" : formatted;
        }

        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

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

        private void InitializeTheme()
        {
            UpdateThemeToggleButton();
        }

        private async void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (_isAnimating) return; // Prevent multiple clicks during animation

            _isAnimating = true;
            var previousMode = _isDarkMode;
            var nextMode = !previousMode;

            try
            {
                await AnimateButtonClick().ConfigureAwait(true);
                await FadeOutWindow().ConfigureAwait(true);

                _isDarkMode = nextMode;
                ApplyTheme();
                UpdateThemeToggleButton();

                await FadeInWindow().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Theme toggle failed: {ex}");
                _isDarkMode = previousMode;
                try
                {
                    ApplyTheme();
                    UpdateThemeToggleButton();
                }
                catch (Exception innerEx)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to restore previous theme: {innerEx}");
                }

                Opacity = 1.0;
                MessageBox.Show("Theme switch failed. The previous theme has been restored.", "Theme Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _isAnimating = false;
                Opacity = 1.0;
            }
        }

        private void ApplyTheme()
        {
            var dictionaries = Resources.MergedDictionaries;
            var targetDictionary = _isDarkMode ? _darkThemeDictionary : _lightThemeDictionary;

            if (_themeDictionaryIndex < 0 || _themeDictionaryIndex >= dictionaries.Count)
            {
                _themeDictionaryIndex = FindThemeDictionaryIndex(dictionaries);
                if (_themeDictionaryIndex < 0)
                {
                    dictionaries.Add(targetDictionary);
                    _themeDictionaryIndex = dictionaries.Count - 1;
                    return;
                }
            }

            if (!ReferenceEquals(dictionaries[_themeDictionaryIndex], targetDictionary))
            {
                dictionaries[_themeDictionaryIndex] = targetDictionary;
            }
        }

        private void UpdateThemeToggleButton()
        {
            var button = ThemeToggleButton;
            button.Content = _isDarkMode ? "Light mode" : "Dark mode";
        }

        // --- Keyboard / shared processing helpers ---
        private void ProcessDigit(string digit)
        {
            _lastOperationDescription = null;
            var currentText = DisplayBox.Text;
            if (_shouldResetDisplay || currentText is "0" or "Error")
            {
                DisplayBox.Text = digit.Length <= MaxDisplayLength ? digit : digit[..MaxDisplayLength];
            }
            else
            {
                if (currentText.Length + digit.Length <= MaxDisplayLength)
                    DisplayBox.Text = currentText + digit;
                // else ignore additional input to prevent uncontrolled growth
            }

            _shouldResetDisplay = false;
        }

        private void ProcessDecimal()
        {
            var currentText = DisplayBox.Text;
            if (_shouldResetDisplay || currentText == "Error")
            {
                DisplayBox.Text = "0.";
                _shouldResetDisplay = false;
                _lastOperationDescription = null;
                return;
            }

            if (!currentText.Contains('.') && currentText.Length + 1 <= MaxDisplayLength)
            {
                DisplayBox.Text = currentText + ".";
                _lastOperationDescription = null;
            }
        }

        private void ProcessOpenParenthesis()
        {
            if (DisplayBox.Text == "Error")
            {
                DisplayBox.Text = "0";
            }

            _operationStack.Push((_leftOperand, _pendingOperator));
            _leftOperand = null;
            _pendingOperator = null;
            DisplayBox.Text = "0";
            _shouldResetDisplay = true;
            _lastOperationDescription = null;
        }

        private void ProcessCloseParenthesis()
        {
            if (_operationStack.Count == 0)
            {
                return;
            }

            if (!TryResolvePendingOperation())
            {
                _operationStack.Clear();
                return;
            }

            if (!TryGetDisplayValue(out _))
            {
                return;
            }

            var context = _operationStack.Pop();
            _leftOperand = context.LeftOperand;
            _pendingOperator = context.PendingOperator;
            _shouldResetDisplay = _pendingOperator is null;
            _lastOperationDescription = null;
        }

        private void ProcessDelete()
        {
            var currentText = DisplayBox.Text;
            if (_shouldResetDisplay || currentText == "Error")
            {
                DisplayBox.Text = "0";
                _shouldResetDisplay = false;
                _lastOperationDescription = null;
                return;
            }
            DisplayBox.Text = currentText.Length <= 1 ? "0" : currentText[..^1];
            _lastOperationDescription = null;
        }

        private void ProcessOperator(string operatorSymbol)
        {
            if (string.IsNullOrWhiteSpace(operatorSymbol)) return;
            if (!TryGetDisplayValue(out var currentValue)) return;

            if (_leftOperand.HasValue && _pendingOperator is not null && !_shouldResetDisplay)
            {
                var leftOperand = _leftOperand.Value;
                var pendingOp = _pendingOperator;
                try
                {
                    var result = Evaluate(leftOperand, currentValue, pendingOp);
                    if (!IsFinite(result))
                    {
                        ShowError();
                        return;
                    }

                    _leftOperand = result;
                    SetDisplayValue(result);
                    if (DisplayBox.Text == "Error") return;
                    RecordOperation($"{FormatNumber(leftOperand)}{pendingOp}{FormatNumber(currentValue)}", result);
                }
                catch (DivideByZeroException)
                {
                    ShowError();
                    return;
                }
                catch (InvalidOperationException)
                {
                    ShowError();
                    return;
                }
            }
            else
            {
                _leftOperand = currentValue;
            }

            _pendingOperator = operatorSymbol;
            _shouldResetDisplay = true;
        }

        private void ProcessEquals()
        {
            while (_operationStack.Count > 0)
            {
                ProcessCloseParenthesis();
                if (DisplayBox.Text == "Error")
                {
                    return;
                }
            }

            if (!_leftOperand.HasValue || _pendingOperator is null) return;
            if (!TryGetDisplayValue(out var rightOperand)) return;

            try
            {
                var leftOperand = _leftOperand.Value;
                var pendingOperator = _pendingOperator!;
                var result = Evaluate(leftOperand, rightOperand, pendingOperator);
                if (!IsFinite(result))
                {
                    ShowError();
                    return;
                }

                SetDisplayValue(result);
                if (DisplayBox.Text == "Error") return;
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

        private bool TryResolvePendingOperation()
        {
            if (_pendingOperator is null || !_leftOperand.HasValue)
            {
                return TryGetDisplayValue(out _);
            }

            if (!TryGetDisplayValue(out var rightOperand)) return false;

            try
            {
                var leftOperand = _leftOperand.Value;
                var pendingOperator = _pendingOperator!;
                var result = Evaluate(leftOperand, rightOperand, pendingOperator);
                if (!IsFinite(result))
                {
                    ShowError();
                    return false;
                }

                SetDisplayValue(result);
                if (DisplayBox.Text == "Error") return false;
                RecordOperation($"{FormatNumber(leftOperand)}{pendingOperator}{FormatNumber(rightOperand)}", result);
                _leftOperand = null;
                _pendingOperator = null;
                _shouldResetDisplay = false;
                return true;
            }
            catch (DivideByZeroException)
            {
                ShowError();
                return false;
            }
            catch (InvalidOperationException)
            {
                ShowError();
                return false;
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Digits
            if (Keyboard.Modifiers == ModifierKeys.None && e.Key >= Key.D0 && e.Key <= Key.D9)
            {
                var ch = (char)('0' + (e.Key - Key.D0));
                ProcessDigit(ch.ToString());
                e.Handled = true;
                return;
            }

            if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9)
            {
                var ch = (char)('0' + (e.Key - Key.NumPad0));
                ProcessDigit(ch.ToString());
                e.Handled = true;
                return;
            }

            // Operators and control keys
            switch (e.Key)
            {
                case Key.Add:
                case Key.OemPlus when Keyboard.Modifiers == ModifierKeys.None:
                    ProcessOperator("+");
                    e.Handled = true;
                    break;
                case Key.Subtract:
                case Key.OemMinus:
                    ProcessOperator("-");
                    e.Handled = true;
                    break;
                case Key.Multiply:
                    ProcessOperator("*");
                    e.Handled = true;
                    break;
                case Key.Divide:
                case Key.Oem2: // '/'
                    ProcessOperator("/");
                    e.Handled = true;
                    break;
                case Key.D9 when Keyboard.Modifiers == ModifierKeys.Shift:
                case Key.OemOpenBrackets when Keyboard.Modifiers == ModifierKeys.Shift:
                    ProcessOpenParenthesis();
                    e.Handled = true;
                    break;
                case Key.D0 when Keyboard.Modifiers == ModifierKeys.Shift:
                    ProcessCloseParenthesis();
                    e.Handled = true;
                    break;
                case Key.Decimal:
                case Key.OemPeriod:
                    ProcessDecimal();
                    e.Handled = true;
                    break;
                case Key.Return:
                    ProcessEquals();
                    e.Handled = true;
                    break;
                case Key.Back:
                    ProcessDelete();
                    e.Handled = true;
                    break;
                case Key.Escape:
                    ResetCalculatorState();
                    e.Handled = true;
                    break;
                case Key.Oem5: // percent? fallback
                    // fallback: '%'
                    Percent_Click(this, new RoutedEventArgs());
                    e.Handled = true;
                    break;
            }
        }

        private async Task AnimateButtonClick()
        {
            var button = ThemeToggleButton;
            // Ensure a ScaleTransform exists (avoid null and extra returns)
            if (button.RenderTransform is not System.Windows.Media.ScaleTransform scaleTransform)
            {
                scaleTransform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                button.RenderTransform = scaleTransform;
            }

            var storyboard = new Storyboard();

            var scaleXDown = new DoubleAnimation(1.0, 0.95, TimeSpan.FromMilliseconds(100));
            var scaleYDown = new DoubleAnimation(1.0, 0.95, TimeSpan.FromMilliseconds(100));
            var scaleXUp = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(100)) { BeginTime = TimeSpan.FromMilliseconds(100) };
            var scaleYUp = new DoubleAnimation(0.95, 1.0, TimeSpan.FromMilliseconds(100)) { BeginTime = TimeSpan.FromMilliseconds(100) };

            Storyboard.SetTarget(scaleXDown, scaleTransform);
            Storyboard.SetTargetProperty(scaleXDown, new PropertyPath("ScaleX"));
            Storyboard.SetTarget(scaleYDown, scaleTransform);
            Storyboard.SetTargetProperty(scaleYDown, new PropertyPath("ScaleY"));
            Storyboard.SetTarget(scaleXUp, scaleTransform);
            Storyboard.SetTargetProperty(scaleXUp, new PropertyPath("ScaleX"));
            Storyboard.SetTarget(scaleYUp, scaleTransform);
            Storyboard.SetTargetProperty(scaleYUp, new PropertyPath("ScaleY"));

            storyboard.Children.Add(scaleXDown);
            storyboard.Children.Add(scaleYDown);
            storyboard.Children.Add(scaleXUp);
            storyboard.Children.Add(scaleYUp);

            var tcs = new TaskCompletionSource<bool>();
            EventHandler handler = null!;
            handler = (s, e) =>
            {
                try { storyboard.Completed -= handler; } catch { }
                tcs.TrySetResult(true);
            };

            storyboard.Completed += handler;

            storyboard.Begin();
            await tcs.Task.ConfigureAwait(true);
        }

        private async Task FadeOutWindow()
        {
            await FadeOpacity(1.0, 0.3, TimeSpan.FromMilliseconds(250), new QuadraticEase { EasingMode = EasingMode.EaseOut });
        }

        private async Task FadeInWindow()
        {
            await FadeOpacity(0.3, 1.0, TimeSpan.FromMilliseconds(250), new QuadraticEase { EasingMode = EasingMode.EaseIn });
        }

        private async Task FadeOpacity(double from, double to, TimeSpan duration, IEasingFunction? easing = null)
        {
            var animation = new DoubleAnimation(from, to, duration) { EasingFunction = easing };
            var storyboard = new Storyboard();
            Storyboard.SetTarget(animation, this);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Opacity"));
            storyboard.Children.Add(animation);

            var tcs = new TaskCompletionSource<bool>();
            EventHandler handler = null!;
            handler = (s, e) =>
            {
                try { storyboard.Completed -= handler; } catch { }
                tcs.TrySetResult(true);
            };

            storyboard.Completed += handler;
            storyboard.Begin();
            await tcs.Task.ConfigureAwait(true);
        }

        private static ResourceDictionary CreateThemeDictionary(string relativePath)
        {
            try
            {
                return new ResourceDictionary { Source = new Uri(relativePath, UriKind.Relative) };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load theme dictionary '{relativePath}': {ex}");
                return new ResourceDictionary();
            }
        }

        private static int FindThemeDictionaryIndex(IList<ResourceDictionary> dictionaries)
        {
            for (var i = 0; i < dictionaries.Count; i++)
            {
                var source = dictionaries[i].Source?.OriginalString;
                if (string.Equals(source, DarkThemePath, StringComparison.Ordinal) ||
                    string.Equals(source, LightThemePath, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
