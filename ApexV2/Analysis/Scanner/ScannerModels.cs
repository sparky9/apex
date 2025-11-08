using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ApexV2.Analysis.Scanner;

/// <summary>
/// Represents a saved scan configuration with criteria and settings
/// </summary>
public class ScanConfiguration : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private string _description = string.Empty;
    private bool _isEnabled = true;
    private DateTime _lastRun = DateTime.MinValue;
    private ScanFrequency _frequency = ScanFrequency.Manual;

    public Guid Id { get; set; } = Guid.NewGuid();
    
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }
    
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }
    
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    
    public DateTime LastRun
    {
        get => _lastRun;
        set => SetProperty(ref _lastRun, value);
    }
    
    public ScanFrequency Frequency
    {
        get => _frequency;
        set => SetProperty(ref _frequency, value);
    }
    
    public List<ScanCriterion> Criteria { get; set; } = new();
    public List<string> Markets { get; set; } = new();
    public List<string> Sectors { get; set; } = new();
    public List<string> Industries { get; set; } = new();
    public ScanResultSettings ResultSettings { get; set; } = new();
    
    public string Author { get; set; } = Environment.UserName;
    public List<string> Tags { get; set; } = new();
    public bool IsPublic { get; set; } = false;
    public int DownloadCount { get; set; } = 0;
    public decimal Rating { get; set; } = 0m;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        ModifiedDate = DateTime.Now;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Individual scan criterion for filtering stocks
/// </summary>
public class ScanCriterion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public ScanCriterionType Type { get; set; }
    public ScanOperator Operator { get; set; }
    public decimal? Value { get; set; }
    public decimal? SecondValue { get; set; } // For range operators
    public string? TextValue { get; set; }
    public string? IndicatorId { get; set; } // For custom indicators
    public Dictionary<string, object> Parameters { get; set; } = new();
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
}

/// <summary>
/// Result of a stock scan operation
/// </summary>
public class ScanResult
{
    public Guid ScanId { get; set; }
    public string ScanName { get; set; } = string.Empty;
    public DateTime ScanDate { get; set; } = DateTime.Now;
    public TimeSpan ScanDuration { get; set; }
    public int TotalSymbolsScanned { get; set; }
    public int MatchingSymbols { get; set; }
    public List<ScanResultItem> Results { get; set; } = new();
    public ScanStatus Status { get; set; } = ScanStatus.Completed;
    public string? ErrorMessage { get; set; }
    public ScanStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Individual stock result from a scan
/// </summary>
public class ScanResultItem : INotifyPropertyChanged
{
    private decimal _lastPrice;
    private decimal _change;
    private decimal _changePercent;
    private long _volume;
    private DateTime _lastUpdate = DateTime.Now;

    public string Symbol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Market { get; set; } = string.Empty;
    public string Sector { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    
    public decimal LastPrice
    {
        get => _lastPrice;
        set => SetProperty(ref _lastPrice, value);
    }
    
    public decimal Change
    {
        get => _change;
        set
        {
            if (SetProperty(ref _change, value))
            {
                OnPropertyChanged(nameof(FormattedChange));
                OnPropertyChanged(nameof(IsPositive));
                OnPropertyChanged(nameof(IsNegative));
            }
        }
    }
    
    public decimal ChangePercent
    {
        get => _changePercent;
        set
        {
            if (SetProperty(ref _changePercent, value))
            {
                OnPropertyChanged(nameof(FormattedChangePercent));
            }
        }
    }
    
    public long Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, value);
    }
    
    public DateTime LastUpdate
    {
        get => _lastUpdate;
        set => SetProperty(ref _lastUpdate, value);
    }
    
    // Fundamental data
    public decimal? MarketCap { get; set; }
    public decimal? PERatio { get; set; }
    public decimal? EPS { get; set; }
    public decimal? DividendYield { get; set; }
    public decimal? BookValue { get; set; }
    public decimal? DebtToEquity { get; set; }
    public decimal? ROE { get; set; }
    public decimal? RevenueTTM { get; set; }
    
    // Technical indicators
    public decimal? RSI { get; set; }
    public decimal? SMA20 { get; set; }
    public decimal? SMA50 { get; set; }
    public decimal? SMA200 { get; set; }
    public decimal? MACD { get; set; }
    public decimal? BollingerUpper { get; set; }
    public decimal? BollingerLower { get; set; }
    
    // Calculated properties
    public decimal Score { get; set; } // Overall scan score
    public int Rank { get; set; } // Rank in results
    public Dictionary<string, decimal> CriterionValues { get; set; } = new();
    
    // Formatted display properties
    public string FormattedPrice => LastPrice.ToString("C2");
    public string FormattedChange => $"{(Change >= 0 ? "+" : "")}{Change:F2}";
    public string FormattedChangePercent => $"{(ChangePercent >= 0 ? "+" : "")}{ChangePercent:F2}%";
    public string FormattedVolume => Volume.ToString("N0");
    public bool IsPositive => Change > 0;
    public bool IsNegative => Change < 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}

/// <summary>
/// Settings for scan result display and processing
/// </summary>
public class ScanResultSettings
{
    public int MaxResults { get; set; } = 100;
    public ScanSortBy SortBy { get; set; } = ScanSortBy.Score;
    public bool SortDescending { get; set; } = true;
    public bool IncludeFundamentals { get; set; } = true;
    public bool IncludeTechnicals { get; set; } = true;
    public bool EnableRealTimeUpdates { get; set; } = false;
    public List<string> VisibleColumns { get; set; } = new()
    {
        "Symbol", "Name", "LastPrice", "Change", "ChangePercent", "Volume", "Score"
    };
}

/// <summary>
/// Statistical information about scan results
/// </summary>
public class ScanStatistics
{
    public decimal AveragePrice { get; set; }
    public decimal AverageChange { get; set; }
    public decimal AverageVolume { get; set; }
    public int WinnersCount { get; set; }
    public int LosersCount { get; set; }
    public int UnchangedCount { get; set; }
    public Dictionary<string, int> SectorBreakdown { get; set; } = new();
    public Dictionary<string, int> MarketBreakdown { get; set; } = new();
    public Dictionary<ScanCriterionType, int> CriterionMatches { get; set; } = new();
}

/// <summary>
/// Real-time scan progress information
/// </summary>
public class ScanProgress
{
    public Guid ScanId { get; set; }
    public int TotalSymbols { get; set; }
    public int ProcessedSymbols { get; set; }
    public int MatchingSymbols { get; set; }
    public string CurrentSymbol { get; set; } = string.Empty;
    public string CurrentStage { get; set; } = string.Empty;
    public decimal ProgressPercentage => TotalSymbols > 0 ? (decimal)ProcessedSymbols / TotalSymbols * 100 : 0;
    public bool CanCancel { get; set; } = true;
    public DateTime StartTime { get; set; } = DateTime.Now;
    public TimeSpan ElapsedTime => DateTime.Now - StartTime;
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// Types of scan criteria
/// </summary>
public enum ScanCriterionType
{
    // Price criteria
    Price,
    PriceChange,
    PriceChangePercent,
    Volume,
    VolumeAverage,
    
    // Technical indicators
    RSI,
    SMA,
    EMA,
    MACD,
    BollingerBands,
    StochasticOscillator,
    WilliamsR,
    CommodityChannelIndex,
    
    // Fundamental criteria
    MarketCap,
    PERatio,
    EPS,
    EPSGrowth,
    Revenue,
    RevenueGrowth,
    DividendYield,
    BookValue,
    DebtToEquity,
    ROE,
    ROA,
    GrossMargin,
    OperatingMargin,
    NetMargin,
    
    // Pattern criteria
    BreakoutUp,
    BreakoutDown,
    Support,
    Resistance,
    GapUp,
    GapDown,
    
    // Custom indicators
    CustomIndicator,
    
    // Sector/Industry
    Sector,
    Industry,
    Market
}

/// <summary>
/// Comparison operators for scan criteria
/// </summary>
public enum ScanOperator
{
    Equals,
    NotEquals,
    GreaterThan,
    GreaterThanOrEqual,
    LessThan,
    LessThanOrEqual,
    Between,
    NotBetween,
    Contains,
    NotContains,
    StartsWith,
    EndsWith,
    IsNull,
    IsNotNull,
    CrossesAbove,
    CrossesBelow,
    InRange,
    OutOfRange
}

/// <summary>
/// Frequency for automated scans
/// </summary>
public enum ScanFrequency
{
    Manual,
    RealTime,
    Every5Minutes,
    Every15Minutes,
    Every30Minutes,
    Hourly,
    Every4Hours,
    Daily,
    Weekly
}

/// <summary>
/// Status of a scan operation
/// </summary>
public enum ScanStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    Paused
}

/// <summary>
/// Sort options for scan results
/// </summary>
public enum ScanSortBy
{
    Score,
    Symbol,
    Name,
    Price,
    Change,
    ChangePercent,
    Volume,
    MarketCap,
    PERatio,
    RSI,
    Custom
}

/// <summary>
/// Event arguments for scan progress updates
/// </summary>
public class ScanProgressEventArgs : EventArgs
{
    public ScanProgress Progress { get; }
    
    public ScanProgressEventArgs(ScanProgress progress)
    {
        Progress = progress;
    }
}

/// <summary>
/// Event arguments for scan completion
/// </summary>
public class ScanCompletedEventArgs : EventArgs
{
    public ScanResult Result { get; }
    
    public ScanCompletedEventArgs(ScanResult result)
    {
        Result = result;
    }
}

/// <summary>
/// Event arguments for scan error
/// </summary>
public class ScanErrorEventArgs : EventArgs
{
    public Guid ScanId { get; }
    public Exception Exception { get; }
    public string Message { get; }
    
    public ScanErrorEventArgs(Guid scanId, Exception exception, string message)
    {
        ScanId = scanId;
        Exception = exception;
        Message = message;
    }
}
