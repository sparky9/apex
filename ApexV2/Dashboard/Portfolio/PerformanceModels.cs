using System;
using System.Collections.Generic;
using System.Linq;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Performance metrics calculation and tracking models
    /// </summary>
    public class PerformanceMetrics
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal StartingValue { get; set; }
        public decimal EndingValue { get; set; }
        public decimal TotalReturn { get; set; }
        public decimal TotalReturnPercent { get; set; }
        public decimal AnnualizedReturn { get; set; }
        public decimal Volatility { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal MaxDrawdownPercent { get; set; }
        public DateTime MaxDrawdownDate { get; set; }
        public decimal Beta { get; set; }
        public decimal Alpha { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal WinRate { get; set; }
        public decimal AverageWin { get; set; }
        public decimal AverageLoss { get; set; }
        public decimal ProfitFactor { get; set; }
        public decimal ExpectancyPerTrade { get; set; }
        public List<decimal> DailyReturns { get; set; } = new();
        public List<PerformanceDataPoint> EquityCurve { get; set; } = new();
        public Dictionary<string, decimal> SectorPerformance { get; set; } = new();
        public Dictionary<string, decimal> AssetClassPerformance { get; set; } = new();
        public RiskMetrics RiskMetrics { get; set; } = new();
    }
    
    /// <summary>
    /// Risk analysis metrics
    /// </summary>
    public class RiskMetrics
    {
        public decimal ValueAtRisk95 { get; set; }
        public decimal ValueAtRisk99 { get; set; }
        public decimal ConditionalVaR95 { get; set; }
        public decimal ConditionalVaR99 { get; set; }
        public decimal DownsideDeviation { get; set; }
        public decimal SortinoRatio { get; set; }
        public decimal CalmarRatio { get; set; }
        public decimal UlcerIndex { get; set; }
        public decimal TrackingError { get; set; }
        public decimal InformationRatio { get; set; }
        public Dictionary<string, decimal> CorrelationMatrix { get; set; } = new();
        public List<RiskContribution> RiskContributions { get; set; } = new();
    }
    
    /// <summary>
    /// Individual position risk contribution
    /// </summary>
    public class RiskContribution
    {
        public string Symbol { get; set; } = "";
        public decimal Weight { get; set; }
        public decimal Volatility { get; set; }
        public decimal Correlation { get; set; }
        public decimal RiskContributionPercent { get; set; }
        public decimal DiversificationRatio { get; set; }
    }
    
    /// <summary>
    /// Performance data point for equity curve
    /// </summary>
    public class PerformanceDataPoint
    {
        public DateTime Date { get; set; }
        public decimal PortfolioValue { get; set; }
        public decimal DailyReturn { get; set; }
        public decimal CumulativeReturn { get; set; }
        public decimal DrawdownPercent { get; set; }
        public decimal Benchmark { get; set; }
        public decimal BenchmarkReturn { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Benchmark comparison results for service interface
    /// </summary>
    public class BenchmarkComparison
    {
        public string BenchmarkSymbol { get; set; } = "";
        public string BenchmarkName { get; set; } = "";
        public PerformanceMetrics PortfolioMetrics { get; set; } = new();
        public PerformanceMetrics BenchmarkMetrics { get; set; } = new();
        public decimal PortfolioReturn { get; set; }
        public decimal BenchmarkReturn { get; set; }
        public decimal Alpha { get; set; }
        public decimal Beta { get; set; }
        public decimal ExcessReturn { get; set; }
        public decimal TrackingError { get; set; }
        public decimal InformationRatio { get; set; }
        public decimal UpCapture { get; set; }
        public decimal DownCapture { get; set; }
        public List<PerformanceDataPoint> ComparisonChart { get; set; } = new();
    }
    
    /// <summary>
    /// Performance attribution analysis
    /// </summary>
    public class PerformanceAttribution
    {
        public List<SectorAttribution> SectorAttributions { get; set; } = new();
        public List<SecurityAttribution> SecurityAttributions { get; set; } = new();
        public AttributionSummary Summary { get; set; } = new();
    }
    
    /// <summary>
    /// Sector performance attribution
    /// </summary>
    public class SectorAttribution
    {
        public string SectorName { get; set; } = "";
        public decimal PortfolioWeight { get; set; }
        public decimal BenchmarkWeight { get; set; }
        public decimal AllocationEffect { get; set; }
        public decimal SelectionEffect { get; set; }
        public decimal InteractionEffect { get; set; }
        public decimal TotalAttribution { get; set; }
    }
    
    /// <summary>
    /// Individual security performance attribution
    /// </summary>
    public class SecurityAttribution
    {
        public string Symbol { get; set; } = "";
        public string SecurityName { get; set; } = "";
        public string Sector { get; set; } = "";
        public decimal Weight { get; set; }
        public decimal Return { get; set; }
        public decimal Contribution { get; set; }
        public decimal RelativeWeight { get; set; }
        public decimal RelativeReturn { get; set; }
        public decimal Attribution { get; set; }
    }
    
    /// <summary>
    /// Attribution analysis summary
    /// </summary>
    public class AttributionSummary
    {
        public decimal TotalPortfolioReturn { get; set; }
        public decimal TotalBenchmarkReturn { get; set; }
        public decimal TotalActiveReturn { get; set; }
        public decimal TotalAllocationEffect { get; set; }
        public decimal TotalSelectionEffect { get; set; }
        public decimal TotalInteractionEffect { get; set; }
        public decimal UnexplainedReturn { get; set; }
    }
    
    /// <summary>
    /// Portfolio performance periods for different timeframes
    /// </summary>
    public class PerformancePeriods
    {
        public PerformanceMetrics Daily { get; set; } = new();
        public PerformanceMetrics Weekly { get; set; } = new();
        public PerformanceMetrics Monthly { get; set; } = new();
        public PerformanceMetrics Quarterly { get; set; } = new();
        public PerformanceMetrics YearToDate { get; set; } = new();
        public PerformanceMetrics OneYear { get; set; } = new();
        public PerformanceMetrics ThreeYear { get; set; } = new();
        public PerformanceMetrics FiveYear { get; set; } = new();
        public PerformanceMetrics SinceInception { get; set; } = new();
        public Dictionary<string, PerformanceMetrics> CustomPeriods { get; set; } = new();
    }
    
    /// <summary>
    /// Attribution analysis for performance comparison service
    /// </summary>
    public class AttributionAnalysis
    {
        public decimal TotalAttribution { get; set; }
        public decimal AllocationEffect { get; set; }
        public decimal SelectionEffect { get; set; }
        public List<SectorAttribution> SectorAttributions { get; set; } = new();
    }

    /// <summary>
    /// Monte Carlo simulation results with statistical analysis
    /// </summary>
    public class MonteCarloSimulation
    {
        public decimal InitialValue { get; set; }
        public decimal MeanReturn { get; set; }
        public decimal Volatility { get; set; }
        public int TimeHorizonYears { get; set; }
        public int NumberOfSimulations { get; set; }
        public int SimulationCount { get; set; }
        public int DaysSimulated { get; set; }
        public decimal ConfidenceLevel { get; set; }
        public List<decimal> SimulationResults { get; set; } = new();
        public List<decimal> FinalValues { get; set; } = new();
        public List<decimal> MaxDrawdowns { get; set; } = new();
        public List<List<decimal>> SimulationPaths { get; set; } = new();
        public decimal ExpectedValue { get; set; }
        public decimal MedianReturn { get; set; }
        public decimal WorstCaseReturn { get; set; }
        public decimal BestCaseReturn { get; set; }
        public decimal Percentile5 { get; set; }
        public decimal Percentile25 { get; set; }
        public decimal Percentile50 { get; set; }
        public decimal Percentile75 { get; set; }
        public decimal Percentile95 { get; set; }
        public decimal ProbabilityOfLoss { get; set; }
        public decimal ValueAtRisk { get; set; }
        public decimal ConditionalVaR { get; set; }
    }
}
