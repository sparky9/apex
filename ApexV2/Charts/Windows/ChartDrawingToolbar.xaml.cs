using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ApexV2.Charts.Controls;
using ApexV2.Charts.Drawing;

namespace ApexV2.Charts.Windows;

/// <summary>
/// Toolbar for chart drawing tools
/// </summary>
public partial class ChartDrawingToolbar : UserControl
{
    private ChartControl? _chartControl;
    
    public event EventHandler<DrawingMode>? DrawingModeChanged;
    
    public ChartDrawingToolbar()
    {
        InitializeComponent();
        UpdateButtonStates(DrawingMode.None);
    }
    
    /// <summary>
    /// Sets the chart control this toolbar controls
    /// </summary>
    public void SetChartControl(ChartControl chartControl)
    {
        _chartControl = chartControl;
    }
    
    /// <summary>
    /// Gets the current drawing mode
    /// </summary>
    public DrawingMode CurrentMode { get; private set; } = DrawingMode.None;
    
    private void SelectButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(DrawingMode.Select);
    }
    
    private void TrendLineButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(DrawingMode.TrendLine);
    }
    
    private void HorizontalLineButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(DrawingMode.HorizontalLine);
    }
    
    private void VerticalLineButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(DrawingMode.VerticalLine);
    }
    
    private void RectangleButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(DrawingMode.Rectangle);
    }
    
    private void PriceChannelButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(DrawingMode.PriceChannel);
    }
    
    private void TimeRangeButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(DrawingMode.TimeRange);
    }
    
    private void TextAnnotationButton_Click(object sender, RoutedEventArgs e)
    {
        SetDrawingMode(DrawingMode.TextAnnotation);
    }
    
    private void ClearDrawingsButton_Click(object sender, RoutedEventArgs e)
    {
        _chartControl?.ClearDrawings();
    }
    
    private void SetDrawingMode(DrawingMode mode)
    {
        CurrentMode = mode;
        _chartControl?.SetDrawingMode(mode);
        UpdateButtonStates(mode);
        DrawingModeChanged?.Invoke(this, mode);
    }
    
    private void UpdateButtonStates(DrawingMode activeMode)
    {
        // Reset all button styles
        SelectButton.Style = (Style)FindResource("ToolbarButton");
        TrendLineButton.Style = (Style)FindResource("ToolbarButton");
        HorizontalLineButton.Style = (Style)FindResource("ToolbarButton");
        VerticalLineButton.Style = (Style)FindResource("ToolbarButton");
        RectangleButton.Style = (Style)FindResource("ToolbarButton");
        PriceChannelButton.Style = (Style)FindResource("ToolbarButton");
        TimeRangeButton.Style = (Style)FindResource("ToolbarButton");
        TextAnnotationButton.Style = (Style)FindResource("ToolbarButton");
        
        // Set active button style
        Button? activeButton = activeMode switch
        {
            DrawingMode.Select => SelectButton,
            DrawingMode.TrendLine => TrendLineButton,
            DrawingMode.HorizontalLine => HorizontalLineButton,
            DrawingMode.VerticalLine => VerticalLineButton,
            DrawingMode.Rectangle => RectangleButton,
            DrawingMode.PriceChannel => PriceChannelButton,
            DrawingMode.TimeRange => TimeRangeButton,
            DrawingMode.TextAnnotation => TextAnnotationButton,
            _ => null
        };
        
        if (activeButton != null)
        {
            activeButton.Style = (Style)FindResource("ActiveToolbarButton");
        }
    }
    
    /// <summary>
    /// Handle keyboard shortcuts
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                SetDrawingMode(DrawingMode.None);
                e.Handled = true;
                break;
                
            case Key.S:
                SetDrawingMode(DrawingMode.Select);
                e.Handled = true;
                break;
                
            case Key.L:
                SetDrawingMode(DrawingMode.TrendLine);
                e.Handled = true;
                break;
                
            case Key.H:
                SetDrawingMode(DrawingMode.HorizontalLine);
                e.Handled = true;
                break;
                
            case Key.V:
                SetDrawingMode(DrawingMode.VerticalLine);
                e.Handled = true;
                break;
                
            case Key.R:
                SetDrawingMode(DrawingMode.Rectangle);
                e.Handled = true;
                break;
                
            case Key.T:
                SetDrawingMode(DrawingMode.TextAnnotation);
                e.Handled = true;
                break;
        }
        
        base.OnKeyDown(e);
    }
}
