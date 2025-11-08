using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Charts.Models;
using ApexV2.Core.Logging;
using ApexV2.Data.MarketData;

namespace ApexV2.Charts.Services;

/// <summary>
/// Service for converting market data to chart data
/// </summary>
public class ChartDataService
{
    private readonly Logger _logger;
    private readonly Dictionary<string, List<CandlestickData>> _cache = new();

    public ChartDataService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Converts market data quotes to candlestick data
    /// </summary>
    public async Task<List<CandlestickData>> GetCandlestickDataAsync(
        string symbol, 
        TimeFrame timeFrame, 
        DateTime? startTime = null, 
        DateTime? endTime = null)
    {
        try
        {
            var cacheKey = $"{symbol}_{timeFrame}_{startTime}_{endTime}";
            
            if (_cache.TryGetValue(cacheKey, out var cachedData))
            {
                _logger.Debug($"Returning cached chart data for {symbol}");
                return cachedData;
            }

            // TODO [REVIEWED]: Integrate with actual market data service
            // For now, generate sample data
            var data = GenerateSampleData(symbol, timeFrame, startTime, endTime);
            
            _cache[cacheKey] = data;
            
            _logger.Info($"Generated chart data for {symbol}: {data.Count} candles");
            return data;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to get chart data for {symbol}: {ex.Message}", ex);
            throw;
        }
    }

    /// <summary>
    /// Converts individual quotes to candlestick data by timeframe
    /// </summary>
    public List<CandlestickData> ConvertQuotesToCandles(
        IEnumerable<StockQuote> quotes, 
        TimeFrame timeFrame)
    {
        if (quotes == null || !quotes.Any())
            return new List<CandlestickData>();

        var sortedQuotes = quotes.OrderBy(q => q.Timestamp).ToList();
        var candles = new List<CandlestickData>();

        var timeSpan = GetTimeSpanForTimeFrame(timeFrame);
        if (timeSpan == TimeSpan.Zero)
        {
            // For tick data, create one candle per quote
            return sortedQuotes.Select(q => new CandlestickData
            {
                Timestamp = q.Timestamp,
                Open = q.Price,
                High = q.Price,
                Low = q.Price,
                Close = q.Price,
                Volume = (long)q.Volume
            }).ToList();
        }

        var currentCandleStart = FloorToTimeFrame(sortedQuotes.First().Timestamp, timeFrame);
        var currentCandle = new CandlestickData { Timestamp = currentCandleStart };
        var quotesInCandle = new List<StockQuote>();

        foreach (var quote in sortedQuotes)
        {
            var candleStart = FloorToTimeFrame(quote.Timestamp, timeFrame);
            
            if (candleStart != currentCandleStart)
            {
                // Finalize current candle
                if (quotesInCandle.Any())
                {
                    FinalizeCandleFromQuotes(currentCandle, quotesInCandle);
                    candles.Add(currentCandle);
                }
                
                // Start new candle
                currentCandleStart = candleStart;
                currentCandle = new CandlestickData { Timestamp = currentCandleStart };
                quotesInCandle.Clear();
            }
            
            quotesInCandle.Add(quote);
        }

        // Finalize last candle
        if (quotesInCandle.Any())
        {
            FinalizeCandleFromQuotes(currentCandle, quotesInCandle);
            candles.Add(currentCandle);
        }

        _logger.Debug($"Converted {sortedQuotes.Count} quotes to {candles.Count} candles for timeframe {timeFrame}");
        return candles;
    }

    /// <summary>
    /// Updates real-time candle with new quote
    /// </summary>
    public void UpdateRealTimeCandle(CandlestickData candle, StockQuote quote)
    {
        if (candle == null || quote == null) return;

        // Update high and low
        if (quote.Price > candle.High)
            candle.High = quote.Price;
        if (quote.Price < candle.Low)
            candle.Low = quote.Price;
        
        // Update close and volume
        candle.Close = quote.Price;
        candle.Volume += (long)quote.Volume;
        
        _logger.Debug($"Updated real-time candle: {candle}");
    }

    /// <summary>
    /// Clears cached data for a symbol
    /// </summary>
    public void ClearCache(string? symbol = null)
    {
        if (string.IsNullOrEmpty(symbol))
        {
            _cache.Clear();
            _logger.Info("Cleared all chart data cache");
        }
        else
        {
            var keysToRemove = _cache.Keys.Where(k => k.StartsWith(symbol)).ToList();
            foreach (var key in keysToRemove)
            {
                _cache.Remove(key);
            }
            _logger.Info($"Cleared chart data cache for {symbol}");
        }
    }

    private void FinalizeCandleFromQuotes(CandlestickData candle, List<StockQuote> quotes)
    {
        if (!quotes.Any()) return;

        var sortedQuotes = quotes.OrderBy(q => q.Timestamp).ToList();
        
        candle.Open = sortedQuotes.First().Price;
        candle.Close = sortedQuotes.Last().Price;
        candle.High = sortedQuotes.Max(q => q.Price);
        candle.Low = sortedQuotes.Min(q => q.Price);
        candle.Volume = sortedQuotes.Sum(q => (long)q.Volume);
    }

    private DateTime FloorToTimeFrame(DateTime timestamp, TimeFrame timeFrame)
    {
        return timeFrame switch
        {
            TimeFrame.OneMinute => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                timestamp.Hour, timestamp.Minute, 0),
            TimeFrame.FiveMinutes => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                timestamp.Hour, timestamp.Minute / 5 * 5, 0),
            TimeFrame.FifteenMinutes => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                timestamp.Hour, timestamp.Minute / 15 * 15, 0),
            TimeFrame.ThirtyMinutes => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                timestamp.Hour, timestamp.Minute / 30 * 30, 0),
            TimeFrame.Hourly => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day, 
                timestamp.Hour, 0, 0),
            TimeFrame.Daily => new DateTime(timestamp.Year, timestamp.Month, timestamp.Day),
            TimeFrame.Weekly => timestamp.StartOfWeek(),
            TimeFrame.Monthly => new DateTime(timestamp.Year, timestamp.Month, 1),
            _ => timestamp
        };
    }

    private TimeSpan GetTimeSpanForTimeFrame(TimeFrame timeFrame)
    {
        return timeFrame switch
        {
            TimeFrame.Tick => TimeSpan.Zero,
            TimeFrame.OneMinute => TimeSpan.FromMinutes(1),
            TimeFrame.FiveMinutes => TimeSpan.FromMinutes(5),
            TimeFrame.FifteenMinutes => TimeSpan.FromMinutes(15),
            TimeFrame.ThirtyMinutes => TimeSpan.FromMinutes(30),
            TimeFrame.Hourly => TimeSpan.FromHours(1),
            TimeFrame.Daily => TimeSpan.FromDays(1),
            TimeFrame.Weekly => TimeSpan.FromDays(7),
            TimeFrame.Monthly => TimeSpan.FromDays(30),
            _ => TimeSpan.Zero
        };
    }

    private List<CandlestickData> GenerateSampleData(
        string symbol, 
        TimeFrame timeFrame, 
        DateTime? startTime, 
        DateTime? endTime)
    {
        var start = startTime ?? DateTime.Now.AddDays(-30);
        var end = endTime ?? DateTime.Now;
        var data = new List<CandlestickData>();
        var random = new Random(symbol.GetHashCode()); // Consistent data per symbol
        
        var basePrice = 100m + (decimal)(symbol.GetHashCode() % 200);
        var current = start;
        var timeSpan = GetTimeSpanForTimeFrame(timeFrame);
        
        if (timeSpan == TimeSpan.Zero)
            timeSpan = TimeSpan.FromMinutes(1); // Default for sample data

        while (current <= end)
        {
            var open = basePrice + (decimal)(random.NextDouble() - 0.5) * basePrice * 0.02m;
            var direction = random.NextDouble() - 0.5;
            var volatility = (decimal)random.NextDouble() * 0.03m;
            
            var close = open + (decimal)direction * basePrice * volatility;
            var high = Math.Max(open, close) + (decimal)random.NextDouble() * basePrice * 0.01m;
            var low = Math.Min(open, close) - (decimal)random.NextDouble() * basePrice * 0.01m;
            var volume = random.Next(10000, 1000000);
            
            data.Add(new CandlestickData
            {
                Timestamp = current,
                Open = Math.Round(open, 2),
                High = Math.Round(high, 2),
                Low = Math.Round(low, 2),
                Close = Math.Round(close, 2),
                Volume = volume
            });
            
            basePrice = close; // Trend continuation
            current += timeSpan;
        }
        
        return data;
    }
}

/// <summary>
/// Extension methods for DateTime
/// </summary>
public static class DateTimeExtensions
{
    public static DateTime StartOfWeek(this DateTime dt, DayOfWeek startOfWeek = DayOfWeek.Monday)
    {
        int diff = (7 + (dt.DayOfWeek - startOfWeek)) % 7;
        return dt.AddDays(-1 * diff).Date;
    }
}
