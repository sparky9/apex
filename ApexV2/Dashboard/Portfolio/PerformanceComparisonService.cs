using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Charts.Export;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Service for comparing portfolio performance against benchmarks and attribution analysis
    /// </summary>
    public class PerformanceComparisonService
    {
        private readonly IChartLogger _logger;
        private readonly PerformanceAnalyticsService _analyticsService;
        
        public PerformanceComparisonService(IChartLogger logger, PerformanceAnalyticsService analyticsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _analyticsService = analyticsService ?? throw new ArgumentNullException(nameof(analyticsService));
        }
        
        #region Benchmark Comparison
        
        /// <summary>
        /// Compare portfolio performance against a benchmark
        /// </summary>
        public async Task<BenchmarkComparison> CompareToBenchmarkAsync(
            Portfolio portfolio,
            string benchmarkSymbol,
            string benchmarkName,
            DateTime startDate,
            DateTime endDate)
        {
            try
            {
                var comparison = new BenchmarkComparison
                {
                    BenchmarkSymbol = benchmarkSymbol,
                    BenchmarkName = benchmarkName
                };
                
                // Get portfolio performance
                var portfolioMetrics = await _analyticsService.CalculatePerformanceAsync(
                    portfolio, startDate, endDate, benchmarkSymbol);
                
                // Get benchmark data (this would typically come from market data service)
                var benchmarkData = await GetBenchmarkDataAsync(benchmarkSymbol, startDate, endDate);
                
                if (!benchmarkData.Any())
                {
                    _logger.Warn($"No benchmark data available for {benchmarkSymbol}");
                    return comparison;
                }
                
                // Calculate benchmark performance
                var benchmarkReturn = CalculateBenchmarkReturn(benchmarkData);
                
                // Populate comparison metrics
                comparison.PortfolioReturn = portfolioMetrics.TotalReturnPercent;
                comparison.BenchmarkReturn = benchmarkReturn;
                comparison.Alpha = portfolioMetrics.Alpha;
                comparison.Beta = portfolioMetrics.Beta;
                
                // Calculate tracking error
                comparison.TrackingError = CalculateTrackingError(
                    portfolioMetrics.DailyReturns, 
                    benchmarkData.Select(b => b.DailyReturn).ToList());
                
                // Calculate information ratio
                var activeReturn = comparison.PortfolioReturn - comparison.BenchmarkReturn;
                comparison.InformationRatio = comparison.TrackingError != 0 ? activeReturn / comparison.TrackingError : 0;
                
                // Calculate up/down capture ratios
                CalculateCaptureRatios(comparison, portfolioMetrics.DailyReturns, 
                    benchmarkData.Select(b => b.DailyReturn).ToList());
                
                // Create comparison chart data
                comparison.ComparisonChart = CreateComparisonChart(portfolioMetrics.EquityCurve, benchmarkData);
                
                _logger.Info($"Benchmark comparison completed for {portfolio.Name} vs {benchmarkSymbol}");
                return comparison;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error comparing to benchmark: {ex.Message}");
                throw;
            }
        }
        
        #endregion
        
        #region Performance Attribution
        
        /// <summary>
        /// Perform performance attribution analysis
        /// </summary>
        public async Task<PerformanceAttribution> PerformAttributionAnalysisAsync(
            Portfolio portfolio,
            string benchmarkSymbol,
            DateTime startDate,
            DateTime endDate)
        {
            try
            {
                var attribution = new PerformanceAttribution();
                
                // Get benchmark composition (would typically come from data provider)
                var benchmarkComposition = await GetBenchmarkCompositionAsync(benchmarkSymbol);
                
                // Calculate sector attribution
                attribution.SectorAttributions = CalculateSectorAttribution(portfolio, benchmarkComposition);
                
                // Calculate security attribution
                attribution.SecurityAttributions = CalculateSecurityAttribution(portfolio, benchmarkComposition);
                
                // Calculate attribution summary
                attribution.Summary = CalculateAttributionSummary(attribution);
                
                _logger.Info($"Performance attribution analysis completed for {portfolio.Name}");
                return attribution;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error performing attribution analysis: {ex.Message}");
                throw;
            }
        }
        
        #endregion
        
        #region Monte Carlo Simulation
        
        /// <summary>
        /// Run Monte Carlo simulation for risk analysis
        /// </summary>
        public async Task<MonteCarloSimulation> RunMonteCarloSimulationAsync(
            Portfolio portfolio,
            int simulations = 1000,
            int daysToSimulate = 252,
            decimal confidenceLevel = 0.95m)
        {
            try
            {
                var simulation = new MonteCarloSimulation
                {
                    SimulationCount = simulations,
                    DaysSimulated = daysToSimulate,
                    ConfidenceLevel = confidenceLevel
                };
                
                // Get historical portfolio volatility and return
                var historicalMetrics = await _analyticsService.CalculatePerformanceAsync(
                    portfolio, DateTime.UtcNow.AddYears(-1), DateTime.UtcNow);
                
                var dailyReturn = historicalMetrics.AnnualizedReturn / 100 / 252; // Daily return
                var dailyVolatility = historicalMetrics.Volatility / 100 / (decimal)Math.Sqrt(252); // Daily volatility
                
                var random = new Random();
                var startingValue = portfolio.TotalValue;
                
                // Run simulations
                for (int sim = 0; sim < simulations; sim++)
                {
                    var simulationPath = new List<decimal>();
                    var currentValue = startingValue;
                    decimal maxDrawdown = 0;
                    decimal peakValue = startingValue;
                    
                    for (int day = 0; day < daysToSimulate; day++)
                    {
                        // Generate random return using normal distribution
                        var randomReturn = (decimal)(random.NextGaussian() * (double)dailyVolatility + (double)dailyReturn);
                        currentValue *= (1 + randomReturn);
                        simulationPath.Add(currentValue);
                        
                        // Track drawdown
                        if (currentValue > peakValue)
                            peakValue = currentValue;
                        
                        var drawdown = (peakValue - currentValue) / peakValue;
                        if (drawdown > maxDrawdown)
                            maxDrawdown = drawdown;
                    }
                    
                    simulation.FinalValues.Add(currentValue);
                    simulation.MaxDrawdowns.Add(maxDrawdown);
                    simulation.SimulationPaths.Add(simulationPath);
                }
                
                // Calculate simulation statistics
                CalculateSimulationStatistics(simulation, startingValue, confidenceLevel);
                
                _logger.Info($"Monte Carlo simulation completed: {simulations} simulations over {daysToSimulate} days");
                return simulation;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error running Monte Carlo simulation: {ex.Message}");
                throw;
            }
        }
        
        #endregion
        
        #region Private Methods
        
        private async Task<List<PerformanceDataPoint>> GetBenchmarkDataAsync(
            string benchmarkSymbol, 
            DateTime startDate, 
            DateTime endDate)
        {
            // In a real implementation, this would fetch actual benchmark data
            // For now, generate sample data
            var data = new List<PerformanceDataPoint>();
            var currentDate = startDate;
            var currentValue = 100m; // Normalized starting value
            var random = new Random(42); // Fixed seed for consistency
            
            while (currentDate <= endDate)
            {
                var dailyReturn = (decimal)(random.NextGaussian() * 0.015 + 0.0003); // Slightly lower volatility
                currentValue *= (1 + dailyReturn);
                
                data.Add(new PerformanceDataPoint
                {
                    Date = currentDate,
                    PortfolioValue = currentValue,
                    DailyReturn = dailyReturn * 100,
                    CumulativeReturn = (currentValue - 100m) / 100m * 100
                });
                
                currentDate = currentDate.AddDays(1);
            }
            
            return data;
        }
        
        private decimal CalculateBenchmarkReturn(List<PerformanceDataPoint> benchmarkData)
        {
            if (!benchmarkData.Any()) return 0;
            
            var firstValue = benchmarkData.First().PortfolioValue;
            var lastValue = benchmarkData.Last().PortfolioValue;
            
            return ((lastValue - firstValue) / firstValue) * 100;
        }
        
        private decimal CalculateTrackingError(List<decimal> portfolioReturns, List<decimal> benchmarkReturns)
        {
            if (portfolioReturns.Count != benchmarkReturns.Count || !portfolioReturns.Any())
                return 0;
            
            var activeSources = portfolioReturns.Zip(benchmarkReturns, (p, b) => p - b).ToList();
            var meanActiveReturn = activeSources.Average();
            var variance = activeSources.Sum(ar => (ar - meanActiveReturn) * (ar - meanActiveReturn)) / activeSources.Count;
            
            return (decimal)Math.Sqrt((double)variance) * (decimal)Math.Sqrt(252); // Annualized
        }
        
        private void CalculateCaptureRatios(
            BenchmarkComparison comparison,
            List<decimal> portfolioReturns,
            List<decimal> benchmarkReturns)
        {
            if (portfolioReturns.Count != benchmarkReturns.Count) return;
            
            var upMarkets = new List<(decimal portfolio, decimal benchmark)>();
            var downMarkets = new List<(decimal portfolio, decimal benchmark)>();
            
            for (int i = 0; i < portfolioReturns.Count; i++)
            {
                if (benchmarkReturns[i] > 0)
                    upMarkets.Add((portfolioReturns[i], benchmarkReturns[i]));
                else if (benchmarkReturns[i] < 0)
                    downMarkets.Add((portfolioReturns[i], benchmarkReturns[i]));
            }
            
            // Up capture ratio
            if (upMarkets.Any())
            {
                var avgPortfolioUp = upMarkets.Average(x => x.portfolio);
                var avgBenchmarkUp = upMarkets.Average(x => x.benchmark);
                comparison.UpCapture = avgBenchmarkUp != 0 ? (avgPortfolioUp / avgBenchmarkUp) * 100 : 0;
            }
            
            // Down capture ratio
            if (downMarkets.Any())
            {
                var avgPortfolioDown = downMarkets.Average(x => x.portfolio);
                var avgBenchmarkDown = downMarkets.Average(x => x.benchmark);
                comparison.DownCapture = avgBenchmarkDown != 0 ? (avgPortfolioDown / avgBenchmarkDown) * 100 : 0;
            }
        }
        
        private List<PerformanceDataPoint> CreateComparisonChart(
            List<PerformanceDataPoint> portfolioData,
            List<PerformanceDataPoint> benchmarkData)
        {
            var comparisonChart = new List<PerformanceDataPoint>();
            
            for (int i = 0; i < Math.Min(portfolioData.Count, benchmarkData.Count); i++)
            {
                var point = new PerformanceDataPoint
                {
                    Date = portfolioData[i].Date,
                    PortfolioValue = portfolioData[i].PortfolioValue,
                    DailyReturn = portfolioData[i].DailyReturn,
                    CumulativeReturn = portfolioData[i].CumulativeReturn,
                    Benchmark = benchmarkData[i].PortfolioValue,
                    BenchmarkReturn = benchmarkData[i].CumulativeReturn
                };
                
                comparisonChart.Add(point);
            }
            
            return comparisonChart;
        }
        
        private async Task<Dictionary<string, decimal>> GetBenchmarkCompositionAsync(string benchmarkSymbol)
        {
            // In a real implementation, this would fetch actual benchmark composition
            // For now, return sample sector weights
            return new Dictionary<string, decimal>
            {
                ["Technology"] = 25.0m,
                ["Healthcare"] = 15.0m,
                ["Finance"] = 12.0m,
                ["Consumer"] = 10.0m,
                ["Industrial"] = 8.0m,
                ["Energy"] = 5.0m,
                ["Utilities"] = 3.0m,
                ["Materials"] = 3.0m,
                ["Real Estate"] = 2.0m,
                ["Telecommunications"] = 2.0m,
                ["Other"] = 15.0m
            };
        }
        
        private List<SectorAttribution> CalculateSectorAttribution(
            Portfolio portfolio, 
            Dictionary<string, decimal> benchmarkComposition)
        {
            var sectorAttributions = new List<SectorAttribution>();
            var totalPortfolioValue = portfolio.TotalValue;
            
            var portfolioSectors = portfolio.Positions
                .GroupBy(p => p.Sector ?? "Other")
                .ToDictionary(g => g.Key, g => g.Sum(p => p.MarketValue));
            
            foreach (var kvp in benchmarkComposition)
            {
                var sectorName = kvp.Key;
                var benchmarkWeight = kvp.Value;
                var portfolioValue = portfolioSectors.GetValueOrDefault(sectorName, 0);
                var portfolioWeight = totalPortfolioValue != 0 ? (portfolioValue / totalPortfolioValue) * 100 : 0;
                
                var attribution = new SectorAttribution
                {
                    SectorName = sectorName,
                    PortfolioWeight = portfolioWeight,
                    BenchmarkWeight = benchmarkWeight,
                    AllocationEffect = (portfolioWeight - benchmarkWeight) * 0.05m, // Simplified calculation
                    SelectionEffect = portfolioWeight * 0.03m, // Simplified calculation
                    InteractionEffect = (portfolioWeight - benchmarkWeight) * 0.01m // Simplified calculation
                };
                
                attribution.TotalAttribution = attribution.AllocationEffect + 
                                             attribution.SelectionEffect + 
                                             attribution.InteractionEffect;
                
                sectorAttributions.Add(attribution);
            }
            
            return sectorAttributions;
        }
        
        private List<SecurityAttribution> CalculateSecurityAttribution(
            Portfolio portfolio,
            Dictionary<string, decimal> benchmarkComposition)
        {
            var securityAttributions = new List<SecurityAttribution>();
            var totalValue = portfolio.TotalValue;
            
            foreach (var position in portfolio.Positions)
            {
                var weight = totalValue != 0 ? (position.MarketValue / totalValue) * 100 : 0;
                var returnPercent = position.TotalCost != 0 ? 
                    ((position.MarketValue - position.TotalCost) / position.TotalCost) * 100 : 0;
                
                var attribution = new SecurityAttribution
                {
                    Symbol = position.Symbol,
                    SecurityName = position.CompanyName,
                    Sector = position.Sector ?? "Other",
                    Weight = weight,
                    Return = returnPercent,
                    Contribution = (weight / 100) * returnPercent,
                    RelativeWeight = weight, // Simplified - would compare to benchmark weight
                    RelativeReturn = returnPercent, // Simplified - would compare to benchmark return
                    Attribution = (weight / 100) * returnPercent // Simplified calculation
                };
                
                securityAttributions.Add(attribution);
            }
            
            return securityAttributions.OrderByDescending(s => Math.Abs(s.Attribution)).ToList();
        }
        
        private AttributionSummary CalculateAttributionSummary(PerformanceAttribution attribution)
        {
            return new AttributionSummary
            {
                TotalAllocationEffect = attribution.SectorAttributions.Sum(s => s.AllocationEffect),
                TotalSelectionEffect = attribution.SectorAttributions.Sum(s => s.SelectionEffect),
                TotalInteractionEffect = attribution.SectorAttributions.Sum(s => s.InteractionEffect),
                TotalPortfolioReturn = attribution.SecurityAttributions.Sum(s => s.Contribution),
                TotalBenchmarkReturn = 8.5m, // Sample benchmark return
                TotalActiveReturn = attribution.SecurityAttributions.Sum(s => s.Contribution) - 8.5m
            };
        }
        
        private void CalculateSimulationStatistics(
            MonteCarloSimulation simulation,
            decimal startingValue,
            decimal confidenceLevel)
        {
            var sortedFinalValues = simulation.FinalValues.OrderBy(v => v).ToList();
            var returns = simulation.FinalValues.Select(v => (v - startingValue) / startingValue).OrderBy(r => r).ToList();
            
            simulation.MedianReturn = returns[returns.Count / 2] * 100;
            simulation.WorstCaseReturn = returns.First() * 100;
            simulation.BestCaseReturn = returns.Last() * 100;
            
            var lossCount = returns.Count(r => r < 0);
            simulation.ProbabilityOfLoss = (decimal)lossCount / simulation.SimulationCount * 100;
            
            // Value at Risk
            var varIndex = (int)((1 - confidenceLevel) * simulation.SimulationCount);
            if (varIndex < returns.Count)
            {
                simulation.ValueAtRisk = Math.Abs(returns[varIndex] * 100);
            }
            
            // Conditional VaR (Expected Shortfall)
            if (varIndex > 0)
            {
                simulation.ConditionalVaR = Math.Abs(returns.Take(varIndex).Average() * 100);
            }
        }
        
        #endregion
    }
}
