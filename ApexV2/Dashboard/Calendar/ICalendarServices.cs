using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApexV2.Dashboard.Calendar
{
    // Economic Calendar Data Provider Interface
    public interface IEconomicCalendarProvider
    {
        Task<List<EconomicEvent>> GetEventsAsync(DateTime startDate, DateTime endDate);
        Task<List<EarningsEvent>> GetEarningsAsync(DateTime startDate, DateTime endDate, string? symbol = null);
        Task<List<EconomicIndicator>> GetEconomicIndicatorsAsync(DateTime startDate, DateTime endDate, string? country = null);
        Task<MarketHours> GetMarketHoursAsync(string market, DateTime date);
        Task<List<EconomicEvent>> SearchEventsAsync(string searchTerm, DateTime? startDate = null, DateTime? endDate = null);
        string ProviderName { get; }
        bool IsConfigured { get; }
    }

    // Economic Calendar Service Interface
    public interface IEconomicCalendarService
    {
        Task<List<EconomicEvent>> GetEventsAsync(CalendarFilter filter);
        Task<CalendarView> GetCalendarViewAsync(DateTime date, CalendarFilter? filter = null);
        Task<List<CalendarSummary>> GetCalendarSummaryAsync(DateTime startDate, DateTime endDate);
        
        Task<EconomicEvent?> GetEventByIdAsync(int eventId);
        Task<EconomicEvent> CreateEventAsync(EconomicEvent economicEvent);
        Task<EconomicEvent> UpdateEventAsync(EconomicEvent economicEvent);
        Task DeleteEventAsync(int eventId);
        
        Task<List<EarningsEvent>> GetUpcomingEarningsAsync(List<string> symbols, int daysAhead = 7);
        Task<List<EconomicIndicator>> GetUpcomingIndicatorsAsync(string? country = null, int daysAhead = 7);
        
        Task RefreshCalendarDataAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<DateTime> GetLastUpdateAsync();
    }

    // Economic Calendar Repository Interface
    public interface IEconomicCalendarRepository
    {
        Task<List<EconomicEvent>> GetEventsAsync(CalendarFilter filter);
        Task<EconomicEvent?> GetEventByIdAsync(int eventId);
        Task<EconomicEvent> AddEventAsync(EconomicEvent economicEvent);
        Task<EconomicEvent> UpdateEventAsync(EconomicEvent economicEvent);
        Task DeleteEventAsync(int eventId);
        
        Task<List<EarningsEvent>> GetEarningsEventsAsync(DateTime startDate, DateTime endDate, string? symbol = null);
        Task<List<EconomicIndicator>> GetEconomicIndicatorsAsync(DateTime startDate, DateTime endDate, string? country = null);
        
        Task<List<EventAlert>> GetPendingAlertsAsync();
        Task<EventAlert> AddAlertAsync(EventAlert alert);
        Task UpdateAlertAsync(EventAlert alert);
        Task DeleteAlertAsync(int alertId);
        
        Task<List<EventNote>> GetEventNotesAsync(int eventId);
        Task<EventNote> AddNoteAsync(EventNote note);
        Task UpdateNoteAsync(EventNote note);
        Task DeleteNoteAsync(int noteId);
        
        Task<MarketHours?> GetMarketHoursAsync(string market, DateTime date);
        Task<MarketHours> SaveMarketHoursAsync(MarketHours marketHours);
        
        Task<EventImpactAnalysis?> GetEventImpactAsync(int eventId);
        Task<EventImpactAnalysis> SaveEventImpactAsync(EventImpactAnalysis impact);
        
        Task BulkInsertEventsAsync(List<EconomicEvent> events);
        Task<DateTime?> GetLastSyncTimeAsync();
        Task UpdateLastSyncTimeAsync(DateTime syncTime);
    }

    // Alert Manager Interface
    public interface ICalendarAlertManager
    {
        Task<List<EventAlert>> GetActiveAlertsAsync();
        Task<EventAlert> CreateAlertAsync(int eventId, DateTime alertTime, string? message = null);
        Task UpdateAlertAsync(EventAlert alert);
        Task DeleteAlertAsync(int alertId);
        Task<List<EventAlert>> GetTriggeredAlertsAsync(DateTime? since = null);
        Task ProcessPendingAlertsAsync();
        
        event EventHandler<EventAlert>? AlertTriggered;
    }

    // Calendar Analytics Interface
    public interface ICalendarAnalyticsService
    {
        Task<EventImpactAnalysis> AnalyzeEventImpactAsync(int eventId, string symbol);
        Task<List<EventImpactAnalysis>> GetImpactHistoryAsync(string symbol, EventType? eventType = null);
        Task<Dictionary<string, decimal>> GetSymbolEventSensitivityAsync(List<string> symbols);
        Task<List<EconomicEvent>> GetHighImpactEventsAsync(DateTime startDate, DateTime endDate);
        Task<Dictionary<EventType, int>> GetEventTypeDistributionAsync(DateTime startDate, DateTime endDate);
        Task<List<string>> GetMostActiveSymbolsAsync(DateTime startDate, DateTime endDate, int topCount = 10);
    }

    // Calendar Data Sync Interface
    public interface ICalendarDataSyncService
    {
        Task SyncAllDataAsync();
        Task SyncEarningsDataAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task SyncEconomicIndicatorsAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task SyncMarketHoursAsync(DateTime? targetDate = null);
        
        Task<bool> IsDataStaleAsync();
        Task<TimeSpan> GetTimeSinceLastSyncAsync();
        Task ScheduleAutoSyncAsync(TimeSpan interval);
        Task StopAutoSyncAsync();
        
        event EventHandler<string>? SyncStatusChanged;
        event EventHandler<Exception>? SyncError;
    }

    // Economic Calendar Export Interface
    public interface ICalendarExportService
    {
        Task<byte[]> ExportToICalAsync(List<EconomicEvent> events);
        Task<byte[]> ExportToCsvAsync(List<EconomicEvent> events);
        Task<byte[]> ExportToExcelAsync(List<EconomicEvent> events);
        Task<string> ExportToJsonAsync(List<EconomicEvent> events);
        
        Task ImportFromICalAsync(byte[] iCalData);
        Task ImportFromCsvAsync(byte[] csvData);
        Task<List<EconomicEvent>> ImportFromJsonAsync(string jsonData);
    }

    // Calendar Widget Interface
    public interface ICalendarWidgetService
    {
        Task<List<EconomicEvent>> GetTodaysEventsAsync();
        Task<List<EconomicEvent>> GetUpcomingEventsAsync(int hoursAhead = 24);
        Task<List<EarningsEvent>> GetThisWeekEarningsAsync();
        Task<int> GetEventCountAsync(DateTime date);
        Task<List<CalendarSummary>> GetWeekSummaryAsync(DateTime weekStart);
        Task<bool> HasImportantEventsAsync(DateTime date);
    }
}
