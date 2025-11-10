using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.Engine
{
    /// <summary>
    /// Calculates comprehensive performance metrics for backtest results
    /// </summary>
    public class PerformanceAnalyzer
    {
        /// <summary>
        /// Calculate all performance metrics from backtest results
        /// </summary>
        public PerformanceMetrics CalculateMetrics(BacktestResults results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            var metrics = new PerformanceMetrics();

            var closedTrades = results.Trades.Where(t => t.IsClosed).ToList();

            if (closedTrades.Count == 0)
            {
                return metrics; // Return empty metrics if no trades
            }

            // Basic returns
            CalculateReturns(results, metrics);

            // Win/Loss statistics
            CalculateWinLossStats(closedTrades, metrics);

            // Drawdown analysis
            CalculateDrawdownStats(results.DrawdownCurve, metrics);

            // Risk-adjusted returns
            CalculateRiskAdjustedReturns(results, closedTrades, metrics);

            // Trade statistics
            CalculateTradeStats(closedTrades, metrics);

            // Expectancy
            CalculateExpectancy(closedTrades, metrics);

            // Exposure
            CalculateExposure(results, closedTrades, metrics);

            return metrics;
        }

        /// <summary>
        /// Calculate return metrics
        /// </summary>
        private void CalculateReturns(BacktestResults results, PerformanceMetrics metrics)
        {
            metrics.TotalReturn = results.FinalCapital - results.InitialCapital;
            metrics.TotalReturnPercent = (metrics.TotalReturn / results.InitialCapital) * 100;

            // Calculate CAGR (Compound Annual Growth Rate)
            double years = results.Duration.TotalDays / 365.25;
            if (years > 0)
            {
                metrics.CAGR = (Math.Pow(results.FinalCapital / results.InitialCapital, 1 / years) - 1) * 100;
            }

            // Average and median returns
            var returns = results.Trades.Where(t => t.NetProfit.HasValue)
                                       .Select(t => t.NetProfit!.Value)
                                       .ToList();

            if (returns.Count > 0)
            {
                metrics.AverageReturn = returns.Average();

                var sortedReturns = returns.OrderBy(r => r).ToList();
                int midpoint = sortedReturns.Count / 2;
                metrics.MedianReturn = sortedReturns.Count % 2 == 0
                    ? (sortedReturns[midpoint - 1] + sortedReturns[midpoint]) / 2
                    : sortedReturns[midpoint];
            }
        }

        /// <summary>
        /// Calculate win/loss statistics
        /// </summary>
        private void CalculateWinLossStats(List<Trade> trades, PerformanceMetrics metrics)
        {
            int totalTrades = trades.Count;
            var winners = trades.Where(t => t.IsWinner).ToList();
            var losers = trades.Where(t => t.IsLoser).ToList();

            metrics.WinRate = totalTrades > 0 ? (double)winners.Count / totalTrades : 0;
            metrics.LossRate = 1 - metrics.WinRate;

            if (winners.Count > 0)
            {
                metrics.AverageWin = winners.Average(t => t.NetProfit!.Value);
                metrics.LargestWin = winners.Max(t => t.NetProfit!.Value);
            }

            if (losers.Count > 0)
            {
                metrics.AverageLoss = losers.Average(t => t.NetProfit!.Value);
                metrics.LargestLoss = losers.Min(t => t.NetProfit!.Value);
            }

            // Calculate longest win/loss streaks
            CalculateStreaks(trades, metrics);
        }

        /// <summary>
        /// Calculate win/loss streaks
        /// </summary>
        private void CalculateStreaks(List<Trade> trades, PerformanceMetrics metrics)
        {
            int currentWinStreak = 0;
            int currentLossStreak = 0;
            int maxWinStreak = 0;
            int maxLossStreak = 0;

            foreach (var trade in trades.OrderBy(t => t.EntryDate))
            {
                if (trade.IsWinner)
                {
                    currentWinStreak++;
                    currentLossStreak = 0;
                    maxWinStreak = Math.Max(maxWinStreak, currentWinStreak);
                }
                else if (trade.IsLoser)
                {
                    currentLossStreak++;
                    currentWinStreak = 0;
                    maxLossStreak = Math.Max(maxLossStreak, currentLossStreak);
                }
            }

            metrics.LongestWinStreak = maxWinStreak;
            metrics.LongestLossStreak = maxLossStreak;
        }

        /// <summary>
        /// Calculate drawdown statistics
        /// </summary>
        private void CalculateDrawdownStats(List<DrawdownPoint> drawdownCurve, PerformanceMetrics metrics)
        {
            if (drawdownCurve.Count == 0)
                return;

            var maxDrawdownPoint = drawdownCurve.OrderByDescending(d => d.Drawdown).FirstOrDefault();
            if (maxDrawdownPoint != null)
            {
                metrics.MaxDrawdown = maxDrawdownPoint.Drawdown;
                metrics.MaxDrawdownPercent = maxDrawdownPoint.DrawdownPercent;
                metrics.MaxDrawdownDate = maxDrawdownPoint.Date;
            }

            // Average drawdown
            var drawdowns = drawdownCurve.Where(d => d.Drawdown > 0).ToList();
            if (drawdowns.Count > 0)
            {
                metrics.AverageDrawdown = drawdowns.Average(d => d.Drawdown);
            }
        }

        /// <summary>
        /// Calculate risk-adjusted return metrics
        /// </summary>
        private void CalculateRiskAdjustedReturns(BacktestResults results, List<Trade> trades, PerformanceMetrics metrics)
        {
            // Calculate returns for each period
            var dailyReturns = CalculateDailyReturns(results.EquityCurve);

            if (dailyReturns.Count > 1)
            {
                double avgReturn = dailyReturns.Average();
                double stdDev = CalculateStandardDeviation(dailyReturns);

                // Sharpe Ratio (assuming 0% risk-free rate for simplicity)
                if (stdDev > 0)
                {
                    metrics.SharpeRatio = (avgReturn * 252) / (stdDev * Math.Sqrt(252)); // Annualized
                }

                // Sortino Ratio (uses downside deviation only)
                var negativeReturns = dailyReturns.Where(r => r < 0).ToList();
                if (negativeReturns.Count > 0)
                {
                    double downsideDeviation = CalculateStandardDeviation(negativeReturns);
                    if (downsideDeviation > 0)
                    {
                        metrics.SortinoRatio = (avgReturn * 252) / (downsideDeviation * Math.Sqrt(252));
                    }
                }
            }

            // Calmar Ratio (return / max drawdown)
            if (metrics.MaxDrawdownPercent > 0)
            {
                metrics.CalmarRatio = metrics.CAGR / metrics.MaxDrawdownPercent;
            }

            // Profit Factor (gross profit / gross loss)
            double grossProfit = trades.Where(t => t.IsWinner).Sum(t => t.NetProfit ?? 0);
            double grossLoss = Math.Abs(trades.Where(t => t.IsLoser).Sum(t => t.NetProfit ?? 0));

            if (grossLoss > 0)
            {
                metrics.ProfitFactor = grossProfit / grossLoss;
            }
        }

        /// <summary>
        /// Calculate trade duration statistics
        /// </summary>
        private void CalculateTradeStats(List<Trade> trades, PerformanceMetrics metrics)
        {
            var tradeDurations = trades.Where(t => t.HoldingPeriod.HasValue)
                                      .Select(t => t.HoldingPeriod!.Value.TotalDays)
                                      .ToList();

            if (tradeDurations.Count > 0)
            {
                metrics.AverageTradeDuration = tradeDurations.Average();
            }

            var winDurations = trades.Where(t => t.IsWinner && t.HoldingPeriod.HasValue)
                                    .Select(t => t.HoldingPeriod!.Value.TotalDays)
                                    .ToList();

            if (winDurations.Count > 0)
            {
                metrics.AverageWinDuration = winDurations.Average();
            }

            var lossDurations = trades.Where(t => t.IsLoser && t.HoldingPeriod.HasValue)
                                     .Select(t => t.HoldingPeriod!.Value.TotalDays)
                                     .ToList();

            if (lossDurations.Count > 0)
            {
                metrics.AverageLossDuration = lossDurations.Average();
            }
        }

        /// <summary>
        /// Calculate expectancy (average profit per trade)
        /// </summary>
        private void CalculateExpectancy(List<Trade> trades, PerformanceMetrics metrics)
        {
            if (trades.Count == 0)
                return;

            double totalProfit = trades.Sum(t => t.NetProfit ?? 0);
            metrics.Expectancy = totalProfit / trades.Count;

            var returnPercents = trades.Where(t => t.ReturnPercent.HasValue)
                                      .Select(t => t.ReturnPercent!.Value)
                                      .ToList();

            if (returnPercents.Count > 0)
            {
                metrics.ExpectancyPercent = returnPercents.Average();
            }
        }

        /// <summary>
        /// Calculate market exposure statistics
        /// </summary>
        private void CalculateExposure(BacktestResults results, List<Trade> trades, PerformanceMetrics metrics)
        {
            if (results.Duration.TotalDays == 0)
                return;

            // Calculate total time in market
            double totalDaysInMarket = trades.Where(t => t.HoldingPeriod.HasValue)
                                             .Sum(t => t.HoldingPeriod!.Value.TotalDays);

            metrics.TimeInMarket = (totalDaysInMarket / results.Duration.TotalDays) * 100;

            // Average exposure (average position size as % of equity)
            var exposures = new List<double>();
            foreach (var equityPoint in results.EquityCurve)
            {
                if (equityPoint.PositionValue > 0 && equityPoint.TotalValue > 0)
                {
                    exposures.Add((equityPoint.PositionValue / equityPoint.TotalValue) * 100);
                }
            }

            if (exposures.Count > 0)
            {
                metrics.AverageExposure = exposures.Average();
            }
        }

        /// <summary>
        /// Calculate daily returns from equity curve
        /// </summary>
        private List<double> CalculateDailyReturns(List<EquityPoint> equityCurve)
        {
            var returns = new List<double>();

            for (int i = 1; i < equityCurve.Count; i++)
            {
                double previousEquity = equityCurve[i - 1].Equity;
                double currentEquity = equityCurve[i].Equity;

                if (previousEquity > 0)
                {
                    double dailyReturn = (currentEquity - previousEquity) / previousEquity;
                    returns.Add(dailyReturn);
                }
            }

            return returns;
        }

        /// <summary>
        /// Calculate standard deviation
        /// </summary>
        private double CalculateStandardDeviation(List<double> values)
        {
            if (values.Count < 2)
                return 0;

            double avg = values.Average();
            double sumSquaredDiff = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sumSquaredDiff / (values.Count - 1));
        }
    }
}
