using System.Windows.Input;

namespace ApexV2.UI.ContextMenus;

/// <summary>
/// Command implementations for context menu actions
/// </summary>
public static class ContextMenuCommands
{
    // Navigation commands
    public static readonly RoutedUICommand RenameTab = new("Rename Tab", "RenameTab", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand CloseTab = new("Close Tab", "CloseTab", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand DuplicateTab = new("Duplicate Tab", "DuplicateTab", typeof(ContextMenuCommands));

    // Panel commands
    public static readonly RoutedUICommand ClosePanel = new("Close Panel", "ClosePanel", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand ConfigurePanel = new("Configure Panel", "ConfigurePanel", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand BringToFront = new("Bring to Front", "BringToFront", typeof(ContextMenuCommands));

    // Workspace commands
    public static readonly RoutedUICommand AddChartPanel = new("Add Chart Panel", "AddChartPanel", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand AddWatchlistPanel = new("Add Watchlist Panel", "AddWatchlistPanel", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand AddNewsPanel = new("Add News Panel", "AddNewsPanel", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand PasteLayout = new("Paste Layout", "PasteLayout", typeof(ContextMenuCommands));

    // Symbol commands
    public static readonly RoutedUICommand OpenChart = new("Open Chart", "OpenChart", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand ViewFundamentals = new("View Fundamentals", "ViewFundamentals", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand AddToWatchlist = new("Add to Watchlist", "AddToWatchlist", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand CopySymbol = new("Copy Symbol", "CopySymbol", typeof(ContextMenuCommands));

    // Chart commands
    public static readonly RoutedUICommand AddIndicator = new("Add Indicator", "AddIndicator", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand DrawLine = new("Draw Line", "DrawLine", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand DrawRectangle = new("Draw Rectangle", "DrawRectangle", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand ClearDrawings = new("Clear Drawings", "ClearDrawings", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand ExportChart = new("Export Chart", "ExportChart", typeof(ContextMenuCommands));

    // Data grid commands
    public static readonly RoutedUICommand RefreshData = new("Refresh Data", "RefreshData", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand ExportData = new("Export Data", "ExportData", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand ConfigureColumns = new("Configure Columns", "ConfigureColumns", typeof(ContextMenuCommands));
    public static readonly RoutedUICommand SelectAll = new("Select All", "SelectAll", typeof(ContextMenuCommands));
}

/// <summary>
/// Base class for command handlers
/// </summary>
public abstract class ContextMenuCommandHandler
{
    protected readonly ApexV2.Core.Logging.Logger _logger;

    protected ContextMenuCommandHandler(ApexV2.Core.Logging.Logger logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Register command bindings with a UI element
    /// </summary>
    public abstract void RegisterCommands(System.Windows.FrameworkElement element);

    /// <summary>
    /// Helper method to create a command binding
    /// </summary>
    protected CommandBinding CreateBinding(RoutedUICommand command, ExecutedRoutedEventHandler executed, CanExecuteRoutedEventHandler? canExecute = null)
    {
        return new CommandBinding(command, executed, canExecute);
    }
}
