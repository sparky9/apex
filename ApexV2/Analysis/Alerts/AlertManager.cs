using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Charts.Export;

namespace ApexV2.Analysis.Alerts
{
    /// <summary>
    /// High-level alert manager that coordinates alerts with market data and indicators
    /// </summary>
    public class AlertManager
    {
        private readonly AlertService _alertService;
        private readonly IChartLogger _logger;
        private readonly InAppNotificationProvider _inAppProvider;
        private readonly SystemNotificationProvider _systemProvider;
        private readonly AudioNotificationProvider _audioProvider;
        private readonly LogNotificationProvider _logProvider;
        
        public AlertManager(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _alertService = new AlertService(_logger);
            
            // Initialize notification providers
            _inAppProvider = new InAppNotificationProvider(_logger);
            _systemProvider = new SystemNotificationProvider(_logger);
            _audioProvider = new AudioNotificationProvider(_logger);
            _logProvider = new LogNotificationProvider(_logger);
            
            // Register providers
            _alertService.AddNotificationProvider(_inAppProvider);
            _alertService.AddNotificationProvider(_systemProvider);
            _alertService.AddNotificationProvider(_audioProvider);
            _alertService.AddNotificationProvider(_logProvider);
            
            _logger.Info("Alert Manager initialized with notification providers");
        }

        public AlertService AlertService => _alertService;
        public InAppNotificationProvider InAppProvider => _inAppProvider;
        public SystemNotificationProvider SystemProvider => _systemProvider;
        public AudioNotificationProvider AudioProvider => _audioProvider;
        public LogNotificationProvider LogProvider => _logProvider;

        /// <summary>
        /// Process market data update and evaluate relevant alerts
        /// </summary>
        public async Task ProcessMarketDataUpdateAsync(string symbol, decimal currentPrice, long volume, decimal? previousClose = null)
        {
            try
            {
                var context = new AlertContext
                {
                    Symbol = symbol,
                    CurrentPrice = currentPrice,
                    ReferencePrice = previousClose,
                    CurrentVolume = volume,
                    Timestamp = DateTime.Now
                };

                var results = await _alertService.EvaluateAlertsAsync(context);
                var triggeredCount = results.Count(r => r.WasTriggered);
                
                if (triggeredCount > 0)
                {
                    _logger.Info($"Market data update for {symbol} triggered {triggeredCount} alerts");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing market data update for {symbol}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Process indicator update and evaluate relevant alerts
        /// </summary>
        public async Task ProcessIndicatorUpdateAsync(string symbol, Dictionary<string, decimal> indicatorValues, Dictionary<string, decimal>? previousValues = null)
        {
            try
            {
                var context = new AlertContext
                {
                    Symbol = symbol,
                    IndicatorValues = indicatorValues,
                    PreviousIndicatorValues = previousValues,
                    Timestamp = DateTime.Now
                };

                var results = await _alertService.EvaluateAlertsAsync(context);
                var triggeredCount = results.Count(r => r.WasTriggered);
                
                if (triggeredCount > 0)
                {
                    _logger.Info($"Indicator update for {symbol} triggered {triggeredCount} alerts");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error processing indicator update for {symbol}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Create a comprehensive alert setup for a symbol
        /// </summary>
        public async Task<AlertSetupResult> CreateSymbolAlertSetupAsync(string symbol, AlertSetupOptions options)
        {
            var result = new AlertSetupResult { Symbol = symbol };
            
            try
            {
                // Price alerts
                if (options.EnablePriceAlerts && options.CurrentPrice.HasValue)
                {
                    var currentPrice = options.CurrentPrice.Value;
                    
                    // Support and resistance levels
                    if (options.SupportLevel.HasValue)
                    {
                        await _alertService.CreatePriceAlertAsync(symbol, options.SupportLevel.Value, PriceAlertCondition.Below, $"{symbol} below support");
                        result.CreatedAlerts++;
                    }
                    
                    if (options.ResistanceLevel.HasValue)
                    {
                        await _alertService.CreatePriceAlertAsync(symbol, options.ResistanceLevel.Value, PriceAlertCondition.Above, $"{symbol} above resistance");
                        result.CreatedAlerts++;
                    }
                    
                    // Percentage moves
                    if (options.PercentageThresholds?.Any() == true)
                    {
                        foreach (var threshold in options.PercentageThresholds)
                        {
                            await _alertService.CreatePercentageChangeAlertAsync(symbol, threshold, ChangeDirection.Either, $"{symbol} ±{threshold}% move");
                            result.CreatedAlerts++;
                        }
                    }
                }

                // Volume alerts
                if (options.EnableVolumeAlerts && options.AverageVolume.HasValue)
                {
                    var avgVolume = options.AverageVolume.Value;
                    await _alertService.CreateVolumeAlertAsync(symbol, 0, VolumeCondition.AboveAverage, $"{symbol} unusual volume");
                    result.CreatedAlerts++;
                }

                // Technical indicator alerts
                if (options.EnableIndicatorAlerts && options.IndicatorTargets?.Any() == true)
                {
                    foreach (var indicatorTarget in options.IndicatorTargets)
                    {
                        var alert = new IndicatorAlert
                        {
                            Symbol = symbol,
                            IndicatorName = indicatorTarget.IndicatorName,
                            TargetValue = indicatorTarget.TargetValue,
                            Condition = indicatorTarget.Condition,
                            Name = $"{symbol} {indicatorTarget.IndicatorName} {indicatorTarget.Condition.ToString().ToLower()} {indicatorTarget.TargetValue}"
                        };
                        
                        await _alertService.AddAlertAsync(alert);
                        result.CreatedAlerts++;
                    }
                }

                result.Success = true;
                _logger.Info($"Created alert setup for {symbol}: {result.CreatedAlerts} alerts");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.Error($"Failed to create alert setup for {symbol}: {ex.Message}", ex);
            }

            return result;
        }

        /// <summary>
        /// Get alert summary for dashboard display
        /// </summary>
        public async Task<AlertDashboardSummary> GetDashboardSummaryAsync()
        {
            try
            {
                var statistics = await _alertService.GetStatisticsAsync();
                var recentNotifications = _inAppProvider.GetNotifications().Take(5).ToList();
                var unreadCount = _inAppProvider.GetUnreadNotifications().Count;
                
                return new AlertDashboardSummary
                {
                    TotalAlerts = statistics.TotalAlerts,
                    ActiveAlerts = statistics.ActiveAlerts,
                    UnreadNotifications = unreadCount,
                    RecentTriggers = statistics.RecentlyTriggered.Take(5).ToList(),
                    AlertsByType = statistics.AlertsByType,
                    AlertsByPriority = statistics.AlertsByPriority,
                    MostActiveSymbol = GetMostActiveSymbol(statistics.RecentlyTriggered)
                };
            }
            catch (Exception ex)
            {
                _logger.Error($"Error getting alert dashboard summary: {ex.Message}", ex);
                return new AlertDashboardSummary();
            }
        }

        /// <summary>
        /// Configure notification provider settings
        /// </summary>
        public void ConfigureNotifications(AlertNotificationSettings settings)
        {
            _inAppProvider.IsEnabled = settings.EnableInAppNotifications;
            _systemProvider.IsEnabled = settings.EnableSystemNotifications;
            _audioProvider.IsEnabled = settings.EnableAudioNotifications;
            _logProvider.IsEnabled = settings.EnableLogNotifications;
            
            _logger.Info("Alert notification settings updated");
        }

        /// <summary>
        /// Import alerts from configuration
        /// </summary>
        public async Task<int> ImportAlertsAsync(List<AlertBase> alerts)
        {
            var importedCount = 0;
            
            foreach (var alert in alerts)
            {
                try
                {
                    if (await _alertService.AddAlertAsync(alert))
                    {
                        importedCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Failed to import alert {alert.Name}: {ex.Message}", ex);
                }
            }
            
            _logger.Info($"Imported {importedCount} alerts");
            return importedCount;
        }

        /// <summary>
        /// Export alerts for backup or sharing
        /// </summary>
        public async Task<List<AlertBase>> ExportAlertsAsync()
        {
            try
            {
                return await _alertService.GetAllAlertsAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to export alerts: {ex.Message}", ex);
                return new List<AlertBase>();
            }
        }

        private string GetMostActiveSymbol(List<AlertBase> recentAlerts)
        {
            if (!recentAlerts.Any()) return "N/A";
            
            return recentAlerts
                .GroupBy(a => a.Symbol)
                .OrderByDescending(g => g.Count())
                .First()
                .Key;
        }

        public void Dispose()
        {
            _alertService?.Dispose();
        }
    }

    /// <summary>
    /// Options for setting up alerts for a symbol
    /// </summary>
    public class AlertSetupOptions
    {
        public bool EnablePriceAlerts { get; set; } = true;
        public bool EnableVolumeAlerts { get; set; } = true;
        public bool EnableIndicatorAlerts { get; set; } = true;
        
        public decimal? CurrentPrice { get; set; }
        public decimal? SupportLevel { get; set; }
        public decimal? ResistanceLevel { get; set; }
        public List<decimal> PercentageThresholds { get; set; } = new();
        
        public long? AverageVolume { get; set; }
        
        public List<IndicatorAlertTarget> IndicatorTargets { get; set; } = new();
    }

    /// <summary>
    /// Indicator alert target configuration
    /// </summary>
    public class IndicatorAlertTarget
    {
        public string IndicatorName { get; set; } = string.Empty;
        public decimal TargetValue { get; set; }
        public IndicatorCondition Condition { get; set; } = IndicatorCondition.Above;
    }

    /// <summary>
    /// Result of alert setup operation
    /// </summary>
    public class AlertSetupResult
    {
        public string Symbol { get; set; } = string.Empty;
        public bool Success { get; set; }
        public int CreatedAlerts { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>
    /// Dashboard summary for alerts
    /// </summary>
    public class AlertDashboardSummary
    {
        public int TotalAlerts { get; set; }
        public int ActiveAlerts { get; set; }
        public int UnreadNotifications { get; set; }
        public List<AlertBase> RecentTriggers { get; set; } = new();
        public Dictionary<AlertType, int> AlertsByType { get; set; } = new();
        public Dictionary<AlertPriority, int> AlertsByPriority { get; set; } = new();
        public string MostActiveSymbol { get; set; } = "N/A";
    }

    /// <summary>
    /// Notification settings configuration
    /// </summary>
    public class AlertNotificationSettings
    {
        public bool EnableInAppNotifications { get; set; } = true;
        public bool EnableSystemNotifications { get; set; } = true;
        public bool EnableAudioNotifications { get; set; } = true;
        public bool EnableLogNotifications { get; set; } = true;
        public bool EnableEmailNotifications { get; set; } = false;
        
        // System notification settings
        public bool ShowSystemNotificationsForHighPriority { get; set; } = true;
        public bool ShowSystemNotificationsForCritical { get; set; } = true;
        
        // Audio settings
        public bool PlaySoundForHighPriority { get; set; } = true;
        public bool PlaySoundForCritical { get; set; } = true;
        
        // Email settings (if enabled)
        public string EmailServer { get; set; } = string.Empty;
        public string EmailUsername { get; set; } = string.Empty;
        public string EmailPassword { get; set; } = string.Empty;
        public string EmailRecipient { get; set; } = string.Empty;
    }
}
