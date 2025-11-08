using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ApexV2.Charts.Export;
using ApexV2.Charts.Controls;
using ApexV2.Core.Logging;

namespace ApexV2.Charts.Windows;

/// <summary>
/// Dialog for exporting charts as images or data
/// </summary>
public partial class ChartExportDialog : Window
{
    private readonly Logger _logger;
    private readonly ChartExportService _exportService;
    private readonly ChartDataExportService _dataExportService;
    private readonly ChartControl _chartControl;
    
    public ChartExportDialog(ChartControl chartControl)
    {
        InitializeComponent();
        
        _logger = App.LogManager.GetLogger("ChartExportDialog");
        _exportService = new ChartExportService();
        _dataExportService = new ChartDataExportService();
        _chartControl = chartControl ?? throw new ArgumentNullException(nameof(chartControl));
        
        InitializeDialog();
    }
    
    private void InitializeDialog()
    {
        // Set default values
        WidthTextBox.Text = "1920";
        HeightTextBox.Text = "1080";
        QualitySlider.Value = 90;
        
        // Set symbol name
        SymbolTextBlock.Text = _chartControl.CurrentSymbol ?? "Unknown";
        
        // Update quality label
        UpdateQualityLabel();
    }
    
    private void ExportImageButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Chart as Image",
                Filter = _exportService.GetFileFilter(),
                DefaultExt = "png",
                FileName = $"{_chartControl.CurrentSymbol}_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            
            if (saveDialog.ShowDialog() == true)
            {
                ExportImage(saveDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error in export image dialog: {ex.Message}", ex);
            MessageBox.Show($"Failed to open export dialog: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void ExportDataButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var saveDialog = new SaveFileDialog
            {
                Title = "Export Chart Data",
                Filter = _dataExportService.GetDataFileFilter(),
                DefaultExt = "csv",
                FileName = $"{_chartControl.CurrentSymbol}_data_{DateTime.Now:yyyyMMdd_HHmmss}"
            };
            
            if (saveDialog.ShowDialog() == true)
            {
                ExportData(saveDialog.FileName);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error in export data dialog: {ex.Message}", ex);
            MessageBox.Show($"Failed to open export dialog: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void CopyToClipboardButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!int.TryParse(WidthTextBox.Text, out int width) || !int.TryParse(HeightTextBox.Text, out int height))
            {
                MessageBox.Show("Please enter valid width and height values.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            StatusTextBlock.Text = "Copying to clipboard...";
            StatusTextBlock.Visibility = Visibility.Visible;
            
            var success = _exportService.CopyChartToClipboard(_chartControl, width, height);
            
            if (success)
            {
                StatusTextBlock.Text = "Chart copied to clipboard successfully!";
                _logger.Info("Chart copied to clipboard successfully");
            }
            else
            {
                StatusTextBlock.Text = "Failed to copy chart to clipboard.";
                MessageBox.Show("Failed to copy chart to clipboard.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error copying chart to clipboard: {ex.Message}", ex);
            StatusTextBlock.Text = "Error copying to clipboard.";
            MessageBox.Show($"Failed to copy chart to clipboard: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void ExportImage(string filePath)
    {
        try
        {
            if (!int.TryParse(WidthTextBox.Text, out int width) || !int.TryParse(HeightTextBox.Text, out int height))
            {
                MessageBox.Show("Please enter valid width and height values.", "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!_exportService.ValidateExportParameters(filePath, width, height))
            {
                MessageBox.Show("Invalid export parameters.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            StatusTextBlock.Text = "Exporting image...";
            StatusTextBlock.Visibility = Visibility.Visible;
            
            bool success = false;
            var extension = Path.GetExtension(filePath).ToLower();
            
            switch (extension)
            {
                case ".png":
                    success = await _exportService.ExportChartAsPngAsync(_chartControl, filePath, width, height);
                    break;
                case ".jpg":
                case ".jpeg":
                    var quality = (int)QualitySlider.Value;
                    success = await _exportService.ExportChartAsJpegAsync(_chartControl, filePath, width, height, quality);
                    break;
                default:
                    success = await _exportService.ExportChartAsPngAsync(_chartControl, filePath, width, height);
                    break;
            }
            
            if (success)
            {
                StatusTextBlock.Text = $"Image exported successfully: {Path.GetFileName(filePath)}";
                _logger.Info($"Chart image exported successfully: {filePath}");
            }
            else
            {
                StatusTextBlock.Text = "Failed to export image.";
                MessageBox.Show("Failed to export chart image.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error exporting chart image: {ex.Message}", ex);
            StatusTextBlock.Text = "Error exporting image.";
            MessageBox.Show($"Failed to export chart image: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private async void ExportData(string filePath)
    {
        try
        {
            var chartData = _chartControl.Data;
            if (chartData == null || !chartData.Any())
            {
                MessageBox.Show("No chart data available for export.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            StatusTextBlock.Text = "Exporting data...";
            StatusTextBlock.Visibility = Visibility.Visible;
            
            bool success = false;
            var extension = Path.GetExtension(filePath).ToLower();
            var symbol = _chartControl.CurrentSymbol ?? "UNKNOWN";
            
            switch (extension)
            {
                case ".csv":
                    if (filePath.Contains("_data_"))
                        success = await _dataExportService.ExportDataAsCsvAsync(chartData, filePath);
                    else
                        success = await _dataExportService.ExportDataAsExcelAsync(chartData, filePath, symbol);
                    break;
                case ".json":
                    success = await _dataExportService.ExportDataAsJsonAsync(chartData, filePath);
                    break;
                case ".txt":
                    success = await _dataExportService.ExportDataSummaryAsync(chartData, filePath, symbol);
                    break;
                default:
                    success = await _dataExportService.ExportDataAsCsvAsync(chartData, filePath);
                    break;
            }
            
            if (success)
            {
                StatusTextBlock.Text = $"Data exported successfully: {Path.GetFileName(filePath)}";
                _logger.Info($"Chart data exported successfully: {filePath}");
            }
            else
            {
                StatusTextBlock.Text = "Failed to export data.";
                MessageBox.Show("Failed to export chart data.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error exporting chart data: {ex.Message}", ex);
            StatusTextBlock.Text = "Error exporting data.";
            MessageBox.Show($"Failed to export chart data: {ex.Message}", "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateQualityLabel();
    }
    
    private void UpdateQualityLabel()
    {
        if (QualityLabel != null)
        {
            QualityLabel.Content = $"Quality: {(int)QualitySlider.Value}%";
        }
    }
    
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
