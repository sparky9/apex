using ApexV2.Analysis.Watchlist;
using ApexV2.Core.Database;
using ApexV2.Core.Database.Repositories;
using ApexV2.Charts.Export; // For IChartLogger
using ApexV2.Data.MarketData;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ApexV2.Tests.Analysis.Watchlist;

public class WatchlistServiceTests
{
    private readonly Mock<IWatchlistRepository> _mockRepository;
    private readonly Mock<IMarketDataProvider> _mockMarketDataProvider;
    private readonly TestLogger _logger;
    private readonly WatchlistService _watchlistService;

    public WatchlistServiceTests()
    {
        _mockRepository = new Mock<IWatchlistRepository>();
        _mockMarketDataProvider = new Mock<IMarketDataProvider>();
        _logger = new TestLogger();
        
        _watchlistService = new WatchlistService(
            _mockRepository.Object,
            _mockMarketDataProvider.Object,
            _logger);
    }

    [Fact]
    public async Task GetAllWatchlistsAsync_ReturnsEmptyList_WhenNoWatchlists()
    {
        // Arrange
        _mockRepository.Setup(r => r.GetAllWatchlistsAsync())
            .ReturnsAsync(new List<WatchlistEntity>());

        // Act
        var result = await _watchlistService.GetAllWatchlistsAsync();

        // Assert
        Assert.Empty(result);
        _mockRepository.Verify(r => r.GetAllWatchlistsAsync(), Times.Once);
    }

    [Fact]
    public async Task GetAllWatchlistsAsync_ReturnsWatchlistsWithMarketData()
    {
        // Arrange
        var entities = new List<WatchlistEntity>
        {
            new()
            {
                Id = 1,
                Name = "Test Watchlist",
                Created = DateTime.Now.AddDays(-1),
                LastModified = DateTime.Now,
                Items = new List<WatchlistItemEntity>
                {
                    new() { Symbol = "AAPL", SortOrder = 1, Added = DateTime.Now },
                    new() { Symbol = "MSFT", SortOrder = 2, Added = DateTime.Now }
                }
            }
        };

        _mockRepository.Setup(r => r.GetAllWatchlistsAsync())
            .ReturnsAsync(entities);

        var quote = new StockQuote
        {
            Symbol = "AAPL",
            Price = 150.00m,
            Change = 2.50m,
            ChangePercent = 1.69m,
            Volume = 1000000,
            Timestamp = DateTime.Now
        };

        _mockMarketDataProvider.Setup(p => p.GetQuoteAsync(It.IsAny<string>()))
            .ReturnsAsync(quote);

        // Act
        var result = await _watchlistService.GetAllWatchlistsAsync();

        // Assert
        Assert.Single(result);
        var watchlist = result.First();
        Assert.Equal("Test Watchlist", watchlist.Name);
        Assert.Equal(2, watchlist.Items.Count);
        
        var item = watchlist.Items.First(i => i.Symbol == "AAPL");
        Assert.Equal(150.00m, item.LastPrice);
        Assert.Equal(2.50m, item.Change);
        Assert.Equal(1.69m, item.ChangePercent);
    }

    [Fact]
    public async Task CreateWatchlistAsync_CreatesWatchlist_WithValidName()
    {
        // Arrange
        var name = "My New Watchlist";
        var entity = new WatchlistEntity
        {
            Id = 1,
            Name = name,
            Created = DateTime.Now,
            LastModified = DateTime.Now,
            Items = new List<WatchlistItemEntity>()
        };

        _mockRepository.Setup(r => r.GetWatchlistByNameAsync(name))
            .ReturnsAsync((WatchlistEntity?)null);
        _mockRepository.Setup(r => r.CreateWatchlistAsync(name))
            .ReturnsAsync(entity);

        // Act
        var result = await _watchlistService.CreateWatchlistAsync(name);

        // Assert
        Assert.Equal(name, result.Name);
        Assert.Equal(1, result.Id);
        Assert.Empty(result.Items);
        _mockRepository.Verify(r => r.CreateWatchlistAsync(name), Times.Once);
    }

    [Fact]
    public async Task CreateWatchlistAsync_ThrowsException_WithDuplicateName()
    {
        // Arrange
        var name = "Existing Watchlist";
        var existingEntity = new WatchlistEntity { Id = 1, Name = name };

        _mockRepository.Setup(r => r.GetWatchlistByNameAsync(name))
            .ReturnsAsync(existingEntity);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _watchlistService.CreateWatchlistAsync(name));
        
        Assert.Contains("already exists", exception.Message);
    }

    [Fact]
    public async Task CreateWatchlistAsync_ThrowsException_WithEmptyName()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _watchlistService.CreateWatchlistAsync(""));
        
        await Assert.ThrowsAsync<ArgumentException>(
            () => _watchlistService.CreateWatchlistAsync("   "));
    }

    [Fact]
    public async Task AddSymbolAsync_AddsSymbol_ToWatchlist()
    {
        // Arrange
        var watchlistId = 1;
        var symbol = "AAPL";

        _mockRepository.Setup(r => r.AddSymbolToWatchlistAsync(watchlistId, symbol))
            .ReturnsAsync(true);

        var quote = new StockQuote
        {
            Symbol = symbol,
            Price = 150.00m,
            Change = 2.50m,
            ChangePercent = 1.69m,
            Volume = 1000000,
            Timestamp = DateTime.Now
        };

        _mockMarketDataProvider.Setup(p => p.GetQuoteAsync(symbol))
            .ReturnsAsync(quote);

        // Act
        var result = await _watchlistService.AddSymbolAsync(watchlistId, symbol);

        // Assert
        Assert.True(result);
        _mockRepository.Verify(r => r.AddSymbolToWatchlistAsync(watchlistId, symbol), Times.Once);
    }

    [Fact]
    public async Task AddSymbolAsync_ValidatesSymbolFormat()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _watchlistService.AddSymbolAsync(1, ""));
        
        await Assert.ThrowsAsync<ArgumentException>(
            () => _watchlistService.AddSymbolAsync(1, "   "));
        
        await Assert.ThrowsAsync<ArgumentException>(
            () => _watchlistService.AddSymbolAsync(1, "TOOLONGSYMBOL"));
    }

    [Fact]
    public async Task RemoveSymbolAsync_RemovesSymbol_FromWatchlist()
    {
        // Arrange
        var watchlistId = 1;
        var symbol = "AAPL";

        _mockRepository.Setup(r => r.RemoveSymbolFromWatchlistAsync(watchlistId, symbol))
            .ReturnsAsync(true);

        // Act
        var result = await _watchlistService.RemoveSymbolAsync(watchlistId, symbol);

        // Assert
        Assert.True(result);
        _mockRepository.Verify(r => r.RemoveSymbolFromWatchlistAsync(watchlistId, symbol), Times.Once);
    }

    [Fact]
    public async Task DeleteWatchlistAsync_DeletesWatchlist()
    {
        // Arrange
        var watchlistId = 1;

        _mockRepository.Setup(r => r.DeleteWatchlistAsync(watchlistId))
            .ReturnsAsync(true);

        // Act
        var result = await _watchlistService.DeleteWatchlistAsync(watchlistId);

        // Assert
        Assert.True(result);
        _mockRepository.Verify(r => r.DeleteWatchlistAsync(watchlistId), Times.Once);
    }

    [Fact]
    public async Task SearchSymbolsAsync_ReturnsMatchingSymbols()
    {
        // Arrange
        var searchTerm = "APP";
        var entities = new List<WatchlistEntity>
        {
            new()
            {
                Id = 1,
                Name = "Tech Stocks",
                Items = new List<WatchlistItemEntity>
                {
                    new() { Symbol = "AAPL" },
                    new() { Symbol = "MSFT" },
                    new() { Symbol = "GOOG" }
                }
            }
        };

        _mockRepository.Setup(r => r.GetAllWatchlistsAsync())
            .ReturnsAsync(entities);

        var quote = new StockQuote
        {
            Symbol = "AAPL",
            Price = 150.00m,
            Change = 2.50m,
            ChangePercent = 1.69m,
            Volume = 1000000,
            Timestamp = DateTime.Now
        };

        _mockMarketDataProvider.Setup(p => p.GetQuoteAsync("AAPL"))
            .ReturnsAsync(quote);

        // Act
        var results = await _watchlistService.SearchSymbolsAsync(searchTerm);

        // Assert
        Assert.Single(results);
        var result = results.First();
        Assert.Equal("AAPL", result.Symbol);
        Assert.Equal("Tech Stocks", result.WatchlistName);
        Assert.Equal(150.00m, result.LastPrice);
    }

    [Fact]
    public async Task GetStatisticsAsync_ReturnsCorrectStatistics()
    {
        // Arrange
        var entities = new List<WatchlistEntity>
        {
            new()
            {
                Id = 1,
                Name = "Test Watchlist",
                Items = new List<WatchlistItemEntity>
                {
                    new() { Symbol = "WINNER" },
                    new() { Symbol = "LOSER" },
                    new() { Symbol = "HIGHVOL" }
                }
            }
        };

        _mockRepository.Setup(r => r.GetAllWatchlistsAsync())
            .ReturnsAsync(entities);

        // Setup different quotes for testing statistics
        _mockMarketDataProvider.Setup(p => p.GetQuoteAsync("WINNER"))
            .ReturnsAsync(new StockQuote { Symbol = "WINNER", ChangePercent = 5.0m, Volume = 500000 });
        
        _mockMarketDataProvider.Setup(p => p.GetQuoteAsync("LOSER"))
            .ReturnsAsync(new StockQuote { Symbol = "LOSER", ChangePercent = -3.0m, Volume = 300000 });
        
        _mockMarketDataProvider.Setup(p => p.GetQuoteAsync("HIGHVOL"))
            .ReturnsAsync(new StockQuote { Symbol = "HIGHVOL", ChangePercent = 1.0m, Volume = 2000000 });

        // Act
        var stats = await _watchlistService.GetStatisticsAsync();

        // Assert
        Assert.Equal(1, stats.TotalWatchlists);
        Assert.Equal(3, stats.TotalSymbols);
        Assert.Single(stats.TopGainers);
        Assert.Single(stats.TopLosers);
        Assert.Equal("WINNER", stats.TopGainers.First().Symbol);
        Assert.Equal("LOSER", stats.TopLosers.First().Symbol);
        Assert.Equal("HIGHVOL", stats.MostActive.First().Symbol);
    }

    [Fact]
    public async Task ReorderSymbolsAsync_UpdatesSymbolOrder()
    {
        // Arrange
        var watchlistId = 1;
        var orderedSymbols = new List<string> { "MSFT", "AAPL", "GOOG" };

        _mockRepository.Setup(r => r.ReorderWatchlistItemsAsync(
            watchlistId, 
            It.IsAny<List<(string symbol, int order)>>()))
            .ReturnsAsync(true);

        // Act
        var result = await _watchlistService.ReorderSymbolsAsync(watchlistId, orderedSymbols);

        // Assert
        Assert.True(result);
        _mockRepository.Verify(r => r.ReorderWatchlistItemsAsync(
            watchlistId,
            It.Is<List<(string symbol, int order)>>(items =>
                items.Count == 3 &&
                items[0].symbol == "MSFT" && items[0].order == 1 &&
                items[1].symbol == "AAPL" && items[1].order == 2 &&
                items[2].symbol == "GOOG" && items[2].order == 3)),
            Times.Once);
    }

    [Fact]
    public async Task GetWatchlistAsync_ReturnsCachedWatchlist_WhenAvailable()
    {
        // Arrange
        var watchlistId = 1;
        var entity = new WatchlistEntity
        {
            Id = watchlistId,
            Name = "Test Watchlist",
            Items = new List<WatchlistItemEntity>()
        };

        _mockRepository.Setup(r => r.GetWatchlistByIdAsync(watchlistId))
            .ReturnsAsync(entity);

        // First call should hit repository
        var firstResult = await _watchlistService.GetWatchlistAsync(watchlistId);
        
        // Act - Second call should use cache
        var secondResult = await _watchlistService.GetWatchlistAsync(watchlistId);

        // Assert
        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(firstResult.Id, secondResult.Id);
        
        // Repository should only be called once (first time)
        _mockRepository.Verify(r => r.GetWatchlistByIdAsync(watchlistId), Times.Once);
    }

    [Fact]
    public void WatchlistChanged_Event_IsFired_WhenWatchlistIsCreated()
    {
        // Arrange
        WatchlistChangedEventArgs? eventArgs = null;
        _watchlistService.WatchlistChanged += (sender, args) => eventArgs = args;

        var entity = new WatchlistEntity
        {
            Id = 1,
            Name = "Test Watchlist",
            Created = DateTime.Now,
            LastModified = DateTime.Now,
            Items = new List<WatchlistItemEntity>()
        };

        _mockRepository.Setup(r => r.GetWatchlistByNameAsync("Test Watchlist"))
            .ReturnsAsync((WatchlistEntity?)null);
        _mockRepository.Setup(r => r.CreateWatchlistAsync("Test Watchlist"))
            .ReturnsAsync(entity);

        // Act
        var task = _watchlistService.CreateWatchlistAsync("Test Watchlist");
        task.Wait();

        // Assert
        Assert.NotNull(eventArgs);
        Assert.Equal(WatchlistChangeType.Created, eventArgs.ChangeType);
        Assert.Equal("Test Watchlist", eventArgs.Watchlist.Name);
    }

    [Fact]
    public async Task RefreshAllDataAsync_RefreshesAllCachedWatchlists()
    {
        // Arrange
        var entity = new WatchlistEntity
        {
            Id = 1,
            Name = "Test Watchlist",
            Items = new List<WatchlistItemEntity>
            {
                new() { Symbol = "AAPL" }
            }
        };

        _mockRepository.Setup(r => r.GetWatchlistByIdAsync(1))
            .ReturnsAsync(entity);

        var quote = new StockQuote
        {
            Symbol = "AAPL",
            Price = 150.00m,
            Change = 2.50m,
            ChangePercent = 1.69m,
            Volume = 1000000,
            Timestamp = DateTime.Now
        };

        _mockMarketDataProvider.Setup(p => p.GetQuoteAsync("AAPL"))
            .ReturnsAsync(quote);

        // Load watchlist into cache first
        await _watchlistService.GetWatchlistAsync(1);

        // Act
        await _watchlistService.RefreshAllDataAsync();

        // Assert
        // Verify that quotes were fetched for refresh
        _mockMarketDataProvider.Verify(p => p.GetQuoteAsync("AAPL"), Times.AtLeast(2));
    }
}

/// <summary>
/// Test logger for capturing log output during tests
/// </summary>
public class TestLogger : IChartLogger
{
    public List<string> LogEntries { get; } = new();

    public void Trace(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"TRACE: {msg}");
    public void Debug(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"DEBUG: {msg}");
    public void Info(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"INFO: {msg}");
    public void Warn(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"WARN: {msg}");
    public void Error(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => LogEntries.Add($"ERROR: {msg}");
    public void Critical(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => LogEntries.Add($"CRITICAL: {msg}");
}
