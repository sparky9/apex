using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApexV2.Charts.Export;

namespace ApexV2.Analysis.Alerts
{
    /// <summary>
    /// Core alert service for managing and evaluating alerts
    /// </summary>
    public class AlertService
    {
        private readonly IChartLogger _logger;
        private readonly List<AlertBase> _alerts = new();
        private readonly Timer _evaluationTimer;
        private readonly SemaphoreSlim _alertLock = new(1, 1);
        private readonly List<IAlertNotificationProvider> _notificationProviders = new();
        
        public AlertService(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Set up periodic alert evaluation (every 10 seconds)
            _evaluationTimer = new Timer(EvaluateAlertsCallback, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10));
        }

        public event EventHandler<AlertTriggeredEventArgs>? AlertTriggered;
        public event EventHandler<AlertManagementEventArgs>? AlertAdded;
        public event EventHandler<AlertManagementEventArgs>? AlertRemoved;
        public event EventHandler<AlertManagementEventArgs>? AlertUpdated;

        /// <summary>
        /// Add a new alert to the system
        /// </summary>
        public async Task<bool> AddAlertAsync(AlertBase alert)
        {
            if (alert == null) throw new ArgumentNullException(nameof(alert));

            var validationErrors = alert.Validate();
            if (validationErrors.Any())
            {
                _logger.Warn($"Alert validation failed: {string.Join(", ", validationErrors)}");
                return false;
            }

            await _alertLock.WaitAsync();
            try
            {
                _alerts.Add(alert);
                _logger.Info($"Added alert: {alert.Name} for {alert.Symbol}");
                
                AlertAdded?.Invoke(this, new AlertManagementEventArgs(alert));
                return true;
            }
            finally
            {
                _alertLock.Release();
            }
        }

        /// <summary>
        /// Remove an alert from the system
        /// </summary>
        public async Task<bool> RemoveAlertAsync(Guid alertId)
        {
            await _alertLock.WaitAsync();
            try
            {
                var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
                if (alert == null) return false;

                _alerts.Remove(alert);
                _logger.Info($"Removed alert: {alert.Name} for {alert.Symbol}");
                
                AlertRemoved?.Invoke(this, new AlertManagementEventArgs(alert));
                return true;
            }
            finally
            {
                _alertLock.Release();
            }
        }

        /// <summary>
        /// Update an existing alert
        /// </summary>
        public async Task<bool> UpdateAlertAsync(AlertBase updatedAlert)
        {
            if (updatedAlert == null) throw new ArgumentNullException(nameof(updatedAlert));

            var validationErrors = updatedAlert.Validate();
            if (validationErrors.Any())
            {
                _logger.Warn($"Alert validation failed: {string.Join(", ", validationErrors)}");
                return false;
            }

            await _alertLock.WaitAsync();
            try
            {
                var existingIndex = _alerts.FindIndex(a => a.Id == updatedAlert.Id);
                if (existingIndex == -1) return false;

                _alerts[existingIndex] = updatedAlert;
                _logger.Info($"Updated alert: {updatedAlert.Name} for {updatedAlert.Symbol}");
                
                AlertUpdated?.Invoke(this, new AlertManagementEventArgs(updatedAlert));
                return true;
            }
            finally
            {
                _alertLock.Release();
            }
        }

        /// <summary>
        /// Get all alerts
        /// </summary>
        public async Task<List<AlertBase>> GetAllAlertsAsync()
        {
            await _alertLock.WaitAsync();
            try
            {
                return _alerts.ToList();
            }
            finally
            {
                _alertLock.Release();
            }
        }

        /// <summary>
        /// Get alerts for a specific symbol
        /// </summary>
        public async Task<List<AlertBase>> GetAlertsForSymbolAsync(string symbol)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return new List<AlertBase>();

            await _alertLock.WaitAsync();
            try
            {
                return _alerts.Where(a => a.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            finally
            {
                _alertLock.Release();
            }
        }

        /// <summary>
        /// Get alerts by type
        /// </summary>
        public async Task<List<AlertBase>> GetAlertsByTypeAsync(AlertType alertType)
        {
            await _alertLock.WaitAsync();
            try
            {
                return _alerts.Where(a => a.AlertType == alertType).ToList();
            }
            finally
            {
                _alertLock.Release();
            }
        }

        /// <summary>
        /// Enable or disable an alert
        /// </summary>
        public async Task<bool> SetAlertEnabledAsync(Guid alertId, bool enabled)
        {
            await _alertLock.WaitAsync();
            try
            {
                var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
                if (alert == null) return false;

                alert.IsEnabled = enabled;
                _logger.Info($"Alert {alert.Name} {(enabled ? "enabled" : "disabled")}");
                
                AlertUpdated?.Invoke(this, new AlertManagementEventArgs(alert));
                return true;
            }
            finally
            {
                _alertLock.Release();
            }
        }

        /// <summary>
        /// Evaluate alerts for a specific symbol with provided context
        /// </summary>
        public async Task<List<AlertTriggerResult>> EvaluateAlertsAsync(AlertContext context)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var results = new List<AlertTriggerResult>();
            var symbolAlerts = await GetAlertsForSymbolAsync(context.Symbol);

            foreach (var alert in symbolAlerts.Where(a => a.IsEnabled))
            {
                try
                {
                    // Check expiration
                    if (alert.ExpiresAt.HasValue && alert.ExpiresAt.Value <= DateTime.Now)
                    {
                        await RemoveAlertAsync(alert.Id);
                        _logger.Info($"Removed expired alert: {alert.Name}");
                        continue;
                    }

                    // Evaluate alert condition
                    var shouldTrigger = alert.ShouldTrigger(context);
                    var result = new AlertTriggerResult
                    {
                        Alert = alert,
                        WasTriggered = shouldTrigger,
                        Context = context,
                        TriggeredAt = DateTime.Now
                    };

                    if (shouldTrigger)
                    {
                        result.Message = alert.GetTriggerMessage(context);
                        alert.LastTriggered = DateTime.Now;
                        alert.TriggerCount++;

                        _logger.Info($"Alert triggered: {alert.Name} - {result.Message}");
                        
                        // Send notifications
                        await SendNotificationsAsync(alert, result.Message, context);
                        
                        // Fire event
                        AlertTriggered?.Invoke(this, new AlertTriggeredEventArgs(alert, result.Message, context));
                    }

                    results.Add(result);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error evaluating alert {alert.Name}: {ex.Message}", ex);
                    results.Add(new AlertTriggerResult
                    {
                        Alert = alert,
                        WasTriggered = false,
                        Error = ex,
                        Context = context
                    });
                }
            }

            return results;
        }

        /// <summary>
        /// Bulk evaluate alerts for multiple symbols
        /// </summary>
        public async Task<Dictionary<string, List<AlertTriggerResult>>> EvaluateMultipleAlertsAsync(Dictionary<string, AlertContext> contexts)
        {
            var results = new Dictionary<string, List<AlertTriggerResult>>();

            var tasks = contexts.Select(async kvp =>
            {
                var symbolResults = await EvaluateAlertsAsync(kvp.Value);
                return new { Symbol = kvp.Key, Results = symbolResults };
            });

            var completedTasks = await Task.WhenAll(tasks);

            foreach (var task in completedTasks)
            {
                results[task.Symbol] = task.Results;
            }

            return results;
        }

        /// <summary>
        /// Add a notification provider
        /// </summary>
        public void AddNotificationProvider(IAlertNotificationProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            
            _notificationProviders.Add(provider);
            _logger.Info($"Added notification provider: {provider.GetType().Name}");
        }

        /// <summary>
        /// Remove a notification provider
        /// </summary>
        public void RemoveNotificationProvider(IAlertNotificationProvider provider)
        {
            if (provider == null) return;
            
            _notificationProviders.Remove(provider);
            _logger.Info($"Removed notification provider: {provider.GetType().Name}");
        }

        /// <summary>
        /// Get alert statistics
        /// </summary>
        public async Task<AlertStatistics> GetStatisticsAsync()
        {
            await _alertLock.WaitAsync();
            try
            {
                var activeAlerts = _alerts.Where(a => a.IsEnabled).ToList();
                var expiredAlerts = _alerts.Where(a => a.ExpiresAt.HasValue && a.ExpiresAt.Value <= DateTime.Now).ToList();
                
                return new AlertStatistics
                {
                    TotalAlerts = _alerts.Count,
                    ActiveAlerts = activeAlerts.Count,
                    ExpiredAlerts = expiredAlerts.Count,
                    TotalTriggers = _alerts.Sum(a => a.TriggerCount),
                    AlertsByType = _alerts.GroupBy(a => a.AlertType).ToDictionary(g => g.Key, g => g.Count()),
                    AlertsByPriority = _alerts.GroupBy(a => a.Priority).ToDictionary(g => g.Key, g => g.Count()),
                    MostTriggeredAlert = _alerts.OrderByDescending(a => a.TriggerCount).FirstOrDefault(),
                    RecentlyTriggered = _alerts.Where(a => a.LastTriggered.HasValue && a.LastTriggered.Value > DateTime.Now.AddHours(-24))
                        .OrderByDescending(a => a.LastTriggered).Take(10).ToList()
                };
            }
            finally
            {
                _alertLock.Release();
            }
        }

        /// <summary>
        /// Clean up expired alerts
        /// </summary>
        public async Task<int> CleanupExpiredAlertsAsync()
        {
            await _alertLock.WaitAsync();
            try
            {
                var expiredAlerts = _alerts.Where(a => a.ExpiresAt.HasValue && a.ExpiresAt.Value <= DateTime.Now).ToList();
                
                foreach (var alert in expiredAlerts)
                {
                    _alerts.Remove(alert);
                    _logger.Info($"Removed expired alert: {alert.Name}");
                }

                return expiredAlerts.Count;
            }
            finally
            {
                _alertLock.Release();
            }
        }

        /// <summary>
        /// Create common alert types quickly
        /// </summary>
        public async Task<bool> CreatePriceAlertAsync(string symbol, decimal targetPrice, PriceAlertCondition condition, string? name = null)
        {
            var alert = new PriceAlert
            {
                Symbol = symbol,
                TargetPrice = targetPrice,
                Condition = condition,
                Name = name ?? $"{symbol} price {condition.ToString().ToLower()} {targetPrice:C}"
            };

            return await AddAlertAsync(alert);
        }

        public async Task<bool> CreatePercentageChangeAlertAsync(string symbol, decimal changePercent, ChangeDirection direction, string? name = null)
        {
            var alert = new PercentageChangeAlert
            {
                Symbol = symbol,
                ChangePercent = changePercent,
                Direction = direction,
                Name = name ?? $"{symbol} {direction.ToString().ToLower()} {changePercent}%"
            };

            return await AddAlertAsync(alert);
        }

        public async Task<bool> CreateVolumeAlertAsync(string symbol, long targetVolume, VolumeCondition condition, string? name = null)
        {
            var alert = new VolumeAlert
            {
                Symbol = symbol,
                TargetVolume = targetVolume,
                Condition = condition,
                Name = name ?? $"{symbol} volume {condition.ToString().ToLower()} {targetVolume:N0}"
            };

            return await AddAlertAsync(alert);
        }

        private async Task SendNotificationsAsync(AlertBase alert, string message, AlertContext context)
        {
            var tasks = _notificationProviders.Select(async provider =>
            {
                try
                {
                    await provider.SendNotificationAsync(alert, message, context);
                }
                catch (Exception ex)
                {
                    _logger.Error($"Notification provider {provider.GetType().Name} failed: {ex.Message}", ex);
                }
            });

            await Task.WhenAll(tasks);
        }

        private async void EvaluateAlertsCallback(object? state)
        {
            try
            {
                // This would be called by market data updates in a real implementation
                // For now, it's a placeholder for time-based alerts
                var timeBasedAlerts = await GetAlertsByTypeAsync(AlertType.TimeBased);
                
                foreach (var alert in timeBasedAlerts.Where(a => a.IsEnabled))
                {
                    var context = new AlertContext
                    {
                        Symbol = alert.Symbol,
                        Timestamp = DateTime.Now
                    };

                    await EvaluateAlertsAsync(context);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in alert evaluation timer: {ex.Message}", ex);
            }
        }

        public void Dispose()
        {
            _evaluationTimer?.Dispose();
            _alertLock?.Dispose();
        }
    }

    /// <summary>
    /// Interface for alert notification providers
    /// </summary>
    public interface IAlertNotificationProvider
    {
        Task SendNotificationAsync(AlertBase alert, string message, AlertContext context);
        string ProviderName { get; }
        bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Event args for alert triggered events
    /// </summary>
    public class AlertTriggeredEventArgs : EventArgs
    {
        public AlertBase Alert { get; }
        public string Message { get; }
        public AlertContext Context { get; }
        public DateTime TriggeredAt { get; } = DateTime.Now;

        public AlertTriggeredEventArgs(AlertBase alert, string message, AlertContext context)
        {
            Alert = alert ?? throw new ArgumentNullException(nameof(alert));
            Message = message ?? throw new ArgumentNullException(nameof(message));
            Context = context ?? throw new ArgumentNullException(nameof(context));
        }
    }

    /// <summary>
    /// Event args for alert management events
    /// </summary>
    public class AlertManagementEventArgs : EventArgs
    {
        public AlertBase Alert { get; }
        public DateTime EventTime { get; } = DateTime.Now;

        public AlertManagementEventArgs(AlertBase alert)
        {
            Alert = alert ?? throw new ArgumentNullException(nameof(alert));
        }
    }

    /// <summary>
    /// Alert system statistics
    /// </summary>
    public class AlertStatistics
    {
        public int TotalAlerts { get; set; }
        public int ActiveAlerts { get; set; }
        public int ExpiredAlerts { get; set; }
        public int TotalTriggers { get; set; }
        public Dictionary<AlertType, int> AlertsByType { get; set; } = new();
        public Dictionary<AlertPriority, int> AlertsByPriority { get; set; } = new();
        public AlertBase? MostTriggeredAlert { get; set; }
        public List<AlertBase> RecentlyTriggered { get; set; } = new();
    }
}
