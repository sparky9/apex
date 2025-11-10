using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Backtesting.Models;
using ApexV2.Backtesting.StrategyGeneration;
using ApexV2.Backtesting.Engine;

namespace ApexV2.Backtesting.RealTime
{
    /// <summary>
    /// Monitors strategies in real-time and generates trading signals
    /// </summary>
    public class RealTimeStrategyMonitor
    {
        private readonly IDataFeed _dataFeed;
        private readonly PaperTradingEngine _tradingEngine;
        private readonly Strategy _strategy;
        private readonly Dictionary<string, List<BarDataEventArgs>> _barHistory = new Dictionary<string, List<BarDataEventArgs>>();
        private readonly int _maxHistoryBars = 500;
        private bool _isMonitoring;

        public event EventHandler<SignalGeneratedEventArgs> OnSignalGenerated;
        public event EventHandler<MonitoringStatusEventArgs> OnStatusChanged;

        public bool IsMonitoring => _isMonitoring;
        public PaperTradingEngine TradingEngine => _tradingEngine;
        public Strategy Strategy => _strategy;

        public RealTimeStrategyMonitor(IDataFeed dataFeed, Strategy strategy, double initialCapital)
        {
            _dataFeed = dataFeed ?? throw new ArgumentNullException(nameof(dataFeed));
            _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
            _tradingEngine = new PaperTradingEngine(initialCapital);

            _dataFeed.OnBarReceived += DataFeed_OnBarReceived;
            _dataFeed.OnConnectionStatusChanged += DataFeed_OnConnectionStatusChanged;
        }

        /// <summary>
        /// Start monitoring with the specified symbols
        /// </summary>
        public async Task<bool> StartMonitoringAsync(string[] symbols, TimeFrame timeFrame)
        {
            if (_isMonitoring)
                return false;

            if (!_dataFeed.IsConnected)
            {
                var connected = await _dataFeed.ConnectAsync();
                if (!connected)
                {
                    RaiseStatusChanged("Failed to connect to data feed", false);
                    return false;
                }
            }

            // Subscribe to symbols
            foreach (var symbol in symbols)
            {
                var subscribed = await _dataFeed.SubscribeAsync(symbol, timeFrame);
                if (!subscribed)
                {
                    RaiseStatusChanged($"Failed to subscribe to {symbol}", false);
                    return false;
                }

                _barHistory[symbol] = new List<BarDataEventArgs>();
            }

            _isMonitoring = true;
            RaiseStatusChanged($"Monitoring {symbols.Length} symbol(s)", true);

            return true;
        }

        /// <summary>
        /// Stop monitoring
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
                return;

            _isMonitoring = false;

            // Unsubscribe from all symbols
            foreach (var symbol in _barHistory.Keys.ToList())
            {
                await _dataFeed.UnsubscribeAsync(symbol);
            }

            _barHistory.Clear();

            RaiseStatusChanged("Monitoring stopped", false);
        }

        private void DataFeed_OnBarReceived(object sender, BarDataEventArgs e)
        {
            if (!_isMonitoring)
                return;

            // Add bar to history
            if (!_barHistory.ContainsKey(e.Symbol))
            {
                _barHistory[e.Symbol] = new List<BarDataEventArgs>();
            }

            _barHistory[e.Symbol].Add(e);

            // Limit history size
            if (_barHistory[e.Symbol].Count > _maxHistoryBars)
            {
                _barHistory[e.Symbol].RemoveAt(0);
            }

            // Check if we have enough bars to evaluate strategy
            if (_barHistory[e.Symbol].Count < 50) // Minimum bars for most indicators
                return;

            // Evaluate strategy and generate signals
            EvaluateStrategy(e.Symbol, e.Close);
        }

        private void EvaluateStrategy(string symbol, double currentPrice)
        {
            try
            {
                // Convert bar history to market data format
                var marketData = ConvertToMarketData(symbol);

                // Calculate indicators
                var indicatorCalculator = new IndicatorCalculator(marketData);
                var indicators = indicatorCalculator.CalculateStrategyIndicators(_strategy);

                // Evaluate entry and exit rules
                int currentBar = marketData.Close.Length - 1;
                bool entrySignal = EvaluateRules(_strategy.EntryRules, indicators, currentBar);
                bool exitSignal = EvaluateRules(_strategy.ExitRules, indicators, currentBar);

                // Check if we have an open position
                bool hasPosition = _tradingEngine.Positions.ContainsKey(symbol);

                SignalType signalType = SignalType.None;
                OrderResult orderResult = null;

                if (entrySignal && !hasPosition)
                {
                    // Generate buy signal
                    signalType = SignalType.Buy;
                    double shares = CalculatePositionSize(currentPrice);

                    if (shares > 0)
                    {
                        orderResult = _tradingEngine.ExecuteMarketOrder(symbol, OrderSide.Buy, shares, currentPrice);
                    }
                }
                else if (exitSignal && hasPosition)
                {
                    // Generate sell signal
                    signalType = SignalType.Sell;
                    double shares = _tradingEngine.Positions[symbol].Shares;

                    orderResult = _tradingEngine.ExecuteMarketOrder(symbol, OrderSide.Sell, shares, currentPrice);
                }

                // Update positions with current prices
                var currentPrices = new Dictionary<string, double> { { symbol, currentPrice } };
                _tradingEngine.UpdatePositions(currentPrices);

                // Raise signal event if signal was generated
                if (signalType != SignalType.None)
                {
                    OnSignalGenerated?.Invoke(this, new SignalGeneratedEventArgs
                    {
                        Symbol = symbol,
                        SignalType = signalType,
                        Price = currentPrice,
                        Timestamp = DateTime.Now,
                        OrderResult = orderResult
                    });
                }
            }
            catch (Exception ex)
            {
                RaiseStatusChanged($"Error evaluating strategy: {ex.Message}", true);
            }
        }

        private bool EvaluateRules(List<StrategyRule> rules, Dictionary<string, double[]> indicators, int bar)
        {
            if (rules == null || rules.Count == 0)
                return false;

            // For simplicity, use AND logic (all rules must be true)
            foreach (var rule in rules)
            {
                if (!EvaluateRule(rule, indicators, bar))
                    return false;
            }

            return true;
        }

        private bool EvaluateRule(StrategyRule rule, Dictionary<string, double[]> indicators, int bar)
        {
            // Get indicator values
            string indicatorKey = GetIndicatorKey(rule);
            if (!indicators.TryGetValue(indicatorKey, out double[] values))
                return false;

            if (bar >= values.Length || double.IsNaN(values[bar]))
                return false;

            double currentValue = values[bar];

            // Evaluate based on condition
            return rule.Condition switch
            {
                RuleCondition.Above => currentValue > GetThreshold(rule),
                RuleCondition.Below => currentValue < GetThreshold(rule),
                RuleCondition.Oversold => currentValue < GetThreshold(rule, "oversold", 30.0),
                RuleCondition.Overbought => currentValue > GetThreshold(rule, "overbought", 70.0),
                RuleCondition.CrossAbove => bar > 0 && values[bar - 1] <= GetThreshold(rule) && currentValue > GetThreshold(rule),
                RuleCondition.CrossBelow => bar > 0 && values[bar - 1] >= GetThreshold(rule) && currentValue < GetThreshold(rule),
                _ => false
            };
        }

        private string GetIndicatorKey(StrategyRule rule)
        {
            // Build indicator key based on rule parameters
            if (rule.Parameters.Count == 0)
                return rule.IndicatorName;

            var window = rule.Parameters.ContainsKey("window") ? rule.Parameters["window"].ToString() : "";
            return string.IsNullOrEmpty(window) ? rule.IndicatorName : $"{rule.IndicatorName}_{window}";
        }

        private double GetThreshold(StrategyRule rule, string key = "threshold", double defaultValue = 0.0)
        {
            if (rule.Parameters.TryGetValue(key, out object value))
            {
                return Convert.ToDouble(value);
            }
            return defaultValue;
        }

        private double CalculatePositionSize(double currentPrice)
        {
            // Simple position sizing: use 10% of available cash
            double targetValue = _tradingEngine.Cash * 0.1;
            return Math.Floor(targetValue / currentPrice);
        }

        private MarketData ConvertToMarketData(string symbol)
        {
            var bars = _barHistory[symbol];
            int count = bars.Count;

            var marketData = new MarketData
            {
                Symbol = symbol,
                Timestamps = new DateTime[count],
                Open = new double[count],
                High = new double[count],
                Low = new double[count],
                Close = new double[count],
                Volume = new double[count]
            };

            for (int i = 0; i < count; i++)
            {
                marketData.Timestamps[i] = bars[i].Timestamp;
                marketData.Open[i] = bars[i].Open;
                marketData.High[i] = bars[i].High;
                marketData.Low[i] = bars[i].Low;
                marketData.Close[i] = bars[i].Close;
                marketData.Volume[i] = bars[i].Volume;
            }

            return marketData;
        }

        private void DataFeed_OnConnectionStatusChanged(object sender, ConnectionStatusEventArgs e)
        {
            RaiseStatusChanged(e.Message, e.IsConnected);
        }

        private void RaiseStatusChanged(string message, bool isActive)
        {
            OnStatusChanged?.Invoke(this, new MonitoringStatusEventArgs
            {
                Message = message,
                IsActive = isActive,
                Timestamp = DateTime.Now
            });
        }
    }

    #region Event Args

    public class SignalGeneratedEventArgs : EventArgs
    {
        public string Symbol { get; set; }
        public SignalType SignalType { get; set; }
        public double Price { get; set; }
        public DateTime Timestamp { get; set; }
        public OrderResult OrderResult { get; set; }
    }

    public class MonitoringStatusEventArgs : EventArgs
    {
        public string Message { get; set; }
        public bool IsActive { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum SignalType
    {
        None,
        Buy,
        Sell
    }

    #endregion
}
