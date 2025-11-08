using ApexV2.Analysis.Watchlist;
using ApexV2.Core.Database;
using ApexV2.Core.Database.Repositories;
using ApexV2.Charts.Export; // For IChartLogger
using ApexV2.Data.MarketData;
using System;

namespace ApexV2.Core;

/// <summary>
/// Simple service locator for dependency injection
/// This will be replaced with a proper DI container in the future
/// </summary>
public static class ServiceLocator
{
    private static DatabaseService? _databaseService;
    private static IChartLogger? _logger;
    private static IMarketDataProvider? _marketDataProvider;
    private static IWatchlistRepository? _watchlistRepository;
    private static WatchlistService? _watchlistService;
    private static WatchlistAnalysisService? _watchlistAnalysisService;

    public static void Initialize(DatabaseService databaseService, IChartLogger logger, IMarketDataProvider marketDataProvider)
    {
        _databaseService = databaseService;
        _logger = logger;
        _marketDataProvider = marketDataProvider;

        // Initialize repositories
        _watchlistRepository = new WatchlistRepository(_databaseService);

        // Initialize services
        _watchlistService = new WatchlistService(_watchlistRepository, _marketDataProvider, _logger);
        _watchlistAnalysisService = new WatchlistAnalysisService(_watchlistService);
    }

    public static WatchlistService GetWatchlistService()
    {
        return _watchlistService ?? throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
    }

    public static WatchlistAnalysisService GetWatchlistAnalysisService()
    {
        return _watchlistAnalysisService ?? throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
    }

    public static IWatchlistRepository GetWatchlistRepository()
    {
        return _watchlistRepository ?? throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
    }

    public static DatabaseService GetDatabaseService()
    {
        return _databaseService ?? throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
    }

    public static IChartLogger GetLogger()
    {
        return _logger ?? throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
    }

    public static IMarketDataProvider GetMarketDataProvider()
    {
        return _marketDataProvider ?? throw new InvalidOperationException("ServiceLocator not initialized. Call Initialize() first.");
    }
}
