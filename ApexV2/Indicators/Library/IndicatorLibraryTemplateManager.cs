using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Indicators.Custom;
using ApexV2.Indicators.Engine;
using ApexV2.Indicators.Basic;
using ApexV2.Charts.Export;
using ApexV2.Core.Logging;

namespace ApexV2.Indicators.Library;

/// <summary>
/// Manages library templates and presets for indicators
/// </summary>
public class IndicatorLibraryTemplateManager
{
    private readonly IndicatorLibraryManager _libraryManager;
    private readonly CustomIndicatorService _customIndicatorService;
    private readonly IndicatorService _indicatorService;
    private readonly IChartLogger _logger;
    private readonly List<IndicatorTemplate> _templates;

    public IndicatorLibraryTemplateManager(IndicatorLibraryManager libraryManager, CustomIndicatorService customIndicatorService, IndicatorService indicatorService, IChartLogger? logger = null)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _customIndicatorService = customIndicatorService ?? throw new ArgumentNullException(nameof(customIndicatorService));
        _indicatorService = indicatorService ?? throw new ArgumentNullException(nameof(indicatorService));
        _logger = logger ?? new NullLogger();
        _templates = new List<IndicatorTemplate>();

        InitializeBuiltInTemplates();
    }

    /// <summary>
    /// Get all available templates
    /// </summary>
    public IReadOnlyList<IndicatorTemplate> GetAllTemplates()
    {
        return _templates.AsReadOnly();
    }

    /// <summary>
    /// Get templates by category
    /// </summary>
    public IReadOnlyList<IndicatorTemplate> GetTemplatesByCategory(string category)
    {
        return _templates.Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    /// <summary>
    /// Create an indicator from a template
    /// </summary>
    public async Task<CustomIndicatorConfig?> CreateIndicatorFromTemplateAsync(string templateId, string name, string author, Dictionary<string, object>? parameters = null)
    {
        try
        {
            var template = _templates.FirstOrDefault(t => t.Id == templateId);
            if (template == null)
            {
                _logger.Error($"Template not found: {templateId}");
                return null;
            }

            CustomIndicatorConfig? config = null;

            switch (template.Type)
            {
                case IndicatorTemplateType.Formula:
                    config = await CreateFormulaFromTemplateAsync(template, name, author, parameters);
                    break;

                case IndicatorTemplateType.Composite:
                    config = await CreateCompositeFromTemplateAsync(template, name, author, parameters);
                    break;

                case IndicatorTemplateType.BuiltIn:
                    config = await CreateBuiltInFromTemplateAsync(template, name, author, parameters);
                    break;
            }

            if (config != null)
            {
                // Add to library
                await _libraryManager.AddIndicatorToLibraryAsync(config, template.Category, template.Description);
                _logger.Info($"Created indicator '{name}' from template '{template.Name}'");
            }

            return config;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create indicator from template: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Create a new template from an existing indicator
    /// </summary>
    public async Task<bool> CreateTemplateFromIndicatorAsync(string indicatorId, string templateName, string description, string category)
    {
        try
        {
            if (!Guid.TryParse(indicatorId, out var guid))
                return false;
                
            var libraryItem = _libraryManager.GetAllIndicators().FirstOrDefault(i => i.Id == guid);
            if (libraryItem == null)
                return false;

            var customIndicators = _customIndicatorService.GetAllIndicators();
            var config = customIndicators.FirstOrDefault(c => c.Id == indicatorId);
            if (config == null)
                return false;

            var template = new IndicatorTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = templateName,
                Description = description,
                Category = category,
                Type = IndicatorTemplateType.Formula, // Assume formula for now
                Formula = config.Formula,
                DefaultParameters = new Dictionary<string, object>(config.Parameters),
                Tags = new List<string>(config.Tags),
                Author = "System",
                CreatedDate = DateTime.UtcNow,
                IsBuiltIn = false
            };

            _templates.Add(template);
            _logger.Info($"Created template '{templateName}' from indicator '{libraryItem.Name}'");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create template from indicator: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Get starter templates for new users
    /// </summary>
    public IReadOnlyList<IndicatorTemplate> GetStarterTemplates()
    {
        return _templates.Where(t => t.IsStarter).OrderBy(t => t.SortOrder).ToList();
    }

    /// <summary>
    /// Search templates
    /// </summary>
    public IReadOnlyList<IndicatorTemplate> SearchTemplates(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return GetAllTemplates();

        var term = searchTerm.ToLowerInvariant();
        return _templates.Where(t =>
            t.Name.ToLowerInvariant().Contains(term) ||
            t.Description.ToLowerInvariant().Contains(term) ||
            t.Tags.Any(tag => tag.ToLowerInvariant().Contains(term))
        ).ToList();
    }

    private async Task<CustomIndicatorConfig?> CreateFormulaFromTemplateAsync(IndicatorTemplate template, string name, string author, Dictionary<string, object>? parameters)
    {
        if (string.IsNullOrWhiteSpace(template.Formula))
            return null;

        var config = _customIndicatorService.CreateFormulaIndicator(name, template.Formula, author, template.Description, parameters ?? template.DefaultParameters);
        
        // Add template tags
        config.Tags.AddRange(template.Tags);
        
        return config;
    }

    private async Task<CustomIndicatorConfig?> CreateCompositeFromTemplateAsync(IndicatorTemplate template, string name, string author, Dictionary<string, object>? parameters)
    {
        if (template.Components == null || !template.Components.Any())
            return null;

        var components = new List<ComponentDefinition>();
        
        foreach (var comp in template.Components)
        {
            components.Add(new ComponentDefinition
            {
                Type = comp.Type,
                Alias = comp.Alias,
                Weight = comp.Weight,
                Parameters = new Dictionary<string, object>(comp.Parameters)
            });
        }

        var config = _customIndicatorService.CreateCompositeIndicator(name, author, components, 
            template.DefaultParameters.GetValueOrDefault("combineMethod", "weighted_average").ToString(), 
            template.Description);
        
        // Apply additional parameters
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                config.Parameters[param.Key] = param.Value;
            }
        }

        // Add template tags
        config.Tags.AddRange(template.Tags);
        
        return config;
    }

    private async Task<CustomIndicatorConfig?> CreateBuiltInFromTemplateAsync(IndicatorTemplate template, string name, string author, Dictionary<string, object>? parameters)
    {
        // For built-in indicators, create a preset configuration
        var config = new CustomIndicatorConfig
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            Description = template.Description,
            Author = author,
            Formula = template.Formula ?? string.Empty,
            Version = 1,
            Tags = new List<string>(template.Tags),
            Parameters = new Dictionary<string, object>(parameters ?? template.DefaultParameters),
            CreatedDate = DateTime.UtcNow,
            ModifiedDate = DateTime.UtcNow,
            MinimumDataPoints = template.MinimumDataPoints
        };

        return config;
    }

    private void InitializeBuiltInTemplates()
    {
        var builtInTemplates = new[]
        {
            // Trend Templates
            new IndicatorTemplate
            {
                Id = "template_simple_trend",
                Name = "Simple Trend Following",
                Description = "Basic trend following using moving averages",
                Category = "Trend",
                Type = IndicatorTemplateType.Formula,
                Formula = "close > sma(close, 20) ? 1 : -1",
                DefaultParameters = new Dictionary<string, object> { ["lookback"] = 20 },
                Tags = new List<string> { "trend", "moving average", "simple" },
                Author = "APEX",
                CreatedDate = DateTime.UtcNow,
                IsBuiltIn = true,
                IsStarter = true,
                SortOrder = 1
            },

            new IndicatorTemplate
            {
                Id = "template_dual_ma_cross",
                Name = "Dual Moving Average Crossover",
                Description = "Two moving averages with crossover signals",
                Category = "Trend", 
                Type = IndicatorTemplateType.Composite,
                Components = new List<TemplateComponent>
                {
                    new TemplateComponent { Type = "SMA", Alias = "fast", Weight = 1.0, Parameters = new Dictionary<string, object> { ["period"] = 10 } },
                    new TemplateComponent { Type = "SMA", Alias = "slow", Weight = 1.0, Parameters = new Dictionary<string, object> { ["period"] = 20 } }
                },
                DefaultParameters = new Dictionary<string, object> { ["combineMethod"] = "signal_cross" },
                Tags = new List<string> { "trend", "crossover", "signals" },
                Author = "APEX",
                CreatedDate = DateTime.UtcNow,
                IsBuiltIn = true,
                IsStarter = true,
                SortOrder = 2
            },

            // Momentum Templates
            new IndicatorTemplate
            {
                Id = "template_momentum_osc",
                Name = "Custom Momentum Oscillator",
                Description = "Price momentum with customizable period",
                Category = "Momentum",
                Type = IndicatorTemplateType.Formula,
                Formula = "((close - close[n]) / close[n]) * 100",
                DefaultParameters = new Dictionary<string, object> { ["n"] = 14, ["lookback"] = 14 },
                Tags = new List<string> { "momentum", "oscillator", "rate of change" },
                Author = "APEX",
                CreatedDate = DateTime.UtcNow,
                IsBuiltIn = true,
                IsStarter = true,
                SortOrder = 3
            },

            // Volume Templates
            new IndicatorTemplate
            {
                Id = "template_volume_price",
                Name = "Volume-Price Trend",
                Description = "Combines volume and price movement",
                Category = "Volume",
                Type = IndicatorTemplateType.Formula,
                Formula = "volume * ((close - open) / high - low)",
                DefaultParameters = new Dictionary<string, object> { ["lookback"] = 1 },
                Tags = new List<string> { "volume", "price", "trend" },
                Author = "APEX",
                CreatedDate = DateTime.UtcNow,
                IsBuiltIn = true,
                IsStarter = false,
                SortOrder = 4
            },

            // Volatility Templates
            new IndicatorTemplate
            {
                Id = "template_volatility_range",
                Name = "True Range Volatility",
                Description = "Measures price volatility using true range",
                Category = "Volatility",
                Type = IndicatorTemplateType.Formula,
                Formula = "max(high - low, abs(high - close[1]), abs(low - close[1]))",
                DefaultParameters = new Dictionary<string, object> { ["lookback"] = 1 },
                Tags = new List<string> { "volatility", "true range", "risk" },
                Author = "APEX",
                CreatedDate = DateTime.UtcNow,
                IsBuiltIn = true,
                IsStarter = false,
                SortOrder = 5
            }
        };

        _templates.AddRange(builtInTemplates);
        _logger.Info($"Initialized {builtInTemplates.Length} built-in templates");
    }
}

/// <summary>
/// Represents an indicator template
/// </summary>
public class IndicatorTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public IndicatorTemplateType Type { get; set; }
    public string? Formula { get; set; }
    public List<TemplateComponent>? Components { get; set; }
    public Dictionary<string, object> DefaultParameters { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public bool IsBuiltIn { get; set; } = false;
    public bool IsStarter { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public int MinimumDataPoints { get; set; } = 1;
    public string? PreviewImage { get; set; }
}

/// <summary>
/// Template component for composite indicators
/// </summary>
public class TemplateComponent
{
    public string Type { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Template type enumeration
/// </summary>
public enum IndicatorTemplateType
{
    Formula,
    Composite,
    BuiltIn
}
