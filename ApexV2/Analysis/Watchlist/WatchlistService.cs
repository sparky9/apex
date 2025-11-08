using ApexV2.Core.Database;
using ApexV2.Core.Database.Repositories;
using ApexV2.Charts.Export; // For IChartLogger
using ApexV2.Data.MarketData;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ApexV2.Analysis.Watchlist;

/// <summary>
/// Price update event arguments
/// </summary>
public class PriceUpdateEventArgs : EventArgs
{
    public string Symbol { get; set; } = string.Empty;
    public StockQuote Quote { get; set; } = new StockQuote();
}

/// <summary>
/// Core service for managing watchlists with real-time data integration
/// </summary>
public class WatchlistService
{
    private readonly IWatchlistRepository _repository;
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IChartLogger _logger;
    private readonly Dictionary<int, WatchlistViewModel> _watchlistCache;
    
    public event EventHandler<WatchlistChangedEventArgs>? WatchlistChanged;
    public event EventHandler<WatchlistItemUpdatedEventArgs>? WatchlistItemUpdated;

    public WatchlistService(
        IWatchlistRepository repository,
        IMarketDataProvider marketDataProvider,
        IChartLogger logger)
    {
        _repository = repository;
        _marketDataProvider = marketDataProvider;
        _logger = logger;
        _watchlistCache = new Dictionary<int, WatchlistViewModel>();
        
        // Subscribe to market data updates for real-time prices
        // TODO [REVIEWED]: Temporarily commented to fix compilation
        // _marketDataProvider.PriceUpdate += OnPriceUpdate;
    }

    /// <summary>
    /// Get all watchlists with current market data
    /// </summary>
    public async Task<List<WatchlistViewModel>> GetAllWatchlistsAsync()
    {
        try
        {
            var entities = await _repository.GetAllWatchlistsAsync();
            var viewModels = new List<WatchlistViewModel>();

            foreach (var entity in entities)
            {
                var viewModel = await ConvertToViewModelAsync(entity);
                _watchlistCache[entity.Id] = viewModel;
                viewModels.Add(viewModel);
            }

            _logger.Info($"Retrieved {viewModels.Count} watchlists");
            return viewModels;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error retrieving watchlists: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Get a specific watchlist by ID with current market data
    /// </summary>
    public async Task<WatchlistViewModel?> GetWatchlistAsync(int id)
    {
        try
        {
            if (_watchlistCache.TryGetValue(id, out var cached))
            {
                await RefreshWatchlistDataAsync(cached);
                return cached;
            }

            var entity = await _repository.GetWatchlistByIdAsync(id);
            if (entity == null) return null;

            var viewModel = await ConvertToViewModelAsync(entity);
            _watchlistCache[id] = viewModel;
            
            _logger.Info($"Retrieved watchlist '{viewModel.Name}' with {viewModel.Items.Count} symbols");
            return viewModel;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error retrieving watchlist {id}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Create a new watchlist
    /// </summary>
    public async Task<WatchlistViewModel> CreateWatchlistAsync(string name)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Watchlist name cannot be empty");

            // Check for duplicate names
            var existing = await _repository.GetWatchlistByNameAsync(name);
            if (existing != null)
                throw new InvalidOperationException($"Watchlist '{name}' already exists");

            var entity = await _repository.CreateWatchlistAsync(name.Trim());
            var viewModel = await ConvertToViewModelAsync(entity);
            
            _watchlistCache[entity.Id] = viewModel;
            OnWatchlistChanged(WatchlistChangeType.Created, viewModel);
            
            _logger.Info($"Created watchlist '{name}'");
            return viewModel;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error creating watchlist '{name}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Delete a watchlist
    /// </summary>
    public async Task<bool> DeleteWatchlistAsync(int id)
    {
        try
        {
            var watchlist = _watchlistCache.TryGetValue(id, out var cached) ? cached : null;
            var success = await _repository.DeleteWatchlistAsync(id);
            
            if (success)
            {
                _watchlistCache.Remove(id);
                if (watchlist != null)
                {
                    OnWatchlistChanged(WatchlistChangeType.Deleted, watchlist);
                }
                _logger.Info($"Deleted watchlist {id}");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error deleting watchlist {id}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Add a symbol to a watchlist
    /// </summary>
    public async Task<bool> AddSymbolAsync(int watchlistId, string symbol)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(symbol))
                throw new ArgumentException("Symbol cannot be empty");

            symbol = symbol.ToUpper().Trim();
            
            // Validate symbol format (basic check)
            if (!IsValidSymbol(symbol))
                throw new ArgumentException($"Invalid symbol format: {symbol}");

            var success = await _repository.AddSymbolToWatchlistAsync(watchlistId, symbol);
            
            if (success)
            {
                // Update cache if watchlist is loaded
                if (_watchlistCache.TryGetValue(watchlistId, out var watchlist))
                {
                    var quote = await GetQuoteAsync(symbol);
                    var item = new WatchlistItemViewModel
                    {
                        Symbol = symbol,
                        LastPrice = quote?.Price ?? 0,
                        Change = quote?.Change ?? 0,
                        ChangePercent = quote?.ChangePercent ?? 0,
                        Volume = quote?.Volume ?? 0,
                        LastUpdate = DateTime.Now
                    };
                    
                    watchlist.Items.Add(item);
                    OnWatchlistChanged(WatchlistChangeType.SymbolAdded, watchlist);
                }
                
                _logger.Info($"Added symbol '{symbol}' to watchlist {watchlistId}");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error adding symbol '{symbol}' to watchlist {watchlistId}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Remove a symbol from a watchlist
    /// </summary>
    public async Task<bool> RemoveSymbolAsync(int watchlistId, string symbol)
    {
        try
        {
            symbol = symbol.ToUpper().Trim();
            var success = await _repository.RemoveSymbolFromWatchlistAsync(watchlistId, symbol);
            
            if (success)
            {
                // Update cache if watchlist is loaded
                if (_watchlistCache.TryGetValue(watchlistId, out var watchlist))
                {
                    var item = watchlist.Items.FirstOrDefault(i => i.Symbol == symbol);
                    if (item != null)
                    {
                        watchlist.Items.Remove(item);
                        OnWatchlistChanged(WatchlistChangeType.SymbolRemoved, watchlist);
                    }
                }
                
                _logger.Info($"Removed symbol '{symbol}' from watchlist {watchlistId}");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error removing symbol '{symbol}' from watchlist {watchlistId}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Reorder symbols in a watchlist
    /// </summary>
    public async Task<bool> ReorderSymbolsAsync(int watchlistId, List<string> orderedSymbols)
    {
        try
        {
            var items = orderedSymbols.Select((symbol, index) => (symbol.ToUpper(), index + 1)).ToList();
            var success = await _repository.ReorderWatchlistItemsAsync(watchlistId, items);
            
            if (success && _watchlistCache.TryGetValue(watchlistId, out var watchlist))
            {
                // Update cache order
                var orderedItems = new List<WatchlistItemViewModel>();
                foreach (var symbol in orderedSymbols)
                {
                    var item = watchlist.Items.FirstOrDefault(i => i.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
                    if (item != null)
                    {
                        orderedItems.Add(item);
                    }
                }
                
                watchlist.Items.Clear();
                watchlist.Items.AddRange(orderedItems);
                OnWatchlistChanged(WatchlistChangeType.Reordered, watchlist);
                
                _logger.Info($"Reordered {orderedSymbols.Count} symbols in watchlist {watchlistId}");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error reordering watchlist {watchlistId}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Search for symbols across all watchlists
    /// </summary>
    public async Task<List<WatchlistSearchResult>> SearchSymbolsAsync(string searchTerm)
    {
        try
        {
            var results = new List<WatchlistSearchResult>();
            var watchlists = await GetAllWatchlistsAsync();
            
            foreach (var watchlist in watchlists)
            {
                var matchingItems = watchlist.Items
                    .Where(item => item.Symbol.Contains(searchTerm.ToUpper()))
                    .Select(item => new WatchlistSearchResult
                    {
                        WatchlistId = watchlist.Id,
                        WatchlistName = watchlist.Name,
                        Symbol = item.Symbol,
                        LastPrice = item.LastPrice,
                        Change = item.Change,
                        ChangePercent = item.ChangePercent
                    })
                    .ToList();
                    
                results.AddRange(matchingItems);
            }
            
            _logger.Info($"Search for '{searchTerm}' returned {results.Count} results");
            return results;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error searching for '{searchTerm}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Get watchlist statistics
    /// </summary>
    public async Task<WatchlistStatistics> GetStatisticsAsync()
    {
        try
        {
            var watchlists = await GetAllWatchlistsAsync();
            
            var stats = new WatchlistStatistics
            {
                TotalWatchlists = watchlists.Count,
                TotalSymbols = watchlists.Sum(w => w.Items.Count),
                TopGainers = new List<WatchlistItemViewModel>(),
                TopLosers = new List<WatchlistItemViewModel>(),
                MostActive = new List<WatchlistItemViewModel>()
            };

            // Aggregate all items
            var allItems = watchlists.SelectMany(w => w.Items).ToList();
            
            if (allItems.Any())
            {
                stats.TopGainers = allItems
                    .Where(i => i.ChangePercent > 0)
                    .OrderByDescending(i => i.ChangePercent)
                    .Take(10)
                    .ToList();
                    
                stats.TopLosers = allItems
                    .Where(i => i.ChangePercent < 0)
                    .OrderBy(i => i.ChangePercent)
                    .Take(10)
                    .ToList();
                    
                stats.MostActive = allItems
                    .OrderByDescending(i => i.Volume)
                    .Take(10)
                    .ToList();
            }
            
            return stats;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error calculating watchlist statistics: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Refresh market data for all cached watchlists
    /// </summary>
    public async Task RefreshAllDataAsync()
    {
        try
        {
            var refreshTasks = _watchlistCache.Values.Select(RefreshWatchlistDataAsync);
            await Task.WhenAll(refreshTasks);
            _logger.Info($"Refreshed data for {_watchlistCache.Count} watchlists");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error refreshing watchlist data: {ex.Message}");
            throw;
        }
    }

    #region Private Methods

    private async Task<WatchlistViewModel> ConvertToViewModelAsync(WatchlistEntity entity)
    {
        var viewModel = new WatchlistViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Created = entity.Created,
            LastModified = entity.LastModified,
            Items = new List<WatchlistItemViewModel>()
        };

        foreach (var item in entity.Items.OrderBy(i => i.SortOrder))
        {
            var quote = await GetQuoteAsync(item.Symbol);
            viewModel.Items.Add(new WatchlistItemViewModel
            {
                Symbol = item.Symbol,
                LastPrice = quote?.Price ?? 0,
                Change = quote?.Change ?? 0,
                ChangePercent = quote?.ChangePercent ?? 0,
                Volume = quote?.Volume ?? 0,
                LastUpdate = quote?.Timestamp ?? DateTime.Now
            });
        }

        return viewModel;
    }

    private async Task RefreshWatchlistDataAsync(WatchlistViewModel watchlist)
    {
        foreach (var item in watchlist.Items)
        {
            var quote = await GetQuoteAsync(item.Symbol);
            if (quote != null)
            {
                var oldPrice = item.LastPrice;
                item.UpdateFromQuote(quote);

                // Fire update event if price changed
                if (Math.Abs(oldPrice - item.LastPrice) > 0.001m)
                {
                    OnWatchlistItemUpdated(watchlist.Id, item);
                }
            }
        }
    }

    private async Task<StockQuote?> GetQuoteAsync(string symbol)
    {
        try
        {
            return await _marketDataProvider.GetQuoteAsync(symbol);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Failed to get quote for {symbol}: {ex.Message}");
            return null;
        }
    }

    private bool IsValidSymbol(string symbol)
    {
        // Basic symbol validation - can be enhanced
        return !string.IsNullOrWhiteSpace(symbol) &&
               symbol.Length >= 1 &&
               symbol.Length <= 10 &&
               symbol.All(c => char.IsLetterOrDigit(c) || c == '.' || c == '-');
    }

    private void OnPriceUpdate(object? sender, PriceUpdateEventArgs e)
    {
        // Update cached watchlist items with new prices
        foreach (var watchlist in _watchlistCache.Values)
        {
            var item = watchlist.Items.FirstOrDefault(i => i.Symbol == e.Symbol);
            if (item != null)
            {
                item.UpdateFromQuote(e.Quote);
                OnWatchlistItemUpdated(watchlist.Id, item);
            }
        }
    }

    private void OnWatchlistChanged(WatchlistChangeType changeType, WatchlistViewModel watchlist)
    {
        WatchlistChanged?.Invoke(this, new WatchlistChangedEventArgs(changeType, watchlist));
    }

    private void OnWatchlistItemUpdated(int watchlistId, WatchlistItemViewModel item)
    {
        WatchlistItemUpdated?.Invoke(this, new WatchlistItemUpdatedEventArgs(watchlistId, item));
    }

    #endregion
}
