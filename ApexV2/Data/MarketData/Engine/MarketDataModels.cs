namespace ApexV2.Data.MarketData.Engine;

public record Quote(string Symbol, decimal Last, decimal Open, decimal High, decimal Low, decimal Close, long Volume, DateTime Timestamp, string Provider, string Currency = "CAD");

[Flags]
public enum QuoteUpdateFields { None=0, Last=1, Open=2, High=4, Low=8, Close=16, Volume=32, All=Last|Open|High|Low|Close|Volume }

// Added isStale flag (default false)
public record QuoteUpdate(string Symbol, QuoteUpdateFields Fields, decimal? Last, decimal? Open, decimal? High, decimal? Low, decimal? Close, long? Volume, DateTime Timestamp, string Provider, bool IsStale = false);

public enum ProviderHealthStatus { Unknown, Healthy, Degraded, Unavailable, CircuitOpen }

public record ProviderStatus(string ProviderName, ProviderHealthStatus Status, string? Message, DateTime Timestamp);

// Added new supporting records
public record ProviderCapabilities(bool SupportsStreaming, bool RequiresApiKey, bool SupportsCanadianSymbols, int? MaxBatchSize = null);

public record ProviderMetrics(
    string ProviderName,
    int SuccessCount,
    int FailureCount,
    int TransientFailureCount,
    int CircuitOpenCount,
    DateTime? LastSuccessUtc,
    DateTime? LastFailureUtc,
    ProviderHealthStatus HealthStatus
);

public record CachedQuoteSnapshot(string Symbol, decimal Last, decimal Open, decimal High, decimal Low, decimal Close, long Volume, DateTime Timestamp, string Provider);

// Structured provider error types
public abstract class ProviderException : Exception { protected ProviderException(string message) : base(message) {} }
public class ProviderTimeoutException : ProviderException { public ProviderTimeoutException(string msg) : base(msg) {} }
public class ProviderRateLimitException : ProviderException { public ProviderRateLimitException(string msg) : base(msg) {} }
public class ProviderNetworkException : ProviderException { public ProviderNetworkException(string msg) : base(msg) {} }
public class ProviderDataFormatException : ProviderException { public ProviderDataFormatException(string msg) : base(msg) {} }
