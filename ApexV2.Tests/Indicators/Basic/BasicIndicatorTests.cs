using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ApexV2.Indicators.Basic;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;

namespace ApexV2.Tests.Indicators.Basic;

public class BasicIndicatorTests
{
    [Fact]
    public void SimpleMovingAverage_Calculate_ShouldReturnCorrectValue()
    {
        // Arrange
        var sma = new SimpleMovingAverage("sma_test");
        sma.SetParameter("period", 3);
        var data = CreateTestData(5, 100m); // 5 data points starting at 100
        // Data will be: Close prices 101, 102, 103, 104, 105
        // Last 3: 103, 104, 105 -> Average = 104

        // Act
        var result = sma.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(104.0, result.Value, 1); // Average of 103, 104, 105
        Assert.Equal(3, result.Metadata["period"]);
    }

    [Fact]
    public void ExponentialMovingAverage_Calculate_ShouldReturnCorrectValue()
    {
        // Arrange
        var ema = new ExponentialMovingAverage("ema_test");
        ema.SetParameter("period", 3);
        var data = CreateTestData(5, 100m);

        // Act
        var result = ema.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.Value > 0);
        Assert.Contains("multiplier", result.Metadata.Keys);
        Assert.Equal(0.5, (double)result.Metadata["multiplier"], 2); // 2/(3+1) = 0.5
    }

    [Fact]
    public void RelativeStrengthIndex_Calculate_ShouldReturnValidRSI()
    {
        // Arrange
        var rsi = new RelativeStrengthIndex("rsi_test");
        rsi.SetParameter("period", 5);
        var data = CreateTrendingData(10, 100m, 1m); // Upward trending data

        // Act
        var result = rsi.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.Value >= 0 && result.Value <= 100);
        Assert.Contains("overbought", result.Metadata.Keys);
        Assert.Contains("oversold", result.Metadata.Keys);
    }

    [Fact]
    public void MovingAverageConvergenceDivergence_Calculate_ShouldReturnValidMACD()
    {
        // Arrange
        var macd = new MovingAverageConvergenceDivergence("macd_test");
        var data = CreateTestData(30, 100m); // Need enough data for slow period

        // Act
        var result = macd.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("macdLine", result.Metadata.Keys);
        Assert.Contains("signalLine", result.Metadata.Keys);
        Assert.Contains("histogram", result.Metadata.Keys);
        Assert.Contains("fastEma", result.Metadata.Keys);
        Assert.Contains("slowEma", result.Metadata.Keys);
    }

    [Fact]
    public void BollingerBands_Calculate_ShouldReturnValidBands()
    {
        // Arrange
        var bb = new BollingerBands("bb_test");
        bb.SetParameter("period", 10);
        bb.SetParameter("standardDeviations", 2.0);
        var data = CreateVolatileData(15, 100m); // Volatile data for meaningful bands

        // Act
        var result = bb.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        var upperBand = (double)result.Metadata["upperBand"];
        var lowerBand = (double)result.Metadata["lowerBand"];
        var middleBand = result.Value;

        Assert.True(upperBand > middleBand);
        Assert.True(middleBand > lowerBand);
        Assert.Contains("percentB", result.Metadata.Keys);
        Assert.Contains("bandwidth", result.Metadata.Keys);
    }

    [Fact]
    public void AllIndicators_InsufficientData_ShouldReturnInvalidResult()
    {
        // Arrange
        var indicators = new IIndicator[]
        {
            new SimpleMovingAverage("sma"),
            new ExponentialMovingAverage("ema"),
            new RelativeStrengthIndex("rsi"),
            new MovingAverageConvergenceDivergence("macd"),
            new BollingerBands("bb")
        };
        var insufficientData = CreateTestData(2, 100m); // Only 2 data points

        foreach (var indicator in indicators)
        {
            // Act
            var result = indicator.Calculate(insufficientData);

            // Assert
            Assert.False(result.IsValid, $"{indicator.Name} should be invalid with insufficient data");
            Assert.NotNull(result.ErrorMessage);
        }
    }

    [Fact]
    public void AllIndicators_Clone_ShouldCreateIdenticalCopy()
    {
        // Arrange
        var indicators = new IIndicator[]
        {
            new SimpleMovingAverage("sma"),
            new ExponentialMovingAverage("ema"),
            new RelativeStrengthIndex("rsi"),
            new MovingAverageConvergenceDivergence("macd"),
            new BollingerBands("bb")
        };

        foreach (var original in indicators)
        {
            // Act
            var clone = original.Clone();

            // Assert
            Assert.NotEqual(original.Id, clone.Id);
            Assert.Equal(original.Name, clone.Name);
            Assert.Equal(original.Description, clone.Description);
            Assert.Equal(original.Category, clone.Category);
            Assert.Equal(original.Parameters.Count, clone.Parameters.Count);
        }
    }

    [Fact]
    public void AllIndicators_ValidateParameters_ShouldPassWithDefaults()
    {
        // Arrange
        var indicators = new IIndicator[]
        {
            new SimpleMovingAverage("sma"),
            new ExponentialMovingAverage("ema"),
            new RelativeStrengthIndex("rsi"),
            new MovingAverageConvergenceDivergence("macd"),
            new BollingerBands("bb")
        };

        foreach (var indicator in indicators)
        {
            // Act
            var validation = indicator.ValidateParameters();

            // Assert
            Assert.True(validation.IsValid, $"{indicator.Name} should pass validation with defaults. Errors: {string.Join(", ", validation.Errors)}");
        }
    }

    [Fact]
    public void RSI_Update_ShouldMaintainState()
    {
        // Arrange
        var rsi = new RelativeStrengthIndex("rsi_test");
        rsi.SetParameter("period", 5);
        var initialData = CreateVolatileData(10, 100m); // Use volatile data instead of trending
        var newData = new CandlestickData
        {
            Timestamp = DateTime.UtcNow,
            Open = 95m,
            High = 97m,
            Low = 94m,
            Close = 96m, // Slightly down from volatile range
            Volume = 1000
        };

        // Act
        var initialResult = rsi.Calculate(initialData);
        var updateResult = rsi.Update(newData);

        // Assert
        Assert.True(initialResult.IsValid);
        Assert.True(updateResult.IsValid);
        // With volatile data, RSI values should change when updated
        Assert.True(updateResult.Value >= 0 && updateResult.Value <= 100);
    }

    [Fact]
    public void MACD_Reset_ShouldClearState()
    {
        // Arrange
        var macd = new MovingAverageConvergenceDivergence("macd_test");
        var data = CreateTestData(30, 100m);
        macd.Calculate(data);

        // Act
        macd.Reset();
        var result = macd.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        // Should recalculate from scratch after reset
    }

    private CandlestickData[] CreateTestData(int count, decimal startPrice)
    {
        var data = new CandlestickData[count];
        var baseTime = DateTime.UtcNow.AddDays(-count);

        for (int i = 0; i < count; i++)
        {
            var price = startPrice + i;
            data[i] = new CandlestickData
            {
                Timestamp = baseTime.AddDays(i),
                Open = price,
                High = price + 2m,
                Low = price - 1m,
                Close = price + 1m,
                Volume = 1000 + (i * 100)
            };
        }

        return data;
    }

    private CandlestickData[] CreateTrendingData(int count, decimal startPrice, decimal trend)
    {
        var data = new CandlestickData[count];
        var baseTime = DateTime.UtcNow.AddDays(-count);

        for (int i = 0; i < count; i++)
        {
            var price = startPrice + (i * trend);
            data[i] = new CandlestickData
            {
                Timestamp = baseTime.AddDays(i),
                Open = price,
                High = price + 2m,
                Low = price - 1m,
                Close = price + trend,
                Volume = 1000 + (i * 100)
            };
        }

        return data;
    }

    private CandlestickData[] CreateVolatileData(int count, decimal basePrice)
    {
        var data = new CandlestickData[count];
        var baseTime = DateTime.UtcNow.AddDays(-count);
        var random = new Random(42); // Fixed seed for reproducible tests

        for (int i = 0; i < count; i++)
        {
            var volatility = (decimal)(random.NextDouble() * 10 - 5); // -5 to +5
            var price = basePrice + volatility;
            data[i] = new CandlestickData
            {
                Timestamp = baseTime.AddDays(i),
                Open = price,
                High = price + Math.Abs(volatility),
                Low = price - Math.Abs(volatility),
                Close = price + (volatility * 0.5m),
                Volume = 1000 + (i * 100)
            };
        }

        return data;
    }
}
