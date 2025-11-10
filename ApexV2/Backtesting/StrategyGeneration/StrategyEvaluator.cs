using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Backtesting.Engine;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.StrategyGeneration
{
    /// <summary>
    /// Evaluates and ranks strategies based on multiple performance criteria
    /// </summary>
    public class StrategyEvaluator
    {
        private readonly BacktestEngine _backtestEngine;
        private readonly PerformanceAnalyzer _performanceAnalyzer;
        private readonly EvaluationConfig _config;

        public StrategyEvaluator(BacktestEngine backtestEngine, EvaluationConfig config)
        {
            _backtestEngine = backtestEngine ?? throw new ArgumentNullException(nameof(backtestEngine));
            _performanceAnalyzer = new PerformanceAnalyzer();
            _config = config ?? new EvaluationConfig();
        }

        /// <summary>
        /// Evaluate a list of strategies and return ranked results
        /// </summary>
        public List<StrategyEvaluationResult> EvaluateAndRankStrategies(
            List<Strategy> strategies,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> preCalculatedIndicators = null)
        {
            var results = new List<StrategyEvaluationResult>();

            foreach (var strategy in strategies)
            {
                try
                {
                    var evaluation = EvaluateStrategy(strategy, marketData, preCalculatedIndicators);
                    results.Add(evaluation);
                }
                catch (Exception ex)
                {
                    // Log error but continue with other strategies
                    Console.WriteLine($"Error evaluating strategy {strategy.Name}: {ex.Message}");
                }
            }

            // Rank strategies by composite score
            results = results.OrderByDescending(r => r.CompositeScore).ToList();

            // Assign ranks
            for (int i = 0; i < results.Count; i++)
            {
                results[i].Rank = i + 1;
            }

            return results;
        }

        /// <summary>
        /// Evaluate a single strategy
        /// </summary>
        public StrategyEvaluationResult EvaluateStrategy(
            Strategy strategy,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> preCalculatedIndicators = null)
        {
            // Generate signals from strategy
            var signals = GenerateSignalsFromStrategy(strategy, marketData, preCalculatedIndicators);

            // Run backtest
            var backtestResults = _backtestEngine.RunBacktest(marketData, signals, preCalculatedIndicators);

            // Calculate performance metrics
            var metrics = _performanceAnalyzer.CalculateMetrics(backtestResults);

            // Calculate composite score
            double compositeScore = CalculateCompositeScore(metrics);

            // Perform robustness tests if configured
            RobustnessMetrics robustness = null;
            if (_config.PerformRobustnessTests)
            {
                robustness = PerformRobustnessTests(strategy, marketData, preCalculatedIndicators);
            }

            return new StrategyEvaluationResult
            {
                Strategy = strategy,
                BacktestResults = backtestResults,
                PerformanceMetrics = metrics,
                CompositeScore = compositeScore,
                RobustnessMetrics = robustness
            };
        }

        /// <summary>
        /// Generate trading signals from strategy rules
        /// </summary>
        private StrategySignals GenerateSignalsFromStrategy(
            Strategy strategy,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> preCalculatedIndicators)
        {
            int length = marketData.Close.Length;
            var entrySignals = new bool[length];
            var exitSignals = new bool[length];

            for (int i = 0; i < length; i++)
            {
                // Evaluate entry rules
                bool entryConditionMet = EvaluateRules(strategy.EntryRules, i, marketData, preCalculatedIndicators);
                entrySignals[i] = entryConditionMet;

                // Evaluate exit rules
                bool exitConditionMet = EvaluateRules(strategy.ExitRules, i, marketData, preCalculatedIndicators);
                exitSignals[i] = exitConditionMet;
            }

            return new StrategySignals
            {
                EntrySignals = entrySignals,
                ExitSignals = exitSignals
            };
        }

        /// <summary>
        /// Evaluate a list of rules with logical operators
        /// </summary>
        private bool EvaluateRules(
            List<StrategyRule> rules,
            int index,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> indicators)
        {
            if (rules == null || rules.Count == 0)
                return false;

            // Evaluate first rule
            bool result = EvaluateRule(rules[0], index, marketData, indicators);

            // Apply logical operators for subsequent rules
            for (int i = 1; i < rules.Count; i++)
            {
                bool ruleResult = EvaluateRule(rules[i], index, marketData, indicators);

                // Apply logical operator (default to AND)
                var logicalOp = i < rules.Count ? LogicalOperator.And : LogicalOperator.And;

                if (logicalOp == LogicalOperator.And)
                {
                    result = result && ruleResult;
                }
                else if (logicalOp == LogicalOperator.Or)
                {
                    result = result || ruleResult;
                }
            }

            return result;
        }

        /// <summary>
        /// Evaluate a single rule
        /// </summary>
        private bool EvaluateRule(
            StrategyRule rule,
            int index,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> indicators)
        {
            // Need at least 2 bars for crossover detection
            if (index < 1)
                return false;

            // Get indicator values
            if (!indicators.TryGetValue(rule.IndicatorName, out var indicatorData))
                return false;

            switch (rule.Condition)
            {
                case RuleCondition.Oversold:
                    return EvaluateOversold(rule, index, indicatorData);

                case RuleCondition.Overbought:
                    return EvaluateOverbought(rule, index, indicatorData);

                case RuleCondition.PriceCrossAbove:
                    return EvaluatePriceCrossAbove(rule, index, marketData, indicatorData);

                case RuleCondition.PriceCrossBelow:
                    return EvaluatePriceCrossBelow(rule, index, marketData, indicatorData);

                case RuleCondition.IndicatorCrossAbove:
                    return EvaluateIndicatorCrossAbove(rule, index, indicatorData);

                case RuleCondition.IndicatorCrossBelow:
                    return EvaluateIndicatorCrossBelow(rule, index, indicatorData);

                case RuleCondition.PriceAbove:
                    return EvaluatePriceAbove(rule, index, marketData, indicatorData);

                case RuleCondition.PriceBelow:
                    return EvaluatePriceBelow(rule, index, marketData, indicatorData);

                case RuleCondition.Rising:
                    return EvaluateRising(rule, index, indicatorData);

                case RuleCondition.Falling:
                    return EvaluateFalling(rule, index, indicatorData);

                case RuleCondition.Positive:
                    return EvaluatePositive(rule, index, indicatorData);

                case RuleCondition.Negative:
                    return EvaluateNegative(rule, index, indicatorData);

                case RuleCondition.Above:
                    return EvaluateAboveThreshold(rule, index, indicatorData);

                case RuleCondition.Below:
                    return EvaluateBelowThreshold(rule, index, indicatorData);

                default:
                    return false;
            }
        }

        #region Rule Evaluation Methods

        private bool EvaluateOversold(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            var values = indicatorData.ContainsKey("value") ? indicatorData["value"] :
                         indicatorData.ContainsKey("k") ? indicatorData["k"] : null;

            if (values == null || double.IsNaN(values[index]))
                return false;

            double oversoldLevel = rule.Parameters.ContainsKey("oversold")
                ? Convert.ToDouble(rule.Parameters["oversold"])
                : 30;

            return values[index] < oversoldLevel;
        }

        private bool EvaluateOverbought(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            var values = indicatorData.ContainsKey("value") ? indicatorData["value"] :
                         indicatorData.ContainsKey("k") ? indicatorData["k"] : null;

            if (values == null || double.IsNaN(values[index]))
                return false;

            double overboughtLevel = rule.Parameters.ContainsKey("overbought")
                ? Convert.ToDouble(rule.Parameters["overbought"])
                : 70;

            return values[index] > overboughtLevel;
        }

        private bool EvaluatePriceCrossAbove(StrategyRule rule, int index, MarketData marketData, Dictionary<string, double[]> indicatorData)
        {
            if (!indicatorData.ContainsKey("value"))
                return false;

            var indicatorValues = indicatorData["value"];
            if (double.IsNaN(indicatorValues[index]) || double.IsNaN(indicatorValues[index - 1]))
                return false;

            return marketData.Close[index - 1] <= indicatorValues[index - 1] &&
                   marketData.Close[index] > indicatorValues[index];
        }

        private bool EvaluatePriceCrossBelow(StrategyRule rule, int index, MarketData marketData, Dictionary<string, double[]> indicatorData)
        {
            if (!indicatorData.ContainsKey("value"))
                return false;

            var indicatorValues = indicatorData["value"];
            if (double.IsNaN(indicatorValues[index]) || double.IsNaN(indicatorValues[index - 1]))
                return false;

            return marketData.Close[index - 1] >= indicatorValues[index - 1] &&
                   marketData.Close[index] < indicatorValues[index];
        }

        private bool EvaluateIndicatorCrossAbove(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            // For MACD: macd crosses above signal
            // For Stochastic: %K crosses above %D
            if (indicatorData.ContainsKey("macd") && indicatorData.ContainsKey("signal"))
            {
                var macd = indicatorData["macd"];
                var signal = indicatorData["signal"];
                return macd[index - 1] <= signal[index - 1] && macd[index] > signal[index];
            }
            else if (indicatorData.ContainsKey("k") && indicatorData.ContainsKey("d"))
            {
                var k = indicatorData["k"];
                var d = indicatorData["d"];
                return k[index - 1] <= d[index - 1] && k[index] > d[index];
            }

            return false;
        }

        private bool EvaluateIndicatorCrossBelow(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            if (indicatorData.ContainsKey("macd") && indicatorData.ContainsKey("signal"))
            {
                var macd = indicatorData["macd"];
                var signal = indicatorData["signal"];
                return macd[index - 1] >= signal[index - 1] && macd[index] < signal[index];
            }
            else if (indicatorData.ContainsKey("k") && indicatorData.ContainsKey("d"))
            {
                var k = indicatorData["k"];
                var d = indicatorData["d"];
                return k[index - 1] >= d[index - 1] && k[index] < d[index];
            }

            return false;
        }

        private bool EvaluatePriceAbove(StrategyRule rule, int index, MarketData marketData, Dictionary<string, double[]> indicatorData)
        {
            if (!indicatorData.ContainsKey("upper") && !indicatorData.ContainsKey("value"))
                return false;

            var bandValues = indicatorData.ContainsKey("upper") ? indicatorData["upper"] : indicatorData["value"];
            if (double.IsNaN(bandValues[index]))
                return false;

            return marketData.Close[index] > bandValues[index];
        }

        private bool EvaluatePriceBelow(StrategyRule rule, int index, MarketData marketData, Dictionary<string, double[]> indicatorData)
        {
            if (!indicatorData.ContainsKey("lower") && !indicatorData.ContainsKey("value"))
                return false;

            var bandValues = indicatorData.ContainsKey("lower") ? indicatorData["lower"] : indicatorData["value"];
            if (double.IsNaN(bandValues[index]))
                return false;

            return marketData.Close[index] < bandValues[index];
        }

        private bool EvaluateRising(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            var values = indicatorData.ContainsKey("value") ? indicatorData["value"] : null;
            if (values == null || double.IsNaN(values[index]) || double.IsNaN(values[index - 1]))
                return false;

            return values[index] > values[index - 1];
        }

        private bool EvaluateFalling(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            var values = indicatorData.ContainsKey("value") ? indicatorData["value"] : null;
            if (values == null || double.IsNaN(values[index]) || double.IsNaN(values[index - 1]))
                return false;

            return values[index] < values[index - 1];
        }

        private bool EvaluatePositive(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            var values = indicatorData.ContainsKey("value") ? indicatorData["value"] :
                         indicatorData.ContainsKey("macd") ? indicatorData["macd"] : null;

            if (values == null || double.IsNaN(values[index]))
                return false;

            return values[index] > 0;
        }

        private bool EvaluateNegative(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            var values = indicatorData.ContainsKey("value") ? indicatorData["value"] :
                         indicatorData.ContainsKey("macd") ? indicatorData["macd"] : null;

            if (values == null || double.IsNaN(values[index]))
                return false;

            return values[index] < 0;
        }

        private bool EvaluateAboveThreshold(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            var values = indicatorData.ContainsKey("value") ? indicatorData["value"] : null;
            if (values == null || double.IsNaN(values[index]))
                return false;

            double threshold = rule.Parameters.ContainsKey("trend_threshold")
                ? Convert.ToDouble(rule.Parameters["trend_threshold"])
                : 25;

            return values[index] > threshold;
        }

        private bool EvaluateBelowThreshold(StrategyRule rule, int index, Dictionary<string, double[]> indicatorData)
        {
            var values = indicatorData.ContainsKey("value") ? indicatorData["value"] : null;
            if (values == null || double.IsNaN(values[index]))
                return false;

            double threshold = rule.Parameters.ContainsKey("trend_threshold")
                ? Convert.ToDouble(rule.Parameters["trend_threshold"])
                : 25;

            return values[index] < threshold;
        }

        #endregion

        /// <summary>
        /// Calculate composite score from multiple metrics
        /// </summary>
        private double CalculateCompositeScore(PerformanceMetrics metrics)
        {
            double score = 0;
            double totalWeight = 0;

            // Sharpe Ratio (higher is better)
            if (_config.Weights.ContainsKey("sharpe_ratio"))
            {
                double weight = _config.Weights["sharpe_ratio"];
                score += NormalizeMetric(metrics.SharpeRatio, 0, 3) * weight;
                totalWeight += weight;
            }

            // Sortino Ratio (higher is better)
            if (_config.Weights.ContainsKey("sortino_ratio"))
            {
                double weight = _config.Weights["sortino_ratio"];
                score += NormalizeMetric(metrics.SortinoRatio, 0, 3) * weight;
                totalWeight += weight;
            }

            // Profit Factor (higher is better)
            if (_config.Weights.ContainsKey("profit_factor"))
            {
                double weight = _config.Weights["profit_factor"];
                score += NormalizeMetric(metrics.ProfitFactor, 1, 3) * weight;
                totalWeight += weight;
            }

            // Max Drawdown (lower is better, so invert)
            if (_config.Weights.ContainsKey("max_drawdown"))
            {
                double weight = _config.Weights["max_drawdown"];
                score += (1 - NormalizeMetric(Math.Abs(metrics.MaxDrawdown), 0, 0.5)) * weight;
                totalWeight += weight;
            }

            // Win Rate (higher is better)
            if (_config.Weights.ContainsKey("win_rate"))
            {
                double weight = _config.Weights["win_rate"];
                score += NormalizeMetric(metrics.WinRate, 0.3, 0.7) * weight;
                totalWeight += weight;
            }

            // Total Return (higher is better)
            if (_config.Weights.ContainsKey("total_return"))
            {
                double weight = _config.Weights["total_return"];
                score += NormalizeMetric(metrics.TotalReturn, 0, 1) * weight;
                totalWeight += weight;
            }

            return totalWeight > 0 ? (score / totalWeight) * 100 : 0;
        }

        /// <summary>
        /// Normalize metric to 0-1 range
        /// </summary>
        private double NormalizeMetric(double value, double min, double max)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                return 0;

            if (value < min) return 0;
            if (value > max) return 1;

            return (value - min) / (max - min);
        }

        /// <summary>
        /// Perform robustness tests (Monte Carlo, Walk-Forward)
        /// </summary>
        private RobustnessMetrics PerformRobustnessTests(
            Strategy strategy,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> preCalculatedIndicators)
        {
            var robustness = new RobustnessMetrics();

            // Monte Carlo simulation
            if (_config.MonteCarloIterations > 0)
            {
                robustness.MonteCarloResults = RunMonteCarloSimulation(strategy, marketData, preCalculatedIndicators);
            }

            // Walk-forward analysis
            if (_config.WalkForwardPeriods > 0)
            {
                robustness.WalkForwardResults = RunWalkForwardAnalysis(strategy, marketData, preCalculatedIndicators);
            }

            return robustness;
        }

        /// <summary>
        /// Run Monte Carlo simulation by randomizing trade order
        /// </summary>
        private MonteCarloResult RunMonteCarloSimulation(
            Strategy strategy,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> preCalculatedIndicators)
        {
            var random = new Random(42); // Seed for reproducibility
            var allReturns = new List<double>();

            // Run base backtest to get trades
            var signals = GenerateSignalsFromStrategy(strategy, marketData, preCalculatedIndicators);
            var baseResults = _backtestEngine.RunBacktest(marketData, signals, preCalculatedIndicators);

            if (baseResults.Trades.Count < 10)
            {
                return new MonteCarloResult
                {
                    MeanReturn = baseResults.TotalReturn,
                    StdDevReturn = 0,
                    PercentProfitable = 100
                };
            }

            // Extract trade returns
            var tradeReturns = baseResults.Trades.Select(t => t.ReturnPct).ToList();

            // Run Monte Carlo iterations
            for (int i = 0; i < _config.MonteCarloIterations; i++)
            {
                // Shuffle trade order
                var shuffledReturns = tradeReturns.OrderBy(x => random.Next()).ToList();

                // Calculate cumulative return for this iteration
                double cumulativeReturn = shuffledReturns.Sum();
                allReturns.Add(cumulativeReturn);
            }

            // Calculate statistics
            double meanReturn = allReturns.Average();
            double variance = allReturns.Select(r => Math.Pow(r - meanReturn, 2)).Average();
            double stdDev = Math.Sqrt(variance);
            double percentProfitable = (allReturns.Count(r => r > 0) / (double)allReturns.Count) * 100;

            return new MonteCarloResult
            {
                MeanReturn = meanReturn,
                StdDevReturn = stdDev,
                PercentProfitable = percentProfitable
            };
        }

        /// <summary>
        /// Run walk-forward analysis
        /// </summary>
        private WalkForwardResult RunWalkForwardAnalysis(
            Strategy strategy,
            MarketData marketData,
            Dictionary<string, Dictionary<string, double[]>> preCalculatedIndicators)
        {
            int dataLength = marketData.Close.Length;
            int periodLength = dataLength / _config.WalkForwardPeriods;

            var periodResults = new List<double>();

            for (int i = 0; i < _config.WalkForwardPeriods; i++)
            {
                int startIdx = i * periodLength;
                int endIdx = Math.Min((i + 1) * periodLength, dataLength);

                // Create subset of market data
                var subsetData = new MarketData
                {
                    Symbol = marketData.Symbol,
                    Timestamps = marketData.Timestamps.Skip(startIdx).Take(endIdx - startIdx).ToArray(),
                    Open = marketData.Open.Skip(startIdx).Take(endIdx - startIdx).ToArray(),
                    High = marketData.High.Skip(startIdx).Take(endIdx - startIdx).ToArray(),
                    Low = marketData.Low.Skip(startIdx).Take(endIdx - startIdx).ToArray(),
                    Close = marketData.Close.Skip(startIdx).Take(endIdx - startIdx).ToArray(),
                    Volume = marketData.Volume.Skip(startIdx).Take(endIdx - startIdx).ToArray()
                };

                // Run backtest on this period
                var signals = GenerateSignalsFromStrategy(strategy, subsetData, null);
                var results = _backtestEngine.RunBacktest(subsetData, signals, null);

                periodResults.Add(results.TotalReturn);
            }

            double avgReturn = periodResults.Average();
            double variance = periodResults.Select(r => Math.Pow(r - avgReturn, 2)).Average();
            double consistency = Math.Sqrt(variance);
            int profitablePeriods = periodResults.Count(r => r > 0);

            return new WalkForwardResult
            {
                PeriodReturns = periodResults,
                AverageReturn = avgReturn,
                Consistency = consistency,
                ProfitablePeriods = profitablePeriods,
                TotalPeriods = _config.WalkForwardPeriods
            };
        }
    }

    #region Supporting Classes

    public class EvaluationConfig
    {
        public Dictionary<string, double> Weights { get; set; } = new Dictionary<string, double>
        {
            { "sharpe_ratio", 0.25 },
            { "sortino_ratio", 0.20 },
            { "profit_factor", 0.20 },
            { "max_drawdown", 0.15 },
            { "win_rate", 0.10 },
            { "total_return", 0.10 }
        };

        public bool PerformRobustnessTests { get; set; } = true;
        public int MonteCarloIterations { get; set; } = 1000;
        public int WalkForwardPeriods { get; set; } = 5;
    }

    public class StrategyEvaluationResult
    {
        public int Rank { get; set; }
        public Strategy Strategy { get; set; }
        public BacktestResults BacktestResults { get; set; }
        public PerformanceMetrics PerformanceMetrics { get; set; }
        public double CompositeScore { get; set; }
        public RobustnessMetrics RobustnessMetrics { get; set; }
    }

    public class RobustnessMetrics
    {
        public MonteCarloResult MonteCarloResults { get; set; }
        public WalkForwardResult WalkForwardResults { get; set; }
    }

    public class MonteCarloResult
    {
        public double MeanReturn { get; set; }
        public double StdDevReturn { get; set; }
        public double PercentProfitable { get; set; }
    }

    public class WalkForwardResult
    {
        public List<double> PeriodReturns { get; set; }
        public double AverageReturn { get; set; }
        public double Consistency { get; set; }
        public int ProfitablePeriods { get; set; }
        public int TotalPeriods { get; set; }
    }

    #endregion
}
