using System;
using System.IO;
using System.Linq;
using Xunit;
using ApexV2.Indicators.Custom;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Tests.Indicators.Custom;

public class CustomIndicatorServiceTests : IDisposable
{
    private readonly string _testDataPath = Path.Combine(Path.GetTempPath(), "ApexV2Tests");
    private readonly IndicatorService _indicatorService;
    private readonly CustomIndicatorService _customIndicatorService;

    public CustomIndicatorServiceTests()
    {
        // Ensure test directory exists
        Directory.CreateDirectory(_testDataPath);
        
        // Create services
        _indicatorService = new IndicatorService();
        _customIndicatorService = new CustomIndicatorService(_indicatorService, _testDataPath, new NullLogger());
    }

    [Fact]
    public void CreateFormulaIndicator_ShouldCreateAndReturnConfig()
    {
        // Act
        var config = _customIndicatorService.CreateFormulaIndicator(
            "Test Formula", 
            "close * 1.1", 
            "TestAuthor", 
            "A simple test formula");

        // Assert
        Assert.NotNull(config);
        Assert.Equal("Test Formula", config.Name);
        Assert.Equal("close * 1.1", config.Formula);
        Assert.Equal("TestAuthor", config.Author);
        Assert.Equal("A simple test formula", config.Description);
        Assert.NotEmpty(config.Id);
    }

    [Fact]
    public void CreateCompositeIndicator_ShouldCreateAndReturnConfig()
    {
        // Arrange
        var components = new List<ComponentDefinition>
        {
            new ComponentDefinition { Type = "SMA", Alias = "sma", Weight = 0.6 },
            new ComponentDefinition { Type = "EMA", Alias = "ema", Weight = 0.4 }
        };

        // Act
        var config = _customIndicatorService.CreateCompositeIndicator(
            "Test Composite", 
            "TestAuthor", 
            components,
            "weighted_average",
            "A composite indicator test");

        // Assert
        Assert.NotNull(config);
        Assert.Equal("Test Composite", config.Name);
        Assert.Equal("TestAuthor", config.Author);
        Assert.Equal("A composite indicator test", config.Description);
        Assert.NotEmpty(config.Id);
    }

    [Fact]
    public void LoadIndicator_FormulaIndicator_ShouldReturnValidIndicator()
    {
        // Arrange
        var config = _customIndicatorService.CreateFormulaIndicator(
            "Test Load Formula", 
            "high - low", 
            "TestAuthor");

        // Act
        var indicator = _customIndicatorService.LoadIndicator(config);

        // Assert
        Assert.NotNull(indicator);
        Assert.IsType<FormulaIndicator>(indicator);
        Assert.Equal(config.Id, indicator.Id);
        Assert.Equal(config.Name, indicator.Name);
    }

    [Fact]
    public void LoadIndicator_CompositeIndicator_ShouldReturnValidIndicator()
    {
        // Arrange
        var components = new List<ComponentDefinition>
        {
            new ComponentDefinition { Type = "SMA", Alias = "sma", Weight = 1.0 }
        };
        var config = _customIndicatorService.CreateCompositeIndicator(
            "Test Load Composite", 
            "TestAuthor",
            components);

        // Act
        var indicator = _customIndicatorService.LoadIndicator(config);

        // Assert
        Assert.NotNull(indicator);
        Assert.IsType<CompositeIndicator>(indicator);
        Assert.Equal(config.Id, indicator.Id);
        Assert.Equal(config.Name, indicator.Name);
    }

    [Fact]
    public void UpdateIndicator_ShouldModifyConfig()
    {
        // Arrange
        var originalConfig = _customIndicatorService.CreateFormulaIndicator(
            "Original Name", 
            "close", 
            "TestAuthor");

        var updates = new CustomIndicatorUpdate
        {
            Name = "Updated Name",
            Description = "Updated description",
            Formula = "close * 2"
        };

        // Act
        var updatedConfig = _customIndicatorService.UpdateIndicator(originalConfig.Id, updates);

        // Assert
        Assert.NotNull(updatedConfig);
        Assert.Equal("Updated Name", updatedConfig.Name);
        Assert.Equal("Updated description", updatedConfig.Description);
        Assert.Equal("close * 2", updatedConfig.Formula);
        Assert.Equal(originalConfig.Id, updatedConfig.Id);
        Assert.Equal(originalConfig.Author, updatedConfig.Author);
    }

    [Fact]
    public void DeleteIndicator_ShouldRemoveIndicator()
    {
        // Arrange
        var config = _customIndicatorService.CreateFormulaIndicator(
            "Test Delete", 
            "volume", 
            "TestAuthor");

        // Verify it exists
        var allIndicators = _customIndicatorService.GetAllIndicators();
        Assert.Contains(allIndicators, i => i.Id == config.Id);

        // Act
        var result = _customIndicatorService.DeleteIndicator(config.Id);

        // Assert
        Assert.True(result);
        var allIndicatorsAfterDelete = _customIndicatorService.GetAllIndicators();
        Assert.DoesNotContain(allIndicatorsAfterDelete, i => i.Id == config.Id);
    }

    [Fact]
    public void GetAllIndicators_ShouldReturnAllCreatedIndicators()
    {
        // Arrange
        var config1 = _customIndicatorService.CreateFormulaIndicator("Test 1", "close", "Author1");
        var config2 = _customIndicatorService.CreateFormulaIndicator("Test 2", "volume", "Author2");

        // Act
        var allIndicators = _customIndicatorService.GetAllIndicators();

        // Assert
        Assert.Contains(allIndicators, i => i.Id == config1.Id);
        Assert.Contains(allIndicators, i => i.Id == config2.Id);
    }

    [Fact]
    public void SearchIndicators_ByAuthor_ShouldReturnMatching()
    {
        // Arrange
        var config1 = _customIndicatorService.CreateFormulaIndicator("Test Author1", "close", "Author1");
        var config2 = _customIndicatorService.CreateFormulaIndicator("Test Author2", "volume", "Author2");
        var config3 = _customIndicatorService.CreateFormulaIndicator("Another Author1", "high", "Author1");

        var criteria = new IndicatorSearchCriteria { Author = "Author1" };

        // Act
        var results = _customIndicatorService.SearchIndicators(criteria);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("Author1", r.Author));
        Assert.Contains(results, r => r.Id == config1.Id);
        Assert.Contains(results, r => r.Id == config3.Id);
        Assert.DoesNotContain(results, r => r.Id == config2.Id);
    }

    [Fact]
    public void SearchIndicators_ByName_ShouldReturnMatching()
    {
        // Arrange
        var config1 = _customIndicatorService.CreateFormulaIndicator("Moving Average Custom", "close", "TestAuthor");
        var config2 = _customIndicatorService.CreateFormulaIndicator("Volume Moving Average", "volume", "TestAuthor");
        var config3 = _customIndicatorService.CreateFormulaIndicator("Price Oscillator", "high - low", "TestAuthor");

        var criteria = new IndicatorSearchCriteria { Name = "Moving Average" };

        // Act
        var results = _customIndicatorService.SearchIndicators(criteria);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("Moving Average", r.Name));
        Assert.Contains(results, r => r.Id == config1.Id);
        Assert.Contains(results, r => r.Id == config2.Id);
        Assert.DoesNotContain(results, r => r.Id == config3.Id);
    }

    [Fact]
    public void SearchIndicators_ByTags_ShouldReturnMatching()
    {
        // Arrange
        var config1 = _customIndicatorService.CreateFormulaIndicator("Test 1", "close", "TestAuthor");
        config1.Tags.Add("momentum");
        config1.Tags.Add("trend");
        
        var config2 = _customIndicatorService.CreateFormulaIndicator("Test 2", "volume", "TestAuthor");
        config2.Tags.Add("volume");
        config2.Tags.Add("liquidity");
        
        var config3 = _customIndicatorService.CreateFormulaIndicator("Test 3", "high", "TestAuthor");
        config3.Tags.Add("momentum");
        config3.Tags.Add("volatility");

        var criteria = new IndicatorSearchCriteria { Tags = new[] { "momentum" }.ToList() };

        // Act
        var results = _customIndicatorService.SearchIndicators(criteria);

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("momentum", r.Tags));
        Assert.Contains(results, r => r.Id == config1.Id);
        Assert.Contains(results, r => r.Id == config3.Id);
        Assert.DoesNotContain(results, r => r.Id == config2.Id);
    }

    [Fact]
    public void ExportToJson_ShouldCreateValidJson()
    {
        // Arrange
        var config = _customIndicatorService.CreateFormulaIndicator(
            "Test Export", 
            "(high + low + close) / 3", 
            "TestAuthor", 
            "Test export functionality");
        config.Tags.Add("price");
        config.Tags.Add("average");

        // Act
        var json = _customIndicatorService.ExportToJson(config.Id);

        // Assert
        Assert.NotNull(json);
        Assert.Contains("Test Export", json);
        Assert.Contains("(high + low + close) / 3", json);
        Assert.Contains("TestAuthor", json);
        Assert.Contains("Test export functionality", json);
        Assert.Contains("price", json);
        Assert.Contains("average", json);
    }

    [Fact]
    public void ImportFromJson_ShouldCreateValidConfig()
    {
        // Arrange
        var originalConfig = _customIndicatorService.CreateFormulaIndicator(
            "Test Import", 
            "close - open", 
            "ImportAuthor", 
            "Test import functionality");
        originalConfig.Tags.Add("import");
        originalConfig.Tags.Add("test");

        var json = _customIndicatorService.ExportToJson(originalConfig.Id);
        Assert.NotNull(json);

        // Act
        var importedConfig = _customIndicatorService.ImportFromJson(json);

        // Assert
        Assert.NotNull(importedConfig);
        Assert.Equal("Test Import", importedConfig.Name);
        Assert.Equal("close - open", importedConfig.Formula);
        Assert.Equal("ImportAuthor", importedConfig.Author);
        Assert.Equal("Test import functionality", importedConfig.Description);
        Assert.Contains("import", importedConfig.Tags);
        Assert.Contains("test", importedConfig.Tags);
    }

    [Fact]
    public void LoadIndicator_CalculateWithRealData_ShouldWork()
    {
        // Arrange
        var config = _customIndicatorService.CreateFormulaIndicator(
            "Price Range", 
            "high - low", 
            "TestAuthor");
        var indicator = _customIndicatorService.LoadIndicator(config);
        var data = CreateTestData(5, 100m);

        // Act
        var result = indicator.Calculate(data);

        // Assert
        Assert.True(result.IsValid);
        // high - low = (price + 2) - (price - 1) = 3
        Assert.Equal(3.0, result.Value, 1);
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

    public void Dispose()
    {
        // Clean up test files
        if (Directory.Exists(_testDataPath))
        {
            Directory.Delete(_testDataPath, true);
        }
    }
}
