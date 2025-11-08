using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApexV2.Data.Trading;

/// <summary>
/// Unified interface for both paper trading and live trading
/// </summary>
public interface ITradingProvider
{
    string ProviderName { get; }
    bool IsConnected { get; }
    bool IsPaperTrading { get; }
    decimal AccountBalance { get; }
    
    Task<bool> ConnectAsync();
    Task DisconnectAsync();
    
    // Account Information
    Task<Account> GetAccountAsync();
    Task<List<Position>> GetPositionsAsync();
    Task<List<Order>> GetOrdersAsync();
    Task<List<Order>> GetOrderHistoryAsync(DateTime? startDate = null);
    
    // Trading Operations
    Task<Order> PlaceOrderAsync(OrderRequest orderRequest);
    Task<Order> CancelOrderAsync(string orderId);
    Task<Order> ModifyOrderAsync(string orderId, OrderRequest orderRequest);
    
    // Portfolio Analysis
    Task<PortfolioPerformance> GetPerformanceAsync();
    Task<List<Transaction>> GetTransactionsAsync(DateTime? startDate = null);
}

public class Account
{
    public string AccountId { get; set; } = string.Empty;
    public decimal Cash { get; set; }
    public decimal MarketValue { get; set; }
    public decimal TotalEquity => Cash + MarketValue;
    public decimal BuyingPower { get; set; }
    public decimal DayTradeCount { get; set; }
    public bool IsPatternDayTrader { get; set; }
    public AccountStatus Status { get; set; }
}

public enum AccountStatus
{
    Active,
    Restricted,
    Frozen,
    Closed
}

public class Position
{
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue => Quantity * CurrentPrice;
    public decimal UnrealizedPL => (CurrentPrice - AveragePrice) * Quantity;
    public decimal UnrealizedPLPercent => AveragePrice == 0 ? 0 : (UnrealizedPL / (AveragePrice * Math.Abs(Quantity))) * 100;
}

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public OrderSide Side { get; set; }
    public OrderType Type { get; set; }
    public decimal? LimitPrice { get; set; }
    public decimal? StopPrice { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? FilledAt { get; set; }
    public decimal FilledQuantity { get; set; }
    public decimal? FilledPrice { get; set; }
}

public enum OrderSide
{
    Buy,
    Sell
}

public enum OrderType
{
    Market,
    Limit,
    Stop,
    StopLimit,
    TrailingStop
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

public class OrderRequest
{
    public string Symbol { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public OrderSide Side { get; set; }
    public OrderType Type { get; set; }
    public decimal? LimitPrice { get; set; }
    public decimal? StopPrice { get; set; }
    public TimeInForce TimeInForce { get; set; } = TimeInForce.Day;
}

public enum TimeInForce
{
    Day,
    GTC,  // Good Till Cancelled
    IOC,  // Immediate or Cancel
    FOK   // Fill or Kill
}

public class Transaction
{
    public string TransactionId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Amount { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTime Date { get; set; }
}

public enum TransactionType
{
    Buy,
    Sell,
    Dividend,
    Interest,
    Fee,
    Deposit,
    Withdrawal
}

public class PortfolioPerformance
{
    public decimal TotalReturn { get; set; }
    public decimal TotalReturnPercent { get; set; }
    public decimal DayReturn { get; set; }
    public decimal DayReturnPercent { get; set; }
    public decimal WeekReturn { get; set; }
    public decimal MonthReturn { get; set; }
    public decimal YearReturn { get; set; }
    public decimal Sharpe { get; set; }
    public decimal MaxDrawdown { get; set; }
    public int WinRate { get; set; }
}