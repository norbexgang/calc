using System;
using System.IO;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using Serilog;
using Serilog.Formatting.Compact;

namespace CalcApp;

/// <summary>
/// Az alkalmazás fő belépési pontja és globális kezelője.
/// </summary>
public partial class App : Application
{
    #region Constants

    private const string AppDataFolderName = "CalcApp";
    private const string LogsFolderName = "logs";
    private const string LogFilePattern = "log-.json";
    private const string StartupProfileName = "Startup.profile";
    private const int LogRetentionDays = 14;
    private const int AsyncBufferSize = 10000;

    #endregion

    #region Fields

    private readonly string _profileRoot;
    private readonly string _logsPath;

    #endregion

    #region Properties

    /// <summary>
    /// Az aktuális alkalmazáspéldány elérése.
    /// </summary>
    public static App? CurrentApp => Current as App;

    /// <summary>
    /// Megadja, hogy a naplózás engedélyezve van-e.
    /// </summary>
    public bool IsLoggingEnabled { get; private set; } = true;

    #endregion

    #region Constructor

    public App()
    {
        _profileRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolderName);
        _logsPath = Path.Combine(_profileRoot, LogsFolderName);

        InitializeDirectories();
        InitializeProfileOptimization();
        RegisterExceptionHandlers();
    }

    #endregion

    #region Lifecycle

    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureLogger(IsLoggingEnabled);
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        UnregisterExceptionHandlers();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Beállítja a naplózás állapotát.
    /// </summary>
    /// <param name="enabled">Engedélyezve legyen-e a naplózás.</param>
    public void SetLoggingEnabled(bool enabled)
    {
        if (IsLoggingEnabled == enabled) return;

        IsLoggingEnabled = enabled;
        ConfigureLogger(enabled);
    }

    #endregion

    #region Private Methods - Initialization

    private void InitializeDirectories()
    {
        Directory.CreateDirectory(_profileRoot);
        Directory.CreateDirectory(_logsPath);
    }

    private void InitializeProfileOptimization()
    {
        ProfileOptimization.SetProfileRoot(_profileRoot);
        ProfileOptimization.StartProfile(StartupProfileName);
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    private void UnregisterExceptionHandlers()
    {
        DispatcherUnhandledException -= OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException -= OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
    }

    #endregion

    #region Private Methods - Logging

    private void ConfigureLogger(bool enabled)
    {
        Log.CloseAndFlush();

        Log.Logger = enabled
            ? CreateFileLogger()
            : CreateEmptyLogger();
    }

    private static Serilog.Core.Logger CreateEmptyLogger()
        => new LoggerConfiguration().CreateLogger();

    private Serilog.Core.Logger CreateFileLogger()
    {
        // Ensure the logs directory exists (tests may delete it between runs)
        Directory.CreateDirectory(_logsPath);
        var logFile = Path.Combine(_logsPath, LogFilePattern);

        return new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
            .MinimumLevel.Warning()
#endif
            .Enrich.WithThreadId()
            .Enrich.FromLogContext()
            .WriteTo.Async(
                configure: a => a.File(
                    formatter: new CompactJsonFormatter(),
                    path: logFile,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: LogRetentionDays,
                    shared: true,
                    buffered: false,
                    flushToDiskInterval: TimeSpan.FromSeconds(2)),
                blockWhenFull: true,
                bufferSize: AsyncBufferSize)
            .CreateLogger();
    }

    #endregion

    #region Exception Handlers

    private void OnDispatcherUnhandledException(
        object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Váratlan hiba történt a UI szálon");

        MessageBox.Show(
            "Váratlan hiba történt. A részletek naplózva lettek.",
            "Hiba",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Error(ex, "AppDomain kezeletlen kivétel");
        }
        else
        {
            Log.Error("AppDomain kezeletlen kivétel: {Exception}", e.ExceptionObject);
        }

        Log.CloseAndFlush();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e == null) return;

        Log.Error(e.Exception, "Nem figyelt Task kivétel");
        e.SetObserved();
    }

    #endregion
}
