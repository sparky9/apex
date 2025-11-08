using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Basic;

/// <summary>
/// MACD (Moving Average Convergence Divergence) indicator
/// </summary>
public class MovingAverageConvergenceDivergence : IndicatorBase
{
    private double? _previousFastEma;
    private double? _previousSlowEma;
    private double? _previousSignalEma;
    
    public MovingAverageConvergenceDivergence(string id, IChartLogger? logger = null) 
        : base(id, "MACD", "Moving Average Convergence Divergence - trend and momentum indicator", IndicatorCategory.Momentum, logger)
    {
        SetParameter("fastPeriod", 12);
        SetParameter("slowPeriod", 26);
        SetParameter("signalPeriod", 9);
    }
    
    public override int MinimumDataPoints => GetParameter<int>("slowPeriod", 26);
    
    public override IIndicator Clone()
    {
        var clone = new MovingAverageConvergenceDivergence(Id + "_clone");
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
        _previousFastEma = null;
        _previousSlowEma = null;
        _previousSignalEma = null;
    }
    
    protected override IndicatorResult CalculateInternal(CandlestickData[] data)
    {
        var fastPeriod = GetParameter<int>("fastPeriod", 12);
        var slowPeriod = GetParameter<int>("slowPeriod", 26);
        var signalPeriod = GetParameter<int>("signalPeriod", 9);
        
        if (data.Length < slowPeriod)
        {
            return new IndicatorResult 
            { 
                IsValid = false, 
                Timestamp = data[^1].Timestamp,
                ErrorMessage = $"Insufficient data: need {slowPeriod} points, have {data.Length}"
            };
        }

        var currentPrice = (double)data[^1].Close;
        
        // Calculate Fast EMA
        double fastEma;
        if (_previousFastEma == null)
        {
            var fastData = data.TakeLast(fastPeriod).ToArray();
            fastEma = IndicatorMath.SimpleMovingAverage(fastData.Select(d => (double)d.Close), fastPeriod);
        }
        else
        {
            var fastMultiplier = 2.0 / (fastPeriod + 1);
            fastEma = (currentPrice * fastMultiplier) + (_previousFastEma.Value * (1 - fastMultiplier));
        }
        _previousFastEma = fastEma;
        
        // Calculate Slow EMA
        double slowEma;
        if (_previousSlowEma == null)
        {
            var slowData = data.TakeLast(slowPeriod).ToArray();
            slowEma = IndicatorMath.SimpleMovingAverage(slowData.Select(d => (double)d.Close), slowPeriod);
        }
        else
        {
            var slowMultiplier = 2.0 / (slowPeriod + 1);
            slowEma = (currentPrice * slowMultiplier) + (_previousSlowEma.Value * (1 - slowMultiplier));
        }
        _previousSlowEma = slowEma;
        
        // Calculate MACD Line
        var macdLine = fastEma - slowEma;
        
        // Calculate Signal Line (EMA of MACD)
        double signalLine;
        if (_previousSignalEma == null)
        {
            signalLine = macdLine; // First signal line equals MACD line
        }
        else
        {
            var signalMultiplier = 2.0 / (signalPeriod + 1);
            signalLine = (macdLine * signalMultiplier) + (_previousSignalEma.Value * (1 - signalMultiplier));
        }
        _previousSignalEma = signalLine;
        
        // Calculate Histogram
        var histogram = macdLine - signalLine;
        
        return new IndicatorResult
        {
            Value = macdLine,
            IsValid = !double.IsNaN(macdLine),
            Timestamp = data[^1].Timestamp,
            Metadata = new Dictionary<string, object>
            {
                ["macdLine"] = macdLine,
                ["signalLine"] = signalLine,
                ["histogram"] = histogram,
                ["fastEma"] = fastEma,
                ["slowEma"] = slowEma,
                ["fastPeriod"] = fastPeriod,
                ["slowPeriod"] = slowPeriod,
                ["signalPeriod"] = signalPeriod,
                ["bullishCrossover"] = histogram > 0 && _previousSignalEma.HasValue,
                ["bearishCrossover"] = histogram < 0 && _previousSignalEma.HasValue
            }
        };
    }
    
    protected override IndicatorValidationResult ValidateParametersInternal()
    {
        var errors = new List<string>();
        
        var fastPeriod = GetParameter<int>("fastPeriod", 0);
        var slowPeriod = GetParameter<int>("slowPeriod", 0);
        var signalPeriod = GetParameter<int>("signalPeriod", 0);
        
        if (!Parameters.ContainsKey("fastPeriod") || fastPeriod <= 0)
        {
            errors.Add("Fast period must be greater than 0");
        }
        
        if (!Parameters.ContainsKey("slowPeriod") || slowPeriod <= 0)
        {
            errors.Add("Slow period must be greater than 0");
        }
        
        if (!Parameters.ContainsKey("signalPeriod") || signalPeriod <= 0)
        {
            errors.Add("Signal period must be greater than 0");
        }
        
        if (fastPeriod >= slowPeriod)
        {
            errors.Add("Fast period must be less than slow period");
        }
        
        if (slowPeriod > 200)
        {
            errors.Add("Slow period should not exceed 200 for performance reasons");
        }
        
        return new IndicatorValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
