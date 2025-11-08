using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ApexV2.Extensions.Plugins
{
    // Base Plugin Interface
    public interface IPlugin : IDisposable
    {
        PluginInfo Info { get; }
        PluginStatus Status { get; }
        
        Task<bool> InitializeAsync(IPluginContext context);
        Task<bool> StartAsync();
        Task<bool> StopAsync();
        Task<bool> ShutdownAsync();
        
        Task<object?> ExecuteAsync(string command, Dictionary<string, object>? parameters = null);
        Task<bool> ConfigureAsync(Dictionary<string, object> settings);
        
        event EventHandler<PluginEventArgs>? StatusChanged;
        event EventHandler<PluginEventArgs>? ErrorOccurred;
    }

    // Plugin Context Interface
    public interface IPluginContext
    {
        // Core Services
        IPluginLogger Logger { get; }
        IPluginConfiguration Configuration { get; }
        IPluginSecurity Security { get; }
        IPluginCommunication Communication { get; }
        
        // Market Data Access
        IMarketDataAccess? MarketData { get; }
        IFundamentalDataAccess? FundamentalData { get; }
        
        // User Interface Access
        IUIAccess? UIAccess { get; }
        IChartAccess? ChartAccess { get; }
        
        // Database Access
        IDatabaseAccess? DatabaseAccess { get; }
        
        // File System Access
        IFileSystemAccess? FileSystemAccess { get; }
        
        // Network Access
        INetworkAccess? NetworkAccess { get; }
        
        // Plugin Information
        PluginInfo PluginInfo { get; }
        PluginPermissions GrantedPermissions { get; }
        PluginSecurityContext SecurityContext { get; }
        
        // Application Information
        Version ApexVersion { get; }
        string ApplicationPath { get; }
        string PluginDirectory { get; }
        string DataDirectory { get; }
    }

    // Plugin Manager Interface
    public interface IPluginManager
    {
        // Plugin Lifecycle
        Task<PluginLoadResult> LoadPluginAsync(string filePath);
        Task<bool> UnloadPluginAsync(Guid pluginId);
        Task<bool> EnablePluginAsync(Guid pluginId);
        Task<bool> DisablePluginAsync(Guid pluginId);
        Task<bool> ReloadPluginAsync(Guid pluginId);
        
        // Plugin Information
        IReadOnlyList<PluginInstance> GetLoadedPlugins();
        PluginInstance? GetPlugin(Guid pluginId);
        IReadOnlyList<PluginInstance> GetPluginsByType(PluginType type);
        
        // Plugin Discovery
        Task<List<string>> DiscoverPluginsAsync(string directory);
        Task<PluginValidationResult> ValidatePluginAsync(string filePath);
        
        // Plugin Configuration
        Task<bool> UpdatePluginConfigurationAsync(Guid pluginId, PluginConfiguration configuration);
        PluginConfiguration? GetPluginConfiguration(Guid pluginId);
        
        // Plugin Communication
        Task<object?> SendMessageToPluginAsync(Guid pluginId, string command, Dictionary<string, object>? parameters = null);
        Task BroadcastMessageAsync(string command, Dictionary<string, object>? parameters = null);
        
        // Events
        event EventHandler<PluginEventArgs>? PluginLoaded;
        event EventHandler<PluginEventArgs>? PluginUnloaded;
        event EventHandler<PluginEventArgs>? PluginError;
        event EventHandler<PluginEventArgs>? PluginStatusChanged;
    }

    // Plugin Logger Interface
    public interface IPluginLogger
    {
        void LogDebug(string message, params object[] args);
        void LogInfo(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogError(string message, Exception? exception = null, params object[] args);
        void LogCritical(string message, Exception? exception = null, params object[] args);
        
        Task LogAsync(LogLevel level, string message, Exception? exception = null, params object[] args);
        
        bool IsEnabled(LogLevel level);
    }

    // Plugin Configuration Interface
    public interface IPluginConfiguration
    {
        T? GetSetting<T>(string key, T? defaultValue = default);
        Task SetSettingAsync<T>(string key, T value);
        Task<bool> RemoveSettingAsync(string key);
        Task<Dictionary<string, object>> GetAllSettingsAsync();
        Task SaveAsync();
        
        event EventHandler<string>? SettingChanged;
    }

    // Plugin Security Interface
    public interface IPluginSecurity
    {
        bool HasPermission(PluginPermissions permission);
        Task<bool> RequestPermissionAsync(PluginPermissions permission, string reason);
        bool IsUrlAllowed(string url);
        bool IsDirectoryAllowed(string path);
        bool ValidateSignature(byte[] data, byte[] signature);
        
        PluginSecurityContext SecurityContext { get; }
    }

    // Plugin Communication Interface
    public interface IPluginCommunication
    {
        Task<object?> SendMessageAsync(Guid targetPluginId, string command, Dictionary<string, object>? parameters = null);
        Task BroadcastAsync(string command, Dictionary<string, object>? parameters = null);
        Task SubscribeAsync(string eventName, Func<Dictionary<string, object>, Task> handler);
        Task UnsubscribeAsync(string eventName);
        
        event EventHandler<PluginMessageEventArgs>? MessageReceived;
    }

    // Market Data Access Interface
    public interface IMarketDataAccess
    {
        Task<decimal?> GetCurrentPriceAsync(string symbol);
        Task<List<object>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate);
        Task<Dictionary<string, decimal>> GetCurrentPricesAsync(IEnumerable<string> symbols);
        Task SubscribeToRealTimeDataAsync(string symbol, Action<object> callback);
        Task UnsubscribeFromRealTimeDataAsync(string symbol);
        
        bool HasPermission { get; }
    }

    // Fundamental Data Access Interface
    public interface IFundamentalDataAccess
    {
        Task<object?> GetFundamentalsAsync(string symbol);
        Task<object?> GetFinancialStatementsAsync(string symbol);
        Task<object?> GetEarningsDataAsync(string symbol);
        Task<object?> GetAnalystRatingsAsync(string symbol);
        
        bool HasPermission { get; }
    }

    // UI Access Interface
    public interface IUIAccess
    {
        Task<bool> ShowNotificationAsync(string title, string message, NotificationType type = NotificationType.Info);
        Task<bool?> ShowDialogAsync(string title, string message, DialogType type = DialogType.YesNo);
        Task<object?> ShowCustomDialogAsync(object dialogContent);
        Task<bool> AddMenuItemAsync(string path, string text, Action callback);
        Task<bool> RemoveMenuItemAsync(string path);
        Task<bool> AddToolbarButtonAsync(string group, string text, string? iconPath, Action callback);
        
        bool HasPermission { get; }
    }

    // Chart Access Interface
    public interface IChartAccess
    {
        Task<bool> AddIndicatorToChartAsync(Guid chartId, object indicator);
        Task<bool> RemoveIndicatorFromChartAsync(Guid chartId, Guid indicatorId);
        Task<bool> AddDrawingToChartAsync(Guid chartId, object drawing);
        Task<bool> SetChartSymbolAsync(Guid chartId, string symbol);
        Task<object?> GetChartDataAsync(Guid chartId);
        
        bool HasPermission { get; }
    }

    // Database Access Interface
    public interface IDatabaseAccess
    {
        Task<T?> ExecuteScalarAsync<T>(string sql, Dictionary<string, object>? parameters = null);
        Task<List<Dictionary<string, object>>> ExecuteQueryAsync(string sql, Dictionary<string, object>? parameters = null);
        Task<int> ExecuteNonQueryAsync(string sql, Dictionary<string, object>? parameters = null);
        Task<bool> TableExistsAsync(string tableName);
        Task<bool> CreateTableAsync(string tableName, Dictionary<string, string> columns);
        
        bool HasPermission { get; }
    }

    // File System Access Interface
    public interface IFileSystemAccess
    {
        Task<string?> ReadTextFileAsync(string path);
        Task<byte[]?> ReadBinaryFileAsync(string path);
        Task<bool> WriteTextFileAsync(string path, string content);
        Task<bool> WriteBinaryFileAsync(string path, byte[] data);
        Task<bool> FileExistsAsync(string path);
        Task<bool> DirectoryExistsAsync(string path);
        Task<bool> CreateDirectoryAsync(string path);
        Task<List<string>> GetFilesAsync(string directory, string pattern = "*");
        
        bool HasPermission { get; }
        List<string> AllowedDirectories { get; }
    }

    // Network Access Interface
    public interface INetworkAccess
    {
        Task<string?> GetAsync(string url, Dictionary<string, string>? headers = null);
        Task<string?> PostAsync(string url, string content, Dictionary<string, string>? headers = null);
        Task<byte[]?> DownloadAsync(string url);
        Task<bool> UploadAsync(string url, byte[] data, string contentType);
        
        bool HasPermission { get; }
        List<string> AllowedUrls { get; }
    }

    // Specialized Plugin Interfaces

    // Custom Indicator Plugin
    public interface IIndicatorPlugin : IPlugin
    {
        string IndicatorName { get; }
        string[] InputParameters { get; }
        string[] OutputSeries { get; }
        
        Task<Dictionary<string, decimal[]>> CalculateAsync(decimal[] prices, Dictionary<string, object> parameters);
        Task<bool> ValidateParametersAsync(Dictionary<string, object> parameters);
    }

    // Data Provider Plugin
    public interface IDataProviderPlugin : IPlugin
    {
        string ProviderName { get; }
        string[] SupportedMarkets { get; }
        bool SupportsRealTime { get; }
        bool SupportsHistorical { get; }
        
        Task<decimal?> GetQuoteAsync(string symbol);
        Task<List<object>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate, string timeframe);
        Task<bool> SubscribeToRealTimeAsync(string symbol, Action<object> callback);
    }

    // UI Panel Plugin
    public interface IUIPanelPlugin : IPlugin
    {
        string PanelTitle { get; }
        object PanelContent { get; }
        bool CanDock { get; }
        
        Task<object> CreatePanelAsync();
        Task<bool> RefreshAsync();
        Task<bool> SaveStateAsync(Dictionary<string, object> state);
        Task<bool> LoadStateAsync(Dictionary<string, object> state);
    }

    // Scanner Plugin
    public interface IScannerPlugin : IPlugin
    {
        string ScannerName { get; }
        string[] ScanCriteria { get; }
        
        Task<List<string>> ScanAsync(Dictionary<string, object> criteria, List<string> symbols);
        Task<bool> ValidateCriteriaAsync(Dictionary<string, object> criteria);
    }

    // Supporting Enums and Classes
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    public enum NotificationType
    {
        Info,
        Warning,
        Error,
        Success
    }

    public enum DialogType
    {
        Ok,
        YesNo,
        YesNoCancel,
        OkCancel
    }

    public class PluginMessageEventArgs : EventArgs
    {
        public Guid SourcePluginId { get; }
        public string Command { get; }
        public Dictionary<string, object> Parameters { get; }
        public DateTime Timestamp { get; }

        public PluginMessageEventArgs(Guid sourcePluginId, string command, Dictionary<string, object> parameters)
        {
            SourcePluginId = sourcePluginId;
            Command = command;
            Parameters = parameters;
            Timestamp = DateTime.UtcNow;
        }
    }
}
