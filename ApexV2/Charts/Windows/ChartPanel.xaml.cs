using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using ApexV2.Charts.Services;
using ApexV2.Charts.Models;
using ApexV2.Charts.Controls;
using ApexV2.Charts.Windows;
using ApexV2.Data.MarketData;
using ApexV2.Core.Logging;

namespace ApexV2.Charts.Windows;

/// <summary>
/// Professional chart panel that integrates ChartControl with UI chrome and controls
/// </summary>
public partial class ChartPanel : UserControl
{
    private readonly Logger _logger;
    private readonly ChartDataService _chartDataService;
    private string _currentSymbol = "AAPL";
    private string _currentTimeframe = "1d";
    private ChartSettings _chartSettings = new();

    // Events
    public event EventHandler<string>? SymbolChanged;
    public event EventHandler<string>? TimeframeChanged;

    // Public properties
    public string CurrentSymbol => _currentSymbol;
    public string CurrentTimeframe => _currentTimeframe;

    public ChartPanel()
    {
        InitializeComponent();
        
        // Get dependencies - simplified for now
        _logger = App.LogManager.GetLogger("ChartPanel");
        _chartDataService = new ChartDataService(App.LogManager.GetLogger("ChartDataService"));

        InitializeChartSettings();
        SetupEventHandlers();
        
        LoadInitialData();
    }

    private void InitializeChartSettings()
    {
        _chartSettings = new ChartSettings
        {
            ShowGrid = true,
            GridColor = "#444444",
            BackgroundColor = "#1e1e1e",
            BullishColor = "#26a69a",
            BearishColor = "#ef5350",
            ShowVolume = true,
            ShowCrosshair = true
        };

        MainChart.UpdateSettings(_chartSettings);
    }

    private void SetupEventHandlers()
    {
        // Subscribe to chart control events
        MainChart.SymbolClicked += OnChartSymbolClicked;
        MainChart.CrosshairMoved += OnChartCrosshairMoved;
        MainChart.ViewportChanged += OnChartViewportChanged;
        
        // Update navigation button states
        UpdateNavigationButtons();
    }

    private async void LoadInitialData()
    {
        try
        {
            await LoadChartData(_currentSymbol, _currentTimeframe);
            UpdateSymbolDisplay();
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to load initial chart data for {_currentSymbol}", ex);
            ShowError("Failed to load chart data");
        }
    }

    public async Task LoadChartData(string symbol, string timeframe)
    {
        try
        {
            _logger.Info($"Loading chart data: {symbol} {timeframe}");
            
            // Convert string timeframe to enum
            if (!Enum.TryParse<TimeFrame>(timeframe, true, out var timeFrameEnum))
            {
                // Default mapping for common timeframes
                timeFrameEnum = timeframe.ToLower() switch
                {
                    "1m" => TimeFrame.OneMinute,
                    "5m" => TimeFrame.FiveMinutes, 
                    "15m" => TimeFrame.FifteenMinutes,
                    "1h" => TimeFrame.Hourly,
                    "1d" => TimeFrame.Daily,
                    "1w" => TimeFrame.Weekly,
                    _ => TimeFrame.Daily
                };
            }
            
            var candlestickData = await _chartDataService.GetCandlestickDataAsync(symbol, timeFrameEnum);
            
            if (candlestickData?.Any() == true)
            {
                MainChart.SetData(candlestickData, symbol);
                UpdatePriceDisplay(candlestickData.Last());
                _logger.Info($"Loaded {candlestickData.Count()} candles for {symbol}");
            }
            else
            {
                _logger.Warn($"No chart data returned for {symbol} {timeframe}");
                ShowError($"No data available for {symbol}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error loading chart data for {symbol} {timeframe}", ex);
            ShowError($"Error loading data: {ex.Message}");
        }
    }

    private void UpdateSymbolDisplay()
    {
        SymbolText.Text = _currentSymbol;
    }

    private void UpdatePriceDisplay(CandlestickData candle)
    {
        if (candle == null) return;

        PriceText.Text = $"${candle.Close:F2}";
        
        var change = candle.Close - candle.Open;
        var changePercent = (change / candle.Open) * 100;
        
        ChangeText.Text = $"{(change >= 0 ? "+" : "")}{change:F2} ({changePercent:F2}%)";
        ChangeText.Foreground = change >= 0 
            ? System.Windows.Media.Brushes.Green 
            : System.Windows.Media.Brushes.Red;

        // Update OHLCV display
        OpenText.Text = candle.Open.ToString("F2");
        HighText.Text = candle.High.ToString("F2");
        LowText.Text = candle.Low.ToString("F2");
        CloseText.Text = candle.Close.ToString("F2");
        VolumeText.Text = FormatVolume(candle.Volume);
    }

    private string FormatVolume(long volume)
    {
        if (volume >= 1_000_000_000)
            return $"{volume / 1_000_000_000.0:F1}B";
        if (volume >= 1_000_000)
            return $"{volume / 1_000_000.0:F1}M";
        if (volume >= 1_000)
            return $"{volume / 1_000.0:F1}K";
        return volume.ToString();
    }

    private void ShowError(string message)
    {
        // For now, just log the error - we'll add error UI later
        _logger.Error($"Chart error: {message}");
    }

    private void HideError()
    {
        // For now, just log - we'll add error UI later  
        _logger.Info("Chart error cleared");
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        HideError();
        _ = Task.Run(async () => await LoadChartData(_currentSymbol, _currentTimeframe));
    }

    #region Public API

    /// <summary>
    /// Set the symbol to display
    /// </summary>
    public void SetSymbol(string symbol)
    {
        if (string.IsNullOrWhiteSpace(symbol) || symbol == _currentSymbol)
            return;

        _currentSymbol = symbol;
        UpdateSymbolDisplay();
        
        // Reload data for new symbol
        _ = Task.Run(async () => await LoadChartData(_currentSymbol, _currentTimeframe));
        
        SymbolChanged?.Invoke(this, _currentSymbol);
    }

    /// <summary>
    /// Set the timeframe to display
    /// </summary>
    public void SetTimeframe(string timeframe)
    {
        if (string.IsNullOrWhiteSpace(timeframe) || timeframe == _currentTimeframe)
            return;

        _currentTimeframe = timeframe;
        
        // Update UI selection
        foreach (ComboBoxItem item in TimeframeCombo.Items)
        {
            if (item.Tag?.ToString() == timeframe)
            {
                TimeframeCombo.SelectedItem = item;
                break;
            }
        }
        
        // Reload data for new timeframe
        _ = Task.Run(async () => await LoadChartData(_currentSymbol, _currentTimeframe));
        
        TimeframeChanged?.Invoke(this, _currentTimeframe);
    }

    #endregion

    #region Event Handlers

    private void TimeframeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TimeframeCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            var newTimeframe = selectedItem.Tag?.ToString();
            if (newTimeframe != null && newTimeframe != _currentTimeframe)
            {
                SetTimeframe(newTimeframe);
            }
        }
    }

    private void ChartTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ChartTypeCombo.SelectedItem is ComboBoxItem selectedItem)
        {
            var chartType = selectedItem.Tag?.ToString();
            if (chartType != null)
            {
                // Update chart settings based on type
                // For now, just log - chart type switching will be enhanced later
                _logger.Info($"Chart type changed to: {chartType}");
            }
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO [REVIEWED]: Open chart settings dialog
        _logger.Info("Chart settings requested");
    }

    private void OnChartSymbolClicked(object? sender, string symbol)
    {
        SetSymbol(symbol);
    }

    private void OnChartCrosshairMoved(object? sender, CandlestickData? candle)
    {
        if (candle != null)
        {
            UpdatePriceDisplay(candle);
        }
    }

    private void OnChartViewportChanged(object sender, ChartViewport e)
    {
        // Update navigation button states when viewport changes
        UpdateNavigationButtons();
    }

    private void UpdateNavigationButtons()
    {
        if (MainChart.NavigationManager != null)
        {
            BackButton.IsEnabled = MainChart.NavigationManager.CanGoBack();
            ForwardButton.IsEnabled = MainChart.NavigationManager.CanGoForward();
        }
    }

    // Navigation event handlers
    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        MainChart.NavigationManager?.GoBack();
    }

    private void ForwardButton_Click(object sender, RoutedEventArgs e)
    {
        MainChart.NavigationManager?.GoForward();
    }

    private void ZoomInButton_Click(object sender, RoutedEventArgs e)
    {
        MainChart.ZoomIn();
    }

    private void ZoomOutButton_Click(object sender, RoutedEventArgs e)
    {
        MainChart.ZoomOut();
    }

    private void ResetZoomButton_Click(object sender, RoutedEventArgs e)
    {
        MainChart.ResetZoom();
    }

    private void FitToDataButton_Click(object sender, RoutedEventArgs e)
    {
        MainChart.FitToData();
    }

    // Timeframe event handlers - using the existing TimeframeCombo  
    // (Removed TimeframeComboBox_SelectionChanged - will use existing TimeframeCombo)

    // Interaction mode event handlers - simplified for now
    // (Will be implemented when corresponding UI elements are added to XAML)

    // Additional navigation event handlers
    private void ZoomFitButton_Click(object sender, RoutedEventArgs e)
    {
        MainChart.FitToData();
    }

    private void PanModeButton_Click(object sender, RoutedEventArgs e)
    {
        MainChart.SetInteractionMode(ChartInteractionMode.Pan);
    }

    private void ZoomBoxModeButton_Click(object sender, RoutedEventArgs e)
    {
        MainChart.SetInteractionMode(ChartInteractionMode.ZoomBox);
    }
    
    private void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var exportDialog = new ChartExportDialog(MainChart);
            exportDialog.Owner = Window.GetWindow(this);
            exportDialog.ShowDialog();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error opening export dialog: {ex.Message}", ex);
            MessageBox.Show($"Failed to open export dialog: {ex.Message}", "Export Error", 
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ZoomPresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.SelectedItem is ComboBoxItem item)
        {
            var preset = item.Tag?.ToString();
            if (!string.IsNullOrEmpty(preset))
            {
                MainChart.NavigationManager?.ApplyZoomPreset(preset);
            }
        }
    }

    #endregion

    #region Public API Methods

    /// <summary>
    /// Refresh chart data from data source
    /// </summary>
    public async Task RefreshAsync()
    {
        await LoadChartData(_currentSymbol, _currentTimeframe);
    }

    /// <summary>
    /// Update chart settings
    /// </summary>
    public void UpdateSettings(ChartSettings settings)
    {
        _chartSettings = settings;
        MainChart.UpdateSettings(settings);
    }

    /// <summary>
    /// Add real-time price update
    /// </summary>
    public void UpdateRealtimePrice(StockQuote quote)
    {
        if (quote.Symbol.Equals(_currentSymbol, StringComparison.OrdinalIgnoreCase))
        {
            MainChart.UpdateRealtimePrice(quote);
            
            // Update current price display
            PriceText.Text = $"${quote.Price:F2}";
            
            // Update data status
            DataStatusIndicator.Fill = System.Windows.Media.Brushes.Green;
            DataStatusText.Text = "Live";
        }
    }

    #endregion
}
