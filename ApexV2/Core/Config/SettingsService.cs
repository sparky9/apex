using System.Text.Json;
using System.IO;
using ApexV2.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace ApexV2.Core.Config;

/// <summary>
/// Service for managing application settings persistence
/// </summary>
public class SettingsService
{
    private readonly DatabaseService _databaseService;
    private SettingsModel _cachedSettings;
    private DateTime _lastCacheTime;
    private readonly TimeSpan _cacheValidTime = TimeSpan.FromMinutes(5);

    public SettingsService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
        _cachedSettings = new SettingsModel();
    }

    /// <summary>
    /// Load all settings from database
    /// </summary>
    public async Task<SettingsModel> LoadSettingsAsync()
    {
        // Return cached settings if still valid
        if (DateTime.Now - _lastCacheTime < _cacheValidTime)
        {
            return _cachedSettings;
        }

        try
        {
            using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
            
            var settingsEntities = await context.UserSettings.ToListAsync();
            var settings = new SettingsModel();

            // Load settings by category
            LoadAppearanceSettings(settings.Appearance, settingsEntities);
            LoadDataSettings(settings.Data, settingsEntities);
            LoadTradingSettings(settings.Trading, settingsEntities);
            LoadPerformanceSettings(settings.Performance, settingsEntities);
            LoadSafetySettings(settings.Safety, settingsEntities);
            LoadAdvancedSettings(settings.Advanced, settingsEntities);

            _cachedSettings = settings;
            _lastCacheTime = DateTime.Now;
            
            return settings;
        }
        catch (Exception ex)
        {
            // Return default settings if load fails
            Console.WriteLine($"Failed to load settings: {ex.Message}");
            return new SettingsModel();
        }
    }

    /// <summary>
    /// Save all settings to database
    /// </summary>
    public async Task<bool> SaveSettingsAsync(SettingsModel settings)
    {
        try
        {
            using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
            
            // Delete existing settings (we'll replace them)
            var existingSettings = await context.UserSettings.ToListAsync();
            context.UserSettings.RemoveRange(existingSettings);

            // Save all setting categories
            await SaveAppearanceSettings(context, settings.Appearance);
            await SaveDataSettings(context, settings.Data);
            await SaveTradingSettings(context, settings.Trading);
            await SavePerformanceSettings(context, settings.Performance);
            await SaveSafetySettings(context, settings.Safety);
            await SaveAdvancedSettings(context, settings.Advanced);

            await context.SaveChangesAsync();

            // Update cache
            _cachedSettings = settings;
            _lastCacheTime = DateTime.Now;

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to save settings: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Get a specific setting value
    /// </summary>
    public async Task<T?> GetSettingAsync<T>(string category, string key, T? defaultValue = default)
    {
        try
        {
            using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
            
            var setting = await context.UserSettings
                .FirstOrDefaultAsync(s => s.Category == category && s.Key == key);

            if (setting == null) return defaultValue;

            return JsonSerializer.Deserialize<T>(setting.Value) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    /// <summary>
    /// Set a specific setting value
    /// </summary>
    public async Task<bool> SetSettingAsync<T>(string category, string key, T value)
    {
        try
        {
            using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
            
            var setting = await context.UserSettings
                .FirstOrDefaultAsync(s => s.Category == category && s.Key == key);

            var jsonValue = JsonSerializer.Serialize(value);

            if (setting == null)
            {
                setting = new UserSettingsEntity
                {
                    Category = category,
                    Key = key,
                    Value = jsonValue,
                    LastModified = DateTime.Now
                };
                context.UserSettings.Add(setting);
            }
            else
            {
                setting.Value = jsonValue;
                setting.LastModified = DateTime.Now;
            }

            await context.SaveChangesAsync();
            
            // Invalidate cache
            _lastCacheTime = DateTime.MinValue;
            
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to set setting: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Reset all settings to defaults
    /// </summary>
    public async Task<bool> ResetToDefaultsAsync()
    {
        var defaultSettings = new SettingsModel();
        return await SaveSettingsAsync(defaultSettings);
    }

    /// <summary>
    /// Export settings to JSON file
    /// </summary>
    public async Task<bool> ExportSettingsAsync(string filePath)
    {
        try
        {
            var settings = await LoadSettingsAsync();
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to export settings: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Import settings from JSON file
    /// </summary>
    public async Task<bool> ImportSettingsAsync(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return false;

            var json = await File.ReadAllTextAsync(filePath);
            var settings = JsonSerializer.Deserialize<SettingsModel>(json);
            
            if (settings != null)
            {
                return await SaveSettingsAsync(settings);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to import settings: {ex.Message}");
            return false;
        }
    }

    #region Private Helper Methods

    private void LoadAppearanceSettings(AppearanceSettings appearance, List<UserSettingsEntity> entities)
    {
        appearance.Theme = GetSettingValue(entities, "Appearance", "Theme", appearance.Theme);
        appearance.ShowToolbar = GetSettingValue(entities, "Appearance", "ShowToolbar", appearance.ShowToolbar);
        appearance.ShowStatusBar = GetSettingValue(entities, "Appearance", "ShowStatusBar", appearance.ShowStatusBar);
        appearance.FontSize = GetSettingValue(entities, "Appearance", "FontSize", appearance.FontSize);
        appearance.EnableAnimations = GetSettingValue(entities, "Appearance", "EnableAnimations", appearance.EnableAnimations);
        appearance.ShowTooltips = GetSettingValue(entities, "Appearance", "ShowTooltips", appearance.ShowTooltips);
        appearance.AccentColor = GetSettingValue(entities, "Appearance", "AccentColor", appearance.AccentColor);
    }

    private void LoadDataSettings(DataSettings data, List<UserSettingsEntity> entities)
    {
        data.DefaultProvider = GetSettingValue(entities, "Data", "DefaultProvider", data.DefaultProvider);
        data.RefreshIntervalMs = GetSettingValue(entities, "Data", "RefreshIntervalMs", data.RefreshIntervalMs);
        data.HistoryDays = GetSettingValue(entities, "Data", "HistoryDays", data.HistoryDays);
        data.EnableRealTime = GetSettingValue(entities, "Data", "EnableRealTime", data.EnableRealTime);
        data.EnableCaching = GetSettingValue(entities, "Data", "EnableCaching", data.EnableCaching);
        data.CacheRetentionDays = GetSettingValue(entities, "Data", "CacheRetentionDays", data.CacheRetentionDays);
        data.AlphaVantageApiKey = GetSettingValue(entities, "Data", "AlphaVantageApiKey", data.AlphaVantageApiKey);
        data.IexApiKey = GetSettingValue(entities, "Data", "IexApiKey", data.IexApiKey);
        data.PolygonApiKey = GetSettingValue(entities, "Data", "PolygonApiKey", data.PolygonApiKey);
        data.FinnhubApiKey = GetSettingValue(entities, "Data", "FinnhubApiKey", data.FinnhubApiKey);
    }

    private void LoadTradingSettings(TradingSettings trading, List<UserSettingsEntity> entities)
    {
        trading.DefaultProvider = GetSettingValue(entities, "Trading", "DefaultProvider", trading.DefaultProvider);
        trading.PaperTradingMode = GetSettingValue(entities, "Trading", "PaperTradingMode", trading.PaperTradingMode);
        trading.RequireConfirmation = GetSettingValue(entities, "Trading", "RequireConfirmation", trading.RequireConfirmation);
        trading.AlpacaApiKey = GetSettingValue(entities, "Trading", "AlpacaApiKey", trading.AlpacaApiKey);
        trading.AlpacaSecretKey = GetSettingValue(entities, "Trading", "AlpacaSecretKey", trading.AlpacaSecretKey);
        trading.IbkrUsername = GetSettingValue(entities, "Trading", "IbkrUsername", trading.IbkrUsername);
        trading.IbkrPassword = GetSettingValue(entities, "Trading", "IbkrPassword", trading.IbkrPassword);
        trading.TdAmeritradeApiKey = GetSettingValue(entities, "Trading", "TdAmeritradeApiKey", trading.TdAmeritradeApiKey);
    }

    private void LoadPerformanceSettings(PerformanceSettings performance, List<UserSettingsEntity> entities)
    {
        performance.MaxConcurrentConnections = GetSettingValue(entities, "Performance", "MaxConcurrentConnections", performance.MaxConcurrentConnections);
        performance.RequestTimeoutMs = GetSettingValue(entities, "Performance", "RequestTimeoutMs", performance.RequestTimeoutMs);
        performance.EnableMultiThreading = GetSettingValue(entities, "Performance", "EnableMultiThreading", performance.EnableMultiThreading);
        performance.MaxMemoryUsageMB = GetSettingValue(entities, "Performance", "MaxMemoryUsageMB", performance.MaxMemoryUsageMB);
        performance.EnableLogging = GetSettingValue(entities, "Performance", "EnableLogging", performance.EnableLogging);
        performance.LogLevel = GetSettingValue(entities, "Performance", "LogLevel", performance.LogLevel);
    }

    private void LoadSafetySettings(SafetySettings safety, List<UserSettingsEntity> entities)
    {
        safety.MaxPositionSize = GetSettingValue(entities, "Safety", "MaxPositionSize", safety.MaxPositionSize);
        safety.MaxDailyLoss = GetSettingValue(entities, "Safety", "MaxDailyLoss", safety.MaxDailyLoss);
        safety.MaxOrderValue = GetSettingValue(entities, "Safety", "MaxOrderValue", safety.MaxOrderValue);
        safety.EnableStopLoss = GetSettingValue(entities, "Safety", "EnableStopLoss", safety.EnableStopLoss);
        safety.DefaultStopLossPercent = GetSettingValue(entities, "Safety", "DefaultStopLossPercent", safety.DefaultStopLossPercent);
        safety.EnablePositionSizing = GetSettingValue(entities, "Safety", "EnablePositionSizing", safety.EnablePositionSizing);
        safety.RiskPerTradePercent = GetSettingValue(entities, "Safety", "RiskPerTradePercent", safety.RiskPerTradePercent);
    }

    private void LoadAdvancedSettings(AdvancedSettings advanced, List<UserSettingsEntity> entities)
    {
        advanced.EnableDebugMode = GetSettingValue(entities, "Advanced", "EnableDebugMode", advanced.EnableDebugMode);
        advanced.EnableBetaFeatures = GetSettingValue(entities, "Advanced", "EnableBetaFeatures", advanced.EnableBetaFeatures);
        advanced.ProxyServer = GetSettingValue(entities, "Advanced", "ProxyServer", advanced.ProxyServer);
        advanced.ProxyPort = GetSettingValue(entities, "Advanced", "ProxyPort", advanced.ProxyPort);
        advanced.CustomDataPath = GetSettingValue(entities, "Advanced", "CustomDataPath", advanced.CustomDataPath);
        advanced.EnableTelemetry = GetSettingValue(entities, "Advanced", "EnableTelemetry", advanced.EnableTelemetry);
    }

    private T GetSettingValue<T>(List<UserSettingsEntity> entities, string category, string key, T defaultValue)
    {
        var entity = entities.FirstOrDefault(e => e.Category == category && e.Key == key);
        if (entity == null) return defaultValue;

        try
        {
            return JsonSerializer.Deserialize<T>(entity.Value) ?? defaultValue;
        }
        catch
        {
            return defaultValue;
        }
    }

    private async Task SaveAppearanceSettings(ApexDbContext context, AppearanceSettings appearance)
    {
        await SaveSetting(context, "Appearance", "Theme", appearance.Theme);
        await SaveSetting(context, "Appearance", "ShowToolbar", appearance.ShowToolbar);
        await SaveSetting(context, "Appearance", "ShowStatusBar", appearance.ShowStatusBar);
        await SaveSetting(context, "Appearance", "FontSize", appearance.FontSize);
        await SaveSetting(context, "Appearance", "EnableAnimations", appearance.EnableAnimations);
        await SaveSetting(context, "Appearance", "ShowTooltips", appearance.ShowTooltips);
        await SaveSetting(context, "Appearance", "AccentColor", appearance.AccentColor);
    }

    private async Task SaveDataSettings(ApexDbContext context, DataSettings data)
    {
        await SaveSetting(context, "Data", "DefaultProvider", data.DefaultProvider);
        await SaveSetting(context, "Data", "RefreshIntervalMs", data.RefreshIntervalMs);
        await SaveSetting(context, "Data", "HistoryDays", data.HistoryDays);
        await SaveSetting(context, "Data", "EnableRealTime", data.EnableRealTime);
        await SaveSetting(context, "Data", "EnableCaching", data.EnableCaching);
        await SaveSetting(context, "Data", "CacheRetentionDays", data.CacheRetentionDays);
        await SaveSetting(context, "Data", "AlphaVantageApiKey", data.AlphaVantageApiKey);
        await SaveSetting(context, "Data", "IexApiKey", data.IexApiKey);
        await SaveSetting(context, "Data", "PolygonApiKey", data.PolygonApiKey);
        await SaveSetting(context, "Data", "FinnhubApiKey", data.FinnhubApiKey);
    }

    private async Task SaveTradingSettings(ApexDbContext context, TradingSettings trading)
    {
        await SaveSetting(context, "Trading", "DefaultProvider", trading.DefaultProvider);
        await SaveSetting(context, "Trading", "PaperTradingMode", trading.PaperTradingMode);
        await SaveSetting(context, "Trading", "RequireConfirmation", trading.RequireConfirmation);
        await SaveSetting(context, "Trading", "AlpacaApiKey", trading.AlpacaApiKey);
        await SaveSetting(context, "Trading", "AlpacaSecretKey", trading.AlpacaSecretKey);
        await SaveSetting(context, "Trading", "IbkrUsername", trading.IbkrUsername);
        await SaveSetting(context, "Trading", "IbkrPassword", trading.IbkrPassword);
        await SaveSetting(context, "Trading", "TdAmeritradeApiKey", trading.TdAmeritradeApiKey);
    }

    private async Task SavePerformanceSettings(ApexDbContext context, PerformanceSettings performance)
    {
        await SaveSetting(context, "Performance", "MaxConcurrentConnections", performance.MaxConcurrentConnections);
        await SaveSetting(context, "Performance", "RequestTimeoutMs", performance.RequestTimeoutMs);
        await SaveSetting(context, "Performance", "EnableMultiThreading", performance.EnableMultiThreading);
        await SaveSetting(context, "Performance", "MaxMemoryUsageMB", performance.MaxMemoryUsageMB);
        await SaveSetting(context, "Performance", "EnableLogging", performance.EnableLogging);
        await SaveSetting(context, "Performance", "LogLevel", performance.LogLevel);
    }

    private async Task SaveSafetySettings(ApexDbContext context, SafetySettings safety)
    {
        await SaveSetting(context, "Safety", "MaxPositionSize", safety.MaxPositionSize);
        await SaveSetting(context, "Safety", "MaxDailyLoss", safety.MaxDailyLoss);
        await SaveSetting(context, "Safety", "MaxOrderValue", safety.MaxOrderValue);
        await SaveSetting(context, "Safety", "EnableStopLoss", safety.EnableStopLoss);
        await SaveSetting(context, "Safety", "DefaultStopLossPercent", safety.DefaultStopLossPercent);
        await SaveSetting(context, "Safety", "EnablePositionSizing", safety.EnablePositionSizing);
        await SaveSetting(context, "Safety", "RiskPerTradePercent", safety.RiskPerTradePercent);
    }

    private async Task SaveAdvancedSettings(ApexDbContext context, AdvancedSettings advanced)
    {
        await SaveSetting(context, "Advanced", "EnableDebugMode", advanced.EnableDebugMode);
        await SaveSetting(context, "Advanced", "EnableBetaFeatures", advanced.EnableBetaFeatures);
        await SaveSetting(context, "Advanced", "ProxyServer", advanced.ProxyServer);
        await SaveSetting(context, "Advanced", "ProxyPort", advanced.ProxyPort);
        await SaveSetting(context, "Advanced", "CustomDataPath", advanced.CustomDataPath);
        await SaveSetting(context, "Advanced", "EnableTelemetry", advanced.EnableTelemetry);
    }

    private async Task SaveSetting<T>(ApexDbContext context, string category, string key, T value)
    {
        var entity = new UserSettingsEntity
        {
            Category = category,
            Key = key,
            Value = JsonSerializer.Serialize(value),
            LastModified = DateTime.Now
        };
        
        context.UserSettings.Add(entity);
        await Task.CompletedTask; // For consistency with async pattern
    }

    #endregion
}