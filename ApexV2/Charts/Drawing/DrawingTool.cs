using System;
using System.Windows;
using System.Windows.Media;

namespace ApexV2.Charts.Drawing;

/// <summary>
/// Base class for all drawing tools
/// </summary>
public abstract class DrawingTool
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public DateTime CreatedAt { get; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "User";
    public bool IsVisible { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public bool IsSelected { get; set; } = false;
    
    // Visual properties
    public Brush Stroke { get; set; } = Brushes.Yellow;
    public double StrokeThickness { get; set; } = 2.0;
    public DoubleCollection? StrokeDashArray { get; set; }
    public Brush? Fill { get; set; }
    public double Opacity { get; set; } = 1.0;
    
    // Drawing state
    public bool IsDrawing { get; set; } = false;
    public bool IsComplete { get; set; } = false;
    
    /// <summary>
    /// Start drawing the tool at the specified point
    /// </summary>
    public abstract void StartDrawing(Point startPoint);
    
    /// <summary>
    /// Update the tool as the user moves the mouse
    /// </summary>
    public abstract void UpdateDrawing(Point currentPoint);
    
    /// <summary>
    /// Complete the drawing of the tool
    /// </summary>
    public abstract void CompleteDrawing(Point endPoint);
    
    /// <summary>
    /// Cancel the current drawing operation
    /// </summary>
    public virtual void CancelDrawing()
    {
        IsDrawing = false;
        IsComplete = false;
    }
    
    /// <summary>
    /// Check if a point is within the tool's bounds for selection
    /// </summary>
    public abstract bool HitTest(Point point, double tolerance = 5.0);
    
    /// <summary>
    /// Get the bounding rectangle of the tool
    /// </summary>
    public abstract Rect GetBounds();
    
    /// <summary>
    /// Move the tool by the specified offset
    /// </summary>
    public abstract void Move(Vector offset);
    
    /// <summary>
    /// Render the tool on the given drawing context
    /// </summary>
    public abstract void Render(DrawingContext context, Rect viewport);
    
    /// <summary>
    /// Clone the drawing tool
    /// </summary>
    public abstract DrawingTool Clone();
    
    /// <summary>
    /// Get control points for resizing/editing
    /// </summary>
    public virtual Point[] GetControlPoints()
    {
        return Array.Empty<Point>();
    }
    
    /// <summary>
    /// Update control point position
    /// </summary>
    public virtual void UpdateControlPoint(int index, Point newPosition)
    {
        // Override in derived classes
    }
}
