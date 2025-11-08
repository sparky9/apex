#nullable disable
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ApexV2.Extensions.RealTime;
using ApexV2.Data.MarketData;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Enhanced PortfolioPanel with real-time data integration
    /// </summary>
    public partial class PortfolioPanel : UserControl, INotifyPropertyChanged
    {
        // Real-time specific fields (Portfolio service fields are in PortfolioPanel.xaml.cs)
        private RealTimeManager _realTimeManager;
        private Dictionary<string, PortfolioPosition> _positions;
        private Dictionary<string, RealTimeSubscription> _subscriptions;
        private DispatcherTimer _updateTimer;
        private bool _isRealTimeEnabled;
        private DateTime _lastUpdate;
        private decimal _totalValue;
        private decimal _totalGainLoss;
        private decimal _totalGainLossPercent;

        // Properties for binding
        public ObservableCollection<PortfolioPosition> Positions { get; } = new ObservableCollection<PortfolioPosition>();
        
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

        public decimal TotalValue
        {
            get => _totalValue;
            private set
            {
                _totalValue = value;
                OnPropertyChanged(nameof(TotalValue));
            }
        }

        public decimal TotalGainLoss
        {
            get => _totalGainLoss;
            private set
            {
                _totalGainLoss = value;
                OnPropertyChanged(nameof(TotalGainLoss));
            }
        }

        public decimal TotalGainLossPercent
        {
            get => _totalGainLossPercent;
            private set
            {
                _totalGainLossPercent = value;
                OnPropertyChanged(nameof(TotalGainLossPercent));
            }
        }

        public bool IsConnected => _realTimeManager?.IsConnected ?? false;
        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";

        // Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<PortfolioUpdatedEventArgs> PortfolioUpdated;

        // Initialize real-time components (called from XAML constructor)
        private void InitializeRealTimeComponents()
        {
            _positions = new Dictionary<string, PortfolioPosition>();
            _subscriptions = new Dictionary<string, RealTimeSubscription>();
            
            // Setup update timer
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1) // Update portfolio totals every second
            };
            _updateTimer.Tick += OnUpdateTimerTick;

            // // _logger.LogInformation("PortfolioPanel real-time components initialized");
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
                await _realTimeManager.RegisterListenerAsync($"Portfolio_{GetHashCode()}", OnPortfolioDataCallback);

                // // _logger.LogInformation("PortfolioPanel real-time integration initialized");
                OnPropertyChanged(nameof(IsConnected));
                OnPropertyChanged(nameof(ConnectionStatus));
            }
            catch (Exception ex)
            {
                // Logger: "Error initializing portfolio real-time integration");
                throw;
            }
        }

        public async Task AddPositionAsync(PortfolioPosition position)
        {
            try
            {
                if (position == null || string.IsNullOrEmpty(position.Symbol))
                    return;

                // Add to internal tracking
                _positions[position.Symbol] = position;
                
                // Add to observable collection on UI thread
                await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    var existingPosition = Positions.FirstOrDefault(p => p.Symbol == position.Symbol);
                    if (existingPosition != null)
                    {
                        Positions.Remove(existingPosition);
                    }
                    Positions.Add(position);
                }));

                // Subscribe to real-time data if enabled
                if (_isRealTimeEnabled && _realTimeManager != null)
                {
                    await SubscribeToSymbolAsync(position.Symbol);
                }

                // Logger: position.Symbol, position.Quantity);
            }
            catch (Exception ex)
            {
                // Logger: "Error adding position for {Symbol}", position?.Symbol);
            }
        }

        public async Task RemovePositionAsync(string symbol)
        {
            try
            {
                if (string.IsNullOrEmpty(symbol))
                    return;

                // Remove from internal tracking
                _positions.Remove(symbol);
                
                // Remove from observable collection on UI thread
                await Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    var position = Positions.FirstOrDefault(p => p.Symbol == symbol);
                    if (position != null)
                    {
                        Positions.Remove(position);
                    }
                }));

                // Unsubscribe from real-time data
                if (_realTimeManager != null)
                {
                    await UnsubscribeFromSymbolAsync(symbol);
                }

                // Logger: symbol);
            }
            catch (Exception ex)
            {
                // Logger: "Error removing position for {Symbol}", symbol);
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
                    // Subscribe to all positions
                    foreach (var symbol in _positions.Keys)
                    {
                        await SubscribeToSymbolAsync(symbol);
                    }
                    
                    _updateTimer.Start();
                    // _logger.LogInformation("Real-time updates enabled for portfolio");
                }
                else
                {
                    // Unsubscribe from all positions
                    _updateTimer.Stop();
                    
                    foreach (var symbol in _positions.Keys)
                    {
                        await UnsubscribeFromSymbolAsync(symbol);
                    }
                    
                    // _logger.LogInformation("Real-time updates disabled for portfolio");
                }
            }
            catch (Exception ex)
            {
                // Logger: "Error changing real-time enabled state to {Enabled}", enabled);
            }
        }

        private async Task SubscribeToSymbolAsync(string symbol)
        {
            try
            {
                if (_realTimeManager == null || string.IsNullOrEmpty(symbol))
                    return;

                // Subscribe to price data for this symbol
                var subscribed = await _realTimeManager.SubscribeAsync(
                    symbol, 
                    RealTimeDataType.Price, 
                    OnPortfolioDataCallback);

                if (subscribed)
                {
                    _subscriptions[symbol] = new RealTimeSubscription
                    {
                        Symbol = symbol,
                        DataType = RealTimeDataType.Price,
                        SubscribedAt = DateTime.UtcNow
                    };
                    
                    // Logger: symbol);
                }
            }
            catch (Exception ex)
            {
                // Logger: "Error subscribing to symbol {Symbol}", symbol);
            }
        }

        private async Task UnsubscribeFromSymbolAsync(string symbol)
        {
            try
            {
                if (_realTimeManager == null || string.IsNullOrEmpty(symbol))
                    return;

                await _realTimeManager.UnsubscribeAsync(symbol, RealTimeDataType.Price);
                _subscriptions.Remove(symbol);
                
                // Logger: symbol);
            }
            catch (Exception ex)
            {
                // Logger: "Error unsubscribing from symbol {Symbol}", symbol);
            }
        }

        private void OnPortfolioDataCallback(RealTimeUpdate update)
        {
            try
            {
                // Process update on UI thread
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    ProcessPortfolioUpdate(update);
                }));
            }
            catch (Exception ex)
            {
                // Logger: "Error processing portfolio data callback for {Symbol}", update?.Symbol);
            }
        }

        private void ProcessPortfolioUpdate(RealTimeUpdate update)
        {
            try
            {
                if (update?.Symbol == null || !_positions.ContainsKey(update.Symbol))
                    return;

                var position = _positions[update.Symbol];
                var uiPosition = Positions.FirstOrDefault(p => p.Symbol == update.Symbol);

                if (position == null || uiPosition == null)
                    return;

                // Update position based on data type
                switch (update.DataType)
                {
                    case RealTimeDataType.Price:
                        if (update.Price.HasValue)
                        {
                            UpdatePositionPrice(position, uiPosition, update.Price.Value);
                        }
                        break;
                    case RealTimeDataType.Trade:
                        // Handle trade updates if needed
                        break;
                    case RealTimeDataType.Quote:
                        // Handle quote updates if needed
                        break;
                }

                // Update last update time
                LastUpdate = update.Timestamp;

                // Trigger portfolio calculation update
                CalculatePortfolioTotals();

                // Notify external listeners
                PortfolioUpdated?.Invoke(this, new PortfolioUpdatedEventArgs
                {
                    UpdatedPosition = uiPosition,
                    TotalValue = _totalValue,
                    TotalGainLoss = _totalGainLoss,
                    UpdateType = PortfolioUpdateType.PriceUpdate
                });

                // Logger would go here: "Processed portfolio update for {Symbol}: {Price}"
                // update.Symbol, update.Data
            }
            catch (Exception ex)
            {
                // Logger: "Error processing portfolio update for {Symbol}", update?.Symbol);
            }
        }

        private void UpdatePositionPrice(PortfolioPosition position, PortfolioPosition uiPosition, decimal price)
        {
            try
            {
                var oldPrice = position.CurrentPrice;
                position.CurrentPrice = price;
                uiPosition.CurrentPrice = price;

                // Calculate new values
                position.MarketValue = position.Quantity * price;
                position.GainLoss = position.MarketValue - position.CostBasis;
                position.GainLossPercent = position.CostBasis != 0 ? 
                    (position.GainLoss / position.CostBasis) * 100 : 0;

                // Update UI position
                uiPosition.MarketValue = position.MarketValue;
                uiPosition.GainLoss = position.GainLoss;
                uiPosition.GainLossPercent = position.GainLossPercent;
                uiPosition.LastUpdated = DateTime.Now;

                // Trigger property change notifications
                uiPosition.OnPropertyChanged(nameof(PortfolioPosition.CurrentPrice));
                uiPosition.OnPropertyChanged(nameof(PortfolioPosition.MarketValue));
                uiPosition.OnPropertyChanged(nameof(PortfolioPosition.GainLoss));
                uiPosition.OnPropertyChanged(nameof(PortfolioPosition.GainLossPercent));
                uiPosition.OnPropertyChanged(nameof(PortfolioPosition.LastUpdated));

                // Logger would go here: "Updated position price: {Symbol} {OldPrice} -> {NewPrice}"
                // position.Symbol, oldPrice, price
            }
            catch (Exception ex)
            {
                // Logger: "Error updating position price for {Symbol}", position?.Symbol);
            }
        }

        private void CalculatePortfolioTotals()
        {
            try
            {
                var totalValue = 0m;
                var totalCostBasis = 0m;

                foreach (var position in _positions.Values)
                {
                    totalValue += position.MarketValue;
                    totalCostBasis += position.CostBasis;
                }

                TotalValue = totalValue;
                TotalGainLoss = totalValue - totalCostBasis;
                TotalGainLossPercent = totalCostBasis != 0 ? 
                    (TotalGainLoss / totalCostBasis) * 100 : 0;

                // Logger: "Calculated portfolio totals - Value={TotalValue}, G/L={GainLoss}"
            }
            catch (Exception ex)
            {
                // Logger: "Error calculating portfolio totals"
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
                // Logger: "Real-time error for {Symbol} {DataType}: {Error}"
            }
            catch (Exception ex)
            {
                // Logger: "Error handling real-time error event"
            }
        }

        private void OnConnectionStatusChanged(object sender, RealTimeConnectionEventArgs e)
        {
            try
            {
                // Logger: "Connection status changed - Connected: {IsConnected}, Provider: {Provider}"

                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    OnPropertyChanged(nameof(IsConnected));
                    OnPropertyChanged(nameof(ConnectionStatus));
                }));
            }
            catch (Exception ex)
            {
                // Logger: "Error handling connection status change"
            }
        }

        private void OnUpdateTimerTick(object sender, EventArgs e)
        {
            try
            {
                // Periodic update of portfolio calculations
                CalculatePortfolioTotals();
            }
            catch (Exception ex)
            {
                // Logger: "Error in portfolio update timer tick"
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
                _updateTimer?.Stop();
                
                if (_realTimeManager != null)
                {
                    _ = Task.Run(async () =>
                    {
                        await _realTimeManager.UnregisterListenerAsync($"Portfolio_{GetHashCode()}");
                        
                        foreach (var symbol in _positions.Keys)
                        {
                            await _realTimeManager.UnsubscribeAsync(symbol, RealTimeDataType.Price);
                        }
                    });

                    _realTimeManager.DataUpdated -= OnRealTimeDataUpdated;
                    _realTimeManager.ErrorOccurred -= OnRealTimeError;
                    _realTimeManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
                }

                // _logger.LogInformation("PortfolioPanel disposed");
            }
            catch (Exception ex)
            {
                // Logger: "Error disposing PortfolioPanel");
            }
        }
    }
}
