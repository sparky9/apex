using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ApexV2.Dashboard.News
{
    /// <summary>
    /// Professional news panel for displaying financial news with filtering and interaction
    /// </summary>
    public partial class NewsPanel : UserControl
    {
        private readonly INewsService _newsService;
        private readonly ILogger<NewsPanel> _logger;
        private readonly DispatcherTimer _refreshTimer;
        private List<NewsArticle> _allNews = new List<NewsArticle>();
        private List<NewsArticle> _filteredNews = new List<NewsArticle>();
        private NewsFilter _currentFilter = new NewsFilter();

        public NewsPanel()
        {
            InitializeComponent();
            
            // Get services from DI container (fallback to mock for design time)
            try
            {
                var serviceProvider = Application.Current?.Properties["ServiceProvider"] as IServiceProvider;
                _newsService = serviceProvider?.GetService<INewsService>() ?? new MockNewsService();
                _logger = serviceProvider?.GetService<ILogger<NewsPanel>>() ?? new MockLogger<NewsPanel>();
            }
            catch
            {
                _newsService = new MockNewsService();
                _logger = new MockLogger<NewsPanel>();
            }

            // Setup refresh timer
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(15) // Auto-refresh every 15 minutes
            };
            _refreshTimer.Tick += async (s, e) => await RefreshNewsAsync();

            InitializeFilters();
            Loaded += NewsPanel_Loaded;
        }

        private async void NewsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadNewsAsync();
            _refreshTimer.Start();
        }

        private void InitializeFilters()
        {
            // Initialize category filter
            CategoryFilterCombo.Items.Add("All Categories");
            foreach (NewsCategory category in Enum.GetValues<NewsCategory>())
            {
                CategoryFilterCombo.Items.Add(category.ToString());
            }
            CategoryFilterCombo.SelectedIndex = 0;

            // Initialize sentiment filter
            SentimentFilterCombo.Items.Add("All Sentiments");
            foreach (SentimentType sentiment in Enum.GetValues<SentimentType>())
            {
                SentimentFilterCombo.Items.Add(sentiment.ToString());
            }
            SentimentFilterCombo.SelectedIndex = 0;
        }

        private async Task LoadNewsAsync()
        {
            try
            {
                ShowLoadingIndicator();
                UpdateStatus("Loading news...");

                _logger.LogInformation("Loading news for news panel");

                // Load market news
                var marketNews = await _newsService.GetMarketNewsAsync(100);
                
                _allNews = marketNews ?? new List<NewsArticle>();
                ApplyFilters();
                UpdateUI();

                UpdateStatus($"Loaded {_allNews.Count} articles");
                _logger.LogInformation("Successfully loaded {Count} news articles", _allNews.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading news");
                UpdateStatus("Error loading news");
                ShowNoNewsIndicator();
            }
            finally
            {
                HideLoadingIndicator();
            }
        }

        private async Task RefreshNewsAsync()
        {
            try
            {
                UpdateStatus("Refreshing news...");
                await _newsService.RefreshNewsAsync();
                await LoadNewsAsync();
                UpdateStatus("News refreshed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing news");
                UpdateStatus("Error refreshing news");
            }
        }

        private void ApplyFilters()
        {
            var filteredNews = _allNews.AsEnumerable();

            // Apply symbol filter
            if (!string.IsNullOrWhiteSpace(SymbolFilterBox.Text))
            {
                var symbol = SymbolFilterBox.Text.Trim().ToUpperInvariant();
                filteredNews = filteredNews.Where(a => 
                    a.Symbol.Contains(symbol, StringComparison.OrdinalIgnoreCase) ||
                    a.Symbols.Any(s => s.Contains(symbol, StringComparison.OrdinalIgnoreCase)));
            }

            // Apply category filter
            if (CategoryFilterCombo.SelectedIndex > 0)
            {
                var selectedCategory = (NewsCategory)Enum.Parse(typeof(NewsCategory), CategoryFilterCombo.SelectedItem.ToString()!);
                filteredNews = filteredNews.Where(a => a.Category == selectedCategory);
            }

            // Apply sentiment filter
            if (SentimentFilterCombo.SelectedIndex > 0)
            {
                var selectedSentiment = (SentimentType)Enum.Parse(typeof(SentimentType), SentimentFilterCombo.SelectedItem.ToString()!);
                filteredNews = filteredNews.Where(a => a.Sentiment?.Type == selectedSentiment);
            }

            // Apply search filter
            if (!string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                var searchText = SearchBox.Text.Trim().ToLowerInvariant();
                filteredNews = filteredNews.Where(a =>
                    a.Title.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    a.Summary.Contains(searchText, StringComparison.OrdinalIgnoreCase));
            }

            _filteredNews = filteredNews
                .OrderByDescending(a => a.PublishedAt)
                .ToList();
        }

        private void UpdateUI()
        {
            Dispatcher.Invoke(() =>
            {
                NewsItemsControl.ItemsSource = _filteredNews;
                NewsCountText.Text = $"({_filteredNews.Count} articles)";
                
                var unreadCount = _allNews.Count(a => !a.IsRead);
                var bookmarkedCount = _allNews.Count(a => a.IsBookmarked);
                
                UnreadCountText.Text = $"{unreadCount} unread";
                BookmarkedCountText.Text = $"{bookmarkedCount} bookmarked";
                
                LastUpdateText.Text = $"Updated {DateTime.Now:HH:mm}";

                // Show/hide no news indicator
                if (_filteredNews.Any())
                {
                    NoNewsIndicator.Visibility = Visibility.Collapsed;
                    NewsItemsControl.Visibility = Visibility.Visible;
                }
                else
                {
                    ShowNoNewsIndicator();
                }
            });
        }

        private void ShowLoadingIndicator()
        {
            Dispatcher.Invoke(() =>
            {
                LoadingIndicator.Visibility = Visibility.Visible;
                NewsItemsControl.Visibility = Visibility.Collapsed;
                NoNewsIndicator.Visibility = Visibility.Collapsed;
            });
        }

        private void HideLoadingIndicator()
        {
            Dispatcher.Invoke(() =>
            {
                LoadingIndicator.Visibility = Visibility.Collapsed;
            });
        }

        private void ShowNoNewsIndicator()
        {
            Dispatcher.Invoke(() =>
            {
                NoNewsIndicator.Visibility = Visibility.Visible;
                NewsItemsControl.Visibility = Visibility.Collapsed;
                LoadingIndicator.Visibility = Visibility.Collapsed;
            });
        }

        private void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = message;
            });
        }

        // Event Handlers
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RefreshNewsAsync();
        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {
            FilterPanel.Visibility = FilterPanel.Visibility == Visibility.Visible 
                ? Visibility.Collapsed 
                : Visibility.Visible;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO [REVIEWED]: Open news settings dialog
            MessageBox.Show("News settings functionality coming soon!", "News Settings", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void SymbolFilter_Changed(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
            UpdateUI();
        }

        private void CategoryFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
            UpdateUI();
        }

        private void SentimentFilter_Changed(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
            UpdateUI();
        }

        private void SearchBox_Changed(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
            UpdateUI();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            SymbolFilterBox.Text = string.Empty;
            CategoryFilterCombo.SelectedIndex = 0;
            SentimentFilterCombo.SelectedIndex = 0;
            SearchBox.Text = string.Empty;
            ApplyFilters();
            UpdateUI();
        }

        private async void BookmarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is NewsArticle article)
            {
                try
                {
                    await _newsService.BookmarkArticleAsync(article.Id);
                    UpdateUI(); // Refresh to show updated bookmark status
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error bookmarking article {ArticleId}", article.Id);
                }
            }
        }

        private async void ArticleTitle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBlock textBlock && textBlock.Tag is NewsArticle article)
            {
                try
                {
                    await _newsService.MarkAsReadAsync(article.Id);
                    
                    // Open article in browser
                    if (!string.IsNullOrEmpty(article.Url))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = article.Url,
                            UseShellExecute = true
                        });
                    }
                    
                    UpdateUI(); // Refresh to show updated read status
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error opening article {ArticleId}", article.Id);
                }
            }
        }

        private void OpenArticle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is NewsArticle article)
            {
                try
                {
                    if (!string.IsNullOrEmpty(article.Url))
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = article.Url,
                            UseShellExecute = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error opening article URL {Url}", article.Url);
                    MessageBox.Show("Could not open article URL", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        public void SetSymbolFilter(string symbol)
        {
            SymbolFilterBox.Text = symbol;
            FilterPanel.Visibility = Visibility.Visible;
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
        }
    }

    // Value Converters for UI
    public class BoolToBackgroundConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? new SolidColorBrush(Color.FromArgb(20, 0, 0, 0)) : Brushes.Transparent;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SentimentToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                SentimentType.VeryPositive => new SolidColorBrush(Color.FromRgb(40, 167, 69)),
                SentimentType.Positive => new SolidColorBrush(Color.FromRgb(92, 184, 92)),
                SentimentType.Neutral => new SolidColorBrush(Color.FromRgb(108, 117, 125)),
                SentimentType.Negative => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                SentimentType.VeryNegative => new SolidColorBrush(Color.FromRgb(169, 68, 66)),
                _ => new SolidColorBrush(Color.FromRgb(108, 117, 125))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ImpactToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                NewsImpact.Critical => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
                NewsImpact.High => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
                NewsImpact.Medium => new SolidColorBrush(Color.FromRgb(23, 162, 184)),
                NewsImpact.Low => new SolidColorBrush(Color.FromRgb(108, 117, 125)),
                _ => new SolidColorBrush(Color.FromRgb(108, 117, 125))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BookmarkIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "★" : "☆";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BookmarkColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? new SolidColorBrush(Color.FromRgb(255, 193, 7)) : new SolidColorBrush(Color.FromRgb(180, 180, 180));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ReadToFontWeightConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? FontWeights.Normal : FontWeights.SemiBold;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // Mock services for design time
    public class MockNewsService : INewsService
    {
        public event EventHandler<NewsUpdate>? NewsUpdated;

        public Task<List<NewsArticle>> GetNewsForSymbolAsync(string symbol, int maxResults = 50)
        {
            return Task.FromResult(GenerateMockNews(symbol, maxResults));
        }

        public Task<List<NewsArticle>> GetMarketNewsAsync(int maxResults = 100)
        {
            return Task.FromResult(GenerateMockNews("MARKET", maxResults));
        }

        public Task<List<NewsArticle>> GetFilteredNewsAsync(NewsFilter filter)
        {
            return Task.FromResult(GenerateMockNews("FILTERED", 10));
        }

        public Task<List<NewsArticle>> SearchNewsAsync(string searchText, DateTime? fromDate = null, DateTime? toDate = null)
        {
            return Task.FromResult(GenerateMockNews("SEARCH", 5));
        }

        public Task<NewsAnalytics> GetNewsAnalyticsAsync()
        {
            return Task.FromResult(new NewsAnalytics());
        }

        public Task<bool> BookmarkArticleAsync(Guid articleId) => Task.FromResult(true);
        public Task<bool> MarkAsReadAsync(Guid articleId) => Task.FromResult(true);
        public Task<bool> RefreshNewsAsync() => Task.FromResult(true);
        public Task<bool> RefreshNewsForSymbolAsync(string symbol) => Task.FromResult(true);

        private List<NewsArticle> GenerateMockNews(string symbol, int count)
        {
            var news = new List<NewsArticle>();
            var random = new Random();
            var categories = Enum.GetValues<NewsCategory>();
            var sentiments = Enum.GetValues<SentimentType>();

            for (int i = 0; i < Math.Min(count, 20); i++)
            {
                news.Add(new NewsArticle
                {
                    Title = $"Mock News Article {i + 1} for {symbol}",
                    Summary = $"This is a mock summary for news article {i + 1}. It contains relevant financial information about {symbol}.",
                    Symbol = symbol,
                    PublishedAt = DateTime.Now.AddHours(-random.Next(1, 48)),
                    Source = new NewsSource { Name = "Mock Source", ReliabilityScore = 0.8 },
                    Category = categories[random.Next(categories.Length)],
                    Sentiment = new NewsSentiment
                    {
                        Type = sentiments[random.Next(sentiments.Length)],
                        Score = random.NextDouble() * 2 - 1,
                        Confidence = random.NextDouble()
                    },
                    RelevanceScore = random.NextDouble(),
                    Impact = (NewsImpact)random.Next(4),
                    IsRead = random.NextDouble() > 0.7,
                    IsBookmarked = random.NextDouble() > 0.9,
                    Url = "https://example.com/mock-article"
                });
            }

            return news;
        }
    }

    public class MockLogger<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}
