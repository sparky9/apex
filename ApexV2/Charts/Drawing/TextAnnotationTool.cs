using System;
using System.Windows;
using System.Windows.Media;

namespace ApexV2.Charts.Drawing;

/// <summary>
/// Drawing tool for text annotations
/// </summary>
public class TextAnnotationTool : DrawingTool
{
    public Point Position { get; set; }
    public string Text { get; set; } = "Annotation";
    public double FontSize { get; set; } = 12.0;
    public FontFamily FontFamily { get; set; } = new FontFamily("Segoe UI");
    public FontWeight FontWeight { get; set; } = FontWeights.Normal;
    public FontStyle FontStyle { get; set; } = FontStyles.Normal;
    public Brush TextBrush { get; set; } = Brushes.White;
    public Brush? BackgroundBrush { get; set; }
    public bool HasBackground { get; set; } = true;
    public Thickness Padding { get; set; } = new Thickness(4, 2, 4, 2);
    
    private FormattedText? _formattedText;
    private bool _needsTextUpdate = true;
    
    /// <summary>
    /// Create a new text annotation tool
    /// </summary>
    public TextAnnotationTool()
    {
        BackgroundBrush = new SolidColorBrush(Color.FromArgb(180, 0, 0, 0)); // Semi-transparent black
        Stroke = Brushes.Gray;
        StrokeThickness = 1.0;
    }
    
    /// <summary>
    /// Create a text annotation with specific text
    /// </summary>
    public TextAnnotationTool(string text) : this()
    {
        Text = text ?? "Annotation";
        _needsTextUpdate = true;
    }
    
    public override void StartDrawing(Point startPoint)
    {
        Position = startPoint;
        IsDrawing = true;
        IsComplete = false;
    }
    
    public override void UpdateDrawing(Point currentPoint)
    {
        if (!IsDrawing) return;
        Position = currentPoint;
    }
    
    public override void CompleteDrawing(Point endPoint)
    {
        Position = endPoint;
        IsDrawing = false;
        IsComplete = true;
    }
    
    public override bool HitTest(Point point, double tolerance = 5.0)
    {
        if (!IsVisible || !IsComplete) return false;
        
        var bounds = GetTextBounds();
        return bounds.Contains(point);
    }
    
    public override Rect GetBounds()
    {
        return GetTextBounds();
    }
    
    public override void Move(Vector offset)
    {
        Position += offset;
    }
    
    public override void Render(DrawingContext context, Rect viewport)
    {
        if (!IsVisible || string.IsNullOrEmpty(Text)) return;
        
        UpdateFormattedText();
        if (_formattedText == null) return;
        
        var textBounds = GetTextBounds();
        
        // Draw background if enabled
        if (HasBackground && BackgroundBrush != null)
        {
            var backgroundRect = new Rect(
                textBounds.X - Padding.Left,
                textBounds.Y - Padding.Top,
                textBounds.Width + Padding.Left + Padding.Right,
                textBounds.Height + Padding.Top + Padding.Bottom);
                
            context.DrawRectangle(BackgroundBrush, 
                new Pen(Stroke, StrokeThickness), backgroundRect);
        }
        
        // Draw text
        context.DrawText(_formattedText, Position);
        
        // Draw selection indicator if selected
        if (IsSelected)
        {
            DrawSelectionIndicator(context, textBounds);
        }
    }
    
    public override DrawingTool Clone()
    {
        return new TextAnnotationTool(Text)
        {
            Position = Position,
            FontSize = FontSize,
            FontFamily = FontFamily,
            FontWeight = FontWeight,
            FontStyle = FontStyle,
            TextBrush = TextBrush,
            BackgroundBrush = BackgroundBrush,
            HasBackground = HasBackground,
            Padding = Padding,
            Stroke = Stroke,
            StrokeThickness = StrokeThickness,
            Opacity = Opacity,
            IsVisible = IsVisible
        };
    }
    
    public override Point[] GetControlPoints()
    {
        var bounds = GetTextBounds();
        return new[]
        {
            new Point(bounds.Left, bounds.Top),
            new Point(bounds.Right, bounds.Top),
            new Point(bounds.Right, bounds.Bottom),
            new Point(bounds.Left, bounds.Bottom)
        };
    }
    
    public override void UpdateControlPoint(int index, Point newPosition)
    {
        // For text annotations, just move the entire annotation
        var bounds = GetTextBounds();
        var offset = newPosition - new Point(bounds.Left, bounds.Top);
        Move(offset);
    }
    
    /// <summary>
    /// Update the text content
    /// </summary>
    public void UpdateText(string newText)
    {
        if (Text != newText)
        {
            Text = newText ?? string.Empty;
            _needsTextUpdate = true;
        }
    }
    
    /// <summary>
    /// Update text formatting
    /// </summary>
    public void UpdateFormat(double fontSize, FontFamily fontFamily, FontWeight fontWeight, FontStyle fontStyle)
    {
        if (Math.Abs(FontSize - fontSize) > 0.1 || 
            !FontFamily.Equals(fontFamily) || 
            FontWeight != fontWeight || 
            FontStyle != fontStyle)
        {
            FontSize = fontSize;
            FontFamily = fontFamily;
            FontWeight = fontWeight;
            FontStyle = fontStyle;
            _needsTextUpdate = true;
        }
    }
    
    private void UpdateFormattedText()
    {
        if (!_needsTextUpdate && _formattedText != null) return;
        
        if (string.IsNullOrEmpty(Text))
        {
            _formattedText = null;
            return;
        }
        
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretches.Normal);
        
        _formattedText = new FormattedText(
            Text,
            System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize,
            TextBrush,
            VisualTreeHelper.GetDpi(Application.Current.MainWindow).PixelsPerDip);
            
        _needsTextUpdate = false;
    }
    
    private Rect GetTextBounds()
    {
        UpdateFormattedText();
        if (_formattedText == null) return new Rect(Position, new Size(0, 0));
        
        return new Rect(Position.X, Position.Y, _formattedText.Width, _formattedText.Height);
    }
    
    private void DrawSelectionIndicator(DrawingContext context, Rect textBounds)
    {
        var selectionPen = new Pen(Brushes.Yellow, 1.0) { DashStyle = DashStyles.Dash };
        var selectionRect = new Rect(
            textBounds.X - 2,
            textBounds.Y - 2,
            textBounds.Width + 4,
            textBounds.Height + 4);
            
        context.DrawRectangle(null, selectionPen, selectionRect);
        
        // Draw resize handle at bottom-right
        var handleSize = 6.0;
        var handleRect = new Rect(
            selectionRect.Right - handleSize / 2,
            selectionRect.Bottom - handleSize / 2,
            handleSize, handleSize);
        context.DrawRectangle(Brushes.Yellow, new Pen(Brushes.Black, 1.0), handleRect);
    }
}
