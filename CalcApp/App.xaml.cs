using System;
using System.Diagnostics;
using System.IO;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Serilog;
using Serilog.Formatting.Compact;

namespace CalcApp;

/// <summary>
/// Main application entry point and global services.
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

    private static readonly Stopwatch StartupStopwatch;

    private readonly string _profileRoot;
    private readonly string _logsPath;
    private bool _isLoggerConfigured;
    private string? _startupBenchmarkOutputPath;

    #endregion

    #region Properties

    public static App? CurrentApp => Current as App;

    public bool IsLoggingEnabled { get; private set; } = true;

    internal bool IsStartupBenchmarkMode { get; private set; }

    internal string? StartupBenchmarkOutputPath => _startupBenchmarkOutputPath;

    internal static long StartupElapsedMilliseconds => StartupStopwatch.ElapsedMilliseconds;

    #endregion

    #region Static Constructor

    static App()
    {
        StartupStopwatch = Stopwatch.StartNew();
    }

    #endregion

    #region Constructor

    public App()
    {
        _profileRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            AppDataFolderName);
        _logsPath = Path.Combine(_profileRoot, LogsFolderName);

        InitializeProfileDirectory();
        InitializeProfileOptimization();
        RegisterExceptionHandlers();
    }

    #endregion

    #region Lifecycle

    protected override void OnStartup(StartupEventArgs e)
    {
        ParseStartupArguments(e.Args);

        Log.Logger = CreateEmptyLogger();
        base.OnStartup(e);

        Dispatcher.BeginInvoke(
            InitializeLoggerAfterStartup,
            DispatcherPriority.Background);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        UnregisterExceptionHandlers();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    #endregion

    #region Public Methods

    public void SetLoggingEnabled(bool enabled)
    {
        if (IsLoggingEnabled == enabled && _isLoggerConfigured) return;

        IsLoggingEnabled = enabled;
        ConfigureLogger(enabled);
    }

    #endregion

    #region Private Methods - Initialization

    private void ParseStartupArguments(string[] args)
    {
        if (args.Length == 0) return;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--startup-benchmark", StringComparison.OrdinalIgnoreCase))
            {
                IsStartupBenchmarkMode = true;
                continue;
            }

            if (string.Equals(arg, "--startup-benchmark-output", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 < args.Length)
                {
                    IsStartupBenchmarkMode = true;
                    _startupBenchmarkOutputPath = args[++i].Trim('"');
                }
                continue;
            }

            const string outputPrefix = "--startup-benchmark-output=";
            if (arg.StartsWith(outputPrefix, StringComparison.OrdinalIgnoreCase))
            {
                IsStartupBenchmarkMode = true;
                _startupBenchmarkOutputPath = arg[outputPrefix.Length..].Trim('"');
            }
        }

        if (IsStartupBenchmarkMode && string.IsNullOrWhiteSpace(_startupBenchmarkOutputPath))
        {
            _startupBenchmarkOutputPath = Path.Combine(_profileRoot, "startup-benchmark.csv");
        }
    }

    private void InitializeProfileDirectory()
    {
        Directory.CreateDirectory(_profileRoot);
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

    private void InitializeLoggerAfterStartup()
    {
        ConfigureLogger(IsLoggingEnabled);
    }

    private void ConfigureLogger(bool enabled)
    {
        Log.CloseAndFlush();

        Log.Logger = enabled
            ? CreateFileLogger()
            : CreateEmptyLogger();
        _isLoggerConfigured = true;
    }

    private static Serilog.Core.Logger CreateEmptyLogger()
        => new LoggerConfiguration().CreateLogger();

    private Serilog.Core.Logger CreateFileLogger()
    {
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
        Log.Error(e.Exception, "Varatlan hiba tortent a UI szalon");

        MessageBox.Show(
            "Varatlan hiba tortent. A reszletek naplozva lettek.",
            "Hiba",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Error(ex, "AppDomain kezeletlen kivetel");
        }
        else
        {
            Log.Error("AppDomain kezeletlen kivetel: {Exception}", e.ExceptionObject);
        }

        Log.CloseAndFlush();
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        if (e == null) return;

        Log.Error(e.Exception, "Nem figyelt Task kivetel");
        e.SetObserved();
    }

    #endregion
}
