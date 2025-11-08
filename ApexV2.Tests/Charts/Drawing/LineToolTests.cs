using System;
using Xunit;
using System.Windows;
using System.Windows.Media;
using ApexV2.Charts.Drawing;

namespace ApexV2.Tests.Charts.Drawing;

public class LineToolTests
{
    [Fact]
    public void LineTool_TrendLine_ShouldDrawCorrectly()
    {
        // Arrange
        var tool = new LineTool(LineTool.LineType.Trend);
        var start = new Point(10, 20);
        var end = new Point(50, 80);
        
        // Act
        tool.StartDrawing(start);
        tool.UpdateDrawing(end);
        tool.CompleteDrawing(end);
        
        // Assert
        Assert.True(tool.IsComplete);
        Assert.False(tool.IsDrawing);
        
        var bounds = tool.GetBounds();
        Assert.Equal(10, bounds.Left);
        Assert.Equal(20, bounds.Top);
        Assert.Equal(40, bounds.Width);
        Assert.Equal(60, bounds.Height);
    }
    
    [Fact]
    public void LineTool_HorizontalLine_ShouldMaintainYPosition()
    {
        // Arrange
        var tool = new LineTool(LineTool.LineType.Horizontal);
        var start = new Point(10, 20);
        var end = new Point(50, 80);
        
        // Act
        tool.StartDrawing(start);
        tool.UpdateDrawing(end);
        tool.CompleteDrawing(end);
        
        // Assert
        Assert.True(tool.IsComplete);
        var bounds = tool.GetBounds();
        Assert.Equal(20, bounds.Top); // Y should remain at start Y
        Assert.Equal(0, bounds.Height); // Height should be 0 for horizontal line
    }
    
    [Fact]
    public void LineTool_VerticalLine_ShouldMaintainXPosition()
    {
        // Arrange
        var tool = new LineTool(LineTool.LineType.Vertical);
        var start = new Point(10, 20);
        var end = new Point(50, 80);
        
        // Act
        tool.StartDrawing(start);
        tool.UpdateDrawing(end);
        tool.CompleteDrawing(end);
        
        // Assert
        Assert.True(tool.IsComplete);
        var bounds = tool.GetBounds();
        Assert.Equal(10, bounds.Left); // X should remain at start X
        Assert.Equal(0, bounds.Width); // Width should be 0 for vertical line
    }
    
    [Fact]
    public void LineTool_HitTest_ShouldDetectNearbyPoints()
    {
        // Arrange
        var tool = new LineTool(LineTool.LineType.Trend);
        tool.StartDrawing(new Point(0, 0));
        tool.CompleteDrawing(new Point(100, 100));
        
        // Act & Assert
        Assert.True(tool.HitTest(new Point(50, 50), 5.0)); // On the line
        Assert.True(tool.HitTest(new Point(50, 53), 5.0)); // Near the line
        Assert.False(tool.HitTest(new Point(50, 60), 5.0)); // Too far from line
    }
    
    [Fact]
    public void LineTool_Move_ShouldTranslateBothPoints()
    {
        // Arrange
        var tool = new LineTool(LineTool.LineType.Trend);
        tool.StartDrawing(new Point(10, 20));
        tool.CompleteDrawing(new Point(30, 40));
        var originalBounds = tool.GetBounds();
        
        // Act
        tool.Move(new Vector(5, 10));
        var newBounds = tool.GetBounds();
        
        // Assert
        Assert.Equal(originalBounds.Left + 5, newBounds.Left);
        Assert.Equal(originalBounds.Top + 10, newBounds.Top);
        Assert.Equal(originalBounds.Width, newBounds.Width);
        Assert.Equal(originalBounds.Height, newBounds.Height);
    }
    
    [Fact]
    public void LineTool_GetControlPoints_ShouldReturnStartAndEnd()
    {
        // Arrange
        var tool = new LineTool(LineTool.LineType.Trend);
        var start = new Point(10, 20);
        var end = new Point(30, 40);
        
        // Act
        tool.StartDrawing(start);
        tool.CompleteDrawing(end);
        var controlPoints = tool.GetControlPoints();
        
        // Assert
        Assert.Equal(2, controlPoints.Length);
        Assert.Equal(start, controlPoints[0]);
        Assert.Equal(end, controlPoints[1]);
    }
    
    [Fact]
    public void LineTool_UpdateControlPoint_ShouldModifyCorrectPoint()
    {
        // Arrange
        var tool = new LineTool(LineTool.LineType.Trend);
        tool.StartDrawing(new Point(10, 20));
        tool.CompleteDrawing(new Point(30, 40));
        var newPosition = new Point(50, 60);
        
        // Act
        tool.UpdateControlPoint(1, newPosition); // Update end point
        var controlPoints = tool.GetControlPoints();
        
        // Assert
        Assert.Equal(new Point(10, 20), controlPoints[0]); // Start unchanged
        Assert.Equal(newPosition, controlPoints[1]); // End updated
    }
    
    [Fact]
    public void LineTool_Clone_ShouldPreserveLineType()
    {
        // Arrange
        var original = new LineTool(LineTool.LineType.Horizontal);
        original.StartDrawing(new Point(10, 20));
        original.CompleteDrawing(new Point(30, 40));
        
        // Act
        var cloned = (LineTool)original.Clone();
        
        // Assert
        Assert.NotSame(original, cloned);
        Assert.Equal(original.Type, cloned.Type);
        Assert.Equal(original.GetBounds(), cloned.GetBounds());
    }
}
