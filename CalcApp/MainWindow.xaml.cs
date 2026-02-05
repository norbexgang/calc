using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using CalcApp.ViewModels;
using Serilog;

namespace CalcApp;

/// <summary>
/// A főablak interakciós logikája.
/// </summary>
public partial class MainWindow : Window
{
    #region Constants

    private const string WindowShadowEffectKey = "WindowShadowEffect";
    private const string ButtonShadowEffectKey = "ButtonShadowEffect";
    private const string ButtonHoverShadowEffectKey = "ButtonHoverShadowEffect";
    private const string NeonBorderEffectKey = "NeonBorderEffect";
    private const string NeonTextEffectKey = "NeonTextEffect";
    private const string NeonBorderEffectDefaultKey = "NeonBorderEffectDefault";
    private const string NeonTextEffectDefaultKey = "NeonTextEffectDefault";

    #endregion

    #region Static Fields

    private static readonly DropShadowEffect DefaultWindowShadow;
    private static readonly DropShadowEffect DefaultButtonShadow;
    private static readonly DropShadowEffect DefaultButtonHoverShadow;
    private static readonly Dictionary<Key, Action<CalculatorViewModel>> KeyMappings = new(30);
    private static readonly string[] DigitStrings = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];

    #endregion

    #region Fields

    private readonly DropShadowEffect? _neonBorderEffectDefault;
    private readonly DropShadowEffect? _neonTextEffectDefault;
    private bool _isTurboMode;

    #endregion

    #region Static Constructor

    static MainWindow()
    {
        DefaultWindowShadow = CreateFrozenShadow(
            Colors.Black, opacity: 0.35, blurRadius: 8, shadowDepth: 3);

        DefaultButtonShadow = CreateFrozenShadow(
            Color.FromRgb(209, 196, 233), opacity: 0.4, blurRadius: 12, shadowDepth: 4);

        DefaultButtonHoverShadow = CreateFrozenShadow(
            Color.FromRgb(209, 196, 233), opacity: 0.6, blurRadius: 16, shadowDepth: 4);

        InitializeKeyMappings();
    }

    #endregion

    #region Constructor

    public MainWindow()
    {
        LoadComponent();

        _neonBorderEffectDefault = TryFindResource(NeonBorderEffectDefaultKey) as DropShadowEffect;
        _neonTextEffectDefault = TryFindResource(NeonTextEffectDefaultKey) as DropShadowEffect;

        ApplyShadowResources();
    }

    #endregion

    #region Private Methods - Initialization

    private void LoadComponent()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Kritikus hiba: nem sikerült betölteni a főablak XAML-jét");
            Application.Current?.Shutdown();
        }
    }

    private static DropShadowEffect CreateFrozenShadow(
        Color color,
        double opacity,
        double blurRadius,
        double shadowDepth)
    {
        var effect = new DropShadowEffect
        {
            Color = color,
            Opacity = opacity,
            BlurRadius = blurRadius,
            ShadowDepth = shadowDepth,
            Direction = 270,
            RenderingBias = RenderingBias.Performance
        };
        effect.Freeze();
        return effect;
    }

    private static void InitializeKeyMappings()
    {
        if (KeyMappings.Count > 0) return;

        RegisterDigitMappings();
        RegisterOperatorMappings();
        RegisterControlMappings();
    }

    private static void RegisterDigitMappings()
    {
        for (var i = 0; i <= 9; i++)
        {
            var digit = DigitStrings[i];
            KeyMappings[Key.D0 + i] = vm => vm.DigitCommand.Execute(digit);
            KeyMappings[Key.NumPad0 + i] = vm => vm.DigitCommand.Execute(digit);
        }
    }

    private static void RegisterOperatorMappings()
    {
        KeyMappings[Key.Add] = vm => vm.OperatorCommand.Execute("+");
        KeyMappings[Key.Subtract] = vm => vm.OperatorCommand.Execute("-");
        KeyMappings[Key.OemMinus] = vm => vm.OperatorCommand.Execute("-");
        KeyMappings[Key.Multiply] = vm => vm.OperatorCommand.Execute("*");
        KeyMappings[Key.Divide] = vm => vm.OperatorCommand.Execute("/");
    }

    private static void RegisterControlMappings()
    {
        KeyMappings[Key.Decimal] = vm => vm.DecimalCommand.Execute(null);
        KeyMappings[Key.OemPeriod] = vm => vm.DecimalCommand.Execute(null);
        KeyMappings[Key.Return] = vm => vm.EqualsCommand.Execute(null);
        KeyMappings[Key.Enter] = vm => vm.EqualsCommand.Execute(null);
        KeyMappings[Key.Back] = vm => vm.DeleteCommand.Execute(null);
        KeyMappings[Key.Escape] = vm => vm.ClearCommand.Execute(null);
    }

    #endregion

    #region Private Methods - Visual Effects

    private void ApplyShadowResources()
    {
        if (_isTurboMode)
        {
            ClearShadowEffects();
        }
        else
        {
            RestoreShadowEffects();
        }
    }

    private void ClearShadowEffects()
    {
        Resources[WindowShadowEffectKey] = null;
        Resources[ButtonShadowEffectKey] = null;
        Resources[ButtonHoverShadowEffectKey] = null;
        Resources[NeonBorderEffectKey] = null;
        Resources[NeonTextEffectKey] = null;
    }

    private void RestoreShadowEffects()
    {
        Resources[WindowShadowEffectKey] = DefaultWindowShadow;
        Resources[ButtonShadowEffectKey] = DefaultButtonShadow;
        Resources[ButtonHoverShadowEffectKey] = DefaultButtonHoverShadow;

        if (_neonBorderEffectDefault != null)
            Resources[NeonBorderEffectKey] = _neonBorderEffectDefault;

        if (_neonTextEffectDefault != null)
            Resources[NeonTextEffectKey] = _neonTextEffectDefault;
    }

    #endregion

    #region Event Handlers

    private void TurboToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton toggleButton) return;

        var newTurboState = toggleButton.IsChecked == true;
        if (_isTurboMode == newTurboState) return;

        _isTurboMode = newTurboState;

        if (DataContext is CalculatorViewModel viewModel)
            viewModel.SetTurboMode(_isTurboMode);

        ApplyShadowResources();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not CalculatorViewModel viewModel) return;

        if (HandleControlKeyShortcuts(viewModel, e)) return;
        if (HandlePlusKey(viewModel, e)) return;
        HandleMappedKey(viewModel, e);
    }

    private static bool HandleControlKeyShortcuts(CalculatorViewModel viewModel, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Control) return false;

        switch (e.Key)
        {
            case Key.C:
                viewModel.ClearCommand.Execute(null);
                e.Handled = true;
                return true;

            case Key.M:
                viewModel.MemoryClearCommand.Execute(null);
                e.Handled = true;
                return true;

            default:
                return false;
        }
    }

    private static bool HandlePlusKey(CalculatorViewModel viewModel, KeyEventArgs e)
    {
        if (e.Key != Key.OemPlus) return false;

        if (Keyboard.Modifiers == ModifierKeys.Shift)
            viewModel.OperatorCommand.Execute("+");
        else if (Keyboard.Modifiers == ModifierKeys.None)
            viewModel.EqualsCommand.Execute(null);

        e.Handled = true;
        return true;
    }

    private static void HandleMappedKey(CalculatorViewModel viewModel, KeyEventArgs e)
    {
        var modifiers = Keyboard.Modifiers;
        if (modifiers != ModifierKeys.None && modifiers != ModifierKeys.Shift) return;

        // Magyar billentyűzet: Shift+5 = %, Shift+6 = /
        if (modifiers == ModifierKeys.Shift)
        {
            if (e.Key == Key.D5)
            {
                viewModel.PercentCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.D6)
            {
                viewModel.OperatorCommand.Execute("/");
                e.Handled = true;
                return;
            }
        }

        if (KeyMappings.TryGetValue(e.Key, out var action))
        {
            action(viewModel);
            e.Handled = true;
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow { Owner = this };
        settingsWindow.ShowDialog();
    }

    #endregion
}
