using System;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Core.Database;
using ApexV2.Data.Caching;
using ApexV2.Data.MarketData.Engine;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ApexV2.Tests.DataCaching;

public class HistoricalDataCacheTests
{
    private DatabaseService CreateInMemoryDb()
    {
        // Use temp path file DB (sqlite) for realism
        var path = System.IO.Path.GetTempFileName();
        return new DatabaseService(path);
    }

    private Quote NewQuote(string symbol, decimal price, decimal open, decimal high, decimal low, long vol, DateTime? ts=null)
        => new(symbol, price, open, high, low, open, vol, (ts?? DateTime.UtcNow), "TestProvider");

    [Fact]
    public async Task AddQuote_CreatesAndUpdates_MinuteBar()
    {
        var db = CreateInMemoryDb();
        await db.InitializeDatabaseAsync();
        var cache = new HistoricalDataCache(db, maxBarsPerSymbol:100, intervalSec:60, flushIntervalMs:5000, immediateFlushThreshold:1000);
        // Align to exact minute to avoid flakiness when near minute boundary
        var now = DateTime.UtcNow;
        var t0 = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, DateTimeKind.Utc);
        cache.AddQuote(NewQuote("TEST.TO", 10m,10m,10m,10m,100,t0));
        cache.AddQuote(NewQuote("TEST.TO", 11m,11m,11m,11m,50,t0.AddSeconds(10))); // same minute -> update
        var recent = cache.GetRecent("TEST.TO", 5);
        recent.Should().HaveCount(1);
        var bar = recent[0];
        bar.High.Should().Be(11m);
        bar.Low.Should().Be(10m);
        bar.Volume.Should().Be(150);
    }

    [Fact]
    public async Task RingBuffer_Trims_At_Capacity()
    {
        var db = CreateInMemoryDb();
        await db.InitializeDatabaseAsync();
        var cache = new HistoricalDataCache(db, maxBarsPerSymbol:5, intervalSec:60, flushIntervalMs:5000, immediateFlushThreshold:1000);
        var baseTs = DateTime.UtcNow.AddMinutes(-10);
        for (int i=0;i<10;i++)
            cache.AddQuote(NewQuote("AAA.TO", 10+i,10+i,10+i,10+i,10, baseTs.AddMinutes(i)));
        cache.GetRecent("AAA.TO", 10).Should().HaveCount(5); // capacity
    }

    [Fact]
    public async Task Flush_Persists_Bars()
    {
        var db = CreateInMemoryDb();
        await db.InitializeDatabaseAsync();
        var cache = new HistoricalDataCache(db, maxBarsPerSymbol:100, intervalSec:60, flushIntervalMs:5000, immediateFlushThreshold:1000);
        cache.AddQuote(NewQuote("BBB.TO", 10m,10m,10m,10m,100));
        await cache.FlushAsync();
        using var ctx = new ApexDbContext(db.GetDbContextOptions());
        var stored = ctx.IntradayPrices.Where(p=>p.Symbol=="BBB.TO").ToList();
        stored.Should().HaveCount(1);
    }
}
