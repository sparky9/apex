using ApexV2.Analysis.Watchlist;
using System;
using System.Linq;
using Xunit;

namespace ApexV2.Tests.Analysis.Watchlist;

public class WatchlistModelsTests
{
    [Fact]
    public void WatchlistViewModel_CalculatesCorrectAggregates()
    {
        // Arrange
        var watchlist = new WatchlistViewModel
        {
            Id = 1,
            Name = "Test Watchlist",
            Items = new()
            {
                new() { Symbol = "AAPL", LastPrice = 150m, Change = 2.5m, ChangePercent = 1.69m },
                new() { Symbol = "MSFT", LastPrice = 200m, Change = -1.2m, ChangePercent = -0.6m },
                new() { Symbol = "GOOG", LastPrice = 100m, Change = 0m, ChangePercent = 0m }
            }
        };

        // Act & Assert
        Assert.Equal(3, watchlist.ItemCount);
        Assert.Equal(450m, watchlist.TotalValue);
        Assert.Equal(1.3m, watchlist.TotalGainLoss);
        Assert.Equal(0.363m, Math.Round(watchlist.AverageChangePercent, 3));
    }

    [Fact]
    public void WatchlistItemViewModel_FormatsValuesCorrectly()
    {
        // Arrange
        var item = new WatchlistItemViewModel
        {
            Symbol = "AAPL",
            LastPrice = 150.75m,
            Change = 2.50m,
            ChangePercent = 1.69m,
            Volume = 1234567
        };

        // Act & Assert
        Assert.Equal("$150.75", item.FormattedPrice);
        Assert.Equal("+2.50", item.FormattedChange);
        Assert.Equal("+1.69%", item.FormattedChangePercent);
        Assert.Equal("1,234,567", item.FormattedVolume);
        Assert.True(item.IsPositive);
        Assert.False(item.IsNegative);
        Assert.Equal("↑", item.TrendIndicator);
    }

    [Fact]
    public void WatchlistItemViewModel_HandlesNegativeValues()
    {
        // Arrange
        var item = new WatchlistItemViewModel
        {
            Symbol = "GOOG",
            LastPrice = 95.25m,
            Change = -3.75m,
            ChangePercent = -3.8m,
            Volume = 987654
        };

        // Act & Assert
        Assert.Equal("$95.25", item.FormattedPrice);
        Assert.Equal("-3.75", item.FormattedChange);
        Assert.Equal("-3.80%", item.FormattedChangePercent);
        Assert.Equal("987,654", item.FormattedVolume);
        Assert.False(item.IsPositive);
        Assert.True(item.IsNegative);
        Assert.Equal("↓", item.TrendIndicator);
    }

    [Fact]
    public void WatchlistItemViewModel_HandlesZeroChange()
    {
        // Arrange
        var item = new WatchlistItemViewModel
        {
            Symbol = "MSFT",
            LastPrice = 200m,
            Change = 0m,
            ChangePercent = 0m,
            Volume = 500000
        };

        // Act & Assert
        Assert.Equal("$200.00", item.FormattedPrice);
        Assert.Equal("0.00", item.FormattedChange);
        Assert.Equal("0.00%", item.FormattedChangePercent);
        Assert.Equal("500,000", item.FormattedVolume);
        Assert.True(item.IsPositive);  // Zero is considered positive
        Assert.False(item.IsNegative);
        Assert.Equal("→", item.TrendIndicator);
    }

    [Fact]
    public void WatchlistItemViewModel_PropertyChanged_FiresForFormattedProperties()
    {
        // Arrange
        var item = new WatchlistItemViewModel();
        var propertyChangedEvents = new List<string>();
        
        item.PropertyChanged += (sender, e) => 
        {
            if (e.PropertyName != null)
                propertyChangedEvents.Add(e.PropertyName);
        };

        // Act
        item.LastPrice = 100m;

        // Assert
        Assert.Contains(nameof(WatchlistItemViewModel.LastPrice), propertyChangedEvents);
        Assert.Contains(nameof(WatchlistItemViewModel.FormattedPrice), propertyChangedEvents);
    }

    [Fact]
    public void WatchlistItemViewModel_PropertyChanged_FiresForChangeProperties()
    {
        // Arrange
        var item = new WatchlistItemViewModel();
        var propertyChangedEvents = new List<string>();
        
        item.PropertyChanged += (sender, e) => 
        {
            if (e.PropertyName != null)
                propertyChangedEvents.Add(e.PropertyName);
        };

        // Act
        item.Change = 5m;

        // Assert
        Assert.Contains(nameof(WatchlistItemViewModel.Change), propertyChangedEvents);
        Assert.Contains(nameof(WatchlistItemViewModel.FormattedChange), propertyChangedEvents);
        Assert.Contains(nameof(WatchlistItemViewModel.IsPositive), propertyChangedEvents);
        Assert.Contains(nameof(WatchlistItemViewModel.IsNegative), propertyChangedEvents);
        Assert.Contains(nameof(WatchlistItemViewModel.TrendIndicator), propertyChangedEvents);
    }

    [Fact]
    public void WatchlistFilter_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var filter = new WatchlistFilter();

        // Assert
        Assert.Null(filter.SymbolFilter);
        Assert.Null(filter.MinPrice);
        Assert.Null(filter.MaxPrice);
        Assert.Null(filter.MinChange);
        Assert.Null(filter.MaxChange);
        Assert.Null(filter.MinVolume);
        Assert.Equal(WatchlistSortBy.Symbol, filter.SortBy);
        Assert.False(filter.SortDescending);
    }

    [Fact]
    public void WatchlistConfig_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new WatchlistConfig();

        // Assert
        Assert.Equal(30, config.RefreshIntervalSeconds);
        Assert.True(config.AutoRefreshEnabled);
        Assert.True(config.ShowChangePercent);
        Assert.True(config.ShowVolume);
        Assert.False(config.PlaySoundOnPriceAlert);
        Assert.True(config.HighlightSignificantChanges);
        Assert.Equal(5.0m, config.SignificantChangeThreshold);
        Assert.Equal(100, config.MaxSymbolsPerWatchlist);
        Assert.False(config.AllowDuplicateSymbols);
    }

    [Fact]
    public void WatchlistStatistics_DefaultState_IsEmpty()
    {
        // Arrange & Act
        var stats = new WatchlistStatistics();

        // Assert
        Assert.Equal(0, stats.TotalWatchlists);
        Assert.Equal(0, stats.TotalSymbols);
        Assert.Empty(stats.TopGainers);
        Assert.Empty(stats.TopLosers);
        Assert.Empty(stats.MostActive);
    }

    [Fact]
    public void WatchlistChangedEventArgs_Constructor_SetsProperties()
    {
        // Arrange
        var watchlist = new WatchlistViewModel { Id = 1, Name = "Test" };
        var changeType = WatchlistChangeType.SymbolAdded;

        // Act
        var eventArgs = new WatchlistChangedEventArgs(changeType, watchlist);

        // Assert
        Assert.Equal(changeType, eventArgs.ChangeType);
        Assert.Equal(watchlist, eventArgs.Watchlist);
    }

    [Fact]
    public void WatchlistItemUpdatedEventArgs_Constructor_SetsProperties()
    {
        // Arrange
        var watchlistId = 1;
        var item = new WatchlistItemViewModel { Symbol = "AAPL" };

        // Act
        var eventArgs = new WatchlistItemUpdatedEventArgs(watchlistId, item);

        // Assert
        Assert.Equal(watchlistId, eventArgs.WatchlistId);
        Assert.Equal(item, eventArgs.Item);
    }

    [Fact]
    public void WatchlistAlert_Properties_CanBeSet()
    {
        // Arrange & Act
        var alert = new WatchlistAlert
        {
            Symbol = "AAPL",
            AlertType = WatchlistAlertType.SignificantChange,
            Message = "Test message",
            Severity = AlertSeverity.High,
            Timestamp = DateTime.Now,
            Value = 5.5m
        };

        // Assert
        Assert.Equal("AAPL", alert.Symbol);
        Assert.Equal(WatchlistAlertType.SignificantChange, alert.AlertType);
        Assert.Equal("Test message", alert.Message);
        Assert.Equal(AlertSeverity.High, alert.Severity);
        Assert.Equal(5.5m, alert.Value);
    }

    [Fact]
    public void WatchlistPerformance_DefaultState_IsEmpty()
    {
        // Arrange & Act
        var performance = new WatchlistPerformance();

        // Assert
        Assert.Equal(0, performance.WatchlistId);
        Assert.Equal(string.Empty, performance.WatchlistName);
        Assert.Equal(0, performance.TotalSymbols);
        Assert.Equal(0m, performance.TotalValue);
        Assert.Equal(0m, performance.AveragePrice);
        Assert.Equal(0, performance.Winners);
        Assert.Equal(0, performance.Losers);
        Assert.Equal(0, performance.Unchanged);
        Assert.Null(performance.BestPerformer);
        Assert.Null(performance.WorstPerformer);
    }

    [Fact]
    public void SectorAnalysis_Properties_CanBeSet()
    {
        // Arrange & Act
        var analysis = new SectorAnalysis
        {
            SectorName = "Technology",
            SymbolCount = 5,
            TotalValue = 1000m,
            AverageChange = 2.5m,
            TotalVolume = 5000000,
            Symbols = new List<string> { "AAPL", "MSFT", "GOOG" }
        };

        // Assert
        Assert.Equal("Technology", analysis.SectorName);
        Assert.Equal(5, analysis.SymbolCount);
        Assert.Equal(1000m, analysis.TotalValue);
        Assert.Equal(2.5m, analysis.AverageChange);
        Assert.Equal(5000000, analysis.TotalVolume);
        Assert.Equal(3, analysis.Symbols.Count);
        Assert.Contains("AAPL", analysis.Symbols);
    }

    [Fact]
    public void WatchlistComparison_DefaultState_IsEmpty()
    {
        // Arrange & Act
        var comparison = new WatchlistComparison();

        // Assert
        Assert.Empty(comparison.Watchlists);
        Assert.Null(comparison.BestPerformingWatchlist);
        Assert.Null(comparison.WorstPerformingWatchlist);
        Assert.Equal(0, comparison.TotalSymbolsAcrossAll);
        Assert.Equal(0m, comparison.AveragePerformance);
    }

    [Fact]
    public void WatchlistSortBy_Enum_HasCorrectValues()
    {
        // Act & Assert
        var values = Enum.GetValues<WatchlistSortBy>();
        
        Assert.Contains(WatchlistSortBy.Symbol, values);
        Assert.Contains(WatchlistSortBy.LastPrice, values);
        Assert.Contains(WatchlistSortBy.Change, values);
        Assert.Contains(WatchlistSortBy.ChangePercent, values);
        Assert.Contains(WatchlistSortBy.Volume, values);
        Assert.Contains(WatchlistSortBy.LastUpdate, values);
    }

    [Fact]
    public void WatchlistChangeType_Enum_HasCorrectValues()
    {
        // Act & Assert
        var values = Enum.GetValues<WatchlistChangeType>();
        
        Assert.Contains(WatchlistChangeType.Created, values);
        Assert.Contains(WatchlistChangeType.Deleted, values);
        Assert.Contains(WatchlistChangeType.SymbolAdded, values);
        Assert.Contains(WatchlistChangeType.SymbolRemoved, values);
        Assert.Contains(WatchlistChangeType.Reordered, values);
        Assert.Contains(WatchlistChangeType.Renamed, values);
    }

    [Fact]
    public void WatchlistAlertType_Enum_HasCorrectValues()
    {
        // Act & Assert
        var values = Enum.GetValues<WatchlistAlertType>();
        
        Assert.Contains(WatchlistAlertType.SignificantChange, values);
        Assert.Contains(WatchlistAlertType.HighVolume, values);
        Assert.Contains(WatchlistAlertType.Breakout, values);
        Assert.Contains(WatchlistAlertType.Breakdown, values);
        Assert.Contains(WatchlistAlertType.NewHigh, values);
        Assert.Contains(WatchlistAlertType.NewLow, values);
    }

    [Fact]
    public void AlertSeverity_Enum_HasCorrectValues()
    {
        // Act & Assert
        var values = Enum.GetValues<AlertSeverity>();
        
        Assert.Contains(AlertSeverity.Low, values);
        Assert.Contains(AlertSeverity.Medium, values);
        Assert.Contains(AlertSeverity.High, values);
        Assert.Contains(AlertSeverity.Critical, values);
    }
}
