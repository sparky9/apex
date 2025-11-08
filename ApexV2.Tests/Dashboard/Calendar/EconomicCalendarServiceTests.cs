using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using ApexV2.Dashboard.Calendar;

namespace ApexV2.Tests.Dashboard.Calendar
{
    public class EconomicCalendarServiceTests
    {
        [Fact]
        public void EconomicCalendarService_Constructor_ShouldInitializeCorrectly()
        {
            // Arrange
            var mockRepository = new TestEconomicCalendarRepository();
            var mockProvider = new TestCalendarDataProvider();
            var mockLogger = new MockLogger<EconomicCalendarService>();

            // Act
            var service = new EconomicCalendarService(mockRepository, mockProvider, mockLogger);

            // Assert
            service.Should().NotBeNull();
        }

        [Fact]
        public async Task GetEventsAsync_WithValidFilter_ShouldReturnFilteredEvents()
        {
            // Arrange
            var mockRepository = new TestEconomicCalendarRepository();
            var mockProvider = new TestCalendarDataProvider();
            var mockLogger = new MockLogger<EconomicCalendarService>();
            var service = new EconomicCalendarService(mockRepository, mockProvider, mockLogger);

            var filter = new CalendarFilter
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7),
                Country = "US",
                Impact = ImportanceLevel.High
            };

            var expectedEvents = new List<EconomicEvent>
            {
                new EconomicEvent 
                { 
                    Id = 1, 
                    EventDate = DateTime.Today.AddDays(1), 
                    Country = "US", 
                    Impact = ImportanceLevel.High,
                    Title = "Fed Interest Rate Decision",
                    Description = "Federal Reserve interest rate announcement"
                }
            };

            mockRepository.SetEvents(expectedEvents);

            // Act
            var result = await service.GetEventsAsync(filter);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Country.Should().Be("US");
            result[0].Impact.Should().Be(ImportanceLevel.High);
        }

        [Fact]
        public async Task GetEarningsEventsAsync_WithDateRange_ShouldReturnEarningsEvents()
        {
            // Arrange
            var mockRepository = new TestEconomicCalendarRepository();
            var mockProvider = new TestCalendarDataProvider();
            var mockLogger = new MockLogger<EconomicCalendarService>();
            var service = new EconomicCalendarService(mockRepository, mockProvider, mockLogger);

            var startDate = DateTime.Today;
            var endDate = DateTime.Today.AddDays(7);
            var symbol = "AAPL";

            var expectedEarnings = new List<EarningsEvent>
            {
                new EarningsEvent
                {
                    Id = 1,
                    Symbol = "AAPL",
                    CompanyName = "Apple Inc.",
                    ReportDate = DateTime.Today.AddDays(2),
                    EpsEstimate = 1.25m,
                    EpsActual = null
                }
            };

            mockRepository.SetEarningsEvents(expectedEarnings);

            // Act
            var result = await service.GetEarningsEventsAsync(startDate, endDate, symbol);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(1);
            result[0].Symbol.Should().Be("AAPL");
            result[0].CompanyName.Should().Be("Apple Inc.");
        }

        [Fact]
        public async Task SyncFromProviderAsync_WithValidProvider_ShouldSyncEvents()
        {
            // Arrange
            var mockRepository = new TestEconomicCalendarRepository();
            var mockProvider = new TestCalendarDataProvider();
            var mockLogger = new MockLogger<EconomicCalendarService>();
            var service = new EconomicCalendarService(mockRepository, mockProvider, mockLogger);

            var startDate = DateTime.Today;
            var endDate = DateTime.Today.AddDays(7);

            var providerEvents = new List<EconomicEvent>
            {
                new EconomicEvent 
                { 
                    Id = 1, 
                    EventDate = DateTime.Today.AddDays(1), 
                    Title = "GDP Release",
                    Country = "US",
                    Impact = ImportanceLevel.High
                }
            };

            mockProvider.SetEvents(providerEvents);

            // Act
            var result = await service.SyncFromProviderAsync(startDate, endDate);

            // Assert
            result.Should().NotBeNull();
            result.EventsAdded.Should().Be(1);
            result.EventsUpdated.Should().Be(0);
            result.SyncTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            mockRepository.BulkInsertedEvents.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetCalendarSummaryAsync_WithValidData_ShouldReturnSummary()
        {
            // Arrange
            var mockRepository = new TestEconomicCalendarRepository();
            var mockProvider = new TestCalendarDataProvider();
            var mockLogger = new MockLogger<EconomicCalendarService>();
            var service = new EconomicCalendarService(mockRepository, mockProvider, mockLogger);

            var filter = new CalendarFilter
            {
                StartDate = DateTime.Today,
                EndDate = DateTime.Today.AddDays(7)
            };

            var events = new List<EconomicEvent>
            {
                new EconomicEvent { Impact = ImportanceLevel.High, EventDate = DateTime.Today.AddDays(1) },
                new EconomicEvent { Impact = ImportanceLevel.Medium, EventDate = DateTime.Today.AddDays(2) },
                new EconomicEvent { Impact = ImportanceLevel.Low, EventDate = DateTime.Today.AddDays(3) }
            };

            var earnings = new List<EarningsEvent>
            {
                new EarningsEvent { ReportDate = DateTime.Today.AddDays(1) },
                new EarningsEvent { ReportDate = DateTime.Today.AddDays(2) }
            };

            mockRepository.SetEvents(events);
            mockRepository.SetEarningsEvents(earnings);

            // Act
            var result = await service.GetCalendarSummaryAsync(filter);

            // Assert
            result.Should().NotBeNull();
            result.TotalEvents.Should().Be(3);
            result.HighImpactEvents.Should().Be(1);
            result.MediumImpactEvents.Should().Be(1);
            result.LowImpactEvents.Should().Be(1);
            result.TotalEarnings.Should().Be(2);
            result.NextHighImpactEvent.Should().NotBeNull();
        }

        [Fact]
        public async Task AddEventNoteAsync_WithValidNote_ShouldAddNote()
        {
            // Arrange
            var mockRepository = new TestEconomicCalendarRepository();
            var mockProvider = new TestCalendarDataProvider();
            var mockLogger = new MockLogger<EconomicCalendarService>();
            var service = new EconomicCalendarService(mockRepository, mockProvider, mockLogger);

            var eventId = 123;
            var noteText = "This event might impact tech stocks";

            // Act
            var result = await service.AddEventNoteAsync(eventId, noteText);

            // Assert
            result.Should().NotBeNull();
            result.EventId.Should().Be(eventId);
            result.NoteText.Should().Be(noteText);
            result.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            mockRepository.AddedNotes.Should().HaveCount(1);
        }

        [Fact]
        public async Task GetLastSyncTimeAsync_ShouldReturnLastSyncTime()
        {
            // Arrange
            var mockRepository = new TestEconomicCalendarRepository();
            var mockProvider = new TestCalendarDataProvider();
            var mockLogger = new MockLogger<EconomicCalendarService>();
            var service = new EconomicCalendarService(mockRepository, mockProvider, mockLogger);

            var expectedSyncTime = DateTime.UtcNow.AddHours(-1);
            mockRepository.SetLastSyncTime(expectedSyncTime);

            // Act
            var result = await service.GetLastSyncTimeAsync();

            // Assert
            result.Should().Be(expectedSyncTime);
        }
    }

    // Test implementations
    public class TestCalendarDataProvider : IEconomicCalendarProvider
    {
        private List<EconomicEvent> _events = new();
        private List<EarningsEvent> _earnings = new();

        public void SetEvents(List<EconomicEvent> events) => _events = events;
        public void SetEarnings(List<EarningsEvent> earnings) => _earnings = earnings;

        public Task<List<EconomicEvent>> GetEventsAsync(DateTime startDate, DateTime endDate)
        {
            return Task.FromResult(_events);
        }

        public Task<List<EarningsEvent>> GetEarningsAsync(DateTime startDate, DateTime endDate, string? symbol = null)
        {
            return Task.FromResult(_earnings);
        }

        public Task<List<EconomicIndicator>> GetEconomicIndicatorsAsync(DateTime startDate, DateTime endDate, string? country = null)
        {
            return Task.FromResult(new List<EconomicIndicator>());
        }

        public Task<MarketHours> GetMarketHoursAsync(string market, DateTime date)
        {
            return Task.FromResult(new MarketHours { Market = market, Date = date, IsOpen = true });
        }

        public Task<List<EconomicEvent>> SearchEventsAsync(string searchTerm, DateTime? startDate = null, DateTime? endDate = null)
        {
            return Task.FromResult(_events);
        }

        public bool IsConfigured => true;
        public string ProviderName => "TestProvider";
    }

    public class TestEconomicCalendarRepository : IEconomicCalendarRepository
    {
        private List<EconomicEvent> _events = new();
        private List<EarningsEvent> _earnings = new();
        private List<EventAlert> _pendingAlerts = new();
        private MarketHours? _marketHours;
        private DateTime? _lastSyncTime;
        public List<EventNote> AddedNotes { get; } = new();
        public List<EconomicEvent> BulkInsertedEvents { get; } = new();
        public List<EventAlert> UpdatedAlerts { get; } = new();
        public List<int> DeletedAlertIds { get; } = new();

        public void SetEvents(List<EconomicEvent> events) => _events = events;
        public void SetEarningsEvents(List<EarningsEvent> earnings) => _earnings = earnings;
        public void SetPendingAlerts(List<EventAlert> alerts) => _pendingAlerts = alerts;
        public void SetMarketHours(MarketHours hours) => _marketHours = hours;
        public void SetLastSyncTime(DateTime syncTime) => _lastSyncTime = syncTime;

        public Task<List<EconomicEvent>> GetEventsAsync(CalendarFilter filter) => Task.FromResult(_events);
        public Task<EconomicEvent?> GetEventByIdAsync(int eventId) => Task.FromResult(_events.FirstOrDefault(e => e.Id == eventId));
        public Task<EconomicEvent> AddEventAsync(EconomicEvent economicEvent) => Task.FromResult(economicEvent);
        public Task<EconomicEvent> UpdateEventAsync(EconomicEvent economicEvent) => Task.FromResult(economicEvent);
        public Task DeleteEventAsync(int eventId) => Task.CompletedTask;
        
        public Task<List<EarningsEvent>> GetEarningsEventsAsync(DateTime startDate, DateTime endDate, string? symbol = null) => Task.FromResult(_earnings);
        public Task<List<EconomicIndicator>> GetEconomicIndicatorsAsync(DateTime startDate, DateTime endDate, string? country = null) => Task.FromResult(new List<EconomicIndicator>());
        
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

        public Task<List<EventNote>> GetEventNotesAsync(int eventId) => Task.FromResult(new List<EventNote>());
        public Task<EventNote> AddNoteAsync(EventNote note)
        {
            note.Id = AddedNotes.Count + 1;
            AddedNotes.Add(note);
            return Task.FromResult(note);
        }
        public Task UpdateNoteAsync(EventNote note) => Task.CompletedTask;
        public Task DeleteNoteAsync(int noteId) => Task.CompletedTask;
        
        public Task<MarketHours?> GetMarketHoursAsync(string market, DateTime date) => Task.FromResult(_marketHours);
        public Task<MarketHours> SaveMarketHoursAsync(MarketHours marketHours) => Task.FromResult(marketHours);
        
        public Task<EventImpactAnalysis?> GetEventImpactAsync(int eventId) => Task.FromResult<EventImpactAnalysis?>(null);
        public Task<EventImpactAnalysis> SaveEventImpactAsync(EventImpactAnalysis impact) => Task.FromResult(impact);
        
        public Task BulkInsertEventsAsync(List<EconomicEvent> events)
        {
            BulkInsertedEvents.AddRange(events);
            return Task.CompletedTask;
        }
        
        public Task<DateTime?> GetLastSyncTimeAsync() => Task.FromResult(_lastSyncTime);
        public Task UpdateLastSyncTimeAsync(DateTime syncTime) => Task.CompletedTask;
    }
}
