using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace ApexV2.Core.Config;

/// <summary>
/// Comprehensive settings model for APEX V2
/// </summary>
public class SettingsModel : INotifyPropertyChanged
{
    public AppearanceSettings Appearance { get; set; } = new();
    public DataSettings Data { get; set; } = new();
    public TradingSettings Trading { get; set; } = new();
    public PerformanceSettings Performance { get; set; } = new();
    public SafetySettings Safety { get; set; } = new();
    public AdvancedSettings Advanced { get; set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// UI Theme and appearance settings
/// </summary>
public class AppearanceSettings : INotifyPropertyChanged
{
    private string _theme = "Dark";
    private bool _showToolbar = true;
    private bool _showStatusBar = true;
    private int _fontSize = 12;
    private bool _enableAnimations = true;
    private bool _showTooltips = true;
    private string _accentColor = "#4A9EFF";

    [Display(Name = "Theme")]
    [Description("Visual theme for the application")]
    public string Theme
    {
        get => _theme;
        set { _theme = value; OnPropertyChanged(nameof(Theme)); }
    }

    [Display(Name = "Show Toolbar")]
    [Description("Display the main toolbar")]
    public bool ShowToolbar
    {
        get => _showToolbar;
        set { _showToolbar = value; OnPropertyChanged(nameof(ShowToolbar)); }
    }

    [Display(Name = "Show Status Bar")]
    [Description("Display the status bar at the bottom")]
    public bool ShowStatusBar
    {
        get => _showStatusBar;
        set { _showStatusBar = value; OnPropertyChanged(nameof(ShowStatusBar)); }
    }

    [Display(Name = "Font Size")]
    [Description("Base font size for the interface")]
    [Range(8, 24)]
    public int FontSize
    {
        get => _fontSize;
        set { _fontSize = value; OnPropertyChanged(nameof(FontSize)); }
    }

    [Display(Name = "Enable Animations")]
    [Description("Enable UI animations and transitions")]
    public bool EnableAnimations
    {
        get => _enableAnimations;
        set { _enableAnimations = value; OnPropertyChanged(nameof(EnableAnimations)); }
    }

    [Display(Name = "Show Tooltips")]
    [Description("Show helpful tooltips on hover")]
    public bool ShowTooltips
    {
        get => _showTooltips;
        set { _showTooltips = value; OnPropertyChanged(nameof(ShowTooltips)); }
    }

    [Display(Name = "Accent Color")]
    [Description("Primary accent color for highlights")]
    public string AccentColor
    {
        get => _accentColor;
        set { _accentColor = value; OnPropertyChanged(nameof(AccentColor)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Market data and provider settings
/// </summary>
public class DataSettings : INotifyPropertyChanged
{
    private string _defaultProvider = "YahooFinance";
    private int _refreshIntervalMs = 5000;
    private int _historyDays = 365;
    private bool _enableRealTime = true;
    private bool _enableCaching = true;
    private int _cacheRetentionDays = 30;
    private string _alphaVantageApiKey = "";
    private string _iexApiKey = "";
    private string _polygonApiKey = "";
    private string _finnhubApiKey = "";
    private int _maxInMemoryBarsPerSymbol = 5000;
    private int _cacheFlushIntervalMs = 2000;
    private int _cacheImmediateFlushThreshold = 500;
    private int _intradayBarIntervalSec = 60;
    private int _intradayRetentionDays = 7;

    [Display(Name = "Default Provider")]
    [Description("Primary market data provider")]
    public string DefaultProvider
    {
        get => _defaultProvider;
        set { _defaultProvider = value; OnPropertyChanged(nameof(DefaultProvider)); }
    }

    [Display(Name = "Refresh Interval (ms)")]
    [Description("How often to refresh market data")]
    [Range(1000, 60000)]
    public int RefreshIntervalMs
    {
        get => _refreshIntervalMs;
        set { _refreshIntervalMs = value; OnPropertyChanged(nameof(RefreshIntervalMs)); }
    }

    [Display(Name = "History Days")]
    [Description("Default days of historical data to load")]
    [Range(30, 3650)]
    public int HistoryDays
    {
        get => _historyDays;
        set { _historyDays = value; OnPropertyChanged(nameof(HistoryDays)); }
    }

    [Display(Name = "Enable Real-Time")]
    [Description("Enable real-time data streaming")]
    public bool EnableRealTime
    {
        get => _enableRealTime;
        set { _enableRealTime = value; OnPropertyChanged(nameof(EnableRealTime)); }
    }

    [Display(Name = "Enable Caching")]
    [Description("Cache market data locally for performance")]
    public bool EnableCaching
    {
        get => _enableCaching;
        set { _enableCaching = value; OnPropertyChanged(nameof(EnableCaching)); }
    }

    [Display(Name = "Cache Retention (days)")]
    [Description("How long to keep cached data")]
    [Range(1, 365)]
    public int CacheRetentionDays
    {
        get => _cacheRetentionDays;
        set { _cacheRetentionDays = value; OnPropertyChanged(nameof(CacheRetentionDays)); }
    }

    [Display(Name = "Alpha Vantage API Key")]
    [Description("API key for Alpha Vantage data provider")]
    public string AlphaVantageApiKey
    {
        get => _alphaVantageApiKey;
        set { _alphaVantageApiKey = value; OnPropertyChanged(nameof(AlphaVantageApiKey)); }
    }

    [Display(Name = "IEX Cloud API Key")]
    [Description("API key for IEX Cloud data provider")]
    public string IexApiKey
    {
        get => _iexApiKey;
        set { _iexApiKey = value; OnPropertyChanged(nameof(IexApiKey)); }
    }

    [Display(Name = "Polygon.io API Key")]
    [Description("API key for Polygon.io data provider")]
    public string PolygonApiKey
    {
        get => _polygonApiKey;
        set { _polygonApiKey = value; OnPropertyChanged(nameof(PolygonApiKey)); }
    }

    [Display(Name = "Finnhub API Key")]
    [Description("API key for Finnhub data provider")]
    public string FinnhubApiKey
    {
        get => _finnhubApiKey;
        set { _finnhubApiKey = value; OnPropertyChanged(nameof(FinnhubApiKey)); }
    }

    [Display(Name = "Max In-Memory Bars / Symbol")]
    [Description("Maximum intraday bars held in RAM per symbol before oldest trimmed")] 
    [Range(100, 100000)]
    public int MaxInMemoryBarsPerSymbol { get => _maxInMemoryBarsPerSymbol; set { _maxInMemoryBarsPerSymbol = value; OnPropertyChanged(nameof(MaxInMemoryBarsPerSymbol)); } }

    [Display(Name = "Cache Flush Interval (ms)")]
    [Description("Interval for flushing pending intraday bars to database")] 
    [Range(250, 60000)]
    public int CacheFlushIntervalMs { get => _cacheFlushIntervalMs; set { _cacheFlushIntervalMs = value; OnPropertyChanged(nameof(CacheFlushIntervalMs)); } }

    [Display(Name = "Immediate Flush Threshold")]
    [Description("Pending bar count that triggers immediate flush")] 
    [Range(50, 10000)]
    public int CacheImmediateFlushThreshold { get => _cacheImmediateFlushThreshold; set { _cacheImmediateFlushThreshold = value; OnPropertyChanged(nameof(CacheImmediateFlushThreshold)); } }

    [Display(Name = "Intraday Bar Interval (sec)")]
    [Description("Aggregation interval for intraday bars")] 
    [Range(15, 3600)]
    public int IntradayBarIntervalSec { get => _intradayBarIntervalSec; set { _intradayBarIntervalSec = value; OnPropertyChanged(nameof(IntradayBarIntervalSec)); } }

    [Display(Name = "Intraday Retention (days)")]
    [Description("How many days of intraday data to retain before pruning")] 
    [Range(1, 90)]
    public int IntradayRetentionDays { get => _intradayRetentionDays; set { _intradayRetentionDays = value; OnPropertyChanged(nameof(IntradayRetentionDays)); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Trading provider and connection settings
/// </summary>
public class TradingSettings : INotifyPropertyChanged
{
    private string _defaultProvider = "Alpaca";
    private bool _paperTradingMode = true;
    private bool _requireConfirmation = true;
    private string _alpacaApiKey = "";
    private string _alpacaSecretKey = "";
    private string _ibkrUsername = "";
    private string _ibkrPassword = "";
    private string _tdAmeritradeApiKey = "";

    [Display(Name = "Default Provider")]
    [Description("Primary trading provider")]
    public string DefaultProvider
    {
        get => _defaultProvider;
        set { _defaultProvider = value; OnPropertyChanged(nameof(DefaultProvider)); }
    }

    [Display(Name = "Paper Trading Mode")]
    [Description("Use paper trading by default (RECOMMENDED)")]
    public bool PaperTradingMode
    {
        get => _paperTradingMode;
        set { _paperTradingMode = value; OnPropertyChanged(nameof(PaperTradingMode)); }
    }

    [Display(Name = "Require Order Confirmation")]
    [Description("Show confirmation dialog before placing orders")]
    public bool RequireConfirmation
    {
        get => _requireConfirmation;
        set { _requireConfirmation = value; OnPropertyChanged(nameof(RequireConfirmation)); }
    }

    [Display(Name = "Alpaca API Key")]
    [Description("API key for Alpaca Markets")]
    public string AlpacaApiKey
    {
        get => _alpacaApiKey;
        set { _alpacaApiKey = value; OnPropertyChanged(nameof(AlpacaApiKey)); }
    }

    [Display(Name = "Alpaca Secret Key")]
    [Description("Secret key for Alpaca Markets")]
    public string AlpacaSecretKey
    {
        get => _alpacaSecretKey;
        set { _alpacaSecretKey = value; OnPropertyChanged(nameof(AlpacaSecretKey)); }
    }

    [Display(Name = "IBKR Username")]
    [Description("Interactive Brokers username")]
    public string IbkrUsername
    {
        get => _ibkrUsername;
        set { _ibkrUsername = value; OnPropertyChanged(nameof(IbkrUsername)); }
    }

    [Display(Name = "IBKR Password")]
    [Description("Interactive Brokers password")]
    public string IbkrPassword
    {
        get => _ibkrPassword;
        set { _ibkrPassword = value; OnPropertyChanged(nameof(IbkrPassword)); }
    }

    [Display(Name = "TD Ameritrade API Key")]
    [Description("API key for TD Ameritrade")]
    public string TdAmeritradeApiKey
    {
        get => _tdAmeritradeApiKey;
        set { _tdAmeritradeApiKey = value; OnPropertyChanged(nameof(TdAmeritradeApiKey)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Performance and resource settings
/// </summary>
public class PerformanceSettings : INotifyPropertyChanged
{
    private int _maxConcurrentConnections = 5;
    private int _requestTimeoutMs = 30000;
    private bool _enableMultiThreading = true;
    private int _maxMemoryUsageMB = 512;
    private bool _enableLogging = true;
    private string _logLevel = "Information";

    [Display(Name = "Max Concurrent Connections")]
    [Description("Maximum simultaneous data connections")]
    [Range(1, 20)]
    public int MaxConcurrentConnections
    {
        get => _maxConcurrentConnections;
        set { _maxConcurrentConnections = value; OnPropertyChanged(nameof(MaxConcurrentConnections)); }
    }

    [Display(Name = "Request Timeout (ms)")]
    [Description("Network request timeout")]
    [Range(5000, 120000)]
    public int RequestTimeoutMs
    {
        get => _requestTimeoutMs;
        set { _requestTimeoutMs = value; OnPropertyChanged(nameof(RequestTimeoutMs)); }
    }

    [Display(Name = "Enable Multi-Threading")]
    [Description("Use multiple threads for data processing")]
    public bool EnableMultiThreading
    {
        get => _enableMultiThreading;
        set { _enableMultiThreading = value; OnPropertyChanged(nameof(EnableMultiThreading)); }
    }

    [Display(Name = "Max Memory Usage (MB)")]
    [Description("Maximum memory usage for caching")]
    [Range(128, 2048)]
    public int MaxMemoryUsageMB
    {
        get => _maxMemoryUsageMB;
        set { _maxMemoryUsageMB = value; OnPropertyChanged(nameof(MaxMemoryUsageMB)); }
    }

    [Display(Name = "Enable Logging")]
    [Description("Enable application logging")]
    public bool EnableLogging
    {
        get => _enableLogging;
        set { _enableLogging = value; OnPropertyChanged(nameof(EnableLogging)); }
    }

    [Display(Name = "Log Level")]
    [Description("Minimum log level to record")]
    public string LogLevel
    {
        get => _logLevel;
        set { _logLevel = value; OnPropertyChanged(nameof(LogLevel)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Safety and risk management settings
/// </summary>
public class SafetySettings : INotifyPropertyChanged
{
    private decimal _maxPositionSize = 1000m;
    private decimal _maxDailyLoss = 500m;
    private decimal _maxOrderValue = 5000m;
    private bool _enableStopLoss = true;
    private decimal _defaultStopLossPercent = 5m;
    private bool _enablePositionSizing = true;
    private decimal _riskPerTradePercent = 2m;

    [Display(Name = "Max Position Size")]
    [Description("Maximum size for any single position")]
    [Range(100, 100000)]
    public decimal MaxPositionSize
    {
        get => _maxPositionSize;
        set { _maxPositionSize = value; OnPropertyChanged(nameof(MaxPositionSize)); }
    }

    [Display(Name = "Max Daily Loss")]
    [Description("Maximum loss allowed per day")]
    [Range(100, 10000)]
    public decimal MaxDailyLoss
    {
        get => _maxDailyLoss;
        set { _maxDailyLoss = value; OnPropertyChanged(nameof(MaxDailyLoss)); }
    }

    [Display(Name = "Max Order Value")]
    [Description("Maximum value for any single order")]
    [Range(100, 50000)]
    public decimal MaxOrderValue
    {
        get => _maxOrderValue;
        set { _maxOrderValue = value; OnPropertyChanged(nameof(MaxOrderValue)); }
    }

    [Display(Name = "Enable Stop Loss")]
    [Description("Automatically suggest stop loss orders")]
    public bool EnableStopLoss
    {
        get => _enableStopLoss;
        set { _enableStopLoss = value; OnPropertyChanged(nameof(EnableStopLoss)); }
    }

    [Display(Name = "Default Stop Loss %")]
    [Description("Default stop loss percentage")]
    [Range(1, 20)]
    public decimal DefaultStopLossPercent
    {
        get => _defaultStopLossPercent;
        set { _defaultStopLossPercent = value; OnPropertyChanged(nameof(DefaultStopLossPercent)); }
    }

    [Display(Name = "Enable Position Sizing")]
    [Description("Use automatic position sizing based on risk")]
    public bool EnablePositionSizing
    {
        get => _enablePositionSizing;
        set { _enablePositionSizing = value; OnPropertyChanged(nameof(EnablePositionSizing)); }
    }

    [Display(Name = "Risk Per Trade %")]
    [Description("Maximum risk per trade as percentage of account")]
    [Range(0.5, 10)]
    public decimal RiskPerTradePercent
    {
        get => _riskPerTradePercent;
        set { _riskPerTradePercent = value; OnPropertyChanged(nameof(RiskPerTradePercent)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Advanced technical settings
/// </summary>
public class AdvancedSettings : INotifyPropertyChanged
{
    private bool _enableDebugMode = false;
    private bool _enableBetaFeatures = false;
    private string _proxyServer = "";
    private int _proxyPort = 8080;
    private string _customDataPath = "";
    private bool _enableTelemetry = false;

    [Display(Name = "Enable Debug Mode")]
    [Description("Enable debugging features and verbose logging")]
    public bool EnableDebugMode
    {
        get => _enableDebugMode;
        set { _enableDebugMode = value; OnPropertyChanged(nameof(EnableDebugMode)); }
    }

    [Display(Name = "Enable Beta Features")]
    [Description("Enable experimental beta features")]
    public bool EnableBetaFeatures
    {
        get => _enableBetaFeatures;
        set { _enableBetaFeatures = value; OnPropertyChanged(nameof(EnableBetaFeatures)); }
    }

    [Display(Name = "Proxy Server")]
    [Description("Proxy server address (if required)")]
    public string ProxyServer
    {
        get => _proxyServer;
        set { _proxyServer = value; OnPropertyChanged(nameof(ProxyServer)); }
    }

    [Display(Name = "Proxy Port")]
    [Description("Proxy server port")]
    [Range(1, 65535)]
    public int ProxyPort
    {
        get => _proxyPort;
        set { _proxyPort = value; OnPropertyChanged(nameof(ProxyPort)); }
    }

    [Display(Name = "Custom Data Path")]
    [Description("Custom directory for data storage")]
    public string CustomDataPath
    {
        get => _customDataPath;
        set { _customDataPath = value; OnPropertyChanged(nameof(CustomDataPath)); }
    }

    [Display(Name = "Enable Telemetry")]
    [Description("Send anonymous usage data to help improve APEX")]
    public bool EnableTelemetry
    {
        get => _enableTelemetry;
        set { _enableTelemetry = value; OnPropertyChanged(nameof(EnableTelemetry)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected virtual void OnPropertyChanged(string propertyName) => 
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}