using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace CalcApp;

/// <summary>
/// Settings window interaction logic.
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

    public event PropertyChangedEventHandler? PropertyChanged;

    #endregion

    #region Private Methods

    private void LoadSettings()
    {
        _isSerilogEnabled = App.CurrentApp?.IsLoggingEnabled ?? true;
        OnPropertyChanged(nameof(IsSerilogEnabled));
    }

    private static void ApplyLoggingSetting(bool enabled)
    {
        App.CurrentApp?.SetLoggingEnabled(enabled);
    }

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
