using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApexV2.Data.MarketData;
using ApexV2.Analysis.Scanner;
using ApexV2.Charts.Export; // For IChartLogger

namespace ApexV2.Analysis.Scanner;

/// <summary>
/// Core scanning service for screening stocks based on custom criteria
/// </summary>
public class ScannerService
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly IChartLogger _logger;
    private readonly Dictionary<Guid, ScanConfiguration> _savedScans = new();
    private readonly Dictionary<Guid, CancellationTokenSource> _runningScans = new();
    private readonly object _lockObject = new();

    public event EventHandler<ScanProgressEventArgs>? ScanProgress;
    public event EventHandler<ScanCompletedEventArgs>? ScanCompleted;
    public event EventHandler<ScanErrorEventArgs>? ScanError;

    public ScannerService(IMarketDataProvider marketDataProvider, IChartLogger logger)
    {
        _marketDataProvider = marketDataProvider ?? throw new ArgumentNullException(nameof(marketDataProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Execute a scan with the given configuration
    /// </summary>
    public async Task<ScanResult> ExecuteScanAsync(ScanConfiguration config, CancellationToken cancellationToken = default)
    {
        var scanId = Guid.NewGuid();
        var startTime = DateTime.Now;

        try
        {
            _logger.Info($"Starting scan: {config.Name} (ID: {scanId})");

            // Register the cancellation token
            lock (_lockObject)
            {
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _runningScans[scanId] = cts;
            }

            // Get symbols to scan
            var symbolsToScan = await GetSymbolsToScanAsync(config);
            _logger.Info($"Scanning {symbolsToScan.Count} symbols");

            var progress = new ScanProgress
            {
                ScanId = scanId,
                TotalSymbols = symbolsToScan.Count,
                ProcessedSymbols = 0,
                MatchingSymbols = 0,
                CurrentStage = "Initializing",
                StartTime = startTime
            };

            ReportProgress(progress);

            var results = new List<ScanResultItem>();
            var processedCount = 0;

            // Process each symbol
            foreach (var symbol in symbolsToScan)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress.CurrentSymbol = symbol;
                progress.CurrentStage = "Analyzing";
                progress.ProcessedSymbols = processedCount;
                ReportProgress(progress);

                try
                {
                    var resultItem = await EvaluateSymbolAsync(symbol, config, cancellationToken);
                    if (resultItem != null)
                    {
                        results.Add(resultItem);
                        progress.MatchingSymbols = results.Count;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Warn($"Error evaluating symbol {symbol}: {ex.Message}");
                }

                processedCount++;

                // Update estimated time remaining
                if (processedCount > 0)
                {
                    var avgTimePerSymbol = progress.ElapsedTime.TotalMilliseconds / processedCount;
                    var remainingSymbols = symbolsToScan.Count - processedCount;
                    progress.EstimatedTimeRemaining = TimeSpan.FromMilliseconds(avgTimePerSymbol * remainingSymbols);
                }

                // Throttle to avoid overwhelming the system
                if (processedCount % 10 == 0)
                {
                    await Task.Delay(100, cancellationToken);
                }
            }

            // Apply sorting and limits
            results = ApplySortingAndLimits(results, config.ResultSettings);

            // Calculate statistics
            var statistics = CalculateStatistics(results, symbolsToScan.Count);

            var scanResult = new ScanResult
            {
                ScanId = scanId,
                ScanName = config.Name,
                ScanDate = startTime,
                ScanDuration = DateTime.Now - startTime,
                TotalSymbolsScanned = symbolsToScan.Count,
                MatchingSymbols = results.Count,
                Results = results,
                Status = ScanStatus.Completed,
                Statistics = statistics
            };

            // Update the configuration's last run time
            config.LastRun = DateTime.Now;

            _logger.Info($"Scan completed: {results.Count} matches from {symbolsToScan.Count} symbols in {scanResult.ScanDuration.TotalSeconds:F1}s");

            OnScanCompleted(new ScanCompletedEventArgs(scanResult));
            return scanResult;
        }
        catch (OperationCanceledException)
        {
            _logger.Info($"Scan cancelled: {config.Name}");
            return new ScanResult
            {
                ScanId = scanId,
                ScanName = config.Name,
                ScanDate = startTime,
                ScanDuration = DateTime.Now - startTime,
                Status = ScanStatus.Cancelled
            };
        }
        catch (Exception ex)
        {
            _logger.Error($"Scan failed: {config.Name} - {ex.Message}", ex);
            OnScanError(new ScanErrorEventArgs(scanId, ex, ex.Message));
            
            return new ScanResult
            {
                ScanId = scanId,
                ScanName = config.Name,
                ScanDate = startTime,
                ScanDuration = DateTime.Now - startTime,
                Status = ScanStatus.Failed,
                ErrorMessage = ex.Message
            };
        }
        finally
        {
            // Clean up
            lock (_lockObject)
            {
                if (_runningScans.TryGetValue(scanId, out var cts))
                {
                    cts.Dispose();
                    _runningScans.Remove(scanId);
                }
            }
        }
    }

    /// <summary>
    /// Cancel a running scan
    /// </summary>
    public bool CancelScan(Guid scanId)
    {
        lock (_lockObject)
        {
            if (_runningScans.TryGetValue(scanId, out var cts))
            {
                cts.Cancel();
                _logger.Info($"Cancellation requested for scan: {scanId}");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Get all running scans
    /// </summary>
    public IReadOnlyList<Guid> GetRunningScanIds()
    {
        lock (_lockObject)
        {
            return _runningScans.Keys.ToList();
        }
    }

    /// <summary>
    /// Save a scan configuration
    /// </summary>
    public void SaveScanConfiguration(ScanConfiguration config)
    {
        lock (_lockObject)
        {
            config.ModifiedDate = DateTime.Now;
            _savedScans[config.Id] = config;
        }
        _logger.Info($"Saved scan configuration: {config.Name}");
    }

    /// <summary>
    /// Get all saved scan configurations
    /// </summary>
    public IReadOnlyList<ScanConfiguration> GetSavedScans()
    {
        lock (_lockObject)
        {
            return _savedScans.Values.OrderBy(s => s.Name).ToList();
        }
    }

    /// <summary>
    /// Get a specific scan configuration
    /// </summary>
    public ScanConfiguration? GetScanConfiguration(Guid id)
    {
        lock (_lockObject)
        {
            return _savedScans.TryGetValue(id, out var config) ? config : null;
        }
    }

    /// <summary>
    /// Delete a saved scan configuration
    /// </summary>
    public bool DeleteScanConfiguration(Guid id)
    {
        lock (_lockObject)
        {
            if (_savedScans.Remove(id))
            {
                _logger.Info($"Deleted scan configuration: {id}");
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Create a preset scan configuration
    /// </summary>
    public ScanConfiguration CreatePresetScan(ScanPresetType preset)
    {
        var config = new ScanConfiguration();

        switch (preset)
        {
            case ScanPresetType.HighVolume:
                config.Name = "High Volume Stocks";
                config.Description = "Stocks with above-average volume";
                config.Criteria.Add(new ScanCriterion
                {
                    Type = ScanCriterionType.Volume,
                    Operator = ScanOperator.GreaterThan,
                    Value = 1000000
                });
                break;

            case ScanPresetType.Momentum:
                config.Name = "Momentum Stocks";
                config.Description = "Stocks with strong upward momentum";
                config.Criteria.AddRange(new[]
                {
                    new ScanCriterion { Type = ScanCriterionType.PriceChangePercent, Operator = ScanOperator.GreaterThan, Value = 5 },
                    new ScanCriterion { Type = ScanCriterionType.RSI, Operator = ScanOperator.Between, Value = 60, SecondValue = 80 }
                });
                break;

            case ScanPresetType.Oversold:
                config.Name = "Oversold Stocks";
                config.Description = "Potentially oversold stocks";
                config.Criteria.AddRange(new[]
                {
                    new ScanCriterion { Type = ScanCriterionType.RSI, Operator = ScanOperator.LessThan, Value = 30 },
                    new ScanCriterion { Type = ScanCriterionType.PriceChangePercent, Operator = ScanOperator.LessThan, Value = -5 }
                });
                break;

            case ScanPresetType.ValueStocks:
                config.Name = "Value Stocks";
                config.Description = "Stocks with attractive valuations";
                config.Criteria.AddRange(new[]
                {
                    new ScanCriterion { Type = ScanCriterionType.PERatio, Operator = ScanOperator.Between, Value = 5, SecondValue = 15 },
                    new ScanCriterion { Type = ScanCriterionType.DividendYield, Operator = ScanOperator.GreaterThan, Value = 2 }
                });
                break;

            case ScanPresetType.GrowthStocks:
                config.Name = "Growth Stocks";
                config.Description = "High-growth companies";
                config.Criteria.AddRange(new[]
                {
                    new ScanCriterion { Type = ScanCriterionType.EPSGrowth, Operator = ScanOperator.GreaterThan, Value = 20 },
                    new ScanCriterion { Type = ScanCriterionType.RevenueGrowth, Operator = ScanOperator.GreaterThan, Value = 15 }
                });
                break;

            case ScanPresetType.Breakouts:
                config.Name = "Breakout Stocks";
                config.Description = "Stocks breaking out of resistance";
                config.Criteria.AddRange(new[]
                {
                    new ScanCriterion { Type = ScanCriterionType.BreakoutUp, Operator = ScanOperator.Equals, Value = 1 },
                    new ScanCriterion { Type = ScanCriterionType.Volume, Operator = ScanOperator.GreaterThan, Value = 500000 }
                });
                break;

            default:
                config.Name = "Custom Scan";
                config.Description = "Custom scan configuration";
                break;
        }

        config.Tags.Add("Preset");
        return config;
    }

    private async Task<List<string>> GetSymbolsToScanAsync(ScanConfiguration config)
    {
        // For now, return a sample list of symbols
        // In a real implementation, this would query the market data provider
        // or database for available symbols based on the configured markets
        
        var allSymbols = new List<string>
        {
            "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA", "META", "NVDA", "NFLX", "CRM", "ORCL",
            "ADBE", "INTC", "AMD", "QCOM", "TXN", "AVGO", "CSCO", "IBM", "HPE", "DELL",
            "SHOP.TO", "RY.TO", "TD.TO", "BNS.TO", "BMO.TO", "CNR.TO", "CP.TO", "ENB.TO",
            "WCN.TO", "SU.TO", "CNQ.TO", "IMO.TO", "TOU.TO", "ARX.TO", "CVE.TO"
        };

        // Filter by markets if specified
        if (config.Markets.Any())
        {
            allSymbols = allSymbols.Where(s =>
                config.Markets.Contains("TSX") && s.EndsWith(".TO") ||
                config.Markets.Contains("NASDAQ") && !s.EndsWith(".TO") ||
                config.Markets.Contains("NYSE") && !s.EndsWith(".TO")
            ).ToList();
        }

        return allSymbols;
    }

    private async Task<ScanResultItem?> EvaluateSymbolAsync(string symbol, ScanConfiguration config, CancellationToken cancellationToken)
    {
        try
        {
            // Get market data for the symbol
            var marketData = await _marketDataProvider.GetQuoteAsync(symbol);
            if (marketData == null) return null;

            // Create the result item
            var resultItem = new ScanResultItem
            {
                Symbol = symbol,
                Name = GetSymbolName(symbol),
                Market = GetSymbolMarket(symbol),
                Sector = GetSymbolSector(symbol),
                Industry = GetSymbolIndustry(symbol),
                LastPrice = marketData.Price,
                Change = marketData.Price - marketData.Close,
                ChangePercent = marketData.Close > 0 
                    ? ((marketData.Price - marketData.Close) / marketData.Close) * 100 
                    : 0,
                Volume = (long)marketData.Volume,
                LastUpdate = marketData.Timestamp
            };

            // Add fundamental data (simulated for now)
            PopulateFundamentalData(resultItem);

            // Add technical indicators (simulated for now)
            PopulateTechnicalData(resultItem);

            // Evaluate all criteria
            var score = 0m;
            var matchingCriteria = 0;

            foreach (var criterion in config.Criteria.Where(c => c.IsEnabled))
            {
                if (EvaluateCriterion(resultItem, criterion))
                {
                    matchingCriteria++;
                    score += GetCriterionWeight(criterion.Type);
                }
            }

            // Only return if all criteria are met
            if (matchingCriteria == config.Criteria.Count(c => c.IsEnabled))
            {
                resultItem.Score = score;
                return resultItem;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Error evaluating {symbol}: {ex.Message}");
            return null;
        }
    }

    private bool EvaluateCriterion(ScanResultItem item, ScanCriterion criterion)
    {
        var value = GetCriterionValue(item, criterion.Type);
        if (!value.HasValue) return false;

        return criterion.Operator switch
        {
            ScanOperator.Equals => value == criterion.Value,
            ScanOperator.NotEquals => value != criterion.Value,
            ScanOperator.GreaterThan => value > criterion.Value,
            ScanOperator.GreaterThanOrEqual => value >= criterion.Value,
            ScanOperator.LessThan => value < criterion.Value,
            ScanOperator.LessThanOrEqual => value <= criterion.Value,
            ScanOperator.Between => criterion.SecondValue.HasValue && value >= criterion.Value && value <= criterion.SecondValue,
            ScanOperator.NotBetween => criterion.SecondValue.HasValue && (value < criterion.Value || value > criterion.SecondValue),
            _ => false
        };
    }

    private decimal? GetCriterionValue(ScanResultItem item, ScanCriterionType type)
    {
        return type switch
        {
            ScanCriterionType.Price => item.LastPrice,
            ScanCriterionType.PriceChange => item.Change,
            ScanCriterionType.PriceChangePercent => item.ChangePercent,
            ScanCriterionType.Volume => item.Volume,
            ScanCriterionType.MarketCap => item.MarketCap,
            ScanCriterionType.PERatio => item.PERatio,
            ScanCriterionType.EPS => item.EPS,
            ScanCriterionType.DividendYield => item.DividendYield,
            ScanCriterionType.DebtToEquity => item.DebtToEquity,
            ScanCriterionType.ROE => item.ROE,
            ScanCriterionType.RSI => item.RSI,
            ScanCriterionType.SMA => item.SMA20, // Default to SMA20
            _ => null
        };
    }

    private decimal GetCriterionWeight(ScanCriterionType type)
    {
        return type switch
        {
            ScanCriterionType.Volume => 1.0m,
            ScanCriterionType.PriceChangePercent => 2.0m,
            ScanCriterionType.RSI => 1.5m,
            ScanCriterionType.BreakoutUp => 3.0m,
            ScanCriterionType.EPSGrowth => 2.5m,
            _ => 1.0m
        };
    }

    private List<ScanResultItem> ApplySortingAndLimits(List<ScanResultItem> results, ScanResultSettings settings)
    {
        // Apply sorting
        var sorted = settings.SortBy switch
        {
            ScanSortBy.Score => results.OrderBy(r => r.Score),
            ScanSortBy.Symbol => results.OrderBy(r => r.Symbol),
            ScanSortBy.Price => results.OrderBy(r => r.LastPrice),
            ScanSortBy.Change => results.OrderBy(r => r.Change),
            ScanSortBy.ChangePercent => results.OrderBy(r => r.ChangePercent),
            ScanSortBy.Volume => results.OrderBy(r => r.Volume),
            ScanSortBy.MarketCap => results.OrderBy(r => r.MarketCap ?? 0),
            ScanSortBy.PERatio => results.OrderBy(r => r.PERatio ?? 0),
            ScanSortBy.RSI => results.OrderBy(r => r.RSI ?? 0),
            _ => results.OrderBy(r => r.Score)
        };

        IEnumerable<ScanResultItem> finalSorted = sorted;
        if (settings.SortDescending)
        {
            finalSorted = sorted.Reverse();
        }

        var resultList = finalSorted.ToList();

        // Apply ranking
        for (int i = 0; i < resultList.Count; i++)
        {
            resultList[i].Rank = i + 1;
        }

        // Apply limits
        if (settings.MaxResults > 0 && resultList.Count > settings.MaxResults)
        {
            resultList = resultList.Take(settings.MaxResults).ToList();
        }

        return resultList;
    }

    private ScanStatistics CalculateStatistics(List<ScanResultItem> results, int totalScanned)
    {
        if (!results.Any())
        {
            return new ScanStatistics();
        }

        return new ScanStatistics
        {
            AveragePrice = results.Average(r => r.LastPrice),
            AverageChange = results.Average(r => r.Change),
            AverageVolume = (decimal)results.Average(r => r.Volume),
            WinnersCount = results.Count(r => r.Change > 0),
            LosersCount = results.Count(r => r.Change < 0),
            UnchangedCount = results.Count(r => r.Change == 0),
            SectorBreakdown = results.GroupBy(r => r.Sector).ToDictionary(g => g.Key, g => g.Count()),
            MarketBreakdown = results.GroupBy(r => r.Market).ToDictionary(g => g.Key, g => g.Count())
        };
    }

    // Helper methods for simulated data
    private string GetSymbolName(string symbol)
    {
        var names = new Dictionary<string, string>
        {
            { "AAPL", "Apple Inc." },
            { "MSFT", "Microsoft Corporation" },
            { "GOOGL", "Alphabet Inc." },
            { "AMZN", "Amazon.com Inc." },
            { "TSLA", "Tesla Inc." },
            { "SHOP.TO", "Shopify Inc." },
            { "RY.TO", "Royal Bank of Canada" }
        };
        return names.TryGetValue(symbol, out var name) ? name : $"{symbol} Corp.";
    }

    private string GetSymbolMarket(string symbol)
    {
        return symbol.EndsWith(".TO") ? "TSX" : "NASDAQ";
    }

    private string GetSymbolSector(string symbol)
    {
        if (symbol.StartsWith("AAPL") || symbol.StartsWith("MSFT") || symbol.StartsWith("GOOGL"))
            return "Technology";
        if (symbol.StartsWith("RY") || symbol.StartsWith("TD") || symbol.StartsWith("BNS"))
            return "Financial Services";
        return "Technology";
    }

    private string GetSymbolIndustry(string symbol)
    {
        if (symbol.StartsWith("AAPL")) return "Consumer Electronics";
        if (symbol.StartsWith("MSFT")) return "Software";
        if (symbol.StartsWith("RY")) return "Banks";
        return "Software";
    }

    private void PopulateFundamentalData(ScanResultItem item)
    {
        // Simulate fundamental data
        var random = new Random(item.Symbol.GetHashCode());
        item.MarketCap = random.Next(1000, 500000) * 1000000m;
        item.PERatio = 10 + (decimal)random.NextDouble() * 40;
        item.EPS = 1 + (decimal)random.NextDouble() * 10;
        item.DividendYield = (decimal)random.NextDouble() * 5;
        item.BookValue = item.LastPrice * (0.5m + (decimal)random.NextDouble() * 1.5m);
        item.DebtToEquity = (decimal)random.NextDouble() * 2;
        item.ROE = 5 + (decimal)random.NextDouble() * 20;
        item.RevenueTTM = item.MarketCap * (0.5m + (decimal)random.NextDouble());
    }

    private void PopulateTechnicalData(ScanResultItem item)
    {
        // Simulate technical indicators
        var random = new Random(item.Symbol.GetHashCode() + 1);
        item.RSI = 20 + (decimal)random.NextDouble() * 60;
        item.SMA20 = item.LastPrice * (0.95m + (decimal)random.NextDouble() * 0.1m);
        item.SMA50 = item.LastPrice * (0.9m + (decimal)random.NextDouble() * 0.2m);
        item.SMA200 = item.LastPrice * (0.8m + (decimal)random.NextDouble() * 0.4m);
        item.MACD = -5 + (decimal)random.NextDouble() * 10;
        item.BollingerUpper = item.LastPrice * 1.05m;
        item.BollingerLower = item.LastPrice * 0.95m;
    }

    private void ReportProgress(ScanProgress progress)
    {
        ScanProgress?.Invoke(this, new ScanProgressEventArgs(progress));
    }

    protected virtual void OnScanCompleted(ScanCompletedEventArgs e)
    {
        ScanCompleted?.Invoke(this, e);
    }

    protected virtual void OnScanError(ScanErrorEventArgs e)
    {
        ScanError?.Invoke(this, e);
    }
}

/// <summary>
/// Preset scan types for quick setup
/// </summary>
public enum ScanPresetType
{
    HighVolume,
    Momentum,
    Oversold,
    ValueStocks,
    GrowthStocks,
    Breakouts,
    Dividends,
    SmallCap,
    LargeCap,
    TechnicalBreakouts,
    FundamentalValue
}
