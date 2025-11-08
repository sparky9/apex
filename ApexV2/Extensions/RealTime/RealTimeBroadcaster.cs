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
    /// Service for broadcasting real-time updates to multiple listeners
    /// </summary>
    public class RealTimeBroadcaster : IRealTimeBroadcaster, IDisposable
    {
        private readonly ILogger<RealTimeBroadcaster> _logger;
        private readonly RealTimeConfiguration _config;
        private readonly ConcurrentDictionary<string, Action<RealTimeUpdate>> _listeners = new();
        private readonly SemaphoreSlim _broadcastLock = new(1, 1);
        private bool _disposed = false;

        public RealTimeBroadcaster(ILogger<RealTimeBroadcaster> logger, RealTimeConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            
            _logger.LogInformation("Real-time broadcaster initialized");
        }

        public async Task BroadcastUpdateAsync(RealTimeUpdate update)
        {
            if (update == null || !update.IsValid())
            {
                _logger.LogWarning("Attempted to broadcast invalid update");
                return;
            }

            try
            {
                var listeners = _listeners.Values.ToList();
                if (!listeners.Any())
                {
                    if (_config.LogRealTimeEvents)
                    {
                        _logger.LogTrace("No listeners registered for broadcast");
                    }
                    return;
                }

                // Broadcast to all listeners in parallel
                var tasks = listeners.Select(listener => Task.Run(() =>
                {
                    try
                    {
                        listener(update);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in listener callback for {Symbol} {DataType}", 
                            update.Symbol, update.DataType);
                    }
                })).ToArray();

                await Task.WhenAll(tasks);

                if (_config.LogRealTimeEvents)
                {
                    _logger.LogTrace("Broadcasted update to {ListenerCount} listeners for {Symbol} {DataType}", 
                        listeners.Count, update.Symbol, update.DataType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting update for {Symbol} {DataType}", 
                    update.Symbol, update.DataType);
            }
        }

        public async Task BroadcastErrorAsync(RealTimeErrorEventArgs error)
        {
            if (error == null)
            {
                _logger.LogWarning("Attempted to broadcast null error");
                return;
            }

            try
            {
                var listeners = _listeners.Values.ToList();
                if (!listeners.Any())
                {
                    _logger.LogTrace("No listeners registered for error broadcast");
                    return;
                }

                // Create error update for broadcasting
                var errorUpdate = new RealTimeUpdate
                {
                    Symbol = error.Symbol ?? "SYSTEM",
                    DataType = error.DataType,
                    Timestamp = error.Timestamp,
                    Priority = error.IsCritical ? RealTimePriority.Critical : RealTimePriority.High,
                    Source = error.Source ?? "RealTimeBroadcaster",
                    Data = new Dictionary<string, object>
                    {
                        ["IsError"] = true,
                        ["ErrorMessage"] = error.ErrorMessage,
                        ["IsCritical"] = error.IsCritical
                    }
                };

                await BroadcastUpdateAsync(errorUpdate);

                _logger.LogDebug("Broadcasted error to {ListenerCount} listeners: {ErrorMessage}", 
                    listeners.Count, error.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting error message: {ErrorMessage}", error.ErrorMessage);
            }
        }

        public async Task BroadcastConnectionStatusAsync(bool isConnected, string provider)
        {
            try
            {
                var listeners = _listeners.Values.ToList();
                if (!listeners.Any())
                {
                    _logger.LogTrace("No listeners registered for connection status broadcast");
                    return;
                }

                // Create connection status update for broadcasting
                var statusUpdate = new RealTimeUpdate
                {
                    Symbol = "SYSTEM",
                    DataType = RealTimeDataType.Quote, // Generic type for system messages
                    Timestamp = DateTime.UtcNow,
                    Priority = RealTimePriority.High,
                    Source = provider ?? "Unknown",
                    Data = new Dictionary<string, object>
                    {
                        ["IsConnectionStatus"] = true,
                        ["IsConnected"] = isConnected,
                        ["Provider"] = provider
                    }
                };

                await BroadcastUpdateAsync(statusUpdate);

                _logger.LogDebug("Broadcasted connection status to {ListenerCount} listeners: {Status} ({Provider})", 
                    listeners.Count, isConnected ? "Connected" : "Disconnected", provider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting connection status: {Status} ({Provider})", 
                    isConnected ? "Connected" : "Disconnected", provider);
            }
        }

        public async Task RegisterListenerAsync(string listenerId, Action<RealTimeUpdate> callback)
        {
            if (string.IsNullOrEmpty(listenerId) || callback == null)
            {
                _logger.LogWarning("Invalid listener registration: id={ListenerId}, callback={Callback}", 
                    listenerId, callback != null ? "provided" : "null");
                return;
            }

            try
            {
                await _broadcastLock.WaitAsync();

                _listeners.AddOrUpdate(listenerId, callback, (key, oldValue) => callback);

                _logger.LogDebug("Registered listener {ListenerId} - Total listeners: {ListenerCount}", 
                    listenerId, _listeners.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering listener {ListenerId}", listenerId);
            }
            finally
            {
                _broadcastLock.Release();
            }
        }

        public async Task UnregisterListenerAsync(string listenerId)
        {
            if (string.IsNullOrEmpty(listenerId))
            {
                _logger.LogWarning("Invalid listener unregistration: id={ListenerId}", listenerId);
                return;
            }

            try
            {
                await _broadcastLock.WaitAsync();

                var removed = _listeners.TryRemove(listenerId, out _);

                _logger.LogDebug("Unregistered listener {ListenerId} - Removed: {Removed}, Total listeners: {ListenerCount}", 
                    listenerId, removed, _listeners.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unregistering listener {ListenerId}", listenerId);
            }
            finally
            {
                _broadcastLock.Release();
            }
        }

        public async Task<List<string>> GetActiveListenersAsync()
        {
            await Task.Yield(); // Make it properly async
            return _listeners.Keys.ToList();
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _listeners.Clear();
                _broadcastLock?.Dispose();

                _logger.LogInformation("Real-time broadcaster disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time broadcaster");
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Service for aggregating real-time data into time intervals
    /// </summary>
    public class RealTimeAggregator : IRealTimeAggregator, IDisposable
    {
        private readonly ILogger<RealTimeAggregator> _logger;
        private readonly RealTimeConfiguration _config;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<TimeSpan, RealTimeAggregateData>> _aggregates = new();
        private readonly Timer _flushTimer;
        private bool _disposed = false;

        public event EventHandler<RealTimeAggregateData> AggregateUpdated;

        public RealTimeAggregator(ILogger<RealTimeAggregator> logger, RealTimeConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Timer to flush completed intervals
            _flushTimer = new Timer(FlushCompletedIntervals, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            _logger.LogInformation("Real-time aggregator initialized with {IntervalCount} intervals", 
                _config.AggregationIntervals?.Count ?? 0);
        }

        public async Task ProcessUpdateAsync(RealTimeUpdate update)
        {
            if (!_config.EnableAggregation || update == null || !update.IsValid())
            {
                return;
            }

            if (!update.Price.HasValue && !update.Volume.HasValue)
            {
                return; // Nothing to aggregate
            }

            try
            {
                var symbol = update.Symbol.ToUpperInvariant();
                var symbolAggregates = _aggregates.GetOrAdd(symbol, 
                    _ => new ConcurrentDictionary<TimeSpan, RealTimeAggregateData>());

                foreach (var interval in _config.AggregationIntervals)
                {
                    var intervalStart = FloorToInterval(update.Timestamp, interval);
                    var intervalEnd = intervalStart.Add(interval);

                    var aggregate = symbolAggregates.AddOrUpdate(interval, 
                        _ => CreateNewAggregate(symbol, intervalStart, intervalEnd, interval, update),
                        (_, existing) => UpdateExistingAggregate(existing, update));

                    // Notify if aggregate was updated
                    if (aggregate.LastUpdated <= update.Timestamp)
                    {
                        aggregate.LastUpdated = update.Timestamp;
                        AggregateUpdated?.Invoke(this, aggregate);
                    }
                }

                if (_config.LogRealTimeEvents)
                {
                    _logger.LogTrace("Processed aggregate update for {Symbol} - Price: {Price}, Volume: {Volume}", 
                        symbol, update.Price, update.Volume);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing update for aggregation: {Symbol} {DataType}", 
                    update.Symbol, update.DataType);
            }
        }

        public async Task<RealTimeAggregateData> GetAggregateDataAsync(string symbol, TimeSpan interval)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                return null;
            }

            await Task.Yield(); // Make it properly async

            symbol = symbol.ToUpperInvariant();
            
            if (_aggregates.TryGetValue(symbol, out var symbolAggregates) &&
                symbolAggregates.TryGetValue(interval, out var aggregate))
            {
                return aggregate;
            }

            return null;
        }

        public async Task<List<RealTimeAggregateData>> GetAggregateDataRangeAsync(string symbol, DateTime startTime, DateTime endTime, TimeSpan interval)
        {
            if (string.IsNullOrEmpty(symbol) || endTime <= startTime)
            {
                return new List<RealTimeAggregateData>();
            }

            await Task.Yield(); // Make it properly async

            symbol = symbol.ToUpperInvariant();
            var result = new List<RealTimeAggregateData>();

            if (!_aggregates.TryGetValue(symbol, out var symbolAggregates))
            {
                return result;
            }

            // For simplicity, return current aggregate if it matches the interval
            // A full implementation would store historical aggregates
            if (symbolAggregates.TryGetValue(interval, out var aggregate) &&
                aggregate.IntervalStart >= startTime && aggregate.IntervalEnd <= endTime)
            {
                result.Add(aggregate);
            }

            return result;
        }

        private RealTimeAggregateData CreateNewAggregate(string symbol, DateTime intervalStart, DateTime intervalEnd, TimeSpan interval, RealTimeUpdate update)
        {
            return new RealTimeAggregateData
            {
                Symbol = symbol,
                IntervalStart = intervalStart,
                IntervalEnd = intervalEnd,
                Interval = interval,
                Open = update.Price ?? 0,
                High = update.Price ?? 0,
                Low = update.Price ?? 0,
                Close = update.Price ?? 0,
                Volume = update.Volume ?? 0,
                TradeCount = 1,
                VWAP = update.Price ?? 0,
                TWAP = update.Price ?? 0,
                LastUpdated = update.Timestamp,
                IsComplete = false
            };
        }

        private RealTimeAggregateData UpdateExistingAggregate(RealTimeAggregateData existing, RealTimeUpdate update)
        {
            if (update.Price.HasValue)
            {
                existing.High = Math.Max(existing.High, update.Price.Value);
                existing.Low = Math.Min(existing.Low, update.Price.Value);
                existing.Close = update.Price.Value;

                // Update VWAP and TWAP (simplified calculation)
                var totalVolume = existing.Volume + (update.Volume ?? 0);
                if (totalVolume > 0)
                {
                    existing.VWAP = ((existing.VWAP * existing.Volume) + ((update.Price.Value) * (update.Volume ?? 0))) / totalVolume;
                }
                existing.TWAP = (existing.TWAP + update.Price.Value) / 2; // Simplified
            }

            if (update.Volume.HasValue)
            {
                existing.Volume += update.Volume.Value;
            }

            existing.TradeCount++;
            existing.LastUpdated = update.Timestamp;

            return existing;
        }

        private DateTime FloorToInterval(DateTime timestamp, TimeSpan interval)
        {
            var ticks = timestamp.Ticks;
            var intervalTicks = interval.Ticks;
            var flooredTicks = (ticks / intervalTicks) * intervalTicks;
            return new DateTime(flooredTicks);
        }

        private void FlushCompletedIntervals(object state)
        {
            try
            {
                var now = DateTime.UtcNow;

                foreach (var symbolKvp in _aggregates)
                {
                    var symbolAggregates = symbolKvp.Value;
                    var completedIntervals = new List<TimeSpan>();

                    foreach (var intervalKvp in symbolAggregates)
                    {
                        var interval = intervalKvp.Key;
                        var aggregate = intervalKvp.Value;

                        // Mark as complete if interval has passed
                        if (now >= aggregate.IntervalEnd && !aggregate.IsComplete)
                        {
                            aggregate.IsComplete = true;
                            AggregateUpdated?.Invoke(this, aggregate);
                            
                            // Remove old completed intervals (keep last few for queries)
                            if (now.Subtract(aggregate.IntervalEnd).TotalMinutes > 60)
                            {
                                completedIntervals.Add(interval);
                            }
                        }
                    }

                    // Clean up old intervals
                    foreach (var interval in completedIntervals)
                    {
                        symbolAggregates.TryRemove(interval, out _);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error flushing completed intervals");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _flushTimer?.Dispose();
                _aggregates.Clear();

                _logger.LogInformation("Real-time aggregator disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time aggregator");
            }

            _disposed = true;
        }
    }
}
