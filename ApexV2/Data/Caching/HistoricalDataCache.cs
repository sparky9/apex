using System.Collections.Concurrent;
using ApexV2.Core.Database;
using ApexV2.Data.MarketData.Engine;
using System.Threading;
using Microsoft.EntityFrameworkCore;

namespace ApexV2.Data.Caching;

public record Bar(string Symbol, DateTime TimestampUtc, decimal Open, decimal High, decimal Low, decimal Close, long Volume, int IntervalSec);

public interface IHistoricalDataCache
{
    void AddQuote(Quote q);
    IReadOnlyList<Bar> GetRecent(string symbol, int max, TimeSpan? interval = null);
    IReadOnlyList<Bar> GetRange(string symbol, DateTime startUtc, DateTime endUtc, TimeSpan? interval = null);
    Task WarmLoadAsync(IEnumerable<string> symbols, TimeSpan? interval = null);
    Task FlushAsync();
    CacheMetrics GetMetrics();
}

public record CacheMetrics(int BarsInMemory, int PendingFlushCount, long TotalBarsFlushed, int FlushFailures, TimeSpan? LastFlushDuration, DateTime? LastFlushUtc);

internal class SymbolBarBuffer
{
    private readonly LinkedList<Bar> _bars = new();
    private readonly int _capacity;
    public SymbolBarBuffer(int capacity) { _capacity = capacity; }
    public void AddOrUpdate(Bar bar)
    {
        // If last bar has same timestamp+interval, update OHLC
        if (_bars.Last != null && _bars.Last.Value.TimestampUtc == bar.TimestampUtc && _bars.Last.Value.IntervalSec == bar.IntervalSec)
        {
            var prev = _bars.Last.Value;
            var merged = prev with
            {
                High = Math.Max(prev.High, bar.High),
                Low = Math.Min(prev.Low, bar.Low),
                Close = bar.Close,
                Volume = prev.Volume + bar.Volume
            };
            _bars.RemoveLast();
            _bars.AddLast(merged);
        }
        else
        {
            _bars.AddLast(bar);
            if (_bars.Count > _capacity)
                _bars.RemoveFirst();
        }
    }
    public IReadOnlyList<Bar> GetRecent(int max) => _bars.Reverse().Take(max).Reverse().ToList();
    public IReadOnlyList<Bar> GetRange(DateTime start, DateTime end)
        => _bars.Where(b => b.TimestampUtc >= start && b.TimestampUtc <= end).ToList();
    public int Count => _bars.Count;
}

public class HistoricalDataCache : IHistoricalDataCache, IDisposable
{
    private readonly ConcurrentDictionary<string, SymbolBarBuffer> _buffers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentQueue<IntradayPriceEntity> _pending = new();
    private readonly int _maxBarsPerSymbol;
    private readonly int _intervalSec;
    private readonly DatabaseService _dbService;
    private readonly System.Threading.Timer _flushTimer;
    private readonly int _flushIntervalMs;
    private readonly int _immediateFlushThreshold;

    private long _totalFlushed; private int _flushFailures; private TimeSpan? _lastFlushDuration; private DateTime? _lastFlushUtc;
    private int _isFlushing;

    public HistoricalDataCache(DatabaseService dbService, int maxBarsPerSymbol = 5000, int intervalSec = 60, int flushIntervalMs = 2000, int immediateFlushThreshold = 500)
    {
        _dbService = dbService;
        _maxBarsPerSymbol = maxBarsPerSymbol;
        _intervalSec = intervalSec;
        _flushIntervalMs = flushIntervalMs;
        _immediateFlushThreshold = immediateFlushThreshold;
        _flushTimer = new System.Threading.Timer(async _ => await FlushSafeAsync(), null, _flushIntervalMs, _flushIntervalMs);
    }

    public void AddQuote(Quote q)
    {
        var tsBucket = new DateTime(q.Timestamp.Year, q.Timestamp.Month, q.Timestamp.Day, q.Timestamp.Hour, q.Timestamp.Minute, 0, DateTimeKind.Utc);
        var bar = new Bar(q.Symbol, tsBucket, q.Open, q.High, q.Low, q.Last, q.Volume, _intervalSec);
        var buffer = _buffers.GetOrAdd(q.Symbol, _ => new SymbolBarBuffer(_maxBarsPerSymbol));
        buffer.AddOrUpdate(bar);
        _pending.Enqueue(new IntradayPriceEntity
        {
            Symbol = q.Symbol,
            Timestamp = tsBucket,
            IntervalSec = _intervalSec,
            Open = bar.Open,
            High = bar.High,
            Low = bar.Low,
            Close = bar.Close,
            Volume = bar.Volume,
            DataProvider = q.Provider
        });
        if (_pending.Count >= _immediateFlushThreshold)
            _ = FlushSafeAsync();
    }

    public IReadOnlyList<Bar> GetRecent(string symbol, int max, TimeSpan? interval = null)
    {
        if (_buffers.TryGetValue(symbol, out var buf))
            return buf.GetRecent(max);
        return Array.Empty<Bar>();
    }

    public IReadOnlyList<Bar> GetRange(string symbol, DateTime startUtc, DateTime endUtc, TimeSpan? interval = null)
    {
        if (_buffers.TryGetValue(symbol, out var buf))
            return buf.GetRange(startUtc, endUtc);
        return Array.Empty<Bar>();
    }

    public async Task WarmLoadAsync(IEnumerable<string> symbols, TimeSpan? interval = null)
    {
        try
        {
            using var ctx = new ApexDbContext(_dbService.GetDbContextOptions());
            foreach (var sym in symbols)
            {
                var bars = await ctx.IntradayPrices
                    .Where(p => p.Symbol == sym && p.IntervalSec == _intervalSec)
                    .OrderByDescending(p => p.Timestamp)
                    .Take(_maxBarsPerSymbol)
                    .ToListAsync();
                if (!bars.Any()) continue;
                var buffer = _buffers.GetOrAdd(sym, _ => new SymbolBarBuffer(_maxBarsPerSymbol));
                foreach (var b in bars.OrderBy(p => p.Timestamp))
                {
                    buffer.AddOrUpdate(new Bar(sym, b.Timestamp, b.Open, b.High, b.Low, b.Close, b.Volume, b.IntervalSec));
                }
            }
        }
        catch { /* log later */ }
    }

    public async Task FlushAsync()
    {
        if (Interlocked.CompareExchange(ref _isFlushing, 1, 0) != 0) return;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var batch = new List<IntradayPriceEntity>();
            while (_pending.TryDequeue(out var item))
            {
                batch.Add(item);
                if (batch.Count > 1000) break;
            }
            if (batch.Count == 0) return;
            using var ctx = new ApexDbContext(_dbService.GetDbContextOptions());
            foreach (var grp in batch.GroupBy(b => new { b.Symbol, b.Timestamp, b.IntervalSec }))
            {
                var last = grp.Last();
                // Upsert-like: try fetch existing
                var existing = await ctx.IntradayPrices.FirstOrDefaultAsync(p => p.Symbol == last.Symbol && p.Timestamp == last.Timestamp && p.IntervalSec == last.IntervalSec);
                if (existing == null)
                {
                    ctx.IntradayPrices.Add(last);
                }
                else
                {
                    existing.High = Math.Max(existing.High, last.High);
                    existing.Low = Math.Min(existing.Low, last.Low);
                    existing.Close = last.Close;
                    existing.Volume = last.Volume; // last is aggregated already
                }
            }
            await ctx.SaveChangesAsync();
            _totalFlushed += batch.Count;
            _lastFlushDuration = sw.Elapsed;
            _lastFlushUtc = DateTime.UtcNow;
        }
        catch
        {
            _flushFailures++;
        }
        finally
        {
            sw.Stop();
            Interlocked.Exchange(ref _isFlushing, 0);
        }
    }

    private Task FlushSafeAsync() => FlushAsync();

    public CacheMetrics GetMetrics() => new(
        BarsInMemory: _buffers.Sum(kv => kv.Value.Count),
        PendingFlushCount: _pending.Count,
        TotalBarsFlushed: _totalFlushed,
        FlushFailures: _flushFailures,
        LastFlushDuration: _lastFlushDuration,
        LastFlushUtc: _lastFlushUtc
    );

    public void Dispose()
    {
        _flushTimer.Dispose();
        _ = FlushSafeAsync();
    }
}
