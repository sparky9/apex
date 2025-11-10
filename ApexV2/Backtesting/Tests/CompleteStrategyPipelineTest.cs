using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Backtesting.Engine;
using ApexV2.Backtesting.Models;
using ApexV2.Backtesting.StrategyGeneration;

namespace ApexV2.Backtesting.Tests
{
    /// <summary>
    /// Complete end-to-end test of strategy generation, evaluation, and ranking
    /// </summary>
    public class CompleteStrategyPipelineTest
    {
        public static void Run()
        {
            Console.WriteLine("=== APEX V3 - Complete Strategy Pipeline Test ===\n");

            // Step 1: Generate test market data
            Console.WriteLine("Step 1: Generating test market data...");
            var marketData = GenerateTestData(252); // One year of daily data
            Console.WriteLine($"Generated {marketData.Close.Length} bars of market data\n");

            // Step 2: Configure strategy generation
            Console.WriteLine("Step 2: Configuring strategy generation...");
            var generationConfig = new StrategyGenerationConfig
            {
                EnabledIndicators = new List<string> { "RSI", "MACD", "BB", "SMA", "EMA", "STOCH" },
                MaxEntryRules = 3,
                MaxExitRules = 2,
                ParameterRanges = new Dictionary<string, ParameterRange>
                {
                    { "rsi_window", new ParameterRange(10, 20, 2) },
                    { "rsi_overbought", new ParameterRange(65, 75, 5) },
                    { "rsi_oversold", new ParameterRange(25, 35, 5) },
                    { "macd_fast", new ParameterRange(10, 14, 2) },
                    { "macd_slow", new ParameterRange(24, 30, 2) },
                    { "macd_signal", new ParameterRange(7, 11, 2) },
                    { "bb_window", new ParameterRange(15, 25, 5) },
                    { "bb_std", new ParameterRange(1.5, 2.5, 0.5) },
                    { "sma_window", new ParameterRange(40, 60, 10) },
                    { "ema_window", new ParameterRange(15, 25, 5) }
                }
            };

            // Step 3: Generate strategies
            Console.WriteLine("Step 3: Generating strategies...");
            var generator = new StrategyGenerator(generationConfig, new Random(42));
            int totalStrategies = 100; // Generate 100 strategies for testing
            var strategies = generator.GenerateStrategies(
                totalToGenerate: totalStrategies,
                randomPercentage: 0.7,
                templatePercentage: 0.3
            );
            Console.WriteLine($"Generated {strategies.Count} strategies");
            Console.WriteLine($"  - Random strategies: {strategies.Count(s => s.GenerationMethod == "Random")}");
            Console.WriteLine($"  - Template strategies: {strategies.Count(s => s.GenerationMethod.StartsWith("Template"))}\n");

            // Step 4: Setup backtesting
            Console.WriteLine("Step 4: Setting up backtesting engine...");
            var backtestParams = new BacktestParameters
            {
                InitialCapital = 10000,
                PositionSize = 1000,
                Commission = 0.001, // 0.1%
                Slippage = 0.0005  // 0.05%
            };
            var backtestEngine = new BacktestEngine(backtestParams);
            Console.WriteLine($"Initial capital: ${backtestParams.InitialCapital:N0}");
            Console.WriteLine($"Position size: ${backtestParams.PositionSize:N0}");
            Console.WriteLine($"Commission: {backtestParams.Commission:P2}\n");

            // Step 5: Configure evaluation
            Console.WriteLine("Step 5: Configuring evaluation...");
            var evalConfig = new EvaluationConfig
            {
                Weights = new Dictionary<string, double>
                {
                    { "sharpe_ratio", 0.30 },
                    { "sortino_ratio", 0.20 },
                    { "profit_factor", 0.20 },
                    { "max_drawdown", 0.15 },
                    { "win_rate", 0.10 },
                    { "total_return", 0.05 }
                },
                PerformRobustnessTests = true,
                MonteCarloIterations = 500,
                WalkForwardPeriods = 4
            };
            Console.WriteLine("Evaluation weights configured");
            Console.WriteLine($"Monte Carlo iterations: {evalConfig.MonteCarloIterations}");
            Console.WriteLine($"Walk-forward periods: {evalConfig.WalkForwardPeriods}\n");

            // Step 6: Process strategies in batch
            Console.WriteLine("Step 6: Evaluating strategies...");
            var batchProcessor = new StrategyBatchProcessor(backtestEngine, marketData, evalConfig);

            // Subscribe to progress updates
            batchProcessor.ProgressUpdated += (sender, e) =>
            {
                Console.WriteLine($"  [{e.Phase}] {e.PercentComplete:F1}% - {e.CurrentStrategy}");
            };

            var evaluationResults = batchProcessor.ProcessBatch(
                strategies,
                marketData,
                useParallel: true,
                batchSize: 50
            );

            Console.WriteLine($"Completed evaluation of {evaluationResults.Count} strategies\n");

            // Step 7: Apply filters
            Console.WriteLine("Step 7: Filtering strategies...");
            var filterCriteria = new FilterCriteria
            {
                MinSharpeRatio = 1.0,
                MinProfitFactor = 1.5,
                MaxDrawdown = -0.15,
                MinWinRate = 0.45,
                MinTrades = 10
            };

            var filteredResults = batchProcessor.FilterStrategies(evaluationResults, filterCriteria);
            Console.WriteLine($"Strategies passing filters: {filteredResults.Count}");
            Console.WriteLine($"Filter criteria:");
            Console.WriteLine($"  - Min Sharpe Ratio: {filterCriteria.MinSharpeRatio}");
            Console.WriteLine($"  - Min Profit Factor: {filterCriteria.MinProfitFactor}");
            Console.WriteLine($"  - Max Drawdown: {filterCriteria.MaxDrawdown:P0}");
            Console.WriteLine($"  - Min Win Rate: {filterCriteria.MinWinRate:P0}");
            Console.WriteLine($"  - Min Trades: {filterCriteria.MinTrades}\n");

            // Step 8: Get top strategies
            Console.WriteLine("Step 8: Ranking top strategies...");
            var topStrategies = batchProcessor.GetTopStrategies(filteredResults, 10);
            Console.WriteLine($"\nTop {topStrategies.Count} Strategies:\n");
            Console.WriteLine($"{"Rank",-6} {"Name",-30} {"Score",-8} {"Sharpe",-8} {"Return",-10} {"Win Rate",-10} {"Trades",-8}");
            Console.WriteLine(new string('-', 90));

            foreach (var result in topStrategies)
            {
                Console.WriteLine($"{result.Rank,-6} {result.Strategy.Name,-30} " +
                                $"{result.CompositeScore,-8:F2} " +
                                $"{result.PerformanceMetrics.SharpeRatio,-8:F2} " +
                                $"{result.PerformanceMetrics.TotalReturn,-10:P2} " +
                                $"{result.PerformanceMetrics.WinRate,-10:P2} " +
                                $"{result.PerformanceMetrics.TotalTrades,-8}");
            }

            // Step 9: Benchmark comparison
            if (topStrategies.Count > 0)
            {
                Console.WriteLine("\n\nStep 9: Benchmark comparison (Top Strategy)...");
                var topStrategy = topStrategies[0];
                var comparator = new BenchmarkComparator(backtestEngine);

                var benchmarkComparison = comparator.CompareToBuyAndHold(topStrategy, marketData);

                Console.WriteLine($"\nTop Strategy: {topStrategy.Strategy.Name}");
                Console.WriteLine($"\nBenchmark: {benchmarkComparison.BenchmarkName}");
                Console.WriteLine($"{"Metric",-25} {"Strategy",-15} {"Benchmark",-15} {"Relative",-15}");
                Console.WriteLine(new string('-', 70));
                Console.WriteLine($"{"Total Return",-25} {benchmarkComparison.StrategyMetrics.TotalReturn,-15:P2} " +
                                $"{benchmarkComparison.BenchmarkMetrics.TotalReturn,-15:P2} " +
                                $"{benchmarkComparison.RelativeReturn,-15:P2}");
                Console.WriteLine($"{"Sharpe Ratio",-25} {benchmarkComparison.StrategyMetrics.SharpeRatio,-15:F2} " +
                                $"{benchmarkComparison.BenchmarkMetrics.SharpeRatio,-15:F2} " +
                                $"{benchmarkComparison.RelativeSharpe,-15:F2}");
                Console.WriteLine($"{"Max Drawdown",-25} {benchmarkComparison.StrategyMetrics.MaxDrawdown,-15:P2} " +
                                $"{benchmarkComparison.BenchmarkMetrics.MaxDrawdown,-15:P2} " +
                                $"{benchmarkComparison.RelativeDrawdown,-15:P2}");
                Console.WriteLine($"{"Win Rate",-25} {benchmarkComparison.StrategyMetrics.WinRate,-15:P2} " +
                                $"{benchmarkComparison.BenchmarkMetrics.WinRate,-15:P2} {"N/A",-15}");
                Console.WriteLine($"\n{"Alpha",-25} {benchmarkComparison.Alpha,-15:P2}");
                Console.WriteLine($"{"Beta",-25} {benchmarkComparison.Beta,-15:F2}");
                Console.WriteLine($"{"Information Ratio",-25} {benchmarkComparison.InformationRatio,-15:F2}");
                Console.WriteLine($"\nOutperforms Benchmark: {(benchmarkComparison.OutperformsBenchmark ? "✓ Yes" : "✗ No")}");

                // Step 10: Detailed analysis of top strategy
                Console.WriteLine("\n\nStep 10: Detailed analysis of top strategy...");
                var topMetrics = topStrategy.PerformanceMetrics;
                Console.WriteLine($"\nStrategy: {topStrategy.Strategy.Name}");
                Console.WriteLine($"Generation Method: {topStrategy.Strategy.GenerationMethod}");
                Console.WriteLine($"\nEntry Rules:");
                foreach (var rule in topStrategy.Strategy.EntryRules)
                {
                    Console.WriteLine($"  - {rule.IndicatorName} {rule.Condition}");
                }
                Console.WriteLine($"Exit Rules:");
                foreach (var rule in topStrategy.Strategy.ExitRules)
                {
                    Console.WriteLine($"  - {rule.IndicatorName} {rule.Condition}");
                }

                Console.WriteLine($"\nPerformance Metrics:");
                Console.WriteLine($"  Total Return:      {topMetrics.TotalReturn:P2}");
                Console.WriteLine($"  Annualized Return: {topMetrics.AnnualizedReturn:P2}");
                Console.WriteLine($"  Sharpe Ratio:      {topMetrics.SharpeRatio:F2}");
                Console.WriteLine($"  Sortino Ratio:     {topMetrics.SortinoRatio:F2}");
                Console.WriteLine($"  Calmar Ratio:      {topMetrics.CalmarRatio:F2}");
                Console.WriteLine($"  Profit Factor:     {topMetrics.ProfitFactor:F2}");
                Console.WriteLine($"  Max Drawdown:      {topMetrics.MaxDrawdown:P2}");
                Console.WriteLine($"  Win Rate:          {topMetrics.WinRate:P2}");
                Console.WriteLine($"  Total Trades:      {topMetrics.TotalTrades}");
                Console.WriteLine($"  Avg Win:           ${topMetrics.AverageWin:F2}");
                Console.WriteLine($"  Avg Loss:          ${topMetrics.AverageLoss:F2}");
                Console.WriteLine($"  Largest Win:       ${topMetrics.LargestWin:F2}");
                Console.WriteLine($"  Largest Loss:      ${topMetrics.LargestLoss:F2}");

                // Robustness metrics
                if (topStrategy.RobustnessMetrics != null)
                {
                    Console.WriteLine($"\nRobustness Metrics:");

                    if (topStrategy.RobustnessMetrics.MonteCarloResults != null)
                    {
                        var mc = topStrategy.RobustnessMetrics.MonteCarloResults;
                        Console.WriteLine($"  Monte Carlo:");
                        Console.WriteLine($"    Mean Return:       {mc.MeanReturn:P2}");
                        Console.WriteLine($"    Std Dev:           {mc.StdDevReturn:P2}");
                        Console.WriteLine($"    % Profitable:      {mc.PercentProfitable:F1}%");
                    }

                    if (topStrategy.RobustnessMetrics.WalkForwardResults != null)
                    {
                        var wf = topStrategy.RobustnessMetrics.WalkForwardResults;
                        Console.WriteLine($"  Walk-Forward:");
                        Console.WriteLine($"    Average Return:    {wf.AverageReturn:P2}");
                        Console.WriteLine($"    Consistency:       {wf.Consistency:F4}");
                        Console.WriteLine($"    Profitable Periods: {wf.ProfitablePeriods}/{wf.TotalPeriods}");
                    }
                }
            }

            Console.WriteLine("\n\n=== Test Complete ===");
            Console.WriteLine("\nSummary:");
            Console.WriteLine($"  Total Strategies Generated: {strategies.Count}");
            Console.WriteLine($"  Strategies Evaluated: {evaluationResults.Count}");
            Console.WriteLine($"  Strategies Passing Filters: {filteredResults.Count}");
            Console.WriteLine($"  Success Rate: {(filteredResults.Count / (double)evaluationResults.Count):P1}");
        }

        /// <summary>
        /// Generate synthetic test data with realistic patterns
        /// </summary>
        private static MarketData GenerateTestData(int bars)
        {
            var random = new Random(42);
            var timestamps = new DateTime[bars];
            var open = new double[bars];
            var high = new double[bars];
            var low = new double[bars];
            var close = new double[bars];
            var volume = new double[bars];

            double price = 100.0;
            DateTime currentDate = DateTime.Now.AddDays(-bars);

            for (int i = 0; i < bars; i++)
            {
                timestamps[i] = currentDate.AddDays(i);

                // Generate price with trend and noise
                double trend = 0.0005; // Slight upward trend
                double volatility = 0.02;
                double change = (random.NextDouble() - 0.5) * volatility + trend;

                price *= (1 + change);

                open[i] = price;
                double dayRange = price * 0.015 * random.NextDouble();
                high[i] = price + dayRange * random.NextDouble();
                low[i] = price - dayRange * random.NextDouble();
                close[i] = low[i] + (high[i] - low[i]) * random.NextDouble();

                // Update price for next bar
                price = close[i];

                // Generate volume
                volume[i] = 1000000 + random.Next(500000);
            }

            return new MarketData
            {
                Symbol = "TEST",
                Timestamps = timestamps,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            };
        }
    }
}
