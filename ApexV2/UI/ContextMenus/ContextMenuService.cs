using System.Windows;
using System.Windows.Controls;
using ApexV2.Core.Logging;

namespace ApexV2.UI.ContextMenus;

/// <summary>
/// Service for creating and managing context menus throughout the application
/// </summary>
public class ContextMenuService
{
    private readonly Logger _logger;

    public ContextMenuService(Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates a context menu for navigation tab items
    /// </summary>
    public ContextMenu CreateNavigationTabMenu(string pageName, Action<string> onRename, Action<string> onClose, Action<string> onDuplicate)
    {
        var menu = new ContextMenu();
        
        var renameItem = new MenuItem { Header = "Rename Page" };
        renameItem.Click += (_, __) => onRename(pageName);
        menu.Items.Add(renameItem);
        
        var duplicateItem = new MenuItem { Header = "Duplicate Page" };
        duplicateItem.Click += (_, __) => onDuplicate(pageName);
        menu.Items.Add(duplicateItem);
        
        menu.Items.Add(new Separator());
        
        var closeItem = new MenuItem { Header = "Close Page" };
        closeItem.Click += (_, __) => onClose(pageName);
        menu.Items.Add(closeItem);
        
        return menu;
    }

    /// <summary>
    /// Creates a context menu for layout panels
    /// </summary>
    public ContextMenu CreatePanelMenu(string panelId, string panelType, Action<string> onClosePanel, Action<string> onConfigurePanel, Action<string> onBringToFront)
    {
        var menu = new ContextMenu();
        
        var configureItem = new MenuItem { Header = $"Configure {panelType}" };
        configureItem.Click += (_, __) => onConfigurePanel(panelId);
        menu.Items.Add(configureItem);
        
        var bringToFrontItem = new MenuItem { Header = "Bring to Front" };
        bringToFrontItem.Click += (_, __) => onBringToFront(panelId);
        menu.Items.Add(bringToFrontItem);
        
        menu.Items.Add(new Separator());
        
        var closeItem = new MenuItem { Header = "Close Panel" };
        closeItem.Click += (_, __) => onClosePanel(panelId);
        menu.Items.Add(closeItem);
        
        return menu;
    }

    /// <summary>
    /// Creates a context menu for the main workspace area
    /// </summary>
    public ContextMenu CreateWorkspaceMenu(Action onAddChart, Action onAddWatchlist, Action onAddNews, Action onPasteLayout)
    {
        var menu = new ContextMenu();
        
        var addSubmenu = new MenuItem { Header = "Add Panel" };
        
        var addChartItem = new MenuItem { Header = "Chart Panel" };
        addChartItem.Click += (_, __) => onAddChart();
        addSubmenu.Items.Add(addChartItem);
        
        var addWatchlistItem = new MenuItem { Header = "Watchlist Panel" };
        addWatchlistItem.Click += (_, __) => onAddWatchlist();
        addSubmenu.Items.Add(addWatchlistItem);
        
        var addNewsItem = new MenuItem { Header = "News Panel" };
        addNewsItem.Click += (_, __) => onAddNews();
        addSubmenu.Items.Add(addNewsItem);
        
        menu.Items.Add(addSubmenu);
        
        menu.Items.Add(new Separator());
        
        var pasteItem = new MenuItem { Header = "Paste Layout" };
        pasteItem.Click += (_, __) => onPasteLayout();
        menu.Items.Add(pasteItem);
        
        return menu;
    }

    /// <summary>
    /// Creates a context menu for symbol/stock items (watchlists, search results, etc.)
    /// </summary>
    public ContextMenu CreateSymbolMenu(string symbol, Action<string> onAddToWatchlist, Action<string> onOpenChart, Action<string> onViewFundamentals, Action<string> onCopySymbol)
    {
        var menu = new ContextMenu();
        
        var openChartItem = new MenuItem { Header = $"Open Chart for {symbol}" };
        openChartItem.Click += (_, __) => onOpenChart(symbol);
        menu.Items.Add(openChartItem);
        
        var fundamentalsItem = new MenuItem { Header = "View Fundamentals" };
        fundamentalsItem.Click += (_, __) => onViewFundamentals(symbol);
        menu.Items.Add(fundamentalsItem);
        
        menu.Items.Add(new Separator());
        
        var addToWatchlistItem = new MenuItem { Header = "Add to Watchlist" };
        addToWatchlistItem.Click += (_, __) => onAddToWatchlist(symbol);
        menu.Items.Add(addToWatchlistItem);
        
        menu.Items.Add(new Separator());
        
        var copyItem = new MenuItem { Header = "Copy Symbol" };
        copyItem.Click += (_, __) => onCopySymbol(symbol);
        menu.Items.Add(copyItem);
        
        return menu;
    }

    /// <summary>
    /// Creates a context menu for chart areas
    /// </summary>
    public ContextMenu CreateChartMenu(Action onAddIndicator, Action onDrawLine, Action onDrawRectangle, Action onClearDrawings, Action onExportChart)
    {
        var menu = new ContextMenu();
        
        var indicatorItem = new MenuItem { Header = "Add Indicator..." };
        indicatorItem.Click += (_, __) => onAddIndicator();
        menu.Items.Add(indicatorItem);
        
        menu.Items.Add(new Separator());
        
        var drawingSubmenu = new MenuItem { Header = "Drawing Tools" };
        
        var lineItem = new MenuItem { Header = "Draw Line" };
        lineItem.Click += (_, __) => onDrawLine();
        drawingSubmenu.Items.Add(lineItem);
        
        var rectItem = new MenuItem { Header = "Draw Rectangle" };
        rectItem.Click += (_, __) => onDrawRectangle();
        drawingSubmenu.Items.Add(rectItem);
        
        drawingSubmenu.Items.Add(new Separator());
        
        var clearItem = new MenuItem { Header = "Clear All Drawings" };
        clearItem.Click += (_, __) => onClearDrawings();
        drawingSubmenu.Items.Add(clearItem);
        
        menu.Items.Add(drawingSubmenu);
        
        menu.Items.Add(new Separator());
        
        var exportItem = new MenuItem { Header = "Export Chart..." };
        exportItem.Click += (_, __) => onExportChart();
        menu.Items.Add(exportItem);
        
        return menu;
    }

    /// <summary>
    /// Creates a context menu for data grids (watchlists, order history, etc.)
    /// </summary>
    public ContextMenu CreateDataGridMenu(Action onRefresh, Action onExportData, Action onConfigureColumns, Action onSelectAll)
    {
        var menu = new ContextMenu();
        
        var refreshItem = new MenuItem { Header = "Refresh Data" };
        refreshItem.Click += (_, __) => onRefresh();
        menu.Items.Add(refreshItem);
        
        menu.Items.Add(new Separator());
        
        var selectAllItem = new MenuItem { Header = "Select All" };
        selectAllItem.Click += (_, __) => onSelectAll();
        menu.Items.Add(selectAllItem);
        
        var configureItem = new MenuItem { Header = "Configure Columns..." };
        configureItem.Click += (_, __) => onConfigureColumns();
        menu.Items.Add(configureItem);
        
        menu.Items.Add(new Separator());
        
        var exportItem = new MenuItem { Header = "Export Data..." };
        exportItem.Click += (_, __) => onExportData();
        menu.Items.Add(exportItem);
        
        return menu;
    }
}
