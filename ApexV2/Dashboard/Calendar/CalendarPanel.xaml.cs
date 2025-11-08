using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace ApexV2.Dashboard.Calendar
{
    public partial class CalendarPanel : UserControl, INotifyPropertyChanged
    {
        private readonly IEconomicCalendarService _calendarService;
        private readonly ICalendarAlertManager _alertManager;
        private readonly ILogger<CalendarPanel> _logger;
        
        private ObservableCollection<EconomicEvent> _events = new();
        private CalendarFilter _currentFilter = new();
        
        // Binding Properties
        private DateTime _startDate = DateTime.Today;
        private DateTime _endDate = DateTime.Today.AddDays(7);
        private string _searchTerm = string.Empty;
        private bool _showEarnings = true;
        private bool _showDividends = true;
        private bool _showEconomic = true;
        private bool _showIPO = true;
        private bool _showHighImportance = true;
        private bool _showMediumImportance = true;
        private bool _showLowImportance = false;
        private bool _watchlistOnly = false;

        public CalendarPanel()
        {
            InitializeComponent();
            DataContext = this;
            
            // Initialize with dummy data for design-time
            if (DesignerProperties.GetIsInDesignMode(this))
            {
                InitializeDesignTimeData();
                return;
            }
        }

        public CalendarPanel(
            IEconomicCalendarService calendarService,
            ICalendarAlertManager alertManager,
            ILogger<CalendarPanel> logger) : this()
        {
            _calendarService = calendarService;
            _alertManager = alertManager;
            _logger = logger;

            Loaded += CalendarPanel_Loaded;
            SetupEventHandlers();
        }

        #region Properties

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    _ = LoadEventsAsync();
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    _ = LoadEventsAsync();
                }
            }
        }

        public string SearchTerm
        {
            get => _searchTerm;
            set
            {
                if (SetProperty(ref _searchTerm, value))
                {
                    _ = FilterEventsAsync();
                }
            }
        }

        public bool ShowEarnings
        {
            get => _showEarnings;
            set
            {
                if (SetProperty(ref _showEarnings, value))
                {
                    _ = FilterEventsAsync();
                }
            }
        }

        public bool ShowDividends
        {
            get => _showDividends;
            set
            {
                if (SetProperty(ref _showDividends, value))
                {
                    _ = FilterEventsAsync();
                }
            }
        }

        public bool ShowEconomic
        {
            get => _showEconomic;
            set
            {
                if (SetProperty(ref _showEconomic, value))
                {
                    _ = FilterEventsAsync();
                }
            }
        }

        public bool ShowIPO
        {
            get => _showIPO;
            set
            {
                if (SetProperty(ref _showIPO, value))
                {
                    _ = FilterEventsAsync();
                }
            }
        }

        public bool ShowHighImportance
        {
            get => _showHighImportance;
            set
            {
                if (SetProperty(ref _showHighImportance, value))
                {
                    _ = FilterEventsAsync();
                }
            }
        }

        public bool ShowMediumImportance
        {
            get => _showMediumImportance;
            set
            {
                if (SetProperty(ref _showMediumImportance, value))
                {
                    _ = FilterEventsAsync();
                }
            }
        }

        public bool ShowLowImportance
        {
            get => _showLowImportance;
            set
            {
                if (SetProperty(ref _showLowImportance, value))
                {
                    _ = FilterEventsAsync();
                }
            }
        }

        public bool WatchlistOnly
        {
            get => _watchlistOnly;
            set
            {
                if (SetProperty(ref _watchlistOnly, value))
                {
                    _ = FilterEventsAsync();
                }
            }
        }

        #endregion

        private void InitializeDesignTimeData()
        {
            var designEvents = new[]
            {
                new EconomicEvent
                {
                    Id = 1,
                    Title = "AAPL Earnings Report",
                    EventDate = DateTime.Today.AddHours(16),
                    Type = EventType.Earnings,
                    Importance = EventImportance.High,
                    Status = EventStatus.Scheduled,
                    Symbol = "AAPL",
                    CompanyName = "Apple Inc."
                },
                new EconomicEvent
                {
                    Id = 2,
                    Title = "GDP Report",
                    EventDate = DateTime.Today.AddHours(8).AddMinutes(30),
                    Type = EventType.EconomicIndicator,
                    Importance = EventImportance.High,
                    Status = EventStatus.Scheduled,
                    Country = "United States"
                }
            };

            EventsListView.ItemsSource = designEvents;
            EventCountText.Text = $"{designEvents.Length} events";
        }

        private void SetupEventHandlers()
        {
            if (_alertManager != null)
            {
                _alertManager.AlertTriggered += OnAlertTriggered;
            }
        }

        private async void CalendarPanel_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadEventsAsync();
            await UpdateMarketStatusAsync();
            await UpdateLastUpdateTimeAsync();
        }

        private async Task LoadEventsAsync()
        {
            if (_calendarService == null) return;

            try
            {
                ShowLoading("Loading calendar events...");

                _currentFilter = CreateCurrentFilter();
                var events = await _calendarService.GetEventsAsync(_currentFilter);

                _events.Clear();
                foreach (var evt in events.OrderBy(e => e.EventDate))
                {
                    _events.Add(evt);
                }

                EventsListView.ItemsSource = _events;
                EventCountText.Text = $"{_events.Count} events";

                _logger?.LogInformation($"Loaded {_events.Count} calendar events");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading calendar events");
                MessageBox.Show($"Error loading calendar events: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private async Task FilterEventsAsync()
        {
            await LoadEventsAsync();
        }

        private CalendarFilter CreateCurrentFilter()
        {
            var filter = new CalendarFilter
            {
                StartDate = StartDate,
                EndDate = EndDate,
                SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm,
                ShowOnlyWatchlistSymbols = WatchlistOnly
            };

            // Event types
            if (ShowEarnings) filter.EventTypes.Add(EventType.Earnings);
            if (ShowDividends) filter.EventTypes.Add(EventType.Dividend);
            if (ShowEconomic) filter.EventTypes.Add(EventType.EconomicIndicator);
            if (ShowIPO) filter.EventTypes.Add(EventType.IPO);

            // Importance levels
            if (ShowHighImportance) filter.ImportanceLevels.Add(EventImportance.High);
            if (ShowMediumImportance) filter.ImportanceLevels.Add(EventImportance.Medium);
            if (ShowLowImportance) filter.ImportanceLevels.Add(EventImportance.Low);

            return filter;
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLoading("Refreshing calendar data...");
                
                if (_calendarService != null)
                {
                    await _calendarService.RefreshCalendarDataAsync(StartDate, EndDate);
                    await LoadEventsAsync();
                    await UpdateLastUpdateTimeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing calendar data");
                MessageBox.Show($"Error refreshing data: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private void TodayButton_Click(object sender, RoutedEventArgs e)
        {
            StartDate = DateTime.Today;
            EndDate = DateTime.Today.AddDays(1);
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV files (*.csv)|*.csv|Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
                    DefaultExt = "csv",
                    AddExtension = true
                };

                if (saveDialog.ShowDialog() == true)
                {
                    ShowLoading("Exporting calendar data...");
                    
                    // Export implementation would go here
                    await Task.Delay(1000); // Placeholder
                    
                    MessageBox.Show("Calendar data exported successfully!", "Export Complete", 
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error exporting calendar data");
                MessageBox.Show($"Error exporting data: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideLoading();
            }
        }

        private void EventsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EventsListView.SelectedItem is EconomicEvent selectedEvent)
            {
                // Could show event details in a popup or side panel
                _logger?.LogInformation($"Selected event: {selectedEvent.Title}");
            }
        }

        private async Task UpdateMarketStatusAsync()
        {
            try
            {
                // This would check current market hours
                var isMarketOpen = IsMarketCurrentlyOpen();
                
                MarketStatusIndicator.Fill = isMarketOpen ? Brushes.Green : Brushes.Red;
                MarketStatusText.Text = isMarketOpen ? "Open" : "Closed";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating market status");
                MarketStatusIndicator.Fill = Brushes.Gray;
                MarketStatusText.Text = "Unknown";
            }
        }

        private async Task UpdateLastUpdateTimeAsync()
        {
            try
            {
                if (_calendarService != null)
                {
                    var lastUpdate = await _calendarService.GetLastUpdateAsync();
                    LastUpdateText.Text = lastUpdate == DateTime.MinValue 
                        ? "Last updated: Never" 
                        : $"Last updated: {lastUpdate:MMM dd, HH:mm}";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating last update time");
                LastUpdateText.Text = "Last updated: Unknown";
            }
        }

        private bool IsMarketCurrentlyOpen()
        {
            var now = DateTime.Now;
            var timeOfDay = now.TimeOfDay;
            
            // Simple check for NYSE/NASDAQ hours (9:30 AM - 4:00 PM EST, weekdays)
            return now.DayOfWeek != DayOfWeek.Saturday &&
                   now.DayOfWeek != DayOfWeek.Sunday &&
                   timeOfDay >= new TimeSpan(9, 30, 0) &&
                   timeOfDay <= new TimeSpan(16, 0, 0);
        }

        private void OnAlertTriggered(object? sender, EventAlert alert)
        {
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    var message = alert.Message ?? $"Calendar event alert for Event ID: {alert.EventId}";
                    MessageBox.Show(message, "Calendar Alert", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error displaying calendar alert");
                }
            });
        }

        private void ShowLoading(string message = "Loading...")
        {
            LoadingText.Text = message;
            LoadingOverlay.Visibility = Visibility.Visible;
        }

        private void HideLoading()
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        #endregion
    }

    // Value Converters
    public class EventTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EventType eventType)
            {
                return eventType switch
                {
                    EventType.Earnings => new SolidColorBrush(Colors.Blue),
                    EventType.Dividend => new SolidColorBrush(Colors.Green),
                    EventType.EconomicIndicator => new SolidColorBrush(Colors.Purple),
                    EventType.IPO => new SolidColorBrush(Colors.Orange),
                    EventType.CentralBankMeeting => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
            return new SolidColorBrush(Colors.Gray);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ImportanceToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is EventImportance importance)
            {
                return importance switch
                {
                    EventImportance.High => "🔴",
                    EventImportance.Medium => "🟡",
                    EventImportance.Low => "🟢",
                    _ => "⚪"
                };
            }
            return "⚪";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime && parameter is string format)
            {
                return dateTime.ToString(format);
            }
            return value?.ToString() ?? string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
