using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ApexV2.Indicators.Custom;
using ApexV2.Indicators.Engine;
using ApexV2.Indicators.Basic;
using ApexV2.Charts.Models;

namespace ApexV2.Tests.Indicators.Custom;

public class CustomIndicatorTests
{
    [Fact]
    public void FormulaIndicator_SimpleFormula_ShouldCalculateCorrectly()
    {
        // Arrange
        var indicator = new FormulaIndicator("test_formula", "Test Formula", "close + 10", "TestAuthor");
        var data = CreateTestData(5, 100m);

        // Act
        var result = indicator.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        // Last close is 105, so formula result should be 105 + 10 = 115
        Assert.Equal(115.0, result.Value, 1);
        Assert.Contains("formula", result.Metadata.Keys);
    }

    [Fact]
    public void FormulaIndicator_ComplexFormula_ShouldCalculateCorrectly()
    {
        // Arrange
        var indicator = new FormulaIndicator("test_complex", "Complex Formula", "(high + low) / 2", "TestAuthor");
        var data = CreateTestData(3, 100m);

        // Act
        var result = indicator.Calculate(data);

        // Assert
        // Debug: Check what we actually got
        if (!result.IsValid)
        {
            // Print the error message for debugging
            throw new Exception($"Formula failed: {result.ErrorMessage}");
        }
        
        Assert.True(result.IsValid);
        // Last data: high = 104, low = 102, so (104 + 102) / 2 = 103
        Assert.Equal(103.0, result.Value, 1);
    }

    [Fact]
    public void FormulaIndicator_InvalidFormula_ShouldReturnInvalid()
    {
        // Arrange
        var indicator = new FormulaIndicator("test_invalid", "Invalid Formula", "unknown_variable + 5", "TestAuthor");
        var data = CreateTestData(5, 100m);

        // Act
        var result = indicator.Calculate(data);

        // Assert
        Assert.False(result.IsValid);
        // Debug: Check what error message we get
        var errorMessage = result.ErrorMessage ?? "No error message provided";
        throw new Exception($"Expected error but got: {errorMessage}");
    }

    [Fact]
    public void FormulaIndicator_ValidateParameters_ShouldCatchInvalidFormula()
    {
        // Arrange
        var indicator = new FormulaIndicator("test_validation", "Validation Test", "((unclosed_parenthesis", "TestAuthor");

        // Act
        var validation = indicator.ValidateParameters();

        // Assert
        Assert.False(validation.IsValid);
        Assert.Contains("parentheses", validation.Errors.First());
    }

    [Fact]
    public void CompositeIndicator_CombineIndicators_ShouldCalculateWeightedAverage()
    {
        // Arrange
        var composite = new CompositeIndicator("test_composite", "Test Composite", "TestAuthor");
        
        var sma = new SimpleMovingAverage("sma_component");
        sma.SetParameter("period", 3);
        
        var ema = new ExponentialMovingAverage("ema_component");
        ema.SetParameter("period", 3);
        
        composite.AddComponent(sma, 0.6, "sma");
        composite.AddComponent(ema, 0.4, "ema");
        
        var data = CreateTestData(5, 100m);

        // Act
        var result = composite.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        Assert.Contains("componentCount", result.Metadata.Keys);
        Assert.Equal(2, result.Metadata["componentCount"]);
        Assert.Contains("combineMethod", result.Metadata.Keys);
        Assert.Equal("weighted_average", result.Metadata["combineMethod"]);
    }

    [Fact]
    public void CompositeIndicator_NoComponents_ShouldReturnInvalid()
    {
        // Arrange
        var composite = new CompositeIndicator("empty_composite", "Empty Composite", "TestAuthor");
        var data = CreateTestData(5, 100m);

        // Act
        var result = composite.Calculate(data);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("No components", result.ErrorMessage);
    }

    [Fact]
    public void CompositeIndicator_RemoveComponent_ShouldUpdateCorrectly()
    {
        // Arrange
        var composite = new CompositeIndicator("test_remove", "Test Remove", "TestAuthor");
        var sma = new SimpleMovingAverage("sma_test");
        var rsi = new RelativeStrengthIndex("rsi_test");
        
        composite.AddComponent(sma, 1.0, "sma");
        composite.AddComponent(rsi, 1.0, "rsi");

        // Act
        composite.RemoveComponent("sma");
        var data = CreateTestData(20, 100m); // Need more data for RSI
        var result = composite.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(1, result.Metadata["componentCount"]);
    }

    [Fact]
    public void CompositeIndicator_DifferentCombineMethods_ShouldProduceDifferentResults()
    {
        // Arrange
        var composite1 = new CompositeIndicator("test_sum", "Test Sum", "TestAuthor");
        var composite2 = new CompositeIndicator("test_max", "Test Max", "TestAuthor");
        
        var sma1 = new SimpleMovingAverage("sma1");
        sma1.SetParameter("period", 3);
        var sma2 = new SimpleMovingAverage("sma2");
        sma2.SetParameter("period", 5);
        
        composite1.AddComponent(sma1.Clone(), 1.0, "sma1");
        composite1.AddComponent(sma2.Clone(), 1.0, "sma2");
        composite1.SetParameter("combineMethod", "sum");
        
        composite2.AddComponent(sma1.Clone(), 1.0, "sma1");
        composite2.AddComponent(sma2.Clone(), 1.0, "sma2");
        composite2.SetParameter("combineMethod", "max");
        
        var data = CreateTestData(10, 100m);

        // Act
        var result1 = composite1.Calculate(data);
        var result2 = composite2.Calculate(data);

        // Assert
        Assert.True(result1.IsValid);
        Assert.True(result2.IsValid);
        Assert.NotEqual(result1.Value, result2.Value);
    }

    [Fact]
    public void CustomIndicatorBase_TagManagement_ShouldWorkCorrectly()
    {
        // Arrange
        var indicator = new FormulaIndicator("test_tags", "Test Tags", "close", "TestAuthor");

        // Act
        indicator.AddTag("momentum");
        indicator.AddTag("trend");
        indicator.AddTag("momentum"); // Duplicate should be ignored
        indicator.RemoveTag("trend");

        // Assert
        Assert.Single(indicator.Tags);
        Assert.Contains("momentum", indicator.Tags);
        Assert.DoesNotContain("trend", indicator.Tags);
    }

    [Fact]
    public void CustomIndicatorBase_Export_ShouldCreateValidConfig()
    {
        // Arrange
        var indicator = new FormulaIndicator("test_export", "Test Export", "high - low", "TestAuthor");
        indicator.SetParameter("lookback", 5);
        indicator.AddTag("volatility");

        // Act
        var config = indicator.Export();

        // Assert
        Assert.Equal("test_export", config.Id);
        Assert.Equal("Test Export", config.Name);
        Assert.Equal("TestAuthor", config.Author);
        Assert.Equal("high - low", config.Formula);
        Assert.Contains("volatility", config.Tags);
        Assert.Contains("lookback", config.Parameters.Keys);
        Assert.Equal(5, config.Parameters["lookback"]);
    }

    [Fact]
    public void FormulaIndicator_Clone_ShouldCreateIdenticalCopy()
    {
        // Arrange
        var original = new FormulaIndicator("original", "Original Formula", "close * 2", "TestAuthor");
        original.SetParameter("lookback", 10);
        original.AddTag("test");

        // Act
        var clone = (FormulaIndicator)original.Clone();

        // Assert
        Assert.NotEqual(original.Id, clone.Id);
        Assert.Equal(original.Name, clone.Name);
        Assert.Equal(original.Formula, clone.Formula);
        Assert.Equal(original.Author, clone.Author);
        Assert.Contains("test", clone.Tags);
    }

    [Fact]
    public void CompositeIndicator_GetComponentResults_ShouldReturnAllResults()
    {
        // Arrange
        var composite = new CompositeIndicator("test_component_results", "Test Component Results", "TestAuthor");
        var sma = new SimpleMovingAverage("sma_comp");
        sma.SetParameter("period", 3);
        var rsi = new RelativeStrengthIndex("rsi_comp");
        rsi.SetParameter("period", 5);
        
        composite.AddComponent(sma, 1.0, "sma");
        composite.AddComponent(rsi, 1.0, "rsi");
        
        var data = CreateTestData(20, 100m);

        // Act
        var componentResults = composite.GetComponentResults(data);

        // Assert
        Assert.Equal(2, componentResults.Count);
        Assert.Contains("sma", componentResults.Keys);
        Assert.Contains("rsi", componentResults.Keys);
        Assert.True(componentResults["sma"].IsValid);
        Assert.True(componentResults["rsi"].IsValid);
    }

    [Fact]
    public void FormulaIndicator_BuiltInFunctions_ShouldWork()
    {
        // Arrange
        var indicator = new FormulaIndicator("test_functions", "Test Functions", "abs(-5)", "TestAuthor");
        var data = CreateTestData(3, 100m);

        // Act
        var result = indicator.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(5.0, result.Value, 1);
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
}
