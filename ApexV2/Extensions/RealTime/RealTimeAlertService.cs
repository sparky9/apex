#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApexV2.Extensions.RealTime
{
    /// <summary>
    /// Service for managing real-time alerts and notifications
    /// </summary>
    public class RealTimeAlertService : IRealTimeAlertService, IDisposable
    {
        private readonly ILogger<RealTimeAlertService> _logger;
        private readonly RealTimeConfiguration _config;
        private readonly ConcurrentDictionary<Guid, RealTimeAlert> _alerts = new();
        private readonly ConcurrentDictionary<string, List<Guid>> _symbolAlerts = new();
        private readonly SemaphoreSlim _alertLock = new(1, 1);
        private bool _disposed = false;

        public event EventHandler<RealTimeAlertTriggeredEventArgs> AlertTriggered;

        public RealTimeAlertService(ILogger<RealTimeAlertService> logger, RealTimeConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _logger.LogInformation("Real-time alert service initialized");
        }

        public async Task<Guid> CreateAlertAsync(RealTimeAlert alert)
        {
            if (alert == null)
            {
                throw new ArgumentNullException(nameof(alert));
            }

            if (string.IsNullOrEmpty(alert.Symbol))
            {
                throw new ArgumentException("Alert symbol cannot be empty", nameof(alert));
            }

            try
            {
                await _alertLock.WaitAsync();

                alert.Id = Guid.NewGuid();
                alert.CreatedAt = DateTime.UtcNow;
                alert.Symbol = alert.Symbol.ToUpperInvariant();

                _alerts[alert.Id] = alert;

                // Add to symbol index
                var symbolAlertList = _symbolAlerts.GetOrAdd(alert.Symbol, _ => new List<Guid>());
                lock (symbolAlertList)
                {
                    symbolAlertList.Add(alert.Id);
                }

                _logger.LogInformation("Created alert {AlertId} for {Symbol}: {Condition} {Threshold}", 
                    alert.Id, alert.Symbol, alert.Condition, alert.Threshold);

                return alert.Id;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating alert for {Symbol}", alert.Symbol);
                throw;
            }
            finally
            {
                _alertLock.Release();
            }
        }

        public async Task<bool> UpdateAlertAsync(Guid alertId, RealTimeAlert alert)
        {
            if (alert == null)
            {
                throw new ArgumentNullException(nameof(alert));
            }

            try
            {
                await _alertLock.WaitAsync();

                if (!_alerts.TryGetValue(alertId, out var existingAlert))
                {
                    _logger.LogWarning("Alert {AlertId} not found for update", alertId);
                    return false;
                }

                // Update symbol index if symbol changed
                if (existingAlert.Symbol != alert.Symbol?.ToUpperInvariant())
                {
                    // Remove from old symbol index
                    if (_symbolAlerts.TryGetValue(existingAlert.Symbol, out var oldSymbolAlerts))
                    {
                        lock (oldSymbolAlerts)
                        {
                            oldSymbolAlerts.Remove(alertId);
                        }
                    }

                    // Add to new symbol index
                    alert.Symbol = alert.Symbol.ToUpperInvariant();
                    var newSymbolAlerts = _symbolAlerts.GetOrAdd(alert.Symbol, _ => new List<Guid>());
                    lock (newSymbolAlerts)
                    {
                        newSymbolAlerts.Add(alertId);
                    }
                }

                // Preserve original creation data
                alert.Id = alertId;
                alert.CreatedAt = existingAlert.CreatedAt;
                
                _alerts[alertId] = alert;

                _logger.LogInformation("Updated alert {AlertId} for {Symbol}: {Condition} {Threshold}", 
                    alertId, alert.Symbol, alert.Condition, alert.Threshold);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating alert {AlertId}", alertId);
                return false;
            }
            finally
            {
                _alertLock.Release();
            }
        }

        public async Task<bool> DeleteAlertAsync(Guid alertId)
        {
            try
            {
                await _alertLock.WaitAsync();

                if (!_alerts.TryRemove(alertId, out var alert))
                {
                    _logger.LogWarning("Alert {AlertId} not found for deletion", alertId);
                    return false;
                }

                // Remove from symbol index
                if (_symbolAlerts.TryGetValue(alert.Symbol, out var symbolAlerts))
                {
                    lock (symbolAlerts)
                    {
                        symbolAlerts.Remove(alertId);
                    }
                    
                    // Clean up empty symbol entries
                    if (symbolAlerts.Count == 0)
                    {
                        _symbolAlerts.TryRemove(alert.Symbol, out _);
                    }
                }

                _logger.LogInformation("Deleted alert {AlertId} for {Symbol}", alertId, alert.Symbol);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting alert {AlertId}", alertId);
                return false;
            }
            finally
            {
                _alertLock.Release();
            }
        }

        public async Task<RealTimeAlert> GetAlertAsync(Guid alertId)
        {
            await Task.Yield(); // Make it properly async
            
            _alerts.TryGetValue(alertId, out var alert);
            return alert;
        }

        public async Task<List<RealTimeAlert>> GetAlertsForSymbolAsync(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return new List<RealTimeAlert>();
            }

            await Task.Yield(); // Make it properly async

            symbol = symbol.ToUpperInvariant();
            var result = new List<RealTimeAlert>();

            if (_symbolAlerts.TryGetValue(symbol, out var alertIds))
            {
                List<Guid> alertIdsCopy;
                lock (alertIds)
                {
                    alertIdsCopy = new List<Guid>(alertIds);
                }

                foreach (var alertId in alertIdsCopy)
                {
                    if (_alerts.TryGetValue(alertId, out var alert))
                    {
                        result.Add(alert);
                    }
                }
            }

            return result;
        }

        public async Task<List<RealTimeAlert>> GetAllActiveAlertsAsync()
        {
            await Task.Yield(); // Make it properly async
            
            return _alerts.Values
                .Where(a => a.IsActive && (!a.ExpiresAt.HasValue || a.ExpiresAt > DateTime.UtcNow))
                .ToList();
        }

        public async Task ProcessUpdateForAlertsAsync(RealTimeUpdate update)
        {
            if (!_config.EnableAlerts || update == null || !update.IsValid())
            {
                return;
            }

            try
            {
                var symbol = update.Symbol.ToUpperInvariant();
                
                if (!_symbolAlerts.TryGetValue(symbol, out var alertIds))
                {
                    return; // No alerts for this symbol
                }

                List<Guid> alertIdsCopy;
                lock (alertIds)
                {
                    alertIdsCopy = new List<Guid>(alertIds);
                }

                var triggeredAlerts = new List<RealTimeAlert>();

                foreach (var alertId in alertIdsCopy)
                {
                    if (_alerts.TryGetValue(alertId, out var alert) && alert.ShouldTrigger(update))
                    {
                        triggeredAlerts.Add(alert);
                    }
                }

                // Process triggered alerts
                foreach (var alert in triggeredAlerts)
                {
                    await ProcessTriggeredAlertAsync(alert, update);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing alerts for update: {Symbol} {DataType}", 
                    update.Symbol, update.DataType);
            }
        }

        private async Task ProcessTriggeredAlertAsync(RealTimeAlert alert, RealTimeUpdate update)
        {
            try
            {
                alert.LastTriggered = DateTime.UtcNow;
                alert.TriggerCount++;

                var eventArgs = new RealTimeAlertTriggeredEventArgs
                {
                    Alert = alert,
                    TriggeringUpdate = update,
                    TriggeredAt = alert.LastTriggered.Value,
                    Message = FormatAlertMessage(alert, update)
                };

                _logger.LogInformation("Alert triggered: {AlertName} for {Symbol} - {Message}", 
                    alert.Name, alert.Symbol, eventArgs.Message);

                // Raise event
                AlertTriggered?.Invoke(this, eventArgs);

                // Handle notifications
                await HandleAlertNotificationsAsync(alert, eventArgs);

                // Remove one-time alerts
                if (alert.IsOneTime)
                {
                    await DeleteAlertAsync(alert.Id);
                    _logger.LogInformation("Removed one-time alert {AlertId} after triggering", alert.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing triggered alert {AlertId}", alert.Id);
            }
        }

        private async Task HandleAlertNotificationsAsync(RealTimeAlert alert, RealTimeAlertTriggeredEventArgs eventArgs)
        {
            try
            {
                // Show popup notification
                if (alert.ShowPopup && _config.ShowAlertPopups)
                {
                    // This would integrate with the UI notification system
                    _logger.LogInformation("Alert popup: {Message}", eventArgs.Message);
                }

                // Play sound
                if (alert.PlaySound)
                {
                    var soundFile = !string.IsNullOrEmpty(alert.SoundFile) 
                        ? alert.SoundFile 
                        : _config.DefaultAlertSoundFile;
                    
                    // This would integrate with the audio system
                    _logger.LogInformation("Alert sound: {SoundFile}", soundFile);
                }

                // Send email (if configured)
                if (alert.SendEmail && !string.IsNullOrEmpty(alert.EmailAddress))
                {
                    // This would integrate with the email system
                    _logger.LogInformation("Alert email to: {EmailAddress}", alert.EmailAddress);
                }

                await Task.Yield(); // Make it properly async
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling alert notifications for {AlertId}", alert.Id);
            }
        }

        private string FormatAlertMessage(RealTimeAlert alert, RealTimeUpdate update)
        {
            try
            {
                return alert.Condition switch
                {
                    RealTimeAlertCondition.GreaterThan => $"{alert.Symbol} price ${update.Price:F2} is above ${alert.Threshold:F2}",
                    RealTimeAlertCondition.LessThan => $"{alert.Symbol} price ${update.Price:F2} is below ${alert.Threshold:F2}",
                    RealTimeAlertCondition.GreaterThanOrEqual => $"{alert.Symbol} price ${update.Price:F2} is at or above ${alert.Threshold:F2}",
                    RealTimeAlertCondition.LessThanOrEqual => $"{alert.Symbol} price ${update.Price:F2} is at or below ${alert.Threshold:F2}",
                    RealTimeAlertCondition.Equals => $"{alert.Symbol} price ${update.Price:F2} equals ${alert.Threshold:F2}",
                    RealTimeAlertCondition.VolumeAbove => $"{alert.Symbol} volume {update.Volume:N0} is above {alert.Threshold:N0}",
                    RealTimeAlertCondition.VolumeBelow => $"{alert.Symbol} volume {update.Volume:N0} is below {alert.Threshold:N0}",
                    RealTimeAlertCondition.PercentChangeAbove => $"{alert.Symbol} change {update.ChangePercent:F2}% is above {alert.Threshold:F2}%",
                    RealTimeAlertCondition.PercentChangeBelow => $"{alert.Symbol} change {update.ChangePercent:F2}% is below {alert.Threshold:F2}%",
                    RealTimeAlertCondition.Custom => $"{alert.Symbol} custom condition met: {alert.CustomExpression}",
                    _ => $"{alert.Symbol} alert triggered"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error formatting alert message for {AlertId}", alert.Id);
                return $"{alert.Symbol} alert triggered";
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _alerts.Clear();
                _symbolAlerts.Clear();
                _alertLock?.Dispose();

                _logger.LogInformation("Real-time alert service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time alert service");
            }

            _disposed = true;
        }
    }
}
