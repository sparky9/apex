#nullable disable
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ApexV2.Extensions.RealTime;
using ApexV2.Data.MarketData;
using ApexV2.Charts.Controls;
using ApexV2.Dashboard.Portfolio;

namespace ApexV2.Dashboard
{
    /// <summary>
    /// Enhanced DashboardPanel with real-time data integration
    /// </summary>
    public partial class DashboardPanel : UserControl, INotifyPropertyChanged
    {
        private readonly ILogger<DashboardPanel> _logger;
        private RealTimeManager _realTimeManager;
        private ChartPanel _chartPanel;
        private PortfolioPanel _portfolioPanel;
        private readonly DispatcherTimer _statusTimer;
        
        private bool _isRealTimeEnabled;
        private DateTime _lastUpdate;
        private string _selectedSymbol;
        private int _totalUpdates;
        private RealTimeMetrics _currentMetrics;

        // Properties for binding
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

        public string SelectedSymbol
        {
            get => _selectedSymbol;
            set
            {
                if (_selectedSymbol != value)
                {
                    _selectedSymbol = value;
                    OnPropertyChanged(nameof(SelectedSymbol));
                    OnSymbolChanged(value);
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

        public int TotalUpdates
        {
            get => _totalUpdates;
            private set
            {
                _totalUpdates = value;
                OnPropertyChanged(nameof(TotalUpdates));
            }
        }

        public RealTimeMetrics CurrentMetrics
        {
            get => _currentMetrics;
            private set
            {
                _currentMetrics = value;
                OnPropertyChanged(nameof(CurrentMetrics));
                OnPropertyChanged(nameof(UpdatesPerSecond));
                OnPropertyChanged(nameof(AverageLatency));
                OnPropertyChanged(nameof(ErrorRate));
            }
        }

        public double UpdatesPerSecond => _currentMetrics?.UpdatesPerSecond ?? 0;
        public double AverageLatency => _currentMetrics?.AverageLatencyMs ?? 0;
        public double ErrorRate => _currentMetrics?.ErrorRatePercent ?? 0;

        public bool IsConnected => _realTimeManager?.IsConnected ?? false;
        public bool IsStreaming => _realTimeManager?.IsStreaming ?? false;
        public string ConnectionStatus => IsConnected ? "Connected" : "Disconnected";

        // Events
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<DashboardEventArgs> DashboardEvent;

        public DashboardPanel()
        {
            InitializeComponent();
            
            _logger = new TestLogger<DashboardPanel>();
            
            // Setup status update timer
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5) // Update metrics every 5 seconds
            };
            _statusTimer.Tick += OnStatusTimerTick;

            DataContext = this;
            
            _logger.LogInformation("DashboardPanel initialized");
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
                _realTimeManager.AlertTriggered += OnAlertTriggered;
                _realTimeManager.MetricsUpdated += OnMetricsUpdated;

                // Initialize child components
                await InitializeChildComponentsAsync();

                // Register as a listener
                await _realTimeManager.RegisterListenerAsync($"Dashboard_{GetHashCode()}", OnDashboardDataCallback);

                _statusTimer.Start();

                _logger.LogInformation("DashboardPanel real-time integration initialized");
                UpdateConnectionStatus();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing dashboard real-time integration");
                throw;
            }
        }

        private async Task InitializeChildComponentsAsync()
        {
            try
            {
                // Initialize chart panel if available
                _chartPanel = FindName("ChartPanelControl") as ChartPanel;
                if (_chartPanel != null)
                {
                    await _chartPanel.InitializeRealTimeAsync(_realTimeManager);
                    _chartPanel.ChartDataUpdated += OnChartDataUpdated;
                    _logger.LogDebug("Chart panel real-time integration initialized");
                }

                // Initialize portfolio panel if available
                _portfolioPanel = FindName("PortfolioPanelControl") as PortfolioPanel;
                if (_portfolioPanel != null)
                {
                    await _portfolioPanel.InitializeRealTimeAsync(_realTimeManager);
                    _portfolioPanel.PortfolioUpdated += OnPortfolioUpdated;
                    _logger.LogDebug("Portfolio panel real-time integration initialized");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing child components");
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
                    // Start real-time services
                    var started = await _realTimeManager.StartAsync();
                    if (!started)
                    {
                        _logger.LogWarning("Failed to start real-time services");
                        // Revert checkbox state
                        Dispatcher.BeginInvoke(() => _isRealTimeEnabled = false);
                        return;
                    }

                    // Update child components
                    if (_chartPanel != null)
                    {
                        _chartPanel.IsRealTimeEnabled = true;
                    }
                    
                    if (_portfolioPanel != null)
                    {
                        _portfolioPanel.IsRealTimeEnabled = true;
                    }

                    _logger.LogInformation("Real-time updates enabled for dashboard");
                }
                else
                {
                    // Stop real-time services
                    await _realTimeManager.StopAsync();

                    // Update child components
                    if (_chartPanel != null)
                    {
                        _chartPanel.IsRealTimeEnabled = false;
                    }
                    
                    if (_portfolioPanel != null)
                    {
                        _portfolioPanel.IsRealTimeEnabled = false;
                    }

                    _logger.LogInformation("Real-time updates disabled for dashboard");
                }

                UpdateConnectionStatus();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing real-time enabled state to {Enabled}", enabled);
            }
        }

        private void OnSymbolChanged(string symbol)
        {
            try
            {
                // Update chart panel with new symbol
                if (_chartPanel != null)
                {
                    _chartPanel.CurrentSymbol = symbol;
                }

                _logger.LogDebug("Symbol changed to: {Symbol}", symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error changing symbol to {Symbol}", symbol);
            }
        }

        private void OnDashboardDataCallback(RealTimeUpdate update)
        {
            try
            {
                // Process update on UI thread
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    ProcessDashboardUpdate(update);
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing dashboard data callback for {Symbol}", update?.Symbol);
            }
        }

        private void ProcessDashboardUpdate(RealTimeUpdate update)
        {
            try
            {
                // Update dashboard-level statistics
                LastUpdate = update.Timestamp;
                TotalUpdates++;

                // Notify external listeners
                DashboardEvent?.Invoke(this, new DashboardEventArgs
                {
                    EventType = DashboardEventType.DataUpdate,
                    Symbol = update.Symbol,
                    Data = update.Data,
                    Timestamp = update.Timestamp
                });

                _logger.LogTrace("Processed dashboard update for {Symbol} {DataType}", 
                    update.Symbol, update.DataType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing dashboard update for {Symbol}", update?.Symbol);
            }
        }

        private void OnRealTimeDataUpdated(object sender, RealTimeUpdate update)
        {
            // Updates are handled by individual components and dashboard callback
        }

        private void OnRealTimeError(object sender, RealTimeErrorEventArgs e)
        {
            try
            {
                _logger.LogWarning("Real-time error for {Symbol} {DataType}: {Error}", 
                    e.Symbol, e.DataType, e.ErrorMessage);

                // Notify UI of error
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    DashboardEvent?.Invoke(this, new DashboardEventArgs
                    {
                        EventType = DashboardEventType.Error,
                        Symbol = e.Symbol,
                        Data = e.ErrorMessage,
                        Timestamp = DateTime.Now
                    });
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
                    UpdateConnectionStatus();
                    
                    DashboardEvent?.Invoke(this, new DashboardEventArgs
                    {
                        EventType = DashboardEventType.ConnectionStatusChanged,
                        Data = e.IsConnected,
                        Timestamp = DateTime.Now
                    });
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling connection status change");
            }
        }

        private void OnAlertTriggered(object sender, RealTimeAlertTriggeredEventArgs e)
        {
            try
            {
                _logger.LogInformation("Alert triggered: {AlertId} for {Symbol}", 
                    e.AlertId, e.Symbol);

                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    DashboardEvent?.Invoke(this, new DashboardEventArgs
                    {
                        EventType = DashboardEventType.AlertTriggered,
                        Symbol = e.Symbol,
                        Data = e.Alert,
                        Timestamp = DateTime.Now
                    });
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling alert trigger");
            }
        }

        private void OnMetricsUpdated(object sender, RealTimeMetrics metrics)
        {
            try
            {
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
                {
                    CurrentMetrics = metrics;
                }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling metrics update");
            }
        }

        private void OnChartDataUpdated(object sender, ChartDataUpdatedEventArgs e)
        {
            try
            {
                // Chart data was updated, potentially update other dashboard components
                _logger.LogTrace("Chart data updated for {Symbol}", e.Symbol);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling chart data update");
            }
        }

        private void OnPortfolioUpdated(object sender, PortfolioUpdatedEventArgs e)
        {
            try
            {
                // Portfolio was updated, potentially update other dashboard components
                _logger.LogTrace("Portfolio updated: {UpdateType}", e.UpdateType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling portfolio update");
            }
        }

        private async void OnStatusTimerTick(object sender, EventArgs e)
        {
            try
            {
                if (_realTimeManager != null)
                {
                    // Get latest metrics
                    var metrics = await _realTimeManager.GetMetricsAsync();
                    if (metrics != null)
                    {
                        CurrentMetrics = metrics;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in status timer tick");
            }
        }

        private void UpdateConnectionStatus()
        {
            OnPropertyChanged(nameof(IsConnected));
            OnPropertyChanged(nameof(IsStreaming));
            OnPropertyChanged(nameof(ConnectionStatus));
        }

        public async Task<bool> CreateAlertAsync(string symbol, decimal price, RealTimeAlertCondition condition)
        {
            try
            {
                if (_realTimeManager == null)
                    return false;

                var alert = new RealTimeAlert
                {
                    Symbol = symbol,
                    Condition = condition,
                    TargetValue = price,
                    IsEnabled = true,
                    CreatedAt = DateTime.UtcNow
                };

                var alertId = await _realTimeManager.CreateAlertAsync(alert);
                
                _logger.LogInformation("Created alert {AlertId} for {Symbol} at {Price}", 
                    alertId, symbol, price);
                
                return alertId != Guid.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating alert for {Symbol}", symbol);
                return false;
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
                _statusTimer?.Stop();
                
                // Dispose child components
                _chartPanel?.Dispose();
                _portfolioPanel?.Dispose();

                if (_realTimeManager != null)
                {
                    _ = Task.Run(async () =>
                    {
                        await _realTimeManager.UnregisterListenerAsync($"Dashboard_{GetHashCode()}");
                    });

                    _realTimeManager.DataUpdated -= OnRealTimeDataUpdated;
                    _realTimeManager.ErrorOccurred -= OnRealTimeError;
                    _realTimeManager.ConnectionStatusChanged -= OnConnectionStatusChanged;
                    _realTimeManager.AlertTriggered -= OnAlertTriggered;
                    _realTimeManager.MetricsUpdated -= OnMetricsUpdated;
                }

                _logger.LogInformation("DashboardPanel disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing DashboardPanel");
            }
        }
    }

    // Supporting classes
    public class DashboardEventArgs : EventArgs
    {
        public DashboardEventType EventType { get; set; }
        public string Symbol { get; set; }
        public object Data { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum DashboardEventType
    {
        DataUpdate,
        Error,
        ConnectionStatusChanged,
        AlertTriggered,
        MetricsUpdated
    }
}
