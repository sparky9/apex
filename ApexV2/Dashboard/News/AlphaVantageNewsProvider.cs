using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApexV2.Dashboard.News
{
    /// <summary>
    /// Alpha Vantage news provider implementation
    /// </summary>
    public class AlphaVantageNewsProvider : INewsProvider
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<AlphaVantageNewsProvider> _logger;
        private readonly string _apiKey;
        private const string BaseUrl = "https://www.alphavantage.co/query";

        public string Name => "Alpha Vantage";
        public bool IsEnabled { get; set; } = true;
        public bool SupportsRealTime => false;
        public bool SupportsSentimentAnalysis => true;

        public AlphaVantageNewsProvider(HttpClient httpClient, ILogger<AlphaVantageNewsProvider> logger, string apiKey = "demo")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _apiKey = apiKey;
        }

        public async Task<List<NewsArticle>> GetNewsAsync(string symbol, int maxResults = 50)
        {
            try
            {
                _logger.LogInformation("Fetching news for symbol {Symbol} from Alpha Vantage", symbol);

                var url = $"{BaseUrl}?function=NEWS_SENTIMENT&tickers={symbol}&apikey={_apiKey}&limit={maxResults}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Alpha Vantage API returned {StatusCode} for symbol {Symbol}", response.StatusCode, symbol);
                    return new List<NewsArticle>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var newsData = JsonSerializer.Deserialize<AlphaVantageNewsResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (newsData?.Feed == null)
                {
                    _logger.LogWarning("No news data received from Alpha Vantage for symbol {Symbol}", symbol);
                    return new List<NewsArticle>();
                }

                var articles = newsData.Feed.Select(item => ConvertToNewsArticle(item, symbol)).ToList();
                _logger.LogInformation("Retrieved {Count} articles from Alpha Vantage for symbol {Symbol}", articles.Count, symbol);
                
                return articles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching news from Alpha Vantage for symbol {Symbol}", symbol);
                return new List<NewsArticle>();
            }
        }

        public async Task<List<NewsArticle>> GetMarketNewsAsync(int maxResults = 100)
        {
            try
            {
                _logger.LogInformation("Fetching market news from Alpha Vantage");

                var url = $"{BaseUrl}?function=NEWS_SENTIMENT&apikey={_apiKey}&limit={maxResults}";
                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Alpha Vantage API returned {StatusCode} for market news", response.StatusCode);
                    return new List<NewsArticle>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var newsData = JsonSerializer.Deserialize<AlphaVantageNewsResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (newsData?.Feed == null)
                {
                    _logger.LogWarning("No market news data received from Alpha Vantage");
                    return new List<NewsArticle>();
                }

                var articles = newsData.Feed.Select(item => ConvertToNewsArticle(item)).ToList();
                _logger.LogInformation("Retrieved {Count} market articles from Alpha Vantage", articles.Count);
                
                return articles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching market news from Alpha Vantage");
                return new List<NewsArticle>();
            }
        }

        public async Task<List<NewsArticle>> SearchNewsAsync(string query, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                _logger.LogInformation("Searching Alpha Vantage news for query: {Query}", query);

                // Alpha Vantage doesn't support search by keyword, so we'll get general news and filter
                var url = $"{BaseUrl}?function=NEWS_SENTIMENT&apikey={_apiKey}&limit=100";
                
                if (fromDate.HasValue)
                {
                    url += $"&time_from={fromDate.Value:yyyyMMddTHHmm}";
                }
                
                if (toDate.HasValue)
                {
                    url += $"&time_to={toDate.Value:yyyyMMddTHHmm}";
                }

                var response = await _httpClient.GetAsync(url);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Alpha Vantage API returned {StatusCode} for search query {Query}", response.StatusCode, query);
                    return new List<NewsArticle>();
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                var newsData = JsonSerializer.Deserialize<AlphaVantageNewsResponse>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (newsData?.Feed == null)
                {
                    return new List<NewsArticle>();
                }

                // Filter articles that contain the search query
                var filteredArticles = newsData.Feed
                    .Where(item => item.Title?.Contains(query, StringComparison.OrdinalIgnoreCase) == true ||
                                   item.Summary?.Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                    .Select(item => ConvertToNewsArticle(item))
                    .ToList();

                _logger.LogInformation("Found {Count} articles matching query '{Query}' from Alpha Vantage", filteredArticles.Count, query);
                return filteredArticles;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching Alpha Vantage news for query {Query}", query);
                return new List<NewsArticle>();
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var url = $"{BaseUrl}?function=NEWS_SENTIMENT&apikey={_apiKey}&limit=1";
                var response = await _httpClient.GetAsync(url);
                
                var isSuccessful = response.IsSuccessStatusCode;
                _logger.LogInformation("Alpha Vantage connection test: {Result}", isSuccessful ? "SUCCESS" : "FAILED");
                
                return isSuccessful;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Alpha Vantage connection test failed");
                return false;
            }
        }

        private NewsArticle ConvertToNewsArticle(AlphaVantageNewsItem item, string? specificSymbol = null)
        {
            var article = new NewsArticle
            {
                Title = item.Title ?? "No Title",
                Summary = item.Summary ?? string.Empty,
                Url = item.Url ?? string.Empty,
                Author = item.Authors?.FirstOrDefault() ?? "Unknown",
                PublishedAt = ParseDateTime(item.TimePublished),
                Source = new NewsSource
                {
                    Name = item.Source ?? "Alpha Vantage",
                    Domain = ExtractDomain(item.Url),
                    ReliabilityScore = 0.8, // Alpha Vantage generally provides reliable sources
                    Type = NewsSourceType.Financial,
                    IsVerified = true
                },
                ImageUrl = item.BannerImage ?? string.Empty
            };

            // Set specific symbol or extract from ticker sentiment
            if (!string.IsNullOrEmpty(specificSymbol))
            {
                article.Symbol = specificSymbol;
                article.Symbols.Add(specificSymbol);
            }
            else if (item.TickerSentiment != null && item.TickerSentiment.Any())
            {
                article.Symbols.AddRange(item.TickerSentiment.Select(ts => ts.Ticker ?? string.Empty).Where(t => !string.IsNullOrEmpty(t)));
                if (article.Symbols.Any())
                {
                    article.Symbol = article.Symbols.First();
                }
            }

            // Convert sentiment data
            if (item.OverallSentimentScore.HasValue)
            {
                article.Sentiment = new NewsSentiment
                {
                    Score = item.OverallSentimentScore.Value,
                    Type = ConvertSentimentLabel(item.OverallSentimentLabel),
                    Confidence = 0.7 // Alpha Vantage doesn't provide confidence, so we estimate
                };
            }

            // Determine category based on content
            article.Category = DetermineCategory(article.Title, article.Summary);

            return article;
        }

        private DateTime ParseDateTime(string? timePublished)
        {
            if (string.IsNullOrEmpty(timePublished))
                return DateTime.UtcNow;

            // Alpha Vantage format: "20231220T120000"
            if (DateTime.TryParseExact(timePublished, "yyyyMMddTHHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var result))
            {
                return result;
            }

            // Fallback to standard parsing
            if (DateTime.TryParse(timePublished, out var fallbackResult))
            {
                return fallbackResult;
            }

            return DateTime.UtcNow;
        }

        private string ExtractDomain(string? url)
        {
            if (string.IsNullOrEmpty(url))
                return string.Empty;

            try
            {
                var uri = new Uri(url);
                return uri.Host;
            }
            catch
            {
                return string.Empty;
            }
        }

        private SentimentType ConvertSentimentLabel(string? sentimentLabel)
        {
            return sentimentLabel?.ToLowerInvariant() switch
            {
                "bearish" => SentimentType.Negative,
                "bearish-leaning" => SentimentType.Negative,
                "neutral" => SentimentType.Neutral,
                "bullish-leaning" => SentimentType.Positive,
                "bullish" => SentimentType.Positive,
                _ => SentimentType.Neutral
            };
        }

        private NewsCategory DetermineCategory(string title, string summary)
        {
            var content = $"{title} {summary}".ToLowerInvariant();

            if (content.Contains("earnings") || content.Contains("eps") || content.Contains("revenue"))
                return NewsCategory.Earnings;
            if (content.Contains("merger") || content.Contains("acquisition") || content.Contains("m&a"))
                return NewsCategory.Mergers;
            if (content.Contains("dividend") || content.Contains("payout"))
                return NewsCategory.Dividends;
            if (content.Contains("analyst") || content.Contains("rating") || content.Contains("upgrade") || content.Contains("downgrade"))
                return NewsCategory.Analyst;
            if (content.Contains("breaking") || content.Contains("urgent"))
                return NewsCategory.Breaking;

            return NewsCategory.General;
        }
    }

    // Alpha Vantage API response models
    public class AlphaVantageNewsResponse
    {
        public string? Items { get; set; }
        public string? SentimentScoreDefinition { get; set; }
        public string? RelevanceScoreDefinition { get; set; }
        public List<AlphaVantageNewsItem>? Feed { get; set; }
    }

    public class AlphaVantageNewsItem
    {
        public string? Title { get; set; }
        public string? Url { get; set; }
        public string? TimePublished { get; set; }
        public List<string>? Authors { get; set; }
        public string? Summary { get; set; }
        public string? BannerImage { get; set; }
        public string? Source { get; set; }
        public string? CategoryWithinSource { get; set; }
        public string? SourceDomain { get; set; }
        public List<string>? Topics { get; set; }
        public double? OverallSentimentScore { get; set; }
        public string? OverallSentimentLabel { get; set; }
        public List<TickerSentiment>? TickerSentiment { get; set; }
    }

    public class TickerSentiment
    {
        public string? Ticker { get; set; }
        public double? RelevanceScore { get; set; }
        public double? TickerSentimentScore { get; set; }
        public string? TickerSentimentLabel { get; set; }
    }
}
