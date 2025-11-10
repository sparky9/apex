using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Backtesting.Engine;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.StrategyGeneration
{
    /// <summary>
    /// Efficiently processes and evaluates large batches of strategies
    /// </summary>
    public class StrategyBatchProcessor
    {
        private readonly BacktestEngine _backtestEngine;
        private readonly StrategyEvaluator _evaluator;
        private readonly IndicatorCalculator _indicatorCalculator;
        private readonly EvaluationConfig _config;

        public event EventHandler<BatchProgressEventArgs> ProgressUpdated;

        public StrategyBatchProcessor(
            BacktestEngine backtestEngine,
            MarketData marketData,
            EvaluationConfig config = null)
        {
            _backtestEngine = backtestEngine ?? throw new ArgumentNullException(nameof(backtestEngine));
            _config = config ?? new EvaluationConfig();
            _evaluator = new StrategyEvaluator(_backtestEngine, _config);
            _indicatorCalculator = new IndicatorCalculator(marketData);
        }

        /// <summary>
        /// Process strategies in batches with pre-calculated indicators
        /// </summary>
        public List<StrategyEvaluationResult> ProcessBatch(
            List<Strategy> strategies,
            MarketData marketData,
            bool useParallel = true,
            int batchSize = 100)
        {
            var allResults = new List<StrategyEvaluationResult>();
            int totalStrategies = strategies.Count;
            int processed = 0;

            // Pre-calculate common indicators to avoid redundant calculations
            OnProgressUpdated(new BatchProgressEventArgs
            {
                Phase = "Calculating Indicators",
                Processed = 0,
                Total = totalStrategies,
                CurrentStrategy = "Pre-calculating common indicators..."
            });

            var commonIndicators = _indicatorCalculator.CalculateAllIndicators();

            // Process in batches
            for (int i = 0; i < strategies.Count; i += batchSize)
            {
                var batch = strategies.Skip(i).Take(batchSize).ToList();

                OnProgressUpdated(new BatchProgressEventArgs
                {
                    Phase = "Evaluating Strategies",
                    Processed = processed,
                    Total = totalStrategies,
                    CurrentStrategy = $"Batch {(i / batchSize) + 1} of {(totalStrategies + batchSize - 1) / batchSize}"
                });

                List<StrategyEvaluationResult> batchResults;

                if (useParallel)
                {
                    batchResults = ProcessBatchParallel(batch, marketData, commonIndicators);
                }
                else
                {
                    batchResults = ProcessBatchSequential(batch, marketData, commonIndicators);
                }

                allResults.AddRange(batchResults);
                processed += batch.Count;

                OnProgressUpdated(new BatchProgressEventArgs
                {
                    Phase = "Evaluating Strategies",
                    Processed = processed,
                    Total = totalStrategies,
                    CurrentStrategy = $"Completed {processed} of {totalStrategies}"
                });
            }

            // Rank all results
            OnProgressUpdated(new BatchProgressEventArgs
            {
                Phase = "Ranking Results",
                Processed = totalStrategies,
                Total = totalStrategies,
                CurrentStrategy = "Ranking strategies by performance..."
            });

            allResults = allResults.OrderByDescending(r => r.CompositeScore).ToList();
            for (int i = 0; i < allResults.Count; i++)
            {
                allResults[i].Rank = i + 1;
            }

            OnProgressUpdated(new BatchProgressEventArgs
            {
                Phase = "Complete",
                Processed = totalStrategies,
                Total = totalStrategies,
                CurrentStrategy = $"Completed evaluation of {totalStrategies} strategies"
            });

            return allResults;
        }

        /// <summary>
        /// Process batch sequentially
        /// </summary>
        private List<StrategyEvaluationResult> ProcessBatchSequential(
            List<Strategy> strategies,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> commonIndicators)
        {
            var results = new List<StrategyEvaluationResult>();

            foreach (var strategy in strategies)
            {
                try
                {
                    // Calculate strategy-specific indicators if needed
                    var indicators = GetStrategyIndicators(strategy, commonIndicators);

                    var result = _evaluator.EvaluateStrategy(strategy, marketData, indicators);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error evaluating strategy {strategy.Name}: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Process batch in parallel for better performance
        /// </summary>
        private List<StrategyEvaluationResult> ProcessBatchParallel(
            List<Strategy> strategies,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> commonIndicators)
        {
            var results = new System.Collections.Concurrent.ConcurrentBag<StrategyEvaluationResult>();

            Parallel.ForEach(strategies, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                strategy =>
                {
                    try
                    {
                        var indicators = GetStrategyIndicators(strategy, commonIndicators);
                        var result = _evaluator.EvaluateStrategy(strategy, marketData, indicators);
                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error evaluating strategy {strategy.Name}: {ex.Message}");
                    }
                });

            return results.ToList();
        }

        /// <summary>
        /// Get indicators for a specific strategy, using pre-calculated ones when available
        /// </summary>
        private Dictionary<string, Dictionary<string, double[]>> GetStrategyIndicators(
            Strategy strategy,
            Dictionary<string, Dictionary<string, double[]>> commonIndicators)
        {
            var indicators = new Dictionary<string, Dictionary<string, double[]>>();

            // Collect all required indicators from rules
            var allRules = new List<StrategyRule>();
            if (strategy.EntryRules != null)
                allRules.AddRange(strategy.EntryRules);
            if (strategy.ExitRules != null)
                allRules.AddRange(strategy.ExitRules);

            foreach (var rule in allRules)
            {
                // Check if we have pre-calculated values with matching parameters
                if (commonIndicators.ContainsKey(rule.IndicatorName))
                {
                    // Check if parameters match standard parameters
                    if (ParametersMatchStandard(rule.IndicatorName, rule.Parameters))
                    {
                        indicators[rule.IndicatorName] = commonIndicators[rule.IndicatorName];
                        continue;
                    }
                }

                // Calculate with specific parameters
                if (!indicators.ContainsKey(rule.IndicatorName))
                {
                    var indicatorData = _indicatorCalculator.CalculateIndicatorWithParameters(
                        rule.IndicatorName,
                        rule.Parameters);

                    if (indicatorData != null)
                    {
                        indicators[rule.IndicatorName] = indicatorData;
                    }
                }
            }

            return indicators;
        }

        /// <summary>
        /// Check if parameters match standard/default values
        /// </summary>
        private bool ParametersMatchStandard(string indicatorName, Dictionary<string, object> parameters)
        {
            // Standard parameters for common indicators
            var standards = new Dictionary<string, Dictionary<string, object>>
            {
                {
                    "RSI", new Dictionary<string, object>
                    {
                        { "window", 14 }, { "overbought", 70.0 }, { "oversold", 30.0 }
                    }
                },
                {
                    "MACD", new Dictionary<string, object>
                    {
                        { "fast_period", 12 }, { "slow_period", 26 }, { "signal_period", 9 }
                    }
                },
                {
                    "BB", new Dictionary<string, object>
                    {
                        { "window", 20 }, { "std_dev", 2.0 }
                    }
                },
                {
                    "ATR", new Dictionary<string, object>
                    {
                        { "window", 14 }
                    }
                },
                {
                    "STOCH", new Dictionary<string, object>
                    {
                        { "k_window", 14 }, { "d_window", 3 }, { "smooth_k", 3 },
                        { "overbought", 80.0 }, { "oversold", 20.0 }
                    }
                },
                {
                    "SMA", new Dictionary<string, object>
                    {
                        { "window", 50 }
                    }
                },
                {
                    "EMA", new Dictionary<string, object>
                    {
                        { "window", 20 }
                    }
                }
            };

            if (!standards.ContainsKey(indicatorName))
                return false;

            var standardParams = standards[indicatorName];

            // Check if all standard parameters match
            foreach (var kvp in standardParams)
            {
                if (!parameters.ContainsKey(kvp.Key))
                    return false;

                var paramValue = parameters[kvp.Key];
                if (!paramValue.Equals(kvp.Value))
                {
                    // Try numeric comparison
                    double paramDouble = Convert.ToDouble(paramValue);
                    double standardDouble = Convert.ToDouble(kvp.Value);

                    if (Math.Abs(paramDouble - standardDouble) > 0.001)
                        return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get top N strategies by composite score
        /// </summary>
        public List<StrategyEvaluationResult> GetTopStrategies(
            List<StrategyEvaluationResult> results,
            int count)
        {
            return results
                .OrderByDescending(r => r.CompositeScore)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Filter strategies by criteria
        /// </summary>
        public List<StrategyEvaluationResult> FilterStrategies(
            List<StrategyEvaluationResult> results,
            FilterCriteria criteria)
        {
            return results.Where(r =>
            {
                var metrics = r.PerformanceMetrics;

                if (criteria.MinSharpeRatio.HasValue && metrics.SharpeRatio < criteria.MinSharpeRatio.Value)
                    return false;

                if (criteria.MinProfitFactor.HasValue && metrics.ProfitFactor < criteria.MinProfitFactor.Value)
                    return false;

                if (criteria.MaxDrawdown.HasValue && Math.Abs(metrics.MaxDrawdown) > Math.Abs(criteria.MaxDrawdown.Value))
                    return false;

                if (criteria.MinWinRate.HasValue && metrics.WinRate < criteria.MinWinRate.Value)
                    return false;

                if (criteria.MinTotalReturn.HasValue && metrics.TotalReturn < criteria.MinTotalReturn.Value)
                    return false;

                if (criteria.MinTrades.HasValue && metrics.TotalTrades < criteria.MinTrades.Value)
                    return false;

                return true;
            }).ToList();
        }

        protected virtual void OnProgressUpdated(BatchProgressEventArgs e)
        {
            ProgressUpdated?.Invoke(this, e);
        }
    }

    #region Supporting Classes

    public class BatchProgressEventArgs : EventArgs
    {
        public string Phase { get; set; }
        public int Processed { get; set; }
        public int Total { get; set; }
        public string CurrentStrategy { get; set; }

        public double PercentComplete => Total > 0 ? (Processed / (double)Total) * 100 : 0;
    }

    public class FilterCriteria
    {
        public double? MinSharpeRatio { get; set; }
        public double? MinProfitFactor { get; set; }
        public double? MaxDrawdown { get; set; }
        public double? MinWinRate { get; set; }
        public double? MinTotalReturn { get; set; }
        public int? MinTrades { get; set; }
    }

    #endregion
}
