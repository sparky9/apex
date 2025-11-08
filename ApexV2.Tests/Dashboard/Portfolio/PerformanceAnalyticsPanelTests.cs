using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xunit;
using FluentAssertions;
using Moq;
using ApexV2.Dashboard.Portfolio;
using ApexV2.Charts.Export;

namespace ApexV2.Tests.Dashboard.Portfolio
{
    public class PerformanceAnalyticsPanelTests : IDisposable
    {
        private readonly PerformanceAnalyticsPanel _panel;

        public PerformanceAnalyticsPanelTests()
        {
            // Initialize STA (Single Threaded Apartment) for WPF tests
            if (Application.Current == null)
            {
                var app = new Application();
            }

            _panel = new PerformanceAnalyticsPanel();
        }

        #region Initialization Tests

        [Fact]
        public void Constructor_ShouldInitializeSuccessfully()
        {
            // Assert
            _panel.Should().NotBeNull();
        }

        #endregion

        #region UI Element Tests

        [Fact]
        public void UI_ShouldHaveRequiredElements()
        {
            // Assert - Check for main grid
            var mainGrid = _panel.FindName("MainGrid") as Grid;
            mainGrid.Should().NotBeNull();

            // Check for key UI elements
            var portfolioSelector = _panel.FindName("PortfolioSelector") as ComboBox;
            portfolioSelector.Should().NotBeNull();

            var dateRangePicker = _panel.FindName("DateRangeGrid") as Grid;
            dateRangePicker.Should().NotBeNull();

            var metricsGrid = _panel.FindName("MetricsGrid") as Grid;
            metricsGrid.Should().NotBeNull();

            var chartsTabControl = _panel.FindName("ChartsTabControl") as TabControl;
            chartsTabControl.Should().NotBeNull();
        }

        [Fact]
        public void TabControl_ShouldHaveAllExpectedTabs()
        {
            // Arrange
            var tabControl = _panel.FindName("ChartsTabControl") as TabControl;
            tabControl.Should().NotBeNull();

            // Assert
            tabControl.Items.Count.Should().BeGreaterThan(0);
            
            // Check for specific tabs
            var tabHeaders = new List<string>();
            foreach (TabItem tab in tabControl.Items)
            {
                if (tab.Header is string header)
                {
                    tabHeaders.Add(header);
                }
            }

            tabHeaders.Should().Contain("Performance");
            tabHeaders.Should().Contain("Risk Analysis");
            tabHeaders.Should().Contain("Attribution");
            tabHeaders.Should().Contain("Monte Carlo");
        }

        #endregion

        #region Data Loading Tests

        [Fact]
        public async Task LoadPortfolioData_WithValidPortfolio_ShouldNotThrow()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();

            // Act & Assert
            var action = () => _panel.LoadPortfolioDataAsync(portfolio);
            await action.Should().NotThrowAsync();
        }

        [Fact]
        public async Task LoadPortfolioData_WithNullPortfolio_ShouldHandleGracefully()
        {
            // Act
            var action = () => _panel.LoadPortfolioDataAsync(null);

            // Assert
            await action.Should().NotThrowAsync();
        }

        #endregion

        #region Metrics Display Tests

        [Fact]
        public async Task MetricsDisplay_WithValidData_ShouldNotThrow()
        {
            // Arrange
            var portfolio = CreateSamplePortfolio();

            // Act & Assert
            var action = () => _panel.LoadPortfolioDataAsync(portfolio);
            await action.Should().NotThrowAsync();
        }

        #endregion

        #region Chart Generation Tests

        [Fact]
        public void GeneratePerformanceChart_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => _panel.GeneratePerformanceChart();
            action.Should().NotThrow();
        }

        [Fact]
        public void GenerateRiskChart_ShouldNotThrow()
        {
            // Act & Assert
            var action = () => _panel.GenerateRiskChart();
            action.Should().NotThrow();
        }

        #endregion

        #region Error Handling Tests

        [Fact]
        public async Task LoadPortfolioData_WithInvalidData_ShouldHandleGracefully()
        {
            // Arrange
            var portfolio = new ApexV2.Dashboard.Portfolio.Portfolio();

            // Act & Assert
            var action = () => _panel.LoadPortfolioDataAsync(portfolio);
            await action.Should().NotThrowAsync();
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
                CreatedDate = DateTime.Now.AddMonths(-6),
                Positions = new List<Position>
                {
                    new Position
                    {
                        Symbol = "AAPL",
                        Quantity = 100,
                        MarketValue = 30000m,
                        Sector = "Technology"
                    },
                    new Position
                    {
                        Symbol = "MSFT",
                        Quantity = 80,
                        MarketValue = 25000m,
                        Sector = "Technology"
                    }
                }
            };
        }

        private PerformanceMetrics CreateSamplePerformanceMetrics()
        {
            return new PerformanceMetrics
            {
                TotalReturn = 12.5m,
                AnnualizedReturn = 11.8m,
                Volatility = 15.2m,
                SharpeRatio = 0.78m,
                MaxDrawdown = -8.3m,
                TotalTrades = 45,
                WinRate = 68.9m,
                ProfitFactor = 1.95m
            };
        }

        private RiskMetrics CreateSampleRiskMetrics()
        {
            return new RiskMetrics
            {
                Beta = 1.05m,
                Alpha = 2.3m,
                RSquared = 0.85m,
                StandardDeviation = 15.2m,
                DownsideDeviation = 8.7m,
                SortinoRatio = 1.24m,
                TreynorRatio = 0.89m,
                CalmarRatio = 1.42m,
                UlcerIndex = 5.8m,
                MaximumDrawdown = -12.5m,
                RecoveryTime = 45,
                ConditionalValueAtRisk = -8.9m
            };
        }

        public void Dispose()
        {
            // Clean up WPF resources if needed
            _panel?.Resources?.Clear();
        }

        #endregion
    }
}
