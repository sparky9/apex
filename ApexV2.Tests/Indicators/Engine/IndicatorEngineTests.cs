using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Tests.Indicators.Engine;

public class IndicatorEngineTests
{
    [Fact]
    public void IndicatorResult_DefaultValues_ShouldBeValid()
    {
        // Arrange & Act
        var result = new IndicatorResult
        {
            Timestamp = DateTime.Now,
            Value = 100.0
        };
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
        Assert.NotNull(result.AdditionalValues);
        Assert.NotNull(result.Metadata);
    }
    
    [Fact]
    public void IndicatorValidationResult_Success_ShouldBeValid()
    {
        // Arrange & Act
        var result = IndicatorValidationResult.Success();
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
        Assert.Empty(result.Warnings);
    }
    
    [Fact]
    public void IndicatorValidationResult_Failure_ShouldContainErrors()
    {
        // Arrange
        var errors = new[] { "Error 1", "Error 2" };
        
        // Act
        var result = IndicatorValidationResult.Failure(errors);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(2, result.Errors.Count);
        Assert.Contains("Error 1", result.Errors);
        Assert.Contains("Error 2", result.Errors);
    }
}

public class TestIndicator : IndicatorBase
{
    public TestIndicator(string id, IChartLogger? logger = null) 
        : base(id, "Test Indicator", "Test indicator for unit testing", IndicatorCategory.Custom, logger)
    {
        SetParameter("period", 10);
    }
    
    public override int MinimumDataPoints => GetParameter<int>("period", 10);
    
    public override IIndicator Clone()
    {
        var clone = new TestIndicator(Id + "_clone");
        clone.Parameters.Clear();
        foreach (var param in Parameters)
        {
            clone.Parameters[param.Key] = param.Value;
        }
        return clone;
    }
    
    protected override IndicatorResult CalculateInternal(CandlestickData[] data)
    {
        var period = GetParameter<int>("period", 10);
        var values = data.TakeLast(period).Select(d => (double)d.Close);
        var average = values.Average();
        
        return CreateResult(data.Last().Timestamp, average);
    }
    
    protected override IndicatorValidationResult ValidateParametersInternal()
    {
        var errors = new List<string>();
        var period = GetParameter<int>("period", 10);
        
        ValidatePositive("period", period, errors);
        ValidateRange("period", period, 1, 1000, errors);
        
        return errors.Count == 0 
            ? IndicatorValidationResult.Success() 
            : IndicatorValidationResult.Failure(errors.ToArray());
    }
}

public class IndicatorBaseTests
{
    [Fact]
    public void IndicatorBase_Constructor_ShouldSetProperties()
    {
        // Arrange & Act
        var indicator = new TestIndicator("test1");
        
        // Assert
        Assert.Equal("test1", indicator.Id);
        Assert.Equal("Test Indicator", indicator.Name);
        Assert.Equal("Test indicator for unit testing", indicator.Description);
        Assert.Equal(IndicatorCategory.Custom, indicator.Category);
        Assert.NotNull(indicator.Parameters);
    }
    
    [Fact]
    public void IndicatorBase_SetParameter_ShouldStoreValue()
    {
        // Arrange
        var indicator = new TestIndicator("test1");
        
        // Act
        indicator.Parameters["testParam"] = 42;
        
        // Assert
        Assert.Equal(42, indicator.Parameters["testParam"]);
    }
    
    [Fact]
    public void IndicatorBase_IsReady_ShouldReturnTrueWhenEnoughData()
    {
        // Arrange
        var indicator = new TestIndicator("test1");
        var data = CreateTestData(15);
        
        // Act
        indicator.Calculate(data);
        
        // Assert
        Assert.True(indicator.IsReady);
    }
    
    [Fact]
    public void IndicatorBase_IsReady_ShouldReturnFalseWhenInsufficientData()
    {
        // Arrange
        var indicator = new TestIndicator("test1");
        var data = CreateTestData(5); // Less than required 10
        
        // Act
        indicator.Calculate(data);
        
        // Assert
        Assert.False(indicator.IsReady);
    }
    
    [Fact]
    public void IndicatorBase_Calculate_ShouldReturnValidResult()
    {
        // Arrange
        var indicator = new TestIndicator("test1");
        var data = CreateTestData(15);
        
        // Act
        var result = indicator.Calculate(data);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.True(result.Value > 0);
        Assert.Null(result.ErrorMessage);
    }
    
    [Fact]
    public void IndicatorBase_Calculate_InsufficientData_ShouldReturnError()
    {
        // Arrange
        var indicator = new TestIndicator("test1");
        var data = CreateTestData(5); // Less than required 10
        
        // Act
        var result = indicator.Calculate(data);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("Insufficient data", result.ErrorMessage);
    }
    
    [Fact]
    public void IndicatorBase_Update_ShouldAddToBuffer()
    {
        // Arrange
        var indicator = new TestIndicator("test1");
        var initialData = CreateTestData(10);
        indicator.Calculate(initialData);
        
        var newData = new CandlestickData
        {
            Timestamp = DateTime.Now,
            Open = 101m,
            High = 105m,
            Low = 100m,
            Close = 103m,
            Volume = 1000
        };
        
        // Act
        var result = indicator.Update(newData);
        
        // Assert
        Assert.True(result.IsValid);
        var testIndicator = (TestIndicator)indicator;
        Assert.Equal(11, testIndicator.DataBuffer.Count);
    }
    
    [Fact]
    public void IndicatorBase_Reset_ShouldClearData()
    {
        // Arrange
        var indicator = new TestIndicator("test1");
        var data = CreateTestData(15);
        indicator.Calculate(data);
        
        // Act
        indicator.Reset();
        
        // Assert
        var testIndicator = (TestIndicator)indicator;
        Assert.Empty(testIndicator.DataBuffer);
        Assert.Empty(testIndicator.ResultCache);
        Assert.False(indicator.IsReady);
    }
    
    [Fact]
    public void IndicatorBase_Clone_ShouldCreateIdenticalCopy()
    {
        // Arrange
        var original = new TestIndicator("test1");
        original.Parameters["customParam"] = "testValue";
        
        // Act
        var clone = (TestIndicator)original.Clone();
        
        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal("test1_clone", clone.Id);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Description, clone.Description);
        Assert.Equal("testValue", clone.Parameters["customParam"]);
    }
    
    [Fact]
    public void IndicatorBase_ValidateParameters_ValidParams_ShouldSucceed()
    {
        // Arrange
        var indicator = new TestIndicator("test1");
        
        // Act
        var result = indicator.ValidateParameters();
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
    
    [Fact]
    public void IndicatorBase_ValidateParameters_InvalidParams_ShouldFail()
    {
        // Arrange
        var indicator = new TestIndicator("test1");
        indicator.Parameters["period"] = -5; // Invalid negative period
        
        // Act
        var result = indicator.ValidateParameters();
        
        // Assert
        Assert.False(result.IsValid);
        Assert.NotEmpty(result.Errors);
    }
    
    private static CandlestickData[] CreateTestData(int count)
    {
        var data = new CandlestickData[count];
        var baseTime = DateTime.Now.AddDays(-count);
        
        for (int i = 0; i < count; i++)
        {
            var price = 100m + i;
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
}
