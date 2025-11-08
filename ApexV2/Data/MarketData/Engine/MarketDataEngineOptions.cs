namespace ApexV2.Data.MarketData.Engine;

public class MarketDataEngineOptions
{
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxRetryAttempts { get; set; } = 3;
    public int BaseRetryDelayMs { get; set; } = 250;
    public int RateLimitPerMinute { get; set; } = 60; // 60 calls/minute
    public int BatchSize { get; set; } = 25;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public TimeSpan CircuitBreakerOpenDuration { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromSeconds(45);
    public bool PersistLastQuotesOnShutdown { get; set; } = true;
    // Validation
    public TimeSpan MaxQuoteStaleness { get; set; } = TimeSpan.FromMinutes(5);
    public decimal ClampSpikePercent { get; set; } = 0.25m; // 25%
    public decimal HardRejectSpikePercent { get; set; } = 0.60m; // 60%
}
