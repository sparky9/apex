using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Charts.Export;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Advanced portfolio performance analytics service
    /// </summary>
    public class PerformanceAnalyticsService
    {
        private readonly IChartLogger _logger;
        private readonly Dictionary<string, List<PerformanceDataPoint>> _performanceHistory;
        private readonly Dictionary<string, List<decimal>> _benchmarkData;
        private const decimal RISK_FREE_RATE = 0.02m; // 2% risk-free rate
        
        public PerformanceAnalyticsService(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _performanceHistory = new Dictionary<string, List<PerformanceDataPoint>>();
            _benchmarkData = new Dictionary<string, List<decimal>>();
        }
        
        #region Performance Calculation
        
        /// <summary>
        /// Calculate comprehensive performance metrics for a portfolio
        /// </summary>
        public async Task<PerformanceMetrics> CalculatePerformanceAsync(
            Portfolio portfolio,
            DateTime startDate,
            DateTime endDate,
            string benchmarkSymbol = "SPY")
        {
            try
            {
                var metrics = new PerformanceMetrics
                {
                    StartDate = startDate,
                    EndDate = endDate
                };
                
                // Get portfolio history
                var portfolioHistory = await GetPortfolioHistoryAsync(portfolio, startDate, endDate);
                if (portfolioHistory == null || !portfolioHistory.Any())
                {
                    _logger.Warn("No portfolio history available for performance calculation");
                    return metrics;
                }
                
                // Calculate basic returns
                CalculateBasicReturns(metrics, portfolioHistory);
                
                // Calculate volatility and risk metrics
                CalculateVolatilityMetrics(metrics, portfolioHistory);
                
                // Calculate drawdown metrics
                CalculateDrawdownMetrics(metrics, portfolioHistory);
                
                // Calculate trade statistics
                CalculateTradeStatistics(metrics, portfolio);
                
                // Calculate benchmark comparison
                await CalculateBenchmarkMetricsAsync(metrics, portfolioHistory, benchmarkSymbol);
                
                // Calculate sector and asset class performance
                CalculateSectorPerformance(metrics, portfolio);
                
                // Calculate risk metrics
                CalculateRiskMetrics(metrics, portfolioHistory);
                
                _logger.Info($"Performance metrics calculated for portfolio: {portfolio.Name}");
                return metrics;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error calculating performance metrics: {ex.Message}");
                throw;
            }
        }
        
        /// <summary>
        /// Calculate performance for multiple time periods
        /// </summary>
        public async Task<PerformancePeriods> CalculatePerformancePeriodsAsync(Portfolio portfolio)
        {
            var periods = new PerformancePeriods();
            var today = DateTime.UtcNow.Date;
            
            try
            {
                // Daily (last 24 hours)
                periods.Daily = await CalculatePerformanceAsync(portfolio, today.AddDays(-1), today);
                
                // Weekly (last 7 days)
                periods.Weekly = await CalculatePerformanceAsync(portfolio, today.AddDays(-7), today);
                
                // Monthly (last 30 days)
                periods.Monthly = await CalculatePerformanceAsync(portfolio, today.AddDays(-30), today);
                
                // Quarterly (last 90 days)
                periods.Quarterly = await CalculatePerformanceAsync(portfolio, today.AddDays(-90), today);
                
                // Year to date
                var yearStart = new DateTime(today.Year, 1, 1);
                periods.YearToDate = await CalculatePerformanceAsync(portfolio, yearStart, today);
                
                // One year
                periods.OneYear = await CalculatePerformanceAsync(portfolio, today.AddYears(-1), today);
                
                // Three years
                periods.ThreeYear = await CalculatePerformanceAsync(portfolio, today.AddYears(-3), today);
                
                // Five years
                periods.FiveYear = await CalculatePerformanceAsync(portfolio, today.AddYears(-5), today);
                
                // Since inception (assuming portfolio has inception date)
                var inceptionDate = portfolio.LastUpdated;
                periods.SinceInception = await CalculatePerformanceAsync(portfolio, inceptionDate, today);
                
                return periods;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error calculating performance periods: {ex.Message}");
                throw;
            }
        }
        
        #endregion
        
        #region Private Calculation Methods
        
        private void CalculateBasicReturns(PerformanceMetrics metrics, List<PerformanceDataPoint> history)
        {
            if (!history.Any()) return;
            
            var firstPoint = history.First();
            var lastPoint = history.Last();
            
            metrics.StartingValue = firstPoint.PortfolioValue;
            metrics.EndingValue = lastPoint.PortfolioValue;
            metrics.TotalReturn = metrics.EndingValue - metrics.StartingValue;
            metrics.TotalReturnPercent = metrics.StartingValue != 0 
                ? (metrics.TotalReturn / metrics.StartingValue) * 100 
                : 0;
            
            // Annualized return
            var days = (metrics.EndDate - metrics.StartDate).TotalDays;
            if (days > 0)
            {
                var years = days / 365.25;
                metrics.AnnualizedReturn = years > 0 
                    ? (decimal)(Math.Pow((double)(metrics.EndingValue / metrics.StartingValue), 1.0 / years) - 1) * 100
                    : 0;
            }
            
            // Daily returns
            metrics.DailyReturns = history.Select(h => h.DailyReturn).ToList();
            metrics.EquityCurve = new List<PerformanceDataPoint>(history);
        }
        
        private void CalculateVolatilityMetrics(PerformanceMetrics metrics, List<PerformanceDataPoint> history)
        {
            var returns = metrics.DailyReturns.Where(r => r != 0).ToList();
            if (!returns.Any()) return;
            
            var meanReturn = returns.Average();
            var variance = returns.Sum(r => (r - meanReturn) * (r - meanReturn)) / returns.Count;
            metrics.Volatility = (decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(252); // Annualized
            
            // Sharpe ratio
            var excessReturn = (metrics.AnnualizedReturn / 100) - RISK_FREE_RATE;
            metrics.SharpeRatio = metrics.Volatility != 0 ? excessReturn / (metrics.Volatility / 100) : 0;
        }
        
        private void CalculateDrawdownMetrics(PerformanceMetrics metrics, List<PerformanceDataPoint> history)
        {
            if (!history.Any()) return;
            
            decimal maxValue = 0;
            decimal maxDrawdown = 0;
            decimal maxDrawdownPercent = 0;
            DateTime maxDrawdownDate = DateTime.MinValue;
            
            foreach (var point in history)
            {
                if (point.PortfolioValue > maxValue)
                {
                    maxValue = point.PortfolioValue;
                }
                
                var drawdown = maxValue - point.PortfolioValue;
                var drawdownPercent = maxValue != 0 ? (drawdown / maxValue) * 100 : 0;
                
                if (drawdown > maxDrawdown)
                {
                    maxDrawdown = drawdown;
                    maxDrawdownPercent = drawdownPercent;
                    maxDrawdownDate = point.Date;
                }
                
                // Update the data point with drawdown
                point.DrawdownPercent = drawdownPercent;
            }
            
            metrics.MaxDrawdown = maxDrawdown;
            metrics.MaxDrawdownPercent = maxDrawdownPercent;
            metrics.MaxDrawdownDate = maxDrawdownDate;
        }
        
        private void CalculateTradeStatistics(PerformanceMetrics metrics, Portfolio portfolio)
        {
            // This would typically require historical trade data
            // For now, we'll use current positions as a proxy
            var winningPositions = portfolio.Positions.Where(p => p.UnrealizedGainLoss > 0).ToList();
            var losingPositions = portfolio.Positions.Where(p => p.UnrealizedGainLoss < 0).ToList();
            
            metrics.WinningTrades = winningPositions.Count;
            metrics.LosingTrades = losingPositions.Count;
            
            var totalTrades = metrics.WinningTrades + metrics.LosingTrades;
            metrics.WinRate = totalTrades > 0 ? (decimal)metrics.WinningTrades / totalTrades * 100 : 0;
            
            metrics.AverageWin = winningPositions.Any() ? winningPositions.Average(p => p.UnrealizedGainLoss) : 0;
            metrics.AverageLoss = losingPositions.Any() ? losingPositions.Average(p => Math.Abs(p.UnrealizedGainLoss)) : 0;
            
            metrics.ProfitFactor = metrics.AverageLoss != 0 ? Math.Abs(metrics.AverageWin / metrics.AverageLoss) : 0;
            metrics.ExpectancyPerTrade = totalTrades > 0 
                ? (metrics.AverageWin * metrics.WinRate / 100) - (metrics.AverageLoss * (100 - metrics.WinRate) / 100)
                : 0;
        }
        
        private async Task CalculateBenchmarkMetricsAsync(
            PerformanceMetrics metrics, 
            List<PerformanceDataPoint> history, 
            string benchmarkSymbol)
        {
            try
            {
                var benchmarkReturns = await GetBenchmarkReturnsAsync(benchmarkSymbol, metrics.StartDate, metrics.EndDate);
                if (!benchmarkReturns.Any()) return;
                
                var portfolioReturns = metrics.DailyReturns.ToList();
                if (portfolioReturns.Count != benchmarkReturns.Count) return;
                
                // Calculate beta
                var portfolioMean = portfolioReturns.Average();
                var benchmarkMean = benchmarkReturns.Average();
                
                var covariance = portfolioReturns.Zip(benchmarkReturns, (p, b) => 
                    (p - portfolioMean) * (b - benchmarkMean)).Average();
                
                var benchmarkVariance = benchmarkReturns.Sum(r => 
                    (r - benchmarkMean) * (r - benchmarkMean)) / benchmarkReturns.Count;
                
                metrics.Beta = benchmarkVariance != 0 ? covariance / benchmarkVariance : 0;
                
                // Calculate alpha
                var benchmarkReturn = benchmarkReturns.Sum() / 100; // Convert to decimal
                var expectedReturn = RISK_FREE_RATE + metrics.Beta * (benchmarkReturn - RISK_FREE_RATE);
                metrics.Alpha = (metrics.AnnualizedReturn / 100) - expectedReturn;
            }
            catch (Exception ex)
            {
                _logger.Warn($"Could not calculate benchmark metrics: {ex.Message}");
            }
        }
        
        private void CalculateSectorPerformance(PerformanceMetrics metrics, Portfolio portfolio)
        {
            var sectorGroups = portfolio.Positions.GroupBy(p => p.Sector ?? "Unknown");
            
            foreach (var group in sectorGroups)
            {
                var sectorValue = group.Sum(p => p.MarketValue);
                var sectorCost = group.Sum(p => p.TotalCost);
                var sectorReturn = sectorCost != 0 ? ((sectorValue - sectorCost) / sectorCost) * 100 : 0;
                
                metrics.SectorPerformance[group.Key] = sectorReturn;
            }
            
            // Asset class performance (simplified)
            var stockValue = portfolio.Positions.Where(p => p.AssetType == "Stock").Sum(p => p.MarketValue);
            var stockCost = portfolio.Positions.Where(p => p.AssetType == "Stock").Sum(p => p.TotalCost);
            var stockReturn = stockCost != 0 ? ((stockValue - stockCost) / stockCost) * 100 : 0;
            
            metrics.AssetClassPerformance["Stocks"] = stockReturn;
        }
        
        private void CalculateRiskMetrics(PerformanceMetrics metrics, List<PerformanceDataPoint> history)
        {
            var returns = metrics.DailyReturns.Where(r => r != 0).OrderBy(r => r).ToList();
            if (!returns.Any()) return;
            
            // Value at Risk (95% and 99%)
            var var95Index = (int)(returns.Count * 0.05);
            var var99Index = (int)(returns.Count * 0.01);
            
            if (var95Index < returns.Count)
                metrics.RiskMetrics.ValueAtRisk95 = Math.Abs(returns[var95Index]);
            
            if (var99Index < returns.Count)
                metrics.RiskMetrics.ValueAtRisk99 = Math.Abs(returns[var99Index]);
            
            // Conditional VaR (Expected Shortfall)
            if (var95Index > 0)
            {
                metrics.RiskMetrics.ConditionalVaR95 = Math.Abs(returns.Take(var95Index).Average());
            }
            
            if (var99Index > 0)
            {
                metrics.RiskMetrics.ConditionalVaR99 = Math.Abs(returns.Take(var99Index).Average());
            }
            
            // Downside deviation
            var negativeReturns = returns.Where(r => r < 0).ToList();
            if (negativeReturns.Any())
            {
                var downVariance = negativeReturns.Sum(r => r * r) / negativeReturns.Count;
                metrics.RiskMetrics.DownsideDeviation = (decimal)Math.Sqrt((double)downVariance);
                
                // Sortino ratio
                var excessReturn = (metrics.AnnualizedReturn / 100) - RISK_FREE_RATE;
                metrics.RiskMetrics.SortinoRatio = metrics.RiskMetrics.DownsideDeviation != 0 
                    ? excessReturn / metrics.RiskMetrics.DownsideDeviation : 0;
            }
            
            // Calmar ratio
            metrics.RiskMetrics.CalmarRatio = metrics.MaxDrawdownPercent != 0 
                ? (metrics.AnnualizedReturn / 100) / (metrics.MaxDrawdownPercent / 100) : 0;
        }
        
        #endregion
        
        #region Helper Methods
        
        private async Task<List<PerformanceDataPoint>> GetPortfolioHistoryAsync(
            Portfolio portfolio, 
            DateTime startDate, 
            DateTime endDate)
        {
            // In a real implementation, this would fetch historical portfolio values
            // For now, generate sample data
            var history = new List<PerformanceDataPoint>();
            var currentDate = startDate;
            var currentValue = 100000m; // Starting portfolio value
            var random = new Random();
            
            while (currentDate <= endDate)
            {
                // Simulate daily return (normally distributed around 0.05% daily)
                var dailyReturn = (decimal)(random.NextGaussian() * 0.02 + 0.0005);
                currentValue *= (1 + dailyReturn);
                
                history.Add(new PerformanceDataPoint
                {
                    Date = currentDate,
                    PortfolioValue = currentValue,
                    DailyReturn = dailyReturn * 100,
                    CumulativeReturn = ((currentValue - 100000m) / 100000m) * 100
                });
                
                currentDate = currentDate.AddDays(1);
            }
            
            return history;
        }
        
        private async Task<List<decimal>> GetBenchmarkReturnsAsync(
            string benchmarkSymbol, 
            DateTime startDate, 
            DateTime endDate)
        {
            // In a real implementation, this would fetch benchmark data
            // For now, generate sample benchmark returns
            var returns = new List<decimal>();
            var random = new Random(42); // Fixed seed for consistency
            var days = (int)(endDate - startDate).TotalDays;
            
            for (int i = 0; i < days; i++)
            {
                // Simulate benchmark return (slightly lower volatility)
                var dailyReturn = (decimal)(random.NextGaussian() * 0.015 + 0.0003);
                returns.Add(dailyReturn * 100);
            }
            
            return returns;
        }
        
        /// <summary>
        /// Record portfolio performance data point
        /// </summary>
        public void RecordPerformanceData(string portfolioId, PerformanceDataPoint dataPoint)
        {
            if (!_performanceHistory.ContainsKey(portfolioId))
            {
                _performanceHistory[portfolioId] = new List<PerformanceDataPoint>();
            }
            
            _performanceHistory[portfolioId].Add(dataPoint);
            
            // Keep only last 1000 data points per portfolio
            if (_performanceHistory[portfolioId].Count > 1000)
            {
                _performanceHistory[portfolioId] = _performanceHistory[portfolioId]
                    .OrderByDescending(p => p.Date)
                    .Take(1000)
                    .OrderBy(p => p.Date)
                    .ToList();
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Extension method for generating Gaussian random numbers
    /// </summary>
    public static class RandomExtensions
    {
        private static double u1 = 0.0, u2 = 0.0;
        private static bool hasSpare = false;
        
        public static double NextGaussian(this Random random)
        {
            if (hasSpare)
            {
                hasSpare = false;
                return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            }
            
            hasSpare = true;
            u1 = random.NextDouble();
            u2 = random.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }
    }
}
