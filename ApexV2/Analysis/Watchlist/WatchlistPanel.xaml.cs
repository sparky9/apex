using ApexV2.Analysis.Watchlist;
using ApexV2.Charts.Windows;
using ApexV2.Charts.Export; // For IChartLogger
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ApexV2.Analysis.Watchlist;

/// <summary>
/// Professional watchlist panel for managing and viewing symbol lists
/// </summary>
public partial class WatchlistPanel : UserControl, INotifyPropertyChanged
{
    private readonly WatchlistService _watchlistService;
    private readonly WatchlistAnalysisService _analysisService;
    private readonly IChartLogger _logger;
    
    private ObservableCollection<WatchlistViewModel> _watchlists;
    private ObservableCollection<WatchlistItemViewModel> _filteredItems;
    private WatchlistViewModel? _currentWatchlist;
    private string _searchText = string.Empty;
    
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WatchlistViewModel> Watchlists
    {
        get => _watchlists;
        set => SetProperty(ref _watchlists, value);
    }

    public ObservableCollection<WatchlistItemViewModel> FilteredItems
    {
        get => _filteredItems;
        set => SetProperty(ref _filteredItems, value);
    }

    public WatchlistViewModel? CurrentWatchlist
    {
        get => _currentWatchlist;
        set => SetProperty(ref _currentWatchlist, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                FilterItems();
            }
        }
    }

    public WatchlistPanel(
        WatchlistService watchlistService,
        WatchlistAnalysisService analysisService,
        IChartLogger logger)
    {
        _watchlistService = watchlistService;
        _analysisService = analysisService;
        _logger = logger;
        
        _watchlists = new ObservableCollection<WatchlistViewModel>();
        _filteredItems = new ObservableCollection<WatchlistItemViewModel>();
        
        InitializeComponent();
        
        DataContext = this;
        WatchlistGrid.ItemsSource = FilteredItems;
        
        // Subscribe to watchlist events
        _watchlistService.WatchlistChanged += OnWatchlistChanged;
        _watchlistService.WatchlistItemUpdated += OnWatchlistItemUpdated;
        
        Loaded += WatchlistPanel_Loaded;
    }

    private async void WatchlistPanel_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await LoadWatchlistsAsync();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error loading watchlists: {ex.Message}");
            UpdateStatus($"Error loading watchlists: {ex.Message}");
        }
    }

    #region Event Handlers

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            RefreshButton.IsEnabled = false;
            UpdateStatus("Refreshing data...");
            
            await _watchlistService.RefreshAllDataAsync();
            await UpdateCurrentWatchlistAsync();
            
            UpdateStatus("Data refreshed");
            UpdateLastUpdateTime();
        }
        catch (Exception ex)
        {
            _logger.Error($"Error refreshing data: {ex.Message}");
            UpdateStatus($"Refresh failed: {ex.Message}");
        }
        finally
        {
            RefreshButton.IsEnabled = true;
        }
    }

    private async void AddSymbolButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentWatchlist == null)
        {
            MessageBox.Show("Please select a watchlist first.", "No Watchlist Selected", 
                          MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new AddSymbolDialog();
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.Symbol))
        {
            try
            {
                UpdateStatus($"Adding symbol {dialog.Symbol}...");
                var success = await _watchlistService.AddSymbolAsync(CurrentWatchlist.Id, dialog.Symbol);
                
                if (success)
                {
                    UpdateStatus($"Added {dialog.Symbol} to watchlist");
                    await UpdateCurrentWatchlistAsync();
                }
                else
                {
                    UpdateStatus($"Failed to add {dialog.Symbol} - symbol may already exist");
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error adding symbol: {ex.Message}");
                UpdateStatus($"Error adding symbol: {ex.Message}");
            }
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        // TODO [REVIEWED]: Implement watchlist settings dialog
        MessageBox.Show("Watchlist settings will be implemented in a future update.", 
                       "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void NewWatchlistButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewWatchlistDialog();
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.WatchlistName))
        {
            try
            {
                UpdateStatus($"Creating watchlist '{dialog.WatchlistName}'...");
                var newWatchlist = await _watchlistService.CreateWatchlistAsync(dialog.WatchlistName);
                
                Watchlists.Add(newWatchlist);
                WatchlistSelector.SelectedItem = newWatchlist;
                
                UpdateStatus($"Created watchlist '{dialog.WatchlistName}'");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error creating watchlist: {ex.Message}");
                UpdateStatus($"Error creating watchlist: {ex.Message}");
                MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void DeleteWatchlistButton_Click(object sender, RoutedEventArgs e)
    {
        if (CurrentWatchlist == null) return;

        var result = MessageBox.Show(
            $"Are you sure you want to delete the watchlist '{CurrentWatchlist.Name}'?",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result == MessageBoxResult.Yes)
        {
            try
            {
                UpdateStatus($"Deleting watchlist '{CurrentWatchlist.Name}'...");
                var success = await _watchlistService.DeleteWatchlistAsync(CurrentWatchlist.Id);
                
                if (success)
                {
                    Watchlists.Remove(CurrentWatchlist);
                    CurrentWatchlist = null;
                    FilteredItems.Clear();
                    
                    UpdateStatus("Watchlist deleted");
                    UpdateWatchlistInfo();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error deleting watchlist: {ex.Message}");
                UpdateStatus($"Error deleting watchlist: {ex.Message}");
            }
        }
    }

    private async void WatchlistSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WatchlistSelector.SelectedItem is WatchlistViewModel selectedWatchlist)
        {
            CurrentWatchlist = selectedWatchlist;
            await UpdateCurrentWatchlistAsync();
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        FilterItems();
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        FilterItems();
    }

    private void SortOrderChanged(object sender, RoutedEventArgs e)
    {
        FilterItems();
    }

    private void WatchlistGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (WatchlistGrid.SelectedItem is WatchlistItemViewModel item)
        {
            OpenChartForSymbol(item.Symbol);
        }
    }

    private void OpenChart_Click(object sender, RoutedEventArgs e)
    {
        if (WatchlistGrid.SelectedItem is WatchlistItemViewModel item)
        {
            OpenChartForSymbol(item.Symbol);
        }
    }

    private void ViewFundamentals_Click(object sender, RoutedEventArgs e)
    {
        if (WatchlistGrid.SelectedItem is WatchlistItemViewModel item)
        {
            // TODO [REVIEWED]: Implement fundamentals view
            MessageBox.Show($"Fundamentals for {item.Symbol} will be available when Component #10 (Fundamental Data Engine) is implemented.",
                           "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private async void RemoveSymbol_Click(object sender, RoutedEventArgs e)
    {
        if (WatchlistGrid.SelectedItem is WatchlistItemViewModel item && CurrentWatchlist != null)
        {
            try
            {
                UpdateStatus($"Removing {item.Symbol}...");
                var success = await _watchlistService.RemoveSymbolAsync(CurrentWatchlist.Id, item.Symbol);
                
                if (success)
                {
                    FilteredItems.Remove(item);
                    CurrentWatchlist.Items.Remove(item);
                    UpdateStatus($"Removed {item.Symbol}");
                    UpdateWatchlistInfo();
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error removing symbol: {ex.Message}");
                UpdateStatus($"Error removing symbol: {ex.Message}");
            }
        }
    }

    private void CopySymbol_Click(object sender, RoutedEventArgs e)
    {
        if (WatchlistGrid.SelectedItem is WatchlistItemViewModel item)
        {
            Clipboard.SetText(item.Symbol);
            UpdateStatus($"Copied {item.Symbol} to clipboard");
        }
    }

    #endregion

    #region Private Methods

    private async Task LoadWatchlistsAsync()
    {
        UpdateStatus("Loading watchlists...");
        
        var watchlists = await _watchlistService.GetAllWatchlistsAsync();
        
        Watchlists.Clear();
        foreach (var watchlist in watchlists)
        {
            Watchlists.Add(watchlist);
        }
        
        WatchlistSelector.ItemsSource = Watchlists;
        WatchlistSelector.DisplayMemberPath = nameof(WatchlistViewModel.Name);
        
        if (Watchlists.Any())
        {
            WatchlistSelector.SelectedIndex = 0;
        }
        
        UpdateStatus($"Loaded {Watchlists.Count} watchlists");
    }

    private async Task UpdateCurrentWatchlistAsync()
    {
        if (CurrentWatchlist == null)
        {
            FilteredItems.Clear();
            UpdateWatchlistInfo();
            return;
        }

        try
        {
            // Refresh the watchlist data
            var refreshedWatchlist = await _watchlistService.GetWatchlistAsync(CurrentWatchlist.Id);
            if (refreshedWatchlist != null)
            {
                CurrentWatchlist = refreshedWatchlist;
                FilterItems();
                UpdateWatchlistInfo();
            }
        }
        catch (Exception ex)
        {
            _logger.Error($"Error updating watchlist: {ex.Message}");
            UpdateStatus($"Error updating watchlist: {ex.Message}");
        }
    }

    private void FilterItems()
    {
        if (CurrentWatchlist == null)
        {
            FilteredItems.Clear();
            return;
        }

        var items = CurrentWatchlist.Items.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            items = items.Where(item => 
                item.Symbol.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Apply sorting
        if (SortComboBox.SelectedItem is ComboBoxItem sortItem && sortItem.Tag is string sortProperty)
        {
            var descending = SortDescendingCheckBox.IsChecked == true;
            
            items = sortProperty switch
            {
                "Symbol" => descending ? items.OrderByDescending(i => i.Symbol) : items.OrderBy(i => i.Symbol),
                "LastPrice" => descending ? items.OrderByDescending(i => i.LastPrice) : items.OrderBy(i => i.LastPrice),
                "Change" => descending ? items.OrderByDescending(i => i.Change) : items.OrderBy(i => i.Change),
                "ChangePercent" => descending ? items.OrderByDescending(i => i.ChangePercent) : items.OrderBy(i => i.ChangePercent),
                "Volume" => descending ? items.OrderByDescending(i => i.Volume) : items.OrderBy(i => i.Volume),
                _ => items
            };
        }

        FilteredItems.Clear();
        foreach (var item in items)
        {
            FilteredItems.Add(item);
        }
    }

    private void UpdateWatchlistInfo()
    {
        if (CurrentWatchlist != null)
        {
            WatchlistTitle.Text = CurrentWatchlist.Name;
            ItemCount.Text = $"({CurrentWatchlist.ItemCount} items)";
            
            var totalValue = CurrentWatchlist.TotalValue;
            var totalGainLoss = CurrentWatchlist.TotalGainLoss;
            
            TotalValueText.Text = $"Total: {totalValue:C2}";
            GainLossText.Text = $"P&L: {(totalGainLoss >= 0 ? "+" : "")}{totalGainLoss:C2}";
            GainLossText.Foreground = totalGainLoss >= 0 ? 
                System.Windows.Media.Brushes.LightGreen : 
                System.Windows.Media.Brushes.LightCoral;
        }
        else
        {
            WatchlistTitle.Text = "No Watchlist Selected";
            ItemCount.Text = "";
            TotalValueText.Text = "Total: $0.00";
            GainLossText.Text = "P&L: $0.00";
            GainLossText.Foreground = System.Windows.Media.Brushes.Gray;
        }
    }

    private void UpdateStatus(string message)
    {
        StatusText.Text = message;
    }

    private void UpdateLastUpdateTime()
    {
        LastUpdateText.Text = $"Last update: {DateTime.Now:HH:mm:ss}";
    }

    private void OpenChartForSymbol(string symbol)
    {
        try
        {
            var chartWindow = new ChartWindow(symbol);
            chartWindow.Show();
            UpdateStatus($"Opened chart for {symbol}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Error opening chart for {symbol}: {ex.Message}");
            UpdateStatus($"Error opening chart: {ex.Message}");
        }
    }

    private void OnWatchlistChanged(object? sender, WatchlistChangedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            switch (e.ChangeType)
            {
                case WatchlistChangeType.SymbolAdded:
                case WatchlistChangeType.SymbolRemoved:
                case WatchlistChangeType.Reordered:
                    if (CurrentWatchlist?.Id == e.Watchlist.Id)
                    {
                        FilterItems();
                        UpdateWatchlistInfo();
                    }
                    break;
            }
        });
    }

    private void OnWatchlistItemUpdated(object? sender, WatchlistItemUpdatedEventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            if (CurrentWatchlist?.Id == e.WatchlistId)
            {
                UpdateWatchlistInfo();
                UpdateLastUpdateTime();
            }
        });
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}

/// <summary>
/// Dialog for adding symbols to watchlist
/// </summary>
public partial class AddSymbolDialog : Window
{
    public string Symbol { get; private set; } = string.Empty;

    public AddSymbolDialog()
    {
        Title = "Add Symbol";
        Width = 300;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        
        var label = new Label { Content = "Symbol:", Margin = new Thickness(10) };
        Grid.SetRow(label, 0);
        
        var textBox = new TextBox { Margin = new Thickness(10), Height = 25 };
        Grid.SetRow(textBox, 1);
        
        var buttonPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10)
        };
        
        var okButton = new Button { Content = "OK", Width = 60, Height = 25, Margin = new Thickness(5, 0, 5, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 60, Height = 25, Margin = new Thickness(5, 0, 5, 0) };
        
        okButton.Click += (s, e) => 
        {
            Symbol = textBox.Text.Trim().ToUpper();
            if (!string.IsNullOrWhiteSpace(Symbol))
            {
                DialogResult = true;
                Close();
            }
        };
        
        cancelButton.Click += (s, e) => 
        {
            DialogResult = false;
            Close();
        };
        
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 2);
        
        grid.Children.Add(label);
        grid.Children.Add(textBox);
        grid.Children.Add(buttonPanel);
        
        Content = grid;
        textBox.Focus();
    }
}

/// <summary>
/// Dialog for creating new watchlists
/// </summary>
public partial class NewWatchlistDialog : Window
{
    public string WatchlistName { get; private set; } = string.Empty;

    public NewWatchlistDialog()
    {
        Title = "New Watchlist";
        Width = 300;
        Height = 150;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        
        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        
        var label = new Label { Content = "Watchlist Name:", Margin = new Thickness(10) };
        Grid.SetRow(label, 0);
        
        var textBox = new TextBox { Margin = new Thickness(10), Height = 25 };
        Grid.SetRow(textBox, 1);
        
        var buttonPanel = new StackPanel 
        { 
            Orientation = Orientation.Horizontal, 
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10)
        };
        
        var okButton = new Button { Content = "OK", Width = 60, Height = 25, Margin = new Thickness(5, 0, 5, 0) };
        var cancelButton = new Button { Content = "Cancel", Width = 60, Height = 25, Margin = new Thickness(5, 0, 5, 0) };
        
        okButton.Click += (s, e) => 
        {
            WatchlistName = textBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(WatchlistName))
            {
                DialogResult = true;
                Close();
            }
        };
        
        cancelButton.Click += (s, e) => 
        {
            DialogResult = false;
            Close();
        };
        
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);
        Grid.SetRow(buttonPanel, 2);
        
        grid.Children.Add(label);
        grid.Children.Add(textBox);
        grid.Children.Add(buttonPanel);
        
        Content = grid;
        textBox.Focus();
    }
}
