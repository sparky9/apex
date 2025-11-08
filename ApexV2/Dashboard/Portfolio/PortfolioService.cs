using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApexV2.Charts.Export;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Central portfolio management service that coordinates multiple broker providers
    /// </summary>
    public class PortfolioService : IDisposable
    {
        private readonly IChartLogger _logger;
        private readonly ConcurrentDictionary<string, IBrokerProvider> _brokerProviders;
        private readonly ConcurrentDictionary<string, Portfolio> _portfolios;
        private readonly ConcurrentDictionary<string, PortfolioSyncStatus> _syncStatuses;
        private readonly Timer _syncTimer;
        private bool _disposed;
        
        // Events
        public event EventHandler<PortfolioUpdatedEventArgs> PortfolioUpdated;
        public event EventHandler<PositionUpdatedEventArgs> PositionUpdated;
        public event EventHandler<OrderUpdatedEventArgs> OrderUpdated;
        public event EventHandler<BrokerConnectionEventArgs> BrokerConnectionChanged;
        public event EventHandler<PortfolioSyncStatusEventArgs> SyncStatusChanged;
        
        public PortfolioService(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _brokerProviders = new ConcurrentDictionary<string, IBrokerProvider>();
            _portfolios = new ConcurrentDictionary<string, Portfolio>();
            _syncStatuses = new ConcurrentDictionary<string, PortfolioSyncStatus>();
            
            // Start sync timer (every 30 seconds)
            _syncTimer = new Timer(SyncAllPortfolios, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            
            _logger.Info("Portfolio service initialized");
        }
        
        #region Broker Management
        
        /// <summary>
        /// Register a broker provider
        /// </summary>
        public void RegisterBrokerProvider(IBrokerProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            
            if (_brokerProviders.TryAdd(provider.BrokerName, provider))
            {
                // Subscribe to provider events
                provider.PortfolioUpdated += OnProviderPortfolioUpdated;
                provider.PositionUpdated += OnProviderPositionUpdated;
                provider.OrderUpdated += OnProviderOrderUpdated;
                provider.ConnectionStatusChanged += OnProviderConnectionStatusChanged;
                
                _logger.Info($"Registered broker provider: {provider.BrokerName}");
            }
            else
            {
                _logger.Warn($"Broker provider already registered: {provider.BrokerName}");
            }
        }
        
        /// <summary>
        /// Connect to a broker
        /// </summary>
        public async Task<bool> ConnectToBrokerAsync(string brokerName, BrokerConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_brokerProviders.TryGetValue(brokerName, out var provider))
                {
                    _logger.Error($"Broker provider not found: {brokerName}");
                    return false;
                }
                
                UpdateSyncStatus(brokerName, SyncStatus.Syncing, "Connecting...");
                
                var success = await provider.ConnectAsync(config, cancellationToken);
                
                if (success)
                {
                    UpdateSyncStatus(brokerName, SyncStatus.Connected, "Connected successfully");
                    
                    // Initial portfolio sync
                    await SyncPortfolioAsync(brokerName, cancellationToken);
                    
                    _logger.Info($"Successfully connected to {brokerName}");
                }
                else
                {
                    UpdateSyncStatus(brokerName, SyncStatus.Error, "Connection failed");
                    _logger.Error($"Failed to connect to {brokerName}");
                }
                
                return success;
            }
            catch (Exception ex)
            {
                UpdateSyncStatus(brokerName, SyncStatus.Error, ex.Message);
                _logger.Error($"Error connecting to {brokerName}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Disconnect from a broker
        /// </summary>
        public async Task DisconnectFromBrokerAsync(string brokerName)
        {
            try
            {
                if (_brokerProviders.TryGetValue(brokerName, out var provider))
                {
                    await provider.DisconnectAsync();
                    _portfolios.TryRemove(brokerName, out _);
                    UpdateSyncStatus(brokerName, SyncStatus.Disconnected, "Disconnected");
                    _logger.Info($"Disconnected from {brokerName}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error disconnecting from {brokerName}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get list of available broker providers
        /// </summary>
        public List<string> GetAvailableBrokers()
        {
            return _brokerProviders.Keys.ToList();
        }
        
        /// <summary>
        /// Get connection status for all brokers
        /// </summary>
        public Dictionary<string, bool> GetBrokerConnectionStatus()
        {
            return _brokerProviders.ToDictionary(
                kvp => kvp.Key, 
                kvp => kvp.Value.IsConnected
            );
        }
        
        #endregion
        
        #region Portfolio Data
        
        /// <summary>
        /// Get portfolio for a specific broker
        /// </summary>
        public Portfolio GetPortfolio(string brokerName)
        {
            _portfolios.TryGetValue(brokerName, out var portfolio);
            return portfolio;
        }
        
        /// <summary>
        /// Get all portfolios
        /// </summary>
        public List<Portfolio> GetAllPortfolios()
        {
            return _portfolios.Values.ToList();
        }
        
        /// <summary>
        /// Get consolidated portfolio (all brokers combined)
        /// </summary>
        public Portfolio GetConsolidatedPortfolio()
        {
            var portfolios = GetAllPortfolios();
            if (!portfolios.Any()) return null;
            
            var consolidated = new Portfolio
            {
                Id = Guid.NewGuid(),
                Name = "Consolidated Portfolio",
                BrokerName = "Multiple",
                AccountId = "CONSOLIDATED",
                LastUpdated = DateTime.Now,
                IsActive = true
            };
            
            // Aggregate financial data
            consolidated.TotalValue = portfolios.Sum(p => p.TotalValue);
            consolidated.TotalCost = portfolios.Sum(p => p.TotalCost);
            consolidated.TotalGainLoss = portfolios.Sum(p => p.TotalGainLoss);
            consolidated.DayChange = portfolios.Sum(p => p.DayChange);
            consolidated.CashBalance = portfolios.Sum(p => p.CashBalance);
            consolidated.BuyingPower = portfolios.Sum(p => p.BuyingPower);
            consolidated.MarginUsed = portfolios.Sum(p => p.MarginUsed);
            
            // Calculate percentages
            if (consolidated.TotalCost > 0)
            {
                consolidated.DayChangePercent = portfolios.Sum(p => p.DayChange * p.TotalValue) / consolidated.TotalValue * 100;
            }
            
            // Combine positions (group by symbol)
            var allPositions = portfolios.SelectMany(p => p.Positions).ToList();
            consolidated.Positions = ConsolidatePositions(allPositions);
            
            // Combine orders and transactions
            consolidated.Orders = portfolios.SelectMany(p => p.Orders).ToList();
            consolidated.Transactions = portfolios.SelectMany(p => p.Transactions).ToList();
            
            return consolidated;
        }
        
        /// <summary>
        /// Get positions for a specific broker
        /// </summary>
        public async Task<List<Position>> GetPositionsAsync(string brokerName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_brokerProviders.TryGetValue(brokerName, out var provider))
                {
                    return await provider.GetPositionsAsync(cancellationToken);
                }
                return new List<Position>();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error getting positions from {brokerName}: {ex.Message}");
                return new List<Position>();
            }
        }
        
        /// <summary>
        /// Get orders for a specific broker
        /// </summary>
        public async Task<List<Order>> GetOrdersAsync(string brokerName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_brokerProviders.TryGetValue(brokerName, out var provider))
                {
                    return await provider.GetOrdersAsync(cancellationToken);
                }
                return new List<Order>();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error getting orders from {brokerName}: {ex.Message}");
                return new List<Order>();
            }
        }
        
        /// <summary>
        /// Get performance data for a specific broker
        /// </summary>
        public async Task<PortfolioPerformance> GetPerformanceAsync(string brokerName, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                if (_brokerProviders.TryGetValue(brokerName, out var provider))
                {
                    return await provider.GetPerformanceAsync(startDate, endDate, cancellationToken);
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error getting performance from {brokerName}: {ex.Message}");
                return null;
            }
        }
        
        #endregion
        
        #region Synchronization
        
        /// <summary>
        /// Manually sync a specific broker's portfolio
        /// </summary>
        public async Task<bool> SyncPortfolioAsync(string brokerName, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_brokerProviders.TryGetValue(brokerName, out var provider) || !provider.IsConnected)
                {
                    return false;
                }
                
                var startTime = DateTime.Now;
                UpdateSyncStatus(brokerName, SyncStatus.Syncing, "Syncing portfolio...");
                
                var portfolio = await provider.GetPortfolioAsync(cancellationToken);
                
                if (portfolio != null)
                {
                    _portfolios.AddOrUpdate(brokerName, portfolio, (key, oldValue) => portfolio);
                    
                    var syncDuration = DateTime.Now - startTime;
                    UpdateSyncStatus(brokerName, SyncStatus.Connected, "Sync completed", 
                        portfolio.Positions.Count, portfolio.Orders.Count, portfolio.Transactions.Count, syncDuration);
                    
                    OnPortfolioUpdated(new PortfolioUpdatedEventArgs { Portfolio = portfolio });
                    
                    _logger.Info($"Synced portfolio for {brokerName} in {syncDuration.TotalMilliseconds:F0}ms");
                    return true;
                }
                else
                {
                    UpdateSyncStatus(brokerName, SyncStatus.Error, "Failed to retrieve portfolio");
                    return false;
                }
            }
            catch (Exception ex)
            {
                UpdateSyncStatus(brokerName, SyncStatus.Error, ex.Message);
                _logger.Error($"Error syncing {brokerName}: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Sync all connected broker portfolios
        /// </summary>
        public async Task SyncAllPortfoliosAsync(CancellationToken cancellationToken = default)
        {
            var tasks = _brokerProviders.Values
                .Where(p => p.IsConnected)
                .Select(p => SyncPortfolioAsync(p.BrokerName, cancellationToken));
            
            await Task.WhenAll(tasks);
        }
        
        /// <summary>
        /// Get sync status for all brokers
        /// </summary>
        public Dictionary<string, PortfolioSyncStatus> GetSyncStatuses()
        {
            return _syncStatuses.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        
        #endregion
        
        #region Private Methods
        
        private async void SyncAllPortfolios(object state)
        {
            try
            {
                await SyncAllPortfoliosAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"Background sync error: {ex.Message}");
            }
        }
        
        private List<Position> ConsolidatePositions(List<Position> positions)
        {
            var consolidated = new List<Position>();
            
            var groupedPositions = positions.GroupBy(p => p.Symbol);
            
            foreach (var group in groupedPositions)
            {
                var totalQuantity = group.Sum(p => p.Quantity);
                var totalCost = group.Sum(p => p.TotalCost);
                var averageCost = totalQuantity != 0 ? totalCost / totalQuantity : 0;
                
                var consolidatedPosition = new Position
                {
                    Symbol = group.Key,
                    CompanyName = group.First().CompanyName,
                    Sector = group.First().Sector,
                    AssetType = group.First().AssetType,
                    Quantity = totalQuantity,
                    AverageCost = averageCost,
                    CurrentPrice = group.First().CurrentPrice, // Use first provider's current price
                    DayChange = group.Sum(p => p.DayChange),
                    LastUpdated = group.Max(p => p.LastUpdated),
                    FirstPurchaseDate = group.Min(p => p.FirstPurchaseDate)
                };
                
                consolidated.Add(consolidatedPosition);
            }
            
            return consolidated;
        }
        
        private void UpdateSyncStatus(string brokerName, SyncStatus status, string message, 
            int positionsCount = 0, int ordersCount = 0, int transactionsCount = 0, TimeSpan? syncDuration = null)
        {
            var syncStatus = new PortfolioSyncStatus
            {
                BrokerName = brokerName,
                LastSyncTime = DateTime.Now,
                Status = status,
                ErrorMessage = status == SyncStatus.Error ? message : "",
                PositionsCount = positionsCount,
                OrdersCount = ordersCount,
                TransactionsCount = transactionsCount,
                SyncDuration = syncDuration ?? TimeSpan.Zero
            };
            
            _syncStatuses.AddOrUpdate(brokerName, syncStatus, (key, oldValue) => syncStatus);
            OnSyncStatusChanged(new PortfolioSyncStatusEventArgs { SyncStatus = syncStatus });
        }
        
        #endregion
        
        #region Event Handlers
        
        private void OnProviderPortfolioUpdated(object sender, BrokerPortfolioUpdatedEventArgs e)
        {
            if (sender is IBrokerProvider provider)
            {
                _portfolios.AddOrUpdate(provider.BrokerName, e.Portfolio, (key, oldValue) => e.Portfolio);
                
                // Convert BrokerPortfolioUpdatedEventArgs to PortfolioUpdatedEventArgs
                var portfolioUpdatedArgs = new PortfolioUpdatedEventArgs
                {
                    Portfolio = e.Portfolio,
                    UpdateType = PortfolioUpdateType.TotalCalculation
                };
                
                OnPortfolioUpdated(portfolioUpdatedArgs);
            }
        }
        
        private void OnProviderPositionUpdated(object sender, PositionUpdatedEventArgs e)
        {
            OnPositionUpdated(e);
        }
        
        private void OnProviderOrderUpdated(object sender, OrderUpdatedEventArgs e)
        {
            OnOrderUpdated(e);
        }
        
        private void OnProviderConnectionStatusChanged(object sender, BrokerConnectionEventArgs e)
        {
            OnBrokerConnectionChanged(e);
            
            // Update sync status based on connection status
            var status = e.Status == SyncStatus.Connected ? SyncStatus.Connected :
                        e.Status == SyncStatus.Error ? SyncStatus.Error :
                        SyncStatus.Disconnected;
            
            UpdateSyncStatus(e.BrokerName, status, e.Message);
        }
        
        #endregion
        
        #region Event Triggers
        
        protected virtual void OnPortfolioUpdated(PortfolioUpdatedEventArgs e)
        {
            PortfolioUpdated?.Invoke(this, e);
        }
        
        protected virtual void OnPositionUpdated(PositionUpdatedEventArgs e)
        {
            PositionUpdated?.Invoke(this, e);
        }
        
        protected virtual void OnOrderUpdated(OrderUpdatedEventArgs e)
        {
            OrderUpdated?.Invoke(this, e);
        }
        
        protected virtual void OnBrokerConnectionChanged(BrokerConnectionEventArgs e)
        {
            BrokerConnectionChanged?.Invoke(this, e);
        }
        
        protected virtual void OnSyncStatusChanged(PortfolioSyncStatusEventArgs e)
        {
            SyncStatusChanged?.Invoke(this, e);
        }
        
        #endregion
        
        #region IDisposable
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _syncTimer?.Dispose();
                
                foreach (var provider in _brokerProviders.Values)
                {
                    try
                    {
                        provider.DisconnectAsync().Wait(5000);
                        if (provider is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"Error disposing provider: {ex.Message}");
                    }
                }
                
                _brokerProviders.Clear();
                _portfolios.Clear();
                _syncStatuses.Clear();
                
                _disposed = true;
                _logger.Info("Portfolio service disposed");
            }
        }
        
        #endregion
    }
    
    #region Additional Event Args
    
    public class PortfolioSyncStatusEventArgs : EventArgs
    {
        public PortfolioSyncStatus SyncStatus { get; set; }
    }
    
    #endregion
}
