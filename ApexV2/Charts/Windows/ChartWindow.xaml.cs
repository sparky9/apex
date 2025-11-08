using System.Windows;
using ApexV2.Core.Logging;

namespace ApexV2.Charts.Windows;

/// <summary>
/// Standalone chart window for displaying individual symbol charts
/// </summary>
public partial class ChartWindow : Window
{
    private readonly Logger _logger;

    public ChartPanel ChartPanel => MainChartPanel;

    public ChartWindow()
    {
        InitializeComponent();
        
        _logger = App.LogManager.GetLogger("ChartWindow");

        SetupEventHandlers();
    }

    public ChartWindow(string symbol) : this()
    {
        MainChartPanel.SetSymbol(symbol);
        UpdateTitle(symbol);
    }

    private void SetupEventHandlers()
    {
        MainChartPanel.SymbolChanged += OnSymbolChanged;
        
        Loaded += ChartWindow_Loaded;
        Closing += ChartWindow_Closing;
    }

    private void UpdateTitle(string symbol)
    {
        Title = $"APEX Chart - {symbol}";
    }

    private void OnSymbolChanged(object? sender, string symbol)
    {
        UpdateTitle(symbol);
    }

    private void ChartWindow_Loaded(object sender, RoutedEventArgs e)
    {
        _logger.Info($"Chart window loaded for symbol: {MainChartPanel.CurrentSymbol}");
    }

    private void ChartWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _logger.Info($"Chart window closing for symbol: {MainChartPanel.CurrentSymbol}");
    }

    /// <summary>
    /// Sets the chart symbol and refreshes data
    /// </summary>
    public async Task SetSymbolAsync(string symbol)
    {
        MainChartPanel.SetSymbol(symbol);
        UpdateTitle(symbol);
        await MainChartPanel.RefreshAsync();
    }
}
