using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ApexV2.Indicators.Custom;
using ApexV2.Core.Logging;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Library;

/// <summary>
/// Manages import/export operations for the indicator library
/// </summary>
public class IndicatorLibraryImportExport
{
    private readonly IndicatorLibraryManager _libraryManager;
    private readonly CustomIndicatorService _customIndicatorService;
    private readonly IChartLogger _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public IndicatorLibraryImportExport(IndicatorLibraryManager libraryManager, CustomIndicatorService customIndicatorService, IChartLogger? logger = null)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _customIndicatorService = customIndicatorService ?? throw new ArgumentNullException(nameof(customIndicatorService));
        _logger = logger ?? new NullLogger();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            AllowTrailingCommas = true
        };
    }

    /// <summary>
    /// Export indicators to a file
    /// </summary>
    public async Task<bool> ExportIndicatorsAsync(IEnumerable<Guid> indicatorIds, string filePath, ExportFormat format = ExportFormat.Json)
    {
        try
        {
            var indicators = new List<CustomIndicatorConfig>();
            var libraryItems = new List<IndicatorLibraryItem>();

            foreach (var id in indicatorIds)
            {
                var config = _customIndicatorService.GetAllIndicators().FirstOrDefault(c => c.Id == id.ToString());
                var libraryItem = _libraryManager.GetAllIndicators().FirstOrDefault(l => l.Id == id);

                if (config != null && libraryItem != null)
                {
                    indicators.Add(config);
                    libraryItems.Add(libraryItem);
                }
            }

            if (!indicators.Any())
            {
                _logger.Warn("No indicators found to export");
                return false;
            }

            var exportData = new IndicatorExportData
            {
                ExportDate = DateTime.UtcNow,
                Version = "1.0",
                Indicators = indicators,
                LibraryItems = libraryItems,
                Metadata = new ExportMetadata
                {
                    ExportedBy = Environment.UserName,
                    ExportSource = "APEX V2",
                    TotalCount = indicators.Count
                }
            };

            switch (format)
            {
                case ExportFormat.Json:
                    await ExportToJsonAsync(exportData, filePath);
                    break;
                case ExportFormat.Xml:
                    await ExportToXmlAsync(exportData, filePath);
                    break;
                case ExportFormat.Csv:
                    await ExportToCsvAsync(exportData, filePath);
                    break;
                default:
                    throw new ArgumentException($"Unsupported export format: {format}");
            }

            _logger.Info($"Exported {indicators.Count} indicators to {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to export indicators: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Import indicators from a file
    /// </summary>
    public async Task<ImportResult> ImportIndicatorsAsync(string filePath, ImportOptions? options = null)
    {
        var result = new ImportResult();
        options ??= new ImportOptions();

        try
        {
            if (!File.Exists(filePath))
            {
                result.Errors.Add($"File not found: {filePath}");
                return result;
            }

            var format = DetermineFileFormat(filePath);
            IndicatorExportData? exportData = null;

            switch (format)
            {
                case ExportFormat.Json:
                    exportData = await ImportFromJsonAsync(filePath);
                    break;
                case ExportFormat.Xml:
                    exportData = await ImportFromXmlAsync(filePath);
                    break;
                case ExportFormat.Csv:
                    exportData = await ImportFromCsvAsync(filePath);
                    break;
                default:
                    result.Errors.Add($"Unsupported file format: {format}");
                    return result;
            }

            if (exportData?.Indicators == null || !exportData.Indicators.Any())
            {
                result.Errors.Add("No indicators found in import file");
                return result;
            }

            // Process each indicator
            foreach (var indicator in exportData.Indicators)
            {
                try
                {
                    var libraryItem = exportData.LibraryItems?.FirstOrDefault(l => l.Id.ToString() == indicator.Id);
                    var importSuccess = await ImportSingleIndicatorAsync(indicator, libraryItem, options, result);
                    
                    if (importSuccess)
                        result.SuccessCount++;
                    else
                        result.SkippedCount++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"Failed to import indicator '{indicator.Name}': {ex.Message}");
                    result.FailedCount++;
                }
            }

            _logger.Info($"Import completed: {result.SuccessCount} success, {result.SkippedCount} skipped, {result.FailedCount} failed");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to import indicators: {ex.Message}", ex);
            result.Errors.Add($"Import failed: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Export library configuration and structure
    /// </summary>
    public async Task<bool> ExportLibraryConfigurationAsync(string filePath)
    {
        try
        {
            var config = new LibraryConfiguration
            {
                Categories = _libraryManager.GetAllCategories().ToList(),
                Tags = _libraryManager.GetAllTags().ToList(),
                Statistics = ConvertStatistics(_libraryManager.GetLibraryStatistics()),
                Settings = new Dictionary<string, object>
                {
                    ["exportDate"] = DateTime.UtcNow,
                    ["version"] = "1.0"
                }
            };

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json);

            _logger.Info($"Exported library configuration to {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to export library configuration: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Create a backup of the entire library
    /// </summary>
    public async Task<bool> CreateLibraryBackupAsync(string backupPath)
    {
        try
        {
            var allIndicators = _libraryManager.GetAllIndicators();
            var indicatorIds = allIndicators.Select(i => i.Id).ToList();

            // Create backup directory structure
            var backupDir = Path.Combine(backupPath, $"ApexIndicatorLibrary_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(backupDir);

            // Export all indicators
            var indicatorsFile = Path.Combine(backupDir, "indicators.json");
            await ExportIndicatorsAsync(indicatorIds, indicatorsFile);

            // Export library configuration
            var configFile = Path.Combine(backupDir, "library_config.json");
            await ExportLibraryConfigurationAsync(configFile);

            // Create backup manifest
            var manifest = new BackupManifest
            {
                BackupDate = DateTime.UtcNow,
                Version = "1.0",
                TotalIndicators = indicatorIds.Count,
                BackupType = "Full",
                Files = new List<string> { "indicators.json", "library_config.json" }
            };

            var manifestFile = Path.Combine(backupDir, "manifest.json");
            var manifestJson = JsonSerializer.Serialize(manifest, _jsonOptions);
            await File.WriteAllTextAsync(manifestFile, manifestJson);

            _logger.Info($"Created library backup at {backupDir}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create library backup: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Restore library from backup
    /// </summary>
    public async Task<ImportResult> RestoreLibraryFromBackupAsync(string backupPath, bool clearExisting = false)
    {
        var result = new ImportResult();

        try
        {
            var manifestFile = Path.Combine(backupPath, "manifest.json");
            if (!File.Exists(manifestFile))
            {
                result.Errors.Add("Backup manifest not found");
                return result;
            }

            var manifestJson = await File.ReadAllTextAsync(manifestFile);
            var manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson, _jsonOptions);

            if (manifest == null)
            {
                result.Errors.Add("Invalid backup manifest");
                return result;
            }

            // Clear existing library if requested
            if (clearExisting)
            {
                // This would need to be implemented in IndicatorLibraryManager
                _logger.Warn("Clear existing library functionality not yet implemented");
            }

            // Restore indicators
            var indicatorsFile = Path.Combine(backupPath, "indicators.json");
            if (File.Exists(indicatorsFile))
            {
                var importOptions = new ImportOptions
                {
                    OverwriteExisting = true,
                    ValidateIndicators = true
                };

                var importResult = await ImportIndicatorsAsync(indicatorsFile, importOptions);
                result.SuccessCount += importResult.SuccessCount;
                result.FailedCount += importResult.FailedCount;
                result.SkippedCount += importResult.SkippedCount;
                result.Errors.AddRange(importResult.Errors);
            }

            _logger.Info($"Restored library from backup: {result.SuccessCount} indicators restored");
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to restore library from backup: {ex.Message}", ex);
            result.Errors.Add($"Restore failed: {ex.Message}");
            return result;
        }
    }

    private async Task<bool> ImportSingleIndicatorAsync(CustomIndicatorConfig indicator, IndicatorLibraryItem? libraryItem, ImportOptions options, ImportResult result)
    {
        // Check if indicator already exists
        var existing = _customIndicatorService.GetAllIndicators().FirstOrDefault(c => c.Id == indicator.Id || c.Name == indicator.Name);
        
        if (existing != null)
        {
            if (!options.OverwriteExisting)
            {
                result.Warnings.Add($"Indicator '{indicator.Name}' already exists and was skipped");
                return false;
            }

            if (options.CreateBackupBeforeOverwrite)
            {
                // Create backup before overwriting
                var backupName = $"{existing.Name}_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}";
                await _libraryManager.AddIndicatorToLibraryAsync(existing, "Backups", $"Backup of {existing.Name} before import");
            }
        }

        // Validate indicator if requested
        if (options.ValidateIndicators)
        {
            var validation = _customIndicatorService.ValidateIndicator(indicator);
            if (!validation.IsValid)
            {
                result.Errors.Add($"Validation failed for '{indicator.Name}': {string.Join(", ", validation.Errors)}");
                return false;
            }
        }

        // Generate new ID if requested
        if (options.GenerateNewIds)
        {
            indicator.Id = Guid.NewGuid().ToString();
        }

        // Update timestamps
        if (options.UpdateTimestamps)
        {
            indicator.ModifiedDate = DateTime.UtcNow;
            if (existing == null)
                indicator.CreatedDate = DateTime.UtcNow;
        }

        // Add or update indicator
        if (existing != null)
        {
            var updates = new CustomIndicatorUpdate
            {
                Name = indicator.Name,
                Description = indicator.Description
            };
            _customIndicatorService.UpdateIndicator(indicator.Id, updates);
        }
        else
        {
            _customIndicatorService.AddIndicator(indicator);
        }

        // Add to library if library item exists
        if (libraryItem != null)
        {
            await _libraryManager.AddIndicatorToLibraryAsync(indicator, libraryItem.Category, libraryItem.Description);
            
            // Apply tags
            foreach (var tag in libraryItem.Tags)
            {
                await _libraryManager.AddTagToIndicator(Guid.Parse(indicator.Id), tag);
            }
        }

        return true;
    }

    private ExportFormat DetermineFileFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".json" => ExportFormat.Json,
            ".xml" => ExportFormat.Xml,
            ".csv" => ExportFormat.Csv,
            _ => ExportFormat.Json
        };
    }

    private async Task ExportToJsonAsync(IndicatorExportData data, string filePath)
    {
        var json = JsonSerializer.Serialize(data, _jsonOptions);
        await File.WriteAllTextAsync(filePath, json);
    }

    private async Task ExportToXmlAsync(IndicatorExportData data, string filePath)
    {
        // XML export implementation would go here
        throw new NotImplementedException("XML export not yet implemented");
    }

    private async Task ExportToCsvAsync(IndicatorExportData data, string filePath)
    {
        // CSV export implementation would go here
        throw new NotImplementedException("CSV export not yet implemented");
    }

    private async Task<IndicatorExportData?> ImportFromJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return JsonSerializer.Deserialize<IndicatorExportData>(json, _jsonOptions);
    }

    private async Task<IndicatorExportData?> ImportFromXmlAsync(string filePath)
    {
        // XML import implementation would go here
        throw new NotImplementedException("XML import not yet implemented");
    }

    private async Task<IndicatorExportData?> ImportFromCsvAsync(string filePath)
    {
        // CSV import implementation would go here
        throw new NotImplementedException("CSV import not yet implemented");
    }

    /// <summary>
    /// Convert LibraryStatistics to IndicatorLibraryStatistics
    /// </summary>
    private IndicatorLibraryStatistics ConvertStatistics(LibraryStatistics stats)
    {
        return new IndicatorLibraryStatistics
        {
            TotalIndicators = stats.TotalIndicators,
            TotalCategories = stats.TotalCategories,
            TotalTags = stats.TagsCount,
            CategoryCounts = new Dictionary<string, int>(),
            TagCounts = new Dictionary<string, int>(),
            AuthorCounts = new Dictionary<string, int>(),
            LastUpdated = DateTime.UtcNow,
            TotalUsageCount = 0,
            AverageRating = stats.AverageRating,
            AdditionalMetrics = new Dictionary<string, object>()
        };
    }
}

/// <summary>
/// Data structure for indicator export/import
/// </summary>
public class IndicatorExportData
{
    public DateTime ExportDate { get; set; }
    public string Version { get; set; } = string.Empty;
    public List<CustomIndicatorConfig> Indicators { get; set; } = new();
    public List<IndicatorLibraryItem>? LibraryItems { get; set; }
    public ExportMetadata? Metadata { get; set; }
}

/// <summary>
/// Export metadata
/// </summary>
public class ExportMetadata
{
    public string ExportedBy { get; set; } = string.Empty;
    public string ExportSource { get; set; } = string.Empty;
    public int TotalCount { get; set; }
    public Dictionary<string, object> AdditionalInfo { get; set; } = new();
}

/// <summary>
/// Library configuration for export/import
/// </summary>
public class LibraryConfiguration
{
    public List<string> Categories { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public IndicatorLibraryStatistics? Statistics { get; set; }
    public Dictionary<string, object> Settings { get; set; } = new();
}

/// <summary>
/// Backup manifest
/// </summary>
public class BackupManifest
{
    public DateTime BackupDate { get; set; }
    public string Version { get; set; } = string.Empty;
    public int TotalIndicators { get; set; }
    public string BackupType { get; set; } = string.Empty;
    public List<string> Files { get; set; } = new();
}

/// <summary>
/// Import options
/// </summary>
public class ImportOptions
{
    public bool OverwriteExisting { get; set; } = false;
    public bool ValidateIndicators { get; set; } = true;
    public bool GenerateNewIds { get; set; } = false;
    public bool UpdateTimestamps { get; set; } = true;
    public bool CreateBackupBeforeOverwrite { get; set; } = true;
}

/// <summary>
/// Import result
/// </summary>
public class ImportResult
{
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    
    public bool HasErrors => Errors.Any();
    public bool HasWarnings => Warnings.Any();
    public int TotalProcessed => SuccessCount + FailedCount + SkippedCount;
}

/// <summary>
/// Export format enumeration
/// </summary>
public enum ExportFormat
{
    Json,
    Xml,
    Csv
}
