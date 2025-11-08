using ApexV2.Core.Database;
using Microsoft.EntityFrameworkCore;

namespace ApexV2.Core.Database.Repositories;

/// <summary>
/// Repository interface for watchlist operations
/// </summary>
public interface IWatchlistRepository
{
    Task<List<WatchlistEntity>> GetAllWatchlistsAsync();
    Task<WatchlistEntity?> GetWatchlistByIdAsync(int id);
    Task<WatchlistEntity?> GetWatchlistByNameAsync(string name);
    Task<WatchlistEntity> CreateWatchlistAsync(string name);
    Task<bool> DeleteWatchlistAsync(int id);
    Task<bool> AddSymbolToWatchlistAsync(int watchlistId, string symbol);
    Task<bool> RemoveSymbolFromWatchlistAsync(int watchlistId, string symbol);
    Task<bool> ReorderWatchlistItemsAsync(int watchlistId, List<(string symbol, int order)> items);
}

/// <summary>
/// Repository implementation for watchlist operations
/// </summary>
public class WatchlistRepository : IWatchlistRepository
{
    private readonly DatabaseService _databaseService;

    public WatchlistRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<WatchlistEntity>> GetAllWatchlistsAsync()
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        return await context.Watchlists
            .Include(w => w.Items)
            .OrderBy(w => w.Name)
            .ToListAsync();
    }

    public async Task<WatchlistEntity?> GetWatchlistByIdAsync(int id)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        return await context.Watchlists
            .Include(w => w.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(w => w.Id == id);
    }

    public async Task<WatchlistEntity?> GetWatchlistByNameAsync(string name)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        return await context.Watchlists
            .Include(w => w.Items.OrderBy(i => i.SortOrder))
            .FirstOrDefaultAsync(w => w.Name == name);
    }

    public async Task<WatchlistEntity> CreateWatchlistAsync(string name)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var watchlist = new WatchlistEntity
        {
            Name = name,
            Created = DateTime.Now,
            LastModified = DateTime.Now
        };

        context.Watchlists.Add(watchlist);
        await context.SaveChangesAsync();
        
        return watchlist;
    }

    public async Task<bool> DeleteWatchlistAsync(int id)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var watchlist = await context.Watchlists
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == id);
            
        if (watchlist == null) return false;

        context.Watchlists.Remove(watchlist);
        await context.SaveChangesAsync();
        
        return true;
    }

    public async Task<bool> AddSymbolToWatchlistAsync(int watchlistId, string symbol)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var watchlist = await context.Watchlists
            .Include(w => w.Items)
            .FirstOrDefaultAsync(w => w.Id == watchlistId);
            
        if (watchlist == null) return false;

        // Check if symbol already exists
        if (watchlist.Items.Any(i => i.Symbol == symbol)) return false;

        var maxOrder = watchlist.Items.Any() ? watchlist.Items.Max(i => i.SortOrder) : 0;
        
        var item = new WatchlistItemEntity
        {
            WatchlistId = watchlistId,
            Symbol = symbol.ToUpper(),
            SortOrder = maxOrder + 1,
            Added = DateTime.Now
        };

        watchlist.Items.Add(item);
        watchlist.LastModified = DateTime.Now;
        
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveSymbolFromWatchlistAsync(int watchlistId, string symbol)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var item = await context.WatchlistItems
            .FirstOrDefaultAsync(i => i.WatchlistId == watchlistId && i.Symbol == symbol);
            
        if (item == null) return false;

        context.WatchlistItems.Remove(item);
        
        // Update parent watchlist timestamp
        var watchlist = await context.Watchlists.FindAsync(watchlistId);
        if (watchlist != null)
        {
            watchlist.LastModified = DateTime.Now;
        }
        
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ReorderWatchlistItemsAsync(int watchlistId, List<(string symbol, int order)> items)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var watchlistItems = await context.WatchlistItems
            .Where(i => i.WatchlistId == watchlistId)
            .ToListAsync();

        foreach (var (symbol, order) in items)
        {
            var item = watchlistItems.FirstOrDefault(i => i.Symbol == symbol);
            if (item != null)
            {
                item.SortOrder = order;
            }
        }

        // Update parent watchlist timestamp
        var watchlist = await context.Watchlists.FindAsync(watchlistId);
        if (watchlist != null)
        {
            watchlist.LastModified = DateTime.Now;
        }

        await context.SaveChangesAsync();
        return true;
    }
}