using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ApexV2.Data.MarketData;

namespace ApexV2.Analysis.Watchlist;

/// <summary>
/// View model for a watchlist with observable properties
/// </summary>
public class WatchlistViewModel : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private List<WatchlistItemViewModel> _items = new();

    public int Id { get; set; }
    
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    
    public DateTime Created { get; set; }
    public DateTime LastModified { get; set; }
    
    public List<WatchlistItemViewModel> Items
    {
        get => _items;
        set => SetProperty(ref _items, value);
    }

    public int ItemCount => Items.Count;
    
    public decimal TotalValue => Items.Sum(i => i.LastPrice);
    
    public decimal TotalGainLoss => Items.Sum(i => i.Change);
    
    public decimal AverageChangePercent => Items.Count > 0 ? Items.Average(i => i.ChangePercent) : 0;

    public event PropertyChangedEventHandler? PropertyChanged;

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
}

/// <summary>
/// View model for an individual watchlist item with real-time data
/// </summary>
public class WatchlistItemViewModel : INotifyPropertyChanged
{
    private string _symbol = string.Empty;
    private decimal _lastPrice;
    private decimal _change;
    private decimal _changePercent;
    private long _volume;
    private DateTime _lastUpdate;

    public string Symbol
    {
        get => _symbol;
        set => SetProperty(ref _symbol, value);
    }
    
    public decimal LastPrice
    {
        get => _lastPrice;
        set => SetProperty(ref _lastPrice, value);
    }
    
    public decimal Change
    {
        get => _change;
        set => SetProperty(ref _change, value);
    }
    
    public decimal ChangePercent
    {
        get => _changePercent;
        set => SetProperty(ref _changePercent, value);
    }
    
    public long Volume
    {
        get => _volume;
        set => SetProperty(ref _volume, value);
    }
    
    public DateTime LastUpdate
    {
        get => _lastUpdate;
        set => SetProperty(ref _lastUpdate, value);
    }

    // Computed properties for UI binding
    public string FormattedPrice => LastPrice.ToString("C2");
    
    public string FormattedChange => $"{(Change >= 0 ? "+" : "")}{Change:F2}";
    
    public string FormattedChangePercent => $"{(ChangePercent >= 0 ? "+" : "")}{ChangePercent:F2}%";
    
    public string FormattedVolume => Volume.ToString("N0");
    
    public bool IsPositive => Change >= 0;
    
    public bool IsNegative => Change < 0;
    
    public string TrendIndicator => Change > 0 ? "↑" : Change < 0 ? "↓" : "→";

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        
        // Update computed properties when base properties change
        if (propertyName == nameof(LastPrice))
        {
            OnPropertyChanged(nameof(FormattedPrice));
        }
        else if (propertyName == nameof(Change))
        {
            OnPropertyChanged(nameof(FormattedChange));
            OnPropertyChanged(nameof(IsPositive));
            OnPropertyChanged(nameof(IsNegative));
            OnPropertyChanged(nameof(TrendIndicator));
        }
        else if (propertyName == nameof(ChangePercent))
        {
            OnPropertyChanged(nameof(FormattedChangePercent));
        }
        else if (propertyName == nameof(Volume))
        {
            OnPropertyChanged(nameof(FormattedVolume));
        }
        
        return true;
    }

    /// <summary>
    /// Update this item with the latest market data from a StockQuote
    /// </summary>
    public void UpdateFromQuote(StockQuote quote)
    {
        LastPrice = quote.Price;
        Change = quote.Change;
        ChangePercent = quote.ChangePercent;
        Volume = quote.Volume;
        LastUpdate = quote.Timestamp;
    }
}

/// <summary>
/// Search result for symbols across watchlists
/// </summary>
public class WatchlistSearchResult
{
    public int WatchlistId { get; set; }
    public string WatchlistName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
}

/// <summary>
/// Statistics aggregated across all watchlists
/// </summary>
public class WatchlistStatistics
{
    public int TotalWatchlists { get; set; }
    public int TotalSymbols { get; set; }
    public List<WatchlistItemViewModel> TopGainers { get; set; } = new();
    public List<WatchlistItemViewModel> TopLosers { get; set; } = new();
    public List<WatchlistItemViewModel> MostActive { get; set; } = new();
}

/// <summary>
/// Event args for watchlist changes
/// </summary>
public class WatchlistChangedEventArgs : EventArgs
{
    public WatchlistChangeType ChangeType { get; }
    public WatchlistViewModel Watchlist { get; }

    public WatchlistChangedEventArgs(WatchlistChangeType changeType, WatchlistViewModel watchlist)
    {
        ChangeType = changeType;
        Watchlist = watchlist;
    }
}

/// <summary>
/// Event args for individual watchlist item updates
/// </summary>
public class WatchlistItemUpdatedEventArgs : EventArgs
{
    public int WatchlistId { get; }
    public WatchlistItemViewModel Item { get; }

    public WatchlistItemUpdatedEventArgs(int watchlistId, WatchlistItemViewModel item)
    {
        WatchlistId = watchlistId;
        Item = item;
    }
}

/// <summary>
/// Types of changes that can occur to a watchlist
/// </summary>
public enum WatchlistChangeType
{
    Created,
    Deleted,
    SymbolAdded,
    SymbolRemoved,
    Reordered,
    Renamed
}

/// <summary>
/// Filter criteria for watchlist display
/// </summary>
public class WatchlistFilter
{
    public string? SymbolFilter { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public decimal? MinChange { get; set; }
    public decimal? MaxChange { get; set; }
    public long? MinVolume { get; set; }
    public WatchlistSortBy SortBy { get; set; } = WatchlistSortBy.Symbol;
    public bool SortDescending { get; set; } = false;
}

/// <summary>
/// Sort options for watchlist items
/// </summary>
public enum WatchlistSortBy
{
    Symbol,
    LastPrice,
    Change,
    ChangePercent,
    Volume,
    LastUpdate
}

/// <summary>
/// Configuration options for watchlist behavior
/// </summary>
public class WatchlistConfig
{
    public int RefreshIntervalSeconds { get; set; } = 30;
    public bool AutoRefreshEnabled { get; set; } = true;
    public bool ShowChangePercent { get; set; } = true;
    public bool ShowVolume { get; set; } = true;
    public bool PlaySoundOnPriceAlert { get; set; } = false;
    public bool HighlightSignificantChanges { get; set; } = true;
    public decimal SignificantChangeThreshold { get; set; } = 5.0m; // 5%
    public int MaxSymbolsPerWatchlist { get; set; } = 100;
    public bool AllowDuplicateSymbols { get; set; } = false;
}
