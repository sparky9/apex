using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Custom;

/// <summary>
/// Custom indicator that combines multiple existing indicators
/// </summary>
public class CompositeIndicator : CustomIndicatorBase
{
    private readonly List<IndicatorComponent> _components;
    private readonly Dictionary<string, IIndicator> _indicators;
    
    public CompositeIndicator(string id, string name, string author, IChartLogger? logger = null)
        : base(id, name, "Composite indicator combining multiple indicators", author, logger)
    {
        _components = new List<IndicatorComponent>();
        _indicators = new Dictionary<string, IIndicator>();
        SetParameter("combineMethod", "weighted_average");
    }
    
    public override int MinimumDataPoints => _indicators.Values.Any() ? _indicators.Values.Max(i => i.MinimumDataPoints) : 1;
    
    /// <summary>
    /// Add an indicator component to this composite
    /// </summary>
    /// <param name="indicator">The indicator to add</param>
    /// <param name="weight">Weight for combining results</param>
    /// <param name="alias">Alias name for referencing this indicator</param>
    public void AddComponent(IIndicator indicator, double weight = 1.0, string? alias = null)
    {
        if (indicator == null) throw new ArgumentNullException(nameof(indicator));
        
        var componentId = alias ?? indicator.Id;
        if (_indicators.ContainsKey(componentId))
        {
            throw new ArgumentException($"Component with ID '{componentId}' already exists");
        }
        
        var component = new IndicatorComponent
        {
            Indicator = indicator,
            Weight = weight,
            Alias = componentId
        };
        
        _components.Add(component);
        _indicators[componentId] = indicator;
        
        // Update minimum data points
        UpdateMinimumDataPoints();
    }
    
    /// <summary>
    /// Remove an indicator component
    /// </summary>
    /// <param name="componentId">ID or alias of the component to remove</param>
    public void RemoveComponent(string componentId)
    {
        if (_indicators.ContainsKey(componentId))
        {
            _indicators.Remove(componentId);
            _components.RemoveAll(c => c.Alias == componentId);
            UpdateMinimumDataPoints();
        }
    }
    
    /// <summary>
    /// Set the weight for a component
    /// </summary>
    /// <param name="componentId">Component ID</param>
    /// <param name="weight">New weight</param>
    public void SetComponentWeight(string componentId, double weight)
    {
        var component = _components.FirstOrDefault(c => c.Alias == componentId);
        if (component != null)
        {
            component.Weight = weight;
        }
    }
    
    public override IIndicator Clone()
    {
        var clone = new CompositeIndicator(Id + "_clone", Name, Author);
        clone.Parameters.Clear();
        foreach (var param in Parameters)
        {
            clone.Parameters[param.Key] = param.Value;
        }
        clone.Tags.AddRange(Tags);
        
        // Clone components
        foreach (var component in _components)
        {
            clone.AddComponent(component.Indicator.Clone(), component.Weight, component.Alias);
        }
        
        return clone;
    }
    
    protected override IndicatorResult CalculateInternal(CandlestickData[] data)
    {
        if (!_indicators.Any())
        {
            return new IndicatorResult 
            { 
                IsValid = false, 
                Timestamp = data[^1].Timestamp,
                ErrorMessage = "No components added to composite indicator"
            };
        }
        
        var results = new Dictionary<string, IndicatorResult>();
        var validResults = new List<(IndicatorResult result, double weight)>();
        
        // Calculate all component indicators
        foreach (var component in _components)
        {
            try
            {
                var result = component.Indicator.Calculate(data);
                results[component.Alias] = result;
                
                if (result.IsValid && !double.IsNaN(result.Value))
                {
                    validResults.Add((result, component.Weight));
                }
            }
            catch (Exception ex)
            {
                results[component.Alias] = new IndicatorResult 
                { 
                    IsValid = false, 
                    Timestamp = data[^1].Timestamp,
                    ErrorMessage = $"Component '{component.Alias}' error: {ex.Message}"
                };
            }
        }
        
        if (!validResults.Any())
        {
            return new IndicatorResult 
            { 
                IsValid = false, 
                Timestamp = data[^1].Timestamp,
                ErrorMessage = "No valid component results"
            };
        }
        
        // Combine results based on method
        var combineMethod = GetParameter<string>("combineMethod", "weighted_average");
        var combinedValue = CombineResults(validResults, combineMethod);
        
        return new IndicatorResult
        {
            Value = combinedValue,
            IsValid = !double.IsNaN(combinedValue),
            Timestamp = data[^1].Timestamp,
            Metadata = new Dictionary<string, object>
            {
                ["componentCount"] = _components.Count,
                ["validResults"] = validResults.Count,
                ["combineMethod"] = combineMethod,
                ["componentResults"] = results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Value),
                ["weights"] = _components.ToDictionary(c => c.Alias, c => c.Weight)
            }
        };
    }
    
    protected override IndicatorValidationResult ValidateParametersInternal()
    {
        var errors = new List<string>();
        
        if (!_indicators.Any())
        {
            errors.Add("At least one component indicator is required");
        }
        
        var combineMethod = GetParameter<string>("combineMethod", "");
        var validMethods = new[] { "weighted_average", "sum", "max", "min", "median" };
        if (!validMethods.Contains(combineMethod))
        {
            errors.Add($"Invalid combine method. Valid options: {string.Join(", ", validMethods)}");
        }
        
        // Validate component indicators
        foreach (var component in _components)
        {
            var validation = component.Indicator.ValidateParameters();
            if (!validation.IsValid)
            {
                errors.Add($"Component '{component.Alias}' validation failed: {string.Join(", ", validation.Errors)}");
            }
        }
        
        return new IndicatorValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
    
    private double CombineResults(List<(IndicatorResult result, double weight)> results, string method)
    {
        switch (method.ToLower())
        {
            case "weighted_average":
                var totalWeight = results.Sum(r => r.weight);
                return totalWeight > 0 ? results.Sum(r => r.result.Value * r.weight) / totalWeight : 0;
                
            case "sum":
                return results.Sum(r => r.result.Value);
                
            case "max":
                return results.Max(r => r.result.Value);
                
            case "min":
                return results.Min(r => r.result.Value);
                
            case "median":
                var sortedValues = results.Select(r => r.result.Value).OrderBy(v => v).ToArray();
                var count = sortedValues.Length;
                return count % 2 == 0 
                    ? (sortedValues[count / 2 - 1] + sortedValues[count / 2]) / 2
                    : sortedValues[count / 2];
                
            default:
                return results.Average(r => r.result.Value);
        }
    }
    
    private void UpdateMinimumDataPoints()
    {
        // Update based on the maximum requirement of all components
        if (_indicators.Any())
        {
            var maxPoints = _indicators.Values.Max(i => i.MinimumDataPoints);
            SetParameter("_minimumDataPoints", maxPoints);
        }
    }
    
    /// <summary>
    /// Get component results for detailed analysis
    /// </summary>
    /// <param name="data">Market data</param>
    /// <returns>Dictionary of component results</returns>
    public Dictionary<string, IndicatorResult> GetComponentResults(CandlestickData[] data)
    {
        var results = new Dictionary<string, IndicatorResult>();
        
        foreach (var component in _components)
        {
            try
            {
                results[component.Alias] = component.Indicator.Calculate(data);
            }
            catch (Exception ex)
            {
                results[component.Alias] = new IndicatorResult 
                { 
                    IsValid = false, 
                    Timestamp = data[^1].Timestamp,
                    ErrorMessage = ex.Message
                };
            }
        }
        
        return results;
    }
}

/// <summary>
/// Represents a component indicator within a composite
/// </summary>
public class IndicatorComponent
{
    public IIndicator Indicator { get; set; } = null!;
    public double Weight { get; set; } = 1.0;
    public string Alias { get; set; } = string.Empty;
}
