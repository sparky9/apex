using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApexV2.Data.MarketData;

/// <summary>
/// Interface for all market data providers (Alpha Vantage, Yahoo Finance, IEX, etc.)
/// </summary>
public interface IMarketDataProvider
{
    string ProviderName { get; }
    bool IsConnected { get; }
    bool SupportsStreaming => false; // added default
    bool RequiresApiKey => false; // added default
    bool SupportsCanadianSymbols => true; // added assumption

    // Events
    event EventHandler<PriceUpdateEventArgs>? PriceUpdate;

    Task<bool> ConnectAsync();
    Task DisconnectAsync();

    Task<StockQuote> GetQuoteAsync(string symbol);
    Task<StockQuote> GetCurrentQuoteAsync(string symbol); // Alias for GetQuoteAsync
    Task<List<StockQuote>> GetQuotesAsync(List<string> symbols);
    // added streamlined convenience wrappers
    async Task<StockQuote?> TryGetQuoteAsync(string symbol)
    {
        try { return await GetQuoteAsync(symbol); } catch { return null; }
    }
    async Task<IReadOnlyList<StockQuote>> TryGetQuotesAsync(IEnumerable<string> symbols)
    {
        var list = new List<StockQuote>();
        foreach (var s in symbols)
        {
            var q = await TryGetQuoteAsync(s);
            if (q != null) list.Add(q);
        }
        return list;
    }

    Task<List<HistoricalPrice>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate);
    Task<List<StockQuote>> SearchSymbolsAsync(string query);

    Task<CompanyFundamentals> GetFundamentalsAsync(string symbol);
    Task<List<NewsItem>> GetNewsAsync(string? symbol = null);
}

public class StockQuote
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public string Source { get; set; } = string.Empty;
}

public class HistoricalPrice
{
    public string Symbol { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

public class CompanyFundamentals
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal MarketCap { get; set; }
    public decimal PE_Ratio { get; set; }
    public decimal PeRatio { get; set; } // Alias for PE_Ratio
    public decimal EPS { get; set; }
    public decimal EpsRatio { get; set; } // Alias for EPS
    public decimal DebtToEquity { get; set; }
    public decimal DebtToEquityRatio { get; set; } // Alias for DebtToEquity
    public decimal PriceToBook { get; set; }
    public decimal ROE { get; set; }
    public decimal DividendYield { get; set; }
    public string Sector { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = string.Empty;
}

public class NewsItem
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty; // Added missing property
    public string Url { get; set; } = string.Empty;
    public DateTime PublishedAt { get; set; }
    public DateTime PublishedDate { get; set; } // Added missing property
    public string Source { get; set; } = string.Empty;
    public List<string> RelatedSymbols { get; set; } = new();
}

/// <summary>
/// Event args for real-time price updates
/// </summary>
public class PriceUpdateEventArgs : EventArgs
{
    public string Symbol { get; }
    public StockQuote Quote { get; }

    public PriceUpdateEventArgs(string symbol, StockQuote quote)
    {
        Symbol = symbol;
        Quote = quote;
    }
}