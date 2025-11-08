using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Represents a complete portfolio with all holdings and performance data
    /// </summary>
    public class Portfolio
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string BrokerName { get; set; } = "";
        public string AccountId { get; set; } = "";
        public DateTime CreatedDate { get; set; } = DateTime.Now;
        public DateTime LastUpdated { get; set; }
        public bool IsActive { get; set; } = true;
        
        // Performance metrics
        public decimal TotalValue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalGainLoss { get; set; }
        public decimal TotalGainLossPercent => TotalCost != 0 ? (TotalGainLoss / TotalCost) * 100 : 0;
        public decimal DayChange { get; set; }
        public decimal DayChangePercent { get; set; }
        
        // Cash and buying power
        public decimal CashBalance { get; set; }
        public decimal BuyingPower { get; set; }
        public decimal MarginUsed { get; set; }
        
        // Holdings
        public List<Position> Positions { get; set; } = new();
        public List<Order> Orders { get; set; } = new();
        public List<Transaction> Transactions { get; set; } = new();
        
        // Risk metrics
        public decimal BetaWeighted { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal Sharpe { get; set; }
        
        /// <summary>
        /// Get positions grouped by sector
        /// </summary>
        public Dictionary<string, List<Position>> GetPositionsBySector()
        {
            return Positions.GroupBy(p => p.Sector ?? "Unknown")
                           .ToDictionary(g => g.Key, g => g.ToList());
        }
        
        /// <summary>
        /// Get top holdings by value
        /// </summary>
        public List<Position> GetTopHoldings(int count = 10)
        {
            return Positions.OrderByDescending(p => p.MarketValue)
                           .Take(count)
                           .ToList();
        }
    }
    
    /// <summary>
    /// Represents an individual position in the portfolio
    /// </summary>
    public class Position
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Symbol { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Sector { get; set; } = "";
        public string AssetType { get; set; } = "Stock"; // Stock, ETF, Option, etc.
        
        // Quantity and pricing
        public decimal Quantity { get; set; }
        public decimal AverageCost { get; set; }
        public decimal CurrentPrice { get; set; }
        public decimal MarketValue => Quantity * CurrentPrice;
        public decimal TotalCost => Quantity * AverageCost;
        
        // Performance
        public decimal UnrealizedGainLoss { get; set; }
        public decimal UnrealizedGainLossPercent => TotalCost != 0 ? (UnrealizedGainLoss / TotalCost) * 100 : 0;
        public decimal DayChange { get; set; }
        public decimal DayChangePercent { get; set; }
        
        // Risk metrics
        public decimal Beta { get; set; }
        public decimal DividendYield { get; set; }
        
        // Timestamps
        public DateTime FirstPurchaseDate { get; set; }
        public DateTime LastUpdated { get; set; }
        
        /// <summary>
        /// Calculate portfolio weight percentage
        /// </summary>
        public decimal GetPortfolioWeight(decimal totalPortfolioValue)
        {
            return totalPortfolioValue != 0 ? (MarketValue / totalPortfolioValue) * 100 : 0;
        }
    }
    
    /// <summary>
    /// Represents an order (pending, filled, or cancelled)
    /// </summary>
    public class Order
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string BrokerOrderId { get; set; } = "";
        public string Symbol { get; set; } = "";
        public OrderSide Side { get; set; }
        public OrderType Type { get; set; }
        public OrderStatus Status { get; set; }
        
        // Quantity and pricing
        public decimal Quantity { get; set; }
        public decimal? LimitPrice { get; set; }
        public decimal? StopPrice { get; set; }
        public decimal FilledQuantity { get; set; }
        public decimal AverageFilledPrice { get; set; }
        
        // Timestamps
        public DateTime CreatedAt { get; set; }
        public DateTime? FilledAt { get; set; }
        public DateTime? CancelledAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
        
        // Additional properties
        public string TimeInForce { get; set; } = "GTC"; // GTC, DAY, IOC, FOK
        public bool IsExtendedHours { get; set; }
        
        /// <summary>
        /// Get remaining unfilled quantity
        /// </summary>
        public decimal RemainingQuantity => Quantity - FilledQuantity;
        
        /// <summary>
        /// Check if order is still active
        /// </summary>
        public bool IsActive => Status == OrderStatus.Pending || Status == OrderStatus.PartiallyFilled;
    }
    
    /// <summary>
    /// Represents a completed transaction
    /// </summary>
    public class Transaction
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string BrokerTransactionId { get; set; } = "";
        public string Symbol { get; set; } = "";
        public TransactionType Type { get; set; }
        public decimal Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal Amount => Quantity * Price;
        public decimal Commission { get; set; }
        public decimal Fees { get; set; }
        public decimal NetAmount => Amount + Commission + Fees;
        public DateTime ExecutedAt { get; set; }
        public string Description { get; set; } = "";
    }
    
    /// <summary>
    /// Portfolio performance summary for a specific period
    /// </summary>
    public class PortfolioPerformance
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public decimal StartValue { get; set; }
        public decimal EndValue { get; set; }
        public decimal NetDeposits { get; set; }
        public decimal TotalReturn { get; set; }
        public decimal TotalReturnPercent { get; set; }
        public decimal AnnualizedReturn { get; set; }
        public decimal Volatility { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxDrawdown { get; set; }
        public decimal MaxDrawdownPercent { get; set; }
        public List<DailyPerformance> DailyReturns { get; set; } = new();
    }
    
    /// <summary>
    /// Daily portfolio performance snapshot
    /// </summary>
    public class DailyPerformance
    {
        public DateTime Date { get; set; }
        public decimal PortfolioValue { get; set; }
        public decimal DayReturn { get; set; }
        public decimal DayReturnPercent { get; set; }
        public decimal CumulativeReturn { get; set; }
        public decimal CumulativeReturnPercent { get; set; }
    }
    
    /// <summary>
    /// Broker connection configuration
    /// </summary>
    public class BrokerConfig
    {
        public string BrokerName { get; set; } = "";
        public string ApiUrl { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string SecretKey { get; set; } = "";
        public string AccountId { get; set; } = "";
        public bool IsLiveTrading { get; set; } = false;
        public bool IsEnabled { get; set; } = true;
        public int RefreshIntervalSeconds { get; set; } = 30;
        public DateTime LastConnected { get; set; }
        public string Environment { get; set; } = "";
        public string BaseUrl { get; set; } = "";
        public Dictionary<string, string> AdditionalSettings { get; set; } = new();
    }
    
    /// <summary>
    /// Portfolio sync status and metrics
    /// </summary>
    public class PortfolioSyncStatus
    {
        public string BrokerName { get; set; } = "";
        public DateTime LastSyncTime { get; set; }
        public SyncStatus Status { get; set; }
        public string ErrorMessage { get; set; } = "";
        public int PositionsCount { get; set; }
        public int OrdersCount { get; set; }
        public int TransactionsCount { get; set; }
        public TimeSpan SyncDuration { get; set; }
    }
    
    #region Enums
    
    public enum OrderSide
    {
        Buy,
        Sell,
        SellShort,
        BuyToCover
    }
    
    public enum OrderType
    {
        Market,
        Limit,
        Stop,
        StopLimit,
        TrailingStop,
        TrailingStopLimit
    }
    
    public enum OrderStatus
    {
        Pending,
        PartiallyFilled,
        Filled,
        Cancelled,
        Rejected,
        Expired
    }
    
    public enum TransactionType
    {
        Buy,
        Sell,
        Dividend,
        Interest,
        Fee,
        Deposit,
        Withdrawal,
        StockSplit,
        StockDividend,
        SpinOff
    }
    
    public enum SyncStatus
    {
        Connected,
        Syncing,
        Disconnected,
        Error,
        AuthenticationFailed,
        RateLimited
    }
    
    #endregion
    
    #region Validation Attributes
    
    /// <summary>
    /// Custom validation for portfolio models
    /// </summary>
    public class PortfolioValidationAttribute : ValidationAttribute
    {
        public override bool IsValid(object? value)
        {
            if (value is Portfolio portfolio)
            {
                return !string.IsNullOrWhiteSpace(portfolio.Name) && 
                       !string.IsNullOrWhiteSpace(portfolio.AccountId);
            }
            return false;
        }
        
        public override string FormatErrorMessage(string name)
        {
            return $"Portfolio must have a valid name and account ID.";
        }
    }
    
    #endregion
    
    #region Real-Time Portfolio Models
    
    /// <summary>
    /// Portfolio position model for real-time updates
    /// </summary>
    public class PortfolioPosition : System.ComponentModel.INotifyPropertyChanged
    {
        private decimal _currentPrice;
        private decimal _marketValue;
        private decimal _gainLoss;
        private decimal _gainLossPercent;
        private DateTime _lastUpdated;

        public string Symbol { get; set; }
        public decimal Quantity { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal CostBasis { get; set; }

        public decimal CurrentPrice
        {
            get => _currentPrice;
            set
            {
                _currentPrice = value;
                OnPropertyChanged();
            }
        }

        public decimal MarketValue
        {
            get => _marketValue;
            set
            {
                _marketValue = value;
                OnPropertyChanged();
            }
        }

        public decimal GainLoss
        {
            get => _gainLoss;
            set
            {
                _gainLoss = value;
                OnPropertyChanged();
            }
        }

        public decimal GainLossPercent
        {
            get => _gainLossPercent;
            set
            {
                _gainLossPercent = value;
                OnPropertyChanged();
            }
        }

        public DateTime LastUpdated
        {
            get => _lastUpdated;
            set
            {
                _lastUpdated = value;
                OnPropertyChanged();
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Real-time subscription tracking for portfolio positions
    /// </summary>
    public class RealTimeSubscription
    {
        public string Symbol { get; set; }
        public Extensions.RealTime.RealTimeDataType DataType { get; set; }
        public DateTime SubscribedAt { get; set; }
    }

    /// <summary>
    /// Event args for portfolio updates from real-time data
    /// </summary>
    public class PortfolioUpdatedEventArgs : EventArgs
    {
        public Portfolio Portfolio { get; set; }
        public PortfolioPosition UpdatedPosition { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalGainLoss { get; set; }
        public PortfolioUpdateType UpdateType { get; set; }
    }

    /// <summary>
    /// Types of portfolio updates for real-time tracking
    /// </summary>
    public enum PortfolioUpdateType
    {
        PriceUpdate,
        PositionAdded,
        PositionRemoved,
        TotalCalculation
    }
    
    #endregion
}
