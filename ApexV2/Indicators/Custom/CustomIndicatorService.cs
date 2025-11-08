using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Linq;
using ApexV2.Indicators.Engine;
using ApexV2.Indicators.Basic;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Custom;

/// <summary>
/// Service for managing custom indicators - creation, storage, and lifecycle
/// </summary>
public class CustomIndicatorService
{
    private readonly Dictionary<string, CustomIndicatorConfig> _customIndicators;
    private readonly string _storageDirectory;
    private readonly IChartLogger _logger;
    private readonly IndicatorService _indicatorService;
    
    public CustomIndicatorService(IndicatorService indicatorService, string? storageDirectory = null, IChartLogger? logger = null)
    {
        _indicatorService = indicatorService ?? throw new ArgumentNullException(nameof(indicatorService));
        _storageDirectory = storageDirectory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ApexV2", "CustomIndicators");
        _logger = logger ?? new NullLogger();
        _customIndicators = new Dictionary<string, CustomIndicatorConfig>();
        
        EnsureStorageDirectory();
        LoadCustomIndicators();
    }
    
    /// <summary>
    /// Create a new formula-based custom indicator
    /// </summary>
    /// <param name="name">Indicator name</param>
    /// <param name="formula">Mathematical formula</param>
    /// <param name="author">Author name</param>
    /// <param name="description">Description</param>
    /// <param name="parameters">Initial parameters</param>
    /// <returns>Created indicator configuration</returns>
    public CustomIndicatorConfig CreateFormulaIndicator(string name, string formula, string author, 
        string? description = null, Dictionary<string, object>? parameters = null)
    {
        var id = GenerateUniqueId(name);
        var indicator = new FormulaIndicator(id, name, formula, author, _logger);
        
        if (!string.IsNullOrWhiteSpace(description))
        {
            // Update description through reflection or add a setter method
        }
        
        if (parameters != null)
        {
            foreach (var param in parameters)
            {
                indicator.SetParameter(param.Key, param.Value);
            }
        }
        
        var config = indicator.Export();
        config.Description = description ?? config.Description;
        
        _customIndicators[id] = config;
        SaveCustomIndicator(config);
        
        // Register with indicator service
        RegisterWithService(id, config);
        
        _logger.Info($"Created formula indicator: {name} by {author}");
        return config;
    }
    
    /// <summary>
    /// Create a new composite indicator
    /// </summary>
    /// <param name="name">Indicator name</param>
    /// <param name="author">Author name</param>
    /// <param name="components">Component definitions</param>
    /// <param name="combineMethod">Method for combining results</param>
    /// <param name="description">Description</param>
    /// <returns>Created indicator configuration</returns>
    public CustomIndicatorConfig CreateCompositeIndicator(string name, string author, 
        List<ComponentDefinition> components, string combineMethod = "weighted_average", string? description = null)
    {
        var id = GenerateUniqueId(name);
        var indicator = new CompositeIndicator(id, name, author, _logger);
        indicator.SetParameter("combineMethod", combineMethod);
        
        // Add components
        foreach (var componentDef in components)
        {
            var componentIndicator = CreateComponentIndicator(componentDef);
            indicator.AddComponent(componentIndicator, componentDef.Weight, componentDef.Alias);
        }
        
        var config = indicator.Export();
        config.Description = description ?? $"Composite of {components.Count} indicators";
        config.Dependencies = components.Select(c => c.Type).ToList();
        
        _customIndicators[id] = config;
        SaveCustomIndicator(config);
        
        // Register with indicator service
        RegisterWithService(id, config);
        
        _logger.Info($"Created composite indicator: {name} by {author}");
        return config;
    }
    
    /// <summary>
    /// Load a custom indicator from configuration
    /// </summary>
    /// <param name="config">Indicator configuration</param>
    /// <returns>Loaded indicator instance</returns>
    public IIndicator LoadIndicator(CustomIndicatorConfig config)
    {
        if (config.Dependencies.Any())
        {
            // Composite indicator
            var composite = new CompositeIndicator(config.Id, config.Name, config.Author, _logger);
            
            // Apply parameters
            foreach (var param in config.Parameters)
            {
                composite.SetParameter(param.Key, param.Value);
            }
            
            // Add tags
            foreach (var tag in config.Tags)
            {
                composite.AddTag(tag);
            }
            
            return composite;
        }
        else
        {
            // Formula indicator
            var formula = new FormulaIndicator(config.Id, config.Name, config.Formula, config.Author, _logger);
            
            // Apply parameters
            foreach (var param in config.Parameters)
            {
                formula.SetParameter(param.Key, param.Value);
            }
            
            // Add tags
            foreach (var tag in config.Tags)
            {
                formula.AddTag(tag);
            }
            
            return formula;
        }
    }
    
    /// <summary>
    /// Update an existing custom indicator
    /// </summary>
    /// <param name="id">Indicator ID</param>
    /// <param name="updates">Updates to apply</param>
    /// <returns>Updated configuration</returns>
    public CustomIndicatorConfig? UpdateIndicator(string id, CustomIndicatorUpdate updates)
    {
        if (!_customIndicators.TryGetValue(id, out var config))
        {
            return null;
        }
        
        var updated = false;
        
        if (!string.IsNullOrWhiteSpace(updates.Name))
        {
            config.Name = updates.Name;
            updated = true;
        }
        
        if (!string.IsNullOrWhiteSpace(updates.Description))
        {
            config.Description = updates.Description;
            updated = true;
        }
        
        if (!string.IsNullOrWhiteSpace(updates.Formula))
        {
            config.Formula = updates.Formula;
            config.Version++;
            updated = true;
        }
        
        if (updates.Parameters != null)
        {
            foreach (var param in updates.Parameters)
            {
                config.Parameters[param.Key] = param.Value;
            }
            updated = true;
        }
        
        if (updates.Tags != null)
        {
            config.Tags = new List<string>(updates.Tags);
            updated = true;
        }
        
        if (updated)
        {
            config.ModifiedDate = DateTime.UtcNow;
            SaveCustomIndicator(config);
            
            // Re-register with indicator service
            RegisterWithService(id, config);
            
            _logger.Info($"Updated custom indicator: {config.Name}");
        }
        
        return config;
    }
    
    /// <summary>
    /// Delete a custom indicator
    /// </summary>
    /// <param name="id">Indicator ID</param>
    /// <returns>True if deleted successfully</returns>
    public bool DeleteIndicator(string id)
    {
        if (!_customIndicators.ContainsKey(id))
        {
            return false;
        }
        
        var config = _customIndicators[id];
        _customIndicators.Remove(id);
        
        // Remove from storage
        var filePath = Path.Combine(_storageDirectory, $"{id}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }
        
        _logger.Info($"Deleted custom indicator: {config.Name}");
        return true;
    }
    
    /// <summary>
    /// Get all custom indicators
    /// </summary>
    /// <returns>List of custom indicator configurations</returns>
    public IReadOnlyList<CustomIndicatorConfig> GetAllIndicators()
    {
        return _customIndicators.Values.ToList();
    }
    
    /// <summary>
    /// Search custom indicators by criteria
    /// </summary>
    /// <param name="criteria">Search criteria</param>
    /// <returns>Matching indicators</returns>
    public IReadOnlyList<CustomIndicatorConfig> SearchIndicators(IndicatorSearchCriteria criteria)
    {
        var query = _customIndicators.Values.AsEnumerable();
        
        if (!string.IsNullOrWhiteSpace(criteria.Name))
        {
            query = query.Where(i => i.Name.Contains(criteria.Name, StringComparison.OrdinalIgnoreCase));
        }
        
        if (!string.IsNullOrWhiteSpace(criteria.Author))
        {
            query = query.Where(i => i.Author.Contains(criteria.Author, StringComparison.OrdinalIgnoreCase));
        }
        
        if (criteria.Tags?.Any() == true)
        {
            query = query.Where(i => criteria.Tags.Any(tag => i.Tags.Contains(tag)));
        }
        
        if (criteria.CreatedAfter.HasValue)
        {
            query = query.Where(i => i.CreatedDate >= criteria.CreatedAfter.Value);
        }
        
        return query.ToList();
    }
    
    /// <summary>
    /// Export custom indicator to JSON
    /// </summary>
    /// <param name="id">Indicator ID</param>
    /// <returns>JSON string or null if not found</returns>
    public string? ExportToJson(string id)
    {
        if (_customIndicators.TryGetValue(id, out var config))
        {
            return JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        }
        return null;
    }
    
    /// <summary>
    /// Import custom indicator from JSON
    /// </summary>
    /// <param name="json">JSON string</param>
    /// <returns>Imported configuration or null if failed</returns>
    public CustomIndicatorConfig? ImportFromJson(string json)
    {
        try
        {
            var config = JsonSerializer.Deserialize<CustomIndicatorConfig>(json);
            if (config != null)
            {
                // Generate new ID to avoid conflicts
                var newId = GenerateUniqueId(config.Name);
                config.Id = newId;
                config.CreatedDate = DateTime.UtcNow;
                config.ModifiedDate = DateTime.UtcNow;
                
                _customIndicators[newId] = config;
                SaveCustomIndicator(config);
                RegisterWithService(newId, config);
                
                _logger.Info($"Imported custom indicator: {config.Name}");
                return config;
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to import custom indicator: {ex.Message}");
        }
        
        return null;
    }
    
    private void EnsureStorageDirectory()
    {
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }
    }
    
    private void LoadCustomIndicators()
    {
        if (!Directory.Exists(_storageDirectory))
        {
            return;
        }
        
        var jsonFiles = Directory.GetFiles(_storageDirectory, "*.json");
        foreach (var file in jsonFiles)
        {
            try
            {
                var json = File.ReadAllText(file);
                var config = JsonSerializer.Deserialize<CustomIndicatorConfig>(json);
                if (config != null)
                {
                    _customIndicators[config.Id] = config;
                    RegisterWithService(config.Id, config);
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"Failed to load custom indicator from {file}: {ex.Message}");
            }
        }
        
        _logger.Info($"Loaded {_customIndicators.Count} custom indicators");
    }
    
    private void SaveCustomIndicator(CustomIndicatorConfig config)
    {
        try
        {
            var filePath = Path.Combine(_storageDirectory, $"{config.Id}.json");
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save custom indicator {config.Name}: {ex.Message}");
        }
    }
    
    private void RegisterWithService(string id, CustomIndicatorConfig config)
    {
        _indicatorService.RegisterIndicatorType($"CUSTOM_{id}", 
            new IndicatorFactory((instanceId, parameters, logger) => 
            {
                var indicator = LoadIndicator(config);
                // Apply any runtime parameters
                if (parameters != null)
                {
                    var baseIndicator = indicator as CustomIndicatorBase;
                    if (baseIndicator != null)
                    {
                        foreach (var param in parameters)
                        {
                            baseIndicator.SetParameter(param.Key, param.Value);
                        }
                    }
                }
                return indicator;
            }));
    }
    
    private IIndicator CreateComponentIndicator(ComponentDefinition def)
    {
        // Create basic indicators based on type
        return def.Type.ToUpper() switch
        {
            "SMA" => new SimpleMovingAverage($"{def.Type}_{def.Alias}", _logger),
            "EMA" => new ExponentialMovingAverage($"{def.Type}_{def.Alias}", _logger),
            "RSI" => new RelativeStrengthIndex($"{def.Type}_{def.Alias}", _logger),
            "MACD" => new MovingAverageConvergenceDivergence($"{def.Type}_{def.Alias}", _logger),
            "BB" => new BollingerBands($"{def.Type}_{def.Alias}", _logger),
            _ => throw new ArgumentException($"Unknown indicator type: {def.Type}")
        };
    }
    
    private string GenerateUniqueId(string name)
    {
        var baseId = name.Replace(" ", "_").ToLower();
        var id = baseId;
        var counter = 1;
        
        while (_customIndicators.ContainsKey(id))
        {
            id = $"{baseId}_{counter}";
            counter++;
        }
        
        return id;
    }

    /// <summary>
    /// Add an indicator to the service
    /// </summary>
    public bool AddIndicator(CustomIndicatorConfig indicator)
    {
        try
        {
            if (_customIndicators.ContainsKey(indicator.Id))
                return false;

            _customIndicators[indicator.Id] = indicator;
            SaveIndicator(indicator);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add indicator: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Validate an indicator configuration
    /// </summary>
    public ValidationResult ValidateIndicator(CustomIndicatorConfig indicator)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(indicator.Name))
            result.Errors.Add("Indicator name is required");

        if (string.IsNullOrWhiteSpace(indicator.Author))
            result.Errors.Add("Indicator author is required");

        // Commented out until IndicatorType enum is defined
        // if (indicator.Type == IndicatorType.Formula && string.IsNullOrWhiteSpace(indicator.Formula))
        //     result.Errors.Add("Formula is required for formula-based indicators");

        result.IsValid = !result.Errors.Any();
        return result;
    }

    /// <summary>
    /// Save a custom indicator to persistent storage
    /// </summary>
    private void SaveIndicator(CustomIndicatorConfig indicator)
    {
        try
        {
            var filePath = Path.Combine(_storageDirectory, $"{indicator.Id}.json");
            var json = System.Text.Json.JsonSerializer.Serialize(indicator, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(filePath, json);
            _logger.Info($"Saved indicator {indicator.Name} to {filePath}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save indicator {indicator.Name}: {ex.Message}", ex);
            throw;
        }
    }
}

/// <summary>
/// Definition for a component in a composite indicator
/// </summary>
public class ComponentDefinition
{
    public string Type { get; set; } = string.Empty; // SMA, EMA, RSI, etc.
    public string Alias { get; set; } = string.Empty;
    public double Weight { get; set; } = 1.0;
    public Dictionary<string, object> Parameters { get; set; } = new();
}

/// <summary>
/// Validation result for indicator operations
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new List<string>();
}

/// <summary>
/// Updates to apply to a custom indicator
/// </summary>
public class CustomIndicatorUpdate
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Formula { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Search criteria for finding custom indicators
/// </summary>
public class IndicatorSearchCriteria
{
    public string? Name { get; set; }
    public string? Author { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
}
