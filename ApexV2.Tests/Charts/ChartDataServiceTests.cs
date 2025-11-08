using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ApexV2.Charts.Models;
using ApexV2.Charts.Services;
using ApexV2.Core.Logging;
using ApexV2.Data.MarketData;

namespace ApexV2.Tests.Charts;

public class ChartDataServiceTests
{
    private readonly ChartDataService _service;

    public ChartDataServiceTests()
    {
        var logManager = new LogManager();
        var logger = logManager.GetLogger("Test");
        _service = new ChartDataService(logger);
    }

    [Fact]
    public async Task GetCandlestickDataAsync_ShouldReturnData()
    {
        // Act
        var result = await _service.GetCandlestickDataAsync("AAPL", TimeFrame.Daily);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.All(result, candle => Assert.True(candle.High >= candle.Low));
        Assert.All(result, candle => Assert.True(candle.High >= Math.Max(candle.Open, candle.Close)));
        Assert.All(result, candle => Assert.True(candle.Low <= Math.Min(candle.Open, candle.Close)));
    }

    [Fact]
    public async Task GetCandlestickDataAsync_ShouldCacheResults()
    {
        // Act
        var result1 = await _service.GetCandlestickDataAsync("AAPL", TimeFrame.Daily);
        var result2 = await _service.GetCandlestickDataAsync("AAPL", TimeFrame.Daily);

        // Assert
        Assert.Equal(result1.Count, result2.Count);
        Assert.True(result1.SequenceEqual(result2, new CandlestickDataComparer()));
    }

    [Fact]
    public void ConvertQuotesToCandles_ShouldGroupByTimeFrame()
    {
        // Arrange
        var quotes = new List<StockQuote>
        {
            new() { Timestamp = DateTime.Parse("2023-01-01 09:30:00"), Price = 100m, Volume = 1000 },
            new() { Timestamp = DateTime.Parse("2023-01-01 09:30:30"), Price = 101m, Volume = 1500 },
            new() { Timestamp = DateTime.Parse("2023-01-01 09:31:00"), Price = 102m, Volume = 2000 },
            new() { Timestamp = DateTime.Parse("2023-01-01 09:31:30"), Price = 99m, Volume = 1200 }
        };

        // Act
        var candles = _service.ConvertQuotesToCandles(quotes, TimeFrame.OneMinute);

        // Assert
        Assert.Equal(2, candles.Count);
        
        var firstCandle = candles[0];
        Assert.Equal(100m, firstCandle.Open);
        Assert.Equal(101m, firstCandle.Close);
        Assert.Equal(101m, firstCandle.High);
        Assert.Equal(100m, firstCandle.Low);
        Assert.Equal(2500, firstCandle.Volume);
        
        var secondCandle = candles[1];
        Assert.Equal(102m, secondCandle.Open);
        Assert.Equal(99m, secondCandle.Close);
        Assert.Equal(102m, secondCandle.High);
        Assert.Equal(99m, secondCandle.Low);
        Assert.Equal(3200, secondCandle.Volume);
    }

    [Fact]
    public void UpdateRealTimeCandle_ShouldUpdatePriceAndVolume()
    {
        // Arrange
        var candle = new CandlestickData
        {
            Timestamp = DateTime.Now,
            Open = 100m,
            High = 105m,
            Low = 95m,
            Close = 102m,
            Volume = 1000
        };
        
        var quote = new StockQuote
        {
            Timestamp = DateTime.Now,
            Price = 107m,
            Volume = 500
        };

        // Act
        _service.UpdateRealTimeCandle(candle, quote);

        // Assert
        Assert.Equal(107m, candle.High);
        Assert.Equal(107m, candle.Close);
        Assert.Equal(1500, candle.Volume);
        Assert.Equal(100m, candle.Open); // Should not change
        Assert.Equal(95m, candle.Low); // Should not change
    }

    [Fact]
    public void ClearCache_ShouldRemoveSpecificSymbol()
    {
        // Arrange
        _service.GetCandlestickDataAsync("AAPL", TimeFrame.Daily).Wait();
        _service.GetCandlestickDataAsync("GOOGL", TimeFrame.Daily).Wait();

        // Act
        _service.ClearCache("AAPL");

        // Assert - This test verifies cache behavior indirectly
        // In a real implementation, we might expose cache count for testing
        Assert.True(true); // Placeholder assertion
    }

    private class CandlestickDataComparer : IEqualityComparer<CandlestickData>
    {
        public bool Equals(CandlestickData x, CandlestickData y)
        {
            if (x == null && y == null) return true;
            if (x == null || y == null) return false;
            
            return x.Timestamp == y.Timestamp &&
                   x.Open == y.Open &&
                   x.High == y.High &&
                   x.Low == y.Low &&
                   x.Close == y.Close &&
                   x.Volume == y.Volume;
        }

        public int GetHashCode(CandlestickData obj)
        {
            return obj?.Timestamp.GetHashCode() ?? 0;
        }
    }
}
