using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Interface for performance analytics service
    /// </summary>
    public interface IPerformanceAnalyticsService
    {
        Task<PerformanceMetrics> CalculatePerformanceMetricsAsync(Portfolio portfolio, DateTime startDate, DateTime endDate);
        Task<PerformanceMetrics> CalculatePerformanceAsync(Portfolio portfolio, DateTime startDate, DateTime endDate, string benchmarkSymbol = "");
        Task<RiskMetrics> CalculateRiskMetricsAsync(Portfolio portfolio, DateTime startDate, DateTime endDate);
    }

    /// <summary>
    /// Interface for performance comparison service
    /// </summary>
    public interface IPerformanceComparisonService
    {
        Task<BenchmarkComparison> CompareToBenchmarkAsync(PerformanceMetrics portfolioMetrics, PerformanceMetrics benchmarkMetrics);
        Task<AttributionAnalysis> CalculateAttributionAsync(Portfolio portfolio, Dictionary<string, decimal> benchmarkReturns);
        Task<MonteCarloSimulation> RunMonteCarloSimulationAsync(decimal initialValue, decimal meanReturn, decimal volatility, int timeHorizonYears, int numberOfSimulations);
    }
}
