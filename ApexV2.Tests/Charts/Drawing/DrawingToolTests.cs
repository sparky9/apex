using System;
using Xunit;
using System.Windows;
using System.Windows.Media;
using ApexV2.Charts.Drawing;

namespace ApexV2.Tests.Charts.Drawing;

public class DrawingToolTests
{
    [Fact]
    public void DrawingTool_BaseProperties_ShouldHaveCorrectDefaults()
    {
        // Arrange & Act
        var tool = new TestDrawingTool();
        
        // Assert
        Assert.False(tool.IsDrawing);
        Assert.False(tool.IsComplete);
        Assert.False(tool.IsSelected);
        Assert.True(tool.IsVisible);
        Assert.Equal(1.0, tool.Opacity);
        Assert.NotNull(tool.Stroke);
        Assert.Equal(1.0, tool.StrokeThickness);
    }
    
    [Fact]
    public void DrawingTool_StartDrawing_ShouldSetCorrectState()
    {
        // Arrange
        var tool = new TestDrawingTool();
        var startPoint = new Point(10, 20);
        
        // Act
        tool.StartDrawing(startPoint);
        
        // Assert
        Assert.True(tool.IsDrawing);
        Assert.False(tool.IsComplete);
    }
    
    [Fact]
    public void DrawingTool_CompleteDrawing_ShouldSetCorrectState()
    {
        // Arrange
        var tool = new TestDrawingTool();
        var startPoint = new Point(10, 20);
        var endPoint = new Point(30, 40);
        
        // Act
        tool.StartDrawing(startPoint);
        tool.CompleteDrawing(endPoint);
        
        // Assert
        Assert.False(tool.IsDrawing);
        Assert.True(tool.IsComplete);
    }
    
    [Fact]
    public void DrawingTool_CancelDrawing_ShouldResetState()
    {
        // Arrange
        var tool = new TestDrawingTool();
        var startPoint = new Point(10, 20);
        
        // Act
        tool.StartDrawing(startPoint);
        tool.CancelDrawing();
        
        // Assert
        Assert.False(tool.IsDrawing);
        Assert.False(tool.IsComplete);
    }
    
    [Fact]
    public void DrawingTool_Clone_ShouldCreateIndependentCopy()
    {
        // Arrange
        var original = new TestDrawingTool();
        original.Stroke = Brushes.Red;
        original.StrokeThickness = 2.0;
        original.Opacity = 0.5;
        original.IsVisible = false;
        
        // Act
        var cloned = original.Clone();
        
        // Assert
        Assert.NotSame(original, cloned);
        Assert.Equal(original.StrokeThickness, cloned.StrokeThickness);
        Assert.Equal(original.Opacity, cloned.Opacity);
        Assert.Equal(original.IsVisible, cloned.IsVisible);
    }
    
    [Fact]
    public void DrawingTool_Move_ShouldUpdatePosition()
    {
        // Arrange
        var tool = new TestDrawingTool();
        var originalBounds = tool.GetBounds();
        var offset = new Vector(10, 20);
        
        // Act
        tool.Move(offset);
        var newBounds = tool.GetBounds();
        
        // Assert - The exact behavior depends on the implementation
        // This test ensures Move is callable
        Assert.NotEqual(originalBounds, newBounds);
    }
    
    // Test implementation of DrawingTool for testing purposes
    private class TestDrawingTool : DrawingTool
    {
        private Point _startPoint;
        private Point _endPoint;
        
        public override void StartDrawing(Point startPoint)
        {
            _startPoint = startPoint;
            IsDrawing = true;
            IsComplete = false;
        }
        
        public override void UpdateDrawing(Point currentPoint)
        {
            if (IsDrawing)
            {
                _endPoint = currentPoint;
            }
        }
        
        public override void CompleteDrawing(Point endPoint)
        {
            _endPoint = endPoint;
            IsDrawing = false;
            IsComplete = true;
        }
        
        public override bool HitTest(Point point, double tolerance = 5.0)
        {
            // Simple hit test - check if point is within bounds
            var bounds = GetBounds();
            return bounds.Contains(point);
        }
        
        public override Rect GetBounds()
        {
            var left = Math.Min(_startPoint.X, _endPoint.X);
            var top = Math.Min(_startPoint.Y, _endPoint.Y);
            var width = Math.Abs(_endPoint.X - _startPoint.X);
            var height = Math.Abs(_endPoint.Y - _startPoint.Y);
            
            return new Rect(left, top, width, height);
        }
        
        public override void Move(Vector offset)
        {
            _startPoint += offset;
            _endPoint += offset;
        }
        
        public override void Render(DrawingContext context, Rect viewport)
        {
            if (!IsVisible) return;
            
            // Simple line rendering for test
            var brush = Stroke.Clone();
            brush.Opacity = Opacity;
            var pen = new Pen(brush, StrokeThickness);
            context.DrawLine(pen, _startPoint, _endPoint);
        }
        
        public override DrawingTool Clone()
        {
            return new TestDrawingTool
            {
                _startPoint = _startPoint,
                _endPoint = _endPoint,
                Stroke = Stroke,
                StrokeThickness = StrokeThickness,
                Opacity = Opacity,
                IsVisible = IsVisible,
                IsDrawing = IsDrawing,
                IsComplete = IsComplete,
                IsSelected = IsSelected
            };
        }
        
        public override Point[] GetControlPoints()
        {
            return new[] { _startPoint, _endPoint };
        }
        
        public override void UpdateControlPoint(int index, Point newPosition)
        {
            switch (index)
            {
                case 0:
                    _startPoint = newPosition;
                    break;
                case 1:
                    _endPoint = newPosition;
                    break;
            }
        }
    }
}
