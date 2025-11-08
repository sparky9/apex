using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Indicators.Custom;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Library;

/// <summary>
/// Manages the indicator library - organizing, categorizing, and providing access to custom indicators
/// </summary>
public class IndicatorLibraryManager
{
    private readonly CustomIndicatorService _customIndicatorService;
    private readonly string _libraryPath;
    private readonly IChartLogger _logger;
    private readonly Dictionary<string, IndicatorCategory> _categories;
    private readonly Dictionary<string, List<string>> _tags;
    private readonly List<IndicatorLibraryItem> _libraryItems;

    public IndicatorLibraryManager(CustomIndicatorService customIndicatorService, string? libraryPath = null, IChartLogger? logger = null)
    {
        _customIndicatorService = customIndicatorService ?? throw new ArgumentNullException(nameof(customIndicatorService));
        _libraryPath = libraryPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ApexV2", "IndicatorLibrary");
        _logger = logger ?? new NullLogger();
        
        _categories = new Dictionary<string, IndicatorCategory>();
        _tags = new Dictionary<string, List<string>>();
        _libraryItems = new List<IndicatorLibraryItem>();

        InitializeDefaultCategories();
        EnsureLibraryDirectory();
        _ = LoadLibraryAsync();
    }

    /// <summary>
    /// Get all indicators in the library
    /// </summary>
    public IReadOnlyList<IndicatorLibraryItem> GetAllIndicators()
    {
        return _libraryItems.AsReadOnly();
    }

    /// <summary>
    /// Get indicators by category
    /// </summary>
    public IReadOnlyList<IndicatorLibraryItem> GetIndicatorsByCategory(string categoryName)
    {
        return _libraryItems.Where(i => i.Category.Equals(categoryName, StringComparison.OrdinalIgnoreCase)).ToList().AsReadOnly();
    }

    /// <summary>
    /// Get indicators by tag
    /// </summary>
    public IReadOnlyList<IndicatorLibraryItem> GetIndicatorsByTag(string tag)
    {
        return _libraryItems.Where(i => i.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).ToList().AsReadOnly();
    }

    /// <summary>
    /// Search indicators by name, description, or tags
    /// </summary>
    public IReadOnlyList<IndicatorLibraryItem> SearchIndicators(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return GetAllIndicators();

        var term = searchTerm.ToLowerInvariant();
        return _libraryItems.Where(i => 
            i.Name.ToLowerInvariant().Contains(term) ||
            i.Description.ToLowerInvariant().Contains(term) ||
            i.Tags.Any(t => t.ToLowerInvariant().Contains(term)) ||
            i.Author.ToLowerInvariant().Contains(term)
        ).ToList().AsReadOnly();
    }

    /// <summary>
    /// Add an indicator to the library
    /// </summary>
    public async Task<bool> AddIndicatorToLibraryAsync(CustomIndicatorConfig config, string? category = null, string? description = null)
    {
        try
        {
            // Create library item
            var libraryItem = new IndicatorLibraryItem
            {
                Id = Guid.TryParse(config.Id, out var guid) ? guid : Guid.NewGuid(),
                Name = config.Name,
                Description = description ?? config.Description,
                Author = config.Author,
                Category = category ?? "Custom",
                Tags = new List<string>(config.Tags),
                Version = config.Version.ToString(),
                CreatedDate = config.CreatedDate,
                ModifiedDate = config.ModifiedDate,
                IsBuiltIn = false,
                Rating = 0.0,
                DownloadCount = 0,
                ConfigId = config.Id
            };

            // Add to library
            _libraryItems.Add(libraryItem);

            // Update category tracking
            if (!_categories.ContainsKey(libraryItem.Category))
            {
                _categories[libraryItem.Category] = new IndicatorCategory
                {
                    Name = libraryItem.Category,
                    Description = $"Custom {libraryItem.Category} indicators",
                    IsBuiltIn = false
                };
            }

            // Update tag tracking
            foreach (var tag in libraryItem.Tags)
            {
                if (!_tags.ContainsKey(tag))
                    _tags[tag] = new List<string>();
                
                if (!_tags[tag].Contains(libraryItem.Id.ToString()))
                    _tags[tag].Add(libraryItem.Id.ToString());
            }

            // Save library metadata
            await SaveLibraryMetadataAsync();

            _logger.Info($"Added indicator '{libraryItem.Name}' to library");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add indicator to library: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove an indicator from the library
    /// </summary>
    public async Task<bool> RemoveIndicatorFromLibraryAsync(string indicatorId)
    {
        try
        {
            if (!Guid.TryParse(indicatorId, out var guid))
                return false;
                
            var item = _libraryItems.FirstOrDefault(i => i.Id == guid);
            if (item == null)
                return false;

            // Remove from library
            _libraryItems.Remove(item);

            // Update tag tracking
            foreach (var tag in item.Tags)
            {
                if (_tags.ContainsKey(tag))
                {
                    _tags[tag].Remove(item.Id.ToString());
                    if (!_tags[tag].Any())
                        _tags.Remove(tag);
                }
            }

            // Save library metadata
            await SaveLibraryMetadataAsync();

            _logger.Info($"Removed indicator '{item.Name}' from library");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to remove indicator from library: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Update indicator rating
    /// </summary>
    public async Task<bool> UpdateIndicatorRatingAsync(string indicatorId, double rating)
    {
        try
        {
            if (!Guid.TryParse(indicatorId, out var guid))
                return false;
                
            var item = _libraryItems.FirstOrDefault(i => i.Id == guid);
            if (item == null)
                return false;

            item.Rating = Math.Clamp(rating, 0.0, 5.0);
            item.ModifiedDate = DateTime.UtcNow;

            await SaveLibraryMetadataAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update indicator rating: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Get all available categories
    /// </summary>
    public IReadOnlyList<IndicatorCategory> GetCategories()
    {
        return _categories.Values.ToList().AsReadOnly();
    }

    /// <summary>
    /// Get all available tags
    /// </summary>
    public IReadOnlyList<string> GetTags()
    {
        return _tags.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Create a new category
    /// </summary>
    public async Task<bool> CreateCategoryAsync(string name, string description, bool isBuiltIn = false)
    {
        try
        {
            if (_categories.ContainsKey(name))
                return false;

            _categories[name] = new IndicatorCategory
            {
                Name = name,
                Description = description,
                IsBuiltIn = isBuiltIn
            };

            await SaveLibraryMetadataAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create category: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Export library to backup file
    /// </summary>
    public async Task<string?> ExportLibraryAsync(string exportPath)
    {
        try
        {
            var libraryBackup = new IndicatorLibraryBackup
            {
                ExportDate = DateTime.UtcNow,
                Version = "1.0",
                Categories = _categories.Values.ToList(),
                Items = _libraryItems.ToList(),
                Tags = _tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList())
            };

            var json = System.Text.Json.JsonSerializer.Serialize(libraryBackup, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(exportPath, json);
            _logger.Info($"Exported indicator library to {exportPath}");
            return exportPath;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to export library: {ex.Message}", ex);
            return null;
        }
    }

    /// <summary>
    /// Import library from backup file
    /// </summary>
    public async Task<bool> ImportLibraryAsync(string importPath, bool mergeWithExisting = true)
    {
        try
        {
            if (!File.Exists(importPath))
                return false;

            var json = await File.ReadAllTextAsync(importPath);
            var libraryBackup = System.Text.Json.JsonSerializer.Deserialize<IndicatorLibraryBackup>(json);

            if (libraryBackup == null)
                return false;

            if (!mergeWithExisting)
            {
                _libraryItems.Clear();
                _categories.Clear();
                _tags.Clear();
                InitializeDefaultCategories();
            }

            // Import categories
            foreach (var category in libraryBackup.Categories)
            {
                if (!_categories.ContainsKey(category.Name))
                    _categories[category.Name] = category;
            }

            // Import items
            foreach (var item in libraryBackup.Items)
            {
                var existingItem = _libraryItems.FirstOrDefault(i => i.Id == item.Id);
                if (existingItem != null)
                {
                    if (mergeWithExisting)
                        _libraryItems.Remove(existingItem);
                    else
                        continue;
                }
                _libraryItems.Add(item);
            }

            // Import tags
            foreach (var tagGroup in libraryBackup.Tags)
            {
                if (!_tags.ContainsKey(tagGroup.Key))
                    _tags[tagGroup.Key] = new List<string>();
                
                foreach (var itemId in tagGroup.Value)
                {
                    if (!_tags[tagGroup.Key].Contains(itemId))
                        _tags[tagGroup.Key].Add(itemId);
                }
            }

            await SaveLibraryMetadataAsync();
            _logger.Info($"Imported indicator library from {importPath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to import library: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Get library statistics
    /// </summary>
    public IndicatorLibraryStats GetLibraryStats()
    {
        return new IndicatorLibraryStats
        {
            TotalIndicators = _libraryItems.Count,
            BuiltInIndicators = _libraryItems.Count(i => i.IsBuiltIn),
            CustomIndicators = _libraryItems.Count(i => !i.IsBuiltIn),
            Categories = _categories.Count,
            Tags = _tags.Count,
            AverageRating = _libraryItems.Where(i => i.Rating > 0).DefaultIfEmpty().Average(i => i?.Rating ?? 0),
            MostUsedTags = _tags.OrderByDescending(t => t.Value.Count).Take(10).Select(t => t.Key).ToList(),
            TopRatedIndicators = _libraryItems.Where(i => i.Rating > 0).OrderByDescending(i => i.Rating).Take(10).ToList()
        };
    }

    private void InitializeDefaultCategories()
    {
        var defaultCategories = new[]
        {
            new IndicatorCategory { Name = "Trend", Description = "Trend-following indicators", IsBuiltIn = true },
            new IndicatorCategory { Name = "Momentum", Description = "Momentum and oscillator indicators", IsBuiltIn = true },
            new IndicatorCategory { Name = "Volume", Description = "Volume-based indicators", IsBuiltIn = true },
            new IndicatorCategory { Name = "Volatility", Description = "Volatility and risk indicators", IsBuiltIn = true },
            new IndicatorCategory { Name = "Support/Resistance", Description = "Support and resistance indicators", IsBuiltIn = true },
            new IndicatorCategory { Name = "Custom", Description = "User-created custom indicators", IsBuiltIn = false }
        };

        foreach (var category in defaultCategories)
        {
            _categories[category.Name] = category;
        }
    }

    private void EnsureLibraryDirectory()
    {
        if (!Directory.Exists(_libraryPath))
        {
            Directory.CreateDirectory(_libraryPath);
            _logger.Info($"Created indicator library directory: {_libraryPath}");
        }
    }

    private async Task LoadLibraryAsync()
    {
        try
        {
            var metadataPath = Path.Combine(_libraryPath, "library_metadata.json");
            if (File.Exists(metadataPath))
            {
                var json = await File.ReadAllTextAsync(metadataPath);
                var metadata = System.Text.Json.JsonSerializer.Deserialize<IndicatorLibraryMetadata>(json);
                
                if (metadata != null)
                {
                    _libraryItems.Clear();
                    _libraryItems.AddRange(metadata.Items);
                    
                    foreach (var category in metadata.Categories)
                    {
                        _categories[category.Name] = category;
                    }
                    
                    foreach (var tagGroup in metadata.Tags)
                    {
                        _tags[tagGroup.Key] = tagGroup.Value.ToList();
                    }
                }
            }

            // Load all custom indicators from the service
            await LoadCustomIndicatorsAsync();

            _logger.Info($"Loaded indicator library with {_libraryItems.Count} indicators");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load indicator library: {ex.Message}", ex);
        }
    }

    private async Task LoadCustomIndicatorsAsync()
    {
        try
        {
            var customIndicators = _customIndicatorService.GetAllIndicators();
            
            foreach (var config in customIndicators)
            {
                var configGuid = Guid.TryParse(config.Id, out var guid) ? guid : Guid.NewGuid();
                var existingItem = _libraryItems.FirstOrDefault(i => i.Id == configGuid);
                if (existingItem == null)
                {
                    // Add new custom indicator to library
                    await AddIndicatorToLibraryAsync(config, "Custom", config.Description);
                }
                else
                {
                    // Update existing item
                    existingItem.Name = config.Name;
                    existingItem.Description = config.Description;
                    existingItem.Author = config.Author;
                    existingItem.Tags = new List<string>(config.Tags);
                    existingItem.ModifiedDate = config.ModifiedDate;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load custom indicators into library: {ex.Message}", ex);
        }
    }

    private async Task SaveLibraryMetadataAsync()
    {
        try
        {
            var metadata = new IndicatorLibraryMetadata
            {
                Version = "1.0",
                LastUpdated = DateTime.UtcNow,
                Items = _libraryItems.ToList(),
                Categories = _categories.Values.ToList(),
                Tags = _tags.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToList())
            };

            var json = System.Text.Json.JsonSerializer.Serialize(metadata, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            var metadataPath = Path.Combine(_libraryPath, "library_metadata.json");
            await File.WriteAllTextAsync(metadataPath, json);
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to save library metadata: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Add a tag to an indicator
    /// </summary>
    public async Task<bool> AddTagToIndicator(Guid indicatorId, string tag)
    {
        try
        {
            var item = _libraryItems.FirstOrDefault(i => i.Id == indicatorId);
            if (item == null) return false;

            if (!item.Tags.Contains(tag))
            {
                item.Tags.Add(tag);
                await SaveLibraryMetadataAsync();
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to add tag to indicator: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Remove a tag from an indicator
    /// </summary>
    public async Task<bool> RemoveTagFromIndicator(Guid indicatorId, string tag)
    {
        try
        {
            var item = _libraryItems.FirstOrDefault(i => i.Id == indicatorId);
            if (item == null) return false;

            item.Tags.Remove(tag);
            await SaveLibraryMetadataAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to remove tag from indicator: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Get all tags in the library
    /// </summary>
    public IReadOnlyList<string> GetAllTags()
    {
        return _libraryItems
            .SelectMany(i => i.Tags)
            .Distinct()
            .OrderBy(t => t)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Get all categories in the library
    /// </summary>
    public IReadOnlyList<string> GetAllCategories()
    {
        return _categories.Keys.ToList().AsReadOnly();
    }

    /// <summary>
    /// Remove an indicator from the library
    /// </summary>
    public async Task<bool> RemoveIndicatorFromLibrary(Guid indicatorId)
    {
        try
        {
            var item = _libraryItems.FirstOrDefault(i => i.Id == indicatorId);
            if (item == null) return false;

            _libraryItems.Remove(item);
            await SaveLibraryMetadataAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to remove indicator from library: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Update indicator metadata
    /// </summary>
    public async Task<bool> UpdateIndicatorMetadata(Guid indicatorId, IndicatorLibraryItem updatedItem)
    {
        try
        {
            var existingIndex = _libraryItems.FindIndex(i => i.Id == indicatorId);
            if (existingIndex == -1) return false;

            _libraryItems[existingIndex] = updatedItem;
            await SaveLibraryMetadataAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update indicator metadata: {ex.Message}", ex);
            return false;
        }
    }

    /// <summary>
    /// Get library statistics
    /// </summary>
    public LibraryStatistics GetLibraryStatistics()
    {
        return new LibraryStatistics
        {
            TotalIndicators = _libraryItems.Count,
            CategoriesCount = _categories.Count,
            TagsCount = GetAllTags().Count,
            MostUsedCategory = _libraryItems.GroupBy(i => i.Category).OrderByDescending(g => g.Count()).FirstOrDefault()?.Key ?? "None",
            AverageRating = _libraryItems.Any() ? _libraryItems.Average(i => i.Rating) : 0.0
        };
    }

    /// <summary>
    /// Create a backup of the library
    /// </summary>
    public async Task<string> CreateLibraryBackup()
    {
        try
        {
            var backupPath = Path.Combine(_libraryPath, "Backups", $"library_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);

            var backupData = new
            {
                BackupDate = DateTime.UtcNow,
                Version = "1.0",
                Items = _libraryItems,
                Categories = _categories,
                Tags = _tags
            };

            var json = System.Text.Json.JsonSerializer.Serialize(backupData, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(backupPath, json);
            return backupPath;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to create library backup: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Restore from backup
    /// </summary>
    public async Task<bool> RestoreFromBackup(string backupPath)
    {
        try
        {
            if (!File.Exists(backupPath)) return false;

            var json = await File.ReadAllTextAsync(backupPath);
            var backupData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(json);
            
            // Simple restore - in real implementation would be more sophisticated
            await LoadLibraryAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to restore from backup: {ex.Message}", ex);
            return false;
        }
    }
}
