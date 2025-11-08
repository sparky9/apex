using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Engine;

/// <summary>
/// Service for managing indicator instances and calculations
/// Provides centralized indicator lifecycle management
/// </summary>
public class IndicatorService
{
    private readonly Dictionary<string, IIndicator> _indicators = new();
    private readonly Dictionary<string, IndicatorFactory> _factories = new();
    private readonly IChartLogger _logger;
    
    public IndicatorService(IChartLogger? logger = null)
    {
        _logger = logger ?? new NullLogger();
        RegisterBuiltInFactories();
    }
    
    /// <summary>
    /// Event fired when an indicator is added
    /// </summary>
    public event EventHandler<IndicatorEventArgs>? IndicatorAdded;
    
    /// <summary>
    /// Event fired when an indicator is removed
    /// </summary>
    public event EventHandler<IndicatorEventArgs>? IndicatorRemoved;
    
    /// <summary>
    /// Event fired when an indicator is updated
    /// </summary>
    public event EventHandler<IndicatorUpdatedEventArgs>? IndicatorUpdated;
    
    /// <summary>
    /// Get all registered indicators
    /// </summary>
    public IReadOnlyDictionary<string, IIndicator> Indicators => _indicators.AsReadOnly();
    
    /// <summary>
    /// Get all available indicator types
    /// </summary>
    public IReadOnlyDictionary<string, IndicatorFactory> AvailableTypes => _factories.AsReadOnly();
    
    /// <summary>
    /// Register an indicator factory
    /// </summary>
    /// <param name="type">Indicator type identifier</param>
    /// <param name="factory">Factory function</param>
    public void RegisterIndicatorType(string type, IndicatorFactory factory)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Indicator type cannot be null or empty", nameof(type));
        }
        
        if (factory == null)
        {
            throw new ArgumentNullException(nameof(factory));
        }
        
        _factories[type] = factory;
        _logger.Info($"Registered indicator type: {type}");
    }
    
    /// <summary>
    /// Create a new indicator instance
    /// </summary>
    /// <param name="type">Indicator type</param>
    /// <param name="instanceId">Unique instance ID</param>
    /// <param name="parameters">Indicator parameters</param>
    /// <returns>Created indicator instance</returns>
    public IIndicator CreateIndicator(string type, string instanceId, Dictionary<string, object>? parameters = null)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ArgumentException("Indicator type cannot be null or empty", nameof(type));
        }
        
        if (string.IsNullOrWhiteSpace(instanceId))
        {
            throw new ArgumentException("Instance ID cannot be null or empty", nameof(instanceId));
        }
        
        if (_indicators.ContainsKey(instanceId))
        {
            throw new InvalidOperationException($"Indicator with ID '{instanceId}' already exists");
        }
        
        if (!_factories.TryGetValue(type, out var factory))
        {
            throw new ArgumentException($"Unknown indicator type: {type}", nameof(type));
        }
        
        try
        {
            var indicator = factory(instanceId, parameters ?? new Dictionary<string, object>(), _logger);
            
            // Validate parameters
            var validation = indicator.ValidateParameters();
            if (!validation.IsValid)
            {
                throw new ArgumentException($"Invalid parameters: {string.Join(", ", validation.Errors)}");
            }
            
            _indicators[instanceId] = indicator;
            
            _logger.Info($"Created indicator: {type} with ID {instanceId}");
            IndicatorAdded?.Invoke(this, new IndicatorEventArgs(indicator));
            
            return indicator;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create indicator {type} with ID {instanceId}", ex);
            throw;
        }
    }
    
    /// <summary>
    /// Get an indicator by ID
    /// </summary>
    /// <param name="instanceId">Instance ID</param>
    /// <returns>Indicator instance or null if not found</returns>
    public IIndicator? GetIndicator(string instanceId)
    {
        return _indicators.TryGetValue(instanceId, out var indicator) ? indicator : null;
    }
    
    /// <summary>
    /// Remove an indicator
    /// </summary>
    /// <param name="instanceId">Instance ID</param>
    /// <returns>True if removed, false if not found</returns>
    public bool RemoveIndicator(string instanceId)
    {
        if (_indicators.TryGetValue(instanceId, out var indicator))
        {
            _indicators.Remove(instanceId);
            indicator.Reset();
            
            _logger.Info($"Removed indicator: {instanceId}");
            IndicatorRemoved?.Invoke(this, new IndicatorEventArgs(indicator));
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Update all indicators with new data
    /// </summary>
    /// <param name="newData">New candlestick data</param>
    public void UpdateAllIndicators(Charts.Models.CandlestickData newData)
    {
        var results = new List<(IIndicator Indicator, IndicatorResult Result)>();
        
        foreach (var indicator in _indicators.Values)
        {
            try
            {
                var result = indicator.Update(newData);
                results.Add((indicator, result));
                
                if (!result.IsValid)
                {
                    _logger.Warn($"Indicator {indicator.Id} update failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating indicator {indicator.Id}", ex);
            }
        }
        
        // Fire update events
        foreach (var (indicator, result) in results)
        {
            IndicatorUpdated?.Invoke(this, new IndicatorUpdatedEventArgs(indicator, result));
        }
    }
    
    /// <summary>
    /// Reset all indicators
    /// </summary>
    public void ResetAllIndicators()
    {
        foreach (var indicator in _indicators.Values)
        {
            try
            {
                indicator.Reset();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error resetting indicator {indicator.Id}", ex);
            }
        }
        
        _logger.Info("Reset all indicators");
    }
    
    /// <summary>
    /// Get indicators by category
    /// </summary>
    /// <param name="category">Indicator category</param>
    /// <returns>Matching indicators</returns>
    public IEnumerable<IIndicator> GetIndicatorsByCategory(IndicatorCategory category)
    {
        return _indicators.Values.Where(i => i.Category == category);
    }
    
    /// <summary>
    /// Get indicator statistics
    /// </summary>
    /// <returns>Statistics about registered indicators</returns>
    public IndicatorStatistics GetStatistics()
    {
        var stats = new IndicatorStatistics
        {
            TotalIndicators = _indicators.Count,
            AvailableTypes = _factories.Count,
            CategoryCounts = new Dictionary<IndicatorCategory, int>()
        };
        
        foreach (var category in Enum.GetValues<IndicatorCategory>())
        {
            stats.CategoryCounts[category] = _indicators.Values.Count(i => i.Category == category);
        }
        
        return stats;
    }
    
    /// <summary>
    /// Register built-in indicator factories
    /// </summary>
    private void RegisterBuiltInFactories()
    {
        // Built-in indicators will be registered here in Component 20
        // For now, we'll have a placeholder system ready
        _logger.Debug("Built-in indicator factories registered");
    }
}

/// <summary>
/// Factory delegate for creating indicator instances
/// </summary>
/// <param name="instanceId">Unique instance ID</param>
/// <param name="parameters">Indicator parameters</param>
/// <param name="logger">Logger instance</param>
/// <returns>Created indicator</returns>
public delegate IIndicator IndicatorFactory(string instanceId, Dictionary<string, object> parameters, IChartLogger logger);

/// <summary>
/// Event arguments for indicator events
/// </summary>
public class IndicatorEventArgs : EventArgs
{
    public IIndicator Indicator { get; }
    
    public IndicatorEventArgs(IIndicator indicator)
    {
        Indicator = indicator;
    }
}

/// <summary>
/// Event arguments for indicator update events
/// </summary>
public class IndicatorUpdatedEventArgs : EventArgs
{
    public IIndicator Indicator { get; }
    public IndicatorResult Result { get; }
    
    public IndicatorUpdatedEventArgs(IIndicator indicator, IndicatorResult result)
    {
        Indicator = indicator;
        Result = result;
    }
}

/// <summary>
/// Statistics about registered indicators
/// </summary>
public class IndicatorStatistics
{
    /// <summary>
    /// Total number of registered indicator instances
    /// </summary>
    public int TotalIndicators { get; init; }
    
    /// <summary>
    /// Number of available indicator types
    /// </summary>
    public int AvailableTypes { get; init; }
    
    /// <summary>
    /// Count of indicators by category
    /// </summary>
    public Dictionary<IndicatorCategory, int> CategoryCounts { get; init; } = new();
}
