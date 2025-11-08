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
    /// Central manager for real-time data subscriptions and updates
    /// </summary>
    public class RealTimeSubscriptionManager : IRealTimeSubscriptionManager, IDisposable
    {
        private readonly ILogger<RealTimeSubscriptionManager> _logger;
        private readonly IRealTimeStreamingService _streamingService;
        private readonly IRealTimeBroadcaster _broadcaster;
        private readonly RealTimeConfiguration _config;
        
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<RealTimeDataType, List<Action<RealTimeUpdate>>>> _subscriptions = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastUpdateTimes = new();
        private readonly SemaphoreSlim _subscriptionLock = new(1, 1);
        private bool _disposed = false;

        public event EventHandler<RealTimeUpdate> DataUpdated;
        public event EventHandler<RealTimeErrorEventArgs> ErrorOccurred;
        public event EventHandler<RealTimeConnectionEventArgs> ConnectionStatusChanged;

        public RealTimeSubscriptionManager(
            ILogger<RealTimeSubscriptionManager> logger,
            IRealTimeStreamingService streamingService,
            IRealTimeBroadcaster broadcaster,
            RealTimeConfiguration config)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _streamingService = streamingService ?? throw new ArgumentNullException(nameof(streamingService));
            _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            // Subscribe to streaming service events
            _streamingService.DataReceived += OnDataReceived;
            _streamingService.StreamError += OnStreamError;
            _streamingService.ConnectionChanged += OnConnectionChanged;

            _logger.LogInformation("Real-time subscription manager initialized");
        }

        public async Task<bool> SubscribeAsync(string symbol, RealTimeDataType dataType, Action<RealTimeUpdate> callback)
        {
            if (string.IsNullOrEmpty(symbol) || callback == null)
            {
                _logger.LogWarning("Invalid subscription parameters: symbol={Symbol}, callback={Callback}", 
                    symbol, callback != null ? "provided" : "null");
                return false;
            }

            try
            {
                await _subscriptionLock.WaitAsync();

                symbol = symbol.ToUpperInvariant();
                
                // Get or create symbol subscriptions
                var symbolSubscriptions = _subscriptions.GetOrAdd(symbol, 
                    _ => new ConcurrentDictionary<RealTimeDataType, List<Action<RealTimeUpdate>>>());
                
                // Get or create data type callbacks
                var callbacks = symbolSubscriptions.GetOrAdd(dataType, _ => new List<Action<RealTimeUpdate>>());
                
                // Add callback if not already present
                lock (callbacks)
                {
                    if (!callbacks.Contains(callback))
                    {
                        callbacks.Add(callback);
                    }
                }

                // Subscribe to streaming service if this is the first subscription for this symbol/type
                if (callbacks.Count == 1)
                {
                    var success = await _streamingService.AddSymbolAsync(symbol, dataType);
                    if (!success)
                    {
                        _logger.LogError("Failed to add symbol {Symbol} for {DataType} to streaming service", symbol, dataType);
                        lock (callbacks)
                        {
                            callbacks.Remove(callback);
                        }
                        return false;
                    }
                }

                _logger.LogDebug("Subscribed to {Symbol} {DataType} - {CallbackCount} callbacks", 
                    symbol, dataType, callbacks.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to {Symbol} {DataType}", symbol, dataType);
                return false;
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        public async Task<bool> UnsubscribeAsync(string symbol, RealTimeDataType dataType)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                _logger.LogWarning("Invalid unsubscribe parameters: symbol={Symbol}", symbol);
                return false;
            }

            try
            {
                await _subscriptionLock.WaitAsync();

                symbol = symbol.ToUpperInvariant();

                if (!_subscriptions.TryGetValue(symbol, out var symbolSubscriptions))
                {
                    _logger.LogDebug("No subscriptions found for symbol {Symbol}", symbol);
                    return true; // Already unsubscribed
                }

                if (!symbolSubscriptions.TryRemove(dataType, out var callbacks))
                {
                    _logger.LogDebug("No subscription found for {Symbol} {DataType}", symbol, dataType);
                    return true; // Already unsubscribed
                }

                // Remove from streaming service
                var success = await _streamingService.RemoveSymbolAsync(symbol, dataType);
                if (!success)
                {
                    _logger.LogWarning("Failed to remove {Symbol} {DataType} from streaming service", symbol, dataType);
                }

                // Clean up empty symbol entry
                if (symbolSubscriptions.IsEmpty)
                {
                    _subscriptions.TryRemove(symbol, out _);
                    _lastUpdateTimes.TryRemove(symbol, out _);
                }

                _logger.LogDebug("Unsubscribed from {Symbol} {DataType} - removed {CallbackCount} callbacks", 
                    symbol, dataType, callbacks?.Count ?? 0);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from {Symbol} {DataType}", symbol, dataType);
                return false;
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        public async Task<bool> UnsubscribeAllAsync(string symbol)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                _logger.LogWarning("Invalid unsubscribe all parameters: symbol={Symbol}", symbol);
                return false;
            }

            try
            {
                await _subscriptionLock.WaitAsync();

                symbol = symbol.ToUpperInvariant();

                if (!_subscriptions.TryRemove(symbol, out var symbolSubscriptions))
                {
                    _logger.LogDebug("No subscriptions found for symbol {Symbol}", symbol);
                    return true; // Already unsubscribed
                }

                // Remove all data types for this symbol from streaming service
                var tasks = symbolSubscriptions.Keys.Select(dataType => 
                    _streamingService.RemoveSymbolAsync(symbol, dataType));
                
                await Task.WhenAll(tasks);

                _lastUpdateTimes.TryRemove(symbol, out _);

                _logger.LogDebug("Unsubscribed all data types for {Symbol} - removed {SubscriptionCount} subscriptions", 
                    symbol, symbolSubscriptions.Count);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing all for symbol {Symbol}", symbol);
                return false;
            }
            finally
            {
                _subscriptionLock.Release();
            }
        }

        public async Task<bool> IsSubscribedAsync(string symbol, RealTimeDataType dataType)
        {
            if (string.IsNullOrEmpty(symbol)) return false;

            await Task.Yield(); // Make it properly async
            
            symbol = symbol.ToUpperInvariant();
            return _subscriptions.TryGetValue(symbol, out var symbolSubscriptions) &&
                   symbolSubscriptions.ContainsKey(dataType);
        }

        public async Task<List<string>> GetSubscribedSymbolsAsync()
        {
            await Task.Yield(); // Make it properly async
            return _subscriptions.Keys.ToList();
        }

        public async Task<Dictionary<string, List<RealTimeDataType>>> GetAllSubscriptionsAsync()
        {
            await Task.Yield(); // Make it properly async
            
            var result = new Dictionary<string, List<RealTimeDataType>>();
            
            foreach (var kvp in _subscriptions)
            {
                result[kvp.Key] = kvp.Value.Keys.ToList();
            }
            
            return result;
        }

        private void OnDataReceived(object sender, RealTimeUpdate update)
        {
            try
            {
                if (update == null || !update.IsValid())
                {
                    _logger.LogWarning("Received invalid real-time update");
                    return;
                }

                var symbol = update.Symbol.ToUpperInvariant();
                _lastUpdateTimes[symbol] = DateTime.UtcNow;

                // Get callbacks for this symbol and data type
                if (_subscriptions.TryGetValue(symbol, out var symbolSubscriptions) &&
                    symbolSubscriptions.TryGetValue(update.DataType, out var callbacks))
                {
                    // Execute callbacks in parallel for better performance
                    var tasks = new List<Task>();
                    
                    List<Action<RealTimeUpdate>> callbacksCopy;
                    lock (callbacks)
                    {
                        callbacksCopy = new List<Action<RealTimeUpdate>>(callbacks);
                    }

                    foreach (var callback in callbacksCopy)
                    {
                        tasks.Add(Task.Run(() =>
                        {
                            try
                            {
                                callback(update);
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error executing callback for {Symbol} {DataType}", 
                                    symbol, update.DataType);
                            }
                        }));
                    }

                    // Don't wait for callbacks to complete - fire and forget for performance
                    _ = Task.WhenAll(tasks);
                }

                // Broadcast to all listeners
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _broadcaster.BroadcastUpdateAsync(update);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error broadcasting update for {Symbol}", symbol);
                    }
                });

                // Raise event for direct subscribers
                DataUpdated?.Invoke(this, update);

                if (_config.LogRealTimeEvents)
                {
                    _logger.LogTrace("Processed update for {Symbol} {DataType} - Price: {Price}, Volume: {Volume}", 
                        symbol, update.DataType, update.Price, update.Volume);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing real-time update");
            }
        }

        private void OnStreamError(object sender, RealTimeErrorEventArgs e)
        {
            try
            {
                _logger.LogError("Real-time stream error for {Symbol} {DataType}: {Error}", 
                    e.Symbol, e.DataType, e.ErrorMessage);

                // Broadcast error
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _broadcaster.BroadcastErrorAsync(e);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error broadcasting stream error");
                    }
                });

                // Raise event
                ErrorOccurred?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling stream error");
            }
        }

        private void OnConnectionChanged(object sender, bool isConnected)
        {
            try
            {
                var eventArgs = new RealTimeConnectionEventArgs
                {
                    IsConnected = isConnected,
                    Provider = _config.PrimaryProvider,
                    Reason = isConnected ? "Connected" : "Disconnected",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation("Real-time connection status changed: {Status} ({Provider})", 
                    isConnected ? "Connected" : "Disconnected", eventArgs.Provider);

                // Broadcast connection status
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _broadcaster.BroadcastConnectionStatusAsync(isConnected, eventArgs.Provider);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error broadcasting connection status");
                    }
                });

                // Raise event
                ConnectionStatusChanged?.Invoke(this, eventArgs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling connection status change");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Unsubscribe from streaming service events
                if (_streamingService != null)
                {
                    _streamingService.DataReceived -= OnDataReceived;
                    _streamingService.StreamError -= OnStreamError;
                    _streamingService.ConnectionChanged -= OnConnectionChanged;
                }

                // Clear all subscriptions
                _subscriptions.Clear();
                _lastUpdateTimes.Clear();

                _subscriptionLock?.Dispose();

                _logger.LogInformation("Real-time subscription manager disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time subscription manager");
            }

            _disposed = true;
        }
    }
}
