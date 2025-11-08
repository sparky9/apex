using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using ApexV2.Indicators.Engine;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Tests.Indicators.Engine;

public class IndicatorServiceTests
{
    [Fact]
    public void IndicatorService_Constructor_ShouldInitialize()
    {
        // Arrange & Act
        var service = new IndicatorService();
        
        // Assert
        Assert.NotNull(service.Indicators);
        Assert.NotNull(service.AvailableTypes);
        Assert.Empty(service.Indicators);
    }
    
    [Fact]
    public void IndicatorService_RegisterIndicatorType_ShouldAddFactory()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        
        // Act
        service.RegisterIndicatorType("TEST", factory);
        
        // Assert
        Assert.True(service.AvailableTypes.ContainsKey("TEST"));
    }
    
    [Fact]
    public void IndicatorService_RegisterIndicatorType_NullType_ShouldThrow()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.RegisterIndicatorType("", factory));
        Assert.Throws<ArgumentException>(() => service.RegisterIndicatorType(null!, factory));
    }
    
    [Fact]
    public void IndicatorService_RegisterIndicatorType_NullFactory_ShouldThrow()
    {
        // Arrange
        var service = new IndicatorService();
        
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.RegisterIndicatorType("TEST", null!));
    }
    
    [Fact]
    public void IndicatorService_CreateIndicator_ShouldCreateAndRegister()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        service.RegisterIndicatorType("TEST", factory);
        
        // Act
        var indicator = service.CreateIndicator("TEST", "test1");
        
        // Assert
        Assert.NotNull(indicator);
        Assert.Equal("test1", indicator.Id);
        Assert.True(service.Indicators.ContainsKey("test1"));
    }
    
    [Fact]
    public void IndicatorService_CreateIndicator_UnknownType_ShouldThrow()
    {
        // Arrange
        var service = new IndicatorService();
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => service.CreateIndicator("UNKNOWN", "test1"));
    }
    
    [Fact]
    public void IndicatorService_CreateIndicator_DuplicateId_ShouldThrow()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        service.RegisterIndicatorType("TEST", factory);
        service.CreateIndicator("TEST", "test1");
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.CreateIndicator("TEST", "test1"));
    }
    
    [Fact]
    public void IndicatorService_CreateIndicator_WithParameters_ShouldApplyParams()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => 
        {
            var indicator = new ServiceTestIndicator(id, logger);
            foreach (var param in parameters)
            {
                indicator.Parameters[param.Key] = param.Value;
            }
            return indicator;
        });
        service.RegisterIndicatorType("TEST", factory);
        
        var parameters = new Dictionary<string, object> { ["period"] = 20 };
        
        // Act
        var indicator = service.CreateIndicator("TEST", "test1", parameters);
        
        // Assert
        Assert.Equal(20, indicator.Parameters["period"]);
    }
    
    [Fact]
    public void IndicatorService_GetIndicator_ExistingId_ShouldReturnIndicator()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        service.RegisterIndicatorType("TEST", factory);
        var created = service.CreateIndicator("TEST", "test1");
        
        // Act
        var retrieved = service.GetIndicator("test1");
        
        // Assert
        Assert.Same(created, retrieved);
    }
    
    [Fact]
    public void IndicatorService_GetIndicator_NonExistingId_ShouldReturnNull()
    {
        // Arrange
        var service = new IndicatorService();
        
        // Act
        var indicator = service.GetIndicator("nonexistent");
        
        // Assert
        Assert.Null(indicator);
    }
    
    [Fact]
    public void IndicatorService_RemoveIndicator_ExistingId_ShouldRemove()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        service.RegisterIndicatorType("TEST", factory);
        service.CreateIndicator("TEST", "test1");
        
        // Act
        var removed = service.RemoveIndicator("test1");
        
        // Assert
        Assert.True(removed);
        Assert.False(service.Indicators.ContainsKey("test1"));
    }
    
    [Fact]
    public void IndicatorService_RemoveIndicator_NonExistingId_ShouldReturnFalse()
    {
        // Arrange
        var service = new IndicatorService();
        
        // Act
        var removed = service.RemoveIndicator("nonexistent");
        
        // Assert
        Assert.False(removed);
    }
    
    [Fact]
    public void IndicatorService_UpdateAllIndicators_ShouldUpdateAll()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        service.RegisterIndicatorType("TEST", factory);
        
        var indicator1 = service.CreateIndicator("TEST", "test1");
        var indicator2 = service.CreateIndicator("TEST", "test2");
        
        // Initialize with some data
        var initialData = CreateTestData(15);
        indicator1.Calculate(initialData);
        indicator2.Calculate(initialData);
        
        var newData = new CandlestickData
        {
            Timestamp = DateTime.Now,
            Open = 101m,
            High = 105m,
            Low = 100m,
            Close = 103m,
            Volume = 1000
        };
        
        var updateEvents = new List<IndicatorUpdatedEventArgs>();
        service.IndicatorUpdated += (s, e) => updateEvents.Add(e);
        
        // Act
        service.UpdateAllIndicators(newData);
        
        // Assert
        Assert.Equal(2, updateEvents.Count);
        Assert.Contains(updateEvents, e => e.Indicator.Id == "test1");
        Assert.Contains(updateEvents, e => e.Indicator.Id == "test2");
    }
    
    [Fact]
    public void IndicatorService_ResetAllIndicators_ShouldResetAll()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        service.RegisterIndicatorType("TEST", factory);
        
        var indicator1 = service.CreateIndicator("TEST", "test1");
        var indicator2 = service.CreateIndicator("TEST", "test2");
        
        // Initialize with some data
        var initialData = CreateTestData(15);
        indicator1.Calculate(initialData);
        indicator2.Calculate(initialData);
        
        // Act
        service.ResetAllIndicators();
        
        // Assert
        var testIndicator1 = (ServiceTestIndicator)indicator1;
        var testIndicator2 = (ServiceTestIndicator)indicator2;
        Assert.Empty(testIndicator1.DataBuffer);
        Assert.Empty(testIndicator2.DataBuffer);
    }
    
    [Fact]
    public void IndicatorService_GetIndicatorsByCategory_ShouldFilterCorrectly()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        service.RegisterIndicatorType("TEST", factory);
        
        service.CreateIndicator("TEST", "test1");
        service.CreateIndicator("TEST", "test2");
        
        // Act
        var customIndicators = service.GetIndicatorsByCategory(IndicatorCategory.Custom);
        
        // Assert
        Assert.Equal(2, customIndicators.Count());
    }
    
    [Fact]
    public void IndicatorService_GetStatistics_ShouldReturnCorrectCounts()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        service.RegisterIndicatorType("TEST", factory);
        
        service.CreateIndicator("TEST", "test1");
        service.CreateIndicator("TEST", "test2");
        
        // Act
        var stats = service.GetStatistics();
        
        // Assert
        Assert.Equal(2, stats.TotalIndicators);
        Assert.True(stats.AvailableTypes > 0);
        Assert.Equal(2, stats.CategoryCounts[IndicatorCategory.Custom]);
    }
    
    [Fact]
    public void IndicatorService_Events_ShouldFireCorrectly()
    {
        // Arrange
        var service = new IndicatorService();
        var factory = new IndicatorFactory((id, parameters, logger) => new ServiceTestIndicator(id, logger));
        service.RegisterIndicatorType("TEST", factory);
        
        var addedEvents = new List<IndicatorEventArgs>();
        var removedEvents = new List<IndicatorEventArgs>();
        
        service.IndicatorAdded += (s, e) => addedEvents.Add(e);
        service.IndicatorRemoved += (s, e) => removedEvents.Add(e);
        
        // Act
        var indicator = service.CreateIndicator("TEST", "test1");
        service.RemoveIndicator("test1");
        
        // Assert
        Assert.Single(addedEvents);
        Assert.Single(removedEvents);
        Assert.Same(indicator, addedEvents[0].Indicator);
        Assert.Same(indicator, removedEvents[0].Indicator);
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
