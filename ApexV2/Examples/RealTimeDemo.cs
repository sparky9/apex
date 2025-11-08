#nullable disable
using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using ApexV2.Extensions.RealTime;
using ApexV2.Dashboard;
using ApexV2.Dashboard.Portfolio;
using ApexV2.Charts.Controls;

namespace ApexV2.Examples
{
    /// <summary>
    /// Demonstration class showing how to use the real-time data system
    /// This can be used for testing and as documentation for integration
    /// </summary>
    public class RealTimeDemo
    {
        private readonly ILogger<RealTimeDemo> _logger;
        private RealTimeIntegration _realTimeIntegration;
        private DashboardPanel _dashboardPanel;
        private ChartPanel _chartPanel;
        private PortfolioPanel _portfolioPanel;

        public RealTimeDemo()
        {
            _logger = new TestLogger<RealTimeDemo>();
        }

        /// <summary>
        /// Comprehensive demo showing all real-time features
        /// </summary>
        public async Task RunComprehensiveDemoAsync()
        {
            try
            {
                _logger.LogInformation("Starting comprehensive real-time demo...");

                // Step 1: Initialize the real-time system
                await InitializeRealTimeSystemAsync();

                // Step 2: Set up UI components
                await SetupUIComponentsAsync();

                // Step 3: Subscribe to data for demo symbols
                await SubscribeToDataAsync();

                // Step 4: Create some alerts
                await CreateAlertsAsync();

                // Step 5: Add portfolio positions
                await AddPortfolioPositionsAsync();

                // Step 6: Monitor for a while
                await MonitorDataAsync();

                _logger.LogInformation("Comprehensive real-time demo completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in comprehensive real-time demo");
            }
        }

        /// <summary>
        /// Quick demo for basic functionality
        /// </summary>
        public async Task RunQuickDemoAsync()
        {
            try
            {
                _logger.LogInformation("Starting quick real-time demo...");

                // Quick start with default configuration
                _realTimeIntegration = await RealTimeIntegration.QuickStartAsync();
                if (_realTimeIntegration == null)
                {
                    _logger.LogError("Failed to quick start real-time integration");
                    return;
                }

                // Subscribe to some data
                await _realTimeIntegration.SubscribeAsync("AAPL", RealTimeDataType.Price, OnPriceUpdate);
                await _realTimeIntegration.SubscribeAsync("MSFT", RealTimeDataType.Price, OnPriceUpdate);

                // Create a simple alert
                var alertId = await _realTimeIntegration.CreatePriceAlertAsync("AAPL", 150m, RealTimeAlertCondition.GreaterThan);
                _logger.LogInformation("Created alert: {AlertId}", alertId);

                // Monitor for 30 seconds
                _logger.LogInformation("Monitoring data for 30 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(30));

                // Get metrics
                var metrics = await _realTimeIntegration.GetMetricsAsync();
                _logger.LogInformation("Metrics: Updates/sec={Updates}, Avg Latency={Latency}ms, Errors={Errors}%", 
                    metrics?.UpdatesPerSecond, metrics?.AverageLatencyMs, metrics?.ErrorRatePercent);

                _logger.LogInformation("Quick real-time demo completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in quick real-time demo");
            }
            finally
            {
                _realTimeIntegration?.Dispose();
            }
        }

        private async Task InitializeRealTimeSystemAsync()
        {
            try
            {
                _logger.LogInformation("Initializing real-time system...");

                // Create the integration with custom configuration
                var config = new RealTimeConfiguration
                {
                    ProviderName = "Demo",
                    AutoConnect = true,
                    EnableAggregation = true,
                    EnableAlerts = true,
                    EnableMetrics = true,
                    UpdateIntervalMs = 500, // 2 updates per second for demo
                    MaxConcurrentSubscriptions = 50,
                    BufferSize = 1000
                };

                _realTimeIntegration = new RealTimeIntegration(null, config);

                // Subscribe to integration events
                _realTimeIntegration.DataUpdated += OnDataUpdated;
                _realTimeIntegration.ErrorOccurred += OnErrorOccurred;
                _realTimeIntegration.ConnectionStatusChanged += OnConnectionStatusChanged;
                _realTimeIntegration.AlertTriggered += OnAlertTriggered;
                _realTimeIntegration.MetricsUpdated += OnMetricsUpdated;

                // Initialize with simulated data
                var initialized = await _realTimeIntegration.InitializeWithSimulatedDataAsync();
                if (!initialized)
                {
                    throw new Exception("Failed to initialize real-time integration");
                }

                // Start the system
                var started = await _realTimeIntegration.StartAsync();
                if (!started)
                {
                    throw new Exception("Failed to start real-time integration");
                }

                _logger.LogInformation("Real-time system initialized and started");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing real-time system");
                throw;
            }
        }

        private async Task SetupUIComponentsAsync()
        {
            try
            {
                _logger.LogInformation("Setting up UI components...");

                // Create dashboard panel
                _dashboardPanel = new DashboardPanel();
                await _dashboardPanel.InitializeRealTimeAsync(_realTimeIntegration.RealTimeManager);
                _dashboardPanel.DashboardEvent += OnDashboardEvent;

                // Create chart panel
                _chartPanel = new ChartPanel();
                await _chartPanel.InitializeRealTimeAsync(_realTimeIntegration.RealTimeManager);
                _chartPanel.ChartDataUpdated += OnChartDataUpdated;

                // Create portfolio panel
                _portfolioPanel = new PortfolioPanel();
                await _portfolioPanel.InitializeRealTimeAsync(_realTimeIntegration.RealTimeManager);
                _portfolioPanel.PortfolioUpdated += OnPortfolioUpdated;

                // Enable real-time updates
                _dashboardPanel.IsRealTimeEnabled = true;
                _chartPanel.IsRealTimeEnabled = true;
                _portfolioPanel.IsRealTimeEnabled = true;

                _logger.LogInformation("UI components set up successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting up UI components");
                throw;
            }
        }

        private async Task SubscribeToDataAsync()
        {
            try
            {
                _logger.LogInformation("Subscribing to data...");

                var symbols = new[] { "AAPL", "MSFT", "GOOGL", "AMZN", "TSLA" };

                foreach (var symbol in symbols)
                {
                    await _realTimeIntegration.SubscribeAsync(symbol, RealTimeDataType.Price, OnPriceUpdate);
                    await _realTimeIntegration.SubscribeAsync(symbol, RealTimeDataType.Volume, OnVolumeUpdate);
                    
                    _logger.LogDebug("Subscribed to {Symbol} price and volume data", symbol);
                }

                // Set chart symbol
                _chartPanel.CurrentSymbol = "AAPL";
                _dashboardPanel.SelectedSymbol = "AAPL";

                _logger.LogInformation("Data subscriptions completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error subscribing to data");
                throw;
            }
        }

        private async Task CreateAlertsAsync()
        {
            try
            {
                _logger.LogInformation("Creating alerts...");

                // Create price alerts for demo
                var alert1 = await _realTimeIntegration.CreatePriceAlertAsync("AAPL", 150m, RealTimeAlertCondition.GreaterThan);
                var alert2 = await _realTimeIntegration.CreatePriceAlertAsync("MSFT", 200m, RealTimeAlertCondition.LessThan);
                var alert3 = await _realTimeIntegration.CreatePriceAlertAsync("GOOGL", 2500m, RealTimeAlertCondition.GreaterThanOrEqual);

                _logger.LogInformation("Created alerts: {Alert1}, {Alert2}, {Alert3}", alert1, alert2, alert3);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating alerts");
                throw;
            }
        }

        private async Task AddPortfolioPositionsAsync()
        {
            try
            {
                _logger.LogInformation("Adding portfolio positions...");

                var positions = new[]
                {
                    new PortfolioPosition { Symbol = "AAPL", Quantity = 100, AveragePrice = 145m, CostBasis = 14500m },
                    new PortfolioPosition { Symbol = "MSFT", Quantity = 50, AveragePrice = 210m, CostBasis = 10500m },
                    new PortfolioPosition { Symbol = "GOOGL", Quantity = 25, AveragePrice = 2400m, CostBasis = 60000m }
                };

                foreach (var position in positions)
                {
                    await _portfolioPanel.AddPositionAsync(position);
                    _logger.LogDebug("Added position: {Symbol} - {Quantity} shares", position.Symbol, position.Quantity);
                }

                _logger.LogInformation("Portfolio positions added");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding portfolio positions");
                throw;
            }
        }

        private async Task MonitorDataAsync()
        {
            try
            {
                _logger.LogInformation("Monitoring real-time data for 2 minutes...");

                var monitoringTime = TimeSpan.FromMinutes(2);
                var startTime = DateTime.Now;

                while (DateTime.Now - startTime < monitoringTime)
                {
                    // Get and log metrics every 10 seconds
                    var metrics = await _realTimeIntegration.GetMetricsAsync();
                    if (metrics != null)
                    {
                        _logger.LogInformation("Real-time metrics: Updates/sec={Updates:F1}, Latency={Latency:F1}ms, Errors={Errors:F1}%", 
                            metrics.UpdatesPerSecond, metrics.AverageLatencyMs, metrics.ErrorRatePercent);
                    }

                    await Task.Delay(TimeSpan.FromSeconds(10));
                }

                _logger.LogInformation("Monitoring completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during monitoring");
            }
        }

        // Event handlers
        private void OnPriceUpdate(RealTimeUpdate update)
        {
            _logger.LogTrace("Price update: {Symbol} = {Price}", update.Symbol, update.Data);
        }

        private void OnVolumeUpdate(RealTimeUpdate update)
        {
            _logger.LogTrace("Volume update: {Symbol} = {Volume}", update.Symbol, update.Data);
        }

        private void OnDataUpdated(object sender, RealTimeUpdate update)
        {
            // Global data update handler
        }

        private void OnErrorOccurred(object sender, RealTimeErrorEventArgs e)
        {
            _logger.LogWarning("Real-time error: {Symbol} {DataType} - {Error}", e.Symbol, e.DataType, e.ErrorMessage);
        }

        private void OnConnectionStatusChanged(object sender, RealTimeConnectionEventArgs e)
        {
            _logger.LogInformation("Connection status: {Connected} ({Provider})", e.IsConnected, e.Provider);
        }

        private void OnAlertTriggered(object sender, RealTimeAlertTriggeredEventArgs e)
        {
            _logger.LogWarning("ALERT TRIGGERED: {Symbol} - {Message}", e.Symbol, e.Message);
        }

        private void OnMetricsUpdated(object sender, RealTimeMetrics metrics)
        {
            // Metrics are logged in the monitoring loop
        }

        private void OnDashboardEvent(object sender, DashboardEventArgs e)
        {
            _logger.LogDebug("Dashboard event: {EventType} for {Symbol}", e.EventType, e.Symbol);
        }

        private void OnChartDataUpdated(object sender, ChartDataUpdatedEventArgs e)
        {
            _logger.LogTrace("Chart data updated: {Symbol} {DataType}", e.Symbol, e.DataType);
        }

        private void OnPortfolioUpdated(object sender, PortfolioUpdatedEventArgs e)
        {
            _logger.LogTrace("Portfolio updated: {UpdateType} - Total Value: {TotalValue:C2}", 
                e.UpdateType, e.TotalValue);
        }

        public void Dispose()
        {
            try
            {
                _dashboardPanel?.Dispose();
                _chartPanel?.Dispose();
                _portfolioPanel?.Dispose();
                _realTimeIntegration?.Dispose();

                _logger.LogInformation("Real-time demo disposed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing real-time demo");
            }
        }
    }

    /// <summary>
    /// Static helper class for easy testing
    /// </summary>
    public static class RealTimeDemoHelper
    {
        /// <summary>
        /// Run a quick demo from anywhere in the application
        /// </summary>
        public static async Task RunQuickDemo()
        {
            var demo = new RealTimeDemo();
            try
            {
                await demo.RunQuickDemoAsync();
            }
            finally
            {
                demo.Dispose();
            }
        }

        /// <summary>
        /// Run the comprehensive demo
        /// </summary>
        public static async Task RunComprehensiveDemo()
        {
            var demo = new RealTimeDemo();
            try
            {
                await demo.RunComprehensiveDemoAsync();
            }
            finally
            {
                demo.Dispose();
            }
        }
    }
}
