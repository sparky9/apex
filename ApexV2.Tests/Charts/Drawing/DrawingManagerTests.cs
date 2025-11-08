using System;
using Xunit;
using System.Windows;
using System.Linq;
using ApexV2.Charts.Drawing;

namespace ApexV2.Tests.Charts.Drawing;

public class DrawingManagerTests
{
    [Fact]
    public void DrawingManager_InitialState_ShouldBeCorrect()
    {
        // Arrange & Act
        var manager = new DrawingManager();
        
        // Assert
        Assert.Equal(DrawingMode.None, manager.CurrentMode);
        Assert.Empty(manager.Drawings);
        Assert.Null(manager.SelectedDrawing);
        Assert.False(manager.IsDrawing);
    }
    
    [Fact]
    public void DrawingManager_AddDrawing_ShouldIncreaseCount()
    {
        // Arrange
        var manager = new DrawingManager();
        var drawing = new LineTool(LineTool.LineType.Trend);
        drawing.StartDrawing(new Point(0, 0));
        drawing.CompleteDrawing(new Point(10, 10));
        
        // Act
        manager.AddDrawing(drawing);
        
        // Assert
        Assert.Single(manager.Drawings);
        Assert.Contains(drawing, manager.Drawings);
    }
    
    [Fact]
    public void DrawingManager_RemoveDrawing_ShouldDecreaseCount()
    {
        // Arrange
        var manager = new DrawingManager();
        var drawing = new LineTool(LineTool.LineType.Trend);
        manager.AddDrawing(drawing);
        
        // Act
        manager.RemoveDrawing(drawing);
        
        // Assert
        Assert.Empty(manager.Drawings);
        Assert.DoesNotContain(drawing, manager.Drawings);
    }
    
    [Fact]
    public void DrawingManager_SelectDrawing_ShouldUpdateSelection()
    {
        // Arrange
        var manager = new DrawingManager();
        var drawing = new LineTool(LineTool.LineType.Trend);
        manager.AddDrawing(drawing);
        
        // Act
        manager.SelectDrawing(drawing);
        
        // Assert
        Assert.Equal(drawing, manager.SelectedDrawing);
        Assert.True(drawing.IsSelected);
    }
    
    [Fact]
    public void DrawingManager_SelectDrawing_ShouldDeselectPrevious()
    {
        // Arrange
        var manager = new DrawingManager();
        var drawing1 = new LineTool(LineTool.LineType.Trend);
        var drawing2 = new LineTool(LineTool.LineType.Horizontal);
        manager.AddDrawing(drawing1);
        manager.AddDrawing(drawing2);
        
        // Act
        manager.SelectDrawing(drawing1);
        manager.SelectDrawing(drawing2);
        
        // Assert
        Assert.Equal(drawing2, manager.SelectedDrawing);
        Assert.False(drawing1.IsSelected);
        Assert.True(drawing2.IsSelected);
    }
    
    [Fact]
    public void DrawingManager_ClearSelection_ShouldDeselectAll()
    {
        // Arrange
        var manager = new DrawingManager();
        var drawing = new LineTool(LineTool.LineType.Trend);
        manager.AddDrawing(drawing);
        manager.SelectDrawing(drawing);
        
        // Act
        manager.ClearSelection();
        
        // Assert
        Assert.Null(manager.SelectedDrawing);
        Assert.False(drawing.IsSelected);
    }
    
    [Fact]
    public void DrawingManager_ChangeMode_ShouldCancelActiveDrawing()
    {
        // Arrange
        var manager = new DrawingManager();
        manager.CurrentMode = DrawingMode.TrendLine;
        manager.OnMouseDown(new Point(0, 0), System.Windows.Input.MouseButton.Left);
        
        // Act
        manager.CurrentMode = DrawingMode.Rectangle;
        
        // Assert
        Assert.False(manager.IsDrawing);
        Assert.Equal(DrawingMode.Rectangle, manager.CurrentMode);
    }
    
    [Fact]
    public void DrawingManager_StartDrawing_ShouldCreateActiveTool()
    {
        // Arrange
        var manager = new DrawingManager();
        manager.CurrentMode = DrawingMode.TrendLine;
        
        // Act
        manager.OnMouseDown(new Point(10, 20), System.Windows.Input.MouseButton.Left);
        
        // Assert
        Assert.True(manager.IsDrawing);
    }
    
    [Fact]
    public void DrawingManager_CompleteDrawing_ShouldAddToCollection()
    {
        // Arrange
        var manager = new DrawingManager();
        manager.CurrentMode = DrawingMode.TrendLine;
        var startPoint = new Point(10, 20);
        var endPoint = new Point(30, 40);
        
        // Act
        manager.OnMouseDown(startPoint, System.Windows.Input.MouseButton.Left);
        manager.OnMouseMove(endPoint);
        manager.OnMouseUp(endPoint, System.Windows.Input.MouseButton.Left);
        
        // Assert
        Assert.False(manager.IsDrawing);
        Assert.Single(manager.Drawings);
        Assert.True(manager.Drawings.First().IsComplete);
    }
    
    [Fact]
    public void DrawingManager_GetDrawingAtPosition_ShouldReturnCorrectDrawing()
    {
        // Arrange
        var manager = new DrawingManager();
        var drawing = new LineTool(LineTool.LineType.Trend);
        drawing.StartDrawing(new Point(0, 0));
        drawing.CompleteDrawing(new Point(100, 100));
        manager.AddDrawing(drawing);
        
        // Act
        var found = manager.GetDrawingAtPosition(new Point(50, 50));
        var notFound = manager.GetDrawingAtPosition(new Point(200, 200));
        
        // Assert
        Assert.Equal(drawing, found);
        Assert.Null(notFound);
    }
    
    [Fact]
    public void DrawingManager_RemoveSelectedDrawing_ShouldWork()
    {
        // Arrange
        var manager = new DrawingManager();
        var drawing = new LineTool(LineTool.LineType.Trend);
        manager.AddDrawing(drawing);
        manager.SelectDrawing(drawing);
        
        // Act
        manager.RemoveSelectedDrawing();
        
        // Assert
        Assert.Empty(manager.Drawings);
        Assert.Null(manager.SelectedDrawing);
    }
    
    [Fact]
    public void DrawingManager_ClearAllDrawings_ShouldRemoveEverything()
    {
        // Arrange
        var manager = new DrawingManager();
        var drawing1 = new LineTool(LineTool.LineType.Trend);
        var drawing2 = new RectangleTool(RectangleTool.RectangleType.Standard);
        manager.AddDrawing(drawing1);
        manager.AddDrawing(drawing2);
        manager.SelectDrawing(drawing1);
        
        // Act
        manager.ClearAllDrawings();
        
        // Assert
        Assert.Empty(manager.Drawings);
        Assert.Null(manager.SelectedDrawing);
    }
}
