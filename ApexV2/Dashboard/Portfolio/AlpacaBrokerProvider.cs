#nullable disable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ApexV2.Charts.Export;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Alpaca Markets broker provider implementation
    /// </summary>
    public class AlpacaBrokerProvider : BrokerProviderBase
    {
        private readonly HttpClient _httpClient;
        private readonly IChartLogger _logger;
        private string _baseUrl;
        private Timer _refreshTimer;
        
        public override string BrokerName => "Alpaca";
        
        public AlpacaBrokerProvider(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "ApexV2/1.0");
        }
        
        protected override async Task<bool> ConnectImplementationAsync(BrokerConfig config, CancellationToken cancellationToken)
        {
            try
            {
                _baseUrl = config.IsLiveTrading 
                    ? "https://api.alpaca.markets" 
                    : "https://paper-api.alpaca.markets";
                
                // Set authentication headers
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("APCA-API-KEY-ID", config.ApiKey);
                _httpClient.DefaultRequestHeaders.Add("APCA-API-SECRET-KEY", config.SecretKey);
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "ApexV2/1.0");
                
                // Test connection by getting account info
                var accountResponse = await _httpClient.GetAsync($"{_baseUrl}/v2/account", cancellationToken);
                
                if (accountResponse.IsSuccessStatusCode)
                {
                    var accountJson = await accountResponse.Content.ReadAsStringAsync(cancellationToken);
                    var account = JsonSerializer.Deserialize<AlpacaAccount>(accountJson, GetJsonOptions());
                    
                    _logger.Info($"Connected to Alpaca - Account: {account.AccountNumber}");
                    
                    // Start refresh timer
                    if (config.RefreshIntervalSeconds > 0)
                    {
                        _refreshTimer = new Timer(RefreshPortfolio, null, 
                            TimeSpan.FromSeconds(config.RefreshIntervalSeconds),
                            TimeSpan.FromSeconds(config.RefreshIntervalSeconds));
                    }
                    
                    return true;
                }
                else
                {
                    var error = await accountResponse.Content.ReadAsStringAsync(cancellationToken);
                    _logger.Error($"Alpaca authentication failed: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Alpaca connection error: {ex.Message}");
                return false;
            }
        }
        
        protected override async Task DisconnectImplementationAsync()
        {
            _refreshTimer?.Dispose();
            _refreshTimer = null;
            _logger.Info("Disconnected from Alpaca");
        }
        
        protected override async Task<bool> TestConnectionImplementationAsync(CancellationToken cancellationToken)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v2/account", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
        
        public override async Task<Portfolio> GetPortfolioAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                // Get account information
                var accountResponse = await _httpClient.GetAsync($"{_baseUrl}/v2/account", cancellationToken);
                accountResponse.EnsureSuccessStatusCode();
                
                var accountJson = await accountResponse.Content.ReadAsStringAsync(cancellationToken);
                var account = JsonSerializer.Deserialize<AlpacaAccount>(accountJson, GetJsonOptions());
                
                // Get positions
                var positions = await GetPositionsAsync(cancellationToken);
                
                // Get orders
                var orders = await GetOrdersAsync(cancellationToken);
                
                // Create portfolio
                var portfolio = new Portfolio
                {
                    Name = $"Alpaca - {account.AccountNumber}",
                    BrokerName = BrokerName,
                    AccountId = account.AccountNumber,
                    LastUpdated = DateTime.Now,
                    TotalValue = decimal.Parse(account.Equity),
                    CashBalance = decimal.Parse(account.Cash),
                    BuyingPower = decimal.Parse(account.BuyingPower),
                    DayChange = decimal.Parse(account.LastDayPl),
                    Positions = positions,
                    Orders = orders
                };
                
                // Calculate derived metrics
                portfolio.TotalCost = positions.Sum(p => p.TotalCost);
                portfolio.TotalGainLoss = portfolio.TotalValue - portfolio.TotalCost;
                
                return portfolio;
            }
            catch (Exception ex)
            {
                HandleApiError(ex, "GetPortfolio");
                throw;
            }
        }
        
        public override async Task<List<Position>> GetPositionsAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v2/positions", cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var alpacaPositions = JsonSerializer.Deserialize<List<AlpacaPosition>>(json, GetJsonOptions());
                
                return alpacaPositions.Select(ConvertPosition).ToList();
            }
            catch (Exception ex)
            {
                HandleApiError(ex, "GetPositions");
                return new List<Position>();
            }
        }
        
        public override async Task<List<Order>> GetOrdersAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/v2/orders?status=all&limit=100", cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var alpacaOrders = JsonSerializer.Deserialize<List<AlpacaOrder>>(json, GetJsonOptions());
                
                return alpacaOrders.Select(ConvertOrder).ToList();
            }
            catch (Exception ex)
            {
                HandleApiError(ex, "GetOrders");
                return new List<Order>();
            }
        }
        
        public override async Task<List<Transaction>> GetTransactionsAsync(DateTime? startDate = null, DateTime? endDate = null, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_baseUrl}/v2/account/activities?activity_type=FILL";
                
                if (startDate.HasValue)
                    url += $"&date_start={startDate.Value:yyyy-MM-dd}";
                if (endDate.HasValue)
                    url += $"&date_end={endDate.Value:yyyy-MM-dd}";
                
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var activities = JsonSerializer.Deserialize<List<AlpacaActivity>>(json, GetJsonOptions());
                
                return activities.Select(ConvertActivity).ToList();
            }
            catch (Exception ex)
            {
                HandleApiError(ex, "GetTransactions");
                return new List<Transaction>();
            }
        }
        
        public override async Task<PortfolioPerformance> GetPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var url = $"{_baseUrl}/v2/account/portfolio/history?" +
                         $"period=1D&timeframe=1D&date_end={endDate:yyyy-MM-dd}&extended_hours=true";
                
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();
                
                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var history = JsonSerializer.Deserialize<AlpacaPortfolioHistory>(json, GetJsonOptions());
                
                return ConvertPortfolioHistory(history, startDate, endDate);
            }
            catch (Exception ex)
            {
                HandleApiError(ex, "GetPerformance");
                throw;
            }
        }
        
        public override async Task<List<DailyPerformance>> GetDailyPerformanceAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
        {
            try
            {
                var performance = await GetPerformanceAsync(startDate, endDate, cancellationToken);
                return performance.DailyReturns;
            }
            catch (Exception ex)
            {
                HandleApiError(ex, "GetDailyPerformance");
                return new List<DailyPerformance>();
            }
        }
        
        private async void RefreshPortfolio(object state)
        {
            try
            {
                if (!_isConnected) return;
                
                var portfolio = await GetPortfolioAsync();
                OnPortfolioUpdated(new BrokerPortfolioUpdatedEventArgs { Portfolio = portfolio });
            }
            catch (Exception ex)
            {
                _logger.Error($"Portfolio refresh error: {ex.Message}");
            }
        }
        
        #region Conversion Methods
        
        private Position ConvertPosition(AlpacaPosition alpacaPosition)
        {
            return new Position
            {
                Symbol = alpacaPosition.Symbol,
                Quantity = decimal.Parse(alpacaPosition.Qty),
                AverageCost = decimal.Parse(alpacaPosition.AvgEntryPrice),
                CurrentPrice = decimal.Parse(alpacaPosition.MarketValue) / decimal.Parse(alpacaPosition.Qty),
                UnrealizedGainLoss = decimal.Parse(alpacaPosition.UnrealizedPl),
                DayChange = decimal.Parse(alpacaPosition.UnrealizedIntradayPl),
                AssetType = alpacaPosition.AssetClass,
                LastUpdated = DateTime.Now
            };
        }
        
        private Order ConvertOrder(AlpacaOrder alpacaOrder)
        {
            return new Order
            {
                BrokerOrderId = alpacaOrder.Id,
                Symbol = alpacaOrder.Symbol,
                Side = alpacaOrder.Side.ToLower() == "buy" ? OrderSide.Buy : OrderSide.Sell,
                Type = ConvertOrderType(alpacaOrder.OrderType),
                Status = ConvertOrderStatus(alpacaOrder.Status),
                Quantity = decimal.Parse(alpacaOrder.Qty),
                LimitPrice = !string.IsNullOrEmpty(alpacaOrder.LimitPrice) ? decimal.Parse(alpacaOrder.LimitPrice) : null,
                StopPrice = !string.IsNullOrEmpty(alpacaOrder.StopPrice) ? decimal.Parse(alpacaOrder.StopPrice) : null,
                FilledQuantity = decimal.Parse(alpacaOrder.FilledQty),
                AverageFilledPrice = !string.IsNullOrEmpty(alpacaOrder.FilledAvgPrice) ? decimal.Parse(alpacaOrder.FilledAvgPrice) : 0,
                CreatedAt = DateTime.Parse(alpacaOrder.CreatedAt),
                TimeInForce = alpacaOrder.TimeInForce
            };
        }
        
        private Transaction ConvertActivity(AlpacaActivity activity)
        {
            return new Transaction
            {
                BrokerTransactionId = activity.Id,
                Symbol = activity.Symbol,
                Type = activity.Side.ToLower() == "buy" ? TransactionType.Buy : TransactionType.Sell,
                Quantity = decimal.Parse(activity.Qty),
                Price = decimal.Parse(activity.Price),
                ExecutedAt = DateTime.Parse(activity.TransactionTime),
                Description = $"{activity.Side} {activity.Qty} {activity.Symbol} @ ${activity.Price}"
            };
        }
        
        private PortfolioPerformance ConvertPortfolioHistory(AlpacaPortfolioHistory history, DateTime startDate, DateTime endDate)
        {
            var dailyReturns = new List<DailyPerformance>();
            
            if (history.Equity?.Count > 0 && history.Timestamp?.Count > 0)
            {
                for (int i = 0; i < Math.Min(history.Equity.Count, history.Timestamp.Count); i++)
                {
                    var date = DateTimeOffset.FromUnixTimeSeconds(history.Timestamp[i]).DateTime;
                    var equity = decimal.Parse(history.Equity[i].ToString());
                    
                    var dayReturn = i > 0 ? equity - decimal.Parse(history.Equity[i-1].ToString()) : 0;
                    var dayReturnPercent = i > 0 && decimal.Parse(history.Equity[i-1].ToString()) > 0 
                        ? (dayReturn / decimal.Parse(history.Equity[i-1].ToString())) * 100 
                        : 0;
                    
                    dailyReturns.Add(new DailyPerformance
                    {
                        Date = date,
                        PortfolioValue = equity,
                        DayReturn = dayReturn,
                        DayReturnPercent = dayReturnPercent
                    });
                }
            }
            
            var startValue = dailyReturns.FirstOrDefault()?.PortfolioValue ?? 0;
            var endValue = dailyReturns.LastOrDefault()?.PortfolioValue ?? 0;
            var totalReturn = endValue - startValue;
            var totalReturnPercent = startValue > 0 ? (totalReturn / startValue) * 100 : 0;
            
            return new PortfolioPerformance
            {
                StartDate = startDate,
                EndDate = endDate,
                StartValue = startValue,
                EndValue = endValue,
                TotalReturn = totalReturn,
                TotalReturnPercent = totalReturnPercent,
                DailyReturns = dailyReturns
            };
        }
        
        private OrderType ConvertOrderType(string alpacaType)
        {
            return alpacaType.ToLower() switch
            {
                "market" => OrderType.Market,
                "limit" => OrderType.Limit,
                "stop" => OrderType.Stop,
                "stop_limit" => OrderType.StopLimit,
                "trailing_stop" => OrderType.TrailingStop,
                _ => OrderType.Market
            };
        }
        
        private OrderStatus ConvertOrderStatus(string alpacaStatus)
        {
            return alpacaStatus.ToLower() switch
            {
                "new" => OrderStatus.Pending,
                "partially_filled" => OrderStatus.PartiallyFilled,
                "filled" => OrderStatus.Filled,
                "canceled" => OrderStatus.Cancelled,
                "rejected" => OrderStatus.Rejected,
                "expired" => OrderStatus.Expired,
                _ => OrderStatus.Pending
            };
        }
        
        #endregion
        
        #region Helper Methods
        
        private JsonSerializerOptions GetJsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _refreshTimer?.Dispose();
                _httpClient?.Dispose();
            }
            base.Dispose(disposing);
        }
        
        #endregion
    }
    
    #region Alpaca API Models
    
    internal class AlpacaAccount
    {
        public string AccountNumber { get; set; }
        public string Status { get; set; }
        public string Currency { get; set; }
        public string Cash { get; set; }
        public string PortfolioValue { get; set; }
        public string PatternDayTrader { get; set; }
        public string TradingBlocked { get; set; }
        public string TransfersBlocked { get; set; }
        public string AccountBlocked { get; set; }
        public string CreatedAt { get; set; }
        public string TradeSuspendedByUser { get; set; }
        public string Multiplier { get; set; }
        public string BuyingPower { get; set; }
        public string RgtBuyingPower { get; set; }
        public string DaytradingBuyingPower { get; set; }
        public string NonMarginableBuyingPower { get; set; }
        public string LongMarketValue { get; set; }
        public string ShortMarketValue { get; set; }
        public string Equity { get; set; }
        public string LastEquity { get; set; }
        public string InitialMargin { get; set; }
        public string MaintenanceMargin { get; set; }
        public string LastMaintenanceMargin { get; set; }
        public string Sma { get; set; }
        public string DaytradeCount { get; set; }
        public string LastDayPl { get; set; }
    }
    
    internal class AlpacaPosition
    {
        public string AssetId { get; set; }
        public string Symbol { get; set; }
        public string Exchange { get; set; }
        public string AssetClass { get; set; }
        public string AssetMarginable { get; set; }
        public string Qty { get; set; }
        public string AvgEntryPrice { get; set; }
        public string Side { get; set; }
        public string MarketValue { get; set; }
        public string CostBasis { get; set; }
        public string UnrealizedPl { get; set; }
        public string UnrealizedPlpc { get; set; }
        public string UnrealizedIntradayPl { get; set; }
        public string UnrealizedIntradayPlpc { get; set; }
        public string CurrentPrice { get; set; }
        public string LastdayPrice { get; set; }
        public string ChangeToday { get; set; }
    }
    
    internal class AlpacaOrder
    {
        public string Id { get; set; }
        public string ClientOrderId { get; set; }
        public string CreatedAt { get; set; }
        public string UpdatedAt { get; set; }
        public string SubmittedAt { get; set; }
        public string FilledAt { get; set; }
        public string ExpiredAt { get; set; }
        public string CanceledAt { get; set; }
        public string FailedAt { get; set; }
        public string ReplacedAt { get; set; }
        public string ReplacedBy { get; set; }
        public string Replaces { get; set; }
        public string AssetId { get; set; }
        public string Symbol { get; set; }
        public string AssetClass { get; set; }
        public string Notional { get; set; }
        public string Qty { get; set; }
        public string FilledQty { get; set; }
        public string FilledAvgPrice { get; set; }
        public string OrderClass { get; set; }
        public string OrderType { get; set; }
        public string Type { get; set; }
        public string Side { get; set; }
        public string TimeInForce { get; set; }
        public string LimitPrice { get; set; }
        public string StopPrice { get; set; }
        public string Status { get; set; }
        public string ExtendedHours { get; set; }
        public string Legs { get; set; }
        public string TrailPercent { get; set; }
        public string TrailPrice { get; set; }
        public string Hwm { get; set; }
    }
    
    internal class AlpacaActivity
    {
        public string Id { get; set; }
        public string ActivityType { get; set; }
        public string TransactionTime { get; set; }
        public string Type { get; set; }
        public string Price { get; set; }
        public string Qty { get; set; }
        public string Side { get; set; }
        public string Symbol { get; set; }
        public string LeavesQty { get; set; }
        public string CumQty { get; set; }
        public string OrderId { get; set; }
    }
    
    internal class AlpacaPortfolioHistory
    {
        public List<long> Timestamp { get; set; } = new();
        public List<object> Equity { get; set; } = new();
        public List<object> ProfitLoss { get; set; } = new();
        public List<object> ProfitLossPct { get; set; } = new();
        public string BaseValue { get; set; }
        public string TimeFrame { get; set; }
    }
    
    #endregion
}
