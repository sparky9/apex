using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Analysis.Scanner;

namespace ApexV2.Analysis.Scanner;

/// <summary>
/// Service for managing scan templates and presets
/// </summary>
public class ScanTemplateManager
{
    private readonly Dictionary<Guid, ScanTemplate> _templates = new();
    private readonly object _lockObject = new();

    /// <summary>
    /// Get all available scan templates
    /// </summary>
    public IReadOnlyList<ScanTemplate> GetAllTemplates()
    {
        lock (_lockObject)
        {
            return _templates.Values.OrderBy(t => t.Category).ThenBy(t => t.Name).ToList();
        }
    }

    /// <summary>
    /// Get templates by category
    /// </summary>
    public IReadOnlyList<ScanTemplate> GetTemplatesByCategory(string category)
    {
        lock (_lockObject)
        {
            return _templates.Values
                .Where(t => t.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.Name)
                .ToList();
        }
    }

    /// <summary>
    /// Get a specific template
    /// </summary>
    public ScanTemplate? GetTemplate(Guid id)
    {
        lock (_lockObject)
        {
            return _templates.TryGetValue(id, out var template) ? template : null;
        }
    }

    /// <summary>
    /// Save a template
    /// </summary>
    public void SaveTemplate(ScanTemplate template)
    {
        lock (_lockObject)
        {
            template.ModifiedDate = DateTime.Now;
            _templates[template.Id] = template;
        }
    }

    /// <summary>
    /// Delete a template
    /// </summary>
    public bool DeleteTemplate(Guid id)
    {
        lock (_lockObject)
        {
            return _templates.Remove(id);
        }
    }

    /// <summary>
    /// Create a template from a scan configuration
    /// </summary>
    public ScanTemplate CreateTemplateFromScan(ScanConfiguration config, string category = "Custom")
    {
        return new ScanTemplate
        {
            Name = config.Name,
            Description = config.Description,
            Category = category,
            Criteria = config.Criteria.Select(c => new ScanCriterion
            {
                Type = c.Type,
                Operator = c.Operator,
                Value = c.Value,
                SecondValue = c.SecondValue,
                TextValue = c.TextValue,
                IndicatorId = c.IndicatorId,
                Parameters = new Dictionary<string, object>(c.Parameters),
                IsEnabled = c.IsEnabled,
                SortOrder = c.SortOrder
            }).ToList(),
            Markets = new List<string>(config.Markets),
            Sectors = new List<string>(config.Sectors),
            Industries = new List<string>(config.Industries),
            ResultSettings = new ScanResultSettings
            {
                MaxResults = config.ResultSettings.MaxResults,
                SortBy = config.ResultSettings.SortBy,
                SortDescending = config.ResultSettings.SortDescending,
                IncludeFundamentals = config.ResultSettings.IncludeFundamentals,
                IncludeTechnicals = config.ResultSettings.IncludeTechnicals,
                EnableRealTimeUpdates = config.ResultSettings.EnableRealTimeUpdates,
                VisibleColumns = new List<string>(config.ResultSettings.VisibleColumns)
            },
            Tags = new List<string>(config.Tags),
            Author = config.Author
        };
    }

    /// <summary>
    /// Create a scan configuration from a template
    /// </summary>
    public ScanConfiguration CreateScanFromTemplate(ScanTemplate template)
    {
        return new ScanConfiguration
        {
            Name = template.Name,
            Description = template.Description,
            Criteria = template.Criteria.Select(c => new ScanCriterion
            {
                Type = c.Type,
                Operator = c.Operator,
                Value = c.Value,
                SecondValue = c.SecondValue,
                TextValue = c.TextValue,
                IndicatorId = c.IndicatorId,
                Parameters = new Dictionary<string, object>(c.Parameters),
                IsEnabled = c.IsEnabled,
                SortOrder = c.SortOrder
            }).ToList(),
            Markets = new List<string>(template.Markets),
            Sectors = new List<string>(template.Sectors),
            Industries = new List<string>(template.Industries),
            ResultSettings = new ScanResultSettings
            {
                MaxResults = template.ResultSettings.MaxResults,
                SortBy = template.ResultSettings.SortBy,
                SortDescending = template.ResultSettings.SortDescending,
                IncludeFundamentals = template.ResultSettings.IncludeFundamentals,
                IncludeTechnicals = template.ResultSettings.IncludeTechnicals,
                EnableRealTimeUpdates = template.ResultSettings.EnableRealTimeUpdates,
                VisibleColumns = new List<string>(template.ResultSettings.VisibleColumns)
            },
            Tags = new List<string>(template.Tags),
            Author = template.Author
        };
    }

    /// <summary>
    /// Initialize with built-in templates
    /// </summary>
    public void InitializeBuiltInTemplates()
    {
        var builtInTemplates = new[]
        {
            CreateMomentumTemplate(),
            CreateValueTemplate(),
            CreateGrowthTemplate(),
            CreateOversoldTemplate(),
            CreateBreakoutTemplate(),
            CreateHighVolumeTemplate(),
            CreateDividendTemplate(),
            CreateSmallCapTemplate(),
            CreateLargeCapTemplate(),
            CreateTechnicalTemplate()
        };

        foreach (var template in builtInTemplates)
        {
            SaveTemplate(template);
        }
    }

    private ScanTemplate CreateMomentumTemplate()
    {
        return new ScanTemplate
        {
            Name = "Momentum Stocks",
            Description = "Stocks showing strong upward momentum with high RSI and positive price movement",
            Category = "Technical",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.PriceChangePercent, Operator = ScanOperator.GreaterThan, Value = 3, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.RSI, Operator = ScanOperator.Between, Value = 60, SecondValue = 80, IsEnabled = true, SortOrder = 2 },
                new() { Type = ScanCriterionType.Volume, Operator = ScanOperator.GreaterThan, Value = 500000, IsEnabled = true, SortOrder = 3 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.ChangePercent, SortDescending = true, MaxResults = 50 },
            Tags = new List<string> { "Momentum", "Technical", "Growth", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }

    private ScanTemplate CreateValueTemplate()
    {
        return new ScanTemplate
        {
            Name = "Value Stocks",
            Description = "Undervalued stocks with attractive P/E ratios and dividend yields",
            Category = "Fundamental",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.PERatio, Operator = ScanOperator.Between, Value = 5, SecondValue = 15, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.DividendYield, Operator = ScanOperator.GreaterThan, Value = 2, IsEnabled = true, SortOrder = 2 },
                new() { Type = ScanCriterionType.DebtToEquity, Operator = ScanOperator.LessThan, Value = 1.5m, IsEnabled = true, SortOrder = 3 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.PERatio, SortDescending = false, MaxResults = 50 },
            Tags = new List<string> { "Value", "Fundamental", "Dividend", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }

    private ScanTemplate CreateGrowthTemplate()
    {
        return new ScanTemplate
        {
            Name = "Growth Stocks",
            Description = "High-growth companies with strong earnings and revenue growth",
            Category = "Fundamental",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.EPSGrowth, Operator = ScanOperator.GreaterThan, Value = 20, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.RevenueGrowth, Operator = ScanOperator.GreaterThan, Value = 15, IsEnabled = true, SortOrder = 2 },
                new() { Type = ScanCriterionType.ROE, Operator = ScanOperator.GreaterThan, Value = 15, IsEnabled = true, SortOrder = 3 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.Custom, SortDescending = true, MaxResults = 50 },
            Tags = new List<string> { "Growth", "Fundamental", "Earnings", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }

    private ScanTemplate CreateOversoldTemplate()
    {
        return new ScanTemplate
        {
            Name = "Oversold Opportunities",
            Description = "Potentially oversold stocks with low RSI and significant price declines",
            Category = "Technical",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.RSI, Operator = ScanOperator.LessThan, Value = 30, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.PriceChangePercent, Operator = ScanOperator.LessThan, Value = -5, IsEnabled = true, SortOrder = 2 },
                new() { Type = ScanCriterionType.Volume, Operator = ScanOperator.GreaterThan, Value = 300000, IsEnabled = true, SortOrder = 3 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.RSI, SortDescending = false, MaxResults = 50 },
            Tags = new List<string> { "Oversold", "Technical", "Opportunity", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }

    private ScanTemplate CreateBreakoutTemplate()
    {
        return new ScanTemplate
        {
            Name = "Breakout Candidates",
            Description = "Stocks breaking out of resistance levels with high volume",
            Category = "Technical",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.BreakoutUp, Operator = ScanOperator.Equals, Value = 1, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.Volume, Operator = ScanOperator.GreaterThan, Value = 1000000, IsEnabled = true, SortOrder = 2 },
                new() { Type = ScanCriterionType.PriceChangePercent, Operator = ScanOperator.GreaterThan, Value = 2, IsEnabled = true, SortOrder = 3 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.Volume, SortDescending = true, MaxResults = 30 },
            Tags = new List<string> { "Breakout", "Technical", "Momentum", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }

    private ScanTemplate CreateHighVolumeTemplate()
    {
        return new ScanTemplate
        {
            Name = "High Volume Activity",
            Description = "Stocks with unusually high trading volume",
            Category = "Volume",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.Volume, Operator = ScanOperator.GreaterThan, Value = 2000000, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.VolumeAverage, Operator = ScanOperator.GreaterThan, Value = 150, IsEnabled = true, SortOrder = 2 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.Volume, SortDescending = true, MaxResults = 100 },
            Tags = new List<string> { "Volume", "Activity", "Liquidity", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }

    private ScanTemplate CreateDividendTemplate()
    {
        return new ScanTemplate
        {
            Name = "Dividend Champions",
            Description = "Stocks with attractive dividend yields and stable fundamentals",
            Category = "Dividend",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.DividendYield, Operator = ScanOperator.GreaterThan, Value = 3, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.PERatio, Operator = ScanOperator.LessThan, Value = 25, IsEnabled = true, SortOrder = 2 },
                new() { Type = ScanCriterionType.DebtToEquity, Operator = ScanOperator.LessThan, Value = 2, IsEnabled = true, SortOrder = 3 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.Custom, SortDescending = true, MaxResults = 50 },
            Tags = new List<string> { "Dividend", "Income", "Stable", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }

    private ScanTemplate CreateSmallCapTemplate()
    {
        return new ScanTemplate
        {
            Name = "Small Cap Opportunities",
            Description = "Small capitalization stocks with growth potential",
            Category = "Market Cap",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.MarketCap, Operator = ScanOperator.Between, Value = 300000000, SecondValue = 2000000000, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.Volume, Operator = ScanOperator.GreaterThan, Value = 100000, IsEnabled = true, SortOrder = 2 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.MarketCap, SortDescending = false, MaxResults = 75 },
            Tags = new List<string> { "SmallCap", "Growth", "Opportunity", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }

    private ScanTemplate CreateLargeCapTemplate()
    {
        return new ScanTemplate
        {
            Name = "Large Cap Stability",
            Description = "Large capitalization stocks for stable investments",
            Category = "Market Cap",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.MarketCap, Operator = ScanOperator.GreaterThan, Value = 10000000000, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.Volume, Operator = ScanOperator.GreaterThan, Value = 1000000, IsEnabled = true, SortOrder = 2 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.MarketCap, SortDescending = true, MaxResults = 50 },
            Tags = new List<string> { "LargeCap", "Stable", "BlueChip", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }

    private ScanTemplate CreateTechnicalTemplate()
    {
        return new ScanTemplate
        {
            Name = "Technical Analysis",
            Description = "Stocks meeting multiple technical indicators criteria",
            Category = "Technical",
            Criteria = new List<ScanCriterion>
            {
                new() { Type = ScanCriterionType.RSI, Operator = ScanOperator.Between, Value = 40, SecondValue = 70, IsEnabled = true, SortOrder = 1 },
                new() { Type = ScanCriterionType.MACD, Operator = ScanOperator.GreaterThan, Value = 0, IsEnabled = true, SortOrder = 2 },
                new() { Type = ScanCriterionType.SMA, Operator = ScanOperator.CrossesAbove, Value = 1, IsEnabled = true, SortOrder = 3 }
            },
            ResultSettings = new ScanResultSettings { SortBy = ScanSortBy.Score, SortDescending = true, MaxResults = 50 },
            Tags = new List<string> { "Technical", "Indicators", "Analysis", "Built-in" },
            IsBuiltIn = true,
            Author = "APEX"
        };
    }
}

/// <summary>
/// Template for creating scan configurations
/// </summary>
public class ScanTemplate
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = "Custom";
    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime ModifiedDate { get; set; } = DateTime.Now;
    
    public List<ScanCriterion> Criteria { get; set; } = new();
    public List<string> Markets { get; set; } = new();
    public List<string> Sectors { get; set; } = new();
    public List<string> Industries { get; set; } = new();
    public ScanResultSettings ResultSettings { get; set; } = new();
    
    public string Author { get; set; } = Environment.UserName;
    public List<string> Tags { get; set; } = new();
    public bool IsBuiltIn { get; set; } = false;
    public bool IsPublic { get; set; } = false;
    public int DownloadCount { get; set; } = 0;
    public decimal Rating { get; set; } = 0m;
    public int UsageCount { get; set; } = 0;
}
