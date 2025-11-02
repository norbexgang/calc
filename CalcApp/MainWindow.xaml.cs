using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Speech.Recognition;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Runtime.CompilerServices;
using System.Linq;

namespace CalcApp
{
    public partial class MainWindow : Window
    {
        private double? _leftOperand;
        private string? _pendingOperator;
        private bool _shouldResetDisplay;
        private double _memoryValue;
        private readonly Queue<(bool IsAddition, string Description)> _memoryHistoryEntries = new();
        private string _memoryHistoryText = string.Empty;
        private string? _lastOperationDescription;

        private TextBox? _display;
        private ListBox? _memoryList;
        private Button? _themeToggle;
        private bool _animationsEnabled = true;
        // Tracks whether a theme animation is currently running (used to avoid overlapping animations)
        private bool _isAnimating = false;
        // Current theme state: true = dark mode, false = light mode
        private bool _isDarkMode = false;
        private readonly ResourceDictionary _darkThemeDictionary = CreateThemeDictionary(DarkThemePath);
        private readonly ResourceDictionary _lightThemeDictionary = CreateThemeDictionary(LightThemePath);
        private int _themeDictionaryIndex = -1;
        // Removed: _operationStack - parenthesis support removed for simplification

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;
        private static readonly double DegreesToRadians = Math.PI / 180.0;
        private const int MaxFactorial = 170; // 170! fits in double, 171! overflows
        private const int MaxDisplayLength = 64; // protect against extremely long input/overflow UI
        private const int MaxMemoryHistoryLength = 1024; // bound memory history to avoid unbounded growth
        private const string DarkThemePath = "Themes/MaterialTheme.xaml";
        private const string LightThemePath = "Themes/ClassicTheme.xaml";
        private static readonly double[] _factorialCache = CreateFactorialCache();

        // --- OPTIMALIZÁLÁS: static readonly delegate-k trigonometrikus függvényekhez ---
        private static readonly Func<double, double> SinFunc = Math.Sin;
        private static readonly Func<double, double> CosFunc = Math.Cos;
        private static readonly Func<double, double> TanFunc = Math.Tan;

        // Cached storyboards to reduce per-click allocations
        private Storyboard? _cachedButtonClickStoryboard;
        private Storyboard? _cachedFadeStoryboard;
    // Speech recognition controller (nullable; initialized on construction)
    private SpeechControl? _speech;
    // Whether user has enabled speech (persisted setting could be used later)
    private bool _speechEnabled = true;

        public MainWindow()
        {
            LoadComponentFromXaml();
            // cache frequently used controls once window is loaded to avoid repeated FindName lookups
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            InitializeMemory();
            InitializeTheme();
            FreezeResourceDictionaries();

            // Initialize speech control only if enabled and a Hungarian recognizer exists
            try
            {
                // Ensure the newly added SpeechToggle can be found (loaded by LoadComponentFromXaml)
                var hasRecognizer = HasHungarianRecognizer();
                // If the toggle exists in XAML, set its initial state and content
                if (FindName("SpeechToggle") is ToggleButton tb)
                {
                    tb.IsChecked = _speechEnabled && hasRecognizer;
                    tb.Content = tb.IsChecked == true ? "🎤 Beszéd: Be" : "🎤 Beszéd: Ki";
                }

                if (_speechEnabled && hasRecognizer)
                {
                    _speech = new SpeechControl(this);
                }
                else if (!hasRecognizer)
                {
                    System.Diagnostics.Debug.WriteLine("No Hungarian speech recognizer installed; speech control disabled.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Speech init failed in MainWindow ctor: {ex}");
            }
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

#if DEBUG
            if (_themeToggle != null && FindName("ThemeToggle") is Button btn)
            {
                _themeToggle = btn;
                _cachedButtonClickStoryboard ??= TryFindResource("ButtonClickAnimation") as Storyboard;
                _cachedFadeStoryboard ??= TryFindResource("FadeOutAnimation") as Storyboard;

                // Try to safely detach known handlers if they were attached from code
                try
                {
                    _themeToggle.Click -= ThemeToggle_Click;
                }
                catch { }
            }
#endif
            // Dispose speech control if present
            try { _speech?.Dispose(); } catch { }
            _speech = null;

            _display = null;
            _memoryList = null;
            _themeToggle = null;
        }

        private void LoadComponentFromXaml()
        {
            // Performance & Security: Manually load the XAML with proper error handling
            try
            {
                var uri = new Uri("/CalcApp;component/MainWindow.xaml", UriKind.Relative);
                Application.LoadComponent(this, uri);
            }
            catch (Exception ex)
            {
                // Security: fail fast on UI load error to avoid running in an inconsistent state
                System.Diagnostics.Debug.WriteLine($"CRITICAL: Failed to load main window XAML: {ex}");

                try
                {
                    MessageBox.Show(
                        "Failed to initialize application UI. The application will exit.",
                        "Initialization Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
                catch
                {
                    // If MessageBox fails, just write to debug
                    System.Diagnostics.Debug.WriteLine("Failed to show error message box");
                }

                Application.Current?.Shutdown();
                Environment.Exit(1); // Force exit if shutdown doesn't work
            }
        }

        // UI handlers for the speech toggle
        private void SpeechToggle_Checked(object sender, RoutedEventArgs e)
        {
            EnableSpeech(true);
            if (sender is ToggleButton tb) tb.Content = "🎤 Beszéd: Be";
        }

        private void SpeechToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            EnableSpeech(false);
            if (sender is ToggleButton tb) tb.Content = "🎤 Beszéd: Ki";
        }

        private void EnableSpeech(bool enable)
        {
            _speechEnabled = enable;
            if (enable)
            {
                if (_speech != null) return; // already enabled
                if (!HasHungarianRecognizer())
                {
                    MessageBox.Show("Nincs telepítve magyar beszédfelismerő; a beszédvezérlés nem elérhető.", "Beszédvezérlés", MessageBoxButton.OK, MessageBoxImage.Information);
                    // Update toggle UI if present
                    if (FindName("SpeechToggle") is ToggleButton tb) { tb.IsChecked = false; tb.Content = "🎤 Beszéd: Ki"; }
                    _speechEnabled = false;
                    return;
                }

                try
                {
                    _speech = new SpeechControl(this);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start speech control: {ex}");
                    MessageBox.Show("A beszédvezérlés indítása nem sikerült.", "Beszédvezérlés hiba", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            else
            {
                try { _speech?.Dispose(); } catch { }
                _speech = null;
            }
        }

        // Check if a Hungarian recognizer is installed
        private static bool HasHungarianRecognizer()
        {
            try
            {
                var culture = new System.Globalization.CultureInfo("hu-HU");
                var recognizerInfo = SpeechRecognitionEngine.InstalledRecognizers()
                    .FirstOrDefault(r => r.Culture.Equals(culture));
                return recognizerInfo != null;
            }
            catch
            {
                return false;
            }
        }

        private TextBox DisplayBox => _display ??= FindRequiredControl<TextBox>("Display");

        private ListBox MemoryListBox => _memoryList ??= FindRequiredControl<ListBox>("MemoryList");

        private Button ThemeToggleButton => _themeToggle ??= FindRequiredControl<Button>("ThemeToggle");

        private void FreezeResourceDictionaries()
        {
            try
            {
                foreach (var dict in Resources.MergedDictionaries)
                {
                    foreach (var key in dict.Keys)
                    {
                        if (dict[key] is System.Windows.Freezable f && f.CanFreeze)
                        {
                            f.Freeze();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Freeze resources failed: {ex}");
            }
        }


        private T FindRequiredControl<T>(string name) where T : class
        {
            if (FindName(name) is T control)
            {
                return control;
            }

            throw new InvalidOperationException($"Could not find control '{name}'.");
        }

        private void Digit_Click(object sender,
                                 RoutedEventArgs e)
        {
            if (sender is not Button button) return;
            var digit = button.Tag?.ToString();
            if (string.IsNullOrEmpty(digit) || digit.Length != 1) return;
            if (!char.IsDigit(digit[0])) return;
            ProcessDigit(digit);

        }

        private void Decimal_Click(object sender, RoutedEventArgs e)
        {
            ProcessDecimal();
        }

        // Removed: OpenParenthesis_Click and CloseParenthesis_Click - parenthesis support removed

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
            // Security: validate sender and operator symbol
            if (sender is not Button button) return;
            var operatorSymbol = button.Tag?.ToString() ?? button.Content?.ToString();
            if (string.IsNullOrWhiteSpace(operatorSymbol)) return;

            // Security: whitelist validation - only allow known operators
            if (operatorSymbol != "+" && operatorSymbol != "-" &&
                operatorSymbol != "*" && operatorSymbol != "/")
            {
                return;
            }

            ProcessOperator(operatorSymbol);
        }

        private void Equals_Click(object sender, RoutedEventArgs e)
        {
            ProcessEquals();
        }

        private void Sin_Click(object sender, RoutedEventArgs e)
        {
            ApplyUnaryFunction(SinFunc, "sin", degrees: true);
        }

        private void Cos_Click(object sender, RoutedEventArgs e)
        {
            ApplyUnaryFunction(CosFunc, "cos", degrees: true);
        }

        private void Tan_Click(object sender, RoutedEventArgs e)
        {
            ApplyUnaryFunction(TanFunc, "tan", degrees: true, validateTan: true);
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

            // Security: validate input range
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

                // Security: double-check bounds before calculation
                if (roundedValue < 0 || roundedValue > MaxFactorial)
                {
                    ShowError();
                    return;
                }

                // Factorial lookup is O(1) thanks to the precomputed cache, so run synchronously
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
            catch (Exception ex)
            {
                // Security: catch any unexpected exceptions
                System.Diagnostics.Debug.WriteLine($"Unexpected error in Factorial: {ex}");
                ShowError();
            }
        }

        private void MemoryAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value)) return;

            try
            {
                // Security: check for overflow before adding
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
                // Security: handle unexpected errors
                System.Diagnostics.Debug.WriteLine($"Error in MemoryAdd: {ex}");
                ResetMemory();
                ShowError();
            }
        }

        private void MemorySubtract_Click(object sender, RoutedEventArgs e)
        {
            if (!TryGetDisplayValue(out var value)) return;

            try
            {
                // Security: check for overflow before subtracting
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
                // Security: handle unexpected errors
                System.Diagnostics.Debug.WriteLine($"Error in MemorySubtract: {ex}");
                ResetMemory();
                ShowError();
            }
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
            _memoryHistoryEntries.Clear();
            _memoryHistoryText = string.Empty;
            UpdateMemoryDisplay();
        }

        private void ApplyUnaryFunction(Func<double, double> func, string operationName, bool degrees = false, bool validateTan = false)
        {
            // Security: validate function and operation name
            if (func == null || string.IsNullOrWhiteSpace(operationName)) return;
            if (!TryGetDisplayValue(out var value)) return;

            var originalValue = value;

            if (degrees)
            {
                // Performance: use precomputed constant
                value *= DegreesToRadians;
            }

            if (validateTan)
            {
                // Security: check for tangent undefined points (where cos = 0)
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

                // Security: validate result
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
            catch (Exception ex)
            {
                // Security: catch any math function errors
                System.Diagnostics.Debug.WriteLine($"Error in unary function {operationName}: {ex}");
                ShowError();
            }
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
            // Security: validate input bounds
            if (value < 0) throw new OverflowException("Factorial is not defined for negative numbers");
            if (value > MaxFactorial) throw new OverflowException($"Factorial overflow: maximum supported value is {MaxFactorial}");

            var result = _factorialCache[value];
            if (!IsFinite(result))
            {
                throw new OverflowException($"Factorial overflow at {value}!");
            }

            return result;
        }

    internal void ResetCalculatorState()
        {
            DisplayBox.Text = "0";
            _leftOperand = null;
            _pendingOperator = null;
            _shouldResetDisplay = false;
            _lastOperationDescription = null;
            // Removed: _operationStack.Clear() - no longer needed
        }

        private bool TryGetDisplayValue(out double value)
        {
            var text = DisplayBox.Text;

            // Security: validate text length to prevent potential issues
            if (string.IsNullOrEmpty(text) || text.Length > MaxDisplayLength || text == "Error")
            {
                value = 0;
                return false;
            }

            // Performance: use NumberStyles.Float for better performance with scientific notation
            if (!double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, Culture, out value))
            {
                value = 0;
                return false;
            }

            // Security: validate result is finite
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
            // Removed: _operationStack.Clear() - no longer needed
        }

        private void InitializeMemory()
        {
            MemoryListBox.ItemsSource = _memoryItems;
            UpdateMemoryDisplay();

        }
        private readonly System.Collections.ObjectModel.ObservableCollection<string> _memoryItems
                   = new System.Collections.ObjectModel.ObservableCollection<string>();

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


                // Performance: update existing item rather than clearing and re-adding
                _memoryItems.Clear();
                if (string.IsNullOrEmpty(historyText))
                {
                    _memoryItems.Add($"Memory: {value}");
                }
                else
                {
                    _memoryItems.Add($"Memory total: {value}");
                    _memoryItems.Add($"History: {historyText}");
                }

            }
            catch (Exception ex)
            {
                // Security: handle display errors gracefully
                System.Diagnostics.Debug.WriteLine($"Error updating memory display: {ex}");
            }
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
            var entryCount = _memoryHistoryEntries.Count;
            if (entryCount == 0)
            {
                _memoryHistoryText = string.Empty;
                return;
            }

            var entries = _memoryHistoryEntries.ToArray();
            var firstContributions = new int[entryCount];
            var subsequentContributions = new int[entryCount];

            for (var i = 0; i < entryCount; i++)
            {
                var entry = entries[i];
                var description = entry.Description ?? string.Empty;
                var descriptionLength = description.Length;
                firstContributions[i] = descriptionLength + (entry.IsAddition ? 0 : 2);
                subsequentContributions[i] = descriptionLength + 4; // "; " + sign + space
            }

            var suffixLengths = new int[entryCount + 1];
            for (var i = entryCount - 1; i >= 0; i--)
            {
                suffixLengths[i] = suffixLengths[i + 1] + subsequentContributions[i];
            }

            var startIndex = 0;
            for (; startIndex < entryCount; startIndex++)
            {
                var totalLength = firstContributions[startIndex] + suffixLengths[startIndex + 1];
                if (totalLength <= MaxMemoryHistoryLength)
                {
                    break;
                }
            }

            if (startIndex >= entryCount)
            {
                _memoryHistoryEntries.Clear();
                _memoryHistoryText = string.Empty;
                return;
            }

            for (var i = 0; i < startIndex; i++)
            {
                _memoryHistoryEntries.Dequeue();
            }

            var trimmedCount = entryCount - startIndex;
            var estimatedLength = firstContributions[startIndex] + suffixLengths[startIndex + 1];
            var builderCapacity = Math.Min(MaxMemoryHistoryLength, Math.Max(128, estimatedLength));
            var builder = new StringBuilder(builderCapacity);

            var isFirst = true;
            for (var i = 0; i < trimmedCount; i++)
            {
                var entry = entries[startIndex + i];
                var description = entry.Description ?? string.Empty;

                if (isFirst)
                {
                    if (!entry.IsAddition)
                    {
                        builder.Append("- ");
                    }
                    builder.Append(description);
                    isFirst = false;
                }
                else
                {
                    builder.Append("; ");
                    builder.Append(entry.IsAddition ? "+ " : "- ");
                    builder.Append(description);
                }
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

            // Fallback if TryFormat fails (shouldn't in practice)
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
            // Performance: use if-else for better branch prediction on frequently used operators
            // Security: validate inputs to prevent edge cases
            if (!IsFinite(left) || !IsFinite(right))
            {
                throw new InvalidOperationException("Invalid operand values");
            }

            if (operatorSymbol == "+")
            {
                var result = left + right;
                // Security: check for overflow
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
                // Security: explicit zero check before division
                if (Math.Abs(right) < double.Epsilon) throw new DivideByZeroException();
                var result = left / right;
                if (!IsFinite(result)) throw new OverflowException("Division overflow");
                return result;
            }

            // Security: reject unknown operators
            throw new InvalidOperationException($"Unknown operator: {operatorSymbol}");
        }

        private void InitializeTheme()
        {
            UpdateThemeToggleButton();
        }

        private async void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            // Performance: early return if already animating (prevents unnecessary processing)
            if (_isAnimating) return;

            _isAnimating = true;
            var previousMode = _isDarkMode;
            var nextMode = !previousMode;

            try
            {
                // Performance: animations run in parallel on UI thread, but we await each for proper sequencing
                await AnimateButtonClick().ConfigureAwait(true);
                await FadeOutWindow().ConfigureAwait(true);

                _isDarkMode = nextMode;
                ApplyTheme();
                UpdateThemeToggleButton();

                await FadeInWindow().ConfigureAwait(true);
            }
            catch (OperationCanceledException)
            {
                // Performance: handle cancellation gracefully without error message
                _isDarkMode = previousMode;
                try
                {
                    ApplyTheme();
                    UpdateThemeToggleButton();
                }
                catch { }
                Opacity = 1.0;
            }
            catch (Exception ex)
            {
                // Security: log error and restore previous state
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
                // Performance: only show message box on actual errors, not cancellations
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
    internal void ProcessDigit(string digit)
        {
            if (string.IsNullOrEmpty(digit)) return; // Security: validate input

            _lastOperationDescription = null;
            var currentText = DisplayBox.Text;
            if (_shouldResetDisplay || currentText is "0" or "Error")
            {
                DisplayBox.Text = digit.Length <= MaxDisplayLength ? digit : digit[..MaxDisplayLength];
            }
            else
            {
                var newLength = currentText.Length + digit.Length;
                if (newLength <= MaxDisplayLength)
                {
                    // Performance: use string concatenation for small strings (more efficient than StringBuilder for <10 concatenations)
                    DisplayBox.Text = currentText + digit;
                }
                // else ignore additional input to prevent uncontrolled growth
            }

            _shouldResetDisplay = false;
        }

    internal void ProcessDecimal()
        {
            var currentText = DisplayBox.Text;
            if (_shouldResetDisplay || currentText == "Error")
            {
                DisplayBox.Text = "0.";
                _shouldResetDisplay = false;
                _lastOperationDescription = null;
                return;
            }

            // Performance: IndexOf is faster than Contains for single character
            if (currentText.IndexOf('.') < 0 && currentText.Length + 1 <= MaxDisplayLength)
            {
                DisplayBox.Text = currentText + ".";
                _lastOperationDescription = null;
            }
        }

        // Removed: ProcessOpenParenthesis() and ProcessCloseParenthesis() - parenthesis support removed for simplification and optimization

    internal void ProcessDelete()
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

    internal void ProcessOperator(string operatorSymbol)
        {
            // Security: validate operator is one of the allowed operations
            if (string.IsNullOrWhiteSpace(operatorSymbol) ||
                (operatorSymbol != "+" && operatorSymbol != "-" && operatorSymbol != "*" && operatorSymbol != "/"))
            {
                return;
            }

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
                catch (Exception ex)
                {
                    // Security: catch any unexpected exceptions to prevent crash
                    System.Diagnostics.Debug.WriteLine($"Unexpected error in ProcessOperator: {ex}");
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

    internal void ProcessEquals()
        {
            // Performance: Simplified without parenthesis support - direct calculation
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
            catch (Exception ex)
            {
                // Security: catch any unexpected calculation errors
                System.Diagnostics.Debug.WriteLine($"Unexpected error in ProcessEquals: {ex}");
                ShowError();
            }
        }

        // Removed: TryResolvePendingOperation() - no longer needed without parenthesis support

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            // Security: validate event args
            if (e == null) return;

            try
            {
                var modifiers = Keyboard.Modifiers;
                var key = e.Key;




                // Performance: check most common cases first (digits)
                if (modifiers == ModifierKeys.None && key >= Key.D0 && key <= Key.D9)
                {
                    var ch = (char)('0' + (e.Key - Key.D0));
                    ProcessDigit(ch.ToString());
                    e.Handled = true;
                    return;
                }

                if (key < Key.NumPad0 || key > Key.NumPad9)
                {
                    // Performance: group related operations using if-else for better branch prediction

                    if (key == Key.Add || (key == Key.OemPlus && Keyboard.Modifiers == ModifierKeys.None))
                    {
                        ProcessOperator("+");
                        e.Handled = true;
                    }
                    else if (key == Key.Subtract || key == Key.OemMinus)
                    {
                        ProcessOperator("-");
                        e.Handled = true;
                    }
                    else if (key == Key.Multiply)
                    {
                        ProcessOperator("*");
                        e.Handled = true;
                    }
                    else if (key == Key.Divide || key == Key.Oem2)
                    {
                        ProcessOperator("/");
                        e.Handled = true;
                    }
                    // Removed: Parenthesis keyboard shortcuts - no longer supported
                    else if (key == Key.Decimal || key == Key.OemPeriod)
                    {
                        ProcessDecimal();
                        e.Handled = true;
                    }
                    else if (key == Key.Return || key == Key.Enter)
                    {
                        ProcessEquals();
                        e.Handled = true;
                    }
                    else if (key == Key.Back)
                    {
                        ProcessDelete();
                        e.Handled = true;
                    }
                    else if (key == Key.Escape)
                    {
                        ResetCalculatorState();
                        e.Handled = true;
                    }
                    else if (modifiers == ModifierKeys.Control && key == Key.C)
                    {
                        ResetCalculatorState();
                        e.Handled = true;
                    }
                    else if (modifiers == ModifierKeys.Control && key == Key.M)
                    {
                        ResetMemory();
                        e.Handled = true;
                    }

                    else if (key == Key.Oem5)
                    {
                        Percent_Click(this, new RoutedEventArgs());
                        e.Handled = true;
                    }
                }
                else
                {
                    var ch = (char)('0' + (key - Key.NumPad0));
                    ProcessDigit(ch.ToString());
                    e.Handled = true;
                    return;
                }
            }
            catch (Exception ex)
            {
                // Security: prevent crashes from keyboard handling
                System.Diagnostics.Debug.WriteLine($"Error in keyboard handler: {ex}");
            }
        }

        private Storyboard EnsureCachedButtonClickStoryboard(ScaleTransform scaleTransform)
        {
            if (_cachedButtonClickStoryboard != null) return _cachedButtonClickStoryboard;

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
            _cachedButtonClickStoryboard = storyboard;
            return storyboard;
        }

        private async Task AnimateButtonClick()
        {
            if (!_animationsEnabled) return;
            var button = ThemeToggleButton;

            // Ensure a ScaleTransform exists (avoid null and extra returns)
            if (button.RenderTransform is not System.Windows.Media.ScaleTransform scaleTransform)
            {
                scaleTransform = new System.Windows.Media.ScaleTransform(1.0, 1.0);
                button.RenderTransform = scaleTransform;
            }

            var storyboard = EnsureCachedButtonClickStoryboard(scaleTransform);

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
            if (!_animationsEnabled) return;
            await FadeOpacity(1.0, 0.3, TimeSpan.FromMilliseconds(250), new QuadraticEase { EasingMode = EasingMode.EaseOut });
        }

        private async Task FadeInWindow()
        {
            await FadeOpacity(0.3, 1.0, TimeSpan.FromMilliseconds(250), new QuadraticEase { EasingMode = EasingMode.EaseIn });
        }

        private async Task FadeOpacity(double from, double to, TimeSpan duration, IEasingFunction? easing = null)
        {
            var animation = new DoubleAnimation(from, to, duration) { EasingFunction = easing };
            var storyboard = _cachedFadeStoryboard ??= new Storyboard();
            storyboard.Children.Clear();
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

