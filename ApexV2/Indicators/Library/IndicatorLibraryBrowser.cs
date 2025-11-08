using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Indicators.Custom;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Library;

/// <summary>
/// Service for browsing and filtering the indicator library
/// </summary>
public class IndicatorLibraryBrowser
{
    private readonly IndicatorLibraryManager _libraryManager;
    private readonly IChartLogger _logger;

    public IndicatorLibraryBrowser(IndicatorLibraryManager libraryManager, IChartLogger? logger = null)
    {
        _libraryManager = libraryManager ?? throw new ArgumentNullException(nameof(libraryManager));
        _logger = logger ?? new NullLogger();
    }

    /// <summary>
    /// Browse indicators with advanced filtering and sorting
    /// </summary>
    public async Task<IndicatorLibraryBrowseResult> BrowseIndicatorsAsync(IndicatorLibraryFilter? filter = null, IndicatorLibrarySortBy sortBy = IndicatorLibrarySortBy.Name, SortDirection sortDirection = SortDirection.Ascending, int pageSize = 50, int pageNumber = 1)
    {
        try
        {
            var allIndicators = _libraryManager.GetAllIndicators();
            var filteredIndicators = ApplyFilters(allIndicators, filter);
            var sortedIndicators = ApplySorting(filteredIndicators, sortBy, sortDirection);
            var paginatedResults = ApplyPagination(sortedIndicators, pageSize, pageNumber);

            return new IndicatorLibraryBrowseResult
            {
                Items = paginatedResults.ToList(),
                TotalItems = filteredIndicators.Count,
                PageSize = pageSize,
                PageNumber = pageNumber,
                TotalPages = (int)Math.Ceiling((double)filteredIndicators.Count / pageSize),
                Filter = filter,
                SortBy = sortBy,
                SortDirection = sortDirection
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to browse indicators: {ex.Message}", ex);
            return new IndicatorLibraryBrowseResult();
        }
    }

    /// <summary>
    /// Get popular indicators based on rating and usage
    /// </summary>
    public IReadOnlyList<IndicatorLibraryItem> GetPopularIndicators(int count = 10)
    {
        return _libraryManager.GetAllIndicators()
            .Where(i => i.Rating > 0)
            .OrderByDescending(i => i.Rating)
            .ThenByDescending(i => i.DownloadCount)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get recently added indicators
    /// </summary>
    public IReadOnlyList<IndicatorLibraryItem> GetRecentIndicators(int count = 10)
    {
        return _libraryManager.GetAllIndicators()
            .OrderByDescending(i => i.CreatedDate)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get recently updated indicators
    /// </summary>
    public IReadOnlyList<IndicatorLibraryItem> GetRecentlyUpdatedIndicators(int count = 10)
    {
        return _libraryManager.GetAllIndicators()
            .OrderByDescending(i => i.ModifiedDate)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Get indicators by author
    /// </summary>
    public IReadOnlyList<IndicatorLibraryItem> GetIndicatorsByAuthor(string author)
    {
        return _libraryManager.GetAllIndicators()
            .Where(i => i.Author.Equals(author, StringComparison.OrdinalIgnoreCase))
            .OrderBy(i => i.Name)
            .ToList();
    }

    /// <summary>
    /// Get related indicators based on tags and category
    /// </summary>
    public IReadOnlyList<IndicatorLibraryItem> GetRelatedIndicators(string indicatorId, int count = 5)
    {
        if (!Guid.TryParse(indicatorId, out var guid))
            return new List<IndicatorLibraryItem>().AsReadOnly();
            
        var allIndicators = _libraryManager.GetAllIndicators();
        var targetIndicator = allIndicators.FirstOrDefault(i => i.Id == guid);
        
        if (targetIndicator == null)
            return new List<IndicatorLibraryItem>().AsReadOnly();

        return allIndicators
            .Where(i => i.Id != guid)
            .Select(i => new { Indicator = i, Score = CalculateRelatedScore(targetIndicator, i) })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select(x => x.Indicator)
            .ToList();
    }

    /// <summary>
    /// Get auto-complete suggestions for search
    /// </summary>
    public IReadOnlyList<string> GetSearchSuggestions(string searchTerm, int maxSuggestions = 10)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return new List<string>().AsReadOnly();

        var term = searchTerm.ToLowerInvariant();
        var suggestions = new HashSet<string>();

        var allIndicators = _libraryManager.GetAllIndicators();

        // Add indicator names
        foreach (var indicator in allIndicators)
        {
            if (indicator.Name.ToLowerInvariant().Contains(term))
                suggestions.Add(indicator.Name);

            // Add tags
            foreach (var tag in indicator.Tags)
            {
                if (tag.ToLowerInvariant().Contains(term))
                    suggestions.Add(tag);
            }

            // Add author names
            if (indicator.Author.ToLowerInvariant().Contains(term))
                suggestions.Add(indicator.Author);
        }

        // Add categories
        foreach (var category in _libraryManager.GetCategories())
        {
            if (category.Name.ToLowerInvariant().Contains(term))
                suggestions.Add(category.Name);
        }

        return suggestions.Take(maxSuggestions).OrderBy(s => s).ToList();
    }

    private IReadOnlyList<IndicatorLibraryItem> ApplyFilters(IReadOnlyList<IndicatorLibraryItem> indicators, IndicatorLibraryFilter? filter)
    {
        if (filter == null)
            return indicators;

        var filtered = indicators.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.Name.ToLowerInvariant().Contains(term) ||
                i.Description.ToLowerInvariant().Contains(term) ||
                i.Tags.Any(t => t.ToLowerInvariant().Contains(term)) ||
                i.Author.ToLowerInvariant().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            filtered = filtered.Where(i => i.Category.Equals(filter.Category, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.Tags != null && filter.Tags.Any())
        {
            filtered = filtered.Where(i => filter.Tags.Any(tag => i.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Author))
        {
            filtered = filtered.Where(i => i.Author.Equals(filter.Author, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.MinRating.HasValue)
        {
            filtered = filtered.Where(i => i.Rating >= filter.MinRating.Value);
        }

        if (filter.CreatedAfter.HasValue)
        {
            filtered = filtered.Where(i => i.CreatedDate >= filter.CreatedAfter.Value);
        }

        if (filter.CreatedBefore.HasValue)
        {
            filtered = filtered.Where(i => i.CreatedDate <= filter.CreatedBefore.Value);
        }

        if (filter.IsBuiltIn.HasValue)
        {
            filtered = filtered.Where(i => i.IsBuiltIn == filter.IsBuiltIn.Value);
        }

        return filtered.ToList();
    }

    private IReadOnlyList<IndicatorLibraryItem> ApplySorting(IReadOnlyList<IndicatorLibraryItem> indicators, IndicatorLibrarySortBy sortBy, SortDirection sortDirection)
    {
        var sorted = sortBy switch
        {
            IndicatorLibrarySortBy.Name => indicators.OrderBy(i => i.Name),
            IndicatorLibrarySortBy.Author => indicators.OrderBy(i => i.Author),
            IndicatorLibrarySortBy.Category => indicators.OrderBy(i => i.Category),
            IndicatorLibrarySortBy.CreatedDate => indicators.OrderBy(i => i.CreatedDate),
            IndicatorLibrarySortBy.ModifiedDate => indicators.OrderBy(i => i.ModifiedDate),
            IndicatorLibrarySortBy.Rating => indicators.OrderBy(i => i.Rating),
            IndicatorLibrarySortBy.DownloadCount => indicators.OrderBy(i => i.DownloadCount),
            _ => indicators.OrderBy(i => i.Name)
        };

        return sortDirection == SortDirection.Descending 
            ? sorted.Reverse().ToList() 
            : sorted.ToList();
    }

    private IEnumerable<IndicatorLibraryItem> ApplyPagination(IReadOnlyList<IndicatorLibraryItem> indicators, int pageSize, int pageNumber)
    {
        var skip = (pageNumber - 1) * pageSize;
        return indicators.Skip(skip).Take(pageSize);
    }

    private int CalculateRelatedScore(IndicatorLibraryItem target, IndicatorLibraryItem candidate)
    {
        var score = 0;

        // Same category
        if (target.Category.Equals(candidate.Category, StringComparison.OrdinalIgnoreCase))
            score += 3;

        // Common tags
        var commonTags = target.Tags.Intersect(candidate.Tags, StringComparer.OrdinalIgnoreCase).Count();
        score += commonTags * 2;

        // Same author
        if (target.Author.Equals(candidate.Author, StringComparison.OrdinalIgnoreCase))
            score += 1;

        return score;
    }
}

/// <summary>
/// Result of a library browse operation
/// </summary>
public class IndicatorLibraryBrowseResult
{
    public List<IndicatorLibraryItem> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int PageSize { get; set; }
    public int PageNumber { get; set; }
    public int TotalPages { get; set; }
    public IndicatorLibraryFilter? Filter { get; set; }
    public IndicatorLibrarySortBy SortBy { get; set; }
    public SortDirection SortDirection { get; set; }

    public bool HasNextPage => PageNumber < TotalPages;
    public bool HasPreviousPage => PageNumber > 1;
}
