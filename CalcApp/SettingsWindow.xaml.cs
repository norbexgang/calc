using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace CalcApp;

/// <summary>
/// A beállítások ablak interakciós logikája.
/// </summary>
public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    #region Fields

    private bool _isSerilogEnabled;

    #endregion

    #region Constructor

    public SettingsWindow()
    {
        InitializeComponent();
        DataContext = this;

        LoadSettings();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Megadja vagy beállítja, hogy a Serilog naplózás engedélyezve van-e.
    /// </summary>
    public bool IsSerilogEnabled
    {
        get => _isSerilogEnabled;
        set
        {
            if (_isSerilogEnabled == value) return;

            _isSerilogEnabled = value;
            OnPropertyChanged();
            ApplyLoggingSetting(value);
        }
    }

    #endregion

    #region Events

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region Private Methods

    private void LoadSettings()
    {
        _isSerilogEnabled = App.CurrentApp?.IsLoggingEnabled ?? true;
    }

    private static void ApplyLoggingSetting(bool enabled)
    {
        App.CurrentApp?.SetLoggingEnabled(enabled);
    }

    /// <summary>
    /// Kiváltja a PropertyChanged eseményt.
    /// </summary>
    /// <param name="propertyName">A megváltozott tulajdonság neve.</param>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region Event Handlers

    private void Close_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion
}
