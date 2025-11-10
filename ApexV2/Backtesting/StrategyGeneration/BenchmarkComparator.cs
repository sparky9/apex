using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Backtesting.Engine;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.StrategyGeneration
{
    /// <summary>
    /// Compares strategy performance against various benchmarks
    /// </summary>
    public class BenchmarkComparator
    {
        private readonly BacktestEngine _backtestEngine;
        private readonly PerformanceAnalyzer _performanceAnalyzer;

        public BenchmarkComparator(BacktestEngine backtestEngine)
        {
            _backtestEngine = backtestEngine ?? throw new ArgumentNullException(nameof(backtestEngine));
            _performanceAnalyzer = new PerformanceAnalyzer();
        }

        /// <summary>
        /// Compare strategy against buy-and-hold benchmark
        /// </summary>
        public BenchmarkComparison CompareToBuyAndHold(
            StrategyEvaluationResult strategyResult,
            MarketData marketData)
        {
            // Create buy-and-hold signals (buy at start, hold till end)
            var buyAndHoldSignals = CreateBuyAndHoldSignals(marketData.Close.Length);

            // Run buy-and-hold backtest
            var buyAndHoldResults = _backtestEngine.RunBacktest(
                marketData,
                buyAndHoldSignals,
                null);

            var buyAndHoldMetrics = _performanceAnalyzer.CalculateMetrics(buyAndHoldResults);

            // Calculate relative metrics
            return new BenchmarkComparison
            {
                BenchmarkName = "Buy and Hold",
                StrategyMetrics = strategyResult.PerformanceMetrics,
                BenchmarkMetrics = buyAndHoldMetrics,
                RelativeReturn = strategyResult.PerformanceMetrics.TotalReturn - buyAndHoldMetrics.TotalReturn,
                RelativeSharpe = strategyResult.PerformanceMetrics.SharpeRatio - buyAndHoldMetrics.SharpeRatio,
                RelativeDrawdown = strategyResult.PerformanceMetrics.MaxDrawdown - buyAndHoldMetrics.MaxDrawdown,
                Alpha = CalculateAlpha(strategyResult.PerformanceMetrics, buyAndHoldMetrics),
                Beta = CalculateBeta(strategyResult.BacktestResults, buyAndHoldResults),
                InformationRatio = CalculateInformationRatio(strategyResult.BacktestResults, buyAndHoldResults)
            };
        }

        /// <summary>
        /// Compare strategy against multiple benchmarks
        /// </summary>
        public List<BenchmarkComparison> CompareToMultipleBenchmarks(
            StrategyEvaluationResult strategyResult,
            MarketData marketData,
            List<BenchmarkType> benchmarkTypes)
        {
            var comparisons = new List<BenchmarkComparison>();

            foreach (var benchmarkType in benchmarkTypes)
            {
                BenchmarkComparison comparison = benchmarkType switch
                {
                    BenchmarkType.BuyAndHold => CompareToBuyAndHold(strategyResult, marketData),
                    BenchmarkType.SimpleMovingAverage => CompareToSMAStrategy(strategyResult, marketData, 50, 200),
                    BenchmarkType.RSIMeanReversion => CompareToRSIStrategy(strategyResult, marketData),
                    _ => null
                };

                if (comparison != null)
                {
                    comparisons.Add(comparison);
                }
            }

            return comparisons;
        }

        /// <summary>
        /// Compare against SMA crossover strategy
        /// </summary>
        private BenchmarkComparison CompareToSMAStrategy(
            StrategyEvaluationResult strategyResult,
            MarketData marketData,
            int fastPeriod,
            int slowPeriod)
        {
            // Calculate SMAs
            var fastSMA = CalculateSMA(marketData.Close, fastPeriod);
            var slowSMA = CalculateSMA(marketData.Close, slowPeriod);

            // Generate signals
            var signals = GenerateSMACrossoverSignals(fastSMA, slowSMA);

            // Run backtest
            var benchmarkResults = _backtestEngine.RunBacktest(marketData, signals, null);
            var benchmarkMetrics = _performanceAnalyzer.CalculateMetrics(benchmarkResults);

            return new BenchmarkComparison
            {
                BenchmarkName = $"SMA {fastPeriod}/{slowPeriod} Crossover",
                StrategyMetrics = strategyResult.PerformanceMetrics,
                BenchmarkMetrics = benchmarkMetrics,
                RelativeReturn = strategyResult.PerformanceMetrics.TotalReturn - benchmarkMetrics.TotalReturn,
                RelativeSharpe = strategyResult.PerformanceMetrics.SharpeRatio - benchmarkMetrics.SharpeRatio,
                RelativeDrawdown = strategyResult.PerformanceMetrics.MaxDrawdown - benchmarkMetrics.MaxDrawdown,
                Alpha = CalculateAlpha(strategyResult.PerformanceMetrics, benchmarkMetrics),
                Beta = CalculateBeta(strategyResult.BacktestResults, benchmarkResults),
                InformationRatio = CalculateInformationRatio(strategyResult.BacktestResults, benchmarkResults)
            };
        }

        /// <summary>
        /// Compare against RSI mean reversion strategy
        /// </summary>
        private BenchmarkComparison CompareToRSIStrategy(
            StrategyEvaluationResult strategyResult,
            MarketData marketData)
        {
            // Calculate RSI
            var rsi = CalculateRSI(marketData.Close, 14);

            // Generate signals (buy oversold, sell overbought)
            var signals = GenerateRSISignals(rsi, 30, 70);

            // Run backtest
            var benchmarkResults = _backtestEngine.RunBacktest(marketData, signals, null);
            var benchmarkMetrics = _performanceAnalyzer.CalculateMetrics(benchmarkResults);

            return new BenchmarkComparison
            {
                BenchmarkName = "RSI Mean Reversion (14, 30/70)",
                StrategyMetrics = strategyResult.PerformanceMetrics,
                BenchmarkMetrics = benchmarkMetrics,
                RelativeReturn = strategyResult.PerformanceMetrics.TotalReturn - benchmarkMetrics.TotalReturn,
                RelativeSharpe = strategyResult.PerformanceMetrics.SharpeRatio - benchmarkMetrics.SharpeRatio,
                RelativeDrawdown = strategyResult.PerformanceMetrics.MaxDrawdown - benchmarkMetrics.MaxDrawdown,
                Alpha = CalculateAlpha(strategyResult.PerformanceMetrics, benchmarkMetrics),
                Beta = CalculateBeta(strategyResult.BacktestResults, benchmarkResults),
                InformationRatio = CalculateInformationRatio(strategyResult.BacktestResults, benchmarkResults)
            };
        }

        #region Signal Generation

        private StrategySignals CreateBuyAndHoldSignals(int length)
        {
            var entrySignals = new bool[length];
            var exitSignals = new bool[length];

            // Buy at first bar
            entrySignals[0] = true;

            // Hold till end (no exit signals)
            return new StrategySignals
            {
                EntrySignals = entrySignals,
                ExitSignals = exitSignals
            };
        }

        private StrategySignals GenerateSMACrossoverSignals(double[] fastSMA, double[] slowSMA)
        {
            int length = fastSMA.Length;
            var entrySignals = new bool[length];
            var exitSignals = new bool[length];

            for (int i = 1; i < length; i++)
            {
                if (!double.IsNaN(fastSMA[i]) && !double.IsNaN(slowSMA[i]) &&
                    !double.IsNaN(fastSMA[i - 1]) && !double.IsNaN(slowSMA[i - 1]))
                {
                    // Bullish crossover
                    if (fastSMA[i - 1] <= slowSMA[i - 1] && fastSMA[i] > slowSMA[i])
                    {
                        entrySignals[i] = true;
                    }

                    // Bearish crossover
                    if (fastSMA[i - 1] >= slowSMA[i - 1] && fastSMA[i] < slowSMA[i])
                    {
                        exitSignals[i] = true;
                    }
                }
            }

            return new StrategySignals
            {
                EntrySignals = entrySignals,
                ExitSignals = exitSignals
            };
        }

        private StrategySignals GenerateRSISignals(double[] rsi, double oversoldLevel, double overboughtLevel)
        {
            int length = rsi.Length;
            var entrySignals = new bool[length];
            var exitSignals = new bool[length];

            for (int i = 0; i < length; i++)
            {
                if (!double.IsNaN(rsi[i]))
                {
                    // Buy when oversold
                    if (rsi[i] < oversoldLevel)
                    {
                        entrySignals[i] = true;
                    }

                    // Sell when overbought
                    if (rsi[i] > overboughtLevel)
                    {
                        exitSignals[i] = true;
                    }
                }
            }

            return new StrategySignals
            {
                EntrySignals = entrySignals,
                ExitSignals = exitSignals
            };
        }

        #endregion

        #region Indicator Calculations

        private double[] CalculateSMA(double[] prices, int period)
        {
            int length = prices.Length;
            var sma = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (i < period - 1)
                {
                    sma[i] = double.NaN;
                    continue;
                }

                double sum = 0;
                for (int j = 0; j < period; j++)
                {
                    sum += prices[i - j];
                }

                sma[i] = sum / period;
            }

            return sma;
        }

        private double[] CalculateRSI(double[] prices, int period)
        {
            int length = prices.Length;
            var rsi = new double[length];

            for (int i = 0; i < period; i++)
            {
                rsi[i] = double.NaN;
            }

            // Calculate initial average gains and losses
            double avgGain = 0;
            double avgLoss = 0;

            for (int i = 1; i <= period; i++)
            {
                double change = prices[i] - prices[i - 1];
                if (change > 0)
                    avgGain += change;
                else
                    avgLoss += Math.Abs(change);
            }

            avgGain /= period;
            avgLoss /= period;

            // Calculate RSI
            for (int i = period; i < length; i++)
            {
                double change = prices[i] - prices[i - 1];
                double gain = change > 0 ? change : 0;
                double loss = change < 0 ? Math.Abs(change) : 0;

                avgGain = ((avgGain * (period - 1)) + gain) / period;
                avgLoss = ((avgLoss * (period - 1)) + loss) / period;

                if (avgLoss == 0)
                {
                    rsi[i] = 100;
                }
                else
                {
                    double rs = avgGain / avgLoss;
                    rsi[i] = 100 - (100 / (1 + rs));
                }
            }

            return rsi;
        }

        #endregion

        #region Performance Metrics

        /// <summary>
        /// Calculate alpha (excess return over benchmark)
        /// </summary>
        private double CalculateAlpha(PerformanceMetrics strategy, PerformanceMetrics benchmark)
        {
            // Simplified alpha calculation
            return strategy.TotalReturn - benchmark.TotalReturn;
        }

        /// <summary>
        /// Calculate beta (correlation with benchmark)
        /// </summary>
        private double CalculateBeta(BacktestResults strategy, BacktestResults benchmark)
        {
            // Need equity curves for both
            if (strategy.EquityCurve.Length != benchmark.EquityCurve.Length)
                return double.NaN;

            // Calculate returns
            var strategyReturns = CalculateReturns(strategy.EquityCurve);
            var benchmarkReturns = CalculateReturns(benchmark.EquityCurve);

            // Calculate covariance and variance
            double covariance = CalculateCovariance(strategyReturns, benchmarkReturns);
            double benchmarkVariance = CalculateVariance(benchmarkReturns);

            if (benchmarkVariance == 0)
                return double.NaN;

            return covariance / benchmarkVariance;
        }

        /// <summary>
        /// Calculate information ratio (excess return / tracking error)
        /// </summary>
        private double CalculateInformationRatio(BacktestResults strategy, BacktestResults benchmark)
        {
            if (strategy.EquityCurve.Length != benchmark.EquityCurve.Length)
                return double.NaN;

            var strategyReturns = CalculateReturns(strategy.EquityCurve);
            var benchmarkReturns = CalculateReturns(benchmark.EquityCurve);

            // Calculate excess returns
            var excessReturns = new double[strategyReturns.Length];
            for (int i = 0; i < strategyReturns.Length; i++)
            {
                excessReturns[i] = strategyReturns[i] - benchmarkReturns[i];
            }

            double meanExcessReturn = excessReturns.Average();
            double trackingError = CalculateStandardDeviation(excessReturns);

            if (trackingError == 0)
                return double.NaN;

            return meanExcessReturn / trackingError;
        }

        private double[] CalculateReturns(double[] equityCurve)
        {
            var returns = new double[equityCurve.Length - 1];

            for (int i = 1; i < equityCurve.Length; i++)
            {
                if (equityCurve[i - 1] > 0)
                {
                    returns[i - 1] = (equityCurve[i] - equityCurve[i - 1]) / equityCurve[i - 1];
                }
            }

            return returns;
        }

        private double CalculateCovariance(double[] x, double[] y)
        {
            if (x.Length != y.Length)
                return 0;

            double meanX = x.Average();
            double meanY = y.Average();

            double sum = 0;
            for (int i = 0; i < x.Length; i++)
            {
                sum += (x[i] - meanX) * (y[i] - meanY);
            }

            return sum / (x.Length - 1);
        }

        private double CalculateVariance(double[] values)
        {
            double mean = values.Average();
            double sum = values.Sum(v => Math.Pow(v - mean, 2));
            return sum / (values.Length - 1);
        }

        private double CalculateStandardDeviation(double[] values)
        {
            return Math.Sqrt(CalculateVariance(values));
        }

        #endregion
    }

    #region Supporting Classes

    public class BenchmarkComparison
    {
        public string BenchmarkName { get; set; }
        public PerformanceMetrics StrategyMetrics { get; set; }
        public PerformanceMetrics BenchmarkMetrics { get; set; }

        // Relative metrics
        public double RelativeReturn { get; set; }
        public double RelativeSharpe { get; set; }
        public double RelativeDrawdown { get; set; }

        // Risk-adjusted metrics
        public double Alpha { get; set; }
        public double Beta { get; set; }
        public double InformationRatio { get; set; }

        public bool OutperformsBenchmark => RelativeReturn > 0;
        public bool BetterRiskAdjusted => RelativeSharpe > 0;
    }

    public enum BenchmarkType
    {
        BuyAndHold,
        SimpleMovingAverage,
        RSIMeanReversion
    }

    #endregion
}
