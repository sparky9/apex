using System;
using System.Linq;
using Xunit;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;

namespace ApexV2.Tests.Indicators.Engine;

public class IndicatorMathTests
{
    [Fact]
    public void SimpleMovingAverage_ValidData_ShouldCalculateCorrectly()
    {
        // Arrange
        var values = new double[] { 10, 20, 30, 40, 50 };
        var period = 3;
        
        // Act
        var result = IndicatorMath.SimpleMovingAverage(values, period);
        
        // Assert
        Assert.Equal(40.0, result); // (30 + 40 + 50) / 3
    }
    
    [Fact]
    public void SimpleMovingAverage_InsufficientData_ShouldReturnNaN()
    {
        // Arrange
        var values = new double[] { 10, 20 };
        var period = 3;
        
        // Act
        var result = IndicatorMath.SimpleMovingAverage(values, period);
        
        // Assert
        Assert.True(double.IsNaN(result));
    }
    
    [Fact]
    public void ExponentialMovingAverage_FirstCalculation_ShouldUseSMA()
    {
        // Arrange
        var values = new double[] { 10, 20, 30, 40, 50 };
        var period = 3;
        
        // Act
        var result = IndicatorMath.ExponentialMovingAverage(values, period);
        
        // Assert
        Assert.Equal(20.0, result); // SMA of first 3 values: (10 + 20 + 30) / 3
    }
    
    [Fact]
    public void ExponentialMovingAverage_WithPreviousEMA_ShouldCalculateCorrectly()
    {
        // Arrange
        var values = new double[] { 50 };
        var period = 3;
        var previousEma = 40.0;
        var multiplier = 2.0 / (period + 1); // 0.5
        
        // Act
        var result = IndicatorMath.ExponentialMovingAverage(values, period, previousEma);
        
        // Assert
        var expected = (50 * multiplier) + (previousEma * (1 - multiplier));
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void TypicalPrice_ValidCandlestick_ShouldCalculateCorrectly()
    {
        // Arrange
        var candlestick = new CandlestickData
        {
            High = 105m,
            Low = 95m,
            Close = 100m
        };
        
        // Act
        var result = IndicatorMath.TypicalPrice(candlestick);
        
        // Assert
        Assert.Equal(100.0, result); // (105 + 95 + 100) / 3
    }
    
    [Fact]
    public void WeightedClose_ValidCandlestick_ShouldCalculateCorrectly()
    {
        // Arrange
        var candlestick = new CandlestickData
        {
            High = 105m,
            Low = 95m,
            Close = 100m
        };
        
        // Act
        var result = IndicatorMath.WeightedClose(candlestick);
        
        // Assert
        Assert.Equal(100.0, result); // (105 + 95 + 100 + 100) / 4
    }
    
    [Fact]
    public void TrueRange_WithPrevious_ShouldCalculateCorrectly()
    {
        // Arrange
        var current = new CandlestickData
        {
            High = 105m,
            Low = 95m,
            Close = 100m
        };
        
        var previous = new CandlestickData
        {
            Close = 98m
        };
        
        // Act
        var result = IndicatorMath.TrueRange(current, previous);
        
        // Assert
        var highLow = 105 - 95; // 10
        var highClose = Math.Abs(105 - 98); // 7
        var lowClose = Math.Abs(95 - 98); // 3
        var expected = Math.Max(highLow, Math.Max(highClose, lowClose)); // 10
        
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void TrueRange_WithoutPrevious_ShouldReturnHighLow()
    {
        // Arrange
        var current = new CandlestickData
        {
            High = 105m,
            Low = 95m,
            Close = 100m
        };
        
        // Act
        var result = IndicatorMath.TrueRange(current, null);
        
        // Assert
        Assert.Equal(10.0, result); // 105 - 95
    }
    
    [Fact]
    public void StandardDeviation_ValidData_ShouldCalculateCorrectly()
    {
        // Arrange
        var values = new double[] { 2, 4, 4, 4, 5, 5, 7, 9 };
        var period = values.Length;
        
        // Act
        var result = IndicatorMath.StandardDeviation(values, period);
        
        // Assert
        var mean = values.Average(); // 5
        var variance = values.Select(v => Math.Pow(v - mean, 2)).Average(); // 4
        var expected = Math.Sqrt(variance); // 2
        
        Assert.Equal(expected, result, 10);
    }
    
    [Fact]
    public void StandardDeviation_InsufficientData_ShouldReturnNaN()
    {
        // Arrange
        var values = new double[] { 1, 2 };
        var period = 5;
        
        // Act
        var result = IndicatorMath.StandardDeviation(values, period);
        
        // Assert
        Assert.True(double.IsNaN(result));
    }
    
    [Fact]
    public void CalculateGainsAndLosses_ValidPrices_ShouldSeparateCorrectly()
    {
        // Arrange
        var prices = new double[] { 100, 105, 102, 108, 106 };
        var period = 4;
        
        // Act
        var (gains, losses) = IndicatorMath.CalculateGainsAndLosses(prices, period);
        
        // Assert
        Assert.Equal(4, gains.Length);
        Assert.Equal(4, losses.Length);
        
        // Changes: +5, -3, +6, -2
        Assert.Equal(5.0, gains[0]);
        Assert.Equal(0.0, gains[1]);
        Assert.Equal(6.0, gains[2]);
        Assert.Equal(0.0, gains[3]);
        
        Assert.Equal(0.0, losses[0]);
        Assert.Equal(3.0, losses[1]);
        Assert.Equal(0.0, losses[2]);
        Assert.Equal(2.0, losses[3]);
    }
    
    [Fact]
    public void CalculateBollingerBands_ValidData_ShouldCalculateCorrectly()
    {
        // Arrange
        var prices = new double[] { 20, 22, 24, 25, 23, 26, 28, 26, 29, 27 };
        var period = 10;
        var standardDeviations = 2.0;
        
        // Act
        var (middle, upper, lower) = IndicatorMath.CalculateBollingerBands(prices, period, standardDeviations);
        
        // Assert
        var expectedMiddle = prices.Average(); // SMA
        var expectedStdDev = IndicatorMath.StandardDeviation(prices, period);
        var expectedUpper = expectedMiddle + (standardDeviations * expectedStdDev);
        var expectedLower = expectedMiddle - (standardDeviations * expectedStdDev);
        
        Assert.Equal(expectedMiddle, middle, 10);
        Assert.Equal(expectedUpper, upper, 10);
        Assert.Equal(expectedLower, lower, 10);
    }
    
    [Fact]
    public void CalculateBollingerBands_InsufficientData_ShouldReturnNaN()
    {
        // Arrange
        var prices = new double[] { 20, 22 };
        var period = 10;
        var standardDeviations = 2.0;
        
        // Act
        var (middle, upper, lower) = IndicatorMath.CalculateBollingerBands(prices, period, standardDeviations);
        
        // Assert
        Assert.True(double.IsNaN(middle));
        Assert.True(double.IsNaN(upper));
        Assert.True(double.IsNaN(lower));
    }
    
    [Fact]
    public void IsValidNumber_ValidNumbers_ShouldReturnTrue()
    {
        // Arrange & Act & Assert
        Assert.True(IndicatorMath.IsValidNumber(10.5));
        Assert.True(IndicatorMath.IsValidNumber(0.0));
        Assert.True(IndicatorMath.IsValidNumber(-5.2));
    }
    
    [Fact]
    public void IsValidNumber_InvalidNumbers_ShouldReturnFalse()
    {
        // Arrange & Act & Assert
        Assert.False(IndicatorMath.IsValidNumber(double.NaN));
        Assert.False(IndicatorMath.IsValidNumber(double.PositiveInfinity));
        Assert.False(IndicatorMath.IsValidNumber(double.NegativeInfinity));
    }
    
    [Fact]
    public void Clamp_ValueWithinRange_ShouldReturnValue()
    {
        // Arrange
        var value = 5.0;
        var min = 0.0;
        var max = 10.0;
        
        // Act
        var result = IndicatorMath.Clamp(value, min, max);
        
        // Assert
        Assert.Equal(value, result);
    }
    
    [Fact]
    public void Clamp_ValueBelowMin_ShouldReturnMin()
    {
        // Arrange
        var value = -5.0;
        var min = 0.0;
        var max = 10.0;
        
        // Act
        var result = IndicatorMath.Clamp(value, min, max);
        
        // Assert
        Assert.Equal(min, result);
    }
    
    [Fact]
    public void Clamp_ValueAboveMax_ShouldReturnMax()
    {
        // Arrange
        var value = 15.0;
        var min = 0.0;
        var max = 10.0;
        
        // Act
        var result = IndicatorMath.Clamp(value, min, max);
        
        // Assert
        Assert.Equal(max, result);
    }
    
    [Fact]
    public void PercentageChange_ValidValues_ShouldCalculateCorrectly()
    {
        // Arrange
        var oldValue = 100.0;
        var newValue = 110.0;
        
        // Act
        var result = IndicatorMath.PercentageChange(oldValue, newValue);
        
        // Assert
        Assert.Equal(10.0, result);
    }
    
    [Fact]
    public void PercentageChange_ZeroOldValue_ShouldReturnNaN()
    {
        // Arrange
        var oldValue = 0.0;
        var newValue = 110.0;
        
        // Act
        var result = IndicatorMath.PercentageChange(oldValue, newValue);
        
        // Assert
        Assert.True(double.IsNaN(result));
    }
}
