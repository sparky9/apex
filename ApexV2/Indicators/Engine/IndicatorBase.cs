using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Engine;

/// <summary>
/// Abstract base class for technical indicators
/// Provides common functionality and structure for all indicators
/// </summary>
public abstract class IndicatorBase : IIndicator
{
    private readonly List<CandlestickData> _dataBuffer = new();
    private readonly List<IndicatorResult> _resultCache = new();
    private readonly IChartLogger _logger;
    
    protected IndicatorBase(string id, string name, string description, IndicatorCategory category, IChartLogger? logger = null)
    {
        Id = id;
        Name = name;
        Description = description;
        Category = category;
        Parameters = new Dictionary<string, object>();
        _logger = logger ?? new NullLogger();
    }
    
    /// <inheritdoc />
    public string Id { get; }
    
    /// <inheritdoc />
    public string Name { get; }
    
    /// <inheritdoc />
    public string Description { get; }
    
    /// <inheritdoc />
    public IndicatorCategory Category { get; }
    
    /// <inheritdoc />
    public Dictionary<string, object> Parameters { get; protected set; }
    
    /// <inheritdoc />
    public abstract int MinimumDataPoints { get; }
    
    /// <inheritdoc />
    public virtual bool IsReady => _dataBuffer.Count >= MinimumDataPoints;
    
    /// <summary>
    /// Current data buffer
    /// </summary>
    protected internal IReadOnlyList<CandlestickData> DataBuffer => _dataBuffer.AsReadOnly();
    
    /// <summary>
    /// Cached indicator results
    /// </summary>
    protected internal IReadOnlyList<IndicatorResult> ResultCache => _resultCache.AsReadOnly();
    
    /// <inheritdoc />
    public virtual IndicatorResult Calculate(IEnumerable<CandlestickData> data)
    {
        try
        {
            _dataBuffer.Clear();
            _resultCache.Clear();
            
            var dataArray = data.OrderBy(d => d.Timestamp).ToArray();
            if (dataArray.Length == 0)
            {
                return CreateErrorResult(DateTime.Now, "No data provided for calculation");
            }
            
            _dataBuffer.AddRange(dataArray);
            
            if (!IsReady)
            {
                return CreateErrorResult(dataArray.Last().Timestamp, 
                    $"Insufficient data: {_dataBuffer.Count} points, need {MinimumDataPoints}");
            }
            
            var result = CalculateInternal(dataArray);
            _resultCache.Add(result);
            
            _logger.Debug($"Calculated {Name} indicator", new Dictionary<string, object?>
            {
                ["DataPoints"] = dataArray.Length,
                ["Result"] = result.Value,
                ["IsValid"] = result.IsValid
            });
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error calculating {Name} indicator", ex);
            return CreateErrorResult(data.LastOrDefault()?.Timestamp ?? DateTime.Now, ex.Message);
        }
    }
    
    /// <inheritdoc />
    public virtual IndicatorResult Update(CandlestickData newData)
    {
        try
        {
            _dataBuffer.Add(newData);
            
            // Maintain reasonable buffer size (keep last 1000 points)
            if (_dataBuffer.Count > 1000)
            {
                _dataBuffer.RemoveAt(0);
            }
            
            if (!IsReady)
            {
                return CreateErrorResult(newData.Timestamp, 
                    $"Insufficient data: {_dataBuffer.Count} points, need {MinimumDataPoints}");
            }
            
            var result = UpdateInternal(newData);
            _resultCache.Add(result);
            
            // Maintain reasonable cache size
            if (_resultCache.Count > 1000)
            {
                _resultCache.RemoveAt(0);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error updating {Name} indicator", ex);
            return CreateErrorResult(newData.Timestamp, ex.Message);
        }
    }
    
    /// <inheritdoc />
    public virtual void Reset()
    {
        _dataBuffer.Clear();
        _resultCache.Clear();
        ResetInternal();
        
        _logger.Debug($"Reset {Name} indicator");
    }
    
    /// <inheritdoc />
    public abstract IIndicator Clone();
    
    /// <inheritdoc />
    public virtual IndicatorValidationResult ValidateParameters()
    {
        try
        {
            return ValidateParametersInternal();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error validating {Name} parameters", ex);
            return IndicatorValidationResult.Failure($"Validation error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Internal calculation method - override in derived classes
    /// </summary>
    /// <param name="data">Historical data for calculation</param>
    /// <returns>Indicator result</returns>
    protected abstract IndicatorResult CalculateInternal(CandlestickData[] data);
    
    /// <summary>
    /// Internal update method - override for incremental calculation
    /// Default implementation recalculates from all available data
    /// </summary>
    /// <param name="newData">New data point</param>
    /// <returns>Updated indicator result</returns>
    protected virtual IndicatorResult UpdateInternal(CandlestickData newData)
    {
        // Default: recalculate from all data
        // Override for more efficient incremental updates
        return CalculateInternal(_dataBuffer.ToArray());
    }
    
    /// <summary>
    /// Internal reset method - override for custom cleanup
    /// </summary>
    protected virtual void ResetInternal()
    {
        // Override in derived classes for custom reset logic
    }
    
    /// <summary>
    /// Internal parameter validation - override in derived classes
    /// </summary>
    /// <returns>Validation result</returns>
    protected virtual IndicatorValidationResult ValidateParametersInternal()
    {
        return IndicatorValidationResult.Success();
    }
    
    /// <summary>
    /// Set a parameter value with type checking
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="name">Parameter name</param>
    /// <param name="value">Parameter value</param>
    protected internal void SetParameter<T>(string name, T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value), $"Parameter {name} cannot be null");
        }
        
        Parameters[name] = value;
    }
    
    /// <summary>
    /// Get a parameter value with type checking
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    /// <param name="name">Parameter name</param>
    /// <param name="defaultValue">Default value if parameter not found</param>
    /// <returns>Parameter value</returns>
    protected T GetParameter<T>(string name, T defaultValue = default!)
    {
        if (!Parameters.TryGetValue(name, out var value))
        {
            return defaultValue;
        }
        
        if (value is T typedValue)
        {
            return typedValue;
        }
        
        // Try to convert
        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            _logger.Warn($"Failed to convert parameter {name} to type {typeof(T).Name}, using default");
            return defaultValue;
        }
    }
    
    /// <summary>
    /// Create a successful indicator result
    /// </summary>
    /// <param name="timestamp">Result timestamp</param>
    /// <param name="value">Primary value</param>
    /// <param name="additionalValues">Additional values</param>
    /// <param name="metadata">Metadata</param>
    /// <returns>Indicator result</returns>
    protected IndicatorResult CreateResult(DateTime timestamp, double value, 
        Dictionary<string, double>? additionalValues = null,
        Dictionary<string, object>? metadata = null)
    {
        return new IndicatorResult
        {
            Timestamp = timestamp,
            Value = value,
            AdditionalValues = additionalValues ?? new Dictionary<string, double>(),
            Metadata = metadata ?? new Dictionary<string, object>(),
            IsValid = true
        };
    }
    
    /// <summary>
    /// Create an error result
    /// </summary>
    /// <param name="timestamp">Result timestamp</param>
    /// <param name="errorMessage">Error message</param>
    /// <returns>Error result</returns>
    protected IndicatorResult CreateErrorResult(DateTime timestamp, string errorMessage)
    {
        return new IndicatorResult
        {
            Timestamp = timestamp,
            Value = double.NaN,
            IsValid = false,
            ErrorMessage = errorMessage
        };
    }
    
    /// <summary>
    /// Validate that a numeric parameter is within range
    /// </summary>
    /// <param name="paramName">Parameter name</param>
    /// <param name="value">Parameter value</param>
    /// <param name="min">Minimum value</param>
    /// <param name="max">Maximum value</param>
    /// <param name="errors">Error list to add to</param>
    protected void ValidateRange(string paramName, double value, double min, double max, List<string> errors)
    {
        if (value < min || value > max)
        {
            errors.Add($"{paramName} must be between {min} and {max}, got {value}");
        }
    }
    
    /// <summary>
    /// Validate that an integer parameter is positive
    /// </summary>
    /// <param name="paramName">Parameter name</param>
    /// <param name="value">Parameter value</param>
    /// <param name="errors">Error list to add to</param>
    protected void ValidatePositive(string paramName, int value, List<string> errors)
    {
        if (value <= 0)
        {
            errors.Add($"{paramName} must be positive, got {value}");
        }
    }
}
