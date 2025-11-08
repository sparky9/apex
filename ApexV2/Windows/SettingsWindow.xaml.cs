using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
// Only use System.Windows.Forms for FolderBrowserDialog
using ApexV2.Core.Config;
using ApexV2.Core.Database;
using ApexV2.Core.Logging;

namespace ApexV2.Windows;

/// <summary>
/// Professional settings window for APEX V2
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private SettingsModel _settings;
    private SettingsModel _originalSettings;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _settings = new SettingsModel();
        _originalSettings = new SettingsModel();
        
        _ = LoadSettingsAsync();
    }

    /// <summary>
    /// Load settings from database and populate UI
    /// </summary>
    private async Task LoadSettingsAsync()
    {
        try
        {
            _originalSettings = await _settingsService.LoadSettingsAsync();
            
            // Create a copy for editing (so we can cancel changes)
            _settings = new SettingsModel
            {
                Appearance = new AppearanceSettings
                {
                    Theme = _originalSettings.Appearance.Theme,
                    ShowToolbar = _originalSettings.Appearance.ShowToolbar,
                    ShowStatusBar = _originalSettings.Appearance.ShowStatusBar,
                    FontSize = _originalSettings.Appearance.FontSize,
                    EnableAnimations = _originalSettings.Appearance.EnableAnimations,
                    ShowTooltips = _originalSettings.Appearance.ShowTooltips,
                    AccentColor = _originalSettings.Appearance.AccentColor
                },
                Data = new DataSettings
                {
                    DefaultProvider = _originalSettings.Data.DefaultProvider,
                    RefreshIntervalMs = _originalSettings.Data.RefreshIntervalMs,
                    HistoryDays = _originalSettings.Data.HistoryDays,
                    EnableRealTime = _originalSettings.Data.EnableRealTime,
                    EnableCaching = _originalSettings.Data.EnableCaching,
                    CacheRetentionDays = _originalSettings.Data.CacheRetentionDays,
                    AlphaVantageApiKey = _originalSettings.Data.AlphaVantageApiKey,
                    IexApiKey = _originalSettings.Data.IexApiKey,
                    PolygonApiKey = _originalSettings.Data.PolygonApiKey,
                    FinnhubApiKey = _originalSettings.Data.FinnhubApiKey
                },
                Trading = new TradingSettings
                {
                    DefaultProvider = _originalSettings.Trading.DefaultProvider,
                    PaperTradingMode = _originalSettings.Trading.PaperTradingMode,
                    RequireConfirmation = _originalSettings.Trading.RequireConfirmation,
                    AlpacaApiKey = _originalSettings.Trading.AlpacaApiKey,
                    AlpacaSecretKey = _originalSettings.Trading.AlpacaSecretKey,
                    IbkrUsername = _originalSettings.Trading.IbkrUsername,
                    IbkrPassword = _originalSettings.Trading.IbkrPassword,
                    TdAmeritradeApiKey = _originalSettings.Trading.TdAmeritradeApiKey
                },
                Performance = new PerformanceSettings
                {
                    MaxConcurrentConnections = _originalSettings.Performance.MaxConcurrentConnections,
                    RequestTimeoutMs = _originalSettings.Performance.RequestTimeoutMs,
                    EnableMultiThreading = _originalSettings.Performance.EnableMultiThreading,
                    MaxMemoryUsageMB = _originalSettings.Performance.MaxMemoryUsageMB,
                    EnableLogging = _originalSettings.Performance.EnableLogging,
                    LogLevel = _originalSettings.Performance.LogLevel
                },
                Safety = new SafetySettings
                {
                    MaxPositionSize = _originalSettings.Safety.MaxPositionSize,
                    MaxDailyLoss = _originalSettings.Safety.MaxDailyLoss,
                    MaxOrderValue = _originalSettings.Safety.MaxOrderValue,
                    EnableStopLoss = _originalSettings.Safety.EnableStopLoss,
                    DefaultStopLossPercent = _originalSettings.Safety.DefaultStopLossPercent,
                    EnablePositionSizing = _originalSettings.Safety.EnablePositionSizing,
                    RiskPerTradePercent = _originalSettings.Safety.RiskPerTradePercent
                },
                Advanced = new AdvancedSettings
                {
                    EnableDebugMode = _originalSettings.Advanced.EnableDebugMode,
                    EnableBetaFeatures = _originalSettings.Advanced.EnableBetaFeatures,
                    ProxyServer = _originalSettings.Advanced.ProxyServer,
                    ProxyPort = _originalSettings.Advanced.ProxyPort,
                    CustomDataPath = _originalSettings.Advanced.CustomDataPath,
                    EnableTelemetry = _originalSettings.Advanced.EnableTelemetry
                }
            };

            PopulateUIFromSettings();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error loading settings: {ex.Message}", 
                           "Settings Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    /// <summary>
    /// Populate UI controls from settings model
    /// </summary>
    private void PopulateUIFromSettings()
    {
        // Appearance settings
        ThemeComboBox.Text = _settings.Appearance.Theme;
        ShowToolbarCheckBox.IsChecked = _settings.Appearance.ShowToolbar;
        ShowStatusBarCheckBox.IsChecked = _settings.Appearance.ShowStatusBar;
        FontSizeSlider.Value = _settings.Appearance.FontSize;
        EnableAnimationsCheckBox.IsChecked = _settings.Appearance.EnableAnimations;
        ShowTooltipsCheckBox.IsChecked = _settings.Appearance.ShowTooltips;
        AccentColorTextBox.Text = _settings.Appearance.AccentColor;

        // Data settings
        DataProviderComboBox.Text = _settings.Data.DefaultProvider;
        RefreshIntervalTextBox.Text = _settings.Data.RefreshIntervalMs.ToString();
        HistoryDaysTextBox.Text = _settings.Data.HistoryDays.ToString();
        EnableRealTimeCheckBox.IsChecked = _settings.Data.EnableRealTime;
        EnableCachingCheckBox.IsChecked = _settings.Data.EnableCaching;
        CacheRetentionTextBox.Text = _settings.Data.CacheRetentionDays.ToString();
        AlphaVantageKeyTextBox.Text = _settings.Data.AlphaVantageApiKey;
        IexKeyTextBox.Text = _settings.Data.IexApiKey;
        PolygonKeyTextBox.Text = _settings.Data.PolygonApiKey;
        FinnhubKeyTextBox.Text = _settings.Data.FinnhubApiKey;

        // Trading settings
        TradingProviderComboBox.Text = _settings.Trading.DefaultProvider;
        PaperTradingCheckBox.IsChecked = _settings.Trading.PaperTradingMode;
        RequireConfirmationCheckBox.IsChecked = _settings.Trading.RequireConfirmation;
        AlpacaApiKeyTextBox.Text = _settings.Trading.AlpacaApiKey;
        AlpacaSecretKeyBox.Password = _settings.Trading.AlpacaSecretKey;
        IbkrUsernameTextBox.Text = _settings.Trading.IbkrUsername;
        IbkrPasswordBox.Password = _settings.Trading.IbkrPassword;
        TdAmeritradeKeyTextBox.Text = _settings.Trading.TdAmeritradeApiKey;

        // Performance settings
        MaxConnectionsTextBox.Text = _settings.Performance.MaxConcurrentConnections.ToString();
        RequestTimeoutTextBox.Text = _settings.Performance.RequestTimeoutMs.ToString();
        EnableMultiThreadingCheckBox.IsChecked = _settings.Performance.EnableMultiThreading;
        MaxMemoryTextBox.Text = _settings.Performance.MaxMemoryUsageMB.ToString();
        EnableLoggingCheckBox.IsChecked = _settings.Performance.EnableLogging;
        LogLevelComboBox.Text = _settings.Performance.LogLevel;

        // Safety settings
        MaxPositionSizeTextBox.Text = _settings.Safety.MaxPositionSize.ToString();
        MaxDailyLossTextBox.Text = _settings.Safety.MaxDailyLoss.ToString();
        MaxOrderValueTextBox.Text = _settings.Safety.MaxOrderValue.ToString();
        EnableStopLossCheckBox.IsChecked = _settings.Safety.EnableStopLoss;
        DefaultStopLossTextBox.Text = _settings.Safety.DefaultStopLossPercent.ToString();
        EnablePositionSizingCheckBox.IsChecked = _settings.Safety.EnablePositionSizing;
        RiskPerTradeTextBox.Text = _settings.Safety.RiskPerTradePercent.ToString();

        // Advanced settings
        EnableDebugCheckBox.IsChecked = _settings.Advanced.EnableDebugMode;
        EnableBetaCheckBox.IsChecked = _settings.Advanced.EnableBetaFeatures;
        ProxyServerTextBox.Text = _settings.Advanced.ProxyServer;
        ProxyPortTextBox.Text = _settings.Advanced.ProxyPort.ToString();
        CustomDataPathTextBox.Text = _settings.Advanced.CustomDataPath;
        EnableTelemetryCheckBox.IsChecked = _settings.Advanced.EnableTelemetry;
    }

    /// <summary>
    /// Update settings model from UI controls
    /// </summary>
    private bool UpdateSettingsFromUI()
    {
        try
        {
            // Appearance settings
            _settings.Appearance.Theme = ThemeComboBox.Text;
            _settings.Appearance.ShowToolbar = ShowToolbarCheckBox.IsChecked ?? true;
            _settings.Appearance.ShowStatusBar = ShowStatusBarCheckBox.IsChecked ?? true;
            _settings.Appearance.FontSize = (int)FontSizeSlider.Value;
            _settings.Appearance.EnableAnimations = EnableAnimationsCheckBox.IsChecked ?? true;
            _settings.Appearance.ShowTooltips = ShowTooltipsCheckBox.IsChecked ?? true;
            _settings.Appearance.AccentColor = AccentColorTextBox.Text;

            // Data settings
            _settings.Data.DefaultProvider = DataProviderComboBox.Text;
            if (!int.TryParse(RefreshIntervalTextBox.Text, out int refreshInterval) || refreshInterval < 1000)
            {
                System.Windows.MessageBox.Show("Refresh interval must be at least 1000ms", "Validation Error");
                return false;
            }
            _settings.Data.RefreshIntervalMs = refreshInterval;

            if (!int.TryParse(HistoryDaysTextBox.Text, out int historyDays) || historyDays < 30)
            {
                System.Windows.MessageBox.Show("History days must be at least 30", "Validation Error");
                return false;
            }
            _settings.Data.HistoryDays = historyDays;

            _settings.Data.EnableRealTime = EnableRealTimeCheckBox.IsChecked ?? true;
            _settings.Data.EnableCaching = EnableCachingCheckBox.IsChecked ?? true;

            if (!int.TryParse(CacheRetentionTextBox.Text, out int cacheRetention) || cacheRetention < 1)
            {
                System.Windows.MessageBox.Show("Cache retention must be at least 1 day", "Validation Error");
                return false;
            }
            _settings.Data.CacheRetentionDays = cacheRetention;

            _settings.Data.AlphaVantageApiKey = AlphaVantageKeyTextBox.Text;
            _settings.Data.IexApiKey = IexKeyTextBox.Text;
            _settings.Data.PolygonApiKey = PolygonKeyTextBox.Text;
            _settings.Data.FinnhubApiKey = FinnhubKeyTextBox.Text;

            // Trading settings
            _settings.Trading.DefaultProvider = TradingProviderComboBox.Text;
            _settings.Trading.PaperTradingMode = PaperTradingCheckBox.IsChecked ?? true;
            _settings.Trading.RequireConfirmation = RequireConfirmationCheckBox.IsChecked ?? true;
            _settings.Trading.AlpacaApiKey = AlpacaApiKeyTextBox.Text;
            _settings.Trading.AlpacaSecretKey = AlpacaSecretKeyBox.Password;
            _settings.Trading.IbkrUsername = IbkrUsernameTextBox.Text;
            _settings.Trading.IbkrPassword = IbkrPasswordBox.Password;
            _settings.Trading.TdAmeritradeApiKey = TdAmeritradeKeyTextBox.Text;

            // Performance settings
            if (!int.TryParse(MaxConnectionsTextBox.Text, out int maxConnections) || maxConnections < 1)
            {
                System.Windows.MessageBox.Show("Max connections must be at least 1", "Validation Error");
                return false;
            }
            _settings.Performance.MaxConcurrentConnections = maxConnections;

            if (!int.TryParse(RequestTimeoutTextBox.Text, out int timeout) || timeout < 5000)
            {
                System.Windows.MessageBox.Show("Request timeout must be at least 5000ms", "Validation Error");
                return false;
            }
            _settings.Performance.RequestTimeoutMs = timeout;

            _settings.Performance.EnableMultiThreading = EnableMultiThreadingCheckBox.IsChecked ?? true;

            if (!int.TryParse(MaxMemoryTextBox.Text, out int maxMemory) || maxMemory < 128)
            {
                System.Windows.MessageBox.Show("Max memory must be at least 128MB", "Validation Error");
                return false;
            }
            _settings.Performance.MaxMemoryUsageMB = maxMemory;

            _settings.Performance.EnableLogging = EnableLoggingCheckBox.IsChecked ?? true;
            _settings.Performance.LogLevel = LogLevelComboBox.Text;

            // Safety settings - CRITICAL VALIDATION
            if (!decimal.TryParse(MaxPositionSizeTextBox.Text, out decimal maxPosition) || maxPosition < 100)
            {
                System.Windows.MessageBox.Show("Max position size must be at least 100", "Safety Validation Error");
                return false;
            }
            _settings.Safety.MaxPositionSize = maxPosition;

            if (!decimal.TryParse(MaxDailyLossTextBox.Text, out decimal maxLoss) || maxLoss < 100)
            {
                System.Windows.MessageBox.Show("Max daily loss must be at least 100", "Safety Validation Error");
                return false;
            }
            _settings.Safety.MaxDailyLoss = maxLoss;

            if (!decimal.TryParse(MaxOrderValueTextBox.Text, out decimal maxOrder) || maxOrder < 100)
            {
                System.Windows.MessageBox.Show("Max order value must be at least 100", "Safety Validation Error");
                return false;
            }
            _settings.Safety.MaxOrderValue = maxOrder;

            _settings.Safety.EnableStopLoss = EnableStopLossCheckBox.IsChecked ?? true;

            if (!decimal.TryParse(DefaultStopLossTextBox.Text, out decimal stopLoss) || stopLoss < 1 || stopLoss > 20)
            {
                System.Windows.MessageBox.Show("Default stop loss must be between 1% and 20%", "Safety Validation Error");
                return false;
            }
            _settings.Safety.DefaultStopLossPercent = stopLoss;

            _settings.Safety.EnablePositionSizing = EnablePositionSizingCheckBox.IsChecked ?? true;

            if (!decimal.TryParse(RiskPerTradeTextBox.Text, out decimal riskPerTrade) || riskPerTrade < 0.5m || riskPerTrade > 10)
            {
                System.Windows.MessageBox.Show("Risk per trade must be between 0.5% and 10%", "Safety Validation Error");
                return false;
            }
            _settings.Safety.RiskPerTradePercent = riskPerTrade;

            // Advanced settings
            _settings.Advanced.EnableDebugMode = EnableDebugCheckBox.IsChecked ?? false;
            _settings.Advanced.EnableBetaFeatures = EnableBetaCheckBox.IsChecked ?? false;
            _settings.Advanced.ProxyServer = ProxyServerTextBox.Text;

            if (!int.TryParse(ProxyPortTextBox.Text, out int proxyPort) || proxyPort < 1 || proxyPort > 65535)
            {
                System.Windows.MessageBox.Show("Proxy port must be between 1 and 65535", "Validation Error");
                return false;
            }
            _settings.Advanced.ProxyPort = proxyPort;

            _settings.Advanced.CustomDataPath = CustomDataPathTextBox.Text;
            _settings.Advanced.EnableTelemetry = EnableTelemetryCheckBox.IsChecked ?? false;

            return true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error validating settings: {ex.Message}", 
                           "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    #region Event Handlers

    private void BrowseDataPath_Click(object sender, RoutedEventArgs e)
    {
        // Replace WinForms FolderBrowserDialog with WPF-friendly folder picker fallback
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            CheckFileExists = false,
            CheckPathExists = true,
            FileName = "SelectFolder.placeholder", // placeholder
            Title = "Select custom data directory"
        };
        if (dlg.ShowDialog() == true)
        {
            var path = System.IO.Path.GetDirectoryName(dlg.FileName);
            if (!string.IsNullOrWhiteSpace(path))
            {
                CustomDataPathTextBox.Text = path;
            }
        }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "Are you sure you want to reset all settings to defaults?\n\nThis action cannot be undone.",
            "Reset Settings", MessageBoxButton.YesNo, MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            _settings = new SettingsModel();
            PopulateUIFromSettings();
            System.Windows.MessageBox.Show("Settings have been reset to defaults.", 
                           "Reset Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void Import_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON Settings|*.json|All Files|*.*",
            Title = "Import Settings"
        };

        if (dialog.ShowDialog() == true)
        {
            var success = await _settingsService.ImportSettingsAsync(dialog.FileName);
            if (success)
            {
                await LoadSettingsAsync();
                System.Windows.MessageBox.Show("Settings imported successfully!", 
                               "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("Failed to import settings. Please check the file format.", 
                               "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "JSON Settings|*.json|All Files|*.*",
            Title = "Export Settings",
            FileName = $"apex_v2_settings_{DateTime.Now:yyyyMMdd}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var success = await _settingsService.ExportSettingsAsync(dialog.FileName);
            if (success)
            {
                System.Windows.MessageBox.Show("Settings exported successfully!", 
                               "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("Failed to export settings.", 
                               "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void OK_Click(object sender, RoutedEventArgs e)
    {
        if (!UpdateSettingsFromUI())
        {
            return; // Validation failed
        }
        try
        {
            var success = await _settingsService.SaveSettingsAsync(_settings);
            if (success)
            {
                App.LogManager.UpdateFromSettings(_settings.Performance.EnableLogging, _settings.Performance.LogLevel);
                DialogResult = true;
                Close();
            }
            else
            {
                System.Windows.MessageBox.Show("Failed to save settings to database.", 
                               "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Error saving settings: {ex.Message}", 
                           "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _settingsService.SaveSettingsAsync(_settings);
            // Apply theme with potential accent override
            App.ThemeManager.ApplyTheme(_settings.Appearance.Theme, persist: true, overrideAccentHex: _settings.Appearance.AccentColor);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Failed to save settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion
}