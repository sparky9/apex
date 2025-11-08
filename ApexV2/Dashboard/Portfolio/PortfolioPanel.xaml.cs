using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ApexV2.Charts.Export;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Portfolio panel user control for displaying portfolio data
    /// </summary>
    public partial class PortfolioPanel : UserControl
    {
        private readonly PortfolioService _portfolioService;
        private readonly IChartLogger _logger;
        private Portfolio _currentPortfolio;
        private string _currentBrokerName;
        
        public PortfolioPanel()
        {
            InitializeComponent();
            
            // Create a test logger for now (would be injected in real app)
            _logger = new TestLogger();
            _portfolioService = new PortfolioService(_logger);
            
            // Initialize real-time components (from PortfolioPanel.cs)
            InitializeRealTimeComponents();
            
            // Register default broker providers
            RegisterBrokerProviders();
            
            // Subscribe to events
            _portfolioService.PortfolioUpdated += OnPortfolioUpdated;
            _portfolioService.BrokerConnectionChanged += OnBrokerConnectionChanged;
            _portfolioService.SyncStatusChanged += OnSyncStatusChanged;
            
            // Load sample data for design-time
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                LoadSampleData();
            }
        }
        
        #region Event Handlers
        
        private async void ConnectButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Show broker connection dialog
                var connectionDialog = new BrokerConnectionDialog();
                if (connectionDialog.ShowDialog() == true)
                {
                    var config = connectionDialog.BrokerConfig;
                    var brokerName = connectionDialog.SelectedBrokerName;
                    
                    UpdateStatus("Connecting...");
                    
                    var success = await _portfolioService.ConnectToBrokerAsync(brokerName, config);
                    
                    if (success)
                    {
                        _currentBrokerName = brokerName;
                        BrokerNameText.Text = brokerName;
                        UpdateStatus($"Connected to {brokerName}");
                    }
                    else
                    {
                        UpdateStatus($"Failed to connect to {brokerName}");
                        MessageBox.Show($"Failed to connect to {brokerName}. Please check your credentials.", 
                                      "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Connection error: {ex.Message}");
                UpdateStatus("Connection error");
                MessageBox.Show($"Connection error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(_currentBrokerName))
                {
                    MessageBox.Show("Please connect to a broker first.", "No Connection", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                UpdateStatus("Refreshing...");
                
                var success = await _portfolioService.SyncPortfolioAsync(_currentBrokerName);
                
                if (success)
                {
                    UpdateStatus("Refresh completed");
                }
                else
                {
                    UpdateStatus("Refresh failed");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Refresh error: {ex.Message}");
                UpdateStatus("Refresh error");
            }
        }
        
        private void OnPortfolioUpdated(object sender, PortfolioUpdatedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _currentPortfolio = e.Portfolio;
                UpdatePortfolioDisplay();
            });
        }
        
        private void OnBrokerConnectionChanged(object sender, BrokerConnectionEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateStatus(e.Message);
                
                if (e.Status == SyncStatus.Connected)
                {
                    BrokerNameText.Text = e.BrokerName;
                    _currentBrokerName = e.BrokerName;
                }
                else if (e.Status == SyncStatus.Disconnected || e.Status == SyncStatus.Error)
                {
                    BrokerNameText.Text = "Not Connected";
                    _currentBrokerName = null;
                    ClearPortfolioDisplay();
                }
            });
        }
        
        private void OnSyncStatusChanged(object sender, PortfolioSyncStatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                var status = e.SyncStatus;
                UpdateStatus($"{status.BrokerName}: {status.Status}");
                
                if (status.Status == SyncStatus.Connected)
                {
                    LastUpdateText.Text = $"Last update: {status.LastSyncTime:HH:mm:ss}";
                }
            });
        }
        
        #endregion
        
        #region UI Updates
        
        private void UpdatePortfolioDisplay()
        {
            if (_currentPortfolio == null) return;
            
            // Update summary
            TotalValueText.Text = _currentPortfolio.TotalValue.ToString("C2");
            
            // Day change with color
            var dayChangeText = $"{_currentPortfolio.DayChange:C2} ({_currentPortfolio.DayChangePercent:F2}%)";
            DayChangeText.Text = dayChangeText;
            DayChangeText.Foreground = GetColorForValue(_currentPortfolio.DayChange);
            
            // Total gain/loss with color
            var gainLossText = $"{_currentPortfolio.TotalGainLoss:C2} ({_currentPortfolio.TotalGainLossPercent:F2}%)";
            TotalGainLossText.Text = gainLossText;
            TotalGainLossText.Foreground = GetColorForValue(_currentPortfolio.TotalGainLoss);
            
            CashBalanceText.Text = _currentPortfolio.CashBalance.ToString("C2");
            BuyingPowerText.Text = _currentPortfolio.BuyingPower.ToString("C2");
            
            // Update positions
            PositionsItemsControl.ItemsSource = _currentPortfolio.Positions.OrderByDescending(p => p.MarketValue);
            
            LastUpdateText.Text = $"Last update: {_currentPortfolio.LastUpdated:HH:mm:ss}";
        }
        
        private void ClearPortfolioDisplay()
        {
            TotalValueText.Text = "$0.00";
            DayChangeText.Text = "$0.00 (0.00%)";
            DayChangeText.Foreground = Brushes.White;
            TotalGainLossText.Text = "$0.00 (0.00%)";
            TotalGainLossText.Foreground = Brushes.White;
            CashBalanceText.Text = "$0.00";
            BuyingPowerText.Text = "$0.00";
            PositionsItemsControl.ItemsSource = null;
            LastUpdateText.Text = "";
        }
        
        private void UpdateStatus(string message)
        {
            StatusText.Text = message;
        }
        
        private Brush GetColorForValue(decimal value)
        {
            if (value > 0) return new SolidColorBrush(Color.FromRgb(78, 201, 176)); // Green
            if (value < 0) return new SolidColorBrush(Color.FromRgb(241, 76, 76));  // Red
            return new SolidColorBrush(Color.FromRgb(204, 204, 204)); // Gray
        }
        
        #endregion
        
        #region Setup Methods
        
        private void RegisterBrokerProviders()
        {
            try
            {
                // Register Alpaca provider
                var alpacaProvider = new AlpacaBrokerProvider(_logger);
                _portfolioService.RegisterBrokerProvider(alpacaProvider);
                
                // Additional providers can be registered here
                // var interactiveBrokersProvider = new IBKRBrokerProvider(_logger);
                // _portfolioService.RegisterBrokerProvider(interactiveBrokersProvider);
                
                _logger.Info("Broker providers registered");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error registering broker providers: {ex.Message}");
            }
        }
        
        private void LoadSampleData()
        {
            // Sample data for design-time viewing
            var samplePortfolio = new Portfolio
            {
                Name = "Sample Portfolio",
                BrokerName = "Sample Broker",
                TotalValue = 125000.50m,
                DayChange = 2345.67m,
                DayChangePercent = 1.91m,
                TotalGainLoss = 15240.30m,
                CashBalance = 5000.00m,
                BuyingPower = 25000.00m,
                LastUpdated = DateTime.Now,
                Positions = new()
                {
                    new Position
                    {
                        Symbol = "AAPL",
                        CompanyName = "Apple Inc.",
                        Sector = "Technology",
                        Quantity = 100,
                        AverageCost = 150.25m,
                        CurrentPrice = 175.30m,
                        DayChange = 2.45m,
                        UnrealizedGainLoss = 2505.00m
                    },
                    new Position
                    {
                        Symbol = "MSFT",
                        CompanyName = "Microsoft Corporation",
                        Sector = "Technology",
                        Quantity = 50,
                        AverageCost = 280.10m,
                        CurrentPrice = 335.20m,
                        DayChange = 5.20m,
                        UnrealizedGainLoss = 2755.00m
                    },
                    new Position
                    {
                        Symbol = "GOOGL",
                        CompanyName = "Alphabet Inc.",
                        Sector = "Technology",
                        Quantity = 25,
                        AverageCost = 2450.00m,
                        CurrentPrice = 2380.50m,
                        DayChange = -15.30m,
                        UnrealizedGainLoss = -1737.50m
                    }
                }
            };
            
            _currentPortfolio = samplePortfolio;
            UpdatePortfolioDisplay();
        }
        
        #endregion
        
        #region Cleanup
        
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            _portfolioService?.Dispose();
        }
        
        #endregion
    }
    
    #region Value Converters
    
    public class GreaterThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
                return decimalValue > 0;
            if (value is double doubleValue)
                return doubleValue > 0;
            if (value is float floatValue)
                return floatValue > 0;
            if (value is int intValue)
                return intValue > 0;
            return false;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class LessThanZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
                return decimalValue < 0;
            if (value is double doubleValue)
                return doubleValue < 0;
            if (value is float floatValue)
                return floatValue < 0;
            if (value is int intValue)
                return intValue < 0;
            return false;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    public class EqualToZeroConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
                return decimalValue == 0;
            if (value is double doubleValue)
                return Math.Abs(doubleValue) < 0.001;
            if (value is float floatValue)
                return Math.Abs(floatValue) < 0.001f;
            if (value is int intValue)
                return intValue == 0;
            return true;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    #endregion
    
    #region Helper Classes
    
    /// <summary>
    /// Simple test logger implementation
    /// </summary>
    public class TestLogger : IChartLogger
    {
        public void Trace(string msg, Dictionary<string, object?>? props = null) => System.Diagnostics.Debug.WriteLine($"[TRACE] {msg}");
        public void Debug(string msg, Dictionary<string, object?>? props = null) => System.Diagnostics.Debug.WriteLine($"[DEBUG] {msg}");
        public void Info(string msg, Dictionary<string, object?>? props = null) => System.Diagnostics.Debug.WriteLine($"[INFO] {msg}");
        public void Warn(string msg, Dictionary<string, object?>? props = null) => System.Diagnostics.Debug.WriteLine($"[WARNING] {msg}");
        public void Error(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => System.Diagnostics.Debug.WriteLine($"[ERROR] {msg}");
        public void Critical(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => System.Diagnostics.Debug.WriteLine($"[CRITICAL] {msg}");
    }
    
    #endregion
}
