using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Library;

/// <summary>
/// Represents an item in the indicator library
/// </summary>
public class IndicatorLibraryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string Version { get; set; } = "1.0";
    public DateTime CreatedDate { get; set; }
    public DateTime ModifiedDate { get; set; }
    public bool IsBuiltIn { get; set; } = false;
    public double Rating { get; set; } = 0.0;
    public int DownloadCount { get; set; } = 0;
    public string ConfigId { get; set; } = string.Empty; // Reference to CustomIndicatorConfig
    public Dictionary<string, object> Metadata { get; set; } = new(); // Additional metadata
}

/// <summary>
/// Represents a category of indicators
/// </summary>
public class IndicatorCategory
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; } = false;
    public string IconPath { get; set; } = string.Empty;
    public int SortOrder { get; set; } = 0;
}

/// <summary>
/// Library statistics information
/// </summary>
public class IndicatorLibraryStats
{
    public int TotalIndicators { get; set; }
    public int BuiltInIndicators { get; set; }
    public int CustomIndicators { get; set; }
    public int Categories { get; set; }
    public int Tags { get; set; }
    public double AverageRating { get; set; }
    public List<string> MostUsedTags { get; set; } = new();
    public List<IndicatorLibraryItem> TopRatedIndicators { get; set; } = new();
}

/// <summary>
/// Metadata for library persistence
/// </summary>
public class IndicatorLibraryMetadata
{
    public string Version { get; set; } = "1.0";
    public DateTime LastUpdated { get; set; }
    public List<IndicatorLibraryItem> Items { get; set; } = new();
    public List<IndicatorCategory> Categories { get; set; } = new();
    public Dictionary<string, List<string>> Tags { get; set; } = new();
}

/// <summary>
/// Library backup structure for import/export
/// </summary>
public class IndicatorLibraryBackup
{
    public DateTime ExportDate { get; set; }
    public string Version { get; set; } = "1.0";
    public List<IndicatorCategory> Categories { get; set; } = new();
    public List<IndicatorLibraryItem> Items { get; set; } = new();
    public Dictionary<string, List<string>> Tags { get; set; } = new();
}

/// <summary>
/// Filter criteria for library searches
/// </summary>
public class IndicatorLibraryFilter
{
    public string? SearchTerm { get; set; }
    public string? Category { get; set; }
    public List<string>? Tags { get; set; }
    public string? Author { get; set; }
    public double? MinRating { get; set; }
    public DateTime? CreatedAfter { get; set; }
    public DateTime? CreatedBefore { get; set; }
    public bool? IsBuiltIn { get; set; }
}

/// <summary>
/// Alternative filter alias for browser compatibility
/// </summary>
public class LibraryFilter : IndicatorLibraryFilter
{
    public List<string>? Categories { get; set; } = new();
    public List<string>? Authors { get; set; } = new();
    public new double? MinRating { get; set; }
    public double? MaxRating { get; set; }
    public new DateTime? CreatedBefore { get; set; }
    public new DateTime? CreatedAfter { get; set; }
    public int? MinUsageCount { get; set; }
    public int? MaxUsageCount { get; set; }
}

/// <summary>
/// Sort options for library items
/// </summary>
public class LibrarySortOptions
{
    public IndicatorLibrarySortBy SortBy { get; set; } = IndicatorLibrarySortBy.Name;
    public SortDirection SortDirection { get; set; } = SortDirection.Ascending;
}

/// <summary>
/// Pagination options for browsing
/// </summary>
public class PaginationOptions
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}

/// <summary>
/// Paginated result container
/// </summary>
public class PaginatedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalItems { get; set; }
    public int TotalPages { get; set; }
    public int CurrentPage { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage => CurrentPage < TotalPages;
    public bool HasPreviousPage => CurrentPage > 1;
}

/// <summary>
/// Sort options for library items
/// </summary>
public enum IndicatorLibrarySortBy
{
    Name,
    Author,
    Category,
    CreatedDate,
    ModifiedDate,
    Rating,
    DownloadCount
}

/// <summary>
/// Sort direction
/// </summary>
public enum SortDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Library statistics for analytics and reporting
/// </summary>
public class IndicatorLibraryStatistics
{
    public int TotalIndicators { get; set; }
    public int TotalCategories { get; set; }
    public int TotalTags { get; set; }
    public Dictionary<string, int> CategoryCounts { get; set; } = new();
    public Dictionary<string, int> TagCounts { get; set; } = new();
    public Dictionary<string, int> AuthorCounts { get; set; } = new();
    public DateTime LastUpdated { get; set; }
    public int TotalUsageCount { get; set; }
    public double AverageRating { get; set; }
    public Dictionary<string, object> AdditionalMetrics { get; set; } = new();
}

/// <summary>
/// Library statistics for reporting
/// </summary>
public class LibraryStatistics
{
    public int TotalIndicators { get; set; }
    public int TotalCategories { get; set; } // Added missing property
    public int CategoriesCount { get; set; }
    public int TagsCount { get; set; }
    public string MostUsedCategory { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public Dictionary<string, int> CategoryCounts { get; set; } = new(); // Added missing property
}
