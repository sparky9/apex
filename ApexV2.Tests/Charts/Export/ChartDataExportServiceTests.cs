using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ApexV2.Charts.Export;
using ApexV2.Charts.Models;

namespace ApexV2.Tests.Charts.Export;

public class ChartDataExportServiceTests
{
    [Fact]
    public void ChartDataExportService_GetSupportedDataFormats_ShouldReturnValidFormats()
    {
        // Arrange
        var service = new ChartDataExportService();
        
        // Act
        var formats = service.GetSupportedDataFormats();
        
        // Assert
        Assert.NotNull(formats);
        Assert.Contains("CSV", formats);
        Assert.Contains("JSON", formats);
        Assert.Contains("Excel (CSV)", formats);
        Assert.Contains("Summary (TXT)", formats);
    }
    
    [Fact]
    public void ChartDataExportService_GetDataFileFilter_ShouldReturnValidFilter()
    {
        // Arrange
        var service = new ChartDataExportService();
        
        // Act
        var filter = service.GetDataFileFilter();
        
        // Assert
        Assert.NotNull(filter);
        Assert.Contains("*.csv", filter);
        Assert.Contains("*.json", filter);
        Assert.Contains("*.txt", filter);
    }
    
    [Fact]
    public async Task ChartDataExportService_ExportDataAsCsv_ShouldCreateValidFile()
    {
        // Arrange
        var service = new ChartDataExportService();
        var data = CreateTestData();
        var filePath = Path.GetTempFileName();
        
        try
        {
            // Act
            var result = await service.ExportDataAsCsvAsync(data, filePath);
            
            // Assert
            Assert.True(result);
            Assert.True(File.Exists(filePath));
            
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("Date,Time,Open,High,Low,Close,Volume", content);
            Assert.Contains("100.00", content); // Check for data values
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
    
    [Fact]
    public async Task ChartDataExportService_ExportDataAsJson_ShouldCreateValidFile()
    {
        // Arrange
        var service = new ChartDataExportService();
        var data = CreateTestData();
        var filePath = Path.GetTempFileName();
        
        try
        {
            // Act
            var result = await service.ExportDataAsJsonAsync(data, filePath);
            
            // Assert
            Assert.True(result);
            Assert.True(File.Exists(filePath));
            
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("timestamp", content);
            Assert.Contains("open", content);
            Assert.Contains("high", content);
            Assert.Contains("low", content);
            Assert.Contains("close", content);
            Assert.Contains("volume", content);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
    
    [Fact]
    public async Task ChartDataExportService_ExportDataAsExcel_ShouldCreateValidFile()
    {
        // Arrange
        var service = new ChartDataExportService();
        var data = CreateTestData();
        var filePath = Path.GetTempFileName();
        var symbol = "TEST";
        
        try
        {
            // Act
            var result = await service.ExportDataAsExcelAsync(data, filePath, symbol);
            
            // Assert
            Assert.True(result);
            Assert.True(File.Exists(filePath));
            
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains($"Symbol,{symbol}", content);
            Assert.Contains("Export Date", content);
            Assert.Contains("Data Points", content);
            Assert.Contains("Change %", content);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
    
    [Fact]
    public async Task ChartDataExportService_ExportDataSummary_ShouldCreateValidFile()
    {
        // Arrange
        var service = new ChartDataExportService();
        var data = CreateTestData();
        var filePath = Path.GetTempFileName();
        var symbol = "TEST";
        
        try
        {
            // Act
            var result = await service.ExportDataSummaryAsync(data, filePath, symbol);
            
            // Assert
            Assert.True(result);
            Assert.True(File.Exists(filePath));
            
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("Chart Data Summary Report", content);
            Assert.Contains($"Symbol: {symbol}", content);
            Assert.Contains("Price Statistics", content);
            Assert.Contains("Volume Statistics", content);
            Assert.Contains("Recent Activity", content);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
    
    [Fact]
    public async Task ChartDataExportService_ExportEmptyData_ShouldHandleGracefully()
    {
        // Arrange
        var service = new ChartDataExportService();
        var emptyData = Enumerable.Empty<CandlestickData>();
        var filePath = Path.GetTempFileName();
        
        try
        {
            // Act
            var result = await service.ExportDataSummaryAsync(emptyData, filePath);
            
            // Assert
            Assert.True(result);
            Assert.True(File.Exists(filePath));
            
            var content = await File.ReadAllTextAsync(filePath);
            Assert.Contains("No data available", content);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
    
    private static CandlestickData[] CreateTestData()
    {
        var baseTime = DateTime.Now.Date;
        return new[]
        {
            new CandlestickData
            {
                Timestamp = baseTime,
                Open = 100.00m,
                High = 105.00m,
                Low = 98.00m,
                Close = 103.00m,
                Volume = 1000000
            },
            new CandlestickData
            {
                Timestamp = baseTime.AddDays(1),
                Open = 103.00m,
                High = 108.00m,
                Low = 101.00m,
                Close = 106.00m,
                Volume = 1200000
            },
            new CandlestickData
            {
                Timestamp = baseTime.AddDays(2),
                Open = 106.00m,
                High = 109.00m,
                Low = 104.00m,
                Close = 107.00m,
                Volume = 900000
            }
        };
    }
}
