using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using ApexV2.Charts.Export;

namespace ApexV2.Tests.Charts.Export;

public class ChartExportServiceTests
{
    [Fact]
    public void ChartExportService_GetSupportedFormats_ShouldReturnValidFormats()
    {
        // Arrange
        var service = new ChartExportService();
        
        // Act
        var formats = service.GetSupportedFormats();
        
        // Assert
        Assert.NotNull(formats);
        Assert.Contains("PNG", formats);
        Assert.Contains("JPEG", formats);
        Assert.Contains("BMP", formats);
        Assert.Contains("GIF", formats);
        Assert.Contains("TIFF", formats);
    }
    
    [Fact]
    public void ChartExportService_GetFileFilter_ShouldReturnValidFilter()
    {
        // Arrange
        var service = new ChartExportService();
        
        // Act
        var filter = service.GetFileFilter();
        
        // Assert
        Assert.NotNull(filter);
        Assert.Contains("*.png", filter);
        Assert.Contains("*.jpg", filter);
        Assert.Contains("*.bmp", filter);
    }
    
    [Theory]
    [InlineData("", 1920, 1080, false)] // Empty path
    [InlineData("test.png", 0, 1080, false)] // Invalid width
    [InlineData("test.png", 1920, 0, false)] // Invalid height
    [InlineData("test.png", 20000, 1080, false)] // Width too large
    [InlineData("test.png", 1920, 20000, false)] // Height too large
    [InlineData("test.png", 1920, 1080, true)] // Valid parameters
    public void ChartExportService_ValidateExportParameters_ShouldReturnExpectedResult(
        string filePath, int width, int height, bool expected)
    {
        // Arrange
        var service = new ChartExportService();
        
        // Act
        var result = service.ValidateExportParameters(filePath, width, height);
        
        // Assert
        Assert.Equal(expected, result);
    }
    
    [Fact]
    public void ChartExportService_ValidateExportParameters_ShouldCreateDirectory()
    {
        // Arrange
        var service = new ChartExportService();
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var filePath = Path.Combine(tempDir, "test.png");
        
        try
        {
            // Act
            var result = service.ValidateExportParameters(filePath, 1920, 1080);
            
            // Assert
            Assert.True(result);
            Assert.True(Directory.Exists(tempDir));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
