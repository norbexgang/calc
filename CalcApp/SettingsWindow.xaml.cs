using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

namespace CalcApp
{
    public partial class SettingsWindow : Window, INotifyPropertyChanged
    {
        private bool _isSerilogEnabled;

        public SettingsWindow()
        {
            InitializeComponent();
            DataContext = this;

            _isSerilogEnabled = App.CurrentApp?.IsLoggingEnabled ?? true;
        }

        public bool IsSerilogEnabled
        {
            get => _isSerilogEnabled;
            set
            {
                if (_isSerilogEnabled == value) return;
                _isSerilogEnabled = value;
                OnPropertyChanged();
                App.CurrentApp?.SetLoggingEnabled(value);
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
