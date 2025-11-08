using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading.Tasks;
using ApexV2.Core.Logging;

namespace ApexV2.Charts.Export;

/// <summary>
/// Service for exporting chart data and images
/// </summary>
public class ChartExportService
{
    private readonly IChartLogger _logger;
    
    public ChartExportService(IChartLogger? logger = null)
    {
        _logger = logger ?? (App.LogManager != null ? new LoggerAdapter(App.LogManager.GetLogger("ChartExportService")) : new NullLogger());
    }
    
    /// <summary>
    /// Export chart as PNG image
    /// </summary>
    public async Task<bool> ExportChartAsPngAsync(FrameworkElement chartElement, string filePath, int width = 1920, int height = 1080)
    {
        try
        {
            _logger.Info($"Exporting chart as PNG to: {filePath}");
            
            // Create render target bitmap
            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            
            // Scale the element to fit the desired size
            var originalWidth = chartElement.ActualWidth;
            var originalHeight = chartElement.ActualHeight;
            
            if (originalWidth > 0 && originalHeight > 0)
            {
                var scaleX = width / originalWidth;
                var scaleY = height / originalHeight;
                var scale = Math.Min(scaleX, scaleY);
                
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    var visualBrush = new VisualBrush(chartElement);
                    drawingContext.PushTransform(new ScaleTransform(scale, scale));
                    drawingContext.DrawRectangle(visualBrush, null, new Rect(0, 0, originalWidth, originalHeight));
                }
                
                renderTarget.Render(drawingVisual);
            }
            else
            {
                renderTarget.Render(chartElement);
            }
            
            // Create PNG encoder
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
            
            // Save to file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
            
            _logger.Info($"Chart exported successfully as PNG: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to export chart as PNG: {ex.Message}", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Export chart as JPEG image
    /// </summary>
    public async Task<bool> ExportChartAsJpegAsync(FrameworkElement chartElement, string filePath, int width = 1920, int height = 1080, int quality = 90)
    {
        try
        {
            _logger.Info($"Exporting chart as JPEG to: {filePath}");
            
            // Create render target bitmap
            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(chartElement);
            
            // Create JPEG encoder
            var encoder = new JpegBitmapEncoder();
            encoder.QualityLevel = quality;
            encoder.Frames.Add(BitmapFrame.Create(renderTarget));
            
            // Save to file
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                encoder.Save(fileStream);
            }
            
            _logger.Info($"Chart exported successfully as JPEG: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to export chart as JPEG: {ex.Message}", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Copy chart to clipboard as image
    /// </summary>
    public bool CopyChartToClipboard(FrameworkElement chartElement, int width = 1920, int height = 1080)
    {
        try
        {
            _logger.Info("Copying chart to clipboard");
            
            // Create render target bitmap
            var renderTarget = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            renderTarget.Render(chartElement);
            
            // Copy to clipboard
            Clipboard.SetImage(renderTarget);
            
            _logger.Info("Chart copied to clipboard successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to copy chart to clipboard: {ex.Message}", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Get supported export formats
    /// </summary>
    public string[] GetSupportedFormats()
    {
        return new[] { "PNG", "JPEG", "BMP", "GIF", "TIFF" };
    }
    
    /// <summary>
    /// Get file filter for save dialog
    /// </summary>
    public string GetFileFilter()
    {
        return "PNG Image (*.png)|*.png|JPEG Image (*.jpg)|*.jpg|Bitmap Image (*.bmp)|*.bmp|GIF Image (*.gif)|*.gif|TIFF Image (*.tiff)|*.tiff|All Files (*.*)|*.*";
    }
    
    /// <summary>
    /// Validate export parameters
    /// </summary>
    public bool ValidateExportParameters(string filePath, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            _logger.Warn("Export file path is empty");
            return false;
        }
        
        if (width <= 0 || height <= 0)
        {
            _logger.Warn($"Invalid export dimensions: {width}x{height}");
            return false;
        }
        
        if (width > 10000 || height > 10000)
        {
            _logger.Warn($"Export dimensions too large: {width}x{height}");
            return false;
        }
        
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Cannot create export directory: {ex.Message}", ex);
            return false;
        }
        
        return true;
    }
}
