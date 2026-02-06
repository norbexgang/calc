using System;
using System.IO;
using CalcApp;
using Serilog;
using Xunit;

namespace CalcApp.Tests;

public class AppAndSettingsTests
{
    [Fact]
    public void SetLoggingEnabled_CreatesLogFileWhenEnabled()
    {
        StaTestHelper.RunInSta(() =>
        {
            var app = App.CurrentApp ?? new App();

            var logsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "CalcApp",
                "logs");

            if (Directory.Exists(logsPath)) Directory.Delete(logsPath, true);

            app.SetLoggingEnabled(true);
            Log.Information("Unit test log entry");
            Log.CloseAndFlush();

            Assert.True(Directory.Exists(logsPath));
            var files = Directory.GetFiles(logsPath, "log-*.json");
            Assert.NotEmpty(files);

            try { Directory.Delete(Path.GetDirectoryName(logsPath)!, true); } catch { }
        });
    }

    [Fact]
    public void SettingsWindow_TogglingUpdatesAppLogging()
    {
        StaTestHelper.RunInSta(() =>
        {
            var app = App.CurrentApp ?? new App();
            app.SetLoggingEnabled(true);

            var sw = new SettingsWindow();
            Assert.True(sw.IsSerilogEnabled);

            sw.IsSerilogEnabled = false;
            Assert.False(App.CurrentApp?.IsLoggingEnabled);

            sw.Close();
            Log.CloseAndFlush();
        });
    }
}
