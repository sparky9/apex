using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Engine;

/// <summary>
/// Registry for managing indicator metadata and discovery
/// Provides information about available indicators without instantiating them
/// </summary>
public class IndicatorRegistry
{
    private readonly Dictionary<string, IndicatorMetadata> _metadata = new();
    private readonly IChartLogger _logger;
    
    public IndicatorRegistry(IChartLogger? logger = null)
    {
        _logger = logger ?? new NullLogger();
        RegisterBuiltInIndicators();
    }
    
    /// <summary>
    /// Get all registered indicator metadata
    /// </summary>
    public IReadOnlyDictionary<string, IndicatorMetadata> Metadata => _metadata.AsReadOnly();
    
    /// <summary>
    /// Register indicator metadata
    /// </summary>
    /// <param name="metadata">Indicator metadata</param>
    public void RegisterIndicator(IndicatorMetadata metadata)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException(nameof(metadata));
        }
        
        if (string.IsNullOrWhiteSpace(metadata.Type))
        {
            throw new ArgumentException("Indicator type cannot be null or empty", nameof(metadata));
        }
        
        _metadata[metadata.Type] = metadata;
        _logger.Info($"Registered indicator metadata: {metadata.Type}");
    }
    
    /// <summary>
    /// Get indicator metadata by type
    /// </summary>
    /// <param name="type">Indicator type</param>
    /// <returns>Metadata or null if not found</returns>
    public IndicatorMetadata? GetMetadata(string type)
    {
        return _metadata.TryGetValue(type, out var metadata) ? metadata : null;
    }
    
    /// <summary>
    /// Get indicators by category
    /// </summary>
    /// <param name="category">Indicator category</param>
    /// <returns>Matching indicator metadata</returns>
    public IEnumerable<IndicatorMetadata> GetByCategory(IndicatorCategory category)
    {
        return _metadata.Values.Where(m => m.Category == category);
    }
    
    /// <summary>
    /// Search indicators by name or description
    /// </summary>
    /// <param name="searchTerm">Search term</param>
    /// <returns>Matching indicator metadata</returns>
    public IEnumerable<IndicatorMetadata> Search(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            return _metadata.Values;
        }
        
        var term = searchTerm.ToLowerInvariant();
        return _metadata.Values.Where(m =>
            m.Name.ToLowerInvariant().Contains(term) ||
            m.Description.ToLowerInvariant().Contains(term) ||
            m.Type.ToLowerInvariant().Contains(term));
    }
    
    /// <summary>
    /// Get indicators that can be applied to a specific data type
    /// </summary>
    /// <param name="dataType">Data type (price, volume, etc.)</param>
    /// <returns>Compatible indicators</returns>
    public IEnumerable<IndicatorMetadata> GetCompatibleIndicators(string dataType)
    {
        return _metadata.Values.Where(m => m.SupportedDataTypes.Contains(dataType));
    }
    
    /// <summary>
    /// Validate indicator parameters against metadata
    /// </summary>
    /// <param name="type">Indicator type</param>
    /// <param name="parameters">Parameters to validate</param>
    /// <returns>Validation result</returns>
    public IndicatorValidationResult ValidateParameters(string type, Dictionary<string, object> parameters)
    {
        var metadata = GetMetadata(type);
        if (metadata == null)
        {
            return IndicatorValidationResult.Failure($"Unknown indicator type: {type}");
        }
        
        var errors = new List<string>();
        var warnings = new List<string>();
        
        // Check required parameters
        foreach (var requiredParam in metadata.Parameters.Where(p => p.IsRequired))
        {
            if (!parameters.ContainsKey(requiredParam.Name))
            {
                errors.Add($"Required parameter '{requiredParam.Name}' is missing");
            }
        }
        
        // Validate parameter values
        foreach (var param in parameters)
        {
            var paramMetadata = metadata.Parameters.FirstOrDefault(p => p.Name == param.Key);
            if (paramMetadata == null)
            {
                warnings.Add($"Unknown parameter '{param.Key}' will be ignored");
                continue;
            }
            
            // Type validation
            if (!IsCompatibleType(param.Value, paramMetadata.Type))
            {
                errors.Add($"Parameter '{param.Key}' has incorrect type. Expected: {paramMetadata.Type}, Got: {param.Value?.GetType().Name ?? "null"}");
                continue;
            }
            
            // Range validation for numeric types
            if (paramMetadata.Type == typeof(int) || paramMetadata.Type == typeof(double))
            {
                var numValue = Convert.ToDouble(param.Value);
                if (paramMetadata.MinValue.HasValue && numValue < paramMetadata.MinValue.Value)
                {
                    errors.Add($"Parameter '{param.Key}' is below minimum value {paramMetadata.MinValue.Value}");
                }
                if (paramMetadata.MaxValue.HasValue && numValue > paramMetadata.MaxValue.Value)
                {
                    errors.Add($"Parameter '{param.Key}' is above maximum value {paramMetadata.MaxValue.Value}");
                }
            }
        }
        
        return new IndicatorValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = warnings
        };
    }
    
    /// <summary>
    /// Register built-in indicators
    /// </summary>
    private void RegisterBuiltInIndicators()
    {
        // SMA - Simple Moving Average
        RegisterIndicator(new IndicatorMetadata
        {
            Type = "SMA",
            Name = "Simple Moving Average",
            Description = "Average price over a specified number of periods",
            Category = IndicatorCategory.Trend,
            SupportedDataTypes = new[] { "price", "volume" },
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "period",
                    Type = typeof(int),
                    IsRequired = true,
                    DefaultValue = 20,
                    MinValue = 1,
                    MaxValue = 500,
                    Description = "Number of periods to average"
                }
            }
        });
        
        // EMA - Exponential Moving Average
        RegisterIndicator(new IndicatorMetadata
        {
            Type = "EMA",
            Name = "Exponential Moving Average",
            Description = "Exponentially weighted moving average giving more weight to recent prices",
            Category = IndicatorCategory.Trend,
            SupportedDataTypes = new[] { "price", "volume" },
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "period",
                    Type = typeof(int),
                    IsRequired = true,
                    DefaultValue = 20,
                    MinValue = 1,
                    MaxValue = 500,
                    Description = "Number of periods for EMA calculation"
                }
            }
        });
        
        // RSI - Relative Strength Index
        RegisterIndicator(new IndicatorMetadata
        {
            Type = "RSI",
            Name = "Relative Strength Index",
            Description = "Momentum oscillator measuring speed and magnitude of price changes",
            Category = IndicatorCategory.Momentum,
            SupportedDataTypes = new[] { "price" },
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "period",
                    Type = typeof(int),
                    IsRequired = true,
                    DefaultValue = 14,
                    MinValue = 2,
                    MaxValue = 100,
                    Description = "Number of periods for RSI calculation"
                }
            }
        });
        
        // MACD - Moving Average Convergence Divergence
        RegisterIndicator(new IndicatorMetadata
        {
            Type = "MACD",
            Name = "Moving Average Convergence Divergence",
            Description = "Trend-following momentum indicator showing relationship between two moving averages",
            Category = IndicatorCategory.Momentum,
            SupportedDataTypes = new[] { "price" },
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "fastPeriod",
                    Type = typeof(int),
                    IsRequired = true,
                    DefaultValue = 12,
                    MinValue = 1,
                    MaxValue = 100,
                    Description = "Fast EMA period"
                },
                new ParameterMetadata
                {
                    Name = "slowPeriod",
                    Type = typeof(int),
                    IsRequired = true,
                    DefaultValue = 26,
                    MinValue = 1,
                    MaxValue = 200,
                    Description = "Slow EMA period"
                },
                new ParameterMetadata
                {
                    Name = "signalPeriod",
                    Type = typeof(int),
                    IsRequired = true,
                    DefaultValue = 9,
                    MinValue = 1,
                    MaxValue = 50,
                    Description = "Signal line EMA period"
                }
            }
        });
        
        // Bollinger Bands
        RegisterIndicator(new IndicatorMetadata
        {
            Type = "BBANDS",
            Name = "Bollinger Bands",
            Description = "Volatility bands placed above and below a moving average",
            Category = IndicatorCategory.Volatility,
            SupportedDataTypes = new[] { "price" },
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "period",
                    Type = typeof(int),
                    IsRequired = true,
                    DefaultValue = 20,
                    MinValue = 2,
                    MaxValue = 100,
                    Description = "Moving average period"
                },
                new ParameterMetadata
                {
                    Name = "standardDeviations",
                    Type = typeof(double),
                    IsRequired = true,
                    DefaultValue = 2.0,
                    MinValue = 0.1,
                    MaxValue = 5.0,
                    Description = "Number of standard deviations"
                }
            }
        });
        
        _logger.Debug("Built-in indicator metadata registered");
    }
    
    /// <summary>
    /// Check if a value is compatible with the expected type
    /// </summary>
    private static bool IsCompatibleType(object? value, Type expectedType)
    {
        if (value == null)
        {
            return !expectedType.IsValueType || Nullable.GetUnderlyingType(expectedType) != null;
        }
        
        var valueType = value.GetType();
        
        // Direct type match
        if (expectedType.IsAssignableFrom(valueType))
        {
            return true;
        }
        
        // Check for convertible types
        try
        {
            Convert.ChangeType(value, expectedType);
            return true;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Metadata describing an indicator type
/// </summary>
public class IndicatorMetadata
{
    /// <summary>
    /// Unique type identifier
    /// </summary>
    public string Type { get; init; } = string.Empty;
    
    /// <summary>
    /// Display name
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Description of what the indicator measures
    /// </summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// Indicator category
    /// </summary>
    public IndicatorCategory Category { get; init; }
    
    /// <summary>
    /// Supported data types (price, volume, etc.)
    /// </summary>
    public string[] SupportedDataTypes { get; init; } = Array.Empty<string>();
    
    /// <summary>
    /// Parameter definitions
    /// </summary>
    public ParameterMetadata[] Parameters { get; init; } = Array.Empty<ParameterMetadata>();
    
    /// <summary>
    /// Whether this is a custom (user-created) indicator
    /// </summary>
    public bool IsCustom { get; init; }
    
    /// <summary>
    /// Author information for custom indicators
    /// </summary>
    public string? Author { get; init; }
    
    /// <summary>
    /// Version information
    /// </summary>
    public string? Version { get; init; }
}

/// <summary>
/// Metadata for indicator parameters
/// </summary>
public class ParameterMetadata
{
    /// <summary>
    /// Parameter name
    /// </summary>
    public string Name { get; init; } = string.Empty;
    
    /// <summary>
    /// Parameter type
    /// </summary>
    public Type Type { get; init; } = typeof(object);
    
    /// <summary>
    /// Whether the parameter is required
    /// </summary>
    public bool IsRequired { get; init; }
    
    /// <summary>
    /// Default value
    /// </summary>
    public object? DefaultValue { get; init; }
    
    /// <summary>
    /// Minimum value for numeric types
    /// </summary>
    public double? MinValue { get; init; }
    
    /// <summary>
    /// Maximum value for numeric types
    /// </summary>
    public double? MaxValue { get; init; }
    
    /// <summary>
    /// Parameter description
    /// </summary>
    public string Description { get; init; } = string.Empty;
    
    /// <summary>
    /// Allowed values for enum/choice parameters
    /// </summary>
    public object[]? AllowedValues { get; init; }
}
