using System;
using System.Collections.Generic;

namespace ApexV2.Charts.Models;

/// <summary>
/// Chart configuration and display settings
/// </summary>
public class ChartSettings
{
    public string Symbol { get; set; } = string.Empty;
    public TimeFrame TimeFrame { get; set; } = TimeFrame.Daily;
    public ChartType ChartType { get; set; } = ChartType.Candlestick;
    public bool ShowVolume { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public bool ShowCrosshair { get; set; } = true;
    
    // Color settings
    public string BullishColor { get; set; } = "#26a69a"; // Green
    public string BearishColor { get; set; } = "#ef5350"; // Red
    public string BackgroundColor { get; set; } = "#1e1e1e"; // Dark
    public string GridColor { get; set; } = "#333333";
    public string TextColor { get; set; } = "#ffffff";
    public string CrosshairColor { get; set; } = "#888888";
    
    // Display settings
    public int CandleWidth { get; set; } = 8;
    public int CandleSpacing { get; set; } = 2;
    public bool AutoScale { get; set; } = true;
    public decimal? FixedMinPrice { get; set; }
    public decimal? FixedMaxPrice { get; set; }
    
    // Performance settings
    public int MaxVisibleCandles { get; set; } = 500;
    public bool EnableAnimations { get; set; } = true;
}

/// <summary>
/// Chart time frame options
/// </summary>
public enum TimeFrame
{
    Tick,
    OneMinute,
    FiveMinutes,
    FifteenMinutes,
    ThirtyMinutes,
    Hourly,
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// Chart display types
/// </summary>
public enum ChartType
{
    Candlestick,
    OHLC,
    Line,
    Area,
    Volume
}
