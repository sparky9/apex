#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApexV2.Extensions.RealTime
{
    /// <summary>
    /// Service for collecting and reporting real-time system metrics
    /// </summary>
    public class RealTimeMetricsService : IRealTimeMetricsService, IDisposable
    {
        private readonly ILogger<RealTimeMetricsService> _logger;
        private readonly RealTimeConfiguration _config;
        private readonly Timer _metricsTimer;
        private readonly object _metricsLock = new object();
        private bool _disposed = false;

        // Metrics data
        private RealTimeMetrics _currentMetrics = new();
        private readonly ConcurrentQueue<DateTime> _updateTimestamps = new();
        private readonly ConcurrentQueue<TimeSpan> _latencyMeasurements = new();
        private readonly ConcurrentQueue<DateTime> _errorTimestamps = new();
        private readonly ConcurrentDictionary<string, int> _symbolUpdateCounts = new();
        private readonly ConcurrentDictionary<RealTimeDataType, int> _dataTypeUpdateCounts = new();
        
        // Connection tracking
        private DateTime? _connectionStartTime;
        private bool _isConnected = false;
        private string _connectedProvider = "";

        public event EventHandler<RealTimeMetrics> MetricsUpdated;

        public RealTimeMetricsService(ILogger<RealTimeMetricsService> logger, RealTimeConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Timer for periodic metrics updates
            if (_config.EnableMetrics)
            {
                var interval = TimeSpan.FromSeconds(_config.MetricsUpdateIntervalSeconds);
                _metricsTimer = new Timer(UpdateMetrics, null, interval, interval);
            }

            _logger.LogInformation("Real-time metrics service initialized with {IntervalSeconds}s update interval", 
                _config.MetricsUpdateIntervalSeconds);
        }

        public RealTimeMetrics GetCurrentMetrics()
        {
            lock (_metricsLock)
            {
                return CloneMetrics(_currentMetrics);
            }
        }

        public async Task<RealTimeMetrics> GetMetricsSnapshotAsync()
        {
            await Task.Yield(); // Make it properly async
            return GetCurrentMetrics();
        }

        public async Task ResetMetricsAsync()
        {
            try
            {
                lock (_metricsLock)
                {
                    _currentMetrics = new RealTimeMetrics
                    {
                        IsConnected = _isConnected,
                        ConnectedProvider = _connectedProvider,
                        LastConnectionTime = _connectionStartTime
                    };
                }

                // Clear queues
                while (_updateTimestamps.TryDequeue(out _)) { }
                while (_latencyMeasurements.TryDequeue(out _)) { }
                while (_errorTimestamps.TryDequeue(out _)) { }
                
                _symbolUpdateCounts.Clear();
                _dataTypeUpdateCounts.Clear();

                _logger.LogInformation("Real-time metrics reset");
                await Task.Yield();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting metrics");
            }
        }

        public async Task RecordUpdateAsync(string symbol, RealTimeDataType dataType, TimeSpan latency)
        {
            if (!_config.EnableMetrics) return;

            try
            {
                var now = DateTime.UtcNow;
                
                // Record update timestamp
                _updateTimestamps.Enqueue(now);
                
                // Record latency
                _latencyMeasurements.Enqueue(latency);
                
                // Update symbol and data type counters
                symbol = symbol?.ToUpperInvariant() ?? "UNKNOWN";
                _symbolUpdateCounts.AddOrUpdate(symbol, 1, (key, value) => value + 1);
                _dataTypeUpdateCounts.AddOrUpdate(dataType, 1, (key, value) => value + 1);

                // Clean old timestamps (keep last hour)
                var cutoff = now.AddHours(-1);
                while (_updateTimestamps.TryPeek(out var timestamp) && timestamp < cutoff)
                {
                    _updateTimestamps.TryDequeue(out _);
                }

                // Clean old latency measurements (keep last 1000)
                while (_latencyMeasurements.Count > 1000)
                {
                    _latencyMeasurements.TryDequeue(out _);
                }

                if (_config.LogRealTimeEvents)
                {
                    _logger.LogTrace("Recorded update metric: {Symbol} {DataType} - Latency: {Latency}ms", 
                        symbol, dataType, latency.TotalMilliseconds);
                }

                await Task.Yield();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording update metric for {Symbol} {DataType}", symbol, dataType);
            }
        }

        public async Task RecordErrorAsync(string symbol, RealTimeDataType dataType, string error)
        {
            if (!_config.EnableMetrics) return;

            try
            {
                var now = DateTime.UtcNow;
                
                // Record error timestamp
                _errorTimestamps.Enqueue(now);

                lock (_metricsLock)
                {
                    _currentMetrics.TotalErrors++;
                    _currentMetrics.LastErrorTime = now;
                    _currentMetrics.LastErrorMessage = error ?? "Unknown error";
                }

                // Clean old error timestamps (keep last hour)
                var cutoff = now.AddHours(-1);
                while (_errorTimestamps.TryPeek(out var timestamp) && timestamp < cutoff)
                {
                    _errorTimestamps.TryDequeue(out _);
                }

                _logger.LogWarning("Recorded error metric: {Symbol} {DataType} - {Error}", 
                    symbol, dataType, error);

                await Task.Yield();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording error metric for {Symbol} {DataType}: {Error}", 
                    symbol, dataType, error);
            }
        }

        public async Task RecordConnectionEventAsync(bool connected, string provider)
        {
            if (!_config.EnableMetrics) return;

            try
            {
                var now = DateTime.UtcNow;

                lock (_metricsLock)
                {
                    if (connected && !_isConnected)
                    {
                        // Connection established
                        _connectionStartTime = now;
                        _currentMetrics.LastConnectionTime = now;
                        _currentMetrics.ReconnectionAttempts = 0;
                    }
                    else if (!connected && _isConnected)
                    {
                        // Connection lost
                        _connectionStartTime = null;
                        _currentMetrics.ReconnectionAttempts++;
                    }

                    _isConnected = connected;
                    _connectedProvider = provider ?? "";
                    _currentMetrics.IsConnected = connected;
                    _currentMetrics.ConnectedProvider = _connectedProvider;
                    
                    if (connected && _connectionStartTime.HasValue)
                    {
                        _currentMetrics.ConnectionUptime = now - _connectionStartTime.Value;
                    }
                    else
                    {
                        _currentMetrics.ConnectionUptime = null;
                    }
                }

                _logger.LogInformation("Recorded connection event: {Status} ({Provider})", 
                    connected ? "Connected" : "Disconnected", provider);

                await Task.Yield();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording connection event: {Status} ({Provider})", 
                    connected ? "Connected" : "Disconnected", provider);
            }
        }

        private void UpdateMetrics(object state)
        {
            try
            {
                if (!_config.EnableMetrics) return;

                var now = DateTime.UtcNow;
                
                lock (_metricsLock)
                {
                    _currentMetrics.Timestamp = now;

                    // Calculate update rates
                    var updateTimestamps = _updateTimestamps.ToArray();
                    _currentMetrics.TotalUpdatesReceived = updateTimestamps.Length;

                    var lastMinute = now.AddMinutes(-1);
                    var lastHour = now.AddHours(-1);
                    
                    _currentMetrics.UpdatesInLastMinute = updateTimestamps.Count(t => t >= lastMinute);
                    _currentMetrics.UpdatesInLastHour = updateTimestamps.Count(t => t >= lastHour);
                    
                    // Calculate updates per second (average over last minute)
                    if (_currentMetrics.UpdatesInLastMinute > 0)
                    {
                        _currentMetrics.UpdatesPerSecond = _currentMetrics.UpdatesInLastMinute / 60;
                    }

                    // Calculate latency metrics
                    var latencies = _latencyMeasurements.ToArray();
                    if (latencies.Any())
                    {
                        _currentMetrics.AverageLatency = TimeSpan.FromTicks((long)latencies.Average(l => l.Ticks));
                        _currentMetrics.MinLatency = TimeSpan.FromTicks(latencies.Min(l => l.Ticks));
                        _currentMetrics.MaxLatency = TimeSpan.FromTicks(latencies.Max(l => l.Ticks));
                        _currentMetrics.LastUpdateLatency = latencies.LastOrDefault();
                    }

                    // Calculate error rates
                    var errorTimestamps = _errorTimestamps.ToArray();
                    _currentMetrics.ErrorsInLastMinute = errorTimestamps.Count(t => t >= lastMinute);
                    _currentMetrics.ErrorsInLastHour = errorTimestamps.Count(t => t >= lastHour);

                    // Update subscription metrics
                    _currentMetrics.ActiveSubscriptions = _symbolUpdateCounts.Count;
                    _currentMetrics.SubscriptionsBySymbol = new Dictionary<string, int>(_symbolUpdateCounts);
                    _currentMetrics.SubscriptionsByType = new Dictionary<RealTimeDataType, int>(_dataTypeUpdateCounts);

                    // Calculate data quality score
                    CalculateDataQualityScore();

                    // Update system metrics
                    UpdateSystemMetrics();

                    // Update connection uptime
                    if (_isConnected && _connectionStartTime.HasValue)
                    {
                        _currentMetrics.ConnectionUptime = now - _connectionStartTime.Value;
                    }
                }

                // Notify listeners
                var metricsSnapshot = GetCurrentMetrics();
                MetricsUpdated?.Invoke(this, metricsSnapshot);

                if (_config.LogRealTimeEvents)
                {
                    _logger.LogTrace("Updated metrics - Updates/min: {UpdatesPerMinute}, Avg Latency: {AvgLatency}ms, Errors/min: {ErrorsPerMinute}", 
                        _currentMetrics.UpdatesInLastMinute, 
                        _currentMetrics.AverageLatency.TotalMilliseconds,
                        _currentMetrics.ErrorsInLastMinute);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating metrics");
            }
        }

        private void CalculateDataQualityScore()
        {
            try
            {
                var score = 100.0;
                
                // Reduce score based on error rate
                var totalUpdates = _currentMetrics.UpdatesInLastHour;
                var totalErrors = _currentMetrics.ErrorsInLastHour;
                
                if (totalUpdates > 0)
                {
                    var errorRate = (double)totalErrors / totalUpdates;
                    score -= (errorRate * 50); // Reduce up to 50 points for high error rate
                }

                // Reduce score based on high latency
                if (_currentMetrics.AverageLatency.TotalSeconds > 1.0)
                {
                    score -= Math.Min(30, _currentMetrics.AverageLatency.TotalSeconds * 5); // Reduce up to 30 points for high latency
                }

                // Reduce score if not connected
                if (!_currentMetrics.IsConnected)
                {
                    score -= 50;
                }

                _currentMetrics.DataQualityScore = Math.Max(0, score);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating data quality score");
                _currentMetrics.DataQualityScore = 0;
            }
        }

        private void UpdateSystemMetrics()
        {
            try
            {
                // Get current process
                using var process = Process.GetCurrentProcess();
                
                // Memory usage
                _currentMetrics.MemoryUsageBytes = process.WorkingSet64;
                
                // CPU usage (simplified - would need more sophisticated calculation for accuracy)
                _currentMetrics.CpuUsagePercent = 0; // Placeholder
                
                // Network metrics (would need to integrate with network monitoring)
                _currentMetrics.NetworkBytesReceived = 0; // Placeholder
                _currentMetrics.NetworkBytesSent = 0; // Placeholder
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating system metrics");
            }
        }

        private RealTimeMetrics CloneMetrics(RealTimeMetrics source)
        {
            return new RealTimeMetrics
            {
                Timestamp = source.Timestamp,
                IsConnected = source.IsConnected,
                ConnectedProvider = source.ConnectedProvider,
                LastConnectionTime = source.LastConnectionTime,
                ConnectionUptime = source.ConnectionUptime,
                ReconnectionAttempts = source.ReconnectionAttempts,
                TotalUpdatesReceived = source.TotalUpdatesReceived,
                UpdatesPerSecond = source.UpdatesPerSecond,
                UpdatesInLastMinute = source.UpdatesInLastMinute,
                UpdatesInLastHour = source.UpdatesInLastHour,
                AverageLatency = source.AverageLatency,
                MinLatency = source.MinLatency,
                MaxLatency = source.MaxLatency,
                LastUpdateLatency = source.LastUpdateLatency,
                TotalErrors = source.TotalErrors,
                ErrorsInLastMinute = source.ErrorsInLastMinute,
                ErrorsInLastHour = source.ErrorsInLastHour,
                LastErrorTime = source.LastErrorTime,
                LastErrorMessage = source.LastErrorMessage,
                ActiveSubscriptions = source.ActiveSubscriptions,
                SubscriptionsBySymbol = new Dictionary<string, int>(source.SubscriptionsBySymbol),
                SubscriptionsByType = new Dictionary<RealTimeDataType, int>(source.SubscriptionsByType),
                DataQualityScore = source.DataQualityScore,
                MissedUpdates = source.MissedUpdates,
                DuplicateUpdates = source.DuplicateUpdates,
                OutOfOrderUpdates = source.OutOfOrderUpdates,
                MemoryUsageBytes = source.MemoryUsageBytes,
                CpuUsagePercent = source.CpuUsagePercent,
                NetworkBytesReceived = source.NetworkBytesReceived,
                NetworkBytesSent = source.NetworkBytesSent
            };
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _metricsTimer?.Dispose();

                // Clear queues
                while (_updateTimestamps.TryDequeue(out _)) { }
                while (_latencyMeasurements.TryDequeue(out _)) { }
                while (_errorTimestamps.TryDequeue(out _)) { }
                
                _symbolUpdateCounts.Clear();
                _dataTypeUpdateCounts.Clear();

                _logger.LogInformation("Real-time metrics service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time metrics service");
            }

            _disposed = true;
        }
    }
}
