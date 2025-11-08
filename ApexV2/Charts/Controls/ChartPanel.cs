#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ApexV2.Extensions.RealTime;
using ApexV2.Data.MarketData;

namespace ApexV2.Charts.Controls
{
    /// <summary>
    /// Enhanced ChartPanel with real-time data integration
    /// </summary>
    public partial class ChartPanel : UserControl, INotifyPropertyChanged
    {
        private readonly ILogger<ChartPanel> _logger;
        private RealTimeManager _realTimeManager;
        private string _currentSymbol;
        private readonly Dictionary<string, RealTimeDataType> _activeSubscriptions;
        private readonly DispatcherTimer _refreshTimer;
        private bool _isRealTimeEnabled;
        private DateTime _lastUpdate;
        private int _updateCount;

        // Properties for binding
        public string CurrentSymbol
        {
            get => _currentSymbol;
            set
            {
                if (_currentSymbol != value)
                {
                    var oldSymbol = _currentSymbol;
                    _currentSymbol = value;
                    OnPropertyChanged(nameof(CurrentSymbol));
                    _ = Task.Run(async () => await OnSymbolChangedAsync(oldSymbol, value));
                }
            }
        }

        public bool IsRealTimeEnabled
        {
            get => _isRealTimeEnabled;
            set
            {
                if (_isRealTimeEnabled != value)
                {
                    _isRealTimeEnabled = value;
                    OnPropertyChanged(nameof(IsRealTimeEnabled));
                    _ = Task.Run(async () => await OnRealTimeEnabledChangedAsync(value));
                }
            }
        }

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            private set
            {
                _lastUpdate = value;
                OnPropertyChanged(nameof(LastUpdate));
            }
        }

        public int UpdateCount
        {
            get => _updateCount;
            private set
            {
                _updateCount = value;
                OnPropertyChanged(nameof(UpdateCount));
            }
        }

        public bool IsConnected => _realTimeManager?.IsConnected ?? false;
        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";

        // Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<ChartDataUpdatedEventArgs> ChartDataUpdated;

        public ChartPanel()
        {
            InitializeComponent();
            
            _logger = new TestLogger<ChartPanel>();
            _activeSubscriptions = new Dictionary<string, RealTimeDataType>();
            
            // Setup refresh timer for UI updates
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100) // 10 FPS
            };
            _refreshTimer.Tick += OnRefreshTimerTick;

            DataContext = this;
            
            _logger.LogInformation("ChartPanel initialized");
        }

        public async Task InitializeRealTimeAsync(RealTimeManager realTimeManager)
        {
            try
            {
                if (realTimeManager == null)
                {
                    throw new ArgumentNullException(nameof(realTimeManager));
                }

                _realTimeManager = realTimeManager;

                // Subscribe to real-time events
                _realTimeManager.DataUpdated += OnRealTimeDataUpdated;
                _realTimeManager.ErrorOccurred += OnRealTimeError;
                _realTimeManager.ConnectionStatusChanged += OnConnectionStatusChanged;

                // Register as a listener
                await _realTimeManager.RegisterListenerAsync($"Chart_{GetHashCode()}", OnChartDataCallback);

                _logger.LogInformation("ChartPanel real-time integration initialized");
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionStatus));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing chart real-time integration");
                throw;
            }
        }

        private async Task OnSymbolChangedAsync(string oldSymbol, string newSymbol)
        {
            try
            {
                if (_realTimeManager == null || !_isRealTimeEnabled)
                    return;

                // Unsubscribe from old symbol
                if (!string.IsNullOrEmpty(oldSymbol))
                {
                    await _realTimeManager.UnsubscribeAllAsync(oldSymbol);
                    _activeSubscriptions.Clear();
                    _logger.LogDebug("Unsubscribed from all data for symbol: {Symbol}", oldSymbol);
                }

                // Subscribe to new symbol
                if (!string.IsNullOrEmpty(newSymbol))
                {
                    // Subscribe to price data
                    var priceSubscribed = await _realTimeManager.SubscribeAsync(
                        newSymbol, 
                        RealTimeDataType.Price, 
                        OnChartDataCallback);

                    if (priceSubscribed)
                    {
                        _activeSubscriptions[newSymbol] = RealTimeDataType.Price;
                        _logger.LogDebug("Subscribed to price data for symbol: {Symbol}", newSymbol);
                    }

                    // Subscribe to volume data
                    var volumeSubscribed = await _realTimeManager.SubscribeAsync(
                        newSymbol, 
                        RealTimeDataType.Volume, 
                        OnChartDataCallback);

                    if (volumeSubscribed)
                    {
                        _activeSubscriptions[$"{newSymbol}_Volume"] = RealTimeDataType.Volume;
                        _logger.LogDebug("Subscribed to volume data for symbol: {Symbol}", newSymbol);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing symbol from {OldSymbol} to {NewSymbol}", oldSymbol, newSymbol);
            }
        }

        private async Task OnRealTimeEnabledChangedAsync(bool enabled)
        {
            try
            {
                if (_realTimeManager == null)
                    return;

                if (enabled)
                {
                    // Enable real-time updates
                    if (!string.IsNullOrEmpty(_currentSymbol))
                    {
                        await OnSymbolChangedAsync(null, _currentSymbol);
                    }
                    
                    _refreshTimer.Start();
                    _logger.LogInformation("Real-time updates enabled for chart");
                }
                else
                {
                    // Disable real-time updates
                    _refreshTimer.Stop();
                    
                    if (!string.IsNullOrEmpty(_currentSymbol))
                    {
                        await _realTimeManager.UnsubscribeAllAsync(_currentSymbol);
                        _activeSubscriptions.Clear();
                    }
                    
                    _logger.LogInformation("Real-time updates disabled for chart");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing real-time enabled state to {Enabled}", enabled);
            }
        }

        private void OnChartDataCallback(RealTimeUpdate update)
        {
            try
            {
                // Process update on UI thread
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    ProcessChartUpdate(update);
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chart data callback for {Symbol}", update?.Symbol);
            }
        }

        private void ProcessChartUpdate(RealTimeUpdate update)
        {
            try
            {
                if (update?.Symbol != _currentSymbol)
                    return;

                // Update chart data based on data type
                switch (update.DataType)
                {
                    case RealTimeDataType.Price:
                        UpdatePriceData(update);
                        break;
                    case RealTimeDataType.Volume:
                        UpdateVolumeData(update);
                        break;
                    case RealTimeDataType.Trade:
                        UpdateTradeData(update);
                        break;
                    case RealTimeDataType.Quote:
                        UpdateQuoteData(update);
                        break;
                }

                // Update UI properties
                LastUpdate = update.Timestamp;
                UpdateCount++;

                // Notify external listeners
                ChartDataUpdated?.Invoke(this, new ChartDataUpdatedEventArgs
                {
                    Symbol = update.Symbol,
                    DataType = update.DataType,
                    Data = update.Data,
                    Timestamp = update.Timestamp
                });

                _logger.LogTrace("Processed chart update for {Symbol} {DataType}", 
                    update.Symbol, update.DataType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chart update for {Symbol}", update?.Symbol);
            }
        }

        private void UpdatePriceData(RealTimeUpdate update)
        {
            try
            {
                if (update.Price.HasValue)
                {
                    var price = update.Price.Value;
                    // Update chart with new price point
                    // This would integrate with your actual chart control
                    _logger.LogTrace("Updated price data: {Symbol} = {Price}", update.Symbol, price);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating price data for {Symbol}", update.Symbol);
            }
        }

        private void UpdateVolumeData(RealTimeUpdate update)
        {
            try
            {
                if (update.Volume.HasValue)
                {
                    var volume = update.Volume.Value;
                    // Update chart with new volume data
                    _logger.LogTrace("Updated volume data: {Symbol} = {Volume}", update.Symbol, volume);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating volume data for {Symbol}", update.Symbol);
            }
        }

        private void UpdateTradeData(RealTimeUpdate update)
        {
            try
            {
                // Update chart with trade data
                _logger.LogTrace("Updated trade data: {Symbol}", update.Symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating trade data for {Symbol}", update.Symbol);
            }
        }

        private void UpdateQuoteData(RealTimeUpdate update)
        {
            try
            {
                // Update chart with quote data
                _logger.LogTrace("Updated quote data: {Symbol}", update.Symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating quote data for {Symbol}", update.Symbol);
            }
        }

        private void OnRealTimeDataUpdated(object sender, RealTimeUpdate update)
        {
            // This event is handled by the callback registration
            // Additional global processing can be done here if needed
        }

        private void OnRealTimeError(object sender, RealTimeErrorEventArgs e)
        {
            try
            {
                _logger.LogWarning("Real-time error for {Symbol} {DataType}: {Error}", 
                    e.Symbol, e.DataType, e.ErrorMessage);

                // Update UI to show error state if needed
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    // Handle error in UI (show notification, etc.)
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling real-time error event");
            }
        }

        private void OnConnectionStatusChanged(object sender, RealTimeConnectionEventArgs e)
        {
            try
            {
                _logger.LogInformation("Connection status changed: {Connected} ({Provider})", 
                    e.IsConnected, e.Provider);

                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(ConnectionStatus));
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling connection status change");
            }
        }

        private void OnRefreshTimerTick(object sender, EventArgs e)
        {
            try
            {
                // Periodic UI refresh for smooth animations
                // This can be used to interpolate between data points or update indicators
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in refresh timer tick");
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            try
            {
                _refreshTimer?.Stop();
                
                if (_realTimeManager != null)
                {
                    _ = Task.Run(async () =>
                    {
                        await _realTimeManager.UnregisterListenerAsync($"Chart_{GetHashCode()}");
                        
                        if (!string.IsNullOrEmpty(_currentSymbol))
                        {
                            await _realTimeManager.UnsubscribeAllAsync(_currentSymbol);
                        }
                    });

                    _realTimeManager.DataUpdated -= OnRealTimeDataUpdated;
                    _realTimeManager.ErrorOccurred -= OnRealTimeError;
                    _realTimeManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
                }

                _logger.LogInformation("ChartPanel disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing ChartPanel");
            }
        }
    }

    public class ChartDataUpdatedEventArgs : EventArgs
    {
        public string Symbol { get; set; }
        public RealTimeDataType DataType { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
