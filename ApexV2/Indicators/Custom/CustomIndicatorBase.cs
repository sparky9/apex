using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Custom;

/// <summary>
/// Base class for custom indicators that can be created through code or configuration
/// </summary>
public abstract class CustomIndicatorBase : IndicatorBase
{
    /// <summary>
    /// Custom formula or calculation logic
    /// </summary>
    public string Formula { get; protected set; } = string.Empty;
    
    /// <summary>
    /// Version of the custom indicator for tracking changes
    /// </summary>
    public int Version { get; protected set; } = 1;
    
    /// <summary>
    /// Author of the custom indicator
    /// </summary>
    public string Author { get; protected set; } = "Unknown";
    
    /// <summary>
    /// Custom tags for categorization
    /// </summary>
    public List<string> Tags { get; protected set; } = new();
    
    protected CustomIndicatorBase(string id, string name, string description, string author, IChartLogger? logger = null)
        : base(id, name, description, IndicatorCategory.Custom, logger)
    {
        Author = author;
    }
    
    /// <summary>
    /// Set the formula for this custom indicator
    /// </summary>
    /// <param name="formula">Mathematical formula or calculation logic</param>
    public virtual void SetFormula(string formula)
    {
        Formula = formula ?? throw new ArgumentNullException(nameof(formula));
        Version++;
    }
    
    /// <summary>
    /// Add a tag to this indicator
    /// </summary>
    /// <param name="tag">Tag to add</param>
    public void AddTag(string tag)
    {
        if (!string.IsNullOrWhiteSpace(tag) && !Tags.Contains(tag))
        {
            Tags.Add(tag);
        }
    }
    
    /// <summary>
    /// Remove a tag from this indicator
    /// </summary>
    /// <param name="tag">Tag to remove</param>
    public void RemoveTag(string tag)
    {
        Tags.Remove(tag);
    }
    
    /// <summary>
    /// Export this custom indicator to a configuration format
    /// </summary>
    /// <returns>Custom indicator configuration</returns>
    public virtual CustomIndicatorConfig Export()
    {
        return new CustomIndicatorConfig
        {
            Id = Id,
            Name = Name,
            Description = Description,
            Author = Author,
            Formula = Formula,
            Version = Version,
            Tags = new List<string>(Tags),
            Parameters = new Dictionary<string, object>(Parameters),
            CreatedDate = DateTime.UtcNow,
            MinimumDataPoints = MinimumDataPoints
        };
    }
}

/// <summary>
/// Configuration for custom indicators that can be serialized
/// </summary>
public class CustomIndicatorConfig
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Formula { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, object> Parameters { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public int MinimumDataPoints { get; set; } = 1;
    public bool IsShared { get; set; } = false;
    public List<string> Dependencies { get; set; } = new(); // Other indicators this depends on
}
