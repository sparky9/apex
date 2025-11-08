using ApexV2.Charts.Models;

namespace ApexV2.Indicators.Engine;

/// <summary>
/// Base interface for all technical indicators
/// </summary>
public interface IIndicator
{
    /// <summary>
    /// Unique identifier for the indicator
    /// </summary>
    string Id { get; }
    
    /// <summary>
    /// Display name of the indicator
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Description of what the indicator measures
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Category of the indicator (Trend, Momentum, Volatility, Volume, etc.)
    /// </summary>
    IndicatorCategory Category { get; }
    
    /// <summary>
    /// Configuration parameters for the indicator
    /// </summary>
    Dictionary<string, object> Parameters { get; }
    
    /// <summary>
    /// Whether the indicator requires a minimum number of data points
    /// </summary>
    int MinimumDataPoints { get; }
    
    /// <summary>
    /// Whether the indicator is ready to calculate (has enough data)
    /// </summary>
    bool IsReady { get; }
    
    /// <summary>
    /// Calculate the indicator value for the given data series
    /// </summary>
    /// <param name="data">Historical price data</param>
    /// <returns>Indicator results</returns>
    IndicatorResult Calculate(IEnumerable<CandlestickData> data);
    
    /// <summary>
    /// Calculate the indicator incrementally with new data point
    /// </summary>
    /// <param name="newData">New candlestick data point</param>
    /// <returns>Updated indicator result</returns>
    IndicatorResult Update(CandlestickData newData);
    
    /// <summary>
    /// Reset the indicator state
    /// </summary>
    void Reset();
    
    /// <summary>
    /// Clone the indicator with the same parameters
    /// </summary>
    /// <returns>New indicator instance</returns>
    IIndicator Clone();
    
    /// <summary>
    /// Validate the indicator parameters
    /// </summary>
    /// <returns>Validation result</returns>
    IndicatorValidationResult ValidateParameters();
}

/// <summary>
/// Indicator categories for organization
/// </summary>
public enum IndicatorCategory
{
    Trend,
    Momentum,
    Volatility,
    Volume,
    Oscillator,
    Overlay,
    Custom
}

/// <summary>
/// Result of indicator calculation
/// </summary>
public class IndicatorResult
{
    /// <summary>
    /// Timestamp of the result
    /// </summary>
    public DateTime Timestamp { get; init; }
    
    /// <summary>
    /// Primary indicator value
    /// </summary>
    public double Value { get; init; }
    
    /// <summary>
    /// Additional values for multi-line indicators (e.g., MACD signal line)
    /// </summary>
    public Dictionary<string, double> AdditionalValues { get; init; } = new();
    
    /// <summary>
    /// Metadata about the calculation
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();
    
    /// <summary>
    /// Whether this is a valid result
    /// </summary>
    public bool IsValid { get; init; } = true;
    
    /// <summary>
    /// Error message if calculation failed
    /// </summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Validation result for indicator parameters
/// </summary>
public class IndicatorValidationResult
{
    /// <summary>
    /// Whether the parameters are valid
    /// </summary>
    public bool IsValid { get; init; }
    
    /// <summary>
    /// Validation error messages
    /// </summary>
    public List<string> Errors { get; init; } = new();
    
    /// <summary>
    /// Validation warnings
    /// </summary>
    public List<string> Warnings { get; init; } = new();
    
    /// <summary>
    /// Create a successful validation result
    /// </summary>
    public static IndicatorValidationResult Success() => new() { IsValid = true };
    
    /// <summary>
    /// Create a failed validation result
    /// </summary>
    public static IndicatorValidationResult Failure(params string[] errors) => new()
    {
        IsValid = false,
        Errors = errors.ToList()
    };
}
