#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ApexV2.Extensions.RealTime
{
    /// <summary>
    /// Types of real-time data available
    /// </summary>
    public enum RealTimeDataType
    {
        Quote,          // Price quotes (bid/ask/last)
        Price,          // Price updates only
        Trade,          // Executed trades
        Volume,         // Volume updates
        Level2,         // Market depth (order book)
        News,           // Real-time news
        Fundamentals,   // Fundamental data changes
        Options,        // Options chain updates
        Forex,          // Currency rates
        Crypto,         // Cryptocurrency data
        Economic        // Economic indicators
    }

    /// <summary>
    /// Priority levels for real-time updates
    /// </summary>
    public enum RealTimePriority
    {
        Low,
        Normal,
        High,
        Critical
    }

    /// <summary>
    /// Alert conditions for real-time monitoring
    /// </summary>
    public enum RealTimeAlertCondition
    {
        GreaterThan,        // Price above threshold
        LessThan,           // Price below threshold  
        GreaterThanOrEqual, // Price at or above threshold
        LessThanOrEqual,    // Price at or below threshold
        Equals,             // Price equals threshold
        PercentChangeAbove, // Percent change above threshold
        PercentChangeBelow, // Percent change below threshold
        VolumeAbove,        // Volume above threshold
        VolumeBelow,        // Volume below threshold
        Custom              // Custom condition
    }

    /// <summary>
    /// Core real-time update data structure
    /// </summary>
    public class RealTimeUpdate
    {
        public string Symbol { get; set; }
        public RealTimeDataType DataType { get; set; }
        public DateTime Timestamp { get; set; }
        public RealTimePriority Priority { get; set; }
        public string Source { get; set; }
        public Dictionary<string, object> Data { get; set; } = new();
        public string RequestId { get; set; }
        public TimeSpan Latency { get; set; }

        // Market data specific fields
        public decimal? Price { get; set; }
        public decimal? BidPrice { get; set; }
        public decimal? AskPrice { get; set; }
        public long? Volume { get; set; }
        public decimal? Change { get; set; }
        public decimal? ChangePercent { get; set; }
        public decimal? High { get; set; }
        public decimal? Low { get; set; }
        public decimal? Open { get; set; }
        public decimal? Close { get; set; }

        // Trade specific fields
        public string TradeId { get; set; }
        public decimal? TradePrice { get; set; }
        public long? TradeSize { get; set; }
        public string TradeSide { get; set; } // Buy/Sell

        // Level 2 specific fields
        public List<MarketDepthLevel> Bids { get; set; } = new();
        public List<MarketDepthLevel> Asks { get; set; } = new();

        // News specific fields
        public string NewsHeadline { get; set; }
        public string NewsContent { get; set; }
        public string NewsUrl { get; set; }
        public DateTime? NewsPublishedAt { get; set; }

        // Validation
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(Symbol) && 
                   Timestamp != default && 
                   !string.IsNullOrEmpty(Source);
        }
    }

    /// <summary>
    /// Market depth level for Level 2 data
    /// </summary>
    public class MarketDepthLevel
    {
        public decimal Price { get; set; }
        public long Size { get; set; }
        public int OrderCount { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Real-time aggregated data for intervals
    /// </summary>
    public class RealTimeAggregateData
    {
        public string Symbol { get; set; }
        public DateTime IntervalStart { get; set; }
        public DateTime IntervalEnd { get; set; }
        public TimeSpan Interval { get; set; }
        
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        public long TradeCount { get; set; }
        
        public decimal VWAP { get; set; } // Volume weighted average price
        public decimal TWAP { get; set; } // Time weighted average price
        
        public DateTime LastUpdated { get; set; }
        public bool IsComplete { get; set; }
    }

    /// <summary>
    /// Real-time alert configuration
    /// </summary>
    public class RealTimeAlert
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string Symbol { get; set; }
        public string Description { get; set; } = "";
        public RealTimeDataType DataType { get; set; }
        public RealTimeAlertCondition Condition { get; set; }
        public decimal Threshold { get; set; }
        public decimal TargetValue { get; set; }
        public string CustomExpression { get; set; } // For custom conditions
        
        public bool IsActive { get; set; } = true;
        public bool IsEnabled { get; set; } = true;
        public bool IsOneTime { get; set; } = false; // Delete after triggering
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiresAt { get; set; }
        public DateTime? LastTriggered { get; set; }
        public int TriggerCount { get; set; }
        
        // Notification settings
        public bool ShowPopup { get; set; } = true;
        public bool PlaySound { get; set; } = true;
        public bool SendEmail { get; set; } = false;
        public string EmailAddress { get; set; }
        public string SoundFile { get; set; }
        
        public bool ShouldTrigger(RealTimeUpdate update)
        {
            if (!IsActive || Symbol != update.Symbol) return false;
            if (ExpiresAt.HasValue && DateTime.UtcNow > ExpiresAt) return false;
            
            return Condition switch
            {
                RealTimeAlertCondition.GreaterThan => update.Price.HasValue && update.Price > Threshold,
                RealTimeAlertCondition.LessThan => update.Price.HasValue && update.Price < Threshold,
                RealTimeAlertCondition.GreaterThanOrEqual => update.Price.HasValue && update.Price >= Threshold,
                RealTimeAlertCondition.LessThanOrEqual => update.Price.HasValue && update.Price <= Threshold,
                RealTimeAlertCondition.Equals => update.Price.HasValue && Math.Abs(update.Price.Value - Threshold) < 0.01m,
                RealTimeAlertCondition.VolumeAbove => update.Volume.HasValue && update.Volume > Threshold,
                RealTimeAlertCondition.VolumeBelow => update.Volume.HasValue && update.Volume < Threshold,
                RealTimeAlertCondition.PercentChangeAbove => update.ChangePercent.HasValue && update.ChangePercent > Threshold,
                RealTimeAlertCondition.PercentChangeBelow => update.ChangePercent.HasValue && update.ChangePercent < Threshold,
                RealTimeAlertCondition.Custom => EvaluateCustomExpression(update),
                _ => false
            };
        }
        
        private bool EvaluateCustomExpression(RealTimeUpdate update)
        {
            // Simple expression evaluator - could be enhanced with a proper parser
            if (string.IsNullOrEmpty(CustomExpression)) return false;
            
            try
            {
                var expression = CustomExpression
                    .Replace("{price}", update.Price?.ToString() ?? "0")
                    .Replace("{volume}", update.Volume?.ToString() ?? "0")
                    .Replace("{change}", update.Change?.ToString() ?? "0")
                    .Replace("{changepct}", update.ChangePercent?.ToString() ?? "0");
                
                // This is a simplified evaluator - production would use a proper expression engine
                return false; // Placeholder
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Real-time system metrics
    /// </summary>
    public class RealTimeMetrics
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        // Connection metrics
        public bool IsConnected { get; set; }
        public string ConnectedProvider { get; set; }
        public DateTime? LastConnectionTime { get; set; }
        public TimeSpan? ConnectionUptime { get; set; }
        public int ReconnectionAttempts { get; set; }
        
        // Update metrics
        public long TotalUpdatesReceived { get; set; }
        public long UpdatesPerSecond { get; set; }
        public long UpdatesInLastMinute { get; set; }
        public long UpdatesInLastHour { get; set; }
        
        // Latency metrics
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan MinLatency { get; set; }
        public TimeSpan MaxLatency { get; set; }
        public TimeSpan LastUpdateLatency { get; set; }
        
        // Error metrics
        public long TotalErrors { get; set; }
        public long ErrorsInLastMinute { get; set; }
        public long ErrorsInLastHour { get; set; }
        public DateTime? LastErrorTime { get; set; }
        public string LastErrorMessage { get; set; }
        
        // Subscription metrics
        public int ActiveSubscriptions { get; set; }
        public Dictionary<string, int> SubscriptionsBySymbol { get; set; } = new();
        public Dictionary<RealTimeDataType, int> SubscriptionsByType { get; set; } = new();
        
        // Data quality metrics
        public double DataQualityScore { get; set; } = 100.0; // Percentage
        public long MissedUpdates { get; set; }
        public long DuplicateUpdates { get; set; }
        public long OutOfOrderUpdates { get; set; }
        
        // Performance metrics
        public long MemoryUsageBytes { get; set; }
        public double CpuUsagePercent { get; set; }
        public long NetworkBytesReceived { get; set; }
        public long NetworkBytesSent { get; set; }
        
        // Calculated properties for backwards compatibility
        public double AverageLatencyMs => AverageLatency.TotalMilliseconds;
        public double ErrorRatePercent => TotalUpdatesReceived > 0 ? (double)TotalErrors / TotalUpdatesReceived * 100.0 : 0.0;
    }

    /// <summary>
    /// Event arguments for real-time errors
    /// </summary>
    public class RealTimeErrorEventArgs : EventArgs
    {
        public string Symbol { get; set; }
        public RealTimeDataType DataType { get; set; }
        public string ErrorMessage { get; set; }
        public Exception Exception { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool IsCritical { get; set; }
        public string Source { get; set; }
    }

    /// <summary>
    /// Event arguments for connection status changes
    /// </summary>
    public class RealTimeConnectionEventArgs : EventArgs
    {
        public bool IsConnected { get; set; }
        public string Provider { get; set; }
        public string Reason { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public TimeSpan? Downtime { get; set; }
        public bool IsReconnection { get; set; }
    }

    /// <summary>
    /// Event arguments for alert triggers
    /// </summary>
    public class RealTimeAlertTriggeredEventArgs : EventArgs
    {
        public RealTimeAlert Alert { get; set; }
        public RealTimeUpdate TriggeringUpdate { get; set; }
        public DateTime TriggeredAt { get; set; } = DateTime.UtcNow;
        public string Message { get; set; }
        
        // Convenience properties
        public Guid AlertId => Alert?.Id ?? Guid.Empty;
        public string Symbol => Alert?.Symbol ?? TriggeringUpdate?.Symbol ?? "";
    }

    /// <summary>
    /// Configuration for real-time data services
    /// </summary>
    public class RealTimeConfiguration
    {
        [Display(Name = "Enable Real-Time Updates")]
        public bool EnableRealTime { get; set; } = true;

        [Display(Name = "Provider Name")]
        public string ProviderName { get; set; } = "Default";

        [Display(Name = "Auto-Connect on Startup")]
        public bool AutoConnect { get; set; } = true;

        [Display(Name = "Connection Timeout (seconds)")]
        [Range(5, 300)]
        public int ConnectionTimeoutSeconds { get; set; } = 30;

        [Display(Name = "Update Interval (ms)")]
        [Range(100, 5000)]
        public int UpdateIntervalMs { get; set; } = 1000;

        [Display(Name = "Reconnection Attempts")]
        [Range(0, 10)]
        public int MaxReconnectionAttempts { get; set; } = 3;
        public int ReconnectAttempts => MaxReconnectionAttempts;

        [Display(Name = "Reconnection Delay (seconds)")]
        [Range(1, 60)]
        public int ReconnectionDelaySeconds { get; set; } = 5;
        public int ReconnectDelayMs => ReconnectionDelaySeconds * 1000;

        [Display(Name = "Max Concurrent Subscriptions")]
        [Range(10, 1000)]
        public int MaxConcurrentSubscriptions { get; set; } = 100;

        [Display(Name = "Buffer Size")]
        [Range(1000, 100000)]
        public int BufferSize { get; set; } = 10000;

        [Display(Name = "Max Updates Per Second")]
        [Range(10, 1000)]
        public int MaxUpdatesPerSecond { get; set; } = 100;

        [Display(Name = "Enable Data Aggregation")]
        public bool EnableAggregation { get; set; } = true;

        [Display(Name = "Aggregation Intervals")]
        public List<TimeSpan> AggregationIntervals { get; set; } = new()
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromMinutes(1),
            TimeSpan.FromMinutes(5)
        };

        [Display(Name = "Enable Alerts")]
        public bool EnableAlerts { get; set; } = true;

        [Display(Name = "Alert Sound File")]
        public string DefaultAlertSoundFile { get; set; } = "alert.wav";

        [Display(Name = "Show Alert Popups")]
        public bool ShowAlertPopups { get; set; } = true;

        [Display(Name = "Enable Metrics Collection")]
        public bool EnableMetrics { get; set; } = true;

        [Display(Name = "Metrics Update Interval (seconds)")]
        [Range(1, 60)]
        public int MetricsUpdateIntervalSeconds { get; set; } = 5;

        [Display(Name = "Data Quality Threshold")]
        [Range(50.0, 100.0)]
        public double DataQualityThreshold { get; set; } = 95.0;

        [Display(Name = "Log Real-Time Events")]
        public bool LogRealTimeEvents { get; set; } = false;

        [Display(Name = "Primary Data Provider")]
        public string PrimaryProvider { get; set; } = "AlphaVantage";

        [Display(Name = "Fallback Providers")]
        public List<string> FallbackProviders { get; set; } = new() { "YahooFinance", "IEX" };
    }
}
