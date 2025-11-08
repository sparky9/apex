using System.Windows;
using System.Windows.Input;
using ApexV2.Core.Logging;
using ApexV2.UI.Navigation;

namespace ApexV2.UI.ContextMenus;

/// <summary>
/// Handles context menu commands for the main window
/// </summary>
public class MainWindowContextMenuHandler : ContextMenuCommandHandler
{
    private readonly NavigationService? _navigationService;
    private readonly UI.Layout.PanelHostControl? _panelHost;

    public MainWindowContextMenuHandler(Logger logger, NavigationService? navigationService = null, UI.Layout.PanelHostControl? panelHost = null) 
        : base(logger)
    {
        _navigationService = navigationService;
        _panelHost = panelHost;
    }

    public override void RegisterCommands(FrameworkElement element)
    {
        // Navigation commands
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.RenameTab, OnRenameTab, CanExecuteTabCommand));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.CloseTab, OnCloseTab, CanExecuteTabCommand));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.DuplicateTab, OnDuplicateTab, CanExecuteTabCommand));

        // Panel commands
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.ClosePanel, OnClosePanel, CanExecutePanelCommand));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.ConfigurePanel, OnConfigurePanel, CanExecutePanelCommand));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.BringToFront, OnBringToFront, CanExecutePanelCommand));

        // Workspace commands
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.AddChartPanel, OnAddChartPanel));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.AddWatchlistPanel, OnAddWatchlistPanel));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.AddNewsPanel, OnAddNewsPanel));

        // Symbol commands
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.OpenChart, OnOpenChart, CanExecuteSymbolCommand));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.ViewFundamentals, OnViewFundamentals, CanExecuteSymbolCommand));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.AddToWatchlist, OnAddToWatchlist, CanExecuteSymbolCommand));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.CopySymbol, OnCopySymbol, CanExecuteSymbolCommand));

        // Chart commands
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.AddIndicator, OnAddIndicator));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.DrawLine, OnDrawLine));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.DrawRectangle, OnDrawRectangle));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.ClearDrawings, OnClearDrawings));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.ExportChart, OnExportChart));

        // Data grid commands
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.RefreshData, OnRefreshData));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.ExportData, OnExportData));
        element.CommandBindings.Add(CreateBinding(ContextMenuCommands.ConfigureColumns, OnConfigureColumns));
    }

    #region Navigation Command Handlers

    private void OnRenameTab(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string pageName && _navigationService != null)
        {
            var newName = Microsoft.VisualBasic.Interaction.InputBox($"Enter new name for '{pageName}':", "Rename Page", pageName);
            if (!string.IsNullOrWhiteSpace(newName))
            {
                _navigationService.RenamePage(pageName, newName.Trim());
                _logger.Info($"Renamed page from '{pageName}' to '{newName}'");
            }
        }
    }

    private void OnCloseTab(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string pageName && _navigationService != null)
        {
            if (_navigationService.Pages.Count <= 1)
            {
                MessageBox.Show("At least one page must remain.", "Close Page", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            _ = _navigationService.RemovePageAsync(pageName);
            _logger.Info($"Closed page '{pageName}'");
        }
    }

    private void OnDuplicateTab(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string pageName && _navigationService != null)
        {
            var newName = $"{pageName} (Copy)";
            var counter = 1;
            while (_navigationService.Pages.Any(p => p.Name == newName))
            {
                newName = $"{pageName} (Copy {++counter})";
            }
            
            var currentPage = _navigationService.Pages.FirstOrDefault(p => p.Name == pageName);
            _ = _navigationService.AddPageAsync(newName, currentPage?.LayoutName);
            _logger.Info($"Duplicated page '{pageName}' as '{newName}'");
        }
    }

    private void CanExecuteTabCommand(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = e.Parameter is string && _navigationService != null;
    }

    #endregion

    #region Panel Command Handlers

    private void OnClosePanel(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string panelId && _panelHost != null)
        {
            _panelHost.RemovePanel(panelId);
            _logger.Info($"Closed panel '{panelId}'");
        }
    }

    private void OnConfigurePanel(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string panelId)
        {
            MessageBox.Show($"Panel configuration for '{panelId}' will be implemented with specific panel types", 
                           "Configure Panel", MessageBoxButton.OK, MessageBoxImage.Information);
            _logger.Info($"Configure panel requested for '{panelId}'");
        }
    }

    private void OnBringToFront(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string panelId && _panelHost != null)
        {
            // Implementation would bring panel to front in the layout
            _logger.Info($"Brought panel '{panelId}' to front");
        }
    }

    private void CanExecutePanelCommand(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = e.Parameter is string && _panelHost != null;
    }

    #endregion

    #region Workspace Command Handlers

    private void OnAddChartPanel(object sender, ExecutedRoutedEventArgs e)
    {
        _panelHost?.AddPanel("chart");
        _logger.Info("Added chart panel");
    }

    private void OnAddWatchlistPanel(object sender, ExecutedRoutedEventArgs e)
    {
        _panelHost?.AddPanel("watchlist");
        _logger.Info("Added watchlist panel");
    }

    private void OnAddNewsPanel(object sender, ExecutedRoutedEventArgs e)
    {
        _panelHost?.AddPanel("news");
        _logger.Info("Added news panel");
    }

    #endregion

    #region Symbol Command Handlers

    private void OnOpenChart(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string symbol)
        {
            MessageBox.Show($"Opening chart for {symbol} will be implemented with chart components", 
                           "Open Chart", MessageBoxButton.OK, MessageBoxImage.Information);
            _logger.Info($"Open chart requested for symbol '{symbol}'");
        }
    }

    private void OnViewFundamentals(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string symbol)
        {
            MessageBox.Show($"Viewing fundamentals for {symbol} will be implemented with fundamental data panels", 
                           "View Fundamentals", MessageBoxButton.OK, MessageBoxImage.Information);
            _logger.Info($"View fundamentals requested for symbol '{symbol}'");
        }
    }

    private void OnAddToWatchlist(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string symbol)
        {
            MessageBox.Show($"Adding {symbol} to watchlist will be implemented with watchlist components", 
                           "Add to Watchlist", MessageBoxButton.OK, MessageBoxImage.Information);
            _logger.Info($"Add to watchlist requested for symbol '{symbol}'");
        }
    }

    private void OnCopySymbol(object sender, ExecutedRoutedEventArgs e)
    {
        if (e.Parameter is string symbol)
        {
            Clipboard.SetText(symbol);
            _logger.Info($"Copied symbol '{symbol}' to clipboard");
        }
    }

    private void CanExecuteSymbolCommand(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = e.Parameter is string symbol && !string.IsNullOrWhiteSpace(symbol);
    }

    #endregion

    #region Chart Command Handlers

    private void OnAddIndicator(object sender, ExecutedRoutedEventArgs e)
    {
        MessageBox.Show("Add Indicator will be implemented with indicator components", 
                       "Add Indicator", MessageBoxButton.OK, MessageBoxImage.Information);
        _logger.Info("Add indicator requested");
    }

    private void OnDrawLine(object sender, ExecutedRoutedEventArgs e)
    {
        MessageBox.Show("Drawing tools will be implemented with chart drawing components", 
                       "Draw Line", MessageBoxButton.OK, MessageBoxImage.Information);
        _logger.Info("Draw line requested");
    }

    private void OnDrawRectangle(object sender, ExecutedRoutedEventArgs e)
    {
        MessageBox.Show("Drawing tools will be implemented with chart drawing components", 
                       "Draw Rectangle", MessageBoxButton.OK, MessageBoxImage.Information);
        _logger.Info("Draw rectangle requested");
    }

    private void OnClearDrawings(object sender, ExecutedRoutedEventArgs e)
    {
        MessageBox.Show("Clear drawings will be implemented with chart drawing components", 
                       "Clear Drawings", MessageBoxButton.OK, MessageBoxImage.Information);
        _logger.Info("Clear drawings requested");
    }

    private void OnExportChart(object sender, ExecutedRoutedEventArgs e)
    {
        MessageBox.Show("Chart export will be implemented with chart export components", 
                       "Export Chart", MessageBoxButton.OK, MessageBoxImage.Information);
        _logger.Info("Export chart requested");
    }

    #endregion

    #region Data Grid Command Handlers

    private void OnRefreshData(object sender, ExecutedRoutedEventArgs e)
    {
        MessageBox.Show("Data refresh will be implemented with specific data panel types", 
                       "Refresh Data", MessageBoxButton.OK, MessageBoxImage.Information);
        _logger.Info("Refresh data requested");
    }

    private void OnExportData(object sender, ExecutedRoutedEventArgs e)
    {
        MessageBox.Show("Data export will be implemented with data export components", 
                       "Export Data", MessageBoxButton.OK, MessageBoxImage.Information);
        _logger.Info("Export data requested");
    }

    private void OnConfigureColumns(object sender, ExecutedRoutedEventArgs e)
    {
        MessageBox.Show("Column configuration will be implemented with data grid components", 
                       "Configure Columns", MessageBoxButton.OK, MessageBoxImage.Information);
        _logger.Info("Configure columns requested");
    }

    #endregion
}
