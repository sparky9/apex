using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Analysis.Scanner;

namespace ApexV2.Analysis.Scanner;

/// <summary>
/// Service for browsing and filtering scan results and configurations
/// </summary>
public class ScanBrowserService
{
    private readonly ScannerService _scannerService;
    private readonly ScanTemplateManager _templateManager;

    public ScanBrowserService(ScannerService scannerService, ScanTemplateManager templateManager)
    {
        _scannerService = scannerService ?? throw new ArgumentNullException(nameof(scannerService));
        _templateManager = templateManager ?? throw new ArgumentNullException(nameof(templateManager));
    }

    /// <summary>
    /// Browse scan configurations with filtering and sorting
    /// </summary>
    public async Task<ScanBrowseResult> BrowseScansAsync(ScanFilter? filter = null, ScanSortBy sortBy = ScanSortBy.Name, bool sortDescending = false, int pageSize = 20, int pageNumber = 1)
    {
        try
        {
            var allScans = _scannerService.GetSavedScans();
            var filteredScans = ApplyFilters(allScans, filter);
            var sortedScans = ApplySorting(filteredScans, sortBy, sortDescending);
            var paginatedResults = ApplyPagination(sortedScans, pageSize, pageNumber);

            return new ScanBrowseResult
            {
                Items = paginatedResults.ToList(),
                TotalItems = filteredScans.Count,
                PageSize = pageSize,
                PageNumber = pageNumber,
                TotalPages = (int)Math.Ceiling((double)filteredScans.Count / pageSize),
                Filter = filter,
                SortBy = sortBy,
                SortDescending = sortDescending
            };
        }
        catch (Exception ex)
        {
            return new ScanBrowseResult
            {
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Browse scan templates with filtering and sorting
    /// </summary>
    public async Task<TemplateBrowseResult> BrowseTemplatesAsync(TemplateFilter? filter = null, ScanSortBy sortBy = ScanSortBy.Name, bool sortDescending = false, int pageSize = 20, int pageNumber = 1)
    {
        try
        {
            var allTemplates = _templateManager.GetAllTemplates();
            var filteredTemplates = ApplyTemplateFilters(allTemplates, filter);
            var sortedTemplates = ApplyTemplateSorting(filteredTemplates, sortBy, sortDescending);
            var paginatedResults = ApplyTemplatePagination(sortedTemplates, pageSize, pageNumber);

            return new TemplateBrowseResult
            {
                Items = paginatedResults.ToList(),
                TotalItems = filteredTemplates.Count,
                PageSize = pageSize,
                PageNumber = pageNumber,
                TotalPages = (int)Math.Ceiling((double)filteredTemplates.Count / pageSize),
                Filter = filter,
                SortBy = sortBy,
                SortDescending = sortDescending
            };
        }
        catch (Exception ex)
        {
            return new TemplateBrowseResult
            {
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Search scan configurations by text
    /// </summary>
    public IReadOnlyList<ScanConfiguration> SearchScans(string searchText, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return new List<ScanConfiguration>();

        var term = searchText.ToLowerInvariant();
        return _scannerService.GetSavedScans()
            .Where(s =>
                s.Name.ToLowerInvariant().Contains(term) ||
                s.Description.ToLowerInvariant().Contains(term) ||
                s.Tags.Any(t => t.ToLowerInvariant().Contains(term)) ||
                s.Author.ToLowerInvariant().Contains(term))
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Search templates by text
    /// </summary>
    public IReadOnlyList<ScanTemplate> SearchTemplates(string searchText, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return new List<ScanTemplate>();

        var term = searchText.ToLowerInvariant();
        return _templateManager.GetAllTemplates()
            .Where(t =>
                t.Name.ToLowerInvariant().Contains(term) ||
                t.Description.ToLowerInvariant().Contains(term) ||
                t.Tags.Any(tag => tag.ToLowerInvariant().Contains(term)) ||
                t.Author.ToLowerInvariant().Contains(term) ||
                t.Category.ToLowerInvariant().Contains(term))
            .Take(maxResults)
            .ToList();
    }

    /// <summary>
    /// Get popular scan configurations
    /// </summary>
    public IReadOnlyList<ScanConfiguration> GetPopularScans(int count = 10)
    {
        return _scannerService.GetSavedScans()
            .Where(s => s.DownloadCount > 0)
            .OrderByDescending(s => s.Rating)
            .ThenByDescending(s => s.DownloadCount)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get recently created scan configurations
    /// </summary>
    public IReadOnlyList<ScanConfiguration> GetRecentScans(int count = 10)
    {
        return _scannerService.GetSavedScans()
            .OrderByDescending(s => s.CreatedDate)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get popular templates
    /// </summary>
    public IReadOnlyList<ScanTemplate> GetPopularTemplates(int count = 10)
    {
        return _templateManager.GetAllTemplates()
            .Where(t => t.Rating > 0)
            .OrderByDescending(t => t.Rating)
            .ThenByDescending(t => t.DownloadCount)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get templates by category
    /// </summary>
    public IReadOnlyList<ScanTemplate> GetTemplatesByCategory(string category)
    {
        return _templateManager.GetTemplatesByCategory(category);
    }

    /// <summary>
    /// Get all available categories
    /// </summary>
    public IReadOnlyList<string> GetAvailableCategories()
    {
        return _templateManager.GetAllTemplates()
            .Select(t => t.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToList();
    }

    /// <summary>
    /// Get scan statistics
    /// </summary>
    public ScanBrowseStatistics GetScanStatistics()
    {
        var scans = _scannerService.GetSavedScans();
        var templates = _templateManager.GetAllTemplates();

        return new ScanBrowseStatistics
        {
            TotalScans = scans.Count,
            TotalTemplates = templates.Count,
            RecentScansCount = scans.Count(s => s.CreatedDate >= DateTime.Now.AddDays(-7)),
            PopularScansCount = scans.Count(s => s.Rating >= 4.0m),
            CategoryBreakdown = templates.GroupBy(t => t.Category).ToDictionary(g => g.Key, g => g.Count()),
            CriteriaUsage = GetCriteriaUsageStatistics(scans.Concat(templates.Select(t => ConvertTemplateToConfig(t))))
        };
    }

    private IReadOnlyList<ScanConfiguration> ApplyFilters(IReadOnlyList<ScanConfiguration> scans, ScanFilter? filter)
    {
        if (filter == null)
            return scans;

        var filtered = scans.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLowerInvariant();
            filtered = filtered.Where(s =>
                s.Name.ToLowerInvariant().Contains(term) ||
                s.Description.ToLowerInvariant().Contains(term) ||
                s.Tags.Any(t => t.ToLowerInvariant().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Author))
        {
            filtered = filtered.Where(s => s.Author.Equals(filter.Author, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Tags != null && filter.Tags.Any())
        {
            filtered = filtered.Where(s => filter.Tags.Any(tag => s.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
        }

        if (filter.CreatedAfter.HasValue)
        {
            filtered = filtered.Where(s => s.CreatedDate >= filter.CreatedAfter.Value);
        }

        if (filter.CreatedBefore.HasValue)
        {
            filtered = filtered.Where(s => s.CreatedDate <= filter.CreatedBefore.Value);
        }

        if (filter.MinRating.HasValue)
        {
            filtered = filtered.Where(s => s.Rating >= filter.MinRating.Value);
        }

        if (filter.IsEnabled.HasValue)
        {
            filtered = filtered.Where(s => s.IsEnabled == filter.IsEnabled.Value);
        }

        return filtered.ToList();
    }

    private IReadOnlyList<ScanTemplate> ApplyTemplateFilters(IReadOnlyList<ScanTemplate> templates, TemplateFilter? filter)
    {
        if (filter == null)
            return templates;

        var filtered = templates.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLowerInvariant();
            filtered = filtered.Where(t =>
                t.Name.ToLowerInvariant().Contains(term) ||
                t.Description.ToLowerInvariant().Contains(term) ||
                t.Tags.Any(tag => tag.ToLowerInvariant().Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            filtered = filtered.Where(t => t.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Author))
        {
            filtered = filtered.Where(t => t.Author.Equals(filter.Author, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.IsBuiltIn.HasValue)
        {
            filtered = filtered.Where(t => t.IsBuiltIn == filter.IsBuiltIn.Value);
        }

        if (filter.MinRating.HasValue)
        {
            filtered = filtered.Where(t => t.Rating >= filter.MinRating.Value);
        }

        return filtered.ToList();
    }

    private IReadOnlyList<ScanConfiguration> ApplySorting(IReadOnlyList<ScanConfiguration> scans, ScanSortBy sortBy, bool sortDescending)
    {
        var sorted = sortBy switch
        {
            ScanSortBy.Name => scans.OrderBy(s => s.Name),
            ScanSortBy.Score => scans.OrderBy(s => s.Rating),
            ScanSortBy.Custom => scans.OrderBy(s => s.CreatedDate),
            _ => scans.OrderBy(s => s.Name)
        };

        return sortDescending ? sorted.Reverse().ToList() : sorted.ToList();
    }

    private IReadOnlyList<ScanTemplate> ApplyTemplateSorting(IReadOnlyList<ScanTemplate> templates, ScanSortBy sortBy, bool sortDescending)
    {
        var sorted = sortBy switch
        {
            ScanSortBy.Name => templates.OrderBy(t => t.Name),
            ScanSortBy.Score => templates.OrderBy(t => t.Rating),
            ScanSortBy.Custom => templates.OrderBy(t => t.CreatedDate),
            _ => templates.OrderBy(t => t.Name)
        };

        return sortDescending ? sorted.Reverse().ToList() : sorted.ToList();
    }

    private IEnumerable<ScanConfiguration> ApplyPagination(IReadOnlyList<ScanConfiguration> scans, int pageSize, int pageNumber)
    {
        return scans.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    }

    private IEnumerable<ScanTemplate> ApplyTemplatePagination(IReadOnlyList<ScanTemplate> templates, int pageSize, int pageNumber)
    {
        return templates.Skip((pageNumber - 1) * pageSize).Take(pageSize);
    }

    private Dictionary<ScanCriterionType, int> GetCriteriaUsageStatistics(IEnumerable<ScanConfiguration> configs)
    {
        return configs
            .SelectMany(c => c.Criteria)
            .GroupBy(c => c.Type)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    private ScanConfiguration ConvertTemplateToConfig(ScanTemplate template)
    {
        return _templateManager.CreateScanFromTemplate(template);
    }
}

/// <summary>
/// Filter for browsing scan configurations
/// </summary>
public class ScanFilter
{
    public string? SearchTerm { get; set; }
    public string? Author { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public decimal? MinRating { get; set; }
    public bool? IsEnabled { get; set; }
}

/// <summary>
/// Filter for browsing scan templates
/// </summary>
public class TemplateFilter
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public string? Author { get; set; }
    public bool? IsBuiltIn { get; set; }
    public decimal? MinRating { get; set; }
}

/// <summary>
/// Result of a scan browse operation
/// </summary>
public class ScanBrowseResult
{
    public List<ScanConfiguration> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public ScanFilter? Filter { get; set; }
    public ScanSortBy SortBy { get; set; }
    public bool SortDescending { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a template browse operation
/// </summary>
public class TemplateBrowseResult
{
    public List<ScanTemplate> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public TemplateFilter? Filter { get; set; }
    public ScanSortBy SortBy { get; set; }
    public bool SortDescending { get; set; }
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Statistics about scans and templates
/// </summary>
public class ScanBrowseStatistics
{
    public int TotalScans { get; set; }
    public int TotalTemplates { get; set; }
    public int RecentScansCount { get; set; }
    public int PopularScansCount { get; set; }
    public Dictionary<string, int> CategoryBreakdown { get; set; } = new();
    public Dictionary<ScanCriterionType, int> CriteriaUsage { get; set; } = new();
}
