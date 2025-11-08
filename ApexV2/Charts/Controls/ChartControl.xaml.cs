using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using ApexV2.Charts.Models;
using ApexV2.Charts.Renderer;
using ApexV2.Charts.Drawing;
using ApexV2.Core.Logging;

// Type aliases to resolve namespace issues
using ChartNavigationManager = ApexV2.Charts.Controls.ChartNavigationManager;
using ChartInteractionMode = ApexV2.Charts.Controls.ChartInteractionMode;

namespace ApexV2.Charts.Controls;

/// <summary>
/// Professional chart control for displaying candlestick and line charts
/// </summary>
public partial class ChartControl : UserControl
{
    private readonly Logger _logger;
    private readonly ChartRenderService _renderService;
    private readonly DispatcherTimer _updateTimer;
    private readonly ChartNavigationManager _navigationManager;
    private readonly DrawingManager _drawingManager;
    
    private List<CandlestickData> _chartData = new();
    private ChartSettings _settings = new();
    private ChartViewport _viewport = new();
    private bool _isLoading;
    private bool _isDragging;
    private Point _lastMousePosition;
    private ChartInteractionMode _interactionMode = ChartInteractionMode.Pan;
    
    // Zoom box selection
    private bool _isZoomBoxActive;
    private Point _zoomBoxStart;
    private Rectangle? _zoomBoxRectangle;

    public event EventHandler<string>? SymbolRequested;
    public event EventHandler<ChartSettings>? SettingsChanged;
    public event EventHandler<string>? SymbolClicked;
    public event EventHandler<CandlestickData?>? CrosshairMoved;
    public event EventHandler<ChartViewport>? ViewportChanged;

    #region Initialization

    public ChartControl()
    {
        InitializeComponent();
        
        _logger = App.LogManager.GetLogger("ChartControl");
        _renderService = new ChartRenderService(_logger);
        _navigationManager = new ChartNavigationManager();
        _drawingManager = new DrawingManager();
        
        _updateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _updateTimer.Tick += UpdateTimer_Tick;
        
        Loaded += ChartControl_Loaded;
        SizeChanged += ChartControl_SizeChanged;
        
        // Enable keyboard focus
        Focusable = true;
        KeyDown += ChartControl_KeyDown;
        
        // Initialize with sample data for design time
        if (System.ComponentModel.DesignerProperties.GetIsInDesignMode(this))
        {
            LoadSampleData();
        }
    }

    private void ChartControl_Loaded(object sender, RoutedEventArgs e)
    {
        _logger.Info("ChartControl loaded");
        UpdateChart();
    }

    private void ChartControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            UpdateChart();
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Navigation manager for chart controls
    /// </summary>
    public ChartNavigationManager NavigationManager => _navigationManager;

    /// <summary>
    /// Drawing manager for chart annotations
    /// </summary>
    public DrawingManager DrawingManager => _drawingManager;

    /// <summary>
    /// Current symbol being displayed
    /// </summary>
    public string CurrentSymbol { get; private set; } = string.Empty;

    /// <summary>
    /// Current chart settings
    /// </summary>
    public ChartSettings Settings { get; private set; } = new();

    /// <summary>
    /// Chart data being displayed
    /// </summary>
    public IEnumerable<CandlestickData>? Data { get; private set; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the chart data
    /// </summary>
    public void SetData(IEnumerable<CandlestickData> data, string symbol)
    {
        _chartData = data?.ToList() ?? new List<CandlestickData>();
        Data = _chartData;
        CurrentSymbol = symbol;
        _settings.Symbol = symbol;
        
        SymbolText.Text = symbol;
        DataPointsText.Text = $"{_chartData.Count} points";
        LastUpdateText.Text = DateTime.Now.ToString("HH:mm:ss");
        
        if (_chartData.Any())
        {
            var latest = _chartData.Last();
            PriceText.Text = $"${latest.Close:F2}";
            
            // Calculate change from previous close
            if (_chartData.Count > 1)
            {
                var previous = _chartData[_chartData.Count - 2];
                var change = latest.Close - previous.Close;
                var changePercent = (change / previous.Close) * 100;
                
                ChangeText.Text = $"{change:+0.00;-0.00} ({changePercent:+0.00;-0.00}%)";
                ChangeText.Foreground = new SolidColorBrush(change >= 0 ? 
                    (Color)ColorConverter.ConvertFromString("#26a69a") : 
                    (Color)ColorConverter.ConvertFromString("#ef5350"));
            }
            
            _viewport = _renderService.CalculateViewport(_chartData, _settings);
            HideError();
        }
        
        UpdateChart();
    }

    /// <summary>
    /// Updates chart settings
    /// </summary>
    public void UpdateSettings(ChartSettings settings)
    {
        _settings = settings;
        Settings = settings;
        TimeFrameText.Text = settings.TimeFrame.ToString();
        UpdateChart();
    }

    /// <summary>
    /// Shows loading state
    /// </summary>
    public void ShowLoading()
    {
        _isLoading = true;
        LoadingPanel.Visibility = Visibility.Visible;
        ErrorPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = "Loading...";
    }

    /// <summary>
    /// Hides loading state
    /// </summary>
    public void HideLoading()
    {
        _isLoading = false;
        LoadingPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = "Ready";
    }

    /// <summary>
    /// Shows error state
    /// </summary>
    public void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
        LoadingPanel.Visibility = Visibility.Collapsed;
        StatusText.Text = "Error";
        _logger.Error($"Chart error: {message}");
    }

    /// <summary>
    /// Hides error state
    /// </summary>
    public void HideError()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    private void UpdateChart()
    {
        if (_isLoading || !IsLoaded || ChartCanvas.ActualWidth <= 0 || ChartCanvas.ActualHeight <= 0)
            return;

        try
        {
            ChartCanvas.Children.Clear();

            if (!_chartData.Any())
            {
                _logger.Debug("No chart data to render");
                return;
            }

            var canvasSize = new Size(ChartCanvas.ActualWidth, ChartCanvas.ActualHeight);
            
            // Create drawing visual
            var drawingVisual = new DrawingVisual();
            using (var drawingContext = drawingVisual.RenderOpen())
            {
                // Render grid
                _renderService.RenderGrid(drawingContext, _viewport, _settings, canvasSize);
                
                // Render chart based on type
                switch (_settings.ChartType)
                {
                    case ChartType.Candlestick:
                        _renderService.RenderCandlesticks(drawingContext, _chartData, _viewport, _settings, canvasSize);
                        break;
                    case ChartType.Line:
                        _renderService.RenderPriceLine(drawingContext, _chartData, _viewport, _settings, canvasSize);
                        break;
                    default:
                        _renderService.RenderCandlesticks(drawingContext, _chartData, _viewport, _settings, canvasSize);
                        break;
                }
                
                // Render drawings and annotations
                var chartRect = new Rect(0, 0, canvasSize.Width, canvasSize.Height);
                _drawingManager.Render(drawingContext, chartRect);
            }

            // Add to canvas
            var image = new Image
            {
                Source = new DrawingImage(drawingVisual.Drawing),
                Stretch = Stretch.None
            };
            
            ChartCanvas.Children.Add(image);
            
            _logger.Debug($"Chart updated with {_chartData.Count} data points");
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to update chart: {ex.Message}", ex);
            ShowError("Failed to render chart");
        }
    }

    private void UpdateTimer_Tick(object? sender, EventArgs e)
    {
        _updateTimer.Stop();
        UpdateChart();
    }

    private void QueueUpdate()
    {
        _updateTimer.Stop();
        _updateTimer.Start();
    }

    #endregion

    #region Mouse Event Handlers

    private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_settings.ShowCrosshair) return;

        var position = e.GetPosition(ChartCanvas);
        
        // Update crosshair
        CrosshairVertical.X1 = CrosshairVertical.X2 = position.X;
        CrosshairVertical.Y1 = 0;
        CrosshairVertical.Y2 = ChartCanvas.ActualHeight;
        CrosshairVertical.Visibility = Visibility.Visible;
        
        CrosshairHorizontal.Y1 = CrosshairHorizontal.Y2 = position.Y;
        CrosshairHorizontal.X1 = 0;
        CrosshairHorizontal.X2 = ChartCanvas.ActualWidth;
        CrosshairHorizontal.Visibility = Visibility.Visible;
        
        // Update price label
        if (_viewport.PriceRange > 0)
        {
            var priceRatio = (ChartCanvas.ActualHeight - position.Y) / ChartCanvas.ActualHeight;
            var price = _viewport.MinPrice + (decimal)priceRatio * _viewport.PriceRange;
            PriceLabelText.Text = $"${price:F2}";
            
            Canvas.SetLeft(PriceLabel, ChartCanvas.ActualWidth - PriceLabel.ActualWidth - 5);
            Canvas.SetTop(PriceLabel, position.Y - PriceLabel.ActualHeight / 2);
            PriceLabel.Visibility = Visibility.Visible;
        }
        
        // Update time label
        if (_viewport.TimeRange.TotalMilliseconds > 0)
        {
            var timeRatio = position.X / ChartCanvas.ActualWidth;
            var time = _viewport.StartTime.AddMilliseconds(timeRatio * _viewport.TimeRange.TotalMilliseconds);
            TimeLabelText.Text = time.ToString("MM/dd HH:mm");
            
            Canvas.SetLeft(TimeLabel, position.X - TimeLabel.ActualWidth / 2);
            Canvas.SetTop(TimeLabel, ChartCanvas.ActualHeight - TimeLabel.ActualHeight - 5);
            TimeLabel.Visibility = Visibility.Visible;
        }

        // Handle dragging and zoom box
        if (_interactionMode == ChartInteractionMode.Pan && _isDragging && e.LeftButton == MouseButtonState.Pressed)
        {
            var delta = position - _lastMousePosition;
            Pan(-delta.X, delta.Y);
            _lastMousePosition = position;
        }
        else if (_interactionMode == ChartInteractionMode.ZoomBox && _isZoomBoxActive)
        {
            UpdateZoomBox(position);
        }
        
        // Handle drawing manager mouse move
        _drawingManager.OnMouseMove(position);
    }

    private void ChartCanvas_MouseLeave(object sender, MouseEventArgs e)
    {
        CrosshairVertical.Visibility = Visibility.Collapsed;
        CrosshairHorizontal.Visibility = Visibility.Collapsed;
        PriceLabel.Visibility = Visibility.Collapsed;
        TimeLabel.Visibility = Visibility.Collapsed;
    }

    private void ChartCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(ChartCanvas);
        _lastMousePosition = position;
        
        // First try drawing manager if not in pan mode
        if (_interactionMode != ChartInteractionMode.Pan)
        {
            _drawingManager.OnMouseDown(position, e.ChangedButton);
            if (_drawingManager.IsDrawing)
            {
                return; // Drawing handled the event
            }
        }
        
        switch (_interactionMode)
        {
            case ChartInteractionMode.Pan:
                _isDragging = true;
                ChartCanvas.CaptureMouse();
                break;
                
            case ChartInteractionMode.ZoomBox:
                StartZoomBox(position);
                break;
        }
    }

    private void ChartCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var position = e.GetPosition(ChartCanvas);
        
        // Handle drawing manager first
        _drawingManager.OnMouseUp(position, e.ChangedButton);
        
        switch (_interactionMode)
        {
            case ChartInteractionMode.Pan:
                if (_isDragging)
                {
                    _isDragging = false;
                    ChartCanvas.ReleaseMouseCapture();
                    
                    // Save viewport state to history
                    _navigationManager.PushState(_viewport);
                }
                break;
                
            case ChartInteractionMode.ZoomBox:
                if (_isZoomBoxActive)
                {
                    CompleteZoomBox(position);
                }
                break;
        }
    }

    private void ChartCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        var zoomFactor = e.Delta > 0 ? 0.9 : 1.1;
        var mousePosition = e.GetPosition(ChartCanvas);
        Zoom(zoomFactor, mousePosition);
    }

    #endregion

    #region Navigation Methods

    private void Pan(double deltaX, double deltaY)
    {
        if (_viewport.TimeRange.TotalMilliseconds <= 0 || _viewport.PriceRange <= 0) return;

        var timeDelta = TimeSpan.FromMilliseconds(deltaX / ChartCanvas.ActualWidth * _viewport.TimeRange.TotalMilliseconds);
        var priceDelta = (decimal)(deltaY / ChartCanvas.ActualHeight) * _viewport.PriceRange;

        _viewport.StartTime += timeDelta;
        _viewport.EndTime += timeDelta;
        _viewport.MinPrice += priceDelta;
        _viewport.MaxPrice += priceDelta;

        QueueUpdate();
    }

    private void Zoom(double factor, Point center)
    {
        if (_viewport.TimeRange.TotalMilliseconds <= 0 || _viewport.PriceRange <= 0) return;

        // Save current state before zooming
        _navigationManager.PushState(_viewport);

        var timeCenter = _viewport.StartTime.AddMilliseconds(center.X / ChartCanvas.ActualWidth * _viewport.TimeRange.TotalMilliseconds);
        var priceCenter = _viewport.MinPrice + (decimal)(1 - center.Y / ChartCanvas.ActualHeight) * _viewport.PriceRange;

        var newTimeRange = TimeSpan.FromMilliseconds(_viewport.TimeRange.TotalMilliseconds * factor);
        var newPriceRange = _viewport.PriceRange * (decimal)factor;

        _viewport.StartTime = timeCenter - TimeSpan.FromMilliseconds(newTimeRange.TotalMilliseconds * center.X / ChartCanvas.ActualWidth);
        _viewport.EndTime = _viewport.StartTime + newTimeRange;
        _viewport.MinPrice = priceCenter - newPriceRange * (decimal)(1 - center.Y / ChartCanvas.ActualHeight);
        _viewport.MaxPrice = _viewport.MinPrice + newPriceRange;

        ViewportChanged?.Invoke(this, _viewport);
        QueueUpdate();
    }

    /// <summary>
    /// Zoom in centered on the chart
    /// </summary>
    public void ZoomIn()
    {
        var center = new Point(ChartCanvas.ActualWidth / 2, ChartCanvas.ActualHeight / 2);
        Zoom(0.8, center);
    }

    /// <summary>
    /// Zoom out centered on the chart
    /// </summary>
    public void ZoomOut()
    {
        var center = new Point(ChartCanvas.ActualWidth / 2, ChartCanvas.ActualHeight / 2);
        Zoom(1.25, center);
    }

    /// <summary>
    /// Reset zoom to show all data
    /// </summary>
    public void ResetZoom()
    {
        FitToData();
    }

    /// <summary>
    /// Fit the chart to show all available data
    /// </summary>
    public void FitToData()
    {
        if (Data?.Any() != true) return;

        _navigationManager.PushState(_viewport);

        _viewport.StartTime = Data.Min(d => d.Timestamp);
        _viewport.EndTime = Data.Max(d => d.Timestamp);
        _viewport.MinPrice = Data.Min(d => d.Low);
        _viewport.MaxPrice = Data.Max(d => d.High);

        // Add some padding
        var timeRange = _viewport.TimeRange;
        var priceRange = _viewport.PriceRange;
        
        _viewport.StartTime -= TimeSpan.FromMilliseconds(timeRange.TotalMilliseconds * 0.05);
        _viewport.EndTime += TimeSpan.FromMilliseconds(timeRange.TotalMilliseconds * 0.05);
        _viewport.MinPrice -= priceRange * 0.05m;
        _viewport.MaxPrice += priceRange * 0.05m;

        ViewportChanged?.Invoke(this, _viewport);
        QueueUpdate();
    }

    #endregion

    #region Zoom Box Methods

    private void StartZoomBox(Point start)
    {
        _isZoomBoxActive = true;
        _zoomBoxStart = start;
        
        _zoomBoxRectangle = new Rectangle
        {
            Stroke = new SolidColorBrush(Colors.LightBlue),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = new SolidColorBrush(Color.FromArgb(30, 173, 216, 230))
        };
        
        ChartCanvas.Children.Add(_zoomBoxRectangle);
        ChartCanvas.CaptureMouse();
    }

    private void UpdateZoomBox(Point current)
    {
        if (_zoomBoxRectangle == null) return;

        var left = Math.Min(_zoomBoxStart.X, current.X);
        var top = Math.Min(_zoomBoxStart.Y, current.Y);
        var width = Math.Abs(current.X - _zoomBoxStart.X);
        var height = Math.Abs(current.Y - _zoomBoxStart.Y);

        Canvas.SetLeft(_zoomBoxRectangle, left);
        Canvas.SetTop(_zoomBoxRectangle, top);
        _zoomBoxRectangle.Width = width;
        _zoomBoxRectangle.Height = height;
    }

    private void CompleteZoomBox(Point end)
    {
        if (_zoomBoxRectangle == null) return;

        try
        {
            // Calculate zoom area
            var left = Math.Min(_zoomBoxStart.X, end.X);
            var right = Math.Max(_zoomBoxStart.X, end.X);
            var top = Math.Min(_zoomBoxStart.Y, end.Y);
            var bottom = Math.Max(_zoomBoxStart.Y, end.Y);

            // Convert to time and price ranges
            if (right - left > 10 && bottom - top > 10) // Minimum size requirement
            {
                // Save current state before zooming
                _navigationManager.PushState(_viewport);

                var leftRatio = left / ChartCanvas.ActualWidth;
                var rightRatio = right / ChartCanvas.ActualWidth;
                var topRatio = 1 - top / ChartCanvas.ActualHeight;
                var bottomRatio = 1 - bottom / ChartCanvas.ActualHeight;

                var newStartTime = _viewport.StartTime.AddMilliseconds(leftRatio * _viewport.TimeRange.TotalMilliseconds);
                var newEndTime = _viewport.StartTime.AddMilliseconds(rightRatio * _viewport.TimeRange.TotalMilliseconds);
                var newMinPrice = _viewport.MinPrice + (decimal)bottomRatio * _viewport.PriceRange;
                var newMaxPrice = _viewport.MinPrice + (decimal)topRatio * _viewport.PriceRange;

                _viewport.StartTime = newStartTime;
                _viewport.EndTime = newEndTime;
                _viewport.MinPrice = newMinPrice;
                _viewport.MaxPrice = newMaxPrice;

                ViewportChanged?.Invoke(this, _viewport);
                QueueUpdate();
            }
        }
        finally
        {
            // Clean up zoom box
            ChartCanvas.Children.Remove(_zoomBoxRectangle);
            _zoomBoxRectangle = null;
            _isZoomBoxActive = false;
            ChartCanvas.ReleaseMouseCapture();
        }
    }

    #endregion

    #region Navigation History Methods

    /// <summary>
    /// Goes back to the previous view
    /// </summary>
    public void NavigateBack()
    {
        var previousViewport = _navigationManager.GoBack();
        if (previousViewport != null)
        {
            _viewport = previousViewport;
            ViewportChanged?.Invoke(this, _viewport);
            QueueUpdate();
        }
    }

    /// <summary>
    /// Goes forward to the next view
    /// </summary>
    public void NavigateForward()
    {
        var nextViewport = _navigationManager.GoForward();
        if (nextViewport != null)
        {
            _viewport = nextViewport;
            ViewportChanged?.Invoke(this, _viewport);
            QueueUpdate();
        }
    }

    /// <summary>
    /// Checks if back navigation is possible
    /// </summary>
    public bool CanNavigateBack() => _navigationManager.CanGoBack();

    /// <summary>
    /// Checks if forward navigation is possible
    /// </summary>
    public bool CanNavigateForward() => _navigationManager.CanGoForward();

    /// <summary>
    /// Resets the chart to show all data
    /// </summary>
    public void ZoomToFit()
    {
        if (!_chartData.Any()) return;

        _navigationManager.PushState(_viewport);

        var minTime = _chartData.Min(d => d.Timestamp);
        var maxTime = _chartData.Max(d => d.Timestamp);
        var minPrice = _chartData.Min(d => d.Low);
        var maxPrice = _chartData.Max(d => d.High);

        // Add 5% padding
        var timePadding = TimeSpan.FromMilliseconds((maxTime - minTime).TotalMilliseconds * 0.05);
        var pricePadding = (maxPrice - minPrice) * 0.05m;

        _viewport.StartTime = minTime - timePadding;
        _viewport.EndTime = maxTime + timePadding;
        _viewport.MinPrice = minPrice - pricePadding;
        _viewport.MaxPrice = maxPrice + pricePadding;

        ViewportChanged?.Invoke(this, _viewport);
        QueueUpdate();
    }

    /// <summary>
    /// Zooms to a specific time range
    /// </summary>
    public void ZoomToTimeRange(TimeSpan range)
    {
        if (!_chartData.Any()) return;

        _navigationManager.PushState(_viewport);

        var latestTime = _chartData.Max(d => d.Timestamp);
        _viewport.EndTime = latestTime;
        _viewport.StartTime = latestTime - range;

        // Auto-fit price range for the time period
        var dataInRange = _chartData.Where(d => d.Timestamp >= _viewport.StartTime && d.Timestamp <= _viewport.EndTime);
        if (dataInRange.Any())
        {
            var minPrice = dataInRange.Min(d => d.Low);
            var maxPrice = dataInRange.Max(d => d.High);
            var pricePadding = (maxPrice - minPrice) * 0.05m;

            _viewport.MinPrice = minPrice - pricePadding;
            _viewport.MaxPrice = maxPrice + pricePadding;
        }

        ViewportChanged?.Invoke(this, _viewport);
        QueueUpdate();
    }

    /// <summary>
    /// Sets the chart interaction mode
    /// </summary>
    public void SetInteractionMode(ChartInteractionMode mode)
    {
        _interactionMode = mode;
        
        // Update cursor based on mode
        switch (mode)
        {
            case ChartInteractionMode.Pan:
                Cursor = Cursors.Hand;
                break;
            case ChartInteractionMode.ZoomBox:
                Cursor = Cursors.Cross;
                break;
            case ChartInteractionMode.Crosshair:
                Cursor = Cursors.Cross;
                break;
            default:
                Cursor = Cursors.Arrow;
                break;
        }
    }

    /// <summary>
    /// Sets the drawing mode for the chart
    /// </summary>
    public void SetDrawingMode(DrawingMode mode)
    {
        _drawingManager.CurrentMode = mode;
        
        // Update interaction mode based on drawing mode
        if (mode == DrawingMode.None || mode == DrawingMode.Select)
        {
            SetInteractionMode(ChartInteractionMode.Pan);
        }
        else
        {
            SetInteractionMode(ChartInteractionMode.Crosshair);
        }
    }

    /// <summary>
    /// Gets the current drawing mode
    /// </summary>
    public DrawingMode GetDrawingMode()
    {
        return _drawingManager.CurrentMode;
    }

    /// <summary>
    /// Adds a drawing tool to the chart
    /// </summary>
    public void AddDrawing(DrawingTool drawing)
    {
        _drawingManager.AddDrawing(drawing);
        QueueUpdate();
    }

    /// <summary>
    /// Removes a drawing from the chart
    /// </summary>
    public void RemoveDrawing(DrawingTool drawing)
    {
        _drawingManager.RemoveDrawing(drawing);
        QueueUpdate();
    }

    /// <summary>
    /// Clear all drawings from the chart
    /// </summary>
    public void ClearDrawings()
    {
        _drawingManager.ClearAllDrawings();
        QueueUpdate();
    }
    public ChartInteractionMode GetInteractionMode() => _interactionMode;

    #endregion

    #region Keyboard Event Handlers

    private void ChartControl_KeyDown(object sender, KeyEventArgs e)
    {
        var handled = true;

        switch (e.Key)
        {
            case Key.Home:
                ZoomToFit();
                break;

            case Key.Back:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt))
                    NavigateBack();
                else
                    handled = false;
                break;

            case Key.Right when Keyboard.Modifiers.HasFlag(ModifierKeys.Alt):
                NavigateForward();
                break;

            case Key.Add:
            case Key.OemPlus:
                Zoom(0.8, new Point(ActualWidth / 2, ActualHeight / 2));
                break;

            case Key.Subtract:
            case Key.OemMinus:
                Zoom(1.25, new Point(ActualWidth / 2, ActualHeight / 2));
                break;

            case Key.Left:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    Pan(-50, 0); // Fast pan
                else
                    Pan(-10, 0); // Slow pan
                break;

            case Key.Right:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    Pan(50, 0);
                else
                    Pan(10, 0);
                break;

            case Key.Up:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    Pan(0, -50);
                else
                    Pan(0, -10);
                break;

            case Key.Down:
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    Pan(0, 50);
                else
                    Pan(0, 10);
                break;

            case Key.D1 when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                ZoomToTimeRange(TimeSpan.FromHours(1));
                break;

            case Key.D2 when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                ZoomToTimeRange(TimeSpan.FromHours(4));
                break;

            case Key.D3 when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                ZoomToTimeRange(TimeSpan.FromDays(1));
                break;

            case Key.D4 when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                ZoomToTimeRange(TimeSpan.FromDays(7));
                break;

            case Key.D5 when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                ZoomToTimeRange(TimeSpan.FromDays(30));
                break;

            case Key.P when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                SetInteractionMode(ChartInteractionMode.Pan);
                break;

            case Key.Z when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                SetInteractionMode(ChartInteractionMode.ZoomBox);
                break;

            case Key.C when Keyboard.Modifiers.HasFlag(ModifierKeys.Control):
                SetInteractionMode(ChartInteractionMode.Crosshair);
                break;

            default:
                // Forward key events to drawing manager
                _drawingManager.OnKeyDown(e.Key);
                handled = false;
                break;
        }

        e.Handled = handled;
    }

    #endregion

    #region Button Event Handlers

    private void LineChartButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.ChartType = ChartType.Line;
        UpdateChart();
        SettingsChanged?.Invoke(this, _settings);
    }

    private void CandlestickButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.ChartType = ChartType.Candlestick;
        UpdateChart();
        SettingsChanged?.Invoke(this, _settings);
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO [REVIEWED]: Open chart settings dialog
        _logger.Info("Chart settings requested");
    }

    private void RetryButton_Click(object sender, RoutedEventArgs e)
    {
        HideError();
        SymbolRequested?.Invoke(this, _settings.Symbol);
    }

    #endregion

    #region Real-time Updates

    /// <summary>
    /// Updates the chart with real-time price data
    /// </summary>
    public void UpdateRealtimePrice(object quote)
    {
        try
        {
            // For now, just trigger a refresh
            // In advanced implementation, we would update the last candle
            Dispatcher.BeginInvoke(() => InvalidateVisual());
        }
        catch (Exception ex)
        {
            _logger.Error("Error updating real-time price", ex);
        }
    }

    #endregion

    #region Sample Data for Design Time

    private void LoadSampleData()
    {
        var random = new Random();
        var basePrice = 150m;
        var data = new List<CandlestickData>();
        
        for (int i = 0; i < 100; i++)
        {
            var timestamp = DateTime.Now.AddDays(-100 + i);
            var open = basePrice + (decimal)(random.NextDouble() - 0.5) * 5;
            var close = open + (decimal)(random.NextDouble() - 0.5) * 3;
            var high = Math.Max(open, close) + (decimal)random.NextDouble() * 2;
            var low = Math.Min(open, close) - (decimal)random.NextDouble() * 2;
            var volume = random.Next(100000, 1000000);
            
            data.Add(new CandlestickData
            {
                Timestamp = timestamp,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            });
            
            basePrice = close; // Trend continuation
        }
        
        SetData(data, "SAMPLE");
    }

    #endregion
}
