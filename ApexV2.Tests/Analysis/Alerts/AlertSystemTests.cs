using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Analysis.Alerts;
using ApexV2.Charts.Export;
using Xunit;

namespace ApexV2.Tests.Analysis.Alerts
{
    public class AlertSystemTests : IDisposable
    {
        private TestLogger _logger = null!;
        private AlertService _alertService = null!;
        private AlertManager _alertManager = null!;

        public AlertSystemTests()
        {
            _logger = new TestLogger();
            _alertService = new AlertService(_logger);
            _alertManager = new AlertManager(_logger);
        }

        public void Dispose()
        {
            _alertService?.Dispose();
            _alertManager?.Dispose();
        }

        [Fact]
        public async Task PriceAlert_ShouldTrigger_WhenPriceAboveTarget()
        {
            // Arrange
            var alert = new PriceAlert
            {
                Symbol = "AAPL",
                Name = "AAPL above $150",
                TargetPrice = 150.00m,
                Condition = PriceAlertCondition.Above
            };

            await _alertService.AddAlertAsync(alert);

            var context = new AlertContext
            {
                Symbol = "AAPL",
                CurrentPrice = 155.00m
            };

            // Act
            var results = await _alertService.EvaluateAlertsAsync(context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].WasTriggered);
            Assert.Contains("above target", results[0].Message);
        }

        [Fact]
        public async Task PriceAlert_ShouldNotTrigger_WhenPriceBelowTarget()
        {
            // Arrange
            var alert = new PriceAlert
            {
                Symbol = "AAPL",
                Name = "AAPL above $150",
                TargetPrice = 150.00m,
                Condition = PriceAlertCondition.Above
            };

            await _alertService.AddAlertAsync(alert);

            var context = new AlertContext
            {
                Symbol = "AAPL",
                CurrentPrice = 145.00m
            };

            // Act
            var results = await _alertService.EvaluateAlertsAsync(context);

            // Assert
            Assert.Single(results);
            Assert.False(results[0].WasTriggered);
        }

        [Fact]
        public async Task PercentageChangeAlert_ShouldTrigger_WhenChangeExceedsThreshold()
        {
            // Arrange
            var alert = new PercentageChangeAlert
            {
                Symbol = "TSLA",
                Name = "TSLA 5% move",
                ChangePercent = 5.0m,
                Direction = ChangeDirection.Either
            };

            await _alertService.AddAlertAsync(alert);

            var context = new AlertContext
            {
                Symbol = "TSLA",
                CurrentPrice = 105.00m,
                ReferencePrice = 100.00m // 5% increase
            };

            // Act
            var results = await _alertService.EvaluateAlertsAsync(context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].WasTriggered);
        }

        [Fact]
        public async Task VolumeAlert_ShouldTrigger_WhenVolumeAboveTarget()
        {
            // Arrange
            var alert = new VolumeAlert
            {
                Symbol = "MSFT",
                Name = "MSFT high volume",
                TargetVolume = 1000000,
                Condition = VolumeCondition.Above
            };

            await _alertService.AddAlertAsync(alert);

            var context = new AlertContext
            {
                Symbol = "MSFT",
                CurrentVolume = 1500000
            };

            // Act
            var results = await _alertService.EvaluateAlertsAsync(context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].WasTriggered);
        }

        [Fact]
        public async Task IndicatorAlert_ShouldTrigger_WhenRSIAboveTarget()
        {
            // Arrange
            var alert = new IndicatorAlert
            {
                Symbol = "GOOGL",
                Name = "GOOGL RSI overbought",
                IndicatorName = "RSI",
                TargetValue = 70.0m,
                Condition = IndicatorCondition.Above
            };

            await _alertService.AddAlertAsync(alert);

            var context = new AlertContext
            {
                Symbol = "GOOGL",
                IndicatorValues = new Dictionary<string, decimal>
                {
                    { "RSI", 75.0m }
                }
            };

            // Act
            var results = await _alertService.EvaluateAlertsAsync(context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].WasTriggered);
            Assert.Contains("RSI", results[0].Message);
        }

        [Fact]
        public async Task IndicatorAlert_ShouldTrigger_WhenCrossingAbove()
        {
            // Arrange
            var alert = new IndicatorAlert
            {
                Symbol = "AMD",
                Name = "AMD RSI crosses above 70",
                IndicatorName = "RSI",
                TargetValue = 70.0m,
                Condition = IndicatorCondition.CrossesAbove
            };

            await _alertService.AddAlertAsync(alert);

            var context = new AlertContext
            {
                Symbol = "AMD",
                IndicatorValues = new Dictionary<string, decimal>
                {
                    { "RSI", 72.0m }
                },
                PreviousIndicatorValues = new Dictionary<string, decimal>
                {
                    { "RSI", 68.0m }
                }
            };

            // Act
            var results = await _alertService.EvaluateAlertsAsync(context);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].WasTriggered);
            Assert.Contains("crossed above", results[0].Message);
        }

        [Fact]
        public async Task AlertService_ShouldRemoveExpiredAlerts()
        {
            // Arrange - create alert with future expiry time first
            var futureExpiry = DateTime.Now.AddMinutes(1);
            var alert = new PriceAlert
            {
                Symbol = "NFLX",
                Name = "Soon to expire alert",
                TargetPrice = 100.00m,
                ExpiresAt = futureExpiry
            };

            var addSuccess = await _alertService.AddAlertAsync(alert);
            Assert.True(addSuccess, "Alert should be added successfully");

            // Verify alert was added
            var alertsBefore = await _alertService.GetAllAlertsAsync();
            Assert.Single(alertsBefore);

            // Now simulate time passing by manually setting expiry to past
            alert.ExpiresAt = DateTime.Now.AddMinutes(-1);

            // Act
            var removedCount = await _alertService.CleanupExpiredAlertsAsync();

            // Assert
            Assert.Equal(1, removedCount);
            
            var allAlerts = await _alertService.GetAllAlertsAsync();
            Assert.Empty(allAlerts);
        }

        [Fact]
        public async Task AlertService_ShouldValidateAlerts()
        {
            // Arrange
            var invalidAlert = new PriceAlert
            {
                Symbol = "", // Invalid: empty symbol
                Name = "", // Invalid: empty name
                TargetPrice = -10.00m // Invalid: negative price
            };

            // Act
            var success = await _alertService.AddAlertAsync(invalidAlert);

            // Assert
            Assert.False(success);
            
            var allAlerts = await _alertService.GetAllAlertsAsync();
            Assert.Empty(allAlerts);
        }

        [Fact]
        public async Task AlertService_ShouldGetStatistics()
        {
            // Arrange
            var alert1 = new PriceAlert { Symbol = "AAPL", Name = "Alert 1", TargetPrice = 150, Priority = AlertPriority.High };
            var alert2 = new VolumeAlert { Symbol = "TSLA", Name = "Alert 2", TargetVolume = 1000000, Priority = AlertPriority.Medium };
            var alert3 = new IndicatorAlert { Symbol = "MSFT", Name = "Alert 3", IndicatorName = "RSI", TargetValue = 70, Priority = AlertPriority.Low };

            await _alertService.AddAlertAsync(alert1);
            await _alertService.AddAlertAsync(alert2);
            await _alertService.AddAlertAsync(alert3);

            // Act
            var statistics = await _alertService.GetStatisticsAsync();

            // Assert
            Assert.Equal(3, statistics.TotalAlerts);
            Assert.Equal(3, statistics.ActiveAlerts);
            Assert.Equal(1, statistics.AlertsByType[AlertType.Price]);
            Assert.Equal(1, statistics.AlertsByType[AlertType.Volume]);
            Assert.Equal(1, statistics.AlertsByType[AlertType.Indicator]);
        }

        [Fact]
        public async Task AlertService_ShouldCreateQuickAlerts()
        {
            // Act
            var priceAlertSuccess = await _alertService.CreatePriceAlertAsync("AAPL", 150.00m, PriceAlertCondition.Above);
            var changeAlertSuccess = await _alertService.CreatePercentageChangeAlertAsync("TSLA", 5.0m, ChangeDirection.Up);
            var volumeAlertSuccess = await _alertService.CreateVolumeAlertAsync("MSFT", 1000000, VolumeCondition.Above);

            // Assert
            Assert.True(priceAlertSuccess);
            Assert.True(changeAlertSuccess);
            Assert.True(volumeAlertSuccess);

            var allAlerts = await _alertService.GetAllAlertsAsync();
            Assert.Equal(3, allAlerts.Count);
        }

        [Fact]
        public async Task AlertManager_ShouldProcessMarketDataUpdate()
        {
            // Arrange
            await _alertManager.AlertService.CreatePriceAlertAsync("AAPL", 150.00m, PriceAlertCondition.Above);

            bool alertTriggered = false;
            _alertManager.AlertService.AlertTriggered += (sender, args) => alertTriggered = true;

            // Act
            await _alertManager.ProcessMarketDataUpdateAsync("AAPL", 155.00m, 1000000);

            // Assert
            Assert.True(alertTriggered);
        }

        [Fact]
        public async Task AlertManager_ShouldCreateSymbolAlertSetup()
        {
            // Arrange
            var options = new AlertSetupOptions
            {
                EnablePriceAlerts = true,
                CurrentPrice = 100.00m,
                SupportLevel = 95.00m,
                ResistanceLevel = 105.00m,
                PercentageThresholds = new List<decimal> { 5.0m, 10.0m },
                EnableVolumeAlerts = true,
                AverageVolume = 1000000
            };

            // Act
            var result = await _alertManager.CreateSymbolAlertSetupAsync("AAPL", options);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.CreatedAlerts > 0);
            
            var allAlerts = await _alertManager.AlertService.GetAllAlertsAsync();
            Assert.True(allAlerts.Count >= 4); // Support, resistance, 2 percentage, volume
        }

        [Fact]
        public async Task AlertManager_ShouldGetDashboardSummary()
        {
            // Arrange
            await _alertManager.AlertService.CreatePriceAlertAsync("AAPL", 150.00m, PriceAlertCondition.Above);
            await _alertManager.AlertService.CreateVolumeAlertAsync("TSLA", 1000000, VolumeCondition.Above);

            // Act
            var summary = await _alertManager.GetDashboardSummaryAsync();

            // Assert
            Assert.Equal(2, summary.TotalAlerts);
            Assert.Equal(2, summary.ActiveAlerts);
            Assert.True(summary.AlertsByType.Count > 0);
        }

        [Fact]
        public void InAppNotificationProvider_ShouldStoreNotifications()
        {
            // Arrange
            var provider = new InAppNotificationProvider(_logger);
            var alert = new PriceAlert { Symbol = "AAPL", Name = "Test Alert", TargetPrice = 150 };
            var context = new AlertContext { Symbol = "AAPL", CurrentPrice = 155 };

            // Act
            provider.SendNotificationAsync(alert, "Test message", context).Wait();

            // Assert
            var notifications = provider.GetNotifications();
            Assert.Single(notifications);
            Assert.Equal("Test message", notifications[0].Message);
            Assert.False(notifications[0].IsRead);
        }

        [Fact]
        public void InAppNotificationProvider_ShouldMarkAsRead()
        {
            // Arrange
            var provider = new InAppNotificationProvider(_logger);
            var alert = new PriceAlert { Symbol = "AAPL", Name = "Test Alert", TargetPrice = 150 };
            var context = new AlertContext { Symbol = "AAPL", CurrentPrice = 155 };

            provider.SendNotificationAsync(alert, "Test message", context).Wait();
            var notification = provider.GetNotifications().First();

            // Act
            provider.MarkAsRead(notification.Id);

            // Assert
            var unreadNotifications = provider.GetUnreadNotifications();
            Assert.Empty(unreadNotifications);
        }

        [Fact]
        public void TimeBasedAlert_ShouldValidateCorrectly()
        {
            // Arrange
            var alert = new TimeBasedAlert
            {
                Symbol = "AAPL",
                Name = "Market open alert",
                TimeAlertType = TimeAlertType.MarketOpen,
                ActiveDays = new List<DayOfWeek> { DayOfWeek.Monday, DayOfWeek.Tuesday }
            };

            // Act
            var errors = alert.Validate();

            // Assert
            Assert.Empty(errors);
        }
    }

    /// <summary>
    /// Test logger implementation for unit tests
    /// </summary>
    public class TestLogger : IChartLogger
    {
        public List<string> LogEntries { get; } = new();

        public void Trace(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"TRACE: {msg}");
        public void Debug(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"DEBUG: {msg}");
        public void Info(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"INFO: {msg}");
        public void Warn(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"WARN: {msg}");
        public void Error(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => LogEntries.Add($"ERROR: {msg}");
        public void Critical(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => LogEntries.Add($"CRITICAL: {msg}");
    }
}
