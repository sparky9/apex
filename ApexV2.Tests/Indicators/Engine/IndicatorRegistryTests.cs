using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ApexV2.Indicators.Engine;

namespace ApexV2.Tests.Indicators.Engine;

public class IndicatorRegistryTests
{
    [Fact]
    public void IndicatorRegistry_Constructor_ShouldInitializeBuiltIns()
    {
        // Arrange & Act
        var registry = new IndicatorRegistry();
        
        // Assert
        Assert.NotEmpty(registry.Metadata);
        Assert.True(registry.Metadata.ContainsKey("SMA"));
        Assert.True(registry.Metadata.ContainsKey("EMA"));
        Assert.True(registry.Metadata.ContainsKey("RSI"));
        Assert.True(registry.Metadata.ContainsKey("MACD"));
        Assert.True(registry.Metadata.ContainsKey("BBANDS"));
    }
    
    [Fact]
    public void IndicatorRegistry_RegisterIndicator_ShouldAddMetadata()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var metadata = new IndicatorMetadata
        {
            Type = "CUSTOM",
            Name = "Custom Indicator",
            Description = "A custom test indicator",
            Category = IndicatorCategory.Custom,
            SupportedDataTypes = new[] { "price" },
            Parameters = new[]
            {
                new ParameterMetadata
                {
                    Name = "period",
                    Type = typeof(int),
                    IsRequired = true,
                    DefaultValue = 14
                }
            }
        };
        
        // Act
        registry.RegisterIndicator(metadata);
        
        // Assert
        Assert.True(registry.Metadata.ContainsKey("CUSTOM"));
        Assert.Equal("Custom Indicator", registry.Metadata["CUSTOM"].Name);
    }
    
    [Fact]
    public void IndicatorRegistry_RegisterIndicator_NullMetadata_ShouldThrow()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => registry.RegisterIndicator(null!));
    }
    
    [Fact]
    public void IndicatorRegistry_RegisterIndicator_EmptyType_ShouldThrow()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var metadata = new IndicatorMetadata
        {
            Type = "",
            Name = "Test"
        };
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => registry.RegisterIndicator(metadata));
    }
    
    [Fact]
    public void IndicatorRegistry_GetMetadata_ExistingType_ShouldReturnMetadata()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        
        // Act
        var metadata = registry.GetMetadata("SMA");
        
        // Assert
        Assert.NotNull(metadata);
        Assert.Equal("Simple Moving Average", metadata.Name);
        Assert.Equal(IndicatorCategory.Trend, metadata.Category);
    }
    
    [Fact]
    public void IndicatorRegistry_GetMetadata_NonExistingType_ShouldReturnNull()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        
        // Act
        var metadata = registry.GetMetadata("NONEXISTENT");
        
        // Assert
        Assert.Null(metadata);
    }
    
    [Fact]
    public void IndicatorRegistry_GetByCategory_ShouldFilterCorrectly()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        
        // Act
        var trendIndicators = registry.GetByCategory(IndicatorCategory.Trend).ToArray();
        var momentumIndicators = registry.GetByCategory(IndicatorCategory.Momentum).ToArray();
        var volatilityIndicators = registry.GetByCategory(IndicatorCategory.Volatility).ToArray();
        
        // Assert
        Assert.Contains(trendIndicators, m => m.Type == "SMA");
        Assert.Contains(trendIndicators, m => m.Type == "EMA");
        Assert.Contains(momentumIndicators, m => m.Type == "RSI");
        Assert.Contains(momentumIndicators, m => m.Type == "MACD");
        Assert.Contains(volatilityIndicators, m => m.Type == "BBANDS");
    }
    
    [Fact]
    public void IndicatorRegistry_Search_ByName_ShouldFindMatches()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        
        // Act
        var results = registry.Search("moving").ToArray();
        
        // Assert
        Assert.Contains(results, m => m.Type == "SMA");
        Assert.Contains(results, m => m.Type == "EMA");
        Assert.Contains(results, m => m.Type == "MACD");
    }
    
    [Fact]
    public void IndicatorRegistry_Search_ByDescription_ShouldFindMatches()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        
        // Act
        var results = registry.Search("momentum").ToArray();
        
        // Assert
        Assert.Contains(results, m => m.Type == "RSI");
        Assert.Contains(results, m => m.Type == "MACD");
    }
    
    [Fact]
    public void IndicatorRegistry_Search_EmptyTerm_ShouldReturnAll()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        
        // Act
        var results = registry.Search("").ToArray();
        
        // Assert
        Assert.Equal(registry.Metadata.Count, results.Length);
    }
    
    [Fact]
    public void IndicatorRegistry_GetCompatibleIndicators_ShouldFilterByDataType()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        
        // Act
        var priceIndicators = registry.GetCompatibleIndicators("price").ToArray();
        var volumeIndicators = registry.GetCompatibleIndicators("volume").ToArray();
        
        // Assert
        Assert.True(priceIndicators.Length > 0);
        Assert.Contains(priceIndicators, m => m.Type == "SMA");
        Assert.Contains(priceIndicators, m => m.Type == "RSI");
        
        Assert.True(volumeIndicators.Length > 0);
        Assert.Contains(volumeIndicators, m => m.Type == "SMA");
        Assert.Contains(volumeIndicators, m => m.Type == "EMA");
    }
    
    [Fact]
    public void IndicatorRegistry_ValidateParameters_ValidParams_ShouldSucceed()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var parameters = new Dictionary<string, object>
        {
            ["period"] = 20
        };
        
        // Act
        var result = registry.ValidateParameters("SMA", parameters);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
    
    [Fact]
    public void IndicatorRegistry_ValidateParameters_MissingRequired_ShouldFail()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var parameters = new Dictionary<string, object>(); // Missing required 'period'
        
        // Act
        var result = registry.ValidateParameters("SMA", parameters);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Required parameter 'period' is missing"));
    }
    
    [Fact]
    public void IndicatorRegistry_ValidateParameters_InvalidType_ShouldFail()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var parameters = new Dictionary<string, object>
        {
            ["period"] = "invalid" // String instead of int
        };
        
        // Act
        var result = registry.ValidateParameters("SMA", parameters);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("incorrect type"));
    }
    
    [Fact]
    public void IndicatorRegistry_ValidateParameters_OutOfRange_ShouldFail()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var parameters = new Dictionary<string, object>
        {
            ["period"] = -5 // Below minimum value
        };
        
        // Act
        var result = registry.ValidateParameters("SMA", parameters);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("below minimum value"));
    }
    
    [Fact]
    public void IndicatorRegistry_ValidateParameters_UnknownParameter_ShouldWarn()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var parameters = new Dictionary<string, object>
        {
            ["period"] = 20,
            ["unknownParam"] = 42
        };
        
        // Act
        var result = registry.ValidateParameters("SMA", parameters);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("Unknown parameter 'unknownParam'"));
    }
    
    [Fact]
    public void IndicatorRegistry_ValidateParameters_UnknownIndicator_ShouldFail()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var parameters = new Dictionary<string, object>();
        
        // Act
        var result = registry.ValidateParameters("UNKNOWN", parameters);
        
        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Unknown indicator type: UNKNOWN"));
    }
    
    [Fact]
    public void IndicatorRegistry_ValidateParameters_MACD_AllParams_ShouldSucceed()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var parameters = new Dictionary<string, object>
        {
            ["fastPeriod"] = 12,
            ["slowPeriod"] = 26,
            ["signalPeriod"] = 9
        };
        
        // Act
        var result = registry.ValidateParameters("MACD", parameters);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
    
    [Fact]
    public void IndicatorRegistry_ValidateParameters_BollingerBands_ShouldAcceptDouble()
    {
        // Arrange
        var registry = new IndicatorRegistry();
        var parameters = new Dictionary<string, object>
        {
            ["period"] = 20,
            ["standardDeviations"] = 2.0
        };
        
        // Act
        var result = registry.ValidateParameters("BBANDS", parameters);
        
        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
