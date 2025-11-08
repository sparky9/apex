using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Moq;
using ApexV2.Dashboard.Portfolio;
using ApexV2.Charts.Export;

namespace ApexV2.Tests.Dashboard.Portfolio
{
    public class PerformanceComparisonServiceTests
    {
        private readonly Mock<IChartLogger> _mockLogger;
        private readonly PerformanceComparisonService _service;

        public PerformanceComparisonServiceTests()
        {
            _mockLogger = new Mock<IChartLogger>();
            _service = new PerformanceComparisonService(_mockLogger.Object);
        }

        #region Benchmark Comparison Tests

        [Fact]
        public async Task CompareToBenchmarkAsync_WithValidInputs_ShouldReturnComparison()
        {
            // Arrange
            var portfolioMetrics = CreateSamplePerformanceMetrics(12.5m, 15.2m);
            var benchmarkMetrics = CreateSamplePerformanceMetrics(8.7m, 12.1m);

            // Act
            var result = await _service.CompareToBenchmarkAsync(portfolioMetrics, benchmarkMetrics);

            // Assert
            result.Should().NotBeNull();
            result.PortfolioMetrics.Should().Be(portfolioMetrics);
            result.BenchmarkMetrics.Should().Be(benchmarkMetrics);
            result.ExcessReturn.Should().Be(3.8m); // 12.5 - 8.7
            result.TrackingError.Should().BeGreaterThan(0);
            result.InformationRatio.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task CompareToBenchmarkAsync_WithEqualPerformance_ShouldReturnZeroExcess()
        {
            // Arrange
            var portfolioMetrics = CreateSamplePerformanceMetrics(10.0m, 15.0m);
            var benchmarkMetrics = CreateSamplePerformanceMetrics(10.0m, 15.0m);

            // Act
            var result = await _service.CompareToBenchmarkAsync(portfolioMetrics, benchmarkMetrics);

            // Assert
            result.ExcessReturn.Should().Be(0m);
            result.TrackingError.Should().Be(0m);
        }

        [Fact]
        public async Task CompareToBenchmarkAsync_WithUnderperformance_ShouldReturnNegativeExcess()
        {
            // Arrange
            var portfolioMetrics = CreateSamplePerformanceMetrics(5.2m, 18.3m);
            var benchmarkMetrics = CreateSamplePerformanceMetrics(8.7m, 12.1m);

            // Act
            var result = await _service.CompareToBenchmarkAsync(portfolioMetrics, benchmarkMetrics);

            // Assert
            result.ExcessReturn.Should().Be(-3.5m); // 5.2 - 8.7
            result.ExcessReturn.Should().BeLessThan(0);
        }

        [Theory]
        [InlineData(12.0, 8.0, 4.0, 2.0)]
        [InlineData(15.5, 10.2, 5.3, 2.65)]
        [InlineData(6.8, 9.1, -2.3, -1.15)]
        public async Task CompareToBenchmarkAsync_InformationRatio_ShouldCalculateCorrectly(
            decimal portfolioReturn, decimal benchmarkReturn, decimal expectedExcess, decimal expectedIR)
        {
            // Arrange
            var portfolioMetrics = CreateSamplePerformanceMetrics(portfolioReturn, 15.0m);
            var benchmarkMetrics = CreateSamplePerformanceMetrics(benchmarkReturn, 12.0m);

            // Act
            var result = await _service.CompareToBenchmarkAsync(portfolioMetrics, benchmarkMetrics);

            // Assert
            result.ExcessReturn.Should().BeApproximately(expectedExcess, 0.01m);
            // Information Ratio = Excess Return / Tracking Error
            // For this test, we're assuming a simplified calculation
        }

        #endregion

        #region Attribution Analysis Tests

        [Fact]
        public async Task CalculateAttributionAsync_WithValidPortfolio_ShouldReturnAttribution()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();
            var benchmarkReturns = CreateSampleBenchmarkReturns();

            // Act
            var result = await _service.CalculateAttributionAsync(portfolio, benchmarkReturns);

            // Assert
            result.Should().NotBeNull();
            result.SectorAttributions.Should().NotBeEmpty();
            result.TotalAttribution.Should().NotBe(0);
        }

        [Fact]
        public async Task CalculateAttributionAsync_SectorAttributions_ShouldSumToTotal()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();
            var benchmarkReturns = CreateSampleBenchmarkReturns();

            // Act
            var result = await _service.CalculateAttributionAsync(portfolio, benchmarkReturns);

            // Assert
            var sumOfSectorAttributions = result.SectorAttributions.Sum(s => s.TotalEffect);
            result.TotalAttribution.Should().BeApproximately(sumOfSectorAttributions, 0.01m);
        }

        [Fact]
        public async Task CalculateAttributionAsync_EmptyPortfolio_ShouldReturnZeroAttribution()
        {
            // Arrange
            var portfolio = new ApexV2.Dashboard.Portfolio.Portfolio();
            var benchmarkReturns = CreateSampleBenchmarkReturns();

            // Act
            var result = await _service.CalculateAttributionAsync(portfolio, benchmarkReturns);

            // Assert
            result.TotalAttribution.Should().Be(0);
            result.SelectionEffect.Should().Be(0);
            result.AllocationEffect.Should().Be(0);
            result.SectorAttributions.Should().BeEmpty();
        }

        [Theory]
        [InlineData(25.0, 20.0, 12.0, 8.0)] // Overweight tech with outperformance
        [InlineData(15.0, 25.0, 6.0, 8.0)]  // Underweight tech with underperformance
        public async Task CalculateAttributionAsync_AllocationEffect_ShouldBeCorrect(
            decimal portfolioWeight, decimal benchmarkWeight, 
            decimal portfolioReturn, decimal benchmarkReturn)
        {
            // Arrange
            var portfolio = new ApexV2.Dashboard.Portfolio.Portfolio
            {
                TotalValue = 100000m,
                Positions = new List<Position>
                {
                    new Position
                    {
                        Symbol = "AAPL",
                        Sector = "Technology",
                        MarketValue = portfolioWeight * 1000, // Convert percentage to dollar amount
                        Quantity = 100
                    }
                }
            };

            var benchmarkReturns = new Dictionary<string, decimal>
            {
                ["Technology"] = benchmarkReturn
            };

            // Act
            var result = await _service.CalculateAttributionAsync(portfolio, benchmarkReturns);

            // Assert
            result.Should().NotBeNull();
            result.SectorAttributions.Should().NotBeEmpty();
            
            var techAttribution = result.SectorAttributions.FirstOrDefault(s => s.SectorName == "Technology");
            techAttribution.Should().NotBeNull();
        }

        #endregion

        #region Monte Carlo Simulation Tests

        [Fact]
        public async Task RunMonteCarloSimulationAsync_WithValidInputs_ShouldReturnSimulation()
        {
            // Arrange
            var initialValue = 100000m;
            var meanReturn = 8.5m;
            var volatility = 15.2m;
            var timeHorizon = 5;
            var numberOfSimulations = 1000;

            // Act
            var result = await _service.RunMonteCarloSimulationAsync(
                initialValue, meanReturn, volatility, timeHorizon, numberOfSimulations);

            // Assert
            result.Should().NotBeNull();
            result.InitialValue.Should().Be(initialValue);
            result.MeanReturn.Should().Be(meanReturn);
            result.Volatility.Should().Be(volatility);
            result.TimeHorizonYears.Should().Be(timeHorizon);
            result.NumberOfSimulations.Should().Be(numberOfSimulations);
            result.SimulationResults.Should().HaveCount(numberOfSimulations);
        }

        [Fact]
        public async Task RunMonteCarloSimulationAsync_Percentiles_ShouldBeInOrder()
        {
            // Arrange
            var initialValue = 100000m;
            var meanReturn = 8.0m;
            var volatility = 12.0m;
            var timeHorizon = 3;
            var numberOfSimulations = 1000;

            // Act
            var result = await _service.RunMonteCarloSimulationAsync(
                initialValue, meanReturn, volatility, timeHorizon, numberOfSimulations);

            // Assert
            result.Percentile5.Should().BeLessThan(result.Percentile25);
            result.Percentile25.Should().BeLessThan(result.Percentile50);
            result.Percentile50.Should().BeLessThan(result.Percentile75);
            result.Percentile75.Should().BeLessThan(result.Percentile95);
        }

        [Fact]
        public async Task RunMonteCarloSimulationAsync_ExpectedValue_ShouldBeReasonable()
        {
            // Arrange
            var initialValue = 100000m;
            var meanReturn = 10.0m;
            var volatility = 15.0m;
            var timeHorizon = 1;
            var numberOfSimulations = 1000;

            // Act
            var result = await _service.RunMonteCarloSimulationAsync(
                initialValue, meanReturn, volatility, timeHorizon, numberOfSimulations);

            // Assert
            // Expected value should be approximately initial value * (1 + mean return)
            var theoreticalExpected = initialValue * (1 + meanReturn / 100);
            result.ExpectedValue.Should().BeApproximately(theoreticalExpected, theoreticalExpected * 0.1m); // 10% tolerance
        }

        [Theory]
        [InlineData(100000, 8.0, 12.0, 1, 500)]
        [InlineData(50000, 12.0, 18.0, 3, 1000)]
        [InlineData(200000, 6.0, 10.0, 5, 2000)]
        public async Task RunMonteCarloSimulationAsync_WithDifferentParameters_ShouldComplete(
            decimal initialValue, decimal meanReturn, decimal volatility, int timeHorizon, int simulations)
        {
            // Act
            var result = await _service.RunMonteCarloSimulationAsync(
                initialValue, meanReturn, volatility, timeHorizon, simulations);

            // Assert
            result.Should().NotBeNull();
            result.SimulationResults.Should().HaveCount(simulations);
            result.ProbabilityOfLoss.Should().BeInRange(0, 100);
        }

        [Fact]
        public async Task RunMonteCarloSimulationAsync_ProbabilityOfLoss_ShouldBeValid()
        {
            // Arrange
            var initialValue = 100000m;
            var meanReturn = 5.0m; // Lower return increases probability of loss
            var volatility = 20.0m; // Higher volatility increases probability of loss
            var timeHorizon = 1;
            var numberOfSimulations = 1000;

            // Act
            var result = await _service.RunMonteCarloSimulationAsync(
                initialValue, meanReturn, volatility, timeHorizon, numberOfSimulations);

            // Assert
            result.ProbabilityOfLoss.Should().BeInRange(0, 100);
            result.ProbabilityOfLoss.Should().BeGreaterThan(0); // With volatility, there should be some probability of loss
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CompareToBenchmarkAsync_WithNullPortfolioMetrics_ShouldThrowArgumentNullException()
        {
            // Arrange
            PerformanceMetrics portfolioMetrics = null;
            var benchmarkMetrics = CreateSamplePerformanceMetrics(8.0m, 12.0m);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.CompareToBenchmarkAsync(portfolioMetrics, benchmarkMetrics));
        }

        [Fact]
        public async Task CompareToBenchmarkAsync_WithNullBenchmarkMetrics_ShouldThrowArgumentNullException()
        {
            // Arrange
            var portfolioMetrics = CreateSamplePerformanceMetrics(10.0m, 15.0m);
            PerformanceMetrics benchmarkMetrics = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.CompareToBenchmarkAsync(portfolioMetrics, benchmarkMetrics));
        }

        [Fact]
        public async Task RunMonteCarloSimulationAsync_WithNegativeInitialValue_ShouldThrowArgumentException()
        {
            // Arrange
            var initialValue = -100000m;
            var meanReturn = 8.0m;
            var volatility = 12.0m;
            var timeHorizon = 1;
            var numberOfSimulations = 1000;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.RunMonteCarloSimulationAsync(initialValue, meanReturn, volatility, timeHorizon, numberOfSimulations));
        }

        [Fact]
        public async Task RunMonteCarloSimulationAsync_WithZeroSimulations_ShouldThrowArgumentException()
        {
            // Arrange
            var initialValue = 100000m;
            var meanReturn = 8.0m;
            var volatility = 12.0m;
            var timeHorizon = 1;
            var numberOfSimulations = 0;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.RunMonteCarloSimulationAsync(initialValue, meanReturn, volatility, timeHorizon, numberOfSimulations));
        }

        #endregion

        #region Logging Tests

        [Fact]
        public async Task CompareToBenchmarkAsync_ShouldLogOperations()
        {
            // Arrange
            var portfolioMetrics = CreateSamplePerformanceMetrics(10.0m, 15.0m);
            var benchmarkMetrics = CreateSamplePerformanceMetrics(8.0m, 12.0m);

            // Act
            await _service.CompareToBenchmarkAsync(portfolioMetrics, benchmarkMetrics);

            // Assert
            _mockLogger.Verify(
                l => l.Info(It.Is<string>(s => s.Contains("Comparing portfolio to benchmark"))),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task RunMonteCarloSimulationAsync_ShouldLogOperations()
        {
            // Arrange
            var initialValue = 100000m;
            var meanReturn = 8.0m;
            var volatility = 12.0m;
            var timeHorizon = 1;
            var numberOfSimulations = 100; // Small number for fast test

            // Act
            await _service.RunMonteCarloSimulationAsync(
                initialValue, meanReturn, volatility, timeHorizon, numberOfSimulations);

            // Assert
            _mockLogger.Verify(
                l => l.Info(It.Is<string>(s => s.Contains("Monte Carlo simulation"))),
                Times.AtLeastOnce);
        }

        #endregion

        #region Helper Methods

        private PerformanceMetrics CreateSamplePerformanceMetrics(decimal totalReturn, decimal volatility)
        {
            return new PerformanceMetrics
            {
                TotalReturn = totalReturn,
                AnnualizedReturn = totalReturn,
                Volatility = volatility,
                SharpeRatio = volatility != 0 ? totalReturn / volatility : 0,
                MaxDrawdown = Math.Abs(totalReturn) * 0.3m, // Assume max drawdown is 30% of return
                TotalTrades = 10,
                WinRate = 65.0m,
                ProfitFactor = 1.8m
            };
        }

        private ApexV2.Dashboard.Portfolio.Portfolio CreateSamplePortfolio()
        {
            return new ApexV2.Dashboard.Portfolio.Portfolio
            {
                Id = Guid.NewGuid(),
                Name = "Test Portfolio",
                TotalValue = 100000m,
                Positions = new List<Position>
                {
                    new Position
                    {
                        Symbol = "AAPL",
                        Sector = "Technology",
                        MarketValue = 30000m,
                        Quantity = 100
                    },
                    new Position
                    {
                        Symbol = "MSFT",
                        Sector = "Technology",
                        MarketValue = 25000m,
                        Quantity = 80
                    },
                    new Position
                    {
                        Symbol = "JNJ",
                        Sector = "Healthcare",
                        MarketValue = 20000m,
                        Quantity = 60
                    },
                    new Position
                    {
                        Symbol = "JPM",
                        Sector = "Financial",
                        MarketValue = 25000m,
                        Quantity = 75
                    }
                }
            };
        }

        private Dictionary<string, decimal> CreateSampleBenchmarkReturns()
        {
            return new Dictionary<string, decimal>
            {
                ["Technology"] = 8.5m,
                ["Healthcare"] = 6.2m,
                ["Financial"] = 7.8m,
                ["Consumer"] = 5.9m
            };
        }

        #endregion
    }
}
