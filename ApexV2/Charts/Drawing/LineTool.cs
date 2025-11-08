using System;
using System.Windows;
using System.Windows.Media;

namespace ApexV2.Charts.Drawing;

/// <summary>
/// Drawing tool for trend lines and horizontal/vertical lines
/// </summary>
public class LineTool : DrawingTool
{
    /// <summary>
    /// Type of line being drawn
    /// </summary>
    public enum LineType
    {
        Trend,
        Horizontal,
        Vertical
    }
    
    public Point StartPoint { get; set; }
    public Point EndPoint { get; set; }
    public LineType Type { get; set; } = LineType.Trend;
    
    /// <summary>
    /// Create a new line tool
    /// </summary>
    public LineTool()
    {
        Stroke = Brushes.Yellow;
        StrokeThickness = 2.0;
    }
    
    /// <summary>
    /// Create a line tool with specific type
    /// </summary>
    public LineTool(LineType lineType) : this()
    {
        Type = lineType;
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
        
        switch (Type)
        {
            case LineType.Horizontal:
                EndPoint = new Point(currentPoint.X, StartPoint.Y);
                break;
            case LineType.Vertical:
                EndPoint = new Point(StartPoint.X, currentPoint.Y);
                break;
            case LineType.Trend:
            default:
                EndPoint = currentPoint;
                break;
        }
    }
    
    public override void CompleteDrawing(Point endPoint)
    {
        UpdateDrawing(endPoint);
        IsDrawing = false;
        IsComplete = true;
    }
    
    public override bool HitTest(Point point, double tolerance = 5.0)
    {
        if (!IsVisible || !IsComplete) return false;
        
        // Calculate distance from point to line
        var distance = DistanceFromPointToLine(point, StartPoint, EndPoint);
        return distance <= tolerance;
    }
    
    public override Rect GetBounds()
    {
        var minX = Math.Min(StartPoint.X, EndPoint.X);
        var minY = Math.Min(StartPoint.Y, EndPoint.Y);
        var maxX = Math.Max(StartPoint.X, EndPoint.X);
        var maxY = Math.Max(StartPoint.Y, EndPoint.Y);
        
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
    
    public override void Move(Vector offset)
    {
        StartPoint += offset;
        EndPoint += offset;
    }
    
    public override void Render(DrawingContext context, Rect viewport)
    {
        if (!IsVisible) return;
        
        var pen = new Pen(Stroke, StrokeThickness)
        {
            DashStyle = StrokeDashArray != null ? new DashStyle(StrokeDashArray, 0) : DashStyles.Solid
        };
        
        // Extend line for infinite lines
        var start = StartPoint;
        var end = EndPoint;
        
        if (Type == LineType.Horizontal)
        {
            start = new Point(viewport.Left, StartPoint.Y);
            end = new Point(viewport.Right, StartPoint.Y);
        }
        else if (Type == LineType.Vertical)
        {
            start = new Point(StartPoint.X, viewport.Top);
            end = new Point(StartPoint.X, viewport.Bottom);
        }
        
        context.DrawLine(pen, start, end);
        
        // Draw selection handles if selected
        if (IsSelected)
        {
            DrawSelectionHandles(context);
        }
    }
    
    public override DrawingTool Clone()
    {
        return new LineTool(Type)
        {
            StartPoint = StartPoint,
            EndPoint = EndPoint,
            Stroke = Stroke,
            StrokeThickness = StrokeThickness,
            StrokeDashArray = StrokeDashArray,
            Opacity = Opacity,
            IsVisible = IsVisible
        };
    }
    
    public override Point[] GetControlPoints()
    {
        return new[] { StartPoint, EndPoint };
    }
    
    public override void UpdateControlPoint(int index, Point newPosition)
    {
        switch (index)
        {
            case 0:
                StartPoint = newPosition;
                break;
            case 1:
                EndPoint = newPosition;
                break;
        }
    }
    
    private void DrawSelectionHandles(DrawingContext context)
    {
        var handleBrush = Brushes.White;
        var handlePen = new Pen(Brushes.Black, 1.0);
        var handleSize = 6.0;
        
        // Draw handles at start and end points
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
    
    private static double DistanceFromPointToLine(Point point, Point lineStart, Point lineEnd)
    {
        var A = point.X - lineStart.X;
        var B = point.Y - lineStart.Y;
        var C = lineEnd.X - lineStart.X;
        var D = lineEnd.Y - lineStart.Y;
        
        var dot = A * C + B * D;
        var lenSq = C * C + D * D;
        
        if (lenSq == 0) return Math.Sqrt(A * A + B * B);
        
        var param = dot / lenSq;
        
        double xx, yy;
        if (param < 0)
        {
            xx = lineStart.X;
            yy = lineStart.Y;
        }
        else if (param > 1)
        {
            xx = lineEnd.X;
            yy = lineEnd.Y;
        }
        else
        {
            xx = lineStart.X + param * C;
            yy = lineStart.Y + param * D;
        }
        
        var dx = point.X - xx;
        var dy = point.Y - yy;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
