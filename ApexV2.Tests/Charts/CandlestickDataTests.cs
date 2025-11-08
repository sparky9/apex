using System;
using System.Collections.Generic;
using Xunit;
using ApexV2.Charts.Models;

namespace ApexV2.Tests.Charts;

public class CandlestickDataTests
{
    [Fact]
    public void IsBullish_ShouldReturnTrueWhenCloseGreaterThanOpen()
    {
        // Arrange
        var candle = new CandlestickData
        {
            Open = 100m,
            Close = 105m,
            High = 110m,
            Low = 95m
        };

        // Act & Assert
        Assert.True(candle.IsBullish);
    }

    [Fact]
    public void IsBullish_ShouldReturnFalseWhenCloseLessThanOpen()
    {
        // Arrange
        var candle = new CandlestickData
        {
            Open = 105m,
            Close = 100m,
            High = 110m,
            Low = 95m
        };

        // Act & Assert
        Assert.False(candle.IsBullish);
    }

    [Fact]
    public void IsBullish_ShouldReturnTrueWhenCloseEqualsOpen()
    {
        // Arrange
        var candle = new CandlestickData
        {
            Open = 100m,
            Close = 100m,
            High = 105m,
            Low = 95m
        };

        // Act & Assert
        Assert.True(candle.IsBullish);
    }

    [Fact]
    public void BodySize_ShouldReturnAbsoluteDifferenceBetweenOpenAndClose()
    {
        // Arrange
        var bullishCandle = new CandlestickData { Open = 100m, Close = 105m };
        var bearishCandle = new CandlestickData { Open = 105m, Close = 100m };

        // Act & Assert
        Assert.Equal(5m, bullishCandle.BodySize);
        Assert.Equal(5m, bearishCandle.BodySize);
    }

    [Fact]
    public void UpperShadow_ShouldCalculateCorrectly()
    {
        // Arrange
        var bullishCandle = new CandlestickData
        {
            Open = 100m,
            Close = 105m,
            High = 110m,
            Low = 95m
        };
        
        var bearishCandle = new CandlestickData
        {
            Open = 105m,
            Close = 100m,
            High = 110m,
            Low = 95m
        };

        // Act & Assert
        Assert.Equal(5m, bullishCandle.UpperShadow); // 110 - 105
        Assert.Equal(5m, bearishCandle.UpperShadow); // 110 - 105
    }

    [Fact]
    public void LowerShadow_ShouldCalculateCorrectly()
    {
        // Arrange
        var bullishCandle = new CandlestickData
        {
            Open = 100m,
            Close = 105m,
            High = 110m,
            Low = 95m
        };
        
        var bearishCandle = new CandlestickData
        {
            Open = 105m,
            Close = 100m,
            High = 110m,
            Low = 95m
        };

        // Act & Assert
        Assert.Equal(5m, bullishCandle.LowerShadow); // 100 - 95
        Assert.Equal(5m, bearishCandle.LowerShadow); // 100 - 95
    }

    [Fact]
    public void Range_ShouldReturnHighMinusLow()
    {
        // Arrange
        var candle = new CandlestickData
        {
            High = 110m,
            Low = 95m
        };

        // Act & Assert
        Assert.Equal(15m, candle.Range);
    }

    [Fact]
    public void ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var candle = new CandlestickData
        {
            Timestamp = new DateTime(2023, 1, 1, 9, 30, 0),
            Open = 100.50m,
            High = 105.75m,
            Low = 99.25m,
            Close = 103.00m,
            Volume = 150000
        };

        // Act
        var result = candle.ToString();

        // Assert
        Assert.Contains("2023-01-01 09:30", result);
        Assert.Contains("O:100.50", result);
        Assert.Contains("H:105.75", result);
        Assert.Contains("L:99.25", result);
        Assert.Contains("C:103.00", result);
        Assert.Contains("V:150000", result);
    }
}
