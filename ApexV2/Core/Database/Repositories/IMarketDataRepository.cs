using ApexV2.Core.Database;
using ApexV2.Data.MarketData;
using Microsoft.EntityFrameworkCore;

namespace ApexV2.Core.Database.Repositories;

/// <summary>
/// Repository interface for market data caching and retrieval
/// </summary>
public interface IMarketDataRepository
{
    Task CacheStockQuoteAsync(StockQuote quote, string provider);
    Task<StockQuoteEntity?> GetLatestQuoteAsync(string symbol);
    Task CacheHistoricalDataAsync(List<HistoricalPrice> prices, string symbol, string provider);
    Task<List<HistoricalPriceEntity>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate);
    Task CacheFundamentalsAsync(CompanyFundamentals fundamentals);
    Task<CompanyFundamentalsEntity?> GetFundamentalsAsync(string symbol);
    Task CacheNewsAsync(List<NewsItem> news);
    Task<List<NewsItemEntity>> GetNewsAsync(string? symbol = null, int maxItems = 50);
    Task CleanOldDataAsync(TimeSpan maxAge);
}

/// <summary>
/// Repository implementation for market data operations
/// </summary>
public class MarketDataRepository : IMarketDataRepository
{
    private readonly DatabaseService _databaseService;

    public MarketDataRepository(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task CacheStockQuoteAsync(StockQuote quote, string provider)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var entity = new StockQuoteEntity
        {
            Symbol = quote.Symbol,
            Price = quote.Price,
            Change = quote.Change,
            ChangePercent = quote.ChangePercent,
            Volume = quote.Volume,
            Timestamp = quote.Timestamp,
            Open = quote.Open,
            High = quote.High,
            Low = quote.Low,
            Close = quote.Close,
            DataProvider = provider
        };

        context.StockQuotes.Add(entity);
        await context.SaveChangesAsync();
    }

    public async Task<StockQuoteEntity?> GetLatestQuoteAsync(string symbol)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        return await context.StockQuotes
            .Where(q => q.Symbol == symbol)
            .OrderByDescending(q => q.Timestamp)
            .FirstOrDefaultAsync();
    }

    public async Task CacheHistoricalDataAsync(List<HistoricalPrice> prices, string symbol, string provider)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var entities = prices.Select(p => new HistoricalPriceEntity
        {
            Symbol = symbol,
            Date = p.Date,
            Open = p.Open,
            High = p.High,
            Low = p.Low,
            Close = p.Close,
            Volume = p.Volume,
            DataProvider = provider
        }).ToList();

        // Remove existing data for the same date range to avoid duplicates
        var startDate = prices.Min(p => p.Date);
        var endDate = prices.Max(p => p.Date);
        
        var existingData = await context.HistoricalPrices
            .Where(h => h.Symbol == symbol && h.Date >= startDate && h.Date <= endDate)
            .ToListAsync();
            
        context.HistoricalPrices.RemoveRange(existingData);
        context.HistoricalPrices.AddRange(entities);
        
        await context.SaveChangesAsync();
    }

    public async Task<List<HistoricalPriceEntity>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        return await context.HistoricalPrices
            .Where(h => h.Symbol == symbol && h.Date >= startDate && h.Date <= endDate)
            .OrderBy(h => h.Date)
            .ToListAsync();
    }

    public async Task CacheFundamentalsAsync(CompanyFundamentals fundamentals)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var existing = await context.CompanyFundamentals
            .FirstOrDefaultAsync(f => f.Symbol == fundamentals.Symbol);

        if (existing != null)
        {
            // Update existing record
            existing.CompanyName = fundamentals.CompanyName;
            existing.MarketCap = fundamentals.MarketCap;
            existing.PE_Ratio = fundamentals.PE_Ratio;
            existing.EPS = fundamentals.EPS;
            existing.DebtToEquity = fundamentals.DebtToEquity;
            existing.PriceToBook = fundamentals.PriceToBook;
            existing.ROE = fundamentals.ROE;
            existing.DividendYield = fundamentals.DividendYield;
            existing.Sector = fundamentals.Sector;
            existing.Industry = fundamentals.Industry;
            existing.LastUpdated = DateTime.Now;
        }
        else
        {
            // Create new record
            var entity = new CompanyFundamentalsEntity
            {
                Symbol = fundamentals.Symbol,
                CompanyName = fundamentals.CompanyName,
                MarketCap = fundamentals.MarketCap,
                PE_Ratio = fundamentals.PE_Ratio,
                EPS = fundamentals.EPS,
                DebtToEquity = fundamentals.DebtToEquity,
                PriceToBook = fundamentals.PriceToBook,
                ROE = fundamentals.ROE,
                DividendYield = fundamentals.DividendYield,
                Sector = fundamentals.Sector,
                Industry = fundamentals.Industry,
                LastUpdated = DateTime.Now
            };
            
            context.CompanyFundamentals.Add(entity);
        }

        await context.SaveChangesAsync();
    }

    public async Task<CompanyFundamentalsEntity?> GetFundamentalsAsync(string symbol)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        return await context.CompanyFundamentals
            .FirstOrDefaultAsync(f => f.Symbol == symbol);
    }

    public async Task CacheNewsAsync(List<NewsItem> news)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var entities = news.Select(n => new NewsItemEntity
        {
            Title = n.Title,
            Summary = n.Summary,
            Url = n.Url,
            PublishedDate = n.PublishedDate,
            Source = n.Source,
            Symbol = string.Empty // Will be set if stock-specific
        }).ToList();

        context.NewsItems.AddRange(entities);
        await context.SaveChangesAsync();
    }

    public async Task<List<NewsItemEntity>> GetNewsAsync(string? symbol = null, int maxItems = 50)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var query = context.NewsItems.AsQueryable();
        
        if (!string.IsNullOrEmpty(symbol))
        {
            query = query.Where(n => n.Symbol == symbol || n.Symbol == string.Empty);
        }

        return await query
            .OrderByDescending(n => n.PublishedDate)
            .Take(maxItems)
            .ToListAsync();
    }

    public async Task CleanOldDataAsync(TimeSpan maxAge)
    {
        using var context = new ApexDbContext(_databaseService.GetDbContextOptions());
        
        var cutoffDate = DateTime.Now - maxAge;

        // Clean old quotes (keep recent ones for real-time display)
        var oldQuotes = await context.StockQuotes
            .Where(q => q.Timestamp < cutoffDate)
            .ToListAsync();
        context.StockQuotes.RemoveRange(oldQuotes);

        // Clean old news
        var oldNews = await context.NewsItems
            .Where(n => n.PublishedDate < cutoffDate)
            .ToListAsync();
        context.NewsItems.RemoveRange(oldNews);

        await context.SaveChangesAsync();
    }
}