using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ApexV2.Charts.Models;
using ApexV2.Core.Logging;

namespace ApexV2.Charts.Export;

/// <summary>
/// Service for exporting chart data to various formats
/// </summary>
public class ChartDataExportService
{
    private readonly IChartLogger _logger;
    
    public ChartDataExportService(IChartLogger? logger = null)
    {
        _logger = logger ?? (App.LogManager != null ? new LoggerAdapter(App.LogManager.GetLogger("ChartDataExportService")) : new NullLogger());
    }
    
    /// <summary>
    /// Export chart data as CSV
    /// </summary>
    public async Task<bool> ExportDataAsCsvAsync(IEnumerable<CandlestickData> data, string filePath, bool includeHeaders = true)
    {
        try
        {
            _logger.Info($"Exporting chart data as CSV to: {filePath}");
            
            var csv = new StringBuilder();
            
            if (includeHeaders)
            {
                csv.AppendLine("Date,Time,Open,High,Low,Close,Volume");
            }
            
            foreach (var candle in data.OrderBy(d => d.Timestamp))
            {
                csv.AppendLine($"{candle.Timestamp:yyyy-MM-dd},{candle.Timestamp:HH:mm:ss},{candle.Open:F4},{candle.High:F4},{candle.Low:F4},{candle.Close:F4},{candle.Volume}");
            }
            
            await File.WriteAllTextAsync(filePath, csv.ToString());
            
            _logger.Info($"Chart data exported successfully as CSV: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to export chart data as CSV: {ex.Message}", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Export chart data as JSON
    /// </summary>
    public async Task<bool> ExportDataAsJsonAsync(IEnumerable<CandlestickData> data, string filePath, bool prettyFormat = true)
    {
        try
        {
            _logger.Info($"Exporting chart data as JSON to: {filePath}");
            
            var dataList = data.OrderBy(d => d.Timestamp).Select(d => new
            {
                timestamp = d.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                open = d.Open,
                high = d.High,
                low = d.Low,
                close = d.Close,
                volume = d.Volume
            }).ToList();
            
            var json = System.Text.Json.JsonSerializer.Serialize(dataList, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = prettyFormat,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            
            await File.WriteAllTextAsync(filePath, json);
            
            _logger.Info($"Chart data exported successfully as JSON: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to export chart data as JSON: {ex.Message}", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Export chart data as Excel-compatible format
    /// </summary>
    public async Task<bool> ExportDataAsExcelAsync(IEnumerable<CandlestickData> data, string filePath, string symbol = "UNKNOWN")
    {
        try
        {
            _logger.Info($"Exporting chart data as Excel to: {filePath}");
            
            var csv = new StringBuilder();
            
            // Add metadata
            csv.AppendLine($"Symbol,{symbol}");
            csv.AppendLine($"Export Date,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            csv.AppendLine($"Data Points,{data.Count()}");
            csv.AppendLine(); // Empty line
            
            // Headers
            csv.AppendLine("Date,Time,Open,High,Low,Close,Volume,Change,Change %");
            
            var dataArray = data.OrderBy(d => d.Timestamp).ToArray();
            
            for (int i = 0; i < dataArray.Length; i++)
            {
                var candle = dataArray[i];
                var change = i > 0 ? candle.Close - dataArray[i - 1].Close : 0;
                var changePercent = i > 0 && dataArray[i - 1].Close != 0 ? (change / dataArray[i - 1].Close) * 100 : 0;
                
                csv.AppendLine($"{candle.Timestamp:yyyy-MM-dd},{candle.Timestamp:HH:mm:ss},{candle.Open:F4},{candle.High:F4},{candle.Low:F4},{candle.Close:F4},{candle.Volume},{change:F4},{changePercent:F2}%");
            }
            
            await File.WriteAllTextAsync(filePath, csv.ToString());
            
            _logger.Info($"Chart data exported successfully as Excel: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to export chart data as Excel: {ex.Message}", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Export chart data summary statistics
    /// </summary>
    public async Task<bool> ExportDataSummaryAsync(IEnumerable<CandlestickData> data, string filePath, string symbol = "UNKNOWN")
    {
        try
        {
            _logger.Info($"Exporting chart data summary to: {filePath}");
            
            var dataArray = data.OrderBy(d => d.Timestamp).ToArray();
            if (!dataArray.Any())
            {
                await File.WriteAllTextAsync(filePath, "No data available for export.");
                return true;
            }
            
            var summary = new StringBuilder();
            
            // Basic info
            summary.AppendLine($"Chart Data Summary Report");
            summary.AppendLine($"========================");
            summary.AppendLine($"Symbol: {symbol}");
            summary.AppendLine($"Export Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            summary.AppendLine($"Data Range: {dataArray.First().Timestamp:yyyy-MM-dd} to {dataArray.Last().Timestamp:yyyy-MM-dd}");
            summary.AppendLine($"Total Data Points: {dataArray.Length}");
            summary.AppendLine();
            
            // Price statistics
            var prices = dataArray.Select(d => d.Close).ToArray();
            var highs = dataArray.Select(d => d.High).ToArray();
            var lows = dataArray.Select(d => d.Low).ToArray();
            var volumes = dataArray.Select(d => d.Volume).ToArray();
            
            summary.AppendLine($"Price Statistics");
            summary.AppendLine($"================");
            summary.AppendLine($"Highest Price: ${highs.Max():F4}");
            summary.AppendLine($"Lowest Price: ${lows.Min():F4}");
            summary.AppendLine($"Opening Price: ${dataArray.First().Open:F4}");
            summary.AppendLine($"Closing Price: ${dataArray.Last().Close:F4}");
            summary.AppendLine($"Average Price: ${prices.Average():F4}");
            summary.AppendLine($"Price Change: ${dataArray.Last().Close - dataArray.First().Open:F4} ({((dataArray.Last().Close - dataArray.First().Open) / dataArray.First().Open * 100):F2}%)");
            summary.AppendLine();
            
            // Volume statistics
            summary.AppendLine($"Volume Statistics");
            summary.AppendLine($"=================");
            summary.AppendLine($"Total Volume: {volumes.Sum():N0}");
            summary.AppendLine($"Average Volume: {volumes.Average():N0}");
            summary.AppendLine($"Highest Volume: {volumes.Max():N0}");
            summary.AppendLine($"Lowest Volume: {volumes.Min():N0}");
            summary.AppendLine();
            
            // Recent activity (last 10 data points)
            summary.AppendLine($"Recent Activity (Last 10 Data Points)");
            summary.AppendLine($"======================================");
            var recent = dataArray.TakeLast(10).ToArray();
            foreach (var candle in recent)
            {
                summary.AppendLine($"{candle.Timestamp:yyyy-MM-dd HH:mm}: O${candle.Open:F2} H${candle.High:F2} L${candle.Low:F2} C${candle.Close:F2} V{candle.Volume:N0}");
            }
            
            await File.WriteAllTextAsync(filePath, summary.ToString());
            
            _logger.Info($"Chart data summary exported successfully: {filePath}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to export chart data summary: {ex.Message}", ex);
            return false;
        }
    }
    
    /// <summary>
    /// Get supported data export formats
    /// </summary>
    public string[] GetSupportedDataFormats()
    {
        return new[] { "CSV", "JSON", "Excel (CSV)", "Summary (TXT)" };
    }
    
    /// <summary>
    /// Get file filter for data export dialog
    /// </summary>
    public string GetDataFileFilter()
    {
        return "CSV File (*.csv)|*.csv|JSON File (*.json)|*.json|Excel File (*.csv)|*.csv|Text Summary (*.txt)|*.txt|All Files (*.*)|*.*";
    }
}
