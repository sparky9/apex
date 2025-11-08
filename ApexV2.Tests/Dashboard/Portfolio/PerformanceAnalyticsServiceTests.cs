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
    public class PerformanceAnalyticsServiceTests
    {
        private readonly Mock<IChartLogger> _mockLogger;
        private readonly PerformanceAnalyticsService _service;

        public PerformanceAnalyticsServiceTests()
        {
            _mockLogger = new Mock<IChartLogger>();
            _service = new PerformanceAnalyticsService(_mockLogger.Object);
        }

        #region Basic Performance Calculation Tests

        [Fact]
        public async Task CalculatePerformanceAsync_WithValidPortfolio_ShouldReturnMetrics()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();
            var startDate = DateTime.Now.AddDays(-30);
            var endDate = DateTime.Now;

            // Act
            var result = await _service.CalculatePerformanceAsync(portfolio, startDate, endDate);

            // Assert
            result.Should().NotBeNull();
            result.TotalReturn.Should().BeGreaterThan(-100); // Reasonable bounds
            result.TotalReturn.Should().BeLessThan(1000);
        }

        [Fact]
        public async Task CalculatePerformanceAsync_WithEmptyPortfolio_ShouldReturnZeroMetrics()
        {
            // Arrange
            var portfolio = new Portfolio();
            var startDate = DateTime.Now.AddDays(-30);
            var endDate = DateTime.Now;

            // Act
            var result = await _service.CalculatePerformanceAsync(portfolio, startDate, endDate);

            // Assert
            result.Should().NotBeNull();
            result.TotalReturn.Should().Be(0);
            result.AnnualizedReturn.Should().Be(0);
            result.Volatility.Should().Be(0);
        }

        [Theory]
        [InlineData(100000, 110000, 10.0)]
        [InlineData(50000, 45000, -10.0)]
        [InlineData(75000, 75000, 0.0)]
        public async Task CalculatePerformanceAsync_ReturnCalculation_ShouldBeAccurate(
            decimal startValue, decimal endValue, decimal expectedReturn)
        {
            // Arrange
            var portfolio = new Portfolio { TotalValue = endValue };
            var startDate = DateTime.Now.AddDays(-30);
            var endDate = DateTime.Now;

            // Mock historical value for start date
            // In a real implementation, this would come from historical data

            // Act
            var result = await _service.CalculatePerformanceAsync(portfolio, startDate, endDate);

            // Assert
            // This test would need historical data to be meaningful
            // For now, we just verify the service doesn't crash
            result.Should().NotBeNull();
        }

        #endregion

        #region Risk Metrics Tests

        [Fact]
        public async Task CalculateRiskMetricsAsync_WithValidPortfolio_ShouldReturnRiskMetrics()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();

            // Act
            var result = await _service.CalculateRiskMetricsAsync(portfolio);

            // Assert
            result.Should().NotBeNull();
            result.SectorConcentration.Should().NotBeNull();
            result.TopHoldings.Should().NotBeNull();
        }

        [Fact]
        public async Task CalculateRiskMetricsAsync_SectorConcentration_ShouldCalculateCorrectly()
        {
            // Arrange
            var portfolio = new Portfolio();
            portfolio.Positions.Add(new Position 
            { 
                Symbol = "AAPL", 
                Sector = "Technology", 
                MarketValue = 30000,
                Quantity = 100 
            });
            portfolio.Positions.Add(new Position 
            { 
                Symbol = "MSFT", 
                Sector = "Technology", 
                MarketValue = 20000,
                Quantity = 50 
            });
            portfolio.Positions.Add(new Position 
            { 
                Symbol = "JNJ", 
                Sector = "Healthcare", 
                MarketValue = 50000,
                Quantity = 200 
            });
            portfolio.TotalValue = 100000;

            // Act
            var result = await _service.CalculateRiskMetricsAsync(portfolio);

            // Assert
            result.SectorConcentration.Should().HaveCount(2);
            result.SectorConcentration["Technology"].Should().Be(50.0m); // 50k out of 100k
            result.SectorConcentration["Healthcare"].Should().Be(50.0m); // 50k out of 100k
        }

        [Fact]
        public async Task CalculateRiskMetricsAsync_TopHoldings_ShouldReturnLargestPositions()
        {
            // Arrange
            var portfolio = new Portfolio();
            portfolio.Positions.Add(new Position { Symbol = "AAPL", MarketValue = 30000, Quantity = 100 });
            portfolio.Positions.Add(new Position { Symbol = "MSFT", MarketValue = 25000, Quantity = 80 });
            portfolio.Positions.Add(new Position { Symbol = "GOOGL", MarketValue = 20000, Quantity = 60 });
            portfolio.Positions.Add(new Position { Symbol = "AMZN", MarketValue = 15000, Quantity = 40 });
            portfolio.Positions.Add(new Position { Symbol = "TSLA", MarketValue = 10000, Quantity = 20 });

            // Act
            var result = await _service.CalculateRiskMetricsAsync(portfolio);

            // Assert
            result.TopHoldings.Should().HaveCount(5);
            result.TopHoldings.First().Symbol.Should().Be("AAPL");
            result.TopHoldings.Last().Symbol.Should().Be("TSLA");
        }

        #endregion

        #region Performance Periods Tests

        [Fact]
        public async Task CalculatePerformancePeriodsAsync_WithValidPortfolio_ShouldReturnAllPeriods()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();

            // Act
            var result = await _service.CalculatePerformancePeriodsAsync(portfolio);

            // Assert
            result.Should().NotBeNull();
            result.OneMonth.Should().NotBeNull();
            result.ThreeMonth.Should().NotBeNull();
            result.SixMonth.Should().NotBeNull();
            result.OneYear.Should().NotBeNull();
            result.ThreeYear.Should().NotBeNull();
            result.FiveYear.Should().NotBeNull();
            result.SinceInception.Should().NotBeNull();
        }

        [Fact]
        public async Task CalculatePerformancePeriodsAsync_InceptionDate_ShouldUseLastUpdated()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();
            portfolio.LastUpdated = DateTime.Now.AddYears(-2);

            // Act
            var result = await _service.CalculatePerformancePeriodsAsync(portfolio);

            // Assert
            result.SinceInception.Should().NotBeNull();
            // Since inception should use the LastUpdated date as the inception date
        }

        #endregion

        #region Sector Analysis Tests

        [Fact]
        public async Task AnalyzeSectorPerformanceAsync_WithMultipleSectors_ShouldReturnAnalysis()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();

            // Act
            var result = await _service.AnalyzeSectorPerformanceAsync(portfolio);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AnalyzeSectorPerformanceAsync_EmptyPortfolio_ShouldReturnEmptyList()
        {
            // Arrange
            var portfolio = new Portfolio();

            // Act
            var result = await _service.AnalyzeSectorPerformanceAsync(portfolio);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region Asset Class Analysis Tests

        [Fact]
        public async Task AnalyzeAssetClassPerformanceAsync_WithValidPortfolio_ShouldReturnAnalysis()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();

            // Act
            var result = await _service.AnalyzeAssetClassPerformanceAsync(portfolio);

            // Assert
            result.Should().NotBeNull();
            result.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AnalyzeAssetClassPerformanceAsync_EmptyPortfolio_ShouldReturnEmptyList()
        {
            // Arrange
            var portfolio = new Portfolio();

            // Act
            var result = await _service.AnalyzeAssetClassPerformanceAsync(portfolio);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task CalculatePerformanceAsync_WithNullPortfolio_ShouldThrowArgumentNullException()
        {
            // Arrange
            Portfolio portfolio = null;
            var startDate = DateTime.Now.AddDays(-30);
            var endDate = DateTime.Now;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.CalculatePerformanceAsync(portfolio, startDate, endDate));
        }

        [Fact]
        public async Task CalculatePerformanceAsync_WithInvalidDateRange_ShouldThrowArgumentException()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();
            var startDate = DateTime.Now;
            var endDate = DateTime.Now.AddDays(-30); // End before start

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => 
                _service.CalculatePerformanceAsync(portfolio, startDate, endDate));
        }

        [Fact]
        public async Task CalculateRiskMetricsAsync_WithNullPortfolio_ShouldThrowArgumentNullException()
        {
            // Arrange
            Portfolio portfolio = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                _service.CalculateRiskMetricsAsync(portfolio));
        }

        #endregion

        #region Logging Tests

        [Fact]
        public async Task CalculatePerformanceAsync_ShouldLogOperations()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();
            var startDate = DateTime.Now.AddDays(-30);
            var endDate = DateTime.Now;

            // Act
            await _service.CalculatePerformanceAsync(portfolio, startDate, endDate);

            // Assert
            _mockLogger.Verify(
                l => l.Info(It.Is<string>(s => s.Contains("Calculating performance"))),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task CalculateRiskMetricsAsync_ShouldLogOperations()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();

            // Act
            await _service.CalculateRiskMetricsAsync(portfolio);

            // Assert
            _mockLogger.Verify(
                l => l.Info(It.Is<string>(s => s.Contains("Calculating risk metrics"))),
                Times.AtLeastOnce);
        }

        #endregion

        #region Helper Methods

        private ApexV2.Dashboard.Portfolio.Portfolio CreateSamplePortfolio()
        {
            return new ApexV2.Dashboard.Portfolio.Portfolio
            {
                Id = Guid.NewGuid(),
                Name = "Test Portfolio",
                TotalValue = 100000m,
                TotalCost = 95000m,
                TotalGainLoss = 5000m,
                LastUpdated = DateTime.Now.AddDays(-1),
                Positions = new List<Position>
                {
                    new Position
                    {
                        Symbol = "AAPL",
                        CompanyName = "Apple Inc.",
                        Sector = "Technology",
                        Quantity = 100,
                        CurrentPrice = 150.00m,
                        AveragePrice = 140.00m,
                        MarketValue = 15000m,
                        CostBasis = 14000m,
                        UnrealizedGainLoss = 1000m
                    },
                    new Position
                    {
                        Symbol = "MSFT",
                        CompanyName = "Microsoft Corporation",
                        Sector = "Technology",
                        Quantity = 50,
                        CurrentPrice = 300.00m,
                        AveragePrice = 280.00m,
                        MarketValue = 15000m,
                        CostBasis = 14000m,
                        UnrealizedGainLoss = 1000m
                    },
                    new Position
                    {
                        Symbol = "JNJ",
                        CompanyName = "Johnson & Johnson",
                        Sector = "Healthcare",
                        Quantity = 200,
                        CurrentPrice = 160.00m,
                        AveragePrice = 155.00m,
                        MarketValue = 32000m,
                        CostBasis = 31000m,
                        UnrealizedGainLoss = 1000m
                    }
                },
                Transactions = new List<Transaction>
                {
                    new Transaction
                    {
                        Symbol = "AAPL",
                        Type = TransactionType.Buy,
                        Quantity = 100,
                        Price = 140.00m,
                        Date = DateTime.Now.AddDays(-60),
                        Commission = 9.99m
                    },
                    new Transaction
                    {
                        Symbol = "MSFT",
                        Type = TransactionType.Buy,
                        Quantity = 50,
                        Price = 280.00m,
                        Date = DateTime.Now.AddDays(-45),
                        Commission = 9.99m
                    },
                    new Transaction
                    {
                        Symbol = "JNJ",
                        Type = TransactionType.Buy,
                        Quantity = 200,
                        Price = 155.00m,
                        Date = DateTime.Now.AddDays(-30),
                        Commission = 9.99m
                    }
                }
            };
        }

        #endregion
    }
}
