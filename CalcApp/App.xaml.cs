using System;
using System.IO;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using Serilog.Formatting.Compact;

namespace CalcApp;

public partial class App : Application
{
    private readonly string _profileRoot;
    public static App? CurrentApp => Current as App;

    public bool IsLoggingEnabled { get; private set; } = true;

    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        _profileRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CalcApp");
        Directory.CreateDirectory(_profileRoot);
        ProfileOptimization.SetProfileRoot(_profileRoot);
        ProfileOptimization.StartProfile("Startup.profile");
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        // Log the exception
        Log.Error(e.Exception, "An unexpected error occurred");

        // Show user-friendly message
        MessageBox.Show("An unexpected error occurred. Details were logged and the app will try to continue.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Error(ex, "AppDomain unhandled exception");
        }
        else
        {
            Log.Error("AppDomain unhandled exception: {Exception}", e.ExceptionObject);
        }

        Log.CloseAndFlush();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e == null) return;

        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        Log.CloseAndFlush();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureLogger(IsLoggingEnabled);

        base.OnStartup(e);
    }

    public void SetLoggingEnabled(bool enabled)
    {
        if (IsLoggingEnabled == enabled) return;

        IsLoggingEnabled = enabled;
        ConfigureLogger(enabled);
    }

    private void ConfigureLogger(bool enabled)
    {
        Log.CloseAndFlush();

        if (!enabled)
        {
            Log.Logger = new LoggerConfiguration().CreateLogger();
            return;
        }

        // Initialize logger after profile optimization but before UI
        var logsPath = Path.Combine(_profileRoot, "logs");
        Directory.CreateDirectory(logsPath);
        var logFile = Path.Combine(logsPath, "log-.json");

        var loggerConfig = new LoggerConfiguration()
#if DEBUG
             .MinimumLevel.Debug()
#else
             .MinimumLevel.Warning()
#endif
             .Enrich.WithThreadId()
             .Enrich.FromLogContext()
             .WriteTo.Async(a => a.File(new CompactJsonFormatter(), logFile, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14, shared: true, buffered: false, flushToDiskInterval: TimeSpan.FromSeconds(2)), blockWhenFull: true, bufferSize: 10000);

        Log.Logger = loggerConfig.CreateLogger();
    }
}
