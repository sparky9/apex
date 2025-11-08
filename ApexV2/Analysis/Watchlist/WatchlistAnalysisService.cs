using ApexV2.Analysis.Watchlist;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ApexV2.Analysis.Watchlist;

/// <summary>
/// Advanced filtering and analysis service for watchlists
/// </summary>
public class WatchlistAnalysisService
{
    private readonly WatchlistService _watchlistService;

    public WatchlistAnalysisService(WatchlistService watchlistService)
    {
        _watchlistService = watchlistService;
    }

    /// <summary>
    /// Apply filters to watchlist items
    /// </summary>
    public async Task<List<WatchlistItemViewModel>> FilterItemsAsync(int watchlistId, WatchlistFilter filter)
    {
        var watchlist = await _watchlistService.GetWatchlistAsync(watchlistId);
        if (watchlist == null) return new List<WatchlistItemViewModel>();

        var filteredItems = watchlist.Items.AsEnumerable();

        // Apply symbol filter
        if (!string.IsNullOrWhiteSpace(filter.SymbolFilter))
        {
            filteredItems = filteredItems.Where(item => 
                item.Symbol.Contains(filter.SymbolFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Apply price filters
        if (filter.MinPrice.HasValue)
        {
            filteredItems = filteredItems.Where(item => item.LastPrice >= filter.MinPrice.Value);
        }
        
        if (filter.MaxPrice.HasValue)
        {
            filteredItems = filteredItems.Where(item => item.LastPrice <= filter.MaxPrice.Value);
        }

        // Apply change filters
        if (filter.MinChange.HasValue)
        {
            filteredItems = filteredItems.Where(item => item.Change >= filter.MinChange.Value);
        }
        
        if (filter.MaxChange.HasValue)
        {
            filteredItems = filteredItems.Where(item => item.Change <= filter.MaxChange.Value);
        }

        // Apply volume filter
        if (filter.MinVolume.HasValue)
        {
            filteredItems = filteredItems.Where(item => item.Volume >= filter.MinVolume.Value);
        }

        // Apply sorting
        filteredItems = ApplySorting(filteredItems, filter.SortBy, filter.SortDescending);

        return filteredItems.ToList();
    }

    /// <summary>
    /// Get performance analytics for a watchlist
    /// </summary>
    public async Task<WatchlistPerformance> GetPerformanceAsync(int watchlistId, TimeSpan period)
    {
        var watchlist = await _watchlistService.GetWatchlistAsync(watchlistId);
        if (watchlist == null) 
        {
            return new WatchlistPerformance();
        }

        var performance = new WatchlistPerformance
        {
            WatchlistId = watchlistId,
            WatchlistName = watchlist.Name,
            Period = period,
            AnalysisDate = DateTime.Now
        };

        if (watchlist.Items.Any())
        {
            performance.TotalSymbols = watchlist.Items.Count;
            performance.TotalValue = watchlist.Items.Sum(i => i.LastPrice);
            performance.TotalChange = watchlist.Items.Sum(i => i.Change);
            performance.AveragePrice = performance.TotalValue / performance.TotalSymbols;
            performance.AverageChange = performance.TotalChange / performance.TotalSymbols;
            performance.AverageChangePercent = watchlist.Items.Average(i => i.ChangePercent);

            // Calculate winners/losers
            performance.Winners = watchlist.Items.Count(i => i.Change > 0);
            performance.Losers = watchlist.Items.Count(i => i.Change < 0);
            performance.Unchanged = watchlist.Items.Count(i => i.Change == 0);

            // Find best/worst performers
            var sortedByChange = watchlist.Items.OrderByDescending(i => i.ChangePercent).ToList();
            performance.BestPerformer = sortedByChange.FirstOrDefault();
            performance.WorstPerformer = sortedByChange.LastOrDefault();

            // Calculate volatility (standard deviation of changes)
            var changes = watchlist.Items.Select(i => (double)i.ChangePercent).ToList();
            performance.Volatility = CalculateStandardDeviation(changes);

            // Risk metrics
            performance.MaxGain = watchlist.Items.Max(i => i.ChangePercent);
            performance.MaxLoss = watchlist.Items.Min(i => i.ChangePercent);
            performance.ValueAtRisk = CalculateValueAtRisk(changes, 0.05); // 5% VaR

            // Volume analysis
            performance.TotalVolume = watchlist.Items.Sum(i => i.Volume);
            performance.AverageVolume = performance.TotalVolume / performance.TotalSymbols;
            performance.HighestVolume = watchlist.Items.Max(i => i.Volume);
        }

        return performance;
    }

    /// <summary>
    /// Detect patterns and anomalies in watchlist data
    /// </summary>
    public async Task<List<WatchlistAlert>> DetectAnomaliesAsync(int watchlistId, WatchlistConfig config)
    {
        var watchlist = await _watchlistService.GetWatchlistAsync(watchlistId);
        if (watchlist == null) return new List<WatchlistAlert>();

        var alerts = new List<WatchlistAlert>();

        foreach (var item in watchlist.Items)
        {
            // Significant price changes
            if (config.HighlightSignificantChanges && 
                Math.Abs(item.ChangePercent) >= config.SignificantChangeThreshold)
            {
                alerts.Add(new WatchlistAlert
                {
                    Symbol = item.Symbol,
                    AlertType = WatchlistAlertType.SignificantChange,
                    Message = $"{item.Symbol} moved {item.FormattedChangePercent}",
                    Severity = Math.Abs(item.ChangePercent) > 10 ? AlertSeverity.High : AlertSeverity.Medium,
                    Timestamp = DateTime.Now,
                    Value = item.ChangePercent
                });
            }

            // High volume alerts
            if (item.Volume > 0) // Only if we have volume data
            {
                // This would need historical volume data for proper analysis
                // For now, using a simple threshold
                if (item.Volume > 1000000) // 1M+ volume
                {
                    alerts.Add(new WatchlistAlert
                    {
                        Symbol = item.Symbol,
                        AlertType = WatchlistAlertType.HighVolume,
                        Message = $"{item.Symbol} has unusually high volume: {item.FormattedVolume}",
                        Severity = AlertSeverity.Medium,
                        Timestamp = DateTime.Now,
                        Value = item.Volume
                    });
                }
            }

            // Price breakouts (would need more sophisticated analysis with support/resistance)
            if (Math.Abs(item.ChangePercent) > 7.5m) // Simple breakout detection
            {
                alerts.Add(new WatchlistAlert
                {
                    Symbol = item.Symbol,
                    AlertType = item.ChangePercent > 0 ? WatchlistAlertType.Breakout : WatchlistAlertType.Breakdown,
                    Message = $"{item.Symbol} potential {(item.ChangePercent > 0 ? "breakout" : "breakdown")}",
                    Severity = AlertSeverity.High,
                    Timestamp = DateTime.Now,
                    Value = item.ChangePercent
                });
            }
        }

        return alerts.OrderByDescending(a => a.Severity).ThenByDescending(a => a.Timestamp).ToList();
    }

    /// <summary>
    /// Compare performance between multiple watchlists
    /// </summary>
    public async Task<WatchlistComparison> CompareWatchlistsAsync(List<int> watchlistIds, TimeSpan period)
    {
        var comparison = new WatchlistComparison
        {
            Period = period,
            ComparisonDate = DateTime.Now,
            Watchlists = new List<WatchlistPerformance>()
        };

        foreach (var id in watchlistIds)
        {
            var performance = await GetPerformanceAsync(id, period);
            if (performance.TotalSymbols > 0)
            {
                comparison.Watchlists.Add(performance);
            }
        }

        if (comparison.Watchlists.Any())
        {
            comparison.BestPerformingWatchlist = comparison.Watchlists
                .OrderByDescending(w => w.AverageChangePercent)
                .First();
                
            comparison.WorstPerformingWatchlist = comparison.Watchlists
                .OrderBy(w => w.AverageChangePercent)
                .First();
                
            comparison.TotalSymbolsAcrossAll = comparison.Watchlists.Sum(w => w.TotalSymbols);
            comparison.AveragePerformance = comparison.Watchlists.Average(w => w.AverageChangePercent);
        }

        return comparison;
    }

    /// <summary>
    /// Get sector analysis for watchlist symbols
    /// </summary>
    public async Task<List<SectorAnalysis>> GetSectorAnalysisAsync(int watchlistId)
    {
        var watchlist = await _watchlistService.GetWatchlistAsync(watchlistId);
        if (watchlist == null) return new List<SectorAnalysis>();

        // Group symbols by sector (this would need fundamental data integration)
        // For now, using a simple classification based on symbol patterns
        var sectorGroups = watchlist.Items
            .GroupBy(item => ClassifySector(item.Symbol))
            .Select(group => new SectorAnalysis
            {
                SectorName = group.Key,
                SymbolCount = group.Count(),
                TotalValue = group.Sum(i => i.LastPrice),
                AverageChange = group.Average(i => i.ChangePercent),
                TotalVolume = group.Sum(i => i.Volume),
                Symbols = group.Select(i => i.Symbol).ToList()
            })
            .OrderByDescending(s => s.SymbolCount)
            .ToList();

        return sectorGroups;
    }

    #region Private Methods

    private IEnumerable<WatchlistItemViewModel> ApplySorting(
        IEnumerable<WatchlistItemViewModel> items, 
        WatchlistSortBy sortBy, 
        bool descending)
    {
        var sortedItems = sortBy switch
        {
            WatchlistSortBy.Symbol => descending 
                ? items.OrderByDescending(i => i.Symbol)
                : items.OrderBy(i => i.Symbol),
            WatchlistSortBy.LastPrice => descending
                ? items.OrderByDescending(i => i.LastPrice)
                : items.OrderBy(i => i.LastPrice),
            WatchlistSortBy.Change => descending
                ? items.OrderByDescending(i => i.Change)
                : items.OrderBy(i => i.Change),
            WatchlistSortBy.ChangePercent => descending
                ? items.OrderByDescending(i => i.ChangePercent)
                : items.OrderBy(i => i.ChangePercent),
            WatchlistSortBy.Volume => descending
                ? items.OrderByDescending(i => i.Volume)
                : items.OrderBy(i => i.Volume),
            WatchlistSortBy.LastUpdate => descending
                ? items.OrderByDescending(i => i.LastUpdate)
                : items.OrderBy(i => i.LastUpdate),
            _ => items.OrderBy(i => i.Symbol)
        };

        return sortedItems;
    }

    private double CalculateStandardDeviation(List<double> values)
    {
        if (values.Count <= 1) return 0;

        var mean = values.Average();
        var sumOfSquares = values.Sum(x => Math.Pow(x - mean, 2));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }

    private double CalculateValueAtRisk(List<double> returns, double confidence)
    {
        if (returns.Count == 0) return 0;
        
        var sortedReturns = returns.OrderBy(r => r).ToList();
        var index = (int)Math.Floor(confidence * sortedReturns.Count);
        
        return index < sortedReturns.Count ? sortedReturns[index] : sortedReturns.Last();
    }

    private string ClassifySector(string symbol)
    {
        // Simple sector classification - this would need proper fundamental data
        return symbol switch
        {
            var s when s.StartsWith("SHOP") || s.StartsWith("AMZN") => "Technology",
            var s when s.StartsWith("JPM") || s.StartsWith("TD") || s.StartsWith("RY") => "Financial",
            var s when s.StartsWith("JNJ") || s.StartsWith("PFE") => "Healthcare",
            var s when s.StartsWith("XOM") || s.StartsWith("CVX") || s.StartsWith("SU") => "Energy",
            var s when s.StartsWith("BA") || s.StartsWith("CAT") => "Industrial",
            var s when s.StartsWith("WMT") || s.StartsWith("TGT") => "Consumer Staples",
            _ => "Other"
        };
    }

    #endregion
}

/// <summary>
/// Performance analytics for a watchlist
/// </summary>
public class WatchlistPerformance
{
    public int WatchlistId { get; set; }
    public string WatchlistName { get; set; } = string.Empty;
    public TimeSpan Period { get; set; }
    public DateTime AnalysisDate { get; set; }
    
    public int TotalSymbols { get; set; }
    public decimal TotalValue { get; set; }
    public decimal TotalChange { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal AverageChange { get; set; }
    public decimal AverageChangePercent { get; set; }
    
    public int Winners { get; set; }
    public int Losers { get; set; }
    public int Unchanged { get; set; }
    
    public WatchlistItemViewModel? BestPerformer { get; set; }
    public WatchlistItemViewModel? WorstPerformer { get; set; }
    
    public double Volatility { get; set; }
    public decimal MaxGain { get; set; }
    public decimal MaxLoss { get; set; }
    public double ValueAtRisk { get; set; }
    
    public long TotalVolume { get; set; }
    public long AverageVolume { get; set; }
    public long HighestVolume { get; set; }
}

/// <summary>
/// Alert for watchlist anomalies and patterns
/// </summary>
public class WatchlistAlert
{
    public string Symbol { get; set; } = string.Empty;
    public WatchlistAlertType AlertType { get; set; }
    public string Message { get; set; } = string.Empty;
    public AlertSeverity Severity { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal Value { get; set; }
}

/// <summary>
/// Types of watchlist alerts
/// </summary>
public enum WatchlistAlertType
{
    SignificantChange,
    HighVolume,
    Breakout,
    Breakdown,
    NewHigh,
    NewLow
}

/// <summary>
/// Alert severity levels
/// </summary>
public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}

/// <summary>
/// Comparison between multiple watchlists
/// </summary>
public class WatchlistComparison
{
    public TimeSpan Period { get; set; }
    public DateTime ComparisonDate { get; set; }
    public List<WatchlistPerformance> Watchlists { get; set; } = new();
    public WatchlistPerformance? BestPerformingWatchlist { get; set; }
    public WatchlistPerformance? WorstPerformingWatchlist { get; set; }
    public int TotalSymbolsAcrossAll { get; set; }
    public decimal AveragePerformance { get; set; }
}

/// <summary>
/// Sector analysis for watchlist symbols
/// </summary>
public class SectorAnalysis
{
    public string SectorName { get; set; } = string.Empty;
    public int SymbolCount { get; set; }
    public decimal TotalValue { get; set; }
    public decimal AverageChange { get; set; }
    public long TotalVolume { get; set; }
    public List<string> Symbols { get; set; } = new();
}
