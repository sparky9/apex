using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using ApexV2.Dashboard.Calendar;

namespace ApexV2.Tests.Dashboard.Calendar
{
    public class CalendarModelsTests
    {
        [Fact]
        public void EconomicEvent_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var economicEvent = new EconomicEvent();

            // Assert
            economicEvent.Id.Should().Be(0);
            economicEvent.Title.Should().Be(string.Empty);
            economicEvent.Description.Should().Be(string.Empty);
            economicEvent.Type.Should().Be(EventType.Earnings);
            economicEvent.Importance.Should().Be(EventImportance.Low);
            economicEvent.Status.Should().Be(EventStatus.Scheduled);
            economicEvent.Alerts.Should().NotBeNull().And.BeEmpty();
            economicEvent.Notes.Should().NotBeNull().And.BeEmpty();
            economicEvent.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
            economicEvent.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void EconomicEvent_WithValidData_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var eventDate = DateTime.Today.AddDays(1);
            var title = "AAPL Earnings Report";
            var description = "Apple quarterly earnings announcement";
            var symbol = "AAPL";
            var company = "Apple Inc.";

            // Act
            var economicEvent = new EconomicEvent
            {
                Title = title,
                Description = description,
                EventDate = eventDate,
                Type = EventType.Earnings,
                Importance = EventImportance.High,
                Status = EventStatus.Scheduled,
                Symbol = symbol,
                CompanyName = company,
                Country = "United States",
                Currency = "USD"
            };

            // Assert
            economicEvent.Title.Should().Be(title);
            economicEvent.Description.Should().Be(description);
            economicEvent.EventDate.Should().Be(eventDate);
            economicEvent.Type.Should().Be(EventType.Earnings);
            economicEvent.Importance.Should().Be(EventImportance.High);
            economicEvent.Status.Should().Be(EventStatus.Scheduled);
            economicEvent.Symbol.Should().Be(symbol);
            economicEvent.CompanyName.Should().Be(company);
            economicEvent.Country.Should().Be("United States");
            economicEvent.Currency.Should().Be("USD");
        }

        [Fact]
        public void EarningsEvent_InheritsFromEconomicEvent_ShouldHaveEarningsSpecificProperties()
        {
            // Arrange & Act
            var earningsEvent = new EarningsEvent
            {
                Title = "MSFT Earnings",
                EventDate = DateTime.Today.AddDays(2),
                EPSActual = 2.50m,
                EPSEstimate = 2.45m,
                EPSPrevious = 2.30m,
                RevenueActual = 50000000000m,
                RevenueEstimate = 49500000000m,
                RevenuePrevious = 48000000000m,
                Quarter = "Q3",
                FiscalYear = 2024,
                ConferenceCallTime = DateTime.Today.AddDays(2).AddHours(17),
                ConferenceCallNumber = "1-800-555-0123",
                WebcastUrl = "https://investor.microsoft.com/webcast"
            };

            // Assert
            earningsEvent.Title.Should().Be("MSFT Earnings");
            earningsEvent.Type.Should().Be(EventType.Earnings); // Default from base class
            earningsEvent.EPSActual.Should().Be(2.50m);
            earningsEvent.EPSEstimate.Should().Be(2.45m);
            earningsEvent.EPSPrevious.Should().Be(2.30m);
            earningsEvent.RevenueActual.Should().Be(50000000000m);
            earningsEvent.RevenueEstimate.Should().Be(49500000000m);
            earningsEvent.RevenuePrevious.Should().Be(48000000000m);
            earningsEvent.Quarter.Should().Be("Q3");
            earningsEvent.FiscalYear.Should().Be(2024);
            earningsEvent.ConferenceCallTime.Should().Be(DateTime.Today.AddDays(2).AddHours(17));
            earningsEvent.ConferenceCallNumber.Should().Be("1-800-555-0123");
            earningsEvent.WebcastUrl.Should().Be("https://investor.microsoft.com/webcast");
        }

        [Fact]
        public void EconomicIndicator_InheritsFromEconomicEvent_ShouldHaveIndicatorSpecificProperties()
        {
            // Arrange & Act
            var indicator = new EconomicIndicator
            {
                Title = "GDP Report",
                EventDate = DateTime.Today.AddDays(5),
                IndicatorName = "Gross Domestic Product",
                ReportingAgency = "Bureau of Economic Analysis",
                Frequency = "Quarterly",
                Impact = 8.5m,
                MarketSector = "Economy",
                ActualValue = 2.1m,
                ForecastValue = 2.0m,
                PreviousValue = 1.9m,
                Unit = "% QoQ"
            };

            // Assert
            indicator.Title.Should().Be("GDP Report");
            indicator.Type.Should().Be(EventType.Earnings); // Default from base class
            indicator.IndicatorName.Should().Be("Gross Domestic Product");
            indicator.ReportingAgency.Should().Be("Bureau of Economic Analysis");
            indicator.Frequency.Should().Be("Quarterly");
            indicator.Impact.Should().Be(8.5m);
            indicator.MarketSector.Should().Be("Economy");
            indicator.ActualValue.Should().Be(2.1m);
            indicator.ForecastValue.Should().Be(2.0m);
            indicator.PreviousValue.Should().Be(1.9m);
            indicator.Unit.Should().Be("% QoQ");
        }

        [Fact]
        public void EventAlert_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var alert = new EventAlert();

            // Assert
            alert.Id.Should().Be(0);
            alert.EventId.Should().Be(0);
            alert.IsEnabled.Should().BeTrue();
            alert.HasBeenTriggered.Should().BeFalse();
            alert.TriggeredAt.Should().BeNull();
            alert.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void EventAlert_WithValidData_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var eventId = 123;
            var alertTime = DateTime.Today.AddHours(9);
            var message = "AAPL earnings in 1 hour";

            // Act
            var alert = new EventAlert
            {
                EventId = eventId,
                AlertTime = alertTime,
                Message = message,
                IsEnabled = true
            };

            // Assert
            alert.EventId.Should().Be(eventId);
            alert.AlertTime.Should().Be(alertTime);
            alert.Message.Should().Be(message);
            alert.IsEnabled.Should().BeTrue();
            alert.HasBeenTriggered.Should().BeFalse();
        }

        [Fact]
        public void CalendarFilter_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var filter = new CalendarFilter();

            // Assert
            filter.StartDate.Should().BeNull();
            filter.EndDate.Should().BeNull();
            filter.EventTypes.Should().NotBeNull().And.BeEmpty();
            filter.ImportanceLevels.Should().NotBeNull().And.BeEmpty();
            filter.Countries.Should().NotBeNull().And.BeEmpty();
            filter.Symbols.Should().NotBeNull().And.BeEmpty();
            filter.ShowOnlyWatchlistSymbols.Should().BeFalse();
            filter.SearchTerm.Should().BeNull();
        }

        [Fact]
        public void CalendarFilter_WithFilterCriteria_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var startDate = DateTime.Today;
            var endDate = DateTime.Today.AddDays(7);
            var eventTypes = new List<EventType> { EventType.Earnings, EventType.EconomicIndicator };
            var importanceLevels = new List<EventImportance> { EventImportance.High };
            var countries = new List<string> { "United States", "Canada" };
            var symbols = new List<string> { "AAPL", "MSFT", "GOOGL" };
            var searchTerm = "earnings";

            // Act
            var filter = new CalendarFilter
            {
                StartDate = startDate,
                EndDate = endDate,
                EventTypes = eventTypes,
                ImportanceLevels = importanceLevels,
                Countries = countries,
                Symbols = symbols,
                ShowOnlyWatchlistSymbols = true,
                SearchTerm = searchTerm
            };

            // Assert
            filter.StartDate.Should().Be(startDate);
            filter.EndDate.Should().Be(endDate);
            filter.EventTypes.Should().BeEquivalentTo(eventTypes);
            filter.ImportanceLevels.Should().BeEquivalentTo(importanceLevels);
            filter.Countries.Should().BeEquivalentTo(countries);
            filter.Symbols.Should().BeEquivalentTo(symbols);
            filter.ShowOnlyWatchlistSymbols.Should().BeTrue();
            filter.SearchTerm.Should().Be(searchTerm);
        }

        [Fact]
        public void CalendarView_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var calendarView = new CalendarView();

            // Assert
            calendarView.Date.Should().Be(default(DateTime));
            calendarView.Events.Should().NotBeNull().And.BeEmpty();
            calendarView.TotalEvents.Should().Be(0);
            calendarView.HighImportanceCount.Should().Be(0);
            calendarView.EarningsCount.Should().Be(0);
            calendarView.EconomicIndicatorCount.Should().Be(0);
        }

        [Fact]
        public void CalendarView_WithEvents_ShouldCalculateCountsCorrectly()
        {
            // Arrange
            var date = DateTime.Today;
            var events = new List<EconomicEvent>
            {
                new EconomicEvent { Type = EventType.Earnings, Importance = EventImportance.High },
                new EconomicEvent { Type = EventType.Earnings, Importance = EventImportance.Medium },
                new EconomicEvent { Type = EventType.EconomicIndicator, Importance = EventImportance.High },
                new EconomicEvent { Type = EventType.Dividend, Importance = EventImportance.Low },
                new EconomicEvent { Type = EventType.EconomicIndicator, Importance = EventImportance.High }
            };

            // Act
            var calendarView = new CalendarView
            {
                Date = date,
                Events = events,
                TotalEvents = events.Count,
                HighImportanceCount = events.Count(e => e.Importance == EventImportance.High),
                EarningsCount = events.Count(e => e.Type == EventType.Earnings),
                EconomicIndicatorCount = events.Count(e => e.Type == EventType.EconomicIndicator)
            };

            // Assert
            calendarView.Date.Should().Be(date);
            calendarView.Events.Should().HaveCount(5);
            calendarView.TotalEvents.Should().Be(5);
            calendarView.HighImportanceCount.Should().Be(3);
            calendarView.EarningsCount.Should().Be(2);
            calendarView.EconomicIndicatorCount.Should().Be(2);
        }

        [Fact]
        public void MarketHours_DefaultValues_ShouldBeSetCorrectly()
        {
            // Arrange & Act
            var marketHours = new MarketHours();

            // Assert
            marketHours.Id.Should().Be(0);
            marketHours.Market.Should().Be(string.Empty);
            marketHours.Timezone.Should().Be(string.Empty);
            marketHours.IsOpen.Should().BeFalse();
            marketHours.IsHoliday.Should().BeFalse();
            marketHours.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public void MarketHours_WithNYSEHours_ShouldSetPropertiesCorrectly()
        {
            // Arrange
            var market = "NYSE";
            var timezone = "EST";
            var preMarketOpen = new TimeSpan(4, 0, 0);
            var marketOpen = new TimeSpan(9, 30, 0);
            var marketClose = new TimeSpan(16, 0, 0);
            var postMarketClose = new TimeSpan(20, 0, 0);
            var date = DateTime.Today;

            // Act
            var marketHours = new MarketHours
            {
                Market = market,
                Timezone = timezone,
                PreMarketOpen = preMarketOpen,
                MarketOpen = marketOpen,
                MarketClose = marketClose,
                PostMarketClose = postMarketClose,
                IsOpen = true,
                IsHoliday = false,
                Date = date
            };

            // Assert
            marketHours.Market.Should().Be(market);
            marketHours.Timezone.Should().Be(timezone);
            marketHours.PreMarketOpen.Should().Be(preMarketOpen);
            marketHours.MarketOpen.Should().Be(marketOpen);
            marketHours.MarketClose.Should().Be(marketClose);
            marketHours.PostMarketClose.Should().Be(postMarketClose);
            marketHours.IsOpen.Should().BeTrue();
            marketHours.IsHoliday.Should().BeFalse();
            marketHours.Date.Should().Be(date);
        }

        [Theory]
        [InlineData(EventType.Earnings)]
        [InlineData(EventType.Dividend)]
        [InlineData(EventType.Split)]
        [InlineData(EventType.EconomicIndicator)]
        [InlineData(EventType.CentralBankMeeting)]
        [InlineData(EventType.IPO)]
        [InlineData(EventType.ConferenceCall)]
        [InlineData(EventType.ProductLaunch)]
        [InlineData(EventType.Acquisition)]
        [InlineData(EventType.Other)]
        public void EventType_AllEnumValues_ShouldBeValid(EventType eventType)
        {
            // Arrange & Act
            var economicEvent = new EconomicEvent { Type = eventType };

            // Assert
            economicEvent.Type.Should().Be(eventType);
            Enum.IsDefined(typeof(EventType), eventType).Should().BeTrue();
        }

        [Theory]
        [InlineData(EventImportance.Low, 1)]
        [InlineData(EventImportance.Medium, 2)]
        [InlineData(EventImportance.High, 3)]
        public void EventImportance_EnumValues_ShouldHaveCorrectIntegerValues(EventImportance importance, int expectedValue)
        {
            // Arrange & Act
            var intValue = (int)importance;

            // Assert
            intValue.Should().Be(expectedValue);
        }

        [Theory]
        [InlineData(EventStatus.Scheduled)]
        [InlineData(EventStatus.InProgress)]
        [InlineData(EventStatus.Completed)]
        [InlineData(EventStatus.Cancelled)]
        [InlineData(EventStatus.Delayed)]
        public void EventStatus_AllEnumValues_ShouldBeValid(EventStatus status)
        {
            // Arrange & Act
            var economicEvent = new EconomicEvent { Status = status };

            // Assert
            economicEvent.Status.Should().Be(status);
            Enum.IsDefined(typeof(EventStatus), status).Should().BeTrue();
        }
    }
}
