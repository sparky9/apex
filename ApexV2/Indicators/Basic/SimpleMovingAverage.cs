using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Basic;

/// <summary>
/// Simple Moving Average (SMA) indicator
/// </summary>
public class SimpleMovingAverage : IndicatorBase
{
    public SimpleMovingAverage(string id, IChartLogger? logger = null) 
        : base(id, "Simple Moving Average", "Calculates the average price over a specified period", IndicatorCategory.Trend, logger)
    {
        SetParameter("period", 20);
    }
    
    public override int MinimumDataPoints => GetParameter<int>("period", 20);
    
    public override IIndicator Clone()
    {
        var clone = new SimpleMovingAverage(Id + "_clone");
        clone.Parameters.Clear();
        foreach (var param in Parameters)
        {
            clone.Parameters[param.Key] = param.Value;
        }
        return clone;
    }
    
    protected override IndicatorResult CalculateInternal(CandlestickData[] data)
    {
        var period = GetParameter<int>("period", 20);
        
        if (data.Length < period)
        {
            return new IndicatorResult 
            { 
                IsValid = false, 
                Timestamp = data[^1].Timestamp,
                ErrorMessage = $"Insufficient data: need {period} points, have {data.Length}"
            };
        }

        var recentData = data.TakeLast(period).ToArray();
        var smaValue = IndicatorMath.SimpleMovingAverage(recentData.Select(d => (double)d.Close), period);
        
        return new IndicatorResult
        {
            Value = smaValue,
            IsValid = !double.IsNaN(smaValue),
            Timestamp = data[^1].Timestamp,
            Metadata = new Dictionary<string, object>
            {
                ["period"] = period,
                ["dataPoints"] = recentData.Length
            }
        };
    }
    
    protected override IndicatorValidationResult ValidateParametersInternal()
    {
        var errors = new List<string>();
        
        if (!Parameters.ContainsKey("period"))
        {
            errors.Add("Period parameter is required");
        }
        else
        {
            var period = GetParameter<int>("period", 0);
            if (period <= 0)
            {
                errors.Add("Period must be greater than 0");
            }
            if (period > 200)
            {
                errors.Add("Period should not exceed 200 for performance reasons");
            }
        }
        
        return new IndicatorValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
