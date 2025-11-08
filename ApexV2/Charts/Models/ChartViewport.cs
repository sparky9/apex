using System;

namespace ApexV2.Charts.Models;

/// <summary>
/// Represents the visible range of the chart
/// </summary>
public class ChartViewport
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    
    /// <summary>
    /// Gets the time span of the viewport
    /// </summary>
    public TimeSpan TimeRange => EndTime - StartTime;
    
    /// <summary>
    /// Gets the price range of the viewport
    /// </summary>
    public decimal PriceRange => MaxPrice - MinPrice;
    
    /// <summary>
    /// Checks if a timestamp is within the viewport
    /// </summary>
    public bool ContainsTime(DateTime timestamp)
    {
        return timestamp >= StartTime && timestamp <= EndTime;
    }
    
    /// <summary>
    /// Checks if a price is within the viewport
    /// </summary>
    public bool ContainsPrice(decimal price)
    {
        return price >= MinPrice && price <= MaxPrice;
    }
    
    /// <summary>
    /// Creates a copy of the viewport
    /// </summary>
    public ChartViewport Clone()
    {
        return new ChartViewport
        {
            StartTime = StartTime,
            EndTime = EndTime,
            MinPrice = MinPrice,
            MaxPrice = MaxPrice
        };
    }
    
    public override string ToString()
    {
        return $"Time: {StartTime:yyyy-MM-dd HH:mm} to {EndTime:yyyy-MM-dd HH:mm}, Price: {MinPrice:F2} to {MaxPrice:F2}";
    }
}
