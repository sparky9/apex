#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ApexV2.Extensions.API
{
    // API Request/Response Models
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Message { get; set; }
        public string ErrorCode { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string RequestId { get; set; }
    }

    public class ApiError
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }
        public string Field { get; set; }
    }

    // Authentication Models
    public class ApiKeyRequest
    {
        [Required]
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Permissions { get; set; } = new List<string>();
        public DateTime? ExpiresAt { get; set; }
    }

    public class ApiKeyResponse
    {
        public string Id { get; set; }
        public string Key { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Permissions { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        public DateTime LastUsed { get; set; }
        public bool IsActive { get; set; }
    }

    // Market Data API Models
    public class MarketDataRequest
    {
        [Required]
        public string Symbol { get; set; }
        public string Timeframe { get; set; } = "1d";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? Limit { get; set; } = 100;
        public bool IncludeVolume { get; set; } = true;
        public bool IncludeIndicators { get; set; } = false;
    }

    public class MarketDataResponse
    {
        public string Symbol { get; set; }
        public string Timeframe { get; set; }
        public List<CandleData> Candles { get; set; } = new List<CandleData>();
        public Dictionary<string, object> Indicators { get; set; } = new Dictionary<string, object>();
        public MarketDataMetadata Metadata { get; set; }
    }

    public class CandleData
    {
        public DateTime Timestamp { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public long Volume { get; set; }
        public decimal? AdjustedClose { get; set; }
    }

    public class MarketDataMetadata
    {
        public string Source { get; set; }
        public DateTime LastUpdated { get; set; }
        public string Currency { get; set; }
        public string Exchange { get; set; }
        public int TotalRecords { get; set; }
        public bool IsRealTime { get; set; }
    }

    // Watchlist API Models
    public class WatchlistRequest
    {
        [Required]
        public string Name { get; set; }
        public string Description { get; set; }
        public List<string> Symbols { get; set; } = new List<string>();
        public bool IsPublic { get; set; } = false;
    }

    public class WatchlistResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<WatchlistItem> Items { get; set; } = new List<WatchlistItem>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedBy { get; set; }
        public bool IsPublic { get; set; }
        public int SymbolCount { get; set; }
    }

    public class WatchlistItem
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public decimal? CurrentPrice { get; set; }
        public decimal? Change { get; set; }
        public decimal? ChangePercent { get; set; }
        public long? Volume { get; set; }
        public DateTime? LastUpdate { get; set; }
        public string Exchange { get; set; }
        public Dictionary<string, object> CustomFields { get; set; } = new Dictionary<string, object>();
    }

    // Scanner API Models
    public class ScanRequest
    {
        [Required]
        public string Name { get; set; }
        public List<ScanCriteria> Criteria { get; set; } = new List<ScanCriteria>();
        public List<string> Markets { get; set; } = new List<string>();
        public int? Limit { get; set; } = 50;
        public string SortBy { get; set; } = "symbol";
        public string SortOrder { get; set; } = "asc";
    }

    public class ScanCriteria
    {
        public string Field { get; set; }
        public string Operator { get; set; }
        public object Value { get; set; }
        public string LogicalOperator { get; set; } = "AND";
    }

    public class ScanResponse
    {
        public string ScanId { get; set; }
        public string Name { get; set; }
        public List<ScanResult> Results { get; set; } = new List<ScanResult>();
        public ScanMetadata Metadata { get; set; }
        public DateTime ExecutedAt { get; set; }
        public TimeSpan ExecutionTime { get; set; }
    }

    public class ScanResult
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public string Exchange { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal Change { get; set; }
        public decimal ChangePercent { get; set; }
        public long Volume { get; set; }
        public decimal MarketCap { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
        public double Score { get; set; }
    }

    public class ScanMetadata
    {
        public int TotalResults { get; set; }
        public int FilteredResults { get; set; }
        public List<string> MarketsScanned { get; set; }
        public List<ScanCriteria> AppliedCriteria { get; set; }
        public string Status { get; set; }
    }

    // Indicator API Models
    public class IndicatorRequest
    {
        [Required]
        public string Symbol { get; set; }
        [Required]
        public string IndicatorType { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();
        public string Timeframe { get; set; } = "1d";
        public int? Period { get; set; } = 20;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }

    public class IndicatorResponse
    {
        public string Symbol { get; set; }
        public string IndicatorType { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public List<IndicatorValue> Values { get; set; } = new List<IndicatorValue>();
        public IndicatorMetadata Metadata { get; set; }
    }

    public class IndicatorValue
    {
        public DateTime Timestamp { get; set; }
        public decimal? Value { get; set; }
        public Dictionary<string, decimal?> Components { get; set; } = new Dictionary<string, decimal?>();
    }

    public class IndicatorMetadata
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public Dictionary<string, object> CalculationParameters { get; set; }
        public DateTime CalculatedAt { get; set; }
        public int DataPoints { get; set; }
    }

    // Portfolio API Models
    public class PortfolioRequest
    {
        public string Name { get; set; }
        public string BrokerName { get; set; }
        public bool IncludePositions { get; set; } = true;
        public bool IncludePerformance { get; set; } = true;
        public DateTime? AsOfDate { get; set; }
    }

    public class PortfolioResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string BrokerName { get; set; }
        public decimal TotalValue { get; set; }
        public decimal Cash { get; set; }
        public decimal InvestedValue { get; set; }
        public decimal TotalGainLoss { get; set; }
        public decimal TotalGainLossPercent { get; set; }
        public List<PositionData> Positions { get; set; } = new List<PositionData>();
        public PerformanceData Performance { get; set; }
        public DateTime LastUpdated { get; set; }
    }

    public class PositionData
    {
        public string Symbol { get; set; }
        public string Name { get; set; }
        public int Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue { get; set; }
        public decimal GainLoss { get; set; }
        public decimal GainLossPercent { get; set; }
        public DateTime LastUpdate { get; set; }
    }

    public class PerformanceData
    {
        public decimal DayGainLoss { get; set; }
        public decimal DayGainLossPercent { get; set; }
        public decimal WeekGainLoss { get; set; }
        public decimal WeekGainLossPercent { get; set; }
        public decimal MonthGainLoss { get; set; }
        public decimal MonthGainLossPercent { get; set; }
        public decimal YearGainLoss { get; set; }
        public decimal YearGainLossPercent { get; set; }
    }

    // Rate Limiting Models
    public class RateLimitInfo
    {
        public int RequestsPerMinute { get; set; }
        public int RequestsPerHour { get; set; }
        public int RequestsPerDay { get; set; }
        public int RemainingRequests { get; set; }
        public DateTime ResetTime { get; set; }
    }

    // Pagination Models
    public class PagedRequest
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public string SortBy { get; set; }
        public string SortOrder { get; set; } = "asc";
    }

    public class PagedResponse<T>
    {
        public List<T> Data { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
        public bool HasNext { get; set; }
        public bool HasPrevious { get; set; }
    }

    // Webhook Models
    public class WebhookRequest
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Url { get; set; }
        public List<string> Events { get; set; } = new List<string>();
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();
        public string Secret { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class WebhookResponse
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public List<string> Events { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastTriggered { get; set; }
        public bool IsActive { get; set; }
        public int TotalDeliveries { get; set; }
        public int SuccessfulDeliveries { get; set; }
    }
}
