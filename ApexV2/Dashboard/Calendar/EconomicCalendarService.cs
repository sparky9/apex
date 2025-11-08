using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApexV2.Dashboard.Calendar
{
    public class EconomicCalendarService : IEconomicCalendarService
    {
        private readonly IEconomicCalendarRepository _repository;
        private readonly List<IEconomicCalendarProvider> _providers;
        private readonly ICalendarDataSyncService _syncService;
        private readonly ILogger<EconomicCalendarService> _logger;

        public EconomicCalendarService(
            IEconomicCalendarRepository repository,
            IEnumerable<IEconomicCalendarProvider> providers,
            ICalendarDataSyncService syncService,
            ILogger<EconomicCalendarService> logger)
        {
            _repository = repository;
            _providers = providers.ToList();
            _syncService = syncService;
            _logger = logger;
        }

        public async Task<List<EconomicEvent>> GetEventsAsync(CalendarFilter filter)
        {
            try
            {
                _logger.LogInformation("Getting economic events with filter");
                
                // Check if data is stale and sync if needed
                if (await _syncService.IsDataStaleAsync())
                {
                    _logger.LogInformation("Data is stale, syncing...");
                    await _syncService.SyncAllDataAsync();
                }

                var events = await _repository.GetEventsAsync(filter);
                
                _logger.LogInformation($"Retrieved {events.Count} events");
                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting economic events");
                throw;
            }
        }

        public async Task<CalendarView> GetCalendarViewAsync(DateTime date, CalendarFilter? filter = null)
        {
            try
            {
                _logger.LogInformation($"Getting calendar view for {date:yyyy-MM-dd}");

                filter ??= new CalendarFilter();
                filter.StartDate = date.Date;
                filter.EndDate = date.Date.AddDays(1).AddTicks(-1);

                var events = await GetEventsAsync(filter);

                var calendarView = new CalendarView
                {
                    Date = date,
                    Events = events,
                    TotalEvents = events.Count,
                    HighImportanceCount = events.Count(e => e.Importance == EventImportance.High),
                    EarningsCount = events.Count(e => e.Type == EventType.Earnings),
                    EconomicIndicatorCount = events.Count(e => e.Type == EventType.EconomicIndicator)
                };

                return calendarView;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting calendar view for {date:yyyy-MM-dd}");
                throw;
            }
        }

        public async Task<List<CalendarSummary>> GetCalendarSummaryAsync(DateTime startDate, DateTime endDate)
        {
            try
            {
                _logger.LogInformation($"Getting calendar summary from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");

                var summaries = new List<CalendarSummary>();
                var currentDate = startDate.Date;

                while (currentDate <= endDate.Date)
                {
                    var filter = new CalendarFilter
                    {
                        StartDate = currentDate,
                        EndDate = currentDate.AddDays(1).AddTicks(-1)
                    };

                    var events = await _repository.GetEventsAsync(filter);

                    var summary = new CalendarSummary
                    {
                        Date = currentDate,
                        TotalEvents = events.Count,
                        HighImportanceEvents = events.Count(e => e.Importance == EventImportance.High),
                        EarningsEvents = events.Count(e => e.Type == EventType.Earnings),
                        TopSymbols = events.Where(e => !string.IsNullOrEmpty(e.Symbol))
                            .GroupBy(e => e.Symbol!)
                            .OrderByDescending(g => g.Count())
                            .Take(5)
                            .Select(g => g.Key)
                            .ToList(),
                        HasWatchlistEvents = events.Any(e => !string.IsNullOrEmpty(e.Symbol))
                    };

                    summaries.Add(summary);
                    currentDate = currentDate.AddDays(1);
                }

                return summaries;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting calendar summary");
                throw;
            }
        }

        public async Task<EconomicEvent?> GetEventByIdAsync(int eventId)
        {
            try
            {
                return await _repository.GetEventByIdAsync(eventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting event by ID: {eventId}");
                throw;
            }
        }

        public async Task<EconomicEvent> CreateEventAsync(EconomicEvent economicEvent)
        {
            try
            {
                _logger.LogInformation($"Creating economic event: {economicEvent.Title}");
                
                economicEvent.CreatedAt = DateTime.UtcNow;
                economicEvent.UpdatedAt = DateTime.UtcNow;
                
                return await _repository.AddEventAsync(economicEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating economic event: {economicEvent.Title}");
                throw;
            }
        }

        public async Task<EconomicEvent> UpdateEventAsync(EconomicEvent economicEvent)
        {
            try
            {
                _logger.LogInformation($"Updating economic event: {economicEvent.Id}");
                
                economicEvent.UpdatedAt = DateTime.UtcNow;
                
                return await _repository.UpdateEventAsync(economicEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating economic event: {economicEvent.Id}");
                throw;
            }
        }

        public async Task DeleteEventAsync(int eventId)
        {
            try
            {
                _logger.LogInformation($"Deleting economic event: {eventId}");
                await _repository.DeleteEventAsync(eventId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting economic event: {eventId}");
                throw;
            }
        }

        public async Task<List<EarningsEvent>> GetUpcomingEarningsAsync(List<string> symbols, int daysAhead = 7)
        {
            try
            {
                _logger.LogInformation($"Getting upcoming earnings for {symbols.Count} symbols, {daysAhead} days ahead");

                var endDate = DateTime.UtcNow.AddDays(daysAhead);
                var events = await _repository.GetEarningsEventsAsync(DateTime.UtcNow, endDate);

                return events.Where(e => symbols.Contains(e.Symbol ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                    .OrderBy(e => e.EventDate)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting upcoming earnings");
                throw;
            }
        }

        public async Task<List<EconomicIndicator>> GetUpcomingIndicatorsAsync(string? country = null, int daysAhead = 7)
        {
            try
            {
                _logger.LogInformation($"Getting upcoming economic indicators for {country ?? "all countries"}, {daysAhead} days ahead");

                var endDate = DateTime.UtcNow.AddDays(daysAhead);
                var indicators = await _repository.GetEconomicIndicatorsAsync(DateTime.UtcNow, endDate, country);

                return indicators.OrderBy(i => i.EventDate).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting upcoming economic indicators");
                throw;
            }
        }

        public async Task RefreshCalendarDataAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                _logger.LogInformation("Refreshing calendar data");

                startDate ??= DateTime.UtcNow.AddDays(-7);
                endDate ??= DateTime.UtcNow.AddDays(30);

                var allEvents = new List<EconomicEvent>();

                foreach (var provider in _providers.Where(p => p.IsConfigured))
                {
                    try
                    {
                        _logger.LogInformation($"Fetching data from provider: {provider.ProviderName}");
                        
                        var events = await provider.GetEventsAsync(startDate.Value, endDate.Value);
                        allEvents.AddRange(events);
                        
                        _logger.LogInformation($"Retrieved {events.Count} events from {provider.ProviderName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to fetch data from provider: {provider.ProviderName}");
                    }
                }

                if (allEvents.Any())
                {
                    await _repository.BulkInsertEventsAsync(allEvents);
                    await _repository.UpdateLastSyncTimeAsync(DateTime.UtcNow);
                    
                    _logger.LogInformation($"Successfully stored {allEvents.Count} events");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing calendar data");
                throw;
            }
        }

        public async Task<DateTime> GetLastUpdateAsync()
        {
            try
            {
                var lastSync = await _repository.GetLastSyncTimeAsync();
                return lastSync ?? DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last update time");
                throw;
            }
        }
    }
}
