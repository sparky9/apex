using System.Collections.Concurrent;
using System.Threading.Channels;
using ApexV2.Core.Logging;
using ApexV2.Data.DataValidation;
using ApexV2.Data.Caching;

namespace ApexV2.Data.MarketData.Engine;

public static class MarketSymbolUtil
{
    internal static string Normalize(string raw)
    {
        var s = raw.Trim().ToUpperInvariant();
        if (!s.EndsWith(".TO") && s.Length <= 5)
            s += ".TO";
        return s;
    }
}

public class MarketDataEngine
{
    private readonly IMarketDataProvider _provider;
    private readonly Logger _logger;
    private readonly ConcurrentDictionary<string, SubscriptionInfo> _subs = new(StringComparer.OrdinalIgnoreCase);
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _fetchGate = new(1,1);
    private readonly Channel<QuoteUpdate> _updateChannel = Channel.CreateUnbounded<QuoteUpdate>();
    private readonly MarketDataEngineOptions _options;
    private readonly IHistoricalDataCache? _historyCache; // optional

    // Rate limiting
    private readonly ConcurrentQueue<DateTime> _callTimestamps = new();

    // Metrics & health
    private int _successCount; private int _failureCount; private int _transientFailureCount; private int _circuitOpenCount;
    private DateTime? _lastSuccessUtc; private DateTime? _lastFailureUtc;

    // Circuit breaker
    private int _consecutiveFailures;
    private DateTime? _circuitOpenedUtc;

    // In-memory cache
    private readonly ConcurrentDictionary<string, Quote> _quoteCache = new(StringComparer.OrdinalIgnoreCase);

    private readonly IQuoteValidator _quoteValidator; // created in ctor now due to options dependency
    private Task? _loopTask;

    // Validation metrics
    private int _validatedCount;
    private int _correctedCount;
    private int _rejectedCount;

    private class SubscriptionInfo
    {
        public int RefCount;
        public Quote? LastPublished;
        public DateTime LastFetch;
    }

    public MarketDataEngine(IMarketDataProvider provider, LogManager logManager, TimeSpan? pollInterval = null)
        : this(provider, logManager, new MarketDataEngineOptions { PollInterval = pollInterval ?? TimeSpan.FromSeconds(10) }) { }

    public MarketDataEngine(IMarketDataProvider provider, LogManager logManager, MarketDataEngineOptions options, IHistoricalDataCache? historyCache = null)
    {
        _provider = provider;
        _logger = logManager.GetLogger("MarketDataEngine");
        _options = options;
        _quoteValidator = QuoteValidatorFactory.CreateDefaultComposite();
        _historyCache = historyCache;
    }

    public ProviderCapabilities Capabilities => new(_provider.SupportsStreaming, _provider.RequiresApiKey, _provider.SupportsCanadianSymbols);

    public ProviderMetrics GetMetrics() => new(
        _provider.ProviderName,
        _successCount,
        _failureCount,
        _transientFailureCount,
        _circuitOpenCount,
        _lastSuccessUtc,
        _lastFailureUtc,
        EvaluateHealth()
    );

    private ProviderHealthStatus EvaluateHealth()
    {
        if (_circuitOpenedUtc.HasValue && DateTime.UtcNow - _circuitOpenedUtc < _options.CircuitBreakerOpenDuration)
            return ProviderHealthStatus.CircuitOpen;
        if (_failureCount > 0 && _successCount == 0) return ProviderHealthStatus.Unavailable;
        if (_transientFailureCount > _successCount / 2) return ProviderHealthStatus.Degraded;
        return ProviderHealthStatus.Healthy;
    }

    public async Task StartAsync()
    {
        if (!_provider.IsConnected)
        {
            await _provider.ConnectAsync();
            _logger.Info("Provider connected", new Dictionary<string, object?>{{"provider", _provider.ProviderName}});
        }
        _loopTask = Task.Run(PollLoopAsync);
    }

    public async Task StopAsync()
    {
        try { _cts.Cancel(); } catch { }
        if (_loopTask != null) await Task.WhenAny(_loopTask, Task.Delay(2000));
        // Persist cache if enabled
        if (_options.PersistLastQuotesOnShutdown)
        {
            try { await PersistCacheAsync(); } catch (Exception ex) { _logger.Warn("Persist cache failed", new(){{"err", ex.Message}}); }
        }
        try { await _provider.DisconnectAsync(); } catch { }
        _logger.Info("Engine stopped");
    }

    public int Subscribe(string symbol)
    {
        var norm = NormalizeSymbol(symbol);
        var info = _subs.AddOrUpdate(norm, _ => new SubscriptionInfo{ RefCount = 1 }, (_, existing) => { existing.RefCount++; return existing; });
        _logger.Debug($"Subscribed {norm} ref={info.RefCount}");
        return info.RefCount;
    }

    public int Unsubscribe(string symbol)
    {
        var norm = NormalizeSymbol(symbol);
        if (_subs.TryGetValue(norm, out var info))
        {
            info.RefCount--;
            if (info.RefCount <= 0)
            {
                _subs.TryRemove(norm, out _);
                _quoteCache.TryRemove(norm, out _);
                _logger.Debug($"Unsubscribed {norm} removed");
                return 0;
            }
            _logger.Debug($"Unsubscribed {norm} ref={info.RefCount}");
            return info.RefCount;
        }
        return 0;
    }

    public IAsyncEnumerable<QuoteUpdate> GetUpdatesAsync(CancellationToken ct = default) => ReadUpdatesAsync(ct);

    public Quote? TryGetCachedQuote(string symbol)
    {
        _quoteCache.TryGetValue(NormalizeSymbol(symbol), out var q); return q;
    }

    private async IAsyncEnumerable<QuoteUpdate> ReadUpdatesAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        while (await _updateChannel.Reader.WaitToReadAsync(ct))
        {
            while (_updateChannel.Reader.TryRead(out var upd))
            {
                yield return upd;
            }
        }
    }

    private TimeSpan _currentPollInterval => _adaptivePollInterval ?? _options.PollInterval; // adaptive
    private TimeSpan? _adaptivePollInterval; // set by adaptive logic

    private async Task PollLoopAsync()
    {
        _logger.Info("Poll loop started");
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                if (IsCircuitOpen())
                {
                    await Task.Delay(_currentPollInterval, _cts.Token); // wait until circuit half-open
                }
                else
                {
                    var symbols = _subs.Keys.ToList();
                    if (symbols.Count > 0)
                    {
                        var batches = Batch(symbols, _options.BatchSize);
                        foreach (var batch in batches)
                        {
                            await FetchBatchAsync(batch);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.Error("Poll loop error", ex);
            }
            UpdateAdaptiveInterval();
            await Task.Delay(_currentPollInterval, _cts.Token).ContinueWith(_=>{}, TaskScheduler.Default);
        }
    }

    private bool IsCircuitOpen()
    {
        if (_circuitOpenedUtc == null) return false;
        if (DateTime.UtcNow - _circuitOpenedUtc >= _options.CircuitBreakerOpenDuration)
        {
            // move to half-open
            _circuitOpenedUtc = null;
            _consecutiveFailures = 0;
            _logger.Warn("Circuit breaker half-open: allowing trial calls");
            return false;
        }
        return true;
    }

    private IEnumerable<List<string>> Batch(List<string> items, int size)
    {
        for (int i=0; i<items.Count; i+=size)
            yield return items.GetRange(i, Math.Min(size, items.Count - i));
    }

    private async Task FetchBatchAsync(List<string> symbols)
    {
        if (!await _fetchGate.WaitAsync(0)) return; // avoid overlapping
        try
        {
            if (!AllowCall())
            {
                _logger.Debug("Rate limit gate - skipping batch");
                return;
            }

            var now = DateTime.UtcNow;
            var quotes = await ExecuteWithRetryAsync(() => _provider.GetQuotesAsync(symbols));
            _successCount += 1; _lastSuccessUtc = now; _consecutiveFailures = 0;
            foreach (var q in quotes)
            {
                if (!IsValidQuote(q)) continue; // pre-filter
                var norm = NormalizeSymbol(q.Symbol);
                if (_subs.TryGetValue(norm, out var info))
                {
                    var rawQuote = new Quote(q.Symbol, q.Price, q.Open, q.High, q.Low, q.Close, q.Volume, q.Timestamp, _provider.ProviderName);
                    var validation = _quoteValidator.Validate(rawQuote, info.LastPublished, now, _options);
                    if (validation.Status == Data.DataValidation.QuoteValidationStatus.Rejected)
                    {
                        _rejectedCount++;
                        _logger.Warn("Quote rejected", new() { {"symbol", q.Symbol}, {"reason", validation.Reason} });
                        continue;
                    }
                    if (validation.Status == Data.DataValidation.QuoteValidationStatus.Corrected)
                    {
                        _correctedCount++;
                        foreach (var kv in validation.Corrections)
                            _logger.Debug("Quote corrected", new() { {"symbol", q.Symbol}, {kv.Key, kv.Value} });
                        rawQuote = new Quote(rawQuote.Symbol,
                                             (validation.Corrections.TryGetValue("Last", out var lst) && lst is decimal) ? (decimal)lst : rawQuote.Last,
                                             (validation.Corrections.TryGetValue("Open", out var op) && op is decimal) ? (decimal)op : rawQuote.Open,
                                             (validation.Corrections.TryGetValue("High", out var hi) && hi is decimal) ? (decimal)hi : rawQuote.High,
                                             (validation.Corrections.TryGetValue("Low", out var lo) && lo is decimal) ? (decimal)lo : rawQuote.Low,
                                             (validation.Corrections.TryGetValue("Close", out var cl) && cl is decimal) ? (decimal)cl : rawQuote.Close,
                                             (validation.Corrections.TryGetValue("Volume", out var vol) && vol is long) ? (long)vol : rawQuote.Volume,
                                             rawQuote.Timestamp,
                                             rawQuote.Provider,
                                             rawQuote.Currency);
                        q.Price = rawQuote.Last; q.Open = rawQuote.Open; q.High = rawQuote.High; q.Low = rawQuote.Low; q.Close = rawQuote.Close; q.Volume = rawQuote.Volume;
                    }
                    else
                    {
                        _validatedCount++;
                    }

                    var update = BuildUpdate(info.LastPublished, q, now);
                    var newQuote = rawQuote;
                    info.LastPublished = newQuote;
                    info.LastFetch = now;
                    _quoteCache[norm] = newQuote;
                    if (update != null)
                    {
                        await _updateChannel.Writer.WriteAsync(update);
                    }
                    // add to history cache
                    _historyCache?.AddQuote(newQuote);
                }
            }
            RemoveStale();
        }
        catch (TransientProviderException tpex)
        {
            _transientFailureCount++; _failureCount++; _lastFailureUtc = DateTime.UtcNow; _consecutiveFailures++;
            _logger.Warn("Transient batch failure", new(){{"error", tpex.Message}});
            EvaluateCircuitAfterFailure();
        }
        catch (Exception ex)
        {
            _failureCount++; _lastFailureUtc = DateTime.UtcNow; _consecutiveFailures++;
            _logger.Error("Batch fetch failed", ex);
            EvaluateCircuitAfterFailure();
        }
        finally
        {
            _fetchGate.Release();
        }
    }

    private void EvaluateCircuitAfterFailure()
    {
        if (_consecutiveFailures >= _options.CircuitBreakerFailureThreshold && _circuitOpenedUtc == null)
        {
            _circuitOpenedUtc = DateTime.UtcNow;
            _circuitOpenCount++;
            _logger.Warn("Circuit breaker opened", new(){{"failures", _consecutiveFailures}});
        }
    }

    private bool AllowCall()
    {
        var now = DateTime.UtcNow;
        _callTimestamps.Enqueue(now);
        while (_callTimestamps.TryPeek(out var ts) && now - ts > TimeSpan.FromMinutes(1))
            _callTimestamps.TryDequeue(out _);
        return _callTimestamps.Count <= _options.RateLimitPerMinute;
    }

    private bool IsValidQuote(StockQuote q)
    {
        if (q.Price <= 0 || q.Open < 0 || q.High < 0 || q.Low < 0 || q.Close < 0) return false;
        if (q.High < q.Low) return false;
        return true;
    }

    private void RemoveStale()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _subs)
        {
            var info = kv.Value;
            if (now - info.LastFetch > _options.StaleAfter && info.LastPublished != null)
            {
                var staleUpdate = new QuoteUpdate(kv.Key, QuoteUpdateFields.None, null, null, null, null, null, null, now, _provider.ProviderName, true);
                _ = _updateChannel.Writer.TryWrite(staleUpdate);
                _logger.Debug($"Symbol {kv.Key} stale {now - info.LastFetch}");
            }
        }
    }

    private ProviderErrorType ClassifyError(Exception ex)
    {
        if (ex is TransientProviderException) return ProviderErrorType.Transient;
        if (ex is ProviderTimeoutException or ProviderRateLimitException or ProviderNetworkException) return ProviderErrorType.Transient;
        if (ex is ProviderDataFormatException) return ProviderErrorType.Fatal;
        var msg = ex.Message.ToLowerInvariant();
        if (msg.Contains("timeout")) return ProviderErrorType.Transient;
        if (msg.Contains("rate") || msg.Contains("429")) return ProviderErrorType.Transient;
        if (msg.Contains("temporar") || msg.Contains("unavailable")) return ProviderErrorType.Transient;
        return ProviderErrorType.Fatal;
    }

    // Adaptive polling skeleton (Medium item)
    private void UpdateAdaptiveInterval()
    {
        // Simple logic: if no successes in last minute increase interval, if many successes and price changes, decrease.
        var metrics = GetMetrics();
        if (metrics.LastSuccessUtc == null) return;
        var age = DateTime.UtcNow - metrics.LastSuccessUtc.Value;
        if (age > TimeSpan.FromSeconds(30))
            _adaptivePollInterval = TimeSpan.FromMilliseconds(Math.Min(_options.PollInterval.TotalMilliseconds * 2, 60_000));
        else if (age < TimeSpan.FromSeconds(5))
            _adaptivePollInterval = TimeSpan.FromMilliseconds(Math.Max(_options.PollInterval.TotalMilliseconds / 2, 500));
        else
            _adaptivePollInterval = null; // default
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> op)
    {
        var attempt = 0;
        List<Exception>? errors = null;
        while (true)
        {
            try
            {
                attempt++;
                return await op();
            }
            catch (Exception ex)
            {
                var classification = ClassifyError(ex);
                if (classification == ProviderErrorType.Fatal) throw;
                errors ??= new List<Exception>();
                errors.Add(ex);
                if (attempt >= _options.MaxRetryAttempts) throw new AggregateException(errors);
                var delay = JitterDelay(_options.BaseRetryDelayMs, attempt);
                _logger.Debug($"Retry attempt {attempt} delay={delay}ms type={classification}");
                await Task.Delay(delay, _cts.Token);
            }
        }
    }

    private int JitterDelay(int baseDelayMs, int attempt)
    {
        var factor = Math.Pow(2, attempt - 1); // 1,2,4
        var rnd = Random.Shared.Next(0, 100);
        return (int)(baseDelayMs * factor) + rnd;
    }

    private string NormalizeSymbol(string raw) => MarketSymbolUtil.Normalize(raw);

    private QuoteUpdate? BuildUpdate(Quote? previous, StockQuote current, DateTime ts)
    {
        if (previous == null)
        {
            return new QuoteUpdate(current.Symbol, QuoteUpdateFields.All, current.Price, current.Open, current.High, current.Low, current.Close, current.Volume, ts, _provider.ProviderName);
        }
        QuoteUpdateFields fields = QuoteUpdateFields.None;
        decimal? last=null, open=null, high=null, low=null, close=null; long? volume=null;
        if (current.Price != previous.Last) { fields |= QuoteUpdateFields.Last; last = current.Price; }
        if (current.Open != previous.Open) { fields |= QuoteUpdateFields.Open; open = current.Open; }
        if (current.High != previous.High) { fields |= QuoteUpdateFields.High; high = current.High; }
        if (current.Low != previous.Low) { fields |= QuoteUpdateFields.Low; low = current.Low; }
        if (current.Close != previous.Close) { fields |= QuoteUpdateFields.Close; close = current.Close; }
        if (current.Volume != previous.Volume) { fields |= QuoteUpdateFields.Volume; volume = current.Volume; }
        if (fields == QuoteUpdateFields.None) return null;
        return new QuoteUpdate(current.Symbol, fields, last, open, high, low, close, volume, ts, _provider.ProviderName);
    }

    private async Task PersistCacheAsync()
    {
        // Placeholder: persist to file or DB for warm start.
        await Task.CompletedTask;
    }

    private enum ProviderErrorType { Transient, Fatal }
}

// Custom transient exception to allow providers to signal retryable errors
public class TransientProviderException : Exception
{
    public TransientProviderException(string message) : base(message) {}
    public TransientProviderException(string message, Exception inner) : base(message, inner) {}
}
