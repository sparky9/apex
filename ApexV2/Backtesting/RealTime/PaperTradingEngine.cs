using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.RealTime
{
    /// <summary>
    /// Paper trading engine for simulated real-time trading
    /// </summary>
    public class PaperTradingEngine
    {
        private readonly Dictionary<string, Position> _positions = new Dictionary<string, Position>();
        private readonly List<PaperTrade> _completedTrades = new List<PaperTrade>();
        private double _cash;
        private double _initialCapital;
        private readonly double _commission;
        private readonly double _slippage;

        public event EventHandler<TradeExecutedEventArgs> OnTradeExecuted;
        public event EventHandler<PositionUpdatedEventArgs> OnPositionUpdated;
        public event EventHandler<EquityUpdatedEventArgs> OnEquityUpdated;

        public double Cash => _cash;
        public double InitialCapital => _initialCapital;
        public double TotalEquity { get; private set; }
        public double UnrealizedPnL { get; private set; }
        public double RealizedPnL { get; private set; }
        public IReadOnlyDictionary<string, Position> Positions => _positions;
        public IReadOnlyList<PaperTrade> CompletedTrades => _completedTrades;

        public PaperTradingEngine(double initialCapital, double commission = 0.001, double slippage = 0.0005)
        {
            _initialCapital = initialCapital;
            _cash = initialCapital;
            _commission = commission;
            _slippage = slippage;
            TotalEquity = initialCapital;
        }

        /// <summary>
        /// Execute a market order
        /// </summary>
        public OrderResult ExecuteMarketOrder(string symbol, OrderSide side, double shares, double currentPrice)
        {
            // Apply slippage
            double executionPrice = side == OrderSide.Buy
                ? currentPrice * (1 + _slippage)
                : currentPrice * (1 - _slippage);

            double orderValue = shares * executionPrice;
            double commissionCost = orderValue * _commission;
            double totalCost = orderValue + commissionCost;

            // Check if we have enough cash for buy orders
            if (side == OrderSide.Buy && totalCost > _cash)
            {
                return new OrderResult
                {
                    Success = false,
                    Message = $"Insufficient funds. Required: ${totalCost:F2}, Available: ${_cash:F2}"
                };
            }

            // Execute the order
            if (side == OrderSide.Buy)
            {
                OpenPosition(symbol, shares, executionPrice, commissionCost);
            }
            else
            {
                ClosePosition(symbol, shares, executionPrice, commissionCost);
            }

            OnTradeExecuted?.Invoke(this, new TradeExecutedEventArgs
            {
                Symbol = symbol,
                Side = side,
                Shares = shares,
                Price = executionPrice,
                Commission = commissionCost,
                Timestamp = DateTime.Now
            });

            return new OrderResult
            {
                Success = true,
                ExecutionPrice = executionPrice,
                Commission = commissionCost,
                Message = $"{side} {shares} shares of {symbol} at ${executionPrice:F2}"
            };
        }

        /// <summary>
        /// Update positions with current market prices
        /// </summary>
        public void UpdatePositions(Dictionary<string, double> currentPrices)
        {
            UnrealizedPnL = 0;

            foreach (var position in _positions.Values)
            {
                if (currentPrices.TryGetValue(position.Symbol, out double currentPrice))
                {
                    position.CurrentPrice = currentPrice;
                    position.MarketValue = position.Shares * currentPrice;
                    position.UnrealizedPnL = (currentPrice - position.AveragePrice) * position.Shares - position.TotalCommission;

                    UnrealizedPnL += position.UnrealizedPnL;

                    OnPositionUpdated?.Invoke(this, new PositionUpdatedEventArgs
                    {
                        Position = position,
                        Timestamp = DateTime.Now
                    });
                }
            }

            TotalEquity = _cash + _positions.Values.Sum(p => p.MarketValue);

            OnEquityUpdated?.Invoke(this, new EquityUpdatedEventArgs
            {
                Cash = _cash,
                Equity = TotalEquity,
                UnrealizedPnL = UnrealizedPnL,
                RealizedPnL = RealizedPnL,
                Timestamp = DateTime.Now
            });
        }

        private void OpenPosition(string symbol, double shares, double price, double commission)
        {
            if (_positions.ContainsKey(symbol))
            {
                // Add to existing position
                var position = _positions[symbol];
                double totalCost = (position.AveragePrice * position.Shares) + (price * shares);
                position.Shares += shares;
                position.AveragePrice = totalCost / position.Shares;
                position.TotalCommission += commission;
                position.MarketValue = position.Shares * price;
            }
            else
            {
                // Create new position
                _positions[symbol] = new Position
                {
                    Symbol = symbol,
                    Shares = shares,
                    AveragePrice = price,
                    CurrentPrice = price,
                    MarketValue = shares * price,
                    TotalCommission = commission,
                    OpenTime = DateTime.Now
                };
            }

            _cash -= (shares * price + commission);
        }

        private void ClosePosition(string symbol, double shares, double price, double commission)
        {
            if (!_positions.TryGetValue(symbol, out Position position))
            {
                return; // No position to close
            }

            double sharesToClose = Math.Min(shares, position.Shares);
            double proceeds = sharesToClose * price - commission;
            double costBasis = sharesToClose * position.AveragePrice;
            double pnl = proceeds - costBasis - position.TotalCommission * (sharesToClose / position.Shares);

            // Create completed trade record
            var trade = new PaperTrade
            {
                Symbol = symbol,
                EntryPrice = position.AveragePrice,
                ExitPrice = price,
                Shares = sharesToClose,
                EntryTime = position.OpenTime,
                ExitTime = DateTime.Now,
                ProfitLoss = pnl,
                Commission = commission + position.TotalCommission * (sharesToClose / position.Shares)
            };
            _completedTrades.Add(trade);

            _cash += proceeds;
            RealizedPnL += pnl;

            // Update or remove position
            if (sharesToClose >= position.Shares)
            {
                _positions.Remove(symbol);
            }
            else
            {
                position.Shares -= sharesToClose;
                position.TotalCommission -= position.TotalCommission * (sharesToClose / position.Shares);
                position.MarketValue = position.Shares * position.CurrentPrice;
            }
        }

        /// <summary>
        /// Get current performance metrics
        /// </summary>
        public PaperTradingMetrics GetMetrics()
        {
            return new PaperTradingMetrics
            {
                InitialCapital = _initialCapital,
                CurrentEquity = TotalEquity,
                Cash = _cash,
                TotalReturn = (TotalEquity - _initialCapital) / _initialCapital,
                RealizedPnL = RealizedPnL,
                UnrealizedPnL = UnrealizedPnL,
                TotalPnL = RealizedPnL + UnrealizedPnL,
                CompletedTrades = _completedTrades.Count,
                WinningTrades = _completedTrades.Count(t => t.ProfitLoss > 0),
                LosingTrades = _completedTrades.Count(t => t.ProfitLoss <= 0),
                WinRate = _completedTrades.Count > 0
                    ? (double)_completedTrades.Count(t => t.ProfitLoss > 0) / _completedTrades.Count
                    : 0,
                ActivePositions = _positions.Count
            };
        }

        /// <summary>
        /// Reset the engine to initial state
        /// </summary>
        public void Reset()
        {
            _positions.Clear();
            _completedTrades.Clear();
            _cash = _initialCapital;
            TotalEquity = _initialCapital;
            UnrealizedPnL = 0;
            RealizedPnL = 0;
        }
    }

    #region Supporting Classes

    public class Position
    {
        public string Symbol { get; set; }
        public double Shares { get; set; }
        public double AveragePrice { get; set; }
        public double CurrentPrice { get; set; }
        public double MarketValue { get; set; }
        public double UnrealizedPnL { get; set; }
        public double TotalCommission { get; set; }
        public DateTime OpenTime { get; set; }
    }

    public class PaperTrade
    {
        public string Symbol { get; set; }
        public double EntryPrice { get; set; }
        public double ExitPrice { get; set; }
        public double Shares { get; set; }
        public DateTime EntryTime { get; set; }
        public DateTime ExitTime { get; set; }
        public double ProfitLoss { get; set; }
        public double Commission { get; set; }
        public double ReturnPct => (ExitPrice - EntryPrice) / EntryPrice;
    }

    public class OrderResult
    {
        public bool Success { get; set; }
        public double ExecutionPrice { get; set; }
        public double Commission { get; set; }
        public string Message { get; set; }
    }

    public enum OrderSide
    {
        Buy,
        Sell
    }

    public class PaperTradingMetrics
    {
        public double InitialCapital { get; set; }
        public double CurrentEquity { get; set; }
        public double Cash { get; set; }
        public double TotalReturn { get; set; }
        public double RealizedPnL { get; set; }
        public double UnrealizedPnL { get; set; }
        public double TotalPnL { get; set; }
        public int CompletedTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public double WinRate { get; set; }
        public int ActivePositions { get; set; }
    }

    public class TradeExecutedEventArgs : EventArgs
    {
        public string Symbol { get; set; }
        public OrderSide Side { get; set; }
        public double Shares { get; set; }
        public double Price { get; set; }
        public double Commission { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class PositionUpdatedEventArgs : EventArgs
    {
        public Position Position { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class EquityUpdatedEventArgs : EventArgs
    {
        public double Cash { get; set; }
        public double Equity { get; set; }
        public double UnrealizedPnL { get; set; }
        public double RealizedPnL { get; set; }
        public DateTime Timestamp { get; set; }
    }

    #endregion
}
