using System;
using System.Windows;
using System.Windows.Media;

namespace ApexV2.Charts.Drawing;

/// <summary>
/// Drawing tool for rectangles and boxes
/// </summary>
public class RectangleTool : DrawingTool
{
    /// <summary>
    /// Type of rectangle being drawn
    /// </summary>
    public enum RectangleType
    {
        Standard,
        PriceChannel,
        TimeRange
    }
    
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
    public RectangleType Type { get; set; } = RectangleType.Standard;
    
    /// <summary>
    /// Create a new rectangle tool
    /// </summary>
    public RectangleTool()
    {
        Stroke = Brushes.Cyan;
        StrokeThickness = 2.0;
        Fill = new SolidColorBrush(Color.FromArgb(50, 0, 255, 255)); // Semi-transparent cyan
    }
    
    /// <summary>
    /// Create a rectangle tool with specific type
    /// </summary>
    public RectangleTool(RectangleType rectangleType) : this()
    {
        Type = rectangleType;
    }
    
    public override void StartDrawing(Point startPoint)
    {
        StartPoint = startPoint;
        EndPoint = startPoint;
        IsDrawing = true;
        IsComplete = false;
    }
    
    public override void UpdateDrawing(Point currentPoint)
    {
        if (!IsDrawing) return;
        
        EndPoint = currentPoint;
    }
    
    public override void CompleteDrawing(Point endPoint)
    {
        EndPoint = endPoint;
        IsDrawing = false;
        IsComplete = true;
    }
    
    public override bool HitTest(Point point, double tolerance = 5.0)
    {
        if (!IsVisible || !IsComplete) return false;
        
        var rect = GetNormalizedRect();
        
        // Check if point is inside rectangle or near border
        if (rect.Contains(point)) return true;
        
        // Check distance to each edge
        var left = Math.Abs(point.X - rect.Left);
        var right = Math.Abs(point.X - rect.Right);
        var top = Math.Abs(point.Y - rect.Top);
        var bottom = Math.Abs(point.Y - rect.Bottom);
        
        // Near vertical edges
        if ((left <= tolerance || right <= tolerance) && 
            point.Y >= rect.Top - tolerance && point.Y <= rect.Bottom + tolerance)
            return true;
            
        // Near horizontal edges
        if ((top <= tolerance || bottom <= tolerance) && 
            point.X >= rect.Left - tolerance && point.X <= rect.Right + tolerance)
            return true;
            
        return false;
    }
    
    public override Rect GetBounds()
    {
        return GetNormalizedRect();
    }
    
    public override void Move(Vector offset)
    {
        StartPoint += offset;
        EndPoint += offset;
    }
    
    public override void Render(DrawingContext context, Rect viewport)
    {
        if (!IsVisible) return;
        
        var rect = GetNormalizedRect();
        
        // Don't render if rectangle is too small
        if (rect.Width < 2 || rect.Height < 2) return;
        
        var pen = new Pen(Stroke, StrokeThickness)
        {
            DashStyle = StrokeDashArray != null ? new DashStyle(StrokeDashArray, 0) : DashStyles.Solid
        };
        
        // Draw rectangle
        context.DrawRectangle(Fill, pen, rect);
        
        // Draw selection handles if selected
        if (IsSelected)
        {
            DrawSelectionHandles(context);
        }
        
        // Draw special features based on type
        switch (Type)
        {
            case RectangleType.PriceChannel:
                DrawPriceChannelLines(context, rect);
                break;
            case RectangleType.TimeRange:
                DrawTimeRangeIndicators(context, rect);
                break;
        }
    }
    
    public override DrawingTool Clone()
    {
        return new RectangleTool(Type)
        {
            StartPoint = StartPoint,
            EndPoint = EndPoint,
            Stroke = Stroke,
            StrokeThickness = StrokeThickness,
            StrokeDashArray = StrokeDashArray,
            Fill = Fill,
            Opacity = Opacity,
            IsVisible = IsVisible
        };
    }
    
    public override Point[] GetControlPoints()
    {
        var rect = GetNormalizedRect();
        return new[]
        {
            new Point(rect.Left, rect.Top),     // Top-left
            new Point(rect.Right, rect.Top),    // Top-right
            new Point(rect.Right, rect.Bottom), // Bottom-right
            new Point(rect.Left, rect.Bottom),  // Bottom-left
            new Point(rect.Left + rect.Width / 2, rect.Top),    // Top-center
            new Point(rect.Right, rect.Top + rect.Height / 2),  // Right-center
            new Point(rect.Left + rect.Width / 2, rect.Bottom), // Bottom-center
            new Point(rect.Left, rect.Top + rect.Height / 2)    // Left-center
        };
    }
    
    public override void UpdateControlPoint(int index, Point newPosition)
    {
        var rect = GetNormalizedRect();
        
        switch (index)
        {
            case 0: // Top-left
                StartPoint = new Point(newPosition.X, newPosition.Y);
                EndPoint = new Point(rect.Right, rect.Bottom);
                break;
            case 1: // Top-right
                StartPoint = new Point(rect.Left, newPosition.Y);
                EndPoint = new Point(newPosition.X, rect.Bottom);
                break;
            case 2: // Bottom-right
                StartPoint = new Point(rect.Left, rect.Top);
                EndPoint = new Point(newPosition.X, newPosition.Y);
                break;
            case 3: // Bottom-left
                StartPoint = new Point(newPosition.X, rect.Top);
                EndPoint = new Point(rect.Right, newPosition.Y);
                break;
            case 4: // Top-center
                StartPoint = new Point(rect.Left, newPosition.Y);
                EndPoint = new Point(rect.Right, rect.Bottom);
                break;
            case 5: // Right-center
                StartPoint = new Point(rect.Left, rect.Top);
                EndPoint = new Point(newPosition.X, rect.Bottom);
                break;
            case 6: // Bottom-center
                StartPoint = new Point(rect.Left, rect.Top);
                EndPoint = new Point(rect.Right, newPosition.Y);
                break;
            case 7: // Left-center
                StartPoint = new Point(newPosition.X, rect.Top);
                EndPoint = new Point(rect.Right, rect.Bottom);
                break;
        }
    }
    
    private Rect GetNormalizedRect()
    {
        var left = Math.Min(StartPoint.X, EndPoint.X);
        var top = Math.Min(StartPoint.Y, EndPoint.Y);
        var right = Math.Max(StartPoint.X, EndPoint.X);
        var bottom = Math.Max(StartPoint.Y, EndPoint.Y);
        
        return new Rect(left, top, right - left, bottom - top);
    }
    
    private void DrawSelectionHandles(DrawingContext context)
    {
        var handleBrush = Brushes.White;
        var handlePen = new Pen(Brushes.Black, 1.0);
        var handleSize = 6.0;
        
        // Draw handles at control points
        var handles = GetControlPoints();
        foreach (var handle in handles)
        {
            var rect = new Rect(
                handle.X - handleSize / 2,
                handle.Y - handleSize / 2,
                handleSize, handleSize);
            context.DrawRectangle(handleBrush, handlePen, rect);
        }
    }
    
    private void DrawPriceChannelLines(DrawingContext context, Rect rect)
    {
        var pen = new Pen(Stroke, 1.0) { DashStyle = DashStyles.Dot };
        
        // Draw center line
        var centerY = rect.Top + rect.Height / 2;
        context.DrawLine(pen, new Point(rect.Left, centerY), new Point(rect.Right, centerY));
    }
    
    private void DrawTimeRangeIndicators(DrawingContext context, Rect rect)
    {
        var pen = new Pen(Stroke, 1.0) { DashStyle = DashStyles.Dash };
        
        // Draw vertical center line
        var centerX = rect.Left + rect.Width / 2;
        context.DrawLine(pen, new Point(centerX, rect.Top), new Point(centerX, rect.Bottom));
    }
}


