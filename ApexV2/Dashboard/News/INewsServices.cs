using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ApexV2.Dashboard.News
{
    /// <summary>
    /// Interface for news data providers (Alpha Vantage, Finnhub, etc.)
    /// </summary>
    public interface INewsProvider
    {
        string Name { get; }
        bool IsEnabled { get; set; }
        bool SupportsRealTime { get; }
        bool SupportsSentimentAnalysis { get; }
        
        Task<List<NewsArticle>> GetNewsAsync(string symbol, int maxResults = 50);
        Task<List<NewsArticle>> GetMarketNewsAsync(int maxResults = 100);
        Task<List<NewsArticle>> SearchNewsAsync(string query, DateTime? fromDate = null, DateTime? toDate = null);
        Task<bool> TestConnectionAsync();
    }

    /// <summary>
    /// Interface for news service operations
    /// </summary>
    public interface INewsService
    {
        Task<List<NewsArticle>> GetNewsForSymbolAsync(string symbol, int maxResults = 50);
        Task<List<NewsArticle>> GetMarketNewsAsync(int maxResults = 100);
        Task<List<NewsArticle>> GetFilteredNewsAsync(NewsFilter filter);
        Task<List<NewsArticle>> SearchNewsAsync(string searchText, DateTime? fromDate = null, DateTime? toDate = null);
        Task<NewsAnalytics> GetNewsAnalyticsAsync();
        Task<bool> BookmarkArticleAsync(Guid articleId);
        Task<bool> MarkAsReadAsync(Guid articleId);
        Task<bool> RefreshNewsAsync();
        Task<bool> RefreshNewsForSymbolAsync(string symbol);
        
        event EventHandler<NewsUpdate>? NewsUpdated;
    }

    /// <summary>
    /// Interface for news sentiment analysis
    /// </summary>
    public interface INewsSentimentAnalyzer
    {
        Task<NewsSentiment> AnalyzeSentimentAsync(string title, string content);
        Task<List<NewsArticle>> AnalyzeBatchSentimentAsync(List<NewsArticle> articles);
        bool IsEnabled { get; set; }
    }

    /// <summary>
    /// Interface for news persistence operations
    /// </summary>
    public interface INewsRepository
    {
        Task<NewsArticle?> GetArticleAsync(Guid id);
        Task<List<NewsArticle>> GetArticlesAsync(NewsFilter filter);
        Task<bool> SaveArticleAsync(NewsArticle article);
        Task<bool> SaveArticlesAsync(List<NewsArticle> articles);
        Task<bool> UpdateArticleAsync(NewsArticle article);
        Task<bool> DeleteArticleAsync(Guid id);
        Task<bool> DeleteOldArticlesAsync(DateTime beforeDate);
        Task<NewsAnalytics> GetAnalyticsAsync();
        Task<List<NewsArticle>> GetBookmarkedArticlesAsync();
        Task<List<NewsArticle>> GetUnreadArticlesAsync();
    }

    /// <summary>
    /// Interface for news feed management
    /// </summary>
    public interface INewsFeedManager
    {
        NewsFeedConfig Config { get; set; }
        bool IsRunning { get; }
        
        Task StartAsync();
        Task StopAsync();
        Task<bool> AddWatchedSymbolAsync(string symbol);
        Task<bool> RemoveWatchedSymbolAsync(string symbol);
        Task<bool> UpdateConfigAsync(NewsFeedConfig config);
        
        event EventHandler<NewsUpdate>? NewsReceived;
    }
}
