#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using ApexV2.Charts.Export;

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Performance Analytics Panel for comprehensive portfolio analysis
    /// </summary>
    public partial class PerformanceAnalyticsPanel : UserControl
    {
        private readonly PerformanceAnalyticsService _analyticsService;
        private readonly PerformanceComparisonService _comparisonService;
        private readonly IChartLogger _logger;
        private readonly PortfolioService _portfolioService;
        private Portfolio _currentPortfolio;
        private PerformanceMetrics _currentMetrics;
        
        public PerformanceAnalyticsPanel()
        {
            InitializeComponent();
            
            // Initialize services
            _logger = new PerformanceTestLogger();
            _analyticsService = new PerformanceAnalyticsService(_logger);
            _comparisonService = new PerformanceComparisonService(_logger, _analyticsService);
            _portfolioService = new PortfolioService(_logger);
            
            // Load sample data for design-time
            if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
            {
                LoadSampleData();
            }
        }
        
        #region Event Handlers
        
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await LoadPortfoliosAsync();
                
                if (PortfolioComboBox.Items.Count > 0)
                {
                    PortfolioComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading performance analytics panel: {ex.Message}");
            }
        }
        
        private async void PortfolioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (PortfolioComboBox.SelectedItem is ComboBoxItem item && item.Tag is Portfolio portfolio)
                {
                    _currentPortfolio = portfolio;
                    PortfolioNameText.Text = $"- {portfolio.Name}";
                    await RefreshAnalyticsAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error selecting portfolio: {ex.Message}");
            }
        }
        
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentPortfolio != null)
                {
                    await RefreshAnalyticsAsync();
                    MessageBox.Show("Performance analytics refreshed successfully!", "Refresh Complete", 
                                  MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Please select a portfolio first.", "No Portfolio Selected", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error refreshing analytics: {ex.Message}");
                MessageBox.Show($"Error refreshing analytics: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private async void RunSimulationButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentPortfolio == null)
                {
                    MessageBox.Show("Please select a portfolio first.", "No Portfolio Selected", 
                                  MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                RunSimulationButton.IsEnabled = false;
                RunSimulationButton.Content = "🔄 Running...";
                
                var simulation = await _comparisonService.RunMonteCarloSimulationAsync(_currentPortfolio);
                UpdateMonteCarloDisplay(simulation);
                
                MessageBox.Show($"Monte Carlo simulation completed with {simulation.SimulationCount} iterations.", 
                              "Simulation Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error running Monte Carlo simulation: {ex.Message}");
                MessageBox.Show($"Error running simulation: {ex.Message}", "Error", 
                              MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RunSimulationButton.IsEnabled = true;
                RunSimulationButton.Content = "🎲 Run Simulation";
            }
        }
        
        #endregion
        
        #region Data Loading
        
        private async Task LoadPortfoliosAsync()
        {
            try
            {
                PortfolioComboBox.Items.Clear();
                
                // In a real implementation, this would load actual portfolios
                // For now, create sample portfolios
                var samplePortfolios = CreateSamplePortfolios();
                
                foreach (var portfolio in samplePortfolios)
                {
                    var item = new ComboBoxItem
                    {
                        Content = $"{portfolio.Name} ({portfolio.BrokerName})",
                        Tag = portfolio
                    };
                    PortfolioComboBox.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error loading portfolios: {ex.Message}");
            }
        }
        
        private async Task RefreshAnalyticsAsync()
        {
            if (_currentPortfolio == null) return;
            
            try
            {
                // Calculate performance metrics
                var endDate = DateTime.UtcNow;
                var startDate = endDate.AddYears(-1);
                _currentMetrics = await _analyticsService.CalculatePerformanceAsync(_currentPortfolio, startDate, endDate);
                
                // Update UI
                UpdateOverviewDisplay();
                await UpdatePerformancePeriodsAsync();
                await UpdateRiskAnalysisAsync();
                await UpdateBenchmarkComparisonAsync();
                await UpdateAttributionAnalysisAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error refreshing analytics: {ex.Message}");
            }
        }
        
        #endregion
        
        #region UI Updates
        
        private void UpdateOverviewDisplay()
        {
            if (_currentMetrics == null) return;
            
            // Update summary cards
            TotalReturnText.Text = _currentMetrics.TotalReturn.ToString("C0");
            TotalReturnPercentText.Text = $"({_currentMetrics.TotalReturnPercent:F2}%)";
            AnnualizedReturnText.Text = $"{_currentMetrics.AnnualizedReturn:F2}%";
            SharpeRatioText.Text = _currentMetrics.SharpeRatio.ToString("F2");
            MaxDrawdownText.Text = $"{_currentMetrics.MaxDrawdownPercent:F2}%";
            MaxDrawdownDateText.Text = _currentMetrics.MaxDrawdownDate.ToString("MMM dd, yyyy");
        }
        
        private async Task UpdatePerformancePeriodsAsync()
        {
            if (_currentPortfolio == null) return;
            
            try
            {
                var periods = await _analyticsService.CalculatePerformancePeriodsAsync(_currentPortfolio);
                
                var periodData = new List<dynamic>
                {
                    new { Period = "1 Day", Return = periods.Daily.TotalReturnPercent, Volatility = periods.Daily.Volatility, 
                          SharpeRatio = periods.Daily.SharpeRatio, MaxDrawdown = periods.Daily.MaxDrawdownPercent, 
                          WinRate = periods.Daily.WinRate },
                    new { Period = "1 Week", Return = periods.Weekly.TotalReturnPercent, Volatility = periods.Weekly.Volatility, 
                          SharpeRatio = periods.Weekly.SharpeRatio, MaxDrawdown = periods.Weekly.MaxDrawdownPercent, 
                          WinRate = periods.Weekly.WinRate },
                    new { Period = "1 Month", Return = periods.Monthly.TotalReturnPercent, Volatility = periods.Monthly.Volatility, 
                          SharpeRatio = periods.Monthly.SharpeRatio, MaxDrawdown = periods.Monthly.MaxDrawdownPercent, 
                          WinRate = periods.Monthly.WinRate },
                    new { Period = "3 Months", Return = periods.Quarterly.TotalReturnPercent, Volatility = periods.Quarterly.Volatility, 
                          SharpeRatio = periods.Quarterly.SharpeRatio, MaxDrawdown = periods.Quarterly.MaxDrawdownPercent, 
                          WinRate = periods.Quarterly.WinRate },
                    new { Period = "YTD", Return = periods.YearToDate.TotalReturnPercent, Volatility = periods.YearToDate.Volatility, 
                          SharpeRatio = periods.YearToDate.SharpeRatio, MaxDrawdown = periods.YearToDate.MaxDrawdownPercent, 
                          WinRate = periods.YearToDate.WinRate },
                    new { Period = "1 Year", Return = periods.OneYear.TotalReturnPercent, Volatility = periods.OneYear.Volatility, 
                          SharpeRatio = periods.OneYear.SharpeRatio, MaxDrawdown = periods.OneYear.MaxDrawdownPercent, 
                          WinRate = periods.OneYear.WinRate }
                };
                
                PerformancePeriodsGrid.ItemsSource = periodData;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating performance periods: {ex.Message}");
            }
        }
        
        private async Task UpdateRiskAnalysisAsync()
        {
            if (_currentMetrics == null) return;
            
            // Update risk metrics cards
            VaR95Text.Text = $"{_currentMetrics.RiskMetrics.ValueAtRisk95:F2}%";
            VolatilityText.Text = $"{_currentMetrics.Volatility:F2}%";
            BetaText.Text = _currentMetrics.Beta.ToString("F2");
            SortinoRatioText.Text = _currentMetrics.RiskMetrics.SortinoRatio.ToString("F2");
            
            // Update risk contribution grid
            RiskContributionGrid.ItemsSource = _currentMetrics.RiskMetrics.RiskContributions;
        }
        
        private async Task UpdateBenchmarkComparisonAsync()
        {
            if (_currentPortfolio == null) return;
            
            try
            {
                var benchmarkItem = BenchmarkComboBox.SelectedItem as ComboBoxItem;
                var benchmarkSymbol = benchmarkItem?.Tag?.ToString() ?? "SPY";
                
                var comparison = await _comparisonService.CompareToBenchmarkAsync(
                    _currentPortfolio, benchmarkSymbol, benchmarkSymbol, 
                    DateTime.UtcNow.AddYears(-1), DateTime.UtcNow);
                
                // Update benchmark metrics
                AlphaText.Text = $"{comparison.Alpha:F2}%";
                InformationRatioText.Text = comparison.InformationRatio.ToString("F2");
                UpCaptureText.Text = $"{comparison.UpCapture:F1}%";
                DownCaptureText.Text = $"{comparison.DownCapture:F1}%";
                
                // Update comparison grid
                var comparisonData = new List<dynamic>
                {
                    new { Metric = "Total Return", PortfolioValue = $"{comparison.PortfolioReturn:F2}%", 
                          BenchmarkValue = $"{comparison.BenchmarkReturn:F2}%", 
                          Difference = $"{comparison.PortfolioReturn - comparison.BenchmarkReturn:F2}%" },
                    new { Metric = "Alpha", PortfolioValue = $"{comparison.Alpha:F2}%", 
                          BenchmarkValue = "0.00%", Difference = $"{comparison.Alpha:F2}%" },
                    new { Metric = "Beta", PortfolioValue = comparison.Beta.ToString("F2"), 
                          BenchmarkValue = "1.00", Difference = $"{comparison.Beta - 1:F2}" },
                    new { Metric = "Tracking Error", PortfolioValue = $"{comparison.TrackingError:F2}%", 
                          BenchmarkValue = "0.00%", Difference = $"{comparison.TrackingError:F2}%" }
                };
                
                BenchmarkComparisonGrid.ItemsSource = comparisonData;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating benchmark comparison: {ex.Message}");
            }
        }
        
        private async Task UpdateAttributionAnalysisAsync()
        {
            if (_currentPortfolio == null) return;
            
            try
            {
                var attribution = await _comparisonService.PerformAttributionAnalysisAsync(
                    _currentPortfolio, "SPY", DateTime.UtcNow.AddYears(-1), DateTime.UtcNow);
                
                // Update attribution summary
                TotalActiveReturnText.Text = $"{attribution.Summary.TotalActiveReturn:F2}%";
                AllocationEffectText.Text = $"{attribution.Summary.TotalAllocationEffect:F2}%";
                SelectionEffectText.Text = $"{attribution.Summary.TotalSelectionEffect:F2}%";
                InteractionEffectText.Text = $"{attribution.Summary.TotalInteractionEffect:F2}%";
                
                // Update grids
                SectorAttributionGrid.ItemsSource = attribution.SectorAttributions;
                SecurityAttributionGrid.ItemsSource = attribution.SecurityAttributions.Take(20).ToList(); // Top 20
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating attribution analysis: {ex.Message}");
            }
        }
        
        private void UpdateMonteCarloDisplay(MonteCarloSimulation simulation)
        {
            ProbabilityOfLossText.Text = $"{simulation.ProbabilityOfLoss:F1}%";
            MedianReturnText.Text = $"{simulation.MedianReturn:F2}%";
            WorstCaseText.Text = $"{simulation.WorstCaseReturn:F2}%";
            BestCaseText.Text = $"{simulation.BestCaseReturn:F2}%";
        }
        
        #endregion
        
        #region Sample Data
        
        private void LoadSampleData()
        {
            // Sample overview data
            TotalReturnText.Text = "$15,240";
            TotalReturnPercentText.Text = "(12.35%)";
            AnnualizedReturnText.Text = "8.7%";
            SharpeRatioText.Text = "1.24";
            MaxDrawdownText.Text = "8.3%";
            MaxDrawdownDateText.Text = "Mar 15, 2024";
            
            // Sample risk metrics
            VaR95Text.Text = "2.1%";
            VolatilityText.Text = "18.5%";
            BetaText.Text = "0.92";
            SortinoRatioText.Text = "1.67";
            
            // Sample benchmark comparison
            AlphaText.Text = "2.3%";
            InformationRatioText.Text = "0.85";
            UpCaptureText.Text = "94.2%";
            DownCaptureText.Text = "87.1%";
            
            // Sample attribution
            TotalActiveReturnText.Text = "2.1%";
            AllocationEffectText.Text = "0.8%";
            SelectionEffectText.Text = "1.1%";
            InteractionEffectText.Text = "0.2%";
            
            // Sample Monte Carlo
            ProbabilityOfLossText.Text = "23.4%";
            MedianReturnText.Text = "8.2%";
            WorstCaseText.Text = "-15.7%";
            BestCaseText.Text = "34.9%";
        }
        
        private List<Portfolio> CreateSamplePortfolios()
        {
            return new List<Portfolio>
            {
                new Portfolio
                {
                    Id = Guid.NewGuid(),
                    Name = "Growth Portfolio",
                    BrokerName = "Alpaca",
                    TotalValue = 125000.50m,
                    DayChange = 2345.67m,
                    DayChangePercent = 1.91m,
                    TotalGainLoss = 15240.30m,
                    CashBalance = 5000.00m,
                    BuyingPower = 25000.00m,
                    LastUpdated = DateTime.Now.AddYears(-2),
                    Positions = new List<Position>
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
                        }
                    }
                },
                new Portfolio
                {
                    Id = Guid.NewGuid(),
                    Name = "Conservative Portfolio",
                    BrokerName = "Alpaca",
                    TotalValue = 75000.00m,
                    DayChange = 150.00m,
                    DayChangePercent = 0.20m,
                    TotalGainLoss = 5000.00m,
                    CashBalance = 10000.00m,
                    BuyingPower = 15000.00m,
                    LastUpdated = DateTime.Now.AddYears(-1),
                    Positions = new List<Position>()
                }
            };
        }
        
        #endregion
    }
    
    #region Value Converters
    
    /// <summary>
    /// Converter for performance-based color coding
    /// </summary>
    public class PerformanceColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value?.ToString() == "--") return new SolidColorBrush(Color.FromRgb(204, 204, 204));
            
            if (decimal.TryParse(value?.ToString()?.Replace("%", "").Replace("$", "").Replace(",", ""), out decimal numValue))
            {
                if (numValue > 0) return new SolidColorBrush(Color.FromRgb(78, 201, 176)); // Green
                if (numValue < 0) return new SolidColorBrush(Color.FromRgb(241, 76, 76));  // Red
            }
            
            return new SolidColorBrush(Color.FromRgb(204, 204, 204)); // Gray
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// Converter for percentage formatting
    /// </summary>
    public class PercentageConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
                return $"{decimalValue:F2}%";
            if (value is double doubleValue)
                return $"{doubleValue:F2}%";
            return "--";
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// Converter for currency formatting
    /// </summary>
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
                return decimalValue.ToString("C2");
            if (value is double doubleValue)
                return doubleValue.ToString("C2");
            return "--";
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// Simple test logger implementation
    /// </summary>
    public class PerformanceTestLogger : IChartLogger
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
