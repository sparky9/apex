using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ApexV2.Backtesting.RealTime
{
    /// <summary>
    /// Mock data feed for testing real-time features
    /// Generates realistic price movements
    /// </summary>
    public class MockDataFeed : IDataFeed
    {
        private bool _isConnected;
        private readonly Dictionary<string, SubscriptionInfo> _subscriptions = new Dictionary<string, SubscriptionInfo>();
        private readonly Dictionary<string, double> _currentPrices = new Dictionary<string, double>();
        private CancellationTokenSource _cancellationTokenSource;
        private readonly Random _random = new Random();

        public event EventHandler<BarDataEventArgs> OnBarReceived;
        public event EventHandler<ConnectionStatusEventArgs> OnConnectionStatusChanged;

        public bool IsConnected => _isConnected;
        public string Name => "Mock Data Feed";

        private class SubscriptionInfo
        {
            public string Symbol { get; set; }
            public TimeFrame TimeFrame { get; set; }
            public Task GeneratorTask { get; set; }
        }

        public async Task<bool> ConnectAsync()
        {
            if (_isConnected)
                return true;

            await Task.Delay(500); // Simulate connection delay

            _isConnected = true;
            _cancellationTokenSource = new CancellationTokenSource();

            OnConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
            {
                IsConnected = true,
                Message = "Connected to mock data feed",
                Timestamp = DateTime.Now
            });

            return true;
        }

        public async Task DisconnectAsync()
        {
            if (!_isConnected)
                return;

            _cancellationTokenSource?.Cancel();

            // Stop all subscriptions
            foreach (var sub in _subscriptions.Values)
            {
                await Task.Delay(100); // Allow generators to stop gracefully
            }

            _subscriptions.Clear();
            _isConnected = false;

            OnConnectionStatusChanged?.Invoke(this, new ConnectionStatusEventArgs
            {
                IsConnected = false,
                Message = "Disconnected from mock data feed",
                Timestamp = DateTime.Now
            });
        }

        public async Task<bool> SubscribeAsync(string symbol, TimeFrame timeFrame)
        {
            if (!_isConnected)
                return false;

            if (_subscriptions.ContainsKey(symbol))
                return true;

            // Initialize price for this symbol
            if (!_currentPrices.ContainsKey(symbol))
            {
                _currentPrices[symbol] = 100.0 + _random.NextDouble() * 400.0; // Random starting price between 100-500
            }

            var subscription = new SubscriptionInfo
            {
                Symbol = symbol,
                TimeFrame = timeFrame
            };

            // Start data generation task
            subscription.GeneratorTask = Task.Run(() => GenerateData(symbol, timeFrame, _cancellationTokenSource.Token));

            _subscriptions[symbol] = subscription;

            await Task.CompletedTask;
            return true;
        }

        public async Task<bool> UnsubscribeAsync(string symbol)
        {
            if (!_subscriptions.ContainsKey(symbol))
                return false;

            _subscriptions.Remove(symbol);

            await Task.CompletedTask;
            return true;
        }

        private async Task GenerateData(string symbol, TimeFrame timeFrame, CancellationToken cancellationToken)
        {
            int intervalMs = GetIntervalMilliseconds(timeFrame);
            DateTime lastBarTime = GetRoundedTime(DateTime.Now, timeFrame);
            double currentPrice = _currentPrices[symbol];

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(intervalMs, cancellationToken);

                    // Generate bar data with realistic price movement
                    var barData = GenerateBar(symbol, ref currentPrice, timeFrame);
                    barData.Timestamp = lastBarTime = GetNextBarTime(lastBarTime, timeFrame);

                    _currentPrices[symbol] = barData.Close;

                    OnBarReceived?.Invoke(this, barData);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private BarDataEventArgs GenerateBar(string symbol, ref double currentPrice, TimeFrame timeFrame)
        {
            // Generate realistic OHLCV data
            double volatility = 0.002; // 0.2% volatility per bar
            double trend = (_random.NextDouble() - 0.5) * 0.0005; // Small trend component

            double open = currentPrice;
            double change = ((_random.NextDouble() - 0.5) * 2 * volatility + trend) * currentPrice;
            double close = open + change;

            double high = Math.Max(open, close) + Math.Abs(change) * _random.NextDouble();
            double low = Math.Min(open, close) - Math.Abs(change) * _random.NextDouble();

            // Volume varies between 1M and 10M
            double volume = (1_000_000 + _random.NextDouble() * 9_000_000);

            return new BarDataEventArgs
            {
                Symbol = symbol,
                Open = Math.Round(open, 2),
                High = Math.Round(high, 2),
                Low = Math.Round(low, 2),
                Close = Math.Round(close, 2),
                Volume = Math.Round(volume),
                TimeFrame = timeFrame
            };
        }

        private int GetIntervalMilliseconds(TimeFrame timeFrame)
        {
            return timeFrame switch
            {
                TimeFrame.Second_1 => 1000,
                TimeFrame.Second_5 => 5000,
                TimeFrame.Second_15 => 15000,
                TimeFrame.Second_30 => 30000,
                TimeFrame.Minute_1 => 60000,
                TimeFrame.Minute_5 => 300000,
                TimeFrame.Minute_15 => 900000,
                TimeFrame.Minute_30 => 1800000,
                TimeFrame.Hour_1 => 3600000,
                TimeFrame.Hour_4 => 14400000,
                TimeFrame.Day_1 => 86400000,
                _ => 1000
            };
        }

        private DateTime GetRoundedTime(DateTime time, TimeFrame timeFrame)
        {
            return timeFrame switch
            {
                TimeFrame.Minute_1 => new DateTime(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0),
                TimeFrame.Minute_5 => new DateTime(time.Year, time.Month, time.Day, time.Hour, (time.Minute / 5) * 5, 0),
                TimeFrame.Minute_15 => new DateTime(time.Year, time.Month, time.Day, time.Hour, (time.Minute / 15) * 15, 0),
                TimeFrame.Minute_30 => new DateTime(time.Year, time.Month, time.Day, time.Hour, (time.Minute / 30) * 30, 0),
                TimeFrame.Hour_1 => new DateTime(time.Year, time.Month, time.Day, time.Hour, 0, 0),
                TimeFrame.Day_1 => new DateTime(time.Year, time.Month, time.Day, 0, 0, 0),
                _ => time
            };
        }

        private DateTime GetNextBarTime(DateTime currentTime, TimeFrame timeFrame)
        {
            return timeFrame switch
            {
                TimeFrame.Second_1 => currentTime.AddSeconds(1),
                TimeFrame.Second_5 => currentTime.AddSeconds(5),
                TimeFrame.Second_15 => currentTime.AddSeconds(15),
                TimeFrame.Second_30 => currentTime.AddSeconds(30),
                TimeFrame.Minute_1 => currentTime.AddMinutes(1),
                TimeFrame.Minute_5 => currentTime.AddMinutes(5),
                TimeFrame.Minute_15 => currentTime.AddMinutes(15),
                TimeFrame.Minute_30 => currentTime.AddMinutes(30),
                TimeFrame.Hour_1 => currentTime.AddHours(1),
                TimeFrame.Hour_4 => currentTime.AddHours(4),
                TimeFrame.Day_1 => currentTime.AddDays(1),
                _ => currentTime.AddSeconds(1)
            };
        }
    }
}
