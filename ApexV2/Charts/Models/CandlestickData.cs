using System;

namespace ApexV2.Charts.Models;

/// <summary>
/// Represents a single candlestick/OHLC data point
/// </summary>
public class CandlestickData
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }

    /// <summary>
    /// Gets whether this is a bullish (green) candle
    /// </summary>
    public bool IsBullish => Close >= Open;

    /// <summary>
    /// Gets the body size of the candle
    /// </summary>
    public decimal BodySize => Math.Abs(Close - Open);

    /// <summary>
    /// Gets the upper shadow size
    /// </summary>
    public decimal UpperShadow => High - Math.Max(Open, Close);

    /// <summary>
    /// Gets the lower shadow size
    /// </summary>
    public decimal LowerShadow => Math.Min(Open, Close) - Low;

    /// <summary>
    /// Gets the total range of the candle
    /// </summary>
    public decimal Range => High - Low;

    public override string ToString()
    {
        return $"{Timestamp:yyyy-MM-dd HH:mm} O:{Open:F2} H:{High:F2} L:{Low:F2} C:{Close:F2} V:{Volume}";
    }
}
