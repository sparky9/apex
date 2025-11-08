using ApexV2.Analysis.Watchlist;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ApexV2.Tests.Analysis.Watchlist;

public class WatchlistAnalysisServiceTests
{
    private readonly Mock<WatchlistService> _mockWatchlistService;
    private readonly WatchlistAnalysisService _analysisService;

    public WatchlistAnalysisServiceTests()
    {
        _mockWatchlistService = new Mock<WatchlistService>(
            Mock.Of<ApexV2.Core.Database.Repositories.IWatchlistRepository>(),
            Mock.Of<ApexV2.Data.MarketData.IMarketDataProvider>(),
            Mock.Of<ApexV2.Charts.Export.IChartLogger>());
        
        _analysisService = new WatchlistAnalysisService(_mockWatchlistService.Object);
    }

    [Fact]
    public async Task FilterItemsAsync_ReturnsAllItems_WithNoFilters()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var filter = new WatchlistFilter();

        // Act
        var result = await _analysisService.FilterItemsAsync(1, filter);

        // Assert
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public async Task FilterItemsAsync_FiltersSymbols_BySymbolFilter()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var filter = new WatchlistFilter { SymbolFilter = "APP" };

        // Act
        var result = await _analysisService.FilterItemsAsync(1, filter);

        // Assert
        Assert.Single(result);
        Assert.Equal("AAPL", result.First().Symbol);
    }

    [Fact]
    public async Task FilterItemsAsync_FiltersSymbols_ByPriceRange()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var filter = new WatchlistFilter 
        { 
            MinPrice = 140m, 
            MaxPrice = 160m 
        };

        // Act
        var result = await _analysisService.FilterItemsAsync(1, filter);

        // Assert
        Assert.Single(result);
        Assert.Equal("AAPL", result.First().Symbol);
    }

    [Fact]
    public async Task FilterItemsAsync_FiltersSymbols_ByChangeRange()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var filter = new WatchlistFilter 
        { 
            MinChange = 0m  // Only positive changes
        };

        // Act
        var result = await _analysisService.FilterItemsAsync(1, filter);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, r => r.Symbol == "AAPL");
        Assert.Contains(result, r => r.Symbol == "MSFT");
    }

    [Fact]
    public async Task FilterItemsAsync_FiltersSymbols_ByVolume()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var filter = new WatchlistFilter 
        { 
            MinVolume = 1500000  // High volume only
        };

        // Act
        var result = await _analysisService.FilterItemsAsync(1, filter);

        // Assert
        Assert.Single(result);
        Assert.Equal("MSFT", result.First().Symbol);
    }

    [Fact]
    public async Task FilterItemsAsync_SortsSymbols_ByChangePercent()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var filter = new WatchlistFilter 
        { 
            SortBy = WatchlistSortBy.ChangePercent,
            SortDescending = true
        };

        // Act
        var result = await _analysisService.FilterItemsAsync(1, filter);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal("MSFT", result[0].Symbol);  // +3.5%
        Assert.Equal("AAPL", result[1].Symbol);  // +1.69%
        Assert.Equal("GOOG", result[2].Symbol);  // -2.1%
    }

    [Fact]
    public async Task GetPerformanceAsync_CalculatesCorrectMetrics()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var period = TimeSpan.FromDays(1);

        // Act
        var performance = await _analysisService.GetPerformanceAsync(1, period);

        // Assert
        Assert.Equal(1, performance.WatchlistId);
        Assert.Equal("Test Watchlist", performance.WatchlistName);
        Assert.Equal(3, performance.TotalSymbols);
        Assert.Equal(2, performance.Winners);  // AAPL, MSFT
        Assert.Equal(1, performance.Losers);   // GOOG
        Assert.Equal(0, performance.Unchanged);
        
        Assert.Equal("MSFT", performance.BestPerformer?.Symbol);
        Assert.Equal("GOOG", performance.WorstPerformer?.Symbol);
        
        // Check calculated averages
        Assert.Equal(450m, performance.TotalValue);  // 150 + 200 + 100
        Assert.Equal(150m, performance.AveragePrice);
        Assert.Equal(1.0267m, performance.AverageChangePercent, 2);  // (1.69 + 3.5 - 2.1) / 3
    }

    [Fact]
    public async Task DetectAnomaliesAsync_DetectsSignificantChanges()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var config = new WatchlistConfig
        {
            HighlightSignificantChanges = true,
            SignificantChangeThreshold = 2.0m  // 2% threshold
        };

        // Act
        var alerts = await _analysisService.DetectAnomaliesAsync(1, config);

        // Assert
        Assert.Equal(2, alerts.Count);
        
        var significantChanges = alerts.Where(a => a.AlertType == WatchlistAlertType.SignificantChange);
        Assert.Equal(2, significantChanges.Count());
        
        // Should detect MSFT (+3.5%) and GOOG (-2.1%)
        Assert.Contains(alerts, a => a.Symbol == "MSFT" && a.AlertType == WatchlistAlertType.SignificantChange);
        Assert.Contains(alerts, a => a.Symbol == "GOOG" && a.AlertType == WatchlistAlertType.SignificantChange);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_DetectsHighVolume()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var config = new WatchlistConfig();

        // Act
        var alerts = await _analysisService.DetectAnomaliesAsync(1, config);

        // Assert
        var volumeAlerts = alerts.Where(a => a.AlertType == WatchlistAlertType.HighVolume);
        Assert.Single(volumeAlerts);
        Assert.Equal("MSFT", volumeAlerts.First().Symbol);
    }

    [Fact]
    public async Task DetectAnomaliesAsync_DetectsBreakouts()
    {
        // Arrange
        var watchlist = new WatchlistViewModel
        {
            Id = 1,
            Name = "Breakout Test",
            Items = new List<WatchlistItemViewModel>
            {
                new() { Symbol = "BREAKOUT", ChangePercent = 8.5m },  // Strong breakout
                new() { Symbol = "BREAKDOWN", ChangePercent = -9.2m }  // Strong breakdown
            }
        };

        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        var config = new WatchlistConfig();

        // Act
        var alerts = await _analysisService.DetectAnomaliesAsync(1, config);

        // Assert
        var breakoutAlerts = alerts.Where(a => 
            a.AlertType == WatchlistAlertType.Breakout || 
            a.AlertType == WatchlistAlertType.Breakdown);
        
        Assert.Equal(2, breakoutAlerts.Count());
        Assert.Contains(alerts, a => a.Symbol == "BREAKOUT" && a.AlertType == WatchlistAlertType.Breakout);
        Assert.Contains(alerts, a => a.Symbol == "BREAKDOWN" && a.AlertType == WatchlistAlertType.Breakdown);
    }

    [Fact]
    public async Task CompareWatchlistsAsync_ComparesMultipleWatchlists()
    {
        // Arrange
        var watchlist1 = CreateTestWatchlist();
        watchlist1.Id = 1;
        watchlist1.Name = "Watchlist 1";

        var watchlist2 = new WatchlistViewModel
        {
            Id = 2,
            Name = "Watchlist 2",
            Items = new List<WatchlistItemViewModel>
            {
                new() { Symbol = "TSLA", LastPrice = 800m, ChangePercent = 5.0m },
                new() { Symbol = "NVDA", LastPrice = 300m, ChangePercent = -1.0m }
            }
        };

        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist1);
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(2))
            .ReturnsAsync(watchlist2);

        var watchlistIds = new List<int> { 1, 2 };
        var period = TimeSpan.FromDays(1);

        // Act
        var comparison = await _analysisService.CompareWatchlistsAsync(watchlistIds, period);

        // Assert
        Assert.Equal(2, comparison.Watchlists.Count);
        Assert.Equal(5, comparison.TotalSymbolsAcrossAll);  // 3 + 2
        
        // Watchlist 2 should be best performing (average: 2.0% vs 1.03%)
        Assert.Equal("Watchlist 2", comparison.BestPerformingWatchlist?.WatchlistName);
        Assert.Equal("Watchlist 1", comparison.WorstPerformingWatchlist?.WatchlistName);
    }

    [Fact]
    public async Task GetSectorAnalysisAsync_GroupsSymbolsBySector()
    {
        // Arrange
        var watchlist = CreateTestWatchlist();
        _mockWatchlistService.Setup(s => s.GetWatchlistAsync(1))
            .ReturnsAsync(watchlist);

        // Act
        var sectorAnalysis = await _analysisService.GetSectorAnalysisAsync(1);

        // Assert
        Assert.NotEmpty(sectorAnalysis);
        
        var techSector = sectorAnalysis.FirstOrDefault(s => s.SectorName == "Technology");
        Assert.NotNull(techSector);
        Assert.Equal(3, techSector.SymbolCount);  // All symbols classified as Technology
        Assert.Contains("AAPL", techSector.Symbols);
        Assert.Contains("MSFT", techSector.Symbols);
        Assert.Contains("GOOG", techSector.Symbols);
    }

    private WatchlistViewModel CreateTestWatchlist()
    {
        return new WatchlistViewModel
        {
            Id = 1,
            Name = "Test Watchlist",
            Created = DateTime.Now.AddDays(-1),
            LastModified = DateTime.Now,
            Items = new List<WatchlistItemViewModel>
            {
                new()
                {
                    Symbol = "AAPL",
                    LastPrice = 150.00m,
                    Change = 2.50m,
                    ChangePercent = 1.69m,
                    Volume = 1000000,
                    LastUpdate = DateTime.Now
                },
                new()
                {
                    Symbol = "MSFT",
                    LastPrice = 200.00m,
                    Change = 6.80m,
                    ChangePercent = 3.5m,
                    Volume = 2000000,
                    LastUpdate = DateTime.Now
                },
                new()
                {
                    Symbol = "GOOG",
                    LastPrice = 100.00m,
                    Change = -2.15m,
                    ChangePercent = -2.1m,
                    Volume = 800000,
                    LastUpdate = DateTime.Now
                }
            }
        };
    }
}
