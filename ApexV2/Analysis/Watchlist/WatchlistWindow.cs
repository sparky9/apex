using ApexV2.Analysis.Watchlist;
using ApexV2.Charts.Export; // For IChartLogger
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ApexV2.Analysis.Watchlist;

/// <summary>
/// Window for managing watchlists - create, edit, delete, analyze
/// </summary>
public partial class WatchlistWindow : Window
{
    private readonly WatchlistService _watchlistService;
    private readonly WatchlistAnalysisService _analysisService;
    private readonly IChartLogger _logger;
    private WatchlistPanel _watchlistPanel;

    public WatchlistWindow(
        WatchlistService watchlistService,
        WatchlistAnalysisService analysisService,
        IChartLogger logger)
    {
        _watchlistService = watchlistService;
        _analysisService = analysisService;
        _logger = logger;

        // InitializeComponent(); // WPF initialization - manually done since no XAML
        Title = "Watchlist Manager";
        Width = 1200;
        Height = 800;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        
        Title = "APEX Watchlists";
        Width = 900;
        Height = 600;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        
        // Create and add the watchlist panel
        _watchlistPanel = new WatchlistPanel(_watchlistService, _analysisService, _logger);
        Content = _watchlistPanel;
        
        _logger.Info("Watchlist window opened");
    }

    protected override void OnClosed(EventArgs e)
    {
        _logger.Info("Watchlist window closed");
        base.OnClosed(e);
    }
}
