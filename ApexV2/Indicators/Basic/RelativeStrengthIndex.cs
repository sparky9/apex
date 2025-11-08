using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Indicators.Basic;

/// <summary>
/// Relative Strength Index (RSI) indicator
/// </summary>
public class RelativeStrengthIndex : IndicatorBase
{
    private readonly List<double> _gainHistory = new();
    private readonly List<double> _lossHistory = new();
    private double? _previousClose;
    
    public RelativeStrengthIndex(string id, IChartLogger? logger = null) 
        : base(id, "Relative Strength Index", "Momentum oscillator that measures speed and magnitude of price changes", IndicatorCategory.Momentum, logger)
    {
        SetParameter("period", 14);
    }
    
    public override int MinimumDataPoints => GetParameter<int>("period", 14) + 1; // Need one extra for price change calculation
    
    public override IIndicator Clone()
    {
        var clone = new RelativeStrengthIndex(Id + "_clone");
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
        _gainHistory.Clear();
        _lossHistory.Clear();
        _previousClose = null;
    }
    
    protected override IndicatorResult CalculateInternal(CandlestickData[] data)
    {
        var period = GetParameter<int>("period", 14);
        
        if (data.Length < MinimumDataPoints)
        {
            return new IndicatorResult 
            { 
                IsValid = false, 
                Timestamp = data[^1].Timestamp,
                ErrorMessage = $"Insufficient data: need {MinimumDataPoints} points, have {data.Length}"
            };
        }

        // Build gain/loss history if not already done
        if (_gainHistory.Count == 0)
        {
            var prices = data.Select(d => (double)d.Close).ToArray();
            var (gains, losses) = IndicatorMath.CalculateGainsAndLosses(prices, period);
            _gainHistory.AddRange(gains);
            _lossHistory.AddRange(losses);
        }
        else if (_previousClose.HasValue)
        {
            // Add latest gain/loss
            var currentClose = (double)data[^1].Close;
            var change = currentClose - _previousClose.Value;
            
            _gainHistory.Add(change > 0 ? change : 0);
            _lossHistory.Add(change < 0 ? Math.Abs(change) : 0);
            
            // Keep only the required period
            if (_gainHistory.Count > period)
            {
                _gainHistory.RemoveAt(0);
                _lossHistory.RemoveAt(0);
            }
        }
        
        _previousClose = (double)data[^1].Close;
        
        if (_gainHistory.Count < period)
        {
            return new IndicatorResult 
            { 
                IsValid = false, 
                Timestamp = data[^1].Timestamp,
                ErrorMessage = "Building RSI history"
            };
        }
        
        var avgGain = _gainHistory.Average();
        var avgLoss = _lossHistory.Average();
        
        double rsiValue;
        if (Math.Abs(avgLoss) < double.Epsilon)
        {
            rsiValue = 100.0; // No losses, RSI = 100
        }
        else
        {
            var rs = avgGain / avgLoss;
            rsiValue = 100.0 - (100.0 / (1.0 + rs));
        }
        
        return new IndicatorResult
        {
            Value = rsiValue,
            IsValid = !double.IsNaN(rsiValue),
            Timestamp = data[^1].Timestamp,
            Metadata = new Dictionary<string, object>
            {
                ["period"] = period,
                ["avgGain"] = avgGain,
                ["avgLoss"] = avgLoss,
                ["overbought"] = rsiValue > 70,
                ["oversold"] = rsiValue < 30
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
            if (period < 2)
            {
                errors.Add("Period should be at least 2 for meaningful RSI calculation");
            }
            if (period > 50)
            {
                errors.Add("Period should not exceed 50 for performance reasons");
            }
        }
        
        return new IndicatorValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
