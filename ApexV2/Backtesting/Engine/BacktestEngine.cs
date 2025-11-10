using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Backtesting.Models;
using ApexV2.Indicators.Backtesting;
using ApexV2.Indicators.Backtesting.Volatility;

namespace ApexV2.Backtesting.Engine
{
    /// <summary>
    /// Core backtesting engine that simulates trading strategies
    /// </summary>
    public class BacktestEngine
    {
        private readonly BacktestParameters _parameters;
        private readonly PositionManager _positionManager;
        private readonly RiskManager _riskManager;
        private readonly PerformanceAnalyzer _performanceAnalyzer;

        // State
        private double _currentCash;
        private double _currentEquity;
        private double _peakEquity;
        private List<Trade> _closedTrades;
        private List<EquityPoint> _equityCurve;
        private List<DrawdownPoint> _drawdownCurve;

        public BacktestEngine(BacktestParameters parameters)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
            _positionManager = new PositionManager();
            _riskManager = new RiskManager(parameters);
            _performanceAnalyzer = new PerformanceAnalyzer();

            Initialize();
        }

        /// <summary>
        /// Initialize the backtesting engine
        /// </summary>
        private void Initialize()
        {
            _currentCash = _parameters.InitialCapital;
            _currentEquity = _parameters.InitialCapital;
            _peakEquity = _parameters.InitialCapital;
            _closedTrades = new List<Trade>();
            _equityCurve = new List<EquityPoint>();
            _drawdownCurve = new List<DrawdownPoint>();
        }

        /// <summary>
        /// Run a backtest with the given market data and strategy signals
        /// </summary>
        public BacktestResults RunBacktest(
            MarketData marketData,
            StrategySignals signals,
            Dictionary<string, double[]> indicatorValues)
        {
            if (marketData == null || signals == null)
                throw new ArgumentNullException("Market data and signals are required");

            if (marketData.Dates.Length != marketData.Closes.Length)
                throw new ArgumentException("Market data arrays must have same length");

            // Get ATR for risk management if available
            double[]? atrValues = null;
            if (indicatorValues.ContainsKey("atr"))
            {
                atrValues = indicatorValues["atr"];
            }

            // Process each bar
            for (int i = 0; i < marketData.Dates.Length; i++)
            {
                DateTime currentDate = marketData.Dates[i];
                double currentPrice = marketData.Closes[i];
                double? currentATR = atrValues != null && i < atrValues.Length ? atrValues[i] : null;

                // Update existing positions
                UpdatePositions(currentDate, currentPrice, currentATR);

                // Check for exit signals on open positions
                if (_positionManager.HasOpenPosition)
                {
                    if (signals.ExitSignals[i])
                    {
                        ExitPosition(currentDate, currentPrice, "Exit Signal");
                    }
                }

                // Check for entry signals if no position
                if (!_positionManager.HasOpenPosition && signals.EntrySignals[i])
                {
                    double shares = CalculatePositionSize(currentPrice, currentATR);
                    if (shares > 0)
                    {
                        EnterPosition(currentDate, currentPrice, shares, currentATR, "Entry Signal");
                    }
                }

                // Record equity
                RecordEquityPoint(currentDate);
            }

            // Close any remaining open positions at end of backtest
            if (_positionManager.HasOpenPosition)
            {
                var lastDate = marketData.Dates[marketData.Dates.Length - 1];
                var lastPrice = marketData.Closes[marketData.Closes.Length - 1];
                ExitPosition(lastDate, lastPrice, "End of Backtest");
            }

            // Calculate final metrics
            return GenerateResults();
        }

        /// <summary>
        /// Enter a new position
        /// </summary>
        private void EnterPosition(DateTime date, double price, double shares, double? atr, string reason)
        {
            // Calculate costs
            double entryValue = price * shares;
            double commission = entryValue * _parameters.Commission;
            double slippage = price * _parameters.Slippage * shares;
            double totalCost = entryValue + commission + slippage;

            // Check if we have enough cash
            if (totalCost > _currentCash)
            {
                return; // Not enough capital
            }

            // Create trade
            var trade = new Trade
            {
                Symbol = _parameters.Symbol,
                EntryDate = date,
                EntryPrice = price + (price * _parameters.Slippage), // Apply slippage
                Shares = shares,
                EntryValue = entryValue,
                EntryCommission = commission,
                EntryReason = reason
            };

            // Set risk management levels
            if (_parameters.UseStopLoss && atr.HasValue)
            {
                trade.StopLossPrice = ATR.CalculateStopLoss(
                    trade.EntryPrice,
                    atr.Value,
                    _parameters.StopLossATRMultiple,
                    isLong: true
                );
            }

            if (_parameters.UseTakeProfit && atr.HasValue)
            {
                trade.TakeProfitPrice = ATR.CalculateTakeProfit(
                    trade.EntryPrice,
                    atr.Value,
                    _parameters.TakeProfitATRMultiple,
                    isLong: true
                );
            }

            // Create position
            var position = new Position
            {
                Symbol = _parameters.Symbol,
                Trade = trade,
                CurrentPrice = price,
                CurrentStopLoss = trade.StopLossPrice,
                CurrentTakeProfit = trade.TakeProfitPrice
            };

            // Update state
            _currentCash -= totalCost;
            _positionManager.OpenPosition(position);
        }

        /// <summary>
        /// Exit current position
        /// </summary>
        private void ExitPosition(DateTime date, double price, string reason)
        {
            var position = _positionManager.GetOpenPosition();
            if (position == null)
                return;

            // Calculate exit values
            double exitPrice = price - (price * _parameters.Slippage); // Apply slippage
            double exitValue = exitPrice * position.Trade.Shares;
            double commission = exitValue * _parameters.Commission;

            // Close the trade
            position.Trade.Close(date, exitPrice, commission, reason);

            // Update state
            _currentCash += exitValue - commission;
            _closedTrades.Add(position.Trade);
            _positionManager.ClosePosition();
        }

        /// <summary>
        /// Update existing positions with current market data
        /// </summary>
        private void UpdatePositions(DateTime date, double price, double? atr)
        {
            if (!_positionManager.HasOpenPosition)
                return;

            var position = _positionManager.GetOpenPosition();
            if (position == null)
                return;

            // Update position price
            position.UpdatePrice(price);

            // Check stop loss
            if (position.IsStopLossHit())
            {
                ExitPosition(date, price, "Stop Loss");
                return;
            }

            // Check take profit
            if (position.IsTakeProfitHit())
            {
                ExitPosition(date, price, "Take Profit");
                return;
            }

            // Update trailing stop
            if (_parameters.UseTrailingStop && atr.HasValue)
            {
                double newTrailingStop = ATR.CalculateTrailingStop(
                    price,
                    atr.Value,
                    _parameters.TrailingStopATRMultiple,
                    isLong: true,
                    previousStop: position.CurrentTrailingStop
                );

                position.CurrentTrailingStop = newTrailingStop;

                // Check if trailing stop is hit
                if (position.IsTrailingStopHit())
                {
                    ExitPosition(date, price, "Trailing Stop");
                    return;
                }
            }
        }

        /// <summary>
        /// Calculate position size based on configured method
        /// </summary>
        private double CalculatePositionSize(double price, double? atr)
        {
            double availableCash = _currentCash;
            double shares = 0;

            switch (_parameters.SizingMethod)
            {
                case PositionSizingMethod.FixedPercentage:
                    double targetValue = availableCash * _parameters.PositionSizePercent;
                    shares = Math.Floor(targetValue / price);
                    break;

                case PositionSizingMethod.ATRBased:
                    if (atr.HasValue && atr.Value > 0)
                    {
                        shares = ATR.CalculatePositionSize(
                            _currentEquity,
                            0.02, // 2% risk per trade
                            price,
                            atr.Value,
                            _parameters.StopLossATRMultiple
                        );
                    }
                    else
                    {
                        // Fallback to fixed percentage
                        double targetVal = availableCash * _parameters.PositionSizePercent;
                        shares = Math.Floor(targetVal / price);
                    }
                    break;

                case PositionSizingMethod.FixedDollar:
                    shares = Math.Floor(_parameters.MaxPositionSize / price);
                    break;

                default:
                    shares = 100; // Default fixed shares
                    break;
            }

            // Ensure we don't exceed available cash
            double totalCost = (price * shares) * (1 + _parameters.Commission + _parameters.Slippage);
            while (totalCost > availableCash && shares > 0)
            {
                shares--;
                totalCost = (price * shares) * (1 + _parameters.Commission + _parameters.Slippage);
            }

            return shares;
        }

        /// <summary>
        /// Record equity curve point
        /// </summary>
        private void RecordEquityPoint(DateTime date)
        {
            double positionValue = 0;
            if (_positionManager.HasOpenPosition)
            {
                var position = _positionManager.GetOpenPosition();
                positionValue = position?.CurrentValue ?? 0;
            }

            _currentEquity = _currentCash + positionValue;

            _equityCurve.Add(new EquityPoint
            {
                Date = date,
                Equity = _currentEquity,
                Cash = _currentCash,
                PositionValue = positionValue
            });

            // Update peak and calculate drawdown
            if (_currentEquity > _peakEquity)
            {
                _peakEquity = _currentEquity;
            }

            double drawdown = _peakEquity - _currentEquity;
            double drawdownPercent = _peakEquity > 0 ? (drawdown / _peakEquity) * 100 : 0;

            _drawdownCurve.Add(new DrawdownPoint
            {
                Date = date,
                Drawdown = drawdown,
                DrawdownPercent = drawdownPercent,
                IsNewPeak = _currentEquity == _peakEquity
            });
        }

        /// <summary>
        /// Generate final backtest results
        /// </summary>
        private BacktestResults GenerateResults()
        {
            var results = new BacktestResults
            {
                Symbol = _parameters.Symbol,
                StartDate = _parameters.StartDate,
                EndDate = _parameters.EndDate,
                InitialCapital = _parameters.InitialCapital,
                FinalCapital = _currentEquity,
                PeakCapital = _peakEquity,
                Trades = _closedTrades,
                EquityCurve = _equityCurve,
                DrawdownCurve = _drawdownCurve
            };

            // Calculate comprehensive metrics
            results.Metrics = _performanceAnalyzer.CalculateMetrics(results);

            return results;
        }
    }

    /// <summary>
    /// Market data for backtesting
    /// </summary>
    public class MarketData
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime[] Dates { get; set; } = Array.Empty<DateTime>();
        public double[] Opens { get; set; } = Array.Empty<double>();
        public double[] Highs { get; set; } = Array.Empty<double>();
        public double[] Lows { get; set; } = Array.Empty<double>();
        public double[] Closes { get; set; } = Array.Empty<double>();
        public double[] Volumes { get; set; } = Array.Empty<double>();
    }

    /// <summary>
    /// Strategy entry and exit signals
    /// </summary>
    public class StrategySignals
    {
        public bool[] EntrySignals { get; set; } = Array.Empty<bool>();
        public bool[] ExitSignals { get; set; } = Array.Empty<bool>();
        public string StrategyName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
