#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ApexV2.Data.MarketData;
using ApexV2.Core.Logging;

namespace ApexV2.Extensions.RealTime
{
    /// <summary>
    /// Central manager for all real-time data operations
    /// </summary>
    public class RealTimeManager : IDisposable
    {
        private readonly ILogger<RealTimeManager> _logger;
        private readonly RealTimeConfiguration _config;
        
        // Core services
        private readonly IRealTimeStreamingService _streamingService;
        private readonly IRealTimeSubscriptionManager _subscriptionManager;
        private readonly IRealTimeBroadcaster _broadcaster;
        private readonly IRealTimeAggregator _aggregator;
        private readonly IRealTimeAlertService _alertService;
        private readonly IRealTimeMetricsService _metricsService;
        
        private bool _disposed = false;
        private bool _isInitialized = false;

        public bool IsConnected => _streamingService?.IsConnected ?? false;
        public bool IsStreaming => _streamingService?.IsStreaming ?? false;
        public RealTimeMetrics CurrentMetrics => _metricsService?.GetCurrentMetrics();

        // Events
        public event EventHandler<RealTimeUpdate> DataUpdated;
        public event EventHandler<RealTimeErrorEventArgs> ErrorOccurred;
        public event EventHandler<RealTimeConnectionEventArgs> ConnectionStatusChanged;
        public event EventHandler<RealTimeAlertTriggeredEventArgs> AlertTriggered;
        public event EventHandler<RealTimeMetrics> MetricsUpdated;

        public RealTimeManager(
            ILogger<RealTimeManager> logger,
            RealTimeConfiguration config,
            IMarketDataProvider marketDataProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (marketDataProvider == null)
            {
                throw new ArgumentNullException(nameof(marketDataProvider));
            }

            try
            {
                // Create logger factory for dependencies
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

                // Initialize core services
                _broadcaster = new RealTimeBroadcaster(
                    loggerFactory.CreateLogger<RealTimeBroadcaster>(), 
                    _config);

                _streamingService = new RealTimeStreamingService(
                    loggerFactory.CreateLogger<RealTimeStreamingService>(),
                    _config,
                    marketDataProvider);

                _subscriptionManager = new RealTimeSubscriptionManager(
                    loggerFactory.CreateLogger<RealTimeSubscriptionManager>(),
                    _streamingService,
                    _broadcaster,
                    _config);

                _aggregator = new RealTimeAggregator(
                    loggerFactory.CreateLogger<RealTimeAggregator>(),
                    _config);

                _alertService = new RealTimeAlertService(
                    loggerFactory.CreateLogger<RealTimeAlertService>(),
                    _config);

                _metricsService = new RealTimeMetricsService(
                    loggerFactory.CreateLogger<RealTimeMetricsService>(),
                    _config);

                // Wire up event handlers
                SetupEventHandlers();

                _logger.LogInformation("Real-time manager created with provider: {Provider}", 
                    marketDataProvider.ProviderName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating real-time manager");
                throw;
            }
        }

        public async Task<bool> InitializeAsync()
        {
            if (_isInitialized)
            {
                _logger.LogInformation("Real-time manager already initialized");
                return true;
            }

            try
            {
                _logger.LogInformation("Initializing real-time manager...");

                // Connect to streaming service if auto-connect is enabled
                if (_config.AutoConnect)
                {
                    var connected = await _streamingService.ConnectAsync();
                    if (!connected)
                    {
                        _logger.LogWarning("Failed to auto-connect to streaming service");
                        return false;
                    }

                    // Start streaming if connected
                    var streamingStarted = await _streamingService.StartStreamingAsync();
                    if (!streamingStarted)
                    {
                        _logger.LogWarning("Failed to start streaming");
                        return false;
                    }
                }

                _isInitialized = true;
                _logger.LogInformation("Real-time manager initialized successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing real-time manager");
                return false;
            }
        }

        public async Task<bool> StartAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    var initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        return false;
                    }
                }

                _logger.LogInformation("Starting real-time data services...");

                // Connect if not already connected
                if (!_streamingService.IsConnected)
                {
                    var connected = await _streamingService.ConnectAsync();
                    if (!connected)
                    {
                        _logger.LogError("Failed to connect to streaming service");
                        return false;
                    }
                }

                // Start streaming if not already streaming
                if (!_streamingService.IsStreaming)
                {
                    var streamingStarted = await _streamingService.StartStreamingAsync();
                    if (!streamingStarted)
                    {
                        _logger.LogError("Failed to start streaming");
                        return false;
                    }
                }

                _logger.LogInformation("Real-time data services started successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting real-time services");
                return false;
            }
        }

        public async Task StopAsync()
        {
            try
            {
                _logger.LogInformation("Stopping real-time data services...");

                // Stop streaming
                if (_streamingService.IsStreaming)
                {
                    await _streamingService.StopStreamingAsync();
                }

                // Disconnect
                if (_streamingService.IsConnected)
                {
                    await _streamingService.DisconnectAsync();
                }

                _logger.LogInformation("Real-time data services stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping real-time services");
            }
        }

        // Subscription management
        public async Task<bool> SubscribeAsync(string symbol, RealTimeDataType dataType, Action<RealTimeUpdate> callback)
        {
            return await _subscriptionManager.SubscribeAsync(symbol, dataType, callback);
        }

        public async Task<bool> UnsubscribeAsync(string symbol, RealTimeDataType dataType)
        {
            return await _subscriptionManager.UnsubscribeAsync(symbol, dataType);
        }

        public async Task<bool> UnsubscribeAllAsync(string symbol)
        {
            return await _subscriptionManager.UnsubscribeAllAsync(symbol);
        }

        // Alert management
        public async Task<Guid> CreateAlertAsync(RealTimeAlert alert)
        {
            return await _alertService.CreateAlertAsync(alert);
        }

        public async Task<bool> UpdateAlertAsync(Guid alertId, RealTimeAlert alert)
        {
            return await _alertService.UpdateAlertAsync(alertId, alert);
        }

        public async Task<bool> DeleteAlertAsync(Guid alertId)
        {
            return await _alertService.DeleteAlertAsync(alertId);
        }

        // Broadcaster management
        public async Task RegisterListenerAsync(string listenerId, Action<RealTimeUpdate> callback)
        {
            await _broadcaster.RegisterListenerAsync(listenerId, callback);
        }

        public async Task UnregisterListenerAsync(string listenerId)
        {
            await _broadcaster.UnregisterListenerAsync(listenerId);
        }

        // Aggregation
        public async Task<RealTimeAggregateData> GetAggregateDataAsync(string symbol, TimeSpan interval)
        {
            return await _aggregator.GetAggregateDataAsync(symbol, interval);
        }

        // Metrics
        public async Task<RealTimeMetrics> GetMetricsAsync()
        {
            return await _metricsService.GetMetricsSnapshotAsync();
        }

        public async Task ResetMetricsAsync()
        {
            await _metricsService.ResetMetricsAsync();
        }

        private void SetupEventHandlers()
        {
            try
            {
                // Subscription manager events
                _subscriptionManager.DataUpdated += OnDataUpdated;
                _subscriptionManager.ErrorOccurred += OnErrorOccurred;
                _subscriptionManager.ConnectionStatusChanged += OnConnectionStatusChanged;

                // Alert service events
                _alertService.AlertTriggered += OnAlertTriggered;

                // Metrics service events
                _metricsService.MetricsUpdated += OnMetricsUpdated;

                _logger.LogDebug("Event handlers set up successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up event handlers");
            }
        }

        private void OnDataUpdated(object sender, RealTimeUpdate update)
        {
            try
            {
                // Record metrics
                if (update.Latency != default)
                {
                    _ = Task.Run(async () => await _metricsService.RecordUpdateAsync(
                        update.Symbol, update.DataType, update.Latency));
                }

                // Process for aggregation
                if (_config.EnableAggregation)
                {
                    _ = Task.Run(async () => await _aggregator.ProcessUpdateAsync(update));
                }

                // Process for alerts
                if (_config.EnableAlerts)
                {
                    _ = Task.Run(async () => await _alertService.ProcessUpdateForAlertsAsync(update));
                }

                // Forward to external listeners
                DataUpdated?.Invoke(this, update);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing data update for {Symbol} {DataType}", 
                    update?.Symbol, update?.DataType);
            }
        }

        private void OnErrorOccurred(object sender, RealTimeErrorEventArgs e)
        {
            try
            {
                // Record error metrics
                _ = Task.Run(async () => await _metricsService.RecordErrorAsync(
                    e.Symbol, e.DataType, e.ErrorMessage));

                // Forward to external listeners
                ErrorOccurred?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing error event");
            }
        }

        private void OnConnectionStatusChanged(object sender, RealTimeConnectionEventArgs e)
        {
            try
            {
                // Record connection metrics
                _ = Task.Run(async () => await _metricsService.RecordConnectionEventAsync(
                    e.IsConnected, e.Provider));

                // Forward to external listeners
                ConnectionStatusChanged?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing connection status change");
            }
        }

        private void OnAlertTriggered(object sender, RealTimeAlertTriggeredEventArgs e)
        {
            try
            {
                // Forward to external listeners
                AlertTriggered?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing alert trigger");
            }
        }

        private void OnMetricsUpdated(object sender, RealTimeMetrics metrics)
        {
            try
            {
                // Forward to external listeners
                MetricsUpdated?.Invoke(this, metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing metrics update");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Stop services
                _ = Task.Run(async () => await StopAsync());

                // Dispose services
                _subscriptionManager?.Dispose();
                _streamingService?.Dispose();
                _broadcaster?.Dispose();
                _aggregator?.Dispose();
                _alertService?.Dispose();
                _metricsService?.Dispose();

                _logger.LogInformation("Real-time manager disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time manager");
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Test logger implementation for services that need ILogger
    /// </summary>
    public class TestLogger<T> : ILogger<T>
    {
        public IDisposable BeginScope<TState>(TState state) => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            // Simple console output for testing
            Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
        }
    }
}
