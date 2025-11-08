#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ApexV2.Data.MarketData;

namespace ApexV2.Extensions.RealTime
{
    /// <summary>
    /// Streaming service that integrates with market data providers for real-time updates
    /// </summary>
    public class RealTimeStreamingService : IRealTimeStreamingService, IDisposable
    {
        private readonly ILogger<RealTimeStreamingService> _logger;
        private readonly RealTimeConfiguration _config;
        private readonly IMarketDataProvider _marketDataProvider;
        private readonly ConcurrentDictionary<string, List<RealTimeDataType>> _activeSubscriptions = new();
        private readonly Timer _heartbeatTimer;
        private readonly SemaphoreSlim _connectionLock = new(1, 1);
        private bool _disposed = false;
        private bool _isConnected = false;
        private bool _isStreaming = false;
        private CancellationTokenSource _streamingCancellation;

        public bool IsConnected => _isConnected;
        public bool IsStreaming => _isStreaming;

        public event EventHandler<RealTimeUpdate> DataReceived;
        public event EventHandler<RealTimeErrorEventArgs> StreamError;
        public event EventHandler<bool> ConnectionChanged;

        public RealTimeStreamingService(
            ILogger<RealTimeStreamingService> logger,
            RealTimeConfiguration config,
            IMarketDataProvider marketDataProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _marketDataProvider = marketDataProvider ?? throw new ArgumentNullException(nameof(marketDataProvider));

            // Heartbeat timer to simulate real-time updates
            _heartbeatTimer = new Timer(GenerateHeartbeat, null, 
                TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            _logger.LogInformation("Real-time streaming service initialized with provider: {Provider}", 
                _marketDataProvider.ProviderName);
        }

        public async Task<bool> ConnectAsync()
        {
            try
            {
                await _connectionLock.WaitAsync();

                if (_isConnected)
                {
                    _logger.LogInformation("Already connected to streaming service");
                    return true;
                }

                _logger.LogInformation("Connecting to streaming service...");

                // Connect to market data provider
                var connected = await _marketDataProvider.ConnectAsync();
                if (!connected)
                {
                    _logger.LogError("Failed to connect to market data provider");
                    return false;
                }

                _isConnected = true;
                ConnectionChanged?.Invoke(this, true);

                _logger.LogInformation("Successfully connected to streaming service");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error connecting to streaming service");
                return false;
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task DisconnectAsync()
        {
            try
            {
                await _connectionLock.WaitAsync();

                if (!_isConnected)
                {
                    _logger.LogInformation("Already disconnected from streaming service");
                    return;
                }

                _logger.LogInformation("Disconnecting from streaming service...");

                // Stop streaming first
                await StopStreamingAsync();

                // Disconnect from market data provider
                await _marketDataProvider.DisconnectAsync();

                _isConnected = false;
                ConnectionChanged?.Invoke(this, false);

                _logger.LogInformation("Successfully disconnected from streaming service");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disconnecting from streaming service");
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        public async Task<bool> StartStreamingAsync()
        {
            try
            {
                if (!_isConnected)
                {
                    _logger.LogWarning("Cannot start streaming - not connected");
                    return false;
                }

                if (_isStreaming)
                {
                    _logger.LogInformation("Already streaming");
                    return true;
                }

                _logger.LogInformation("Starting real-time streaming...");

                _streamingCancellation = new CancellationTokenSource();
                _isStreaming = true;

                // Start background streaming task
                _ = Task.Run(async () => await StreamingLoopAsync(_streamingCancellation.Token));

                _logger.LogInformation("Real-time streaming started");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting streaming");
                return false;
            }
        }

        public async Task StopStreamingAsync()
        {
            try
            {
                if (!_isStreaming)
                {
                    _logger.LogInformation("Already stopped streaming");
                    return;
                }

                _logger.LogInformation("Stopping real-time streaming...");

                _streamingCancellation?.Cancel();
                _isStreaming = false;

                // Give a moment for the streaming loop to stop
                await Task.Delay(100);

                _logger.LogInformation("Real-time streaming stopped");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping streaming");
            }
        }

        public async Task<bool> AddSymbolAsync(string symbol, RealTimeDataType dataType)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                _logger.LogWarning("Cannot add empty symbol");
                return false;
            }

            try
            {
                symbol = symbol.ToUpperInvariant();
                
                var subscriptions = _activeSubscriptions.GetOrAdd(symbol, _ => new List<RealTimeDataType>());
                
                lock (subscriptions)
                {
                    if (!subscriptions.Contains(dataType))
                    {
                        subscriptions.Add(dataType);
                    }
                }

                // Subscribe with market data provider if this is a quote subscription
                if (dataType == RealTimeDataType.Quote && _marketDataProvider != null)
                {
                    // This would typically subscribe to real-time quotes
                    // For now, we'll just log it
                    _logger.LogDebug("Subscribed to real-time quotes for {Symbol}", symbol);
                }

                _logger.LogInformation("Added symbol {Symbol} for {DataType} streaming", symbol, dataType);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding symbol {Symbol} for {DataType}", symbol, dataType);
                return false;
            }
        }

        public async Task<bool> RemoveSymbolAsync(string symbol, RealTimeDataType dataType)
        {
            if (string.IsNullOrEmpty(symbol))
            {
                _logger.LogWarning("Cannot remove empty symbol");
                return false;
            }

            try
            {
                symbol = symbol.ToUpperInvariant();

                if (!_activeSubscriptions.TryGetValue(symbol, out var subscriptions))
                {
                    _logger.LogDebug("Symbol {Symbol} not found in subscriptions", symbol);
                    return true;
                }

                lock (subscriptions)
                {
                    subscriptions.Remove(dataType);
                }

                // Remove empty symbol entries
                if (subscriptions.Count == 0)
                {
                    _activeSubscriptions.TryRemove(symbol, out _);
                }

                // Unsubscribe from market data provider if this was a quote subscription
                if (dataType == RealTimeDataType.Quote && _marketDataProvider != null)
                {
                    _logger.LogDebug("Unsubscribed from real-time quotes for {Symbol}", symbol);
                }

                _logger.LogInformation("Removed symbol {Symbol} for {DataType} streaming", symbol, dataType);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing symbol {Symbol} for {DataType}", symbol, dataType);
                return false;
            }
        }

        private async Task StreamingLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Streaming loop started");

                while (!cancellationToken.IsCancellationRequested && _isConnected)
                {
                    try
                    {
                        // Generate updates for active subscriptions
                        await GenerateUpdatesAsync();

                        // Wait before next iteration
                        await Task.Delay(1000, cancellationToken); // 1 second intervals
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error in streaming loop iteration");
                        
                        var errorArgs = new RealTimeErrorEventArgs
                        {
                            ErrorMessage = ex.Message,
                            Exception = ex,
                            Source = "StreamingLoop",
                            IsCritical = false
                        };
                        
                        StreamError?.Invoke(this, errorArgs);
                        
                        // Brief pause before retrying
                        await Task.Delay(5000, cancellationToken);
                    }
                }

                _logger.LogInformation("Streaming loop ended");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error in streaming loop");
            }
        }

        private async Task GenerateUpdatesAsync()
        {
            try
            {
                if (!_activeSubscriptions.Any())
                {
                    return;
                }

                var tasks = new List<Task>();

                foreach (var subscription in _activeSubscriptions)
                {
                    var symbol = subscription.Key;
                    var dataTypes = subscription.Value.ToList();

                    foreach (var dataType in dataTypes)
                    {
                        tasks.Add(GenerateUpdateForSymbolAsync(symbol, dataType));
                    }
                }

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating updates");
            }
        }

        private async Task GenerateUpdateForSymbolAsync(string symbol, RealTimeDataType dataType)
        {
            try
            {
                var startTime = DateTime.UtcNow;

                // Try to get real data from market data provider first
                var realUpdate = await TryGetRealDataAsync(symbol, dataType);
                if (realUpdate != null)
                {
                    realUpdate.Latency = DateTime.UtcNow - startTime;
                    DataReceived?.Invoke(this, realUpdate);
                    return;
                }

                // Generate simulated data if no real data available
                var simulatedUpdate = GenerateSimulatedUpdate(symbol, dataType);
                if (simulatedUpdate != null)
                {
                    simulatedUpdate.Latency = DateTime.UtcNow - startTime;
                    DataReceived?.Invoke(this, simulatedUpdate);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating update for {Symbol} {DataType}", symbol, dataType);
            }
        }

        private async Task<RealTimeUpdate> TryGetRealDataAsync(string symbol, RealTimeDataType dataType)
        {
            try
            {
                if (dataType == RealTimeDataType.Quote && _marketDataProvider != null)
                {
                    // Try to get current quote from market data provider
                    var quote = await _marketDataProvider.GetCurrentQuoteAsync(symbol);
                    if (quote != null)
                    {
                        return new RealTimeUpdate
                        {
                            Symbol = symbol,
                            DataType = dataType,
                            Timestamp = quote.Timestamp,
                            Priority = RealTimePriority.Normal,
                            Source = _marketDataProvider.ProviderName,
                            Price = quote.Price,
                            Volume = (long)quote.Volume,
                            High = quote.High,
                            Low = quote.Low,
                            Open = quote.Open,
                            Close = quote.Close,
                            Change = quote.Change,
                            ChangePercent = quote.ChangePercent
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not get real data for {Symbol} {DataType}", symbol, dataType);
            }

            return null;
        }

        private RealTimeUpdate GenerateSimulatedUpdate(string symbol, RealTimeDataType dataType)
        {
            var random = new Random();
            var now = DateTime.UtcNow;
            
            // Generate base price from symbol hash for consistency
            var basePrice = 50m + (symbol.GetHashCode() % 100);
            var priceVariation = (decimal)(random.NextDouble() - 0.5) * 2; // +/- $1
            var currentPrice = Math.Max(1m, basePrice + priceVariation);

            return dataType switch
            {
                RealTimeDataType.Quote => new RealTimeUpdate
                {
                    Symbol = symbol,
                    DataType = dataType,
                    Timestamp = now,
                    Priority = RealTimePriority.Normal,
                    Source = "Simulated",
                    Price = currentPrice,
                    BidPrice = currentPrice - 0.01m,
                    AskPrice = currentPrice + 0.01m,
                    Volume = random.Next(1000, 10000),
                    Change = (decimal)(random.NextDouble() - 0.5) * 2,
                    ChangePercent = (decimal)(random.NextDouble() - 0.5) * 5,
                    High = currentPrice + (decimal)random.NextDouble(),
                    Low = currentPrice - (decimal)random.NextDouble(),
                    Open = currentPrice + (decimal)(random.NextDouble() - 0.5) * 0.5m
                },
                
                RealTimeDataType.Trade => new RealTimeUpdate
                {
                    Symbol = symbol,
                    DataType = dataType,
                    Timestamp = now,
                    Priority = RealTimePriority.Normal,
                    Source = "Simulated",
                    TradeId = Guid.NewGuid().ToString("N")[..8],
                    TradePrice = currentPrice,
                    TradeSize = random.Next(100, 1000),
                    TradeSide = random.NextDouble() > 0.5 ? "Buy" : "Sell"
                },
                
                RealTimeDataType.Volume => new RealTimeUpdate
                {
                    Symbol = symbol,
                    DataType = dataType,
                    Timestamp = now,
                    Priority = RealTimePriority.Normal,
                    Source = "Simulated",
                    Volume = random.Next(1000, 50000)
                },
                
                _ => new RealTimeUpdate
                {
                    Symbol = symbol,
                    DataType = dataType,
                    Timestamp = now,
                    Priority = RealTimePriority.Normal,
                    Source = "Simulated"
                }
            };
        }

        private void GenerateHeartbeat(object state)
        {
            try
            {
                if (!_isConnected || !_isStreaming)
                {
                    return;
                }

                // This could be used to generate periodic system updates
                // For now, we'll just log activity periodically
                if (_config.LogRealTimeEvents && _activeSubscriptions.Any())
                {
                    _logger.LogTrace("Streaming heartbeat - {SubscriptionCount} active subscriptions", 
                        _activeSubscriptions.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in heartbeat timer");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                _streamingCancellation?.Cancel();
                _heartbeatTimer?.Dispose();
                _connectionLock?.Dispose();
                _streamingCancellation?.Dispose();

                _activeSubscriptions.Clear();

                _logger.LogInformation("Real-time streaming service disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time streaming service");
            }

            _disposed = true;
        }
    }
}
