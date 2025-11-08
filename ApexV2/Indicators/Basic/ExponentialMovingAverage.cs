using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Basic;

/// <summary>
/// Exponential Moving Average (EMA) indicator
/// </summary>
public class ExponentialMovingAverage : IndicatorBase
{
    private double? _previousEma;
    
    public ExponentialMovingAverage(string id, IChartLogger? logger = null) 
        : base(id, "Exponential Moving Average", "Gives more weight to recent prices", IndicatorCategory.Trend, logger)
    {
        SetParameter("period", 20);
    }
    
    public override int MinimumDataPoints => GetParameter<int>("period", 20);
    
    public override IIndicator Clone()
    {
        var clone = new ExponentialMovingAverage(Id + "_clone");
        clone.Parameters.Clear();
        foreach (var param in Parameters)
        {
            clone.Parameters[param.Key] = param.Value;
        }
        return clone;
    }
    
    public override void Reset()
    {
        base.Reset();
        _previousEma = null;
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

        double emaValue;
        
        if (_previousEma == null)
        {
            // First calculation - use SMA as seed
            var initialData = data.TakeLast(period).ToArray();
            emaValue = IndicatorMath.SimpleMovingAverage(initialData.Select(d => (double)d.Close), period);
        }
        else
        {
            // Use previous EMA for calculation
            var currentPrice = (double)data[^1].Close;
            var multiplier = 2.0 / (period + 1);
            emaValue = (currentPrice * multiplier) + (_previousEma.Value * (1 - multiplier));
        }
        
        _previousEma = emaValue;
        
        return new IndicatorResult
        {
            Value = emaValue,
            IsValid = !double.IsNaN(emaValue),
            Timestamp = data[^1].Timestamp,
            Metadata = new Dictionary<string, object>
            {
                ["period"] = period,
                ["multiplier"] = 2.0 / (period + 1)
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
