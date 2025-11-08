using System;
using ApexV2.Indicators.Engine;
using ApexV2.Indicators.Basic;
using ApexV2.Charts.Export;
using IndicatorCategoryEnum = ApexV2.Indicators.Engine.IndicatorCategory;

namespace ApexV2.Indicators.Library;

/// <summary>
/// Service for registering and creating built-in indicators
/// </summary>
public static class BuiltInIndicators
{
    /// <summary>
    /// Register all built-in indicators with the service
    /// </summary>
    /// <param name="indicatorService">The indicator service to register with</param>
    public static void RegisterAll(IndicatorService indicatorService)
    {
        // Register SMA
        indicatorService.RegisterIndicatorType("SMA", 
            new IndicatorFactory((id, parameters, logger) => 
            {
                var sma = new SimpleMovingAverage(id, logger);
                ApplyParameters(sma, parameters);
                return sma;
            }));

        // Register EMA
        indicatorService.RegisterIndicatorType("EMA", 
            new IndicatorFactory((id, parameters, logger) => 
            {
                var ema = new ExponentialMovingAverage(id, logger);
                ApplyParameters(ema, parameters);
                return ema;
            }));

        // Register RSI
        indicatorService.RegisterIndicatorType("RSI", 
            new IndicatorFactory((id, parameters, logger) => 
            {
                var rsi = new RelativeStrengthIndex(id, logger);
                ApplyParameters(rsi, parameters);
                return rsi;
            }));

        // Register MACD
        indicatorService.RegisterIndicatorType("MACD", 
            new IndicatorFactory((id, parameters, logger) => 
            {
                var macd = new MovingAverageConvergenceDivergence(id, logger);
                ApplyParameters(macd, parameters);
                return macd;
            }));

        // Register Bollinger Bands
        indicatorService.RegisterIndicatorType("BB", 
            new IndicatorFactory((id, parameters, logger) => 
            {
                var bb = new BollingerBands(id, logger);
                ApplyParameters(bb, parameters);
                return bb;
            }));
    }

    /// <summary>
    /// Register all built-in indicators with the registry
    /// </summary>
    /// <param name="registry">The indicator registry to register with</param>
    public static void RegisterMetadata(IndicatorRegistry registry)
    {
        // SMA Metadata
        registry.RegisterIndicator(new IndicatorMetadata
        {
            Type = "SMA",
            Name = "Simple Moving Average",
            Description = "Calculates the average price over a specified period",
            Category = IndicatorCategoryEnum.Trend,
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "period",
                    Type = typeof(int),
                    DefaultValue = 20,
                    Description = "Number of periods for the moving average",
                    MinValue = 1,
                    MaxValue = 200
                }
            }
        });

        // EMA Metadata
        registry.RegisterIndicator(new IndicatorMetadata
        {
            Type = "EMA",
            Name = "Exponential Moving Average",
            Description = "Gives more weight to recent prices",
            Category = IndicatorCategoryEnum.Trend,
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "period",
                    Type = typeof(int),
                    DefaultValue = 20,
                    Description = "Number of periods for the moving average",
                    MinValue = 1,
                    MaxValue = 200
                }
            }
        });

        // RSI Metadata
        registry.RegisterIndicator(new IndicatorMetadata
        {
            Type = "RSI",
            Name = "Relative Strength Index",
            Description = "Momentum oscillator that measures speed and magnitude of price changes",
            Category = IndicatorCategoryEnum.Momentum,
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "period",
                    Type = typeof(int),
                    DefaultValue = 14,
                    Description = "Number of periods for RSI calculation",
                    MinValue = 2,
                    MaxValue = 50
                }
            }
        });

        // MACD Metadata
        registry.RegisterIndicator(new IndicatorMetadata
        {
            Type = "MACD",
            Name = "Moving Average Convergence Divergence",
            Description = "Trend and momentum indicator",
            Category = IndicatorCategoryEnum.Momentum,
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "fastPeriod",
                    Type = typeof(int),
                    DefaultValue = 12,
                    Description = "Fast EMA period",
                    MinValue = 1,
                    MaxValue = 100
                },
                new ParameterMetadata
                {
                    Name = "slowPeriod",
                    Type = typeof(int),
                    DefaultValue = 26,
                    Description = "Slow EMA period",
                    MinValue = 1,
                    MaxValue = 200
                },
                new ParameterMetadata
                {
                    Name = "signalPeriod",
                    Type = typeof(int),
                    DefaultValue = 9,
                    Description = "Signal line EMA period",
                    MinValue = 1,
                    MaxValue = 50
                }
            }
        });

        // Bollinger Bands Metadata
        registry.RegisterIndicator(new IndicatorMetadata
        {
            Type = "BB",
            Name = "Bollinger Bands",
            Description = "Price envelope indicator with upper and lower bands",
            Category = IndicatorCategoryEnum.Volatility,
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "period",
                    Type = typeof(int),
                    DefaultValue = 20,
                    Description = "Number of periods for the moving average",
                    MinValue = 5,
                    MaxValue = 200
                },
                new ParameterMetadata
                {
                    Name = "standardDeviations",
                    Type = typeof(double),
                    DefaultValue = 2.0,
                    Description = "Number of standard deviations for the bands",
                    MinValue = 0.1,
                    MaxValue = 5.0
                }
            }
        });
    }

    private static void ApplyParameters(IIndicator indicator, System.Collections.Generic.Dictionary<string, object> parameters)
    {
        if (parameters == null) return;

        var baseIndicator = indicator as IndicatorBase;
        if (baseIndicator == null) return;

        foreach (var param in parameters)
        {
            baseIndicator.SetParameter(param.Key, param.Value);
        }
    }
}
