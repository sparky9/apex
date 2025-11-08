using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApexV2.Dashboard.News
{
    /// <summary>
    /// Core news service for managing financial news feeds and analysis
    /// </summary>
    public class NewsService : INewsService
    {
        private readonly INewsRepository _repository;
        private readonly INewsSentimentAnalyzer _sentimentAnalyzer;
        private readonly List<INewsProvider> _providers;
        private readonly ILogger<NewsService> _logger;

        public event EventHandler<NewsUpdate>? NewsUpdated;

        public NewsService(
            INewsRepository repository,
            INewsSentimentAnalyzer sentimentAnalyzer,
            IEnumerable<INewsProvider> providers,
            ILogger<NewsService> logger)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _sentimentAnalyzer = sentimentAnalyzer ?? throw new ArgumentNullException(nameof(sentimentAnalyzer));
            _providers = providers?.ToList() ?? throw new ArgumentNullException(nameof(providers));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<List<NewsArticle>> GetNewsForSymbolAsync(string symbol, int maxResults = 50)
        {
            try
            {
                _logger.LogInformation("Getting news for symbol {Symbol}, maxResults: {MaxResults}", symbol, maxResults);

                // First check local cache
                var filter = new NewsFilter
                {
                    Symbols = new List<string> { symbol },
                    MaxResults = maxResults,
                    SortBy = NewsSortBy.PublishedDate,
                    SortDescending = true
                };

                var cachedNews = await _repository.GetArticlesAsync(filter);
                
                // If we have recent cached news, return it
                if (cachedNews.Any() && cachedNews.First().PublishedAt > DateTime.UtcNow.AddHours(-1))
                {
                    _logger.LogDebug("Returning {Count} cached articles for {Symbol}", cachedNews.Count, symbol);
                    return cachedNews;
                }

                // Fetch fresh news from providers
                var freshNews = new List<NewsArticle>();
                foreach (var provider in _providers.Where(p => p.IsEnabled))
                {
                    try
                    {
                        var providerNews = await provider.GetNewsAsync(symbol, maxResults / _providers.Count(p => p.IsEnabled));
                        if (providerNews != null && providerNews.Any())
                        {
                            freshNews.AddRange(providerNews);
                            _logger.LogDebug("Retrieved {Count} articles from {Provider} for {Symbol}", 
                                providerNews.Count, provider.Name, symbol);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get news from provider {Provider} for {Symbol}", provider.Name, symbol);
                    }
                }

                // Process and analyze sentiment
                if (freshNews.Any())
                {
                    await ProcessNewsArticles(freshNews);
                    await _repository.SaveArticlesAsync(freshNews);
                    
                    // Notify about new articles
                    foreach (var article in freshNews.Take(5)) // Notify about top 5 most relevant
                    {
                        OnNewsUpdated(new NewsUpdate
                        {
                            Type = NewsUpdateType.NewArticle,
                            Article = article,
                            Symbol = symbol,
                            Message = $"New article: {article.Title}"
                        });
                    }
                }

                // Return combined results
                var combinedNews = freshNews.Concat(cachedNews)
                    .GroupBy(a => a.Url)
                    .Select(g => g.First())
                    .OrderByDescending(a => a.PublishedAt)
                    .Take(maxResults)
                    .ToList();

                _logger.LogInformation("Returning {Count} total articles for {Symbol}", combinedNews.Count, symbol);
                return combinedNews;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting news for symbol {Symbol}", symbol);
                return new List<NewsArticle>();
            }
        }

        public async Task<List<NewsArticle>> GetMarketNewsAsync(int maxResults = 100)
        {
            try
            {
                _logger.LogInformation("Getting market news, maxResults: {MaxResults}", maxResults);

                var marketNews = new List<NewsArticle>();
                foreach (var provider in _providers.Where(p => p.IsEnabled))
                {
                    try
                    {
                        var providerNews = await provider.GetMarketNewsAsync(maxResults / _providers.Count(p => p.IsEnabled));
                        if (providerNews != null && providerNews.Any())
                        {
                            marketNews.AddRange(providerNews);
                            _logger.LogDebug("Retrieved {Count} market articles from {Provider}", 
                                providerNews.Count, provider.Name);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get market news from provider {Provider}", provider.Name);
                    }
                }

                if (marketNews.Any())
                {
                    await ProcessNewsArticles(marketNews);
                    await _repository.SaveArticlesAsync(marketNews);
                }

                var result = marketNews
                    .OrderByDescending(a => a.PublishedAt)
                    .Take(maxResults)
                    .ToList();

                _logger.LogInformation("Returning {Count} market news articles", result.Count);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting market news");
                return new List<NewsArticle>();
            }
        }

        public async Task<List<NewsArticle>> GetFilteredNewsAsync(NewsFilter filter)
        {
            try
            {
                _logger.LogInformation("Getting filtered news with {SymbolCount} symbols, {CategoryCount} categories", 
                    filter.Symbols.Count, filter.Categories.Count);

                return await _repository.GetArticlesAsync(filter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filtered news");
                return new List<NewsArticle>();
            }
        }

        public async Task<List<NewsArticle>> SearchNewsAsync(string searchText, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                _logger.LogInformation("Searching news for '{SearchText}' from {FromDate} to {ToDate}", 
                    searchText, fromDate, toDate);

                var filter = new NewsFilter
                {
                    SearchText = searchText,
                    FromDate = fromDate,
                    ToDate = toDate,
                    MaxResults = 200
                };

                var localResults = await _repository.GetArticlesAsync(filter);
                
                // Also search providers for fresh results
                var providerResults = new List<NewsArticle>();
                foreach (var provider in _providers.Where(p => p.IsEnabled))
                {
                    try
                    {
                        var results = await provider.SearchNewsAsync(searchText, fromDate, toDate);
                        if (results != null && results.Any())
                        {
                            providerResults.AddRange(results);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to search news from provider {Provider}", provider.Name);
                    }
                }

                if (providerResults.Any())
                {
                    await ProcessNewsArticles(providerResults);
                    await _repository.SaveArticlesAsync(providerResults);
                }

                var combinedResults = localResults.Concat(providerResults)
                    .GroupBy(a => a.Url)
                    .Select(g => g.First())
                    .OrderByDescending(a => a.RelevanceScore)
                    .ThenByDescending(a => a.PublishedAt)
                    .ToList();

                _logger.LogInformation("Search returned {Count} articles for '{SearchText}'", 
                    combinedResults.Count, searchText);
                return combinedResults;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching news for '{SearchText}'", searchText);
                return new List<NewsArticle>();
            }
        }

        public async Task<NewsAnalytics> GetNewsAnalyticsAsync()
        {
            try
            {
                return await _repository.GetAnalyticsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting news analytics");
                return new NewsAnalytics();
            }
        }

        public async Task<bool> BookmarkArticleAsync(Guid articleId)
        {
            try
            {
                var article = await _repository.GetArticleAsync(articleId);
                if (article == null)
                {
                    _logger.LogWarning("Article {ArticleId} not found for bookmarking", articleId);
                    return false;
                }

                article.IsBookmarked = !article.IsBookmarked;
                var result = await _repository.UpdateArticleAsync(article);
                
                if (result)
                {
                    _logger.LogInformation("Article {ArticleId} bookmark status changed to {IsBookmarked}", 
                        articleId, article.IsBookmarked);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error bookmarking article {ArticleId}", articleId);
                return false;
            }
        }

        public async Task<bool> MarkAsReadAsync(Guid articleId)
        {
            try
            {
                var article = await _repository.GetArticleAsync(articleId);
                if (article == null)
                {
                    _logger.LogWarning("Article {ArticleId} not found for marking as read", articleId);
                    return false;
                }

                article.IsRead = true;
                var result = await _repository.UpdateArticleAsync(article);
                
                if (result)
                {
                    _logger.LogDebug("Article {ArticleId} marked as read", articleId);
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking article {ArticleId} as read", articleId);
                return false;
            }
        }

        public async Task<bool> RefreshNewsAsync()
        {
            try
            {
                _logger.LogInformation("Starting full news refresh");

                var success = true;
                var totalArticles = 0;

                // Get market news
                var marketNews = await GetMarketNewsAsync(100);
                totalArticles += marketNews.Count;

                _logger.LogInformation("News refresh completed. Total articles: {TotalArticles}", totalArticles);
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during news refresh");
                return false;
            }
        }

        public async Task<bool> RefreshNewsForSymbolAsync(string symbol)
        {
            try
            {
                _logger.LogInformation("Refreshing news for symbol {Symbol}", symbol);

                var news = await GetNewsForSymbolAsync(symbol, 50);
                
                _logger.LogInformation("Refreshed {Count} articles for symbol {Symbol}", news.Count, symbol);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing news for symbol {Symbol}", symbol);
                return false;
            }
        }

        private async Task ProcessNewsArticles(List<NewsArticle> articles)
        {
            try
            {
                // Analyze sentiment if enabled
                if (_sentimentAnalyzer.IsEnabled)
                {
                    await _sentimentAnalyzer.AnalyzeBatchSentimentAsync(articles);
                }

                // Calculate relevance scores
                foreach (var article in articles)
                {
                    article.RelevanceScore = CalculateRelevanceScore(article);
                    article.Impact = DetermineImpact(article);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing news articles");
            }
        }

        private double CalculateRelevanceScore(NewsArticle article)
        {
            double score = 0.5; // Base score

            // Source reliability factor
            score += article.Source.ReliabilityScore * 0.2;

            // Recency factor (more recent = higher score)
            var hoursSincePublished = (DateTime.UtcNow - article.PublishedAt).TotalHours;
            if (hoursSincePublished < 1) score += 0.3;
            else if (hoursSincePublished < 24) score += 0.2;
            else if (hoursSincePublished < 168) score += 0.1; // 1 week

            // Category importance
            if (article.Category == NewsCategory.Breaking) score += 0.2;
            else if (article.Category == NewsCategory.Earnings) score += 0.15;

            // Sentiment confidence
            if (article.Sentiment != null)
            {
                score += article.Sentiment.Confidence * 0.1;
            }

            return Math.Max(0, Math.Min(1, score));
        }

        private NewsImpact DetermineImpact(NewsArticle article)
        {
            if (article.Category == NewsCategory.Breaking) return NewsImpact.Critical;
            if (article.Category == NewsCategory.Earnings) return NewsImpact.High;
            if (article.RelevanceScore > 0.8) return NewsImpact.High;
            if (article.RelevanceScore > 0.6) return NewsImpact.Medium;
            return NewsImpact.Low;
        }

        protected virtual void OnNewsUpdated(NewsUpdate update)
        {
            NewsUpdated?.Invoke(this, update);
        }
    }
}
