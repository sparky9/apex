using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ApexV2.Charts.Models;
using ApexV2.Core.Logging;

namespace ApexV2.Charts.Renderer;

/// <summary>
/// Core chart rendering service that converts data to visual elements
/// </summary>
public class ChartRenderService
{
    private readonly Logger _logger;

    public ChartRenderService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Renders candlestick data to drawing context
    /// </summary>
    public void RenderCandlesticks(DrawingContext drawingContext, 
        IEnumerable<CandlestickData> data, 
        ChartViewport viewport, 
        ChartSettings settings,
        Size canvasSize)
    {
        if (drawingContext == null || data == null || viewport == null || settings == null)
        {
            _logger.Warn("RenderCandlesticks: Invalid parameters provided");
            return;
        }

        var candles = data.Where(c => viewport.ContainsTime(c.Timestamp)).ToList();
        if (!candles.Any())
        {
            _logger.Debug("No candles in viewport to render");
            return;
        }

        var bullishBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.BullishColor));
        var bearishBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.BearishColor));
        var pen = new Pen(Brushes.Black, 1);

        var timeRange = viewport.TimeRange;
        var priceRange = viewport.PriceRange;

        foreach (var candle in candles)
        {
            // Calculate X position based on time
            var timeOffset = candle.Timestamp - viewport.StartTime;
            var xRatio = timeOffset.TotalMilliseconds / timeRange.TotalMilliseconds;
            var x = xRatio * canvasSize.Width;

            // Calculate Y positions based on price (inverted because Y increases downward)
            var openY = canvasSize.Height - ((double)(candle.Open - viewport.MinPrice) / (double)priceRange * canvasSize.Height);
            var highY = canvasSize.Height - ((double)(candle.High - viewport.MinPrice) / (double)priceRange * canvasSize.Height);
            var lowY = canvasSize.Height - ((double)(candle.Low - viewport.MinPrice) / (double)priceRange * canvasSize.Height);
            var closeY = canvasSize.Height - ((double)(candle.Close - viewport.MinPrice) / (double)priceRange * canvasSize.Height);

            var brush = candle.IsBullish ? bullishBrush : bearishBrush;
            var candleWidth = settings.CandleWidth;
            var halfWidth = candleWidth / 2.0;

            // Draw the wick (high-low line)
            drawingContext.DrawLine(pen, new Point(x, highY), new Point(x, lowY));

            // Draw the body (open-close rectangle)
            var bodyTop = Math.Min(openY, closeY);
            var bodyHeight = Math.Abs(openY - closeY);
            
            // Ensure minimum body height for visibility
            if (bodyHeight < 1) bodyHeight = 1;

            var bodyRect = new Rect(x - halfWidth, bodyTop, candleWidth, bodyHeight);
            drawingContext.DrawRectangle(brush, pen, bodyRect);
        }

        _logger.Debug($"Rendered {candles.Count} candlesticks");
    }

    /// <summary>
    /// Renders price line chart
    /// </summary>
    public void RenderPriceLine(DrawingContext drawingContext,
        IEnumerable<CandlestickData> data,
        ChartViewport viewport,
        ChartSettings settings,
        Size canvasSize,
        PriceLineType lineType = PriceLineType.Close)
    {
        if (drawingContext == null || data == null || viewport == null || settings == null)
        {
            _logger.Warn("RenderPriceLine: Invalid parameters provided");
            return;
        }

        var candles = data.Where(c => viewport.ContainsTime(c.Timestamp)).ToList();
        if (candles.Count < 2)
        {
            _logger.Debug("Insufficient data points for price line");
            return;
        }

        var pen = new Pen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.BullishColor)), 2);
        var timeRange = viewport.TimeRange;
        var priceRange = viewport.PriceRange;

        Point? previousPoint = null;

        foreach (var candle in candles)
        {
            // Get price based on line type
            var price = lineType switch
            {
                PriceLineType.Open => candle.Open,
                PriceLineType.High => candle.High,
                PriceLineType.Low => candle.Low,
                PriceLineType.Close => candle.Close,
                _ => candle.Close
            };

            // Calculate position
            var timeOffset = candle.Timestamp - viewport.StartTime;
            var xRatio = timeOffset.TotalMilliseconds / timeRange.TotalMilliseconds;
            var x = xRatio * canvasSize.Width;
            var y = canvasSize.Height - ((double)(price - viewport.MinPrice) / (double)priceRange * canvasSize.Height);

            var currentPoint = new Point(x, y);

            if (previousPoint.HasValue)
            {
                drawingContext.DrawLine(pen, previousPoint.Value, currentPoint);
            }

            previousPoint = currentPoint;
        }

        _logger.Debug($"Rendered price line with {candles.Count} points");
    }

    /// <summary>
    /// Renders chart grid
    /// </summary>
    public void RenderGrid(DrawingContext drawingContext,
        ChartViewport viewport,
        ChartSettings settings,
        Size canvasSize)
    {
        if (!settings.ShowGrid) return;

        var gridBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(settings.GridColor));
        var gridPen = new Pen(gridBrush, 0.5);

        // Vertical grid lines (time)
        var timeGridCount = 10;
        for (int i = 0; i <= timeGridCount; i++)
        {
            var x = (i / (double)timeGridCount) * canvasSize.Width;
            drawingContext.DrawLine(gridPen, new Point(x, 0), new Point(x, canvasSize.Height));
        }

        // Horizontal grid lines (price)
        var priceGridCount = 8;
        for (int i = 0; i <= priceGridCount; i++)
        {
            var y = (i / (double)priceGridCount) * canvasSize.Height;
            drawingContext.DrawLine(gridPen, new Point(0, y), new Point(canvasSize.Width, y));
        }
    }

    /// <summary>
    /// Calculates optimal viewport for given data
    /// </summary>
    public ChartViewport CalculateViewport(IEnumerable<CandlestickData> data, ChartSettings settings)
    {
        if (data == null || !data.Any())
        {
            return new ChartViewport
            {
                StartTime = DateTime.Now.AddDays(-30),
                EndTime = DateTime.Now,
                MinPrice = 0,
                MaxPrice = 100
            };
        }

        var candles = data.ToList();
        var startTime = candles.Min(c => c.Timestamp);
        var endTime = candles.Max(c => c.Timestamp);
        var minPrice = candles.Min(c => c.Low);
        var maxPrice = candles.Max(c => c.High);

        // Add padding
        var priceRange = maxPrice - minPrice;
        var pricePadding = priceRange * 0.05m; // 5% padding
        
        var timeRange = endTime - startTime;
        var timePadding = TimeSpan.FromMilliseconds(timeRange.TotalMilliseconds * 0.02); // 2% padding

        return new ChartViewport
        {
            StartTime = startTime - timePadding,
            EndTime = endTime + timePadding,
            MinPrice = minPrice - pricePadding,
            MaxPrice = maxPrice + pricePadding
        };
    }
}

/// <summary>
/// Type of price line to render
/// </summary>
public enum PriceLineType
{
    Open,
    High, 
    Low,
    Close
}
