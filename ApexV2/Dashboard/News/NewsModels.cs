#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ApexV2.Dashboard.News
{
    /// <summary>
    /// Represents a financial news article with metadata
    /// </summary>
    public class NewsArticle : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _summary = string.Empty;
        private string _content = string.Empty;
        private string _symbol = string.Empty;
        private NewsSource _source;
        private NewsSentiment _sentiment;
        private double _relevanceScore;
        private bool _isBookmarked;
        private bool _isRead;

        public Guid Id { get; set; } = Guid.NewGuid();
        
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public string Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public string Symbol
        {
            get => _symbol;
            set => SetProperty(ref _symbol, value);
        }

        public NewsSource Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }

        public DateTime PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string Url { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;

        public NewsSentiment Sentiment
        {
            get => _sentiment;
            set => SetProperty(ref _sentiment, value);
        }

        public double RelevanceScore
        {
            get => _relevanceScore;
            set => SetProperty(ref _relevanceScore, Math.Max(0, Math.Min(1, value)));
        }

        public bool IsBookmarked
        {
            get => _isBookmarked;
            set => SetProperty(ref _isBookmarked, value);
        }

        public bool IsRead
        {
            get => _isRead;
            set => SetProperty(ref _isRead, value);
        }

        public List<string> Tags { get; set; } = new List<string>();
        public List<string> Symbols { get; set; } = new List<string>();
        public NewsCategory Category { get; set; }
        public NewsImpact Impact { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// News source information and reliability metrics
    /// </summary>
    public class NewsSource
    {
        public string Name { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string LogoUrl { get; set; } = string.Empty;
        public double ReliabilityScore { get; set; } = 0.5; // 0-1 scale
        public NewsSourceType Type { get; set; }
        public bool IsVerified { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// News sentiment analysis result
    /// </summary>
    public class NewsSentiment
    {
        public SentimentType Type { get; set; }
        public double Score { get; set; } // -1 (very negative) to +1 (very positive)
        public double Confidence { get; set; } // 0-1 confidence in sentiment analysis
        public List<string> Keywords { get; set; } = new List<string>();
        public string Explanation { get; set; } = string.Empty;
    }

    /// <summary>
    /// News filter and search criteria
    /// </summary>
    public class NewsFilter
    {
        public List<string> Symbols { get; set; } = new List<string>();
        public List<NewsCategory> Categories { get; set; } = new List<NewsCategory>();
        public List<NewsSourceType> SourceTypes { get; set; } = new List<NewsSourceType>();
        public List<SentimentType> Sentiments { get; set; } = new List<SentimentType>();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public string SearchText { get; set; } = string.Empty;
        public double MinRelevanceScore { get; set; } = 0.0;
        public bool ShowBookmarkedOnly { get; set; }
        public bool ShowUnreadOnly { get; set; }
        public NewsSortBy SortBy { get; set; } = NewsSortBy.PublishedDate;
        public bool SortDescending { get; set; } = true;
        public int MaxResults { get; set; } = 100;
    }

    /// <summary>
    /// News feed configuration for symbols and sources
    /// </summary>
    public class NewsFeedConfig
    {
        public List<string> WatchedSymbols { get; set; } = new List<string>();
        public List<string> EnabledSources { get; set; } = new List<string>();
        public int RefreshIntervalMinutes { get; set; } = 15;
        public bool EnableRealTimeUpdates { get; set; } = true;
        public bool EnableSentimentAnalysis { get; set; } = true;
        public double MinRelevanceThreshold { get; set; } = 0.3;
        public int MaxArticlesPerSymbol { get; set; } = 50;
        public int RetentionDays { get; set; } = 30;
        public bool EnablePushNotifications { get; set; }
        public List<NewsCategory> NotificationCategories { get; set; } = new List<NewsCategory>();
    }

    /// <summary>
    /// News analytics and metrics
    /// </summary>
    public class NewsAnalytics
    {
        public int TotalArticles { get; set; }
        public int TodayArticles { get; set; }
        public int UnreadArticles { get; set; }
        public int BookmarkedArticles { get; set; }
        public Dictionary<string, int> ArticlesBySymbol { get; set; } = new Dictionary<string, int>();
        public Dictionary<SentimentType, int> ArticlesBySentiment { get; set; } = new Dictionary<SentimentType, int>();
        public Dictionary<NewsCategory, int> ArticlesByCategory { get; set; } = new Dictionary<NewsCategory, int>();
        public Dictionary<string, int> ArticlesBySource { get; set; } = new Dictionary<string, int>();
        public double AverageSentimentScore { get; set; }
        public double AverageRelevanceScore { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Real-time news update notification
    /// </summary>
    public class NewsUpdate
    {
        public NewsUpdateType Type { get; set; }
        public NewsArticle? Article { get; set; }
        public string Symbol { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
    }

    // Enumerations
    public enum NewsCategory
    {
        Breaking,
        Earnings,
        Mergers,
        Dividends,
        Analyst,
        Economic,
        Regulatory,
        Technology,
        Energy,
        Healthcare,
        Financial,
        General
    }

    public enum NewsSourceType
    {
        Financial,
        Mainstream,
        Specialized,
        SocialMedia,
        Press,
        Research,
        Government,
        Exchange
    }

    public enum SentimentType
    {
        VeryNegative,
        Negative,
        Neutral,
        Positive,
        VeryPositive
    }

    public enum NewsImpact
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum NewsSortBy
    {
        PublishedDate,
        Relevance,
        Sentiment,
        Symbol,
        Source,
        Impact
    }

    public enum NewsUpdateType
    {
        NewArticle,
        UpdatedArticle,
        BreakingNews,
        HighImpact,
        SentimentChange
    }
}
