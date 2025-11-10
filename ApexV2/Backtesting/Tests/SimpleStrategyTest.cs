using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Backtesting.Engine;
using ApexV2.Backtesting.Models;
using ApexV2.Indicators.Backtesting.Momentum;
using ApexV2.Indicators.Backtesting.Volatility;
using ApexV2.Indicators.Backtesting.MovingAverages;

namespace ApexV2.Backtesting.Tests
{
    /// <summary>
    /// Simple validation test for the backtesting engine
    /// Tests a basic RSI oversold/overbought strategy
    /// </summary>
    public class SimpleStrategyTest
    {
        public static void RunTest()
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("APEX V3 BACKTESTING ENGINE - VALIDATION TEST");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            // Generate sample market data
            Console.WriteLine("📊 Generating sample market data...");
            var marketData = GenerateSampleData();
            Console.WriteLine($"✅ Generated {marketData.Dates.Length} days of data");
            Console.WriteLine($"   Start: {marketData.Dates[0]:yyyy-MM-dd}");
            Console.WriteLine($"   End: {marketData.Dates[marketData.Dates.Length - 1]:yyyy-MM-dd}");
            Console.WriteLine($"   Price range: ${marketData.Closes.Min():F2} - ${marketData.Closes.Max():F2}");
            Console.WriteLine();

            // Calculate indicators
            Console.WriteLine("📈 Calculating technical indicators...");
            var indicators = CalculateIndicators(marketData);
            Console.WriteLine($"✅ RSI calculated (window: 14)");
            Console.WriteLine($"✅ ATR calculated (window: 14)");
            Console.WriteLine($"✅ SMA calculated (window: 50)");
            Console.WriteLine();

            // Generate strategy signals
            Console.WriteLine("🎯 Generating strategy signals...");
            var signals = GenerateRSIStrategy(marketData, indicators);
            int entryCount = signals.EntrySignals.Count(s => s);
            int exitCount = signals.ExitSignals.Count(s => s);
            Console.WriteLine($"✅ Strategy: RSI Oversold/Overbought");
            Console.WriteLine($"   Entry signals: {entryCount}");
            Console.WriteLine($"   Exit signals: {exitCount}");
            Console.WriteLine();

            // Configure backtest parameters
            var parameters = new BacktestParameters
            {
                Symbol = "TEST",
                StartDate = marketData.Dates[0],
                EndDate = marketData.Dates[marketData.Dates.Length - 1],
                InitialCapital = 10000,
                Commission = 0.001,
                Slippage = 0.001,
                SizingMethod = PositionSizingMethod.FixedPercentage,
                PositionSizePercent = 0.95,
                UseStopLoss = true,
                StopLossATRMultiple = 2.0,
                UseTakeProfit = true,
                TakeProfitATRMultiple = 3.0,
                UseTrailingStop = false
            };

            // Run backtest
            Console.WriteLine("🚀 Running backtest...");
            var engine = new BacktestEngine(parameters);
            var results = engine.RunBacktest(marketData, signals, indicators);
            Console.WriteLine("✅ Backtest complete!");
            Console.WriteLine();

            // Display results
            DisplayResults(results);
        }

        /// <summary>
        /// Generate sample market data for testing
        /// Creates realistic-looking price movements with trend and volatility
        /// </summary>
        private static MarketData GenerateSampleData()
        {
            int days = 252; // One year of trading days
            var random = new Random(42); // Fixed seed for reproducibility
            var startDate = new DateTime(2023, 1, 1);

            var dates = new DateTime[days];
            var opens = new double[days];
            var highs = new double[days];
            var lows = new double[days];
            var closes = new double[days];
            var volumes = new double[days];

            double price = 100.0; // Starting price

            for (int i = 0; i < days; i++)
            {
                dates[i] = startDate.AddDays(i);

                // Generate daily movement with trend and noise
                double trend = 0.0002; // Slight upward bias
                double volatility = 0.015; // 1.5% daily volatility
                double randomMove = (random.NextDouble() - 0.5) * 2 * volatility;
                double dailyReturn = trend + randomMove;

                opens[i] = price;
                closes[i] = price * (1 + dailyReturn);

                // Generate high/low with intraday volatility
                double intradayRange = Math.Abs(closes[i] - opens[i]) * 1.5;
                highs[i] = Math.Max(opens[i], closes[i]) + intradayRange * random.NextDouble();
                lows[i] = Math.Min(opens[i], closes[i]) - intradayRange * random.NextDouble();

                // Generate volume
                volumes[i] = 1000000 + random.Next(-200000, 500000);

                price = closes[i];
            }

            return new MarketData
            {
                Symbol = "TEST",
                Dates = dates,
                Opens = opens,
                Highs = highs,
                Lows = lows,
                Closes = closes,
                Volumes = volumes
            };
        }

        /// <summary>
        /// Calculate technical indicators
        /// </summary>
        private static Dictionary<string, double[]> CalculateIndicators(MarketData data)
        {
            var indicators = new Dictionary<string, double[]>();

            // RSI
            var rsi = new RSI(window: 14, overbought: 70, oversold: 30);
            var rsiResults = rsi.Calculate(data.Closes);
            indicators["rsi"] = rsiResults["value"];

            // ATR
            var atr = new ATR(window: 14);
            var atrResults = atr.Calculate(data.Closes, data.Highs, data.Lows);
            indicators["atr"] = atrResults["atr"];

            // SMA
            var sma = new SMA(window: 50);
            var smaResults = sma.Calculate(data.Closes);
            indicators["sma"] = smaResults["value"];

            return indicators;
        }

        /// <summary>
        /// Generate trading signals using RSI strategy
        /// Entry: RSI crosses below 30 (oversold)
        /// Exit: RSI crosses above 70 (overbought)
        /// </summary>
        private static StrategySignals GenerateRSIStrategy(MarketData data, Dictionary<string, double[]> indicators)
        {
            var rsiValues = indicators["rsi"];
            int length = data.Dates.Length;

            var entrySignals = new bool[length];
            var exitSignals = new bool[length];

            for (int i = 1; i < length; i++)
            {
                // Skip if RSI not yet calculated
                if (double.IsNaN(rsiValues[i]) || double.IsNaN(rsiValues[i - 1]))
                    continue;

                // Entry: RSI crosses below 30
                if (rsiValues[i - 1] >= 30 && rsiValues[i] < 30)
                {
                    entrySignals[i] = true;
                }

                // Exit: RSI crosses above 70
                if (rsiValues[i - 1] <= 70 && rsiValues[i] > 70)
                {
                    exitSignals[i] = true;
                }
            }

            return new StrategySignals
            {
                EntrySignals = entrySignals,
                ExitSignals = exitSignals,
                StrategyName = "RSI Oversold/Overbought",
                Description = "Buy when RSI < 30, Sell when RSI > 70"
            };
        }

        /// <summary>
        /// Display comprehensive backtest results
        /// </summary>
        private static void DisplayResults(BacktestResults results)
        {
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("BACKTEST RESULTS");
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine();

            // Summary
            Console.WriteLine("📊 SUMMARY");
            Console.WriteLine($"   Strategy: {results.StrategyName}");
            Console.WriteLine($"   Symbol: {results.Symbol}");
            Console.WriteLine($"   Period: {results.StartDate:yyyy-MM-dd} to {results.EndDate:yyyy-MM-dd}");
            Console.WriteLine($"   Duration: {results.Duration.TotalDays:F0} days");
            Console.WriteLine();

            // Capital
            Console.WriteLine("💰 CAPITAL");
            Console.WriteLine($"   Initial: ${results.InitialCapital:N2}");
            Console.WriteLine($"   Final: ${results.FinalCapital:N2}");
            Console.WriteLine($"   Peak: ${results.PeakCapital:N2}");
            Console.WriteLine($"   Net Profit: ${results.Metrics.TotalReturn:N2}");
            Console.WriteLine($"   Return: {results.Metrics.TotalReturnPercent:F2}%");
            Console.WriteLine($"   CAGR: {results.Metrics.CAGR:F2}%");
            Console.WriteLine();

            // Trades
            Console.WriteLine("📈 TRADES");
            Console.WriteLine($"   Total: {results.TotalTrades}");
            Console.WriteLine($"   Winners: {results.WinningTrades} ({results.Metrics.WinRate:P1})");
            Console.WriteLine($"   Losers: {results.LosingTrades} ({results.Metrics.LossRate:P1})");
            Console.WriteLine($"   Average: ${results.Metrics.AverageReturn:N2}");
            Console.WriteLine($"   Largest Win: ${results.Metrics.LargestWin:N2}");
            Console.WriteLine($"   Largest Loss: ${results.Metrics.LargestLoss:N2}");
            Console.WriteLine();

            // Risk Metrics
            Console.WriteLine("⚠️  RISK METRICS");
            Console.WriteLine($"   Max Drawdown: ${results.Metrics.MaxDrawdown:N2} ({results.Metrics.MaxDrawdownPercent:F2}%)");
            Console.WriteLine($"   Sharpe Ratio: {results.Metrics.SharpeRatio:F2}");
            Console.WriteLine($"   Sortino Ratio: {results.Metrics.SortinoRatio:F2}");
            Console.WriteLine($"   Profit Factor: {results.Metrics.ProfitFactor:F2}");
            Console.WriteLine();

            // Trade Statistics
            Console.WriteLine("📊 STATISTICS");
            Console.WriteLine($"   Avg Trade Duration: {results.Metrics.AverageTradeDuration:F1} days");
            Console.WriteLine($"   Expectancy: ${results.Metrics.Expectancy:N2} per trade");
            Console.WriteLine($"   Time in Market: {results.Metrics.TimeInMarket:F1}%");
            Console.WriteLine($"   Longest Win Streak: {results.Metrics.LongestWinStreak}");
            Console.WriteLine($"   Longest Loss Streak: {results.Metrics.LongestLossStreak}");
            Console.WriteLine();

            // Individual Trades
            if (results.Trades.Count > 0)
            {
                Console.WriteLine("📋 TRADE LOG (First 10 trades)");
                Console.WriteLine($"{"Date",-12} {"Entry",-10} {"Exit",-10} {"P&L",-12} {"Return",-10} {"Days",-6} {"Reason"}");
                Console.WriteLine("-".PadRight(80, '-'));

                int displayCount = Math.Min(10, results.Trades.Count);
                for (int i = 0; i < displayCount; i++)
                {
                    var trade = results.Trades[i];
                    string entryDate = trade.EntryDate.ToString("yyyy-MM-dd");
                    string entryPrice = $"${trade.EntryPrice:F2}";
                    string exitPrice = trade.ExitPrice.HasValue ? $"${trade.ExitPrice.Value:F2}" : "Open";
                    string pnl = trade.NetProfit.HasValue ? $"${trade.NetProfit.Value:F2}" : "-";
                    string returnPct = trade.ReturnPercent.HasValue ? $"{trade.ReturnPercent.Value:F2}%" : "-";
                    string days = trade.HoldingPeriod.HasValue ? $"{trade.HoldingPeriod.Value.TotalDays:F0}" : "-";
                    string reason = trade.ExitReason ?? "-";

                    Console.WriteLine($"{entryDate,-12} {entryPrice,-10} {exitPrice,-10} {pnl,-12} {returnPct,-10} {days,-6} {reason}");
                }

                if (results.Trades.Count > 10)
                {
                    Console.WriteLine($"... and {results.Trades.Count - 10} more trades");
                }
            }

            Console.WriteLine();
            Console.WriteLine("=".PadRight(80, '='));
            Console.WriteLine("✅ VALIDATION TEST COMPLETE!");
            Console.WriteLine("=".PadRight(80, '='));
        }
    }
}
