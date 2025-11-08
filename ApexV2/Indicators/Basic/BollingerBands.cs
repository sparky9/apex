using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Basic;

/// <summary>
/// Bollinger Bands indicator
/// </summary>
public class BollingerBands : IndicatorBase
{
    public BollingerBands(string id, IChartLogger? logger = null) 
        : base(id, "Bollinger Bands", "Price envelope indicator with upper and lower bands", IndicatorCategory.Volatility, logger)
    {
        SetParameter("period", 20);
        SetParameter("standardDeviations", 2.0);
    }
    
    public override int MinimumDataPoints => GetParameter<int>("period", 20);
    
    public override IIndicator Clone()
    {
        var clone = new BollingerBands(Id + "_clone");
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
        var stdDevMultiplier = GetParameter<double>("standardDeviations", 2.0);
        
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
        var prices = recentData.Select(d => (double)d.Close).ToArray();
        
        // Calculate middle band (SMA)
        var middleBand = IndicatorMath.SimpleMovingAverage(prices, period);
        
        // Calculate standard deviation
        var standardDeviation = IndicatorMath.StandardDeviation(prices, period);
        
        // Calculate upper and lower bands
        var upperBand = middleBand + (standardDeviation * stdDevMultiplier);
        var lowerBand = middleBand - (standardDeviation * stdDevMultiplier);
        
        var currentPrice = (double)data[^1].Close;
        
        // Calculate %B (price position within bands)
        var percentB = (currentPrice - lowerBand) / (upperBand - lowerBand);
        
        // Calculate bandwidth
        var bandwidth = (upperBand - lowerBand) / middleBand;
        
        return new IndicatorResult
        {
            Value = middleBand, // Main value is the middle band
            IsValid = !double.IsNaN(middleBand),
            Timestamp = data[^1].Timestamp,
            Metadata = new Dictionary<string, object>
            {
                ["upperBand"] = upperBand,
                ["middleBand"] = middleBand,
                ["lowerBand"] = lowerBand,
                ["standardDeviation"] = standardDeviation,
                ["percentB"] = percentB,
                ["bandwidth"] = bandwidth,
                ["period"] = period,
                ["stdDevMultiplier"] = stdDevMultiplier,
                ["squeeze"] = bandwidth < 0.1, // Bollinger Band squeeze
                ["breakoutUp"] = currentPrice > upperBand,
                ["breakoutDown"] = currentPrice < lowerBand,
                ["oversold"] = percentB < 0,
                ["overbought"] = percentB > 1
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
            if (period < 5)
            {
                errors.Add("Period should be at least 5 for meaningful Bollinger Bands");
            }
            if (period > 200)
            {
                errors.Add("Period should not exceed 200 for performance reasons");
            }
        }
        
        if (!Parameters.ContainsKey("standardDeviations"))
        {
            errors.Add("Standard deviations parameter is required");
        }
        else
        {
            var stdDev = GetParameter<double>("standardDeviations", 0);
            if (stdDev <= 0)
            {
                errors.Add("Standard deviations must be greater than 0");
            }
            if (stdDev > 5)
            {
                errors.Add("Standard deviations should not exceed 5 for practical use");
            }
        }
        
        return new IndicatorValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
