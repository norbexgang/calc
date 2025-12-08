using System;
using System.IO;
using System.Runtime;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace CalcApp;

public partial class App : Application
{
    public App()
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        var profileRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CalcApp");
        Directory.CreateDirectory(profileRoot);
        ProfileOptimization.SetProfileRoot(profileRoot);
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
        // Initialize logger after profile optimization but before UI
        var loggerConfig = new LoggerConfiguration()
#if DEBUG
             .MinimumLevel.Debug()
#else
             .MinimumLevel.Warning()
#endif
             .Enrich.WithThreadId()
             .Enrich.FromLogContext()
             .WriteTo.Async(a => a.File("logs/log.txt", rollingInterval: RollingInterval.Day));

        Log.Logger = loggerConfig.CreateLogger();

        base.OnStartup(e);
    }
}
