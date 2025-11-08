using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using ApexV2.Dashboard.Calendar;

namespace ApexV2.Tests.Dashboard.Calendar
{
    public class CalendarAlertManagerTests
    {
        [Fact]
        public void CalendarAlertManager_Constructor_ShouldInitializeCorrectly()
        {
            // Arrange
            var mockRepository = new TestAlertRepository();
            var mockLogger = new MockLogger<CalendarAlertManager>();

            // Act
            using var alertManager = new CalendarAlertManager(mockRepository, mockLogger);

            // Assert
            alertManager.Should().NotBeNull();
        }

        [Fact]
        public async Task CreateAlertAsync_WithValidData_ShouldCreateAlert()
        {
            // Arrange
            var mockRepository = new TestAlertRepository();
            var mockLogger = new MockLogger<CalendarAlertManager>();
            using var alertManager = new CalendarAlertManager(mockRepository, mockLogger);

            var eventId = 123;
            var alertTime = DateTime.UtcNow.AddMinutes(30);
            var message = "AAPL earnings in 30 minutes";

            // Act
            var result = await alertManager.CreateAlertAsync(eventId, alertTime, message);

            // Assert
            result.Should().NotBeNull();
            result.EventId.Should().Be(eventId);
            result.AlertTime.Should().Be(alertTime);
            result.Message.Should().Be(message);
            result.IsEnabled.Should().BeTrue();
            result.HasBeenTriggered.Should().BeFalse();
            result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task GetActiveAlertsAsync_WithPendingAlerts_ShouldReturnOnlyActiveAlerts()
        {
            // Arrange
            var mockRepository = new TestAlertRepository();
            var mockLogger = new MockLogger<CalendarAlertManager>();
            using var alertManager = new CalendarAlertManager(mockRepository, mockLogger);

            var alerts = new List<EventAlert>
            {
                new EventAlert { Id = 1, EventId = 100, IsEnabled = true, HasBeenTriggered = false },
                new EventAlert { Id = 2, EventId = 101, IsEnabled = false, HasBeenTriggered = false },
                new EventAlert { Id = 3, EventId = 102, IsEnabled = true, HasBeenTriggered = true },
                new EventAlert { Id = 4, EventId = 103, IsEnabled = true, HasBeenTriggered = false }
            };

            mockRepository.SetPendingAlerts(alerts);

            // Act
            var result = await alertManager.GetActiveAlertsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(a => a.IsEnabled && !a.HasBeenTriggered);
            result.Should().Contain(a => a.Id == 1);
            result.Should().Contain(a => a.Id == 4);
        }

        [Fact]
        public async Task ProcessPendingAlertsAsync_WithTriggeredAlerts_ShouldTriggerEvents()
        {
            // Arrange
            var mockRepository = new TestAlertRepository();
            var mockLogger = new MockLogger<CalendarAlertManager>();
            using var alertManager = new CalendarAlertManager(mockRepository, mockLogger);

            var pastTime = DateTime.UtcNow.AddMinutes(-5);
            var futureTime = DateTime.UtcNow.AddMinutes(5);

            var alerts = new List<EventAlert>
            {
                new EventAlert { Id = 1, EventId = 100, AlertTime = pastTime, IsEnabled = true, HasBeenTriggered = false },
                new EventAlert { Id = 2, EventId = 101, AlertTime = futureTime, IsEnabled = true, HasBeenTriggered = false }
            };

            mockRepository.SetPendingAlerts(alerts);

            var triggeredAlerts = new List<EventAlert>();
            alertManager.AlertTriggered += (sender, alert) => triggeredAlerts.Add(alert);

            // Act
            await alertManager.ProcessPendingAlertsAsync();

            // Assert
            triggeredAlerts.Should().HaveCount(1);
            triggeredAlerts[0].Id.Should().Be(1);
            triggeredAlerts[0].HasBeenTriggered.Should().BeTrue();
            triggeredAlerts[0].TriggeredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task UpdateAlertAsync_WithValidAlert_ShouldUpdateAlert()
        {
            // Arrange
            var mockRepository = new TestAlertRepository();
            var mockLogger = new MockLogger<CalendarAlertManager>();
            using var alertManager = new CalendarAlertManager(mockRepository, mockLogger);

            var alert = new EventAlert
            {
                Id = 1,
                EventId = 100,
                AlertTime = DateTime.UtcNow.AddHours(1),
                Message = "Updated message",
                IsEnabled = false
            };

            // Act
            await alertManager.UpdateAlertAsync(alert);

            // Assert
            mockRepository.UpdatedAlerts.Should().HaveCount(1);
            mockRepository.UpdatedAlerts[0].Should().Be(alert);
        }

        [Fact]
        public async Task DeleteAlertAsync_WithValidId_ShouldDeleteAlert()
        {
            // Arrange
            var mockRepository = new TestAlertRepository();
            var mockLogger = new MockLogger<CalendarAlertManager>();
            using var alertManager = new CalendarAlertManager(mockRepository, mockLogger);

            var alertId = 123;

            // Act
            await alertManager.DeleteAlertAsync(alertId);

            // Assert
            mockRepository.DeletedAlertIds.Should().Contain(alertId);
        }

        [Fact]
        public async Task GetTriggeredAlertsAsync_WithTriggeredAlerts_ShouldReturnTriggeredAlerts()
        {
            // Arrange
            var mockRepository = new TestAlertRepository();
            var mockLogger = new MockLogger<CalendarAlertManager>();
            using var alertManager = new CalendarAlertManager(mockRepository, mockLogger);

            var now = DateTime.UtcNow;
            var alerts = new List<EventAlert>
            {
                new EventAlert { Id = 1, HasBeenTriggered = true, TriggeredAt = now.AddMinutes(-10) },
                new EventAlert { Id = 2, HasBeenTriggered = false },
                new EventAlert { Id = 3, HasBeenTriggered = true, TriggeredAt = now.AddMinutes(-5) }
            };

            mockRepository.SetPendingAlerts(alerts);

            // Act
            var result = await alertManager.GetTriggeredAlertsAsync();

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(a => a.HasBeenTriggered);
            result[0].Id.Should().Be(3); // Most recent first
            result[1].Id.Should().Be(1);
        }

        [Fact]
        public async Task GetTriggeredAlertsAsync_WithSinceFilter_ShouldReturnFilteredAlerts()
        {
            // Arrange
            var mockRepository = new TestAlertRepository();
            var mockLogger = new MockLogger<CalendarAlertManager>();
            using var alertManager = new CalendarAlertManager(mockRepository, mockLogger);

            var now = DateTime.UtcNow;
            var since = now.AddMinutes(-7);
            var alerts = new List<EventAlert>
            {
                new EventAlert { Id = 1, HasBeenTriggered = true, TriggeredAt = now.AddMinutes(-10) },
                new EventAlert { Id = 2, HasBeenTriggered = true, TriggeredAt = now.AddMinutes(-5) },
                new EventAlert { Id = 3, HasBeenTriggered = true, TriggeredAt = now.AddMinutes(-2) }
            };

            mockRepository.SetPendingAlerts(alerts);

            // Act
            var result = await alertManager.GetTriggeredAlertsAsync(since);

            // Assert
            result.Should().HaveCount(2);
            result.Should().OnlyContain(a => a.TriggeredAt >= since);
            result[0].Id.Should().Be(3);
            result[1].Id.Should().Be(2);
        }
    }

    // Test implementations for testing
    public class TestAlertRepository : IEconomicCalendarRepository
    {
        private List<EventAlert> _pendingAlerts = new();
        public List<EventAlert> UpdatedAlerts { get; } = new();
        public List<int> DeletedAlertIds { get; } = new();

        public void SetPendingAlerts(List<EventAlert> alerts) => _pendingAlerts = alerts;

        public Task<List<EventAlert>> GetPendingAlertsAsync() => Task.FromResult(_pendingAlerts);

        public Task<EventAlert> AddAlertAsync(EventAlert alert)
        {
            alert.Id = _pendingAlerts.Count + 1;
            _pendingAlerts.Add(alert);
            return Task.FromResult(alert);
        }

        public Task UpdateAlertAsync(EventAlert alert)
        {
            UpdatedAlerts.Add(alert);
            return Task.CompletedTask;
        }

        public Task DeleteAlertAsync(int alertId)
        {
            DeletedAlertIds.Add(alertId);
            return Task.CompletedTask;
        }

        // Other interface methods with basic implementations
        public Task<List<EconomicEvent>> GetEventsAsync(CalendarFilter filter) => Task.FromResult(new List<EconomicEvent>());
        public Task<EconomicEvent?> GetEventByIdAsync(int eventId) => Task.FromResult<EconomicEvent?>(null);
        public Task<EconomicEvent> AddEventAsync(EconomicEvent economicEvent) => Task.FromResult(economicEvent);
        public Task<EconomicEvent> UpdateEventAsync(EconomicEvent economicEvent) => Task.FromResult(economicEvent);
        public Task DeleteEventAsync(int eventId) => Task.CompletedTask;
        public Task<List<EarningsEvent>> GetEarningsEventsAsync(DateTime startDate, DateTime endDate, string? symbol = null) => Task.FromResult(new List<EarningsEvent>());
        public Task<List<EconomicIndicator>> GetEconomicIndicatorsAsync(DateTime startDate, DateTime endDate, string? country = null) => Task.FromResult(new List<EconomicIndicator>());
        public Task<List<EventNote>> GetEventNotesAsync(int eventId) => Task.FromResult(new List<EventNote>());
        public Task<EventNote> AddNoteAsync(EventNote note) => Task.FromResult(note);
        public Task UpdateNoteAsync(EventNote note) => Task.CompletedTask;
        public Task DeleteNoteAsync(int noteId) => Task.CompletedTask;
        public Task<MarketHours?> GetMarketHoursAsync(string market, DateTime date) => Task.FromResult<MarketHours?>(null);
        public Task<MarketHours> SaveMarketHoursAsync(MarketHours marketHours) => Task.FromResult(marketHours);
        public Task<EventImpactAnalysis?> GetEventImpactAsync(int eventId) => Task.FromResult<EventImpactAnalysis?>(null);
        public Task<EventImpactAnalysis> SaveEventImpactAsync(EventImpactAnalysis impact) => Task.FromResult(impact);
        public Task BulkInsertEventsAsync(List<EconomicEvent> events) => Task.CompletedTask;
        public Task<DateTime?> GetLastSyncTimeAsync() => Task.FromResult<DateTime?>(DateTime.UtcNow);
        public Task UpdateLastSyncTimeAsync(DateTime syncTime) => Task.CompletedTask;
    }

    public class MockLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
