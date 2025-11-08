#nullable disable
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ApexV2.Extensions.RealTime;
using ApexV2.Data.MarketData;
using ApexV2.Core.Config;
using ApexV2.Core.Logging;

namespace ApexV2.Extensions.RealTime
{
    /// <summary>
    /// Main integration class for the real-time data system
    /// Provides a simple way to initialize and manage all real-time components
    /// </summary>
    public class RealTimeIntegration : IDisposable
    {
        private readonly ILogger<RealTimeIntegration> _logger;
        private readonly RealTimeConfiguration _config;
        private RealTimeManager _realTimeManager;
        private IMarketDataProvider _marketDataProvider;
        private bool _isInitialized;
        private bool _disposed;

        // Events for external listeners
        public event EventHandler<RealTimeUpdate> DataUpdated;
        public event EventHandler<RealTimeErrorEventArgs> ErrorOccurred;
        public event EventHandler<RealTimeConnectionEventArgs> ConnectionStatusChanged;
        public event EventHandler<RealTimeAlertTriggeredEventArgs> AlertTriggered;
        public event EventHandler<RealTimeMetrics> MetricsUpdated;

        public bool IsInitialized => _isInitialized;
        public bool IsConnected => _realTimeManager?.IsConnected ?? false;
        public bool IsStreaming => _realTimeManager?.IsStreaming ?? false;
        public RealTimeManager RealTimeManager => _realTimeManager;

        public RealTimeIntegration(ILogger<RealTimeIntegration> logger = null, RealTimeConfiguration config = null)
        {
            _logger = logger ?? new TestLogger<RealTimeIntegration>();
            _config = config ?? CreateDefaultConfiguration();
            
            _logger.LogInformation("Real-time integration created with configuration: Provider={Provider}, AutoConnect={AutoConnect}", 
                _config.ProviderName, _config.AutoConnect);
        }

        /// <summary>
        /// Initialize the real-time system with a market data provider
        /// </summary>
        public async Task<bool> InitializeAsync(IMarketDataProvider marketDataProvider)
        {
            try
            {
                if (_isInitialized)
                {
                    _logger.LogWarning("Real-time integration already initialized");
                    return true;
                }

                if (marketDataProvider == null)
                {
                    throw new ArgumentNullException(nameof(marketDataProvider));
                }

                _marketDataProvider = marketDataProvider;

                // Create the real-time manager
                _realTimeManager = new RealTimeManager(
                    new TestLogger<RealTimeManager>(),
                    _config,
                    _marketDataProvider);

                // Subscribe to events
                _realTimeManager.DataUpdated += OnDataUpdated;
                _realTimeManager.ErrorOccurred += OnErrorOccurred;
                _realTimeManager.ConnectionStatusChanged += OnConnectionStatusChanged;
                _realTimeManager.AlertTriggered += OnAlertTriggered;
                _realTimeManager.MetricsUpdated += OnMetricsUpdated;

                // Initialize the manager
                var initialized = await _realTimeManager.InitializeAsync();
                if (!initialized)
                {
                    _logger.LogError("Failed to initialize real-time manager");
                    return false;
                }

                _isInitialized = true;
                _logger.LogInformation("Real-time integration initialized successfully with provider: {Provider}", 
                    _marketDataProvider.ProviderName);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing real-time integration");
                return false;
            }
        }

        /// <summary>
        /// Initialize with default market data provider (simulated data)
        /// </summary>
        public async Task<bool> InitializeWithSimulatedDataAsync()
        {
            try
            {
                var simulatedProvider = new SimulatedMarketDataProvider();
                return await InitializeAsync(simulatedProvider);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing with simulated data");
                return false;
            }
        }

        /// <summary>
        /// Start the real-time data streaming
        /// </summary>
        public async Task<bool> StartAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogError("Cannot start real-time services - not initialized");
                    return false;
                }

                var started = await _realTimeManager.StartAsync();
                if (started)
                {
                    _logger.LogInformation("Real-time integration started successfully");
                }
                else
                {
                    _logger.LogError("Failed to start real-time integration");
                }

                return started;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting real-time integration");
                return false;
            }
        }

        /// <summary>
        /// Stop the real-time data streaming
        /// </summary>
        public async Task StopAsync()
        {
            try
            {
                if (_realTimeManager != null)
                {
                    await _realTimeManager.StopAsync();
                    _logger.LogInformation("Real-time integration stopped");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping real-time integration");
            }
        }

        /// <summary>
        /// Subscribe to real-time data for a symbol
        /// </summary>
        public async Task<bool> SubscribeAsync(string symbol, RealTimeDataType dataType, Action<RealTimeUpdate> callback)
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogError("Cannot subscribe - not initialized");
                    return false;
                }

                return await _realTimeManager.SubscribeAsync(symbol, dataType, callback);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to {Symbol} {DataType}", symbol, dataType);
                return false;
            }
        }

        /// <summary>
        /// Unsubscribe from real-time data for a symbol
        /// </summary>
        public async Task<bool> UnsubscribeAsync(string symbol, RealTimeDataType dataType)
        {
            try
            {
                if (!_isInitialized)
                {
                    return false;
                }

                return await _realTimeManager.UnsubscribeAsync(symbol, dataType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error unsubscribing from {Symbol} {DataType}", symbol, dataType);
                return false;
            }
        }

        /// <summary>
        /// Create a price alert for a symbol
        /// </summary>
        public async Task<Guid> CreatePriceAlertAsync(string symbol, decimal targetPrice, RealTimeAlertCondition condition)
        {
            try
            {
                if (!_isInitialized)
                {
                    _logger.LogError("Cannot create alert - not initialized");
                    return Guid.Empty;
                }

                var alert = new RealTimeAlert
                {
                    Symbol = symbol,
                    DataType = RealTimeDataType.Price,
                    Condition = condition,
                    TargetValue = targetPrice,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow,
                    Name = $"{symbol} Price Alert",
                    Description = $"Alert when {symbol} price {condition} {targetPrice:C2}"
                };

                return await _realTimeManager.CreateAlertAsync(alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating price alert for {Symbol}", symbol);
                return Guid.Empty;
            }
        }

        /// <summary>
        /// Get current real-time metrics
        /// </summary>
        public async Task<RealTimeMetrics> GetMetricsAsync()
        {
            try
            {
                if (!_isInitialized)
                {
                    return null;
                }

                return await _realTimeManager.GetMetricsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting real-time metrics");
                return null;
            }
        }

        /// <summary>
        /// Quick start method for testing and demos
        /// </summary>
        public static async Task<RealTimeIntegration> QuickStartAsync()
        {
            var integration = new RealTimeIntegration();
            
            var initialized = await integration.InitializeWithSimulatedDataAsync();
            if (!initialized)
            {
                return null;
            }

            var started = await integration.StartAsync();
            if (!started)
            {
                return null;
            }

            return integration;
        }

        private static RealTimeConfiguration CreateDefaultConfiguration()
        {
            return new RealTimeConfiguration
            {
                ProviderName = "Simulated",
                AutoConnect = true,
                EnableAggregation = true,
                EnableAlerts = true,
                EnableMetrics = true,
                UpdateIntervalMs = 100,
                MaxReconnectionAttempts = 5,
                ReconnectionDelaySeconds = 1,
                MaxConcurrentSubscriptions = 100,
                BufferSize = 1000
            };
        }

        // Event handlers that forward to external listeners
        private void OnDataUpdated(object sender, RealTimeUpdate update)
        {
            DataUpdated?.Invoke(this, update);
        }

        private void OnErrorOccurred(object sender, RealTimeErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        private void OnConnectionStatusChanged(object sender, RealTimeConnectionEventArgs e)
        {
            ConnectionStatusChanged?.Invoke(this, e);
        }

        private void OnAlertTriggered(object sender, RealTimeAlertTriggeredEventArgs e)
        {
            AlertTriggered?.Invoke(this, e);
        }

        private void OnMetricsUpdated(object sender, RealTimeMetrics metrics)
        {
            MetricsUpdated?.Invoke(this, metrics);
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Stop services
                _ = Task.Run(async () => await StopAsync());

                // Unsubscribe from events
                if (_realTimeManager != null)
                {
                    _realTimeManager.DataUpdated -= OnDataUpdated;
                    _realTimeManager.ErrorOccurred -= OnErrorOccurred;
                    _realTimeManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    _realTimeManager.AlertTriggered -= OnAlertTriggered;
                    _realTimeManager.MetricsUpdated -= OnMetricsUpdated;
                }

                // Dispose manager
                _realTimeManager?.Dispose();

                _logger.LogInformation("Real-time integration disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time integration");
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Simple simulated market data provider for testing
    /// </summary>
    public class SimulatedMarketDataProvider : IMarketDataProvider
    {
        public string ProviderName => "Simulated";
        public bool IsConnected { get; private set; }
        public bool SupportsStreaming => true;
        public bool RequiresApiKey => false;
        public bool SupportsCanadianSymbols => true;

        public event EventHandler<PriceUpdateEventArgs>? PriceUpdate;

        public Task<bool> ConnectAsync()
        {
            IsConnected = true;
            return Task.FromResult(true);
        }

        public Task DisconnectAsync()
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task<StockQuote> GetQuoteAsync(string symbol)
        {
            var random = new Random();
            var basePrice = 100m + (decimal)(random.NextDouble() * 100);
            
            return Task.FromResult(new StockQuote
            {
                Symbol = symbol,
                Price = basePrice,
                Volume = random.Next(1000, 10000),
                Timestamp = DateTime.Now,
                Source = ProviderName
            });
        }

        public Task<StockQuote> GetCurrentQuoteAsync(string symbol)
        {
            return GetQuoteAsync(symbol); // Same implementation for simulated provider
        }

        public async Task<List<StockQuote>> GetQuotesAsync(List<string> symbols)
        {
            var quotes = new List<StockQuote>();
            foreach (var symbol in symbols)
            {
                quotes.Add(await GetQuoteAsync(symbol));
            }
            return quotes;
        }

        public Task<List<HistoricalPrice>> GetHistoricalDataAsync(string symbol, DateTime startDate, DateTime endDate)
        {
            var historical = new List<HistoricalPrice>();
            var random = new Random();
            var current = startDate;
            var basePrice = 100m + (decimal)(random.NextDouble() * 100);

            while (current <= endDate)
            {
                basePrice += (decimal)((random.NextDouble() - 0.5) * 2); // Random walk
                historical.Add(new HistoricalPrice
                {
                    Symbol = symbol,
                    Date = current,
                    Open = basePrice,
                    High = basePrice + (decimal)random.NextDouble(),
                    Low = basePrice - (decimal)random.NextDouble(),
                    Close = basePrice,
                    Volume = random.Next(1000, 10000)
                });
                current = current.AddDays(1);
            }
            return Task.FromResult(historical);
        }

        public Task<List<StockQuote>> SearchSymbolsAsync(string query)
        {
            var results = new List<StockQuote>
            {
                new() { Symbol = query.ToUpper(), Price = 100m, Volume = 1000, Timestamp = DateTime.Now, Source = ProviderName },
                new() { Symbol = $"{query}1", Price = 95m, Volume = 1500, Timestamp = DateTime.Now, Source = ProviderName },
                new() { Symbol = $"{query}2", Price = 105m, Volume = 800, Timestamp = DateTime.Now, Source = ProviderName }
            };
            return Task.FromResult(results);
        }

        public Task<CompanyFundamentals> GetFundamentalsAsync(string symbol)
        {
            var random = new Random();
            var fundamentals = new CompanyFundamentals
            {
                Symbol = symbol,
                CompanyName = $"{symbol} Corporation",
                MarketCap = (long)random.Next(1000000000, int.MaxValue) + random.Next(0, 1000000000),
                PeRatio = (decimal)(random.NextDouble() * 30 + 5),
                EpsRatio = (decimal)(random.NextDouble() * 10),
                DebtToEquityRatio = (decimal)(random.NextDouble() * 2),
                LastUpdated = DateTime.Now,
                Source = ProviderName
            };
            return Task.FromResult(fundamentals);
        }

        public Task<List<NewsItem>> GetNewsAsync(string? symbol = null)
        {
            var news = new List<NewsItem>
            {
                new() { Title = $"Breaking: {symbol ?? "Market"} news", Content = "Sample news content", PublishedAt = DateTime.Now, Source = ProviderName },
                new() { Title = $"{symbol ?? "Market"} analysis update", Content = "Sample analysis content", PublishedAt = DateTime.Now.AddHours(-1), Source = ProviderName }
            };
            return Task.FromResult(news);
        }

        public void Dispose()
        {
            IsConnected = false;
        }
    }
}
