using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ApexV2.Charts.Drawing;

/// <summary>
/// Manages drawing tools and annotations on the chart
/// </summary>
public class DrawingManager
{
    private readonly List<DrawingTool> _drawings = new();
    private DrawingTool? _activeDrawing;
    private DrawingTool? _selectedDrawing;
    private DrawingMode _currentMode = DrawingMode.None;
    private bool _isDragging;
    private Point _lastDragPoint;
    private int _selectedControlPoint = -1;
    
    public event EventHandler<DrawingTool>? DrawingAdded;
    public event EventHandler<DrawingTool>? DrawingRemoved;
    public event EventHandler<DrawingTool>? DrawingSelected;
    public event EventHandler? SelectionCleared;
    
    /// <summary>
    /// Current drawing mode
    /// </summary>
    public DrawingMode CurrentMode
    {
        get => _currentMode;
        set
        {
            if (_currentMode != value)
            {
                _currentMode = value;
                CancelActiveDrawing();
                ClearSelection();
            }
        }
    }
    
    /// <summary>
    /// All drawings in the manager
    /// </summary>
    public IReadOnlyList<DrawingTool> Drawings => _drawings.AsReadOnly();
    
    /// <summary>
    /// Currently selected drawing
    /// </summary>
    public DrawingTool? SelectedDrawing => _selectedDrawing;
    
    /// <summary>
    /// Whether a drawing is currently being created
    /// </summary>
    public bool IsDrawing => _activeDrawing?.IsDrawing == true;
    
    /// <summary>
    /// Add a drawing to the manager
    /// </summary>
    public void AddDrawing(DrawingTool drawing)
    {
        if (drawing == null) return;
        
        _drawings.Add(drawing);
        DrawingAdded?.Invoke(this, drawing);
    }
    
    /// <summary>
    /// Remove a drawing from the manager
    /// </summary>
    public void RemoveDrawing(DrawingTool drawing)
    {
        if (drawing == null) return;
        
        if (_drawings.Remove(drawing))
        {
            if (_selectedDrawing == drawing)
            {
                ClearSelection();
            }
            DrawingRemoved?.Invoke(this, drawing);
        }
    }
    
    /// <summary>
    /// Remove the currently selected drawing
    /// </summary>
    public void RemoveSelectedDrawing()
    {
        if (_selectedDrawing != null)
        {
            RemoveDrawing(_selectedDrawing);
        }
    }
    
    /// <summary>
    /// Clear all drawings
    /// </summary>
    public void ClearAllDrawings()
    {
        var drawingsToRemove = _drawings.ToList();
        _drawings.Clear();
        ClearSelection();
        CancelActiveDrawing();
        
        foreach (var drawing in drawingsToRemove)
        {
            DrawingRemoved?.Invoke(this, drawing);
        }
    }
    
    /// <summary>
    /// Select a drawing
    /// </summary>
    public void SelectDrawing(DrawingTool? drawing)
    {
        if (_selectedDrawing == drawing) return;
        
        if (_selectedDrawing != null)
        {
            _selectedDrawing.IsSelected = false;
        }
        
        _selectedDrawing = drawing;
        
        if (_selectedDrawing != null)
        {
            _selectedDrawing.IsSelected = true;
            DrawingSelected?.Invoke(this, _selectedDrawing);
        }
        else
        {
            SelectionCleared?.Invoke(this, EventArgs.Empty);
        }
    }
    
    /// <summary>
    /// Clear the current selection
    /// </summary>
    public void ClearSelection()
    {
        SelectDrawing(null);
    }
    
    /// <summary>
    /// Handle mouse down event
    /// </summary>
    public void OnMouseDown(Point position, MouseButton button)
    {
        if (button != MouseButton.Left) return;
        
        if (_currentMode == DrawingMode.Select)
        {
            HandleSelectionMouseDown(position);
        }
        else if (_currentMode != DrawingMode.None)
        {
            HandleDrawingMouseDown(position);
        }
    }
    
    /// <summary>
    /// Handle mouse move event
    /// </summary>
    public void OnMouseMove(Point position)
    {
        if (_activeDrawing?.IsDrawing == true)
        {
            _activeDrawing.UpdateDrawing(position);
        }
        else if (_isDragging && _selectedDrawing != null)
        {
            HandleDragging(position);
        }
    }
    
    /// <summary>
    /// Handle mouse up event
    /// </summary>
    public void OnMouseUp(Point position, MouseButton button)
    {
        if (button != MouseButton.Left) return;
        
        if (_activeDrawing?.IsDrawing == true)
        {
            _activeDrawing.CompleteDrawing(position);
            AddDrawing(_activeDrawing);
            _activeDrawing = null;
        }
        
        if (_isDragging)
        {
            _isDragging = false;
            _selectedControlPoint = -1;
        }
    }
    
    /// <summary>
    /// Handle key down event
    /// </summary>
    public void OnKeyDown(Key key)
    {
        switch (key)
        {
            case Key.Delete:
                RemoveSelectedDrawing();
                break;
                
            case Key.Escape:
                if (IsDrawing)
                {
                    CancelActiveDrawing();
                }
                else
                {
                    ClearSelection();
                }
                break;
        }
    }
    
    /// <summary>
    /// Render all drawings
    /// </summary>
    public void Render(DrawingContext context, Rect viewport)
    {
        // Render completed drawings
        foreach (var drawing in _drawings.Where(d => d.IsVisible))
        {
            drawing.Render(context, viewport);
        }
        
        // Render active drawing
        if (_activeDrawing?.IsDrawing == true)
        {
            _activeDrawing.Render(context, viewport);
        }
    }
    
    /// <summary>
    /// Get drawing at position with hit testing
    /// </summary>
    public DrawingTool? GetDrawingAtPosition(Point position, double tolerance = 5.0)
    {
        // Check in reverse order (top-most first)
        for (int i = _drawings.Count - 1; i >= 0; i--)
        {
            var drawing = _drawings[i];
            if (drawing.IsVisible && drawing.HitTest(position, tolerance))
            {
                return drawing;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Cancel the currently active drawing
    /// </summary>
    private void CancelActiveDrawing()
    {
        if (_activeDrawing?.IsDrawing == true)
        {
            _activeDrawing.CancelDrawing();
        }
        _activeDrawing = null;
    }
    
    private void HandleSelectionMouseDown(Point position)
    {
        var hitDrawing = GetDrawingAtPosition(position);
        
        if (hitDrawing != null)
        {
            SelectDrawing(hitDrawing);
            
            // Check if clicking on a control point
            var controlPoints = hitDrawing.GetControlPoints();
            for (int i = 0; i < controlPoints.Length; i++)
            {
                var controlPoint = controlPoints[i];
                var distance = (position - controlPoint).Length;
                if (distance <= 5.0)
                {
                    _selectedControlPoint = i;
                    _isDragging = true;
                    _lastDragPoint = position;
                    return;
                }
            }
            
            // Start dragging the entire drawing
            _isDragging = true;
            _lastDragPoint = position;
        }
        else
        {
            ClearSelection();
        }
    }
    
    private void HandleDrawingMouseDown(Point position)
    {
        ClearSelection();
        
        _activeDrawing = CreateDrawingTool(_currentMode);
        if (_activeDrawing != null)
        {
            _activeDrawing.StartDrawing(position);
        }
    }
    
    private void HandleDragging(Point position)
    {
        if (_selectedDrawing == null) return;
        
        var offset = position - _lastDragPoint;
        
        if (_selectedControlPoint >= 0)
        {
            // Move control point
            _selectedDrawing.UpdateControlPoint(_selectedControlPoint, position);
        }
        else
        {
            // Move entire drawing
            _selectedDrawing.Move(offset);
        }
        
        _lastDragPoint = position;
    }
    
    private DrawingTool? CreateDrawingTool(DrawingMode mode)
    {
        return mode switch
        {
            DrawingMode.TrendLine => new LineTool(LineTool.LineType.Trend),
            DrawingMode.HorizontalLine => new LineTool(LineTool.LineType.Horizontal),
            DrawingMode.VerticalLine => new LineTool(LineTool.LineType.Vertical),
            DrawingMode.Rectangle => new RectangleTool(RectangleTool.RectangleType.Standard),
            DrawingMode.PriceChannel => new RectangleTool(RectangleTool.RectangleType.PriceChannel),
            DrawingMode.TimeRange => new RectangleTool(RectangleTool.RectangleType.TimeRange),
            DrawingMode.TextAnnotation => new TextAnnotationTool(),
            _ => null
        };
    }
}

/// <summary>
/// Drawing modes for the chart
/// </summary>
public enum DrawingMode
{
    None,
    Select,
    TrendLine,
    HorizontalLine,
    VerticalLine,
    Rectangle,
    PriceChannel,
    TimeRange,
    TextAnnotation
}
