using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;

namespace ApexV2.Dashboard.Calendar
{
    public class CalendarAlertManager : ICalendarAlertManager, IDisposable
    {
        private readonly IEconomicCalendarRepository _repository;
        private readonly ILogger<CalendarAlertManager> _logger;
        private readonly System.Timers.Timer _alertTimer;
        private bool _disposed = false;

        public event EventHandler<EventAlert>? AlertTriggered;

        public CalendarAlertManager(
            IEconomicCalendarRepository repository,
            ILogger<CalendarAlertManager> logger)
        {
            _repository = repository;
            _logger = logger;
            
            // Check for alerts every minute
            _alertTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds);
            _alertTimer.Elapsed += OnTimerElapsed;
            _alertTimer.AutoReset = true;
            _alertTimer.Start();
        }

        public async Task<List<EventAlert>> GetActiveAlertsAsync()
        {
            try
            {
                var alerts = await _repository.GetPendingAlertsAsync();
                return alerts.Where(a => a.IsEnabled && !a.HasBeenTriggered).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting active alerts");
                throw;
            }
        }

        public async Task<EventAlert> CreateAlertAsync(int eventId, DateTime alertTime, string? message = null)
        {
            try
            {
                _logger.LogInformation($"Creating alert for event {eventId} at {alertTime}");

                var alert = new EventAlert
                {
                    EventId = eventId,
                    AlertTime = alertTime,
                    Message = message,
                    IsEnabled = true,
                    HasBeenTriggered = false,
                    CreatedAt = DateTime.UtcNow
                };

                return await _repository.AddAlertAsync(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating alert for event {eventId}");
                throw;
            }
        }

        public async Task UpdateAlertAsync(EventAlert alert)
        {
            try
            {
                _logger.LogInformation($"Updating alert {alert.Id}");
                await _repository.UpdateAlertAsync(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating alert {alert.Id}");
                throw;
            }
        }

        public async Task DeleteAlertAsync(int alertId)
        {
            try
            {
                _logger.LogInformation($"Deleting alert {alertId}");
                await _repository.DeleteAlertAsync(alertId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting alert {alertId}");
                throw;
            }
        }

        public async Task<List<EventAlert>> GetTriggeredAlertsAsync(DateTime? since = null)
        {
            try
            {
                var alerts = await _repository.GetPendingAlertsAsync();
                
                var query = alerts.Where(a => a.HasBeenTriggered);
                
                if (since.HasValue)
                {
                    query = query.Where(a => a.TriggeredAt >= since.Value);
                }

                return query.OrderByDescending(a => a.TriggeredAt).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting triggered alerts");
                throw;
            }
        }

        public async Task ProcessPendingAlertsAsync()
        {
            try
            {
                var currentTime = DateTime.UtcNow;
                var pendingAlerts = await GetActiveAlertsAsync();

                var alertsToTrigger = pendingAlerts.Where(a => 
                    a.AlertTime <= currentTime && 
                    !a.HasBeenTriggered && 
                    a.IsEnabled).ToList();

                foreach (var alert in alertsToTrigger)
                {
                    await TriggerAlertAsync(alert);
                }

                if (alertsToTrigger.Any())
                {
                    _logger.LogInformation($"Processed {alertsToTrigger.Count} pending alerts");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing pending alerts");
            }
        }

        private async Task TriggerAlertAsync(EventAlert alert)
        {
            try
            {
                _logger.LogInformation($"Triggering alert {alert.Id} for event {alert.EventId}");

                // Mark alert as triggered
                alert.HasBeenTriggered = true;
                alert.TriggeredAt = DateTime.UtcNow;
                
                await _repository.UpdateAlertAsync(alert);

                // Fire the event
                AlertTriggered?.Invoke(this, alert);

                _logger.LogInformation($"Alert {alert.Id} triggered successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error triggering alert {alert.Id}");
            }
        }

        private async void OnTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            await ProcessPendingAlertsAsync();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _alertTimer?.Stop();
                _alertTimer?.Dispose();
                _disposed = true;
            }
        }
    }

    // Calendar Data Sync Service Implementation
    public class CalendarDataSyncService : ICalendarDataSyncService
    {
        private readonly IEconomicCalendarRepository _repository;
        private readonly List<IEconomicCalendarProvider> _providers;
        private readonly ILogger<CalendarDataSyncService> _logger;
        private readonly System.Timers.Timer? _autoSyncTimer;
        private bool _disposed = false;

        public event EventHandler<string>? SyncStatusChanged;
        public event EventHandler<Exception>? SyncError;

        public CalendarDataSyncService(
            IEconomicCalendarRepository repository,
            IEnumerable<IEconomicCalendarProvider> providers,
            ILogger<CalendarDataSyncService> logger)
        {
            _repository = repository;
            _providers = providers.ToList();
            _logger = logger;
        }

        public async Task SyncAllDataAsync()
        {
            try
            {
                _logger.LogInformation("Starting full calendar data sync");
                SyncStatusChanged?.Invoke(this, "Starting sync...");

                var startDate = DateTime.UtcNow.AddDays(-7);
                var endDate = DateTime.UtcNow.AddDays(90);

                await SyncEarningsDataAsync(startDate, endDate);
                await SyncEconomicIndicatorsAsync(startDate, endDate);
                await SyncMarketHoursAsync();

                await _repository.UpdateLastSyncTimeAsync(DateTime.UtcNow);

                _logger.LogInformation("Calendar data sync completed successfully");
                SyncStatusChanged?.Invoke(this, "Sync completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during calendar data sync");
                SyncError?.Invoke(this, ex);
                SyncStatusChanged?.Invoke(this, "Sync failed");
                throw;
            }
        }

        public async Task SyncEarningsDataAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow;
                endDate ??= DateTime.UtcNow.AddDays(30);

                _logger.LogInformation($"Syncing earnings data from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                SyncStatusChanged?.Invoke(this, "Syncing earnings data...");

                var allEarnings = new List<EarningsEvent>();

                foreach (var provider in _providers.Where(p => p.IsConfigured))
                {
                    try
                    {
                        var earnings = await provider.GetEarningsAsync(startDate.Value, endDate.Value);
                        allEarnings.AddRange(earnings);
                        
                        _logger.LogInformation($"Retrieved {earnings.Count} earnings from {provider.ProviderName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to sync earnings from {provider.ProviderName}");
                    }
                }

                if (allEarnings.Any())
                {
                    await _repository.BulkInsertEventsAsync(allEarnings.Cast<EconomicEvent>().ToList());
                    _logger.LogInformation($"Stored {allEarnings.Count} earnings events");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing earnings data");
                throw;
            }
        }

        public async Task SyncEconomicIndicatorsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                startDate ??= DateTime.UtcNow;
                endDate ??= DateTime.UtcNow.AddDays(30);

                _logger.LogInformation($"Syncing economic indicators from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                SyncStatusChanged?.Invoke(this, "Syncing economic indicators...");

                var allIndicators = new List<EconomicIndicator>();

                foreach (var provider in _providers.Where(p => p.IsConfigured))
                {
                    try
                    {
                        var indicators = await provider.GetEconomicIndicatorsAsync(startDate.Value, endDate.Value);
                        allIndicators.AddRange(indicators);
                        
                        _logger.LogInformation($"Retrieved {indicators.Count} indicators from {provider.ProviderName}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to sync indicators from {provider.ProviderName}");
                    }
                }

                if (allIndicators.Any())
                {
                    await _repository.BulkInsertEventsAsync(allIndicators.Cast<EconomicEvent>().ToList());
                    _logger.LogInformation($"Stored {allIndicators.Count} economic indicators");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing economic indicators");
                throw;
            }
        }

        public async Task SyncMarketHoursAsync(DateTime? targetDate = null)
        {
            try
            {
                targetDate ??= DateTime.UtcNow;

                _logger.LogInformation($"Syncing market hours for {targetDate:yyyy-MM-dd}");
                SyncStatusChanged?.Invoke(this, "Syncing market hours...");

                var markets = new[] { "NYSE", "NASDAQ", "TSX" };

                foreach (var provider in _providers.Where(p => p.IsConfigured))
                {
                    foreach (var market in markets)
                    {
                        try
                        {
                            var marketHours = await provider.GetMarketHoursAsync(market, targetDate.Value);
                            await _repository.SaveMarketHoursAsync(marketHours);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, $"Failed to sync market hours for {market} from {provider.ProviderName}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error syncing market hours");
                throw;
            }
        }

        public async Task<bool> IsDataStaleAsync()
        {
            try
            {
                var lastSync = await _repository.GetLastSyncTimeAsync();
                if (!lastSync.HasValue)
                    return true;

                var staleThreshold = TimeSpan.FromHours(4);
                return DateTime.UtcNow - lastSync.Value > staleThreshold;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if data is stale");
                return true; // Assume stale if we can't check
            }
        }

        public async Task<TimeSpan> GetTimeSinceLastSyncAsync()
        {
            try
            {
                var lastSync = await _repository.GetLastSyncTimeAsync();
                if (!lastSync.HasValue)
                    return TimeSpan.MaxValue;

                return DateTime.UtcNow - lastSync.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting time since last sync");
                return TimeSpan.MaxValue;
            }
        }

        public Task ScheduleAutoSyncAsync(TimeSpan interval)
        {
            _logger.LogInformation($"Scheduling auto sync every {interval}");
            // Implementation would start a timer for auto sync
            return Task.CompletedTask;
        }

        public Task StopAutoSyncAsync()
        {
            _logger.LogInformation("Stopping auto sync");
            // Implementation would stop the auto sync timer
            return Task.CompletedTask;
        }
    }
}
