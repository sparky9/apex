using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using ApexV2.Core.Logging;
using ApexV2.Core.Config;
using ApexV2.UI.Themes; // added

namespace ApexV2;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static LogManager LogManager { get; private set; } = null!;
    public static ThemeManager ThemeManager { get; private set; } = null!; // added

    protected override void OnStartup(StartupEventArgs e) // removed async (no awaits)
    {
        base.OnStartup(e);
        LogManager = new LogManager { MinimumLevel = LogLevel.Debug };
        var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
        LogManager.AddSink(new RollingFileLogSink(logDir, "apex", maxBytes: 2_000_000, maxFiles: 7));
        LogManager.AddSink(new InMemoryLogSink(2000));
        // init theme manager
        var settingsService = new SettingsService(new Core.Database.DatabaseService());
        ThemeManager = new ThemeManager(LogManager.GetLogger("ThemeManager"), settingsService);
        // attempt load saved theme
        _ = ApplySavedThemeAsync(settingsService);
        
        AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
        {
            try { LogManager.GetLogger("App").Critical("Unhandled domain exception", exArgs.ExceptionObject as Exception); } catch { }
        };
        DispatcherUnhandledException += (s, exArgs) =>
        {
            try { LogManager.GetLogger("App").Error("Dispatcher unhandled exception", exArgs.Exception); } catch { }
            exArgs.Handled = true;
            System.Windows.MessageBox.Show("A critical error occurred. See logs.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        };
        TaskScheduler.UnobservedTaskException += (s, exArgs) =>
        {
            try { LogManager.GetLogger("App").Error("Unobserved task exception", exArgs.Exception); } catch { }
            exArgs.SetObserved();
        };

        LogManager.GetLogger("App").Info("Application startup complete");
    }

    private async Task ApplySavedThemeAsync(SettingsService settingsService)
    {
        try
        {
            var settings = await settingsService.LoadSettingsAsync();
            var theme = string.IsNullOrWhiteSpace(settings.Appearance.Theme) ? "Dark" : settings.Appearance.Theme;
            var accent = settings.Appearance.AccentColor;
            ThemeManager.ApplyTheme(theme, persist: false, overrideAccentHex: accent);
        }
        catch (Exception ex)
        {
            LogManager.GetLogger("App").Error("ApplySavedThemeAsync failed", ex);
            ThemeManager.ApplyTheme("Dark", persist: false);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        LogManager.GetLogger("App").Info("Application shutting down");
        await LogManager.DisposeAsync();
        base.OnExit(e);
    }
}