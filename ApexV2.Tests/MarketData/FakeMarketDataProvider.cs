using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ApexV2.Data.MarketData;

namespace ApexV2.Tests.MarketData;

internal class FakeMarketDataProvider : IMarketDataProvider
{
    private readonly object _lock = new();
    private readonly Dictionary<string, decimal> _prices = new(StringComparer.OrdinalIgnoreCase);
    private int _callCount;
    public string ProviderName => "Fake";
    public bool IsConnected { get; private set; }
    public bool SupportsStreaming => false;
    public bool RequiresApiKey => false;
    public bool SupportsCanadianSymbols => true;
    public int CallCount => _callCount;

    public event EventHandler<PriceUpdateEventArgs>? PriceUpdate;

    public Task<bool> ConnectAsync() { IsConnected = true; return Task.FromResult(true); }
    public Task DisconnectAsync() { IsConnected = false; return Task.CompletedTask; }

    public void SetPrice(string symbol, decimal price)
    {
        lock (_lock) _prices[symbol] = price;
    }

    public Task<StockQuote> GetQuoteAsync(string symbol)
    {
        Interlocked.Increment(ref _callCount);
        lock (_lock)
        {
            if (!_prices.TryGetValue(symbol, out var p)) p = 100m;
            return Task.FromResult(new StockQuote
            {
                Symbol = symbol,
                Price = p,
                Open = p - 1,
                High = p + 1,
                Low = p - 2,
                Close = p - 0.5m,
                Volume = 1000,
                Timestamp = DateTime.UtcNow
            });
        }
    }

    public async Task<List<StockQuote>> GetQuotesAsync(List<string> symbols)
    {
        var list = new List<StockQuote>();
        foreach (var s in symbols)
            list.Add(await GetQuoteAsync(s));
        return list;
    }

    public async Task<StockQuote> GetCurrentQuoteAsync(string symbol)
    {
        return await GetQuoteAsync(symbol);
    }

    public Task<List<HistoricalPrice>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate) => Task.FromResult(new List<HistoricalPrice>());
    public Task<List<StockQuote>> SearchSymbolsAsync(string query) => Task.FromResult(new List<StockQuote>());
    public Task<CompanyFundamentals> GetFundamentalsAsync(string symbol) => Task.FromResult(new CompanyFundamentals { Symbol = symbol });
    public Task<List<NewsItem>> GetNewsAsync(string? symbol = null) => Task.FromResult(new List<NewsItem>());
}
