using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http; // added for HttpClient
using System.Threading;
using System.Threading.Tasks;
using ApexV2.Core.Logging;
using ApexV2.Data.Caching;

namespace ApexV2.Data.Fundamentals;

/// <summary>
/// Options governing caching, refresh and validation behavior for fundamentals.
/// </summary>
public class FundamentalDataEngineOptions
{
    public TimeSpan SnapshotTtl = TimeSpan.FromMinutes(30); // how long to keep snapshot before refresh
    public TimeSpan MinRefreshInterval = TimeSpan.FromMinutes(2); // throttle repeated requests
    public int MaxConcurrentFetches = 4;
    public bool EnableInMemoryCache = true;
    public bool PersistToDatabase = true; // placeholder (future integration to EF)
    public bool EnableMetrics = true;
}

/// <summary>
/// Runtime metrics for fundamentals engine.
/// </summary>
public record FundamentalEngineMetrics(
    int ActiveFetches,
    int CacheEntries,
    long CacheHits,
    long CacheMisses,
    long ProviderCalls,
    long Errors,
    DateTime? LastSuccessUtc,
    DateTime? LastErrorUtc);

internal class CachedSnapshot
{
    public FundamentalSnapshot Snapshot = null!;
    public DateTime CachedUtc;
    public DateTime LastAccessUtc;
    public bool RefreshInProgress;
}

/// <summary>
/// Core engine responsible for retrieving, caching and providing fundamental data snapshots.
/// - Per-symbol cache with TTL and soft refresh
/// - Throttled provider calls (MinRefreshInterval)
/// - Concurrent request de-duplication (single flight)
/// - Basic metrics for diagnostics
/// </summary>
public class FundamentalDataEngine : IDisposable
{
    private readonly IFundamentalDataProvider _provider;
    private readonly FundamentalDataEngineOptions _options;
    private readonly Logger _logger;
    private readonly ConcurrentDictionary<string, CachedSnapshot> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _symbolLocks = new(StringComparer.OrdinalIgnoreCase);

    private long _cacheHits; private long _cacheMisses; private long _providerCalls; private long _errors;
    private DateTime? _lastSuccessUtc; private DateTime? _lastErrorUtc;

    public FundamentalDataEngine(IFundamentalDataProvider provider, LogManager logManager, FundamentalDataEngineOptions? options = null)
    {
        _provider = provider;
        _options = options ?? new FundamentalDataEngineOptions();
        _logger = logManager.GetLogger("FundamentalDataEngine");
    }

    public async Task<FundamentalSnapshot?> GetSnapshotAsync(string symbol, FundamentalDataScope scope = FundamentalDataScope.Core, bool forceRefresh = false, CancellationToken ct = default)
    {
        symbol = Normalize(symbol);
        var now = DateTime.UtcNow;

        CachedSnapshot? existing = null; // ensure definite assignment tracking
        if (_options.EnableInMemoryCache && _cache.TryGetValue(symbol, out var existingLookup))
        {
            existing = existingLookup;
            existing.LastAccessUtc = now;
            if (!forceRefresh && now - existing.CachedUtc < _options.SnapshotTtl)
            {
                Interlocked.Increment(ref _cacheHits);
                return existing.Snapshot;
            }
            if (existing.RefreshInProgress && !forceRefresh)
            {
                Interlocked.Increment(ref _cacheHits);
                return existing.Snapshot;
            }
        }
        else
        {
            Interlocked.Increment(ref _cacheMisses);
        }

        var sem = _symbolLocks.GetOrAdd(symbol, _ => new SemaphoreSlim(1,1));
        await sem.WaitAsync(ct);
        try
        {
            if (_options.EnableInMemoryCache && _cache.TryGetValue(symbol, out existingLookup))
            {
                existing = existingLookup;
                if (!forceRefresh && now - existing.CachedUtc < _options.SnapshotTtl)
                {
                    Interlocked.Increment(ref _cacheHits);
                    return existing.Snapshot;
                }
                if (!forceRefresh && existing.RefreshInProgress && now - existing.CachedUtc < _options.SnapshotTtl + TimeSpan.FromMinutes(5))
                {
                    Interlocked.Increment(ref _cacheHits);
                    return existing.Snapshot;
                }
                if (!forceRefresh && now - existing.CachedUtc < _options.MinRefreshInterval)
                {
                    Interlocked.Increment(ref _cacheHits);
                    return existing.Snapshot;
                }
            }

            if (existing != null) existing.RefreshInProgress = true;
            var snapshot = await FetchSnapshotInternalAsync(symbol, scope, ct);
            if (snapshot != null && _options.EnableInMemoryCache)
            {
                var cacheEntry = existing ?? new CachedSnapshot();
                cacheEntry.Snapshot = snapshot;
                cacheEntry.CachedUtc = now;
                cacheEntry.LastAccessUtc = now;
                cacheEntry.RefreshInProgress = false;
                _cache[symbol] = cacheEntry;
                existing = cacheEntry;
            }
            return snapshot;
        }
        finally
        {
            if (existing != null) existing.RefreshInProgress = false;
            sem.Release();
        }
    }

    private async Task<FundamentalSnapshot?> FetchSnapshotInternalAsync(string symbol, FundamentalDataScope scope, CancellationToken ct)
    {
        try
        {
            Interlocked.Increment(ref _providerCalls);
            var snapshot = await _provider.GetSnapshotAsync(symbol, scope);
            if (snapshot != null)
            {
                _lastSuccessUtc = DateTime.UtcNow;
            }
            return snapshot;
        }
        catch (Exception ex)
        {
            _lastErrorUtc = DateTime.UtcNow; Interlocked.Increment(ref _errors);
            _logger.Error($"Snapshot fetch failed {symbol}", ex);
            return null;
        }
    }

    public FundamentalEngineMetrics GetMetrics() => new(
        ActiveFetches: _symbolLocks.Count(s => s.Value.CurrentCount == 0),
        CacheEntries: _cache.Count,
        CacheHits: Interlocked.Read(ref _cacheHits),
        CacheMisses: Interlocked.Read(ref _cacheMisses),
        ProviderCalls: Interlocked.Read(ref _providerCalls),
        Errors: Interlocked.Read(ref _errors),
        LastSuccessUtc: _lastSuccessUtc,
        LastErrorUtc: _lastErrorUtc
    );

    public IReadOnlyList<(string Symbol, DateTime CachedUtc, DateTime LastAccessUtc, TimeSpan Age)> GetCacheInventory()
    {
        return _cache.Select(kv => (kv.Key, kv.Value.CachedUtc, kv.Value.LastAccessUtc, DateTime.UtcNow - kv.Value.CachedUtc)).ToList();
    }

    private static string Normalize(string raw)
    {
        var s = raw.Trim().ToUpperInvariant();
        if (!s.EndsWith(".TO") && s.Length <= 5) s += ".TO"; // reuse rule
        return s;
    }

    public void Dispose()
    {
        foreach (var s in _symbolLocks.Values) s.Dispose();
    }
}

public interface IFundamentalProviderFactory
{
    IFundamentalDataProvider GetPrimary();
    IReadOnlyList<IFundamentalDataProvider> GetAll();
}

public class SingleFundamentalProviderFactory : IFundamentalProviderFactory
{
    private readonly IFundamentalDataProvider _provider;
    public SingleFundamentalProviderFactory(IFundamentalDataProvider provider) { _provider = provider; }
    public IFundamentalDataProvider GetPrimary() => _provider;
    public IReadOnlyList<IFundamentalDataProvider> GetAll() => new []{_provider};
}

/// <summary>
/// Finnhub provider adapter for fetching fundamental data.
/// </summary>
public class FinnhubFundamentalProvider : IFundamentalDataProvider
{
    private readonly string _apiKey;
    private readonly HttpClient _http;
    public string ProviderName => "Finnhub";
    public bool RequiresApiKey => true;
    public FinnhubFundamentalProvider(string apiKey, HttpClient? httpClient = null)
    {
        _apiKey = apiKey; _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
    }
    private bool ApiReady => !string.IsNullOrWhiteSpace(_apiKey);

    public async Task<FundamentalSnapshot?> GetSnapshotAsync(string symbol, FundamentalDataScope scope = FundamentalDataScope.Core)
    {
        if (!ApiReady) return null;
        try
        {
            var metrics = await GetMetricsAsync(symbol);
            if (metrics == null) return null;
            var core = new CoreRatios(
                PERatio: GetDecimal(metrics, "peBasicExclExtraTTM"),
                EPS: GetDecimal(metrics, "epsBasicExclExtraItemsTTM"),
                DebtToEquity: GetDecimal(metrics, "totalDebtToEquityQuarterly"),
                PriceToBook: GetDecimal(metrics, "priceToBookRatioQuarterly"),
                ReturnOnEquity: GetDecimal(metrics, "roeTTM"),
                DividendYield: GetDecimal(metrics, "dividendYieldIndicatedAnnual") * 100m,
                MarketCap: GetDecimal(metrics, "marketCapitalization")
            );
            ExtendedRatios? ext = null; FullRatios? full = null;
            if (scope is FundamentalDataScope.Extended or FundamentalDataScope.Full)
            {
                ext = new ExtendedRatios(
                    GrossMargin: GetDecimal(metrics, "grossMarginTTM"),
                    OperatingMargin: GetDecimal(metrics, "operatingMarginTTM"),
                    NetMargin: GetDecimal(metrics, "netProfitMarginTTM"),
                    RevenueGrowthYear: GetDecimal(metrics, "revenueGrowthTTMYoy"),
                    Ebitda: GetDecimal(metrics, "ebitdaTTM"),
                    FreeCashFlow: GetDecimal(metrics, "freeCashFlowTTM")
                );
            }
            if (scope == FundamentalDataScope.Full)
            {
                full = new FullRatios(
                    CurrentRatio: GetDecimal(metrics, "currentRatioQuarterly"),
                    QuickRatio: GetDecimal(metrics, "quickRatioQuarterly"),
                    InterestCoverage: GetDecimal(metrics, "interestCoverageTTM"),
                    AssetTurnover: GetDecimal(metrics, "assetTurnoverTTM"),
                    InventoryTurnover: GetDecimal(metrics, "inventoryTurnoverTTM"),
                    PiotroskiFScore: GetDecimal(metrics, "piotroskiScoreTTM")
                );
            }
            return new FundamentalSnapshot(symbol, DateTime.UtcNow, core, ext, full);
        }
        catch { return null; }
    }

    public async Task<FinancialStatements?> GetFinancialStatementsAsync(string symbol)
    {
        if (!ApiReady) return null;
        try
        {
            var income = await GetIncomeStatementAsync(symbol);
            var balance = await GetBalanceSheetAsync(symbol);
            var cash = await GetCashFlowAsync(symbol);
            return new FinancialStatements(symbol, DateTime.UtcNow, income.Take(2).ToList(), balance.Take(2).ToList(), cash.Take(2).ToList());
        }
        catch { return null; }
    }

    public async Task<CompanyProfile?> GetCompanyProfileAsync(string symbol)
    {
        if (!ApiReady) return null;
        try
        {
            var url = $"https://finnhub.io/api/v1/stock/profile2?symbol={Uri.EscapeDataString(symbol)}&token={_apiKey}";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
            var root = doc.RootElement;
            string val(string n) => root.TryGetProperty(n, out var e) ? e.GetString() ?? string.Empty : string.Empty;
            return new CompanyProfile(symbol, val("name"), val("finnhubIndustry"), val("finnhubIndustry"), val("exchange"), val("country"), val("currency"), string.Empty, DateTime.UtcNow);
        }
        catch { return null; }
    }

    public async Task<IReadOnlyList<DividendRecord>> GetDividendHistoryAsync(string symbol, int years = 5)
    {
        if (!ApiReady) return Array.Empty<DividendRecord>();
        try
        {
            var to = DateTime.UtcNow.Date; var from = to.AddYears(-years);
            var url = $"https://finnhub.io/api/v1/stock/dividend?symbol={Uri.EscapeDataString(symbol)}&from={from:yyyy-MM-dd}&to={to:yyyy-MM-dd}&token={_apiKey}";
            using var resp = await _http.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return Array.Empty<DividendRecord>();
            using var stream = await resp.Content.ReadAsStreamAsync();
            using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
            var list = new List<DividendRecord>();
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                DateTime ex = el.TryGetProperty("date", out var dEl) && DateTime.TryParse(dEl.GetString(), out var dt1) ? dt1 : DateTime.MinValue;
                decimal amount = el.TryGetProperty("amount", out var aEl) && aEl.TryGetDecimal(out var dec) ? dec : 0m;
                list.Add(new DividendRecord(ex, ex.AddDays(30), amount, string.Empty));
            }
            return list;
        }
        catch { return Array.Empty<DividendRecord>(); }
    }

    private async Task<System.Text.Json.JsonElement?> GetMetricsAsync(string symbol)
    {
        var url = $"https://finnhub.io/api/v1/stock/metric?symbol={Uri.EscapeDataString(symbol)}&metric=all&token={_apiKey}";
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return null;
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
        if (doc.RootElement.TryGetProperty("metric", out var metricObj)) return metricObj.Clone();
        return null;
    }

    private async Task<List<IncomeStatementPeriod>> GetIncomeStatementAsync(string symbol)
    {
        var url = $"https://finnhub.io/api/v1/stock/financials?symbol={Uri.EscapeDataString(symbol)}&statement=is&freq=annual&token={_apiKey}";
        return await ParseIncomeAsync(url);
    }
    private async Task<List<BalanceSheetPeriod>> GetBalanceSheetAsync(string symbol)
    {
        var url = $"https://finnhub.io/api/v1/stock/financials?symbol={Uri.EscapeDataString(symbol)}&statement=bs&freq=annual&token={_apiKey}";
        return await ParseBalanceAsync(url);
    }
    private async Task<List<CashFlowStatementPeriod>> GetCashFlowAsync(string symbol)
    {
        var url = $"https://finnhub.io/api/v1/stock/financials?symbol={Uri.EscapeDataString(symbol)}&statement=cf&freq=annual&token={_apiKey}";
        return await ParseCashAsync(url);
    }

    private async Task<List<IncomeStatementPeriod>> ParseIncomeAsync(string url)
    {
        var list = new List<IncomeStatementPeriod>();
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return list;
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
        if (doc.RootElement.TryGetProperty("financials", out var arr))
        {
            foreach (var el in arr.EnumerateArray())
            {
                DateTime periodEnd = el.TryGetProperty("period", out var pEl) && DateTime.TryParse(pEl.GetString(), out var dt) ? dt : DateTime.MinValue;
                list.Add(new IncomeStatementPeriod(periodEnd,
                    Revenue: GetNum(el, "revenue"),
                    GrossProfit: GetNum(el, "grossProfit"),
                    OperatingIncome: GetNum(el, "operatingIncome"),
                    NetIncome: GetNum(el, "netIncome"),
                    DilutedEPS: GetNum(el, "eps")));
            }
        }
        return list;
    }

    private async Task<List<BalanceSheetPeriod>> ParseBalanceAsync(string url)
    {
        var list = new List<BalanceSheetPeriod>();
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return list;
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
        if (doc.RootElement.TryGetProperty("financials", out var arr))
        {
            foreach (var el in arr.EnumerateArray())
            {
                DateTime periodEnd = el.TryGetProperty("period", out var pEl) && DateTime.TryParse(pEl.GetString(), out var dt) ? dt : DateTime.MinValue;
                list.Add(new BalanceSheetPeriod(periodEnd,
                    TotalAssets: GetNum(el, "totalAssets"),
                    TotalLiabilities: GetNum(el, "totalLiabilities"),
                    ShareholderEquity: GetNum(el, "totalShareholderEquity"),
                    CashAndEquivalents: GetNum(el, "cashAndCashEquivalents"),
                    LongTermDebt: GetNum(el, "longTermDebt")));
            }
        }
        return list;
    }

    private async Task<List<CashFlowStatementPeriod>> ParseCashAsync(string url)
    {
        var list = new List<CashFlowStatementPeriod>();
        using var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return list;
        using var stream = await resp.Content.ReadAsStreamAsync();
        using var doc = await System.Text.Json.JsonDocument.ParseAsync(stream);
        if (doc.RootElement.TryGetProperty("financials", out var arr))
        {
            foreach (var el in arr.EnumerateArray())
            {
                DateTime periodEnd = el.TryGetProperty("period", out var pEl) && DateTime.TryParse(pEl.GetString(), out var dt) ? dt : DateTime.MinValue;
                var op = GetNum(el, "cashFlowFromOperations");
                var investing = GetNum(el, "cashFlowFromInvesting");
                var financing = GetNum(el, "cashFlowFromFinancing");
                var fcf = op + investing + financing; // fallback
                list.Add(new CashFlowStatementPeriod(periodEnd, op, investing, financing, fcf));
            }
        }
        return list;
    }

    private static decimal GetNum(System.Text.Json.JsonElement el, string name)
    {
        if (el.TryGetProperty(name, out var v))
        {
            if (v.ValueKind == System.Text.Json.JsonValueKind.Number && v.TryGetDecimal(out var dec)) return dec;
            if (v.ValueKind == System.Text.Json.JsonValueKind.String && decimal.TryParse(v.GetString(), out dec)) return dec;
        }
        return 0m;
    }
    private static decimal GetDecimal(System.Text.Json.JsonElement? metrics, string name)
    {
        if (metrics == null) return 0m;
        if (metrics.Value.TryGetProperty(name, out var el))
        {
            if (el.ValueKind == System.Text.Json.JsonValueKind.Number && el.TryGetDecimal(out var dec)) return dec;
            if (el.ValueKind == System.Text.Json.JsonValueKind.String && decimal.TryParse(el.GetString(), out var dec2)) return dec2;
        }
        return 0m;
    }
}
