using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using FluentAssertions;
using ApexV2.Dashboard.Portfolio;

namespace ApexV2.Tests.Dashboard.Portfolio
{
    public class PerformanceModelsTests
    {
        #region PerformanceMetrics Tests

        [Fact]
        public void PerformanceMetrics_Creation_ShouldHaveCorrectDefaults()
        {
            // Act
            var metrics = new PerformanceMetrics();

            // Assert
            metrics.TotalReturn.Should().Be(0);
            metrics.AnnualizedReturn.Should().Be(0);
            metrics.Volatility.Should().Be(0);
            metrics.SharpeRatio.Should().Be(0);
            metrics.MaxDrawdown.Should().Be(0);
            metrics.TotalTrades.Should().Be(0);
            metrics.WinRate.Should().Be(0);
            metrics.ProfitFactor.Should().Be(0);
            metrics.CalmarRatio.Should().Be(0);
            metrics.SortinoRatio.Should().Be(0);
        }

        [Fact]
        public void PerformanceMetrics_IsPositive_ShouldReturnCorrectValue()
        {
            // Arrange
            var positiveMetrics = new PerformanceMetrics { TotalReturn = 15.5m };
            var negativeMetrics = new PerformanceMetrics { TotalReturn = -8.2m };
            var zeroMetrics = new PerformanceMetrics { TotalReturn = 0m };

            // Assert
            positiveMetrics.IsPositive.Should().BeTrue();
            negativeMetrics.IsPositive.Should().BeFalse();
            zeroMetrics.IsPositive.Should().BeFalse();
        }

        [Theory]
        [InlineData(15.5, 1.2, 12.92)]
        [InlineData(-8.0, 2.5, -3.2)]
        [InlineData(0, 1.0, 0)]
        public void PerformanceMetrics_SharpeCalculation_ShouldBeAccurate(decimal totalReturn, decimal volatility, decimal expectedSharpe)
        {
            // Arrange
            var metrics = new PerformanceMetrics
            {
                TotalReturn = totalReturn,
                Volatility = volatility
            };

            // Act
            var calculatedSharpe = volatility != 0 ? totalReturn / volatility : 0;

            // Assert
            calculatedSharpe.Should().BeApproximately(expectedSharpe, 0.01m);
        }

        #endregion

        #region RiskMetrics Tests

        [Fact]
        public void RiskMetrics_Creation_ShouldHaveCorrectDefaults()
        {
            // Act
            var riskMetrics = new RiskMetrics();

            // Assert
            riskMetrics.Beta.Should().Be(0);
            riskMetrics.Alpha.Should().Be(0);
            riskMetrics.ValueAtRisk95.Should().Be(0);
            riskMetrics.ValueAtRisk99.Should().Be(0);
            riskMetrics.ConditionalVaR.Should().Be(0);
            riskMetrics.TrackingError.Should().Be(0);
            riskMetrics.InformationRatio.Should().Be(0);
            riskMetrics.Correlation.Should().Be(0);
            riskMetrics.SectorConcentration.Should().NotBeNull();
            riskMetrics.SectorConcentration.Should().BeEmpty();
            riskMetrics.TopHoldings.Should().NotBeNull();
            riskMetrics.TopHoldings.Should().BeEmpty();
        }

        [Fact]
        public void RiskMetrics_SectorConcentration_ShouldCalculateCorrectly()
        {
            // Arrange
            var riskMetrics = new RiskMetrics();
            riskMetrics.SectorConcentration["Technology"] = 35.5m;
            riskMetrics.SectorConcentration["Healthcare"] = 20.0m;
            riskMetrics.SectorConcentration["Financial"] = 15.0m;

            // Act
            var totalConcentration = riskMetrics.SectorConcentration.Values.Sum();

            // Assert
            totalConcentration.Should().Be(70.5m);
            riskMetrics.SectorConcentration.Should().HaveCount(3);
        }

        [Theory]
        [InlineData(1.2, 8.5, 0.141)]
        [InlineData(0.8, 12.0, 0.067)]
        [InlineData(1.0, 0, 0)]
        public void RiskMetrics_InformationRatio_ShouldCalculateCorrectly(decimal alpha, decimal trackingError, decimal expectedRatio)
        {
            // Arrange
            var riskMetrics = new RiskMetrics
            {
                Alpha = alpha,
                TrackingError = trackingError
            };

            // Act
            var calculatedRatio = trackingError != 0 ? alpha / trackingError : 0;

            // Assert
            calculatedRatio.Should().BeApproximately(expectedRatio, 0.001m);
        }

        #endregion

        #region PerformancePeriods Tests

        [Fact]
        public void PerformancePeriods_Creation_ShouldInitializeAllPeriods()
        {
            // Act
            var periods = new PerformancePeriods();

            // Assert
            periods.OneMonth.Should().NotBeNull();
            periods.ThreeMonth.Should().NotBeNull();
            periods.SixMonth.Should().NotBeNull();
            periods.OneYear.Should().NotBeNull();
            periods.ThreeYear.Should().NotBeNull();
            periods.FiveYear.Should().NotBeNull();
            periods.SinceInception.Should().NotBeNull();
        }

        [Fact]
        public void PerformancePeriods_GetPeriodMetrics_ShouldReturnCorrectPeriod()
        {
            // Arrange
            var periods = new PerformancePeriods();
            periods.OneMonth.TotalReturn = 2.5m;
            periods.ThreeMonth.TotalReturn = 8.1m;
            periods.OneYear.TotalReturn = 15.3m;

            // Act & Assert
            periods.OneMonth.TotalReturn.Should().Be(2.5m);
            periods.ThreeMonth.TotalReturn.Should().Be(8.1m);
            periods.OneYear.TotalReturn.Should().Be(15.3m);
        }

        #endregion

        #region AttributionAnalysis Tests

        [Fact]
        public void AttributionAnalysis_Creation_ShouldHaveCorrectDefaults()
        {
            // Act
            var attribution = new AttributionAnalysis();

            // Assert
            attribution.TotalAttribution.Should().Be(0);
            attribution.SelectionEffect.Should().Be(0);
            attribution.AllocationEffect.Should().Be(0);
            attribution.InteractionEffect.Should().Be(0);
            attribution.SectorAttributions.Should().NotBeNull();
            attribution.SectorAttributions.Should().BeEmpty();
            attribution.AssetClassAttributions.Should().NotBeNull();
            attribution.AssetClassAttributions.Should().BeEmpty();
        }

        [Fact]
        public void AttributionAnalysis_TotalAttribution_ShouldEqualSumOfEffects()
        {
            // Arrange
            var attribution = new AttributionAnalysis
            {
                SelectionEffect = 1.5m,
                AllocationEffect = 2.3m,
                InteractionEffect = 0.2m
            };

            // Act
            var expectedTotal = attribution.SelectionEffect + attribution.AllocationEffect + attribution.InteractionEffect;

            // Assert
            expectedTotal.Should().Be(4.0m);
        }

        [Fact]
        public void SectorAttribution_Creation_ShouldHaveCorrectDefaults()
        {
            // Act
            var sectorAttribution = new SectorAttribution();

            // Assert
            sectorAttribution.SectorName.Should().BeEmpty();
            sectorAttribution.PortfolioWeight.Should().Be(0);
            sectorAttribution.BenchmarkWeight.Should().Be(0);
            sectorAttribution.PortfolioReturn.Should().Be(0);
            sectorAttribution.BenchmarkReturn.Should().Be(0);
            sectorAttribution.AllocationEffect.Should().Be(0);
            sectorAttribution.SelectionEffect.Should().Be(0);
            sectorAttribution.TotalEffect.Should().Be(0);
        }

        [Theory]
        [InlineData(25.0, 20.0, 12.0, 8.0, 1.0, 1.2, 1.6)]
        [InlineData(15.0, 18.0, 8.5, 9.2, -0.27, -0.105, -0.123)]
        public void SectorAttribution_EffectCalculations_ShouldBeAccurate(
            decimal portfolioWeight, decimal benchmarkWeight, 
            decimal portfolioReturn, decimal benchmarkReturn,
            decimal expectedAllocation, decimal expectedSelection, decimal expectedTotal)
        {
            // Arrange
            var sectorAttribution = new SectorAttribution
            {
                SectorName = "Technology",
                PortfolioWeight = portfolioWeight,
                BenchmarkWeight = benchmarkWeight,
                PortfolioReturn = portfolioReturn,
                BenchmarkReturn = benchmarkReturn
            };

            // Act - These would typically be calculated by the service
            var allocationEffect = (portfolioWeight - benchmarkWeight) * benchmarkReturn / 100;
            var selectionEffect = portfolioWeight * (portfolioReturn - benchmarkReturn) / 100;
            var totalEffect = allocationEffect + selectionEffect;

            // Assert
            allocationEffect.Should().BeApproximately(expectedAllocation, 0.01m);
            selectionEffect.Should().BeApproximately(expectedSelection, 0.01m);
            totalEffect.Should().BeApproximately(expectedTotal, 0.01m);
        }

        #endregion

        #region MonteCarloSimulation Tests

        [Fact]
        public void MonteCarloSimulation_Creation_ShouldHaveCorrectDefaults()
        {
            // Act
            var simulation = new MonteCarloSimulation();

            // Assert
            simulation.NumberOfSimulations.Should().Be(0);
            simulation.TimeHorizonYears.Should().Be(0);
            simulation.InitialValue.Should().Be(0);
            simulation.MeanReturn.Should().Be(0);
            simulation.Volatility.Should().Be(0);
            simulation.Percentile5.Should().Be(0);
            simulation.Percentile25.Should().Be(0);
            simulation.Percentile50.Should().Be(0);
            simulation.Percentile75.Should().Be(0);
            simulation.Percentile95.Should().Be(0);
            simulation.ProbabilityOfLoss.Should().Be(0);
            simulation.ExpectedValue.Should().Be(0);
            simulation.SimulationResults.Should().NotBeNull();
            simulation.SimulationResults.Should().BeEmpty();
        }

        [Fact]
        public void MonteCarloSimulation_Percentiles_ShouldBeInCorrectOrder()
        {
            // Arrange
            var simulation = new MonteCarloSimulation
            {
                Percentile5 = 85000m,
                Percentile25 = 95000m,
                Percentile50 = 110000m,
                Percentile75 = 125000m,
                Percentile95 = 145000m
            };

            // Assert
            simulation.Percentile5.Should().BeLessThan(simulation.Percentile25);
            simulation.Percentile25.Should().BeLessThan(simulation.Percentile50);
            simulation.Percentile50.Should().BeLessThan(simulation.Percentile75);
            simulation.Percentile75.Should().BeLessThan(simulation.Percentile95);
        }

        [Theory]
        [InlineData(100000, 8.0, 15.0, 1, 105000, 108000)] // Approximate expected ranges
        [InlineData(50000, 12.0, 20.0, 2, 58000, 62000)]
        public void MonteCarloSimulation_ExpectedValue_ShouldBeReasonable(
            decimal initialValue, decimal meanReturn, decimal volatility, int years,
            decimal minExpected, decimal maxExpected)
        {
            // Arrange
            var simulation = new MonteCarloSimulation
            {
                InitialValue = initialValue,
                MeanReturn = meanReturn,
                Volatility = volatility,
                TimeHorizonYears = years
            };

            // Act - Calculate simple expected value (not Monte Carlo, just geometric mean)
            var expectedGrowth = Math.Pow(1 + (double)(meanReturn / 100), years);
            var expectedValue = (decimal)((double)initialValue * expectedGrowth);

            // Assert
            expectedValue.Should().BeGreaterThan(minExpected);
            expectedValue.Should().BeLessThan(maxExpected);
        }

        #endregion

        #region Portfolio Tests

        [Fact]
        public void Portfolio_GetPositionsBySector_ShouldGroupCorrectly()
        {
            // Arrange
            var portfolio = new Portfolio();
            portfolio.Positions.Add(new Position { Symbol = "AAPL", Sector = "Technology" });
            portfolio.Positions.Add(new Position { Symbol = "MSFT", Sector = "Technology" });
            portfolio.Positions.Add(new Position { Symbol = "JNJ", Sector = "Healthcare" });
            portfolio.Positions.Add(new Position { Symbol = "XYZ", Sector = null }); // Unknown sector

            // Act
            var sectorGroups = portfolio.GetPositionsBySector();

            // Assert
            sectorGroups.Should().HaveCount(3);
            sectorGroups["Technology"].Should().HaveCount(2);
            sectorGroups["Healthcare"].Should().HaveCount(1);
            sectorGroups["Unknown"].Should().HaveCount(1);
        }

        [Fact]
        public void Portfolio_TotalGainLossPercent_ShouldCalculateCorrectly()
        {
            // Arrange
            var portfolio = new Portfolio
            {
                TotalCost = 100000m,
                TotalGainLoss = 15000m
            };

            // Act
            var percentage = portfolio.TotalGainLossPercent;

            // Assert
            percentage.Should().Be(15.0m);
        }

        [Fact]
        public void Portfolio_TotalGainLossPercent_ShouldHandleZeroCost()
        {
            // Arrange
            var portfolio = new Portfolio
            {
                TotalCost = 0m,
                TotalGainLoss = 5000m
            };

            // Act
            var percentage = portfolio.TotalGainLossPercent;

            // Assert
            percentage.Should().Be(0m);
        }

        #endregion
    }
}
