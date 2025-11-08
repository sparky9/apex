using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Tests.Indicators.Engine;

/// <summary>
/// Shared test indicator for service tests
/// </summary>
public class ServiceTestIndicator : IndicatorBase
{
    public ServiceTestIndicator(string id, IChartLogger? logger = null) 
        : base(id, "Service Test Indicator", "Test indicator for service testing", IndicatorCategory.Custom, logger)
    {
        SetParameter("period", 10);
    }
    
    public override int MinimumDataPoints => GetParameter<int>("period", 10);
    
    public override IIndicator Clone()
    {
        var clone = new ServiceTestIndicator(Id + "_clone");
        clone.Parameters.Clear();
        foreach (var param in Parameters)
        {
            clone.Parameters[param.Key] = param.Value;
        }
        return clone;
    }
    
    protected override IndicatorResult CalculateInternal(CandlestickData[] data)
    {
        if (data.Length < MinimumDataPoints)
        {
            return new IndicatorResult 
            { 
                IsValid = false, 
                Timestamp = DateTime.UtcNow,
                ErrorMessage = "Insufficient data"
            };
        }

        var period = GetParameter<int>("period", 10);
        var recentData = data.TakeLast(period).ToArray();
        var avgClose = recentData.Average(d => (double)d.Close);
        
        return new IndicatorResult
        {
            Value = avgClose,
            IsValid = true,
            Timestamp = data[^1].Timestamp
        };
    }
    
    protected override IndicatorValidationResult ValidateParametersInternal()
    {
        var errors = new List<string>();
        
        if (!Parameters.ContainsKey("period"))
        {
            errors.Add("Period parameter is required");
        }
        else if (GetParameter<int>("period", 0) <= 0)
        {
            errors.Add("Period must be greater than 0");
        }
        
        return new IndicatorValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
