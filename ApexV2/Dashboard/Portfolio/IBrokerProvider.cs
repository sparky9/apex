#nullable disable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Interface for broker portfolio data providers
    /// </summary>
    public interface IBrokerProvider
    {
        string BrokerName { get; }
        bool IsConnected { get; }
        bool IsAuthenticated { get; }
        
        // Connection management
        Task<bool> ConnectAsync(BrokerConfig config, CancellationToken cancellationToken = default);
        Task DisconnectAsync();
        Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);
        
        // Account information
        Task<Portfolio> GetPortfolioAsync(CancellationToken cancellationToken = default);
        Task<List<Position>> GetPositionsAsync(CancellationToken cancellationToken = default);
        Task<List<Order>> GetOrdersAsync(CancellationToken cancellationToken = default);
        Task<List<Transaction>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
        
        // Real-time updates
        event EventHandler<BrokerPortfolioUpdatedEventArgs> PortfolioUpdated;
        event EventHandler<PositionUpdatedEventArgs> PositionUpdated;
        event EventHandler<OrderUpdatedEventArgs> OrderUpdated;
        event EventHandler<BrokerConnectionEventArgs> ConnectionStatusChanged;
        
        // Historical data
        Task<PortfolioPerformance> GetPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        Task<List<DailyPerformance>> GetDailyPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
    }
    
    /// <summary>
    /// Base implementation for broker providers with common functionality
    /// </summary>
    public abstract class BrokerProviderBase : IBrokerProvider
    {
        protected BrokerConfig _config;
        protected bool _isConnected;
        protected bool _isAuthenticated;
        protected CancellationTokenSource _connectionCts;
        
        public abstract string BrokerName { get; }
        public bool IsConnected => _isConnected;
        public bool IsAuthenticated => _isAuthenticated;
        
        // Events
        public event EventHandler<BrokerPortfolioUpdatedEventArgs> PortfolioUpdated;
        public event EventHandler<PositionUpdatedEventArgs> PositionUpdated;
        public event EventHandler<OrderUpdatedEventArgs> OrderUpdated;
        public event EventHandler<BrokerConnectionEventArgs> ConnectionStatusChanged;
        
        public virtual async Task<bool> ConnectAsync(BrokerConfig config, CancellationToken cancellationToken = default)
        {
            try
            {
                _config = config ?? throw new ArgumentNullException(nameof(config));
                _connectionCts = new CancellationTokenSource();
                
                OnConnectionStatusChanged(new BrokerConnectionEventArgs 
                { 
                    BrokerName = BrokerName, 
                    Status = SyncStatus.Syncing, 
                    Message = "Connecting..." 
                });
                
                var success = await ConnectImplementationAsync(config, cancellationToken);
                
                if (success)
                {
                    _isConnected = true;
                    _isAuthenticated = true;
                    OnConnectionStatusChanged(new BrokerConnectionEventArgs 
                    { 
                        BrokerName = BrokerName, 
                        Status = SyncStatus.Connected, 
                        Message = "Connected successfully" 
                    });
                }
                else
                {
                    OnConnectionStatusChanged(new BrokerConnectionEventArgs 
                    { 
                        BrokerName = BrokerName, 
                        Status = SyncStatus.Error, 
                        Message = "Connection failed" 
                    });
                }
                
                return success;
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged(new BrokerConnectionEventArgs 
                { 
                    BrokerName = BrokerName, 
                    Status = SyncStatus.Error, 
                    Message = ex.Message 
                });
                return false;
            }
        }
        
        public virtual async Task DisconnectAsync()
        {
            try
            {
                _connectionCts?.Cancel();
                await DisconnectImplementationAsync();
                
                _isConnected = false;
                _isAuthenticated = false;
                
                OnConnectionStatusChanged(new BrokerConnectionEventArgs 
                { 
                    BrokerName = BrokerName, 
                    Status = SyncStatus.Disconnected, 
                    Message = "Disconnected" 
                });
            }
            catch (Exception ex)
            {
                OnConnectionStatusChanged(new BrokerConnectionEventArgs 
                { 
                    BrokerName = BrokerName, 
                    Status = SyncStatus.Error, 
                    Message = $"Disconnect error: {ex.Message}" 
                });
            }
        }
        
        public virtual async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_isConnected) return false;
                return await TestConnectionImplementationAsync(cancellationToken);
            }
            catch
            {
                return false;
            }
        }
        
        // Abstract methods for broker-specific implementation
        protected abstract Task<bool> ConnectImplementationAsync(BrokerConfig config, CancellationToken cancellationToken);
        protected abstract Task DisconnectImplementationAsync();
        protected abstract Task<bool> TestConnectionImplementationAsync(CancellationToken cancellationToken);
        
        public abstract Task<Portfolio> GetPortfolioAsync(CancellationToken cancellationToken = default);
        public abstract Task<List<Position>> GetPositionsAsync(CancellationToken cancellationToken = default);
        public abstract Task<List<Order>> GetOrdersAsync(CancellationToken cancellationToken = default);
        public abstract Task<List<Transaction>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default);
        public abstract Task<PortfolioPerformance> GetPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        public abstract Task<List<DailyPerformance>> GetDailyPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
        
        // Event helper methods
        protected virtual void OnPortfolioUpdated(BrokerPortfolioUpdatedEventArgs args)
        {
            PortfolioUpdated?.Invoke(this, args);
        }
        
        protected virtual void OnPositionUpdated(PositionUpdatedEventArgs args)
        {
            PositionUpdated?.Invoke(this, args);
        }
        
        protected virtual void OnOrderUpdated(OrderUpdatedEventArgs args)
        {
            OrderUpdated?.Invoke(this, args);
        }
        
        protected virtual void OnConnectionStatusChanged(BrokerConnectionEventArgs args)
        {
            ConnectionStatusChanged?.Invoke(this, args);
        }
        
        // Helper method for error handling
        protected void HandleApiError(Exception ex, string operation)
        {
            var status = ex.Message.Contains("authentication", StringComparison.OrdinalIgnoreCase) 
                ? SyncStatus.AuthenticationFailed 
                : ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase)
                ? SyncStatus.RateLimited
                : SyncStatus.Error;
                
            OnConnectionStatusChanged(new BrokerConnectionEventArgs 
            { 
                BrokerName = BrokerName, 
                Status = status, 
                Message = $"{operation} failed: {ex.Message}" 
            });
        }
        
        // Dispose pattern
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _connectionCts?.Cancel();
                _connectionCts?.Dispose();
            }
        }
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
    
    #region Event Args
    
    public class BrokerPortfolioUpdatedEventArgs : EventArgs
    {
        public Portfolio Portfolio { get; set; }
        public DateTime UpdateTime { get; set; } = DateTime.Now;
    }
    
    public class PositionUpdatedEventArgs : EventArgs
    {
        public Position Position { get; set; }
        public string ChangeType { get; set; } // Added, Updated, Removed
        public DateTime UpdateTime { get; set; } = DateTime.Now;
    }
    
    public class OrderUpdatedEventArgs : EventArgs
    {
        public Order Order { get; set; }
        public string ChangeType { get; set; } // Added, Updated, Filled, Cancelled
        public DateTime UpdateTime { get; set; } = DateTime.Now;
    }
    
    public class BrokerConnectionEventArgs : EventArgs
    {
        public string BrokerName { get; set; }
        public SyncStatus Status { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
    
    #endregion
}
