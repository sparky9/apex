using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApexV2.Dashboard.News
{
    /// <summary>
    /// Basic sentiment analyzer using keyword-based analysis
    /// Can be extended with ML models or external sentiment APIs
    /// </summary>
    public class NewsSentimentAnalyzer : INewsSentimentAnalyzer
    {
        private readonly ILogger<NewsSentimentAnalyzer> _logger;
        private static readonly Dictionary<string, double> PositiveKeywords;
        private static readonly Dictionary<string, double> NegativeKeywords;

        public bool IsEnabled { get; set; } = true;

        static NewsSentimentAnalyzer()
        {
            PositiveKeywords = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Strong positive
                { "surge", 0.8 }, { "soar", 0.8 }, { "rally", 0.8 }, { "boom", 0.8 },
                { "breakthrough", 0.8 }, { "record", 0.7 }, { "strong", 0.7 },
                { "growth", 0.6 }, { "gain", 0.6 }, { "profit", 0.6 }, { "earnings beat", 0.8 },
                { "upgrade", 0.7 }, { "bullish", 0.8 }, { "optimistic", 0.6 },
                
                // Moderate positive
                { "rise", 0.5 }, { "increase", 0.5 }, { "improve", 0.5 }, { "progress", 0.5 },
                { "positive", 0.5 }, { "success", 0.6 }, { "opportunity", 0.4 },
                { "momentum", 0.5 }, { "expansion", 0.5 }, { "recover", 0.5 },
                
                // Financial positive
                { "dividend increase", 0.7 }, { "buyback", 0.6 }, { "acquisition", 0.4 },
                { "merger", 0.4 }, { "partnership", 0.4 }, { "contract", 0.3 },
                { "revenue growth", 0.7 }, { "margin expansion", 0.6 }
            };

            NegativeKeywords = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
            {
                // Strong negative
                { "crash", -0.8 }, { "plunge", -0.8 }, { "collapse", -0.8 }, { "plummet", -0.8 },
                { "bankruptcy", -0.9 }, { "fraud", -0.9 }, { "scandal", -0.8 },
                { "loss", -0.6 }, { "decline", -0.5 }, { "fall", -0.5 },
                { "downgrade", -0.7 }, { "bearish", -0.8 }, { "pessimistic", -0.6 },
                
                // Moderate negative
                { "drop", -0.5 }, { "decrease", -0.5 }, { "concern", -0.4 }, { "worry", -0.5 },
                { "risk", -0.4 }, { "challenge", -0.3 }, { "pressure", -0.4 },
                { "weakness", -0.5 }, { "struggle", -0.6 }, { "disappoint", -0.6 },
                
                // Financial negative
                { "earnings miss", -0.8 }, { "guidance cut", -0.7 }, { "dividend cut", -0.8 },
                { "layoffs", -0.6 }, { "restructuring", -0.4 }, { "debt", -0.3 },
                { "investigation", -0.6 }, { "lawsuit", -0.5 }
            };
        }

        public NewsSentimentAnalyzer(ILogger<NewsSentimentAnalyzer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<NewsSentiment> AnalyzeSentimentAsync(string title, string content)
        {
            try
            {
                if (!IsEnabled)
                {
                    return new NewsSentiment
                    {
                        Type = SentimentType.Neutral,
                        Score = 0.0,
                        Confidence = 0.0
                    };
                }

                var combinedText = $"{title} {content}".ToLowerInvariant();
                var words = combinedText.Split(new[] { ' ', '.', ',', '!', '?', ';', ':', '\n', '\r' }, 
                    StringSplitOptions.RemoveEmptyEntries);

                double sentimentScore = 0.0;
                int matchedKeywords = 0;
                var foundKeywords = new List<string>();

                // Analyze positive keywords
                foreach (var keyword in PositiveKeywords)
                {
                    if (combinedText.Contains(keyword.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        sentimentScore += keyword.Value;
                        matchedKeywords++;
                        foundKeywords.Add($"+{keyword.Key}");
                    }
                }

                // Analyze negative keywords
                foreach (var keyword in NegativeKeywords)
                {
                    if (combinedText.Contains(keyword.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        sentimentScore += keyword.Value; // Already negative values
                        matchedKeywords++;
                        foundKeywords.Add($"-{keyword.Key}");
                    }
                }

                // Normalize score based on text length and keyword density
                if (words.Length > 0)
                {
                    sentimentScore = sentimentScore / Math.Max(1, words.Length / 50.0); // Normalize by text length
                }

                // Clamp score to -1 to +1 range
                sentimentScore = Math.Max(-1.0, Math.Min(1.0, sentimentScore));

                // Calculate confidence based on keyword matches and text length
                double confidence = Math.Min(1.0, matchedKeywords / 10.0); // Max confidence at 10+ keywords
                if (words.Length < 20) confidence *= 0.5; // Lower confidence for short text

                var sentimentType = DetermineSentimentType(sentimentScore);

                var sentiment = new NewsSentiment
                {
                    Type = sentimentType,
                    Score = sentimentScore,
                    Confidence = confidence,
                    Keywords = foundKeywords,
                    Explanation = GenerateExplanation(sentimentScore, matchedKeywords, foundKeywords)
                };

                _logger.LogDebug("Sentiment analysis: Score={Score:F2}, Type={Type}, Confidence={Confidence:F2}, Keywords={KeywordCount}",
                    sentimentScore, sentimentType, confidence, matchedKeywords);

                return await Task.FromResult(sentiment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing sentiment for text: {TextPreview}", 
                    title.Length > 50 ? title.Substring(0, 50) + "..." : title);
                
                return new NewsSentiment
                {
                    Type = SentimentType.Neutral,
                    Score = 0.0,
                    Confidence = 0.0,
                    Explanation = "Error during sentiment analysis"
                };
            }
        }

        public async Task<List<NewsArticle>> AnalyzeBatchSentimentAsync(List<NewsArticle> articles)
        {
            try
            {
                if (!IsEnabled || articles == null || !articles.Any())
                {
                    return articles ?? new List<NewsArticle>();
                }

                _logger.LogInformation("Analyzing sentiment for {Count} articles", articles.Count);

                var tasks = articles.Select(async article =>
                {
                    try
                    {
                        article.Sentiment = await AnalyzeSentimentAsync(article.Title, article.Summary);
                        return article;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to analyze sentiment for article {ArticleId}", article.Id);
                        article.Sentiment = new NewsSentiment
                        {
                            Type = SentimentType.Neutral,
                            Score = 0.0,
                            Confidence = 0.0
                        };
                        return article;
                    }
                });

                var results = await Task.WhenAll(tasks);
                
                _logger.LogInformation("Completed sentiment analysis for {Count} articles. " +
                    "Positive: {Positive}, Negative: {Negative}, Neutral: {Neutral}",
                    results.Length,
                    results.Count(a => a.Sentiment?.Type == SentimentType.Positive || a.Sentiment?.Type == SentimentType.VeryPositive),
                    results.Count(a => a.Sentiment?.Type == SentimentType.Negative || a.Sentiment?.Type == SentimentType.VeryNegative),
                    results.Count(a => a.Sentiment?.Type == SentimentType.Neutral));

                return results.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch sentiment analysis");
                return articles;
            }
        }

        private SentimentType DetermineSentimentType(double score)
        {
            return score switch
            {
                >= 0.6 => SentimentType.VeryPositive,
                >= 0.2 => SentimentType.Positive,
                <= -0.6 => SentimentType.VeryNegative,
                <= -0.2 => SentimentType.Negative,
                _ => SentimentType.Neutral
            };
        }

        private string GenerateExplanation(double score, int keywordCount, List<string> keywords)
        {
            if (keywordCount == 0)
            {
                return "No sentiment-indicating keywords found. Classified as neutral.";
            }

            var sentimentDescription = score switch
            {
                >= 0.6 => "very positive",
                >= 0.2 => "positive",
                <= -0.6 => "very negative",
                <= -0.2 => "negative",
                _ => "neutral"
            };

            var keywordList = keywords.Count > 5 
                ? string.Join(", ", keywords.Take(5)) + $" and {keywords.Count - 5} more"
                : string.Join(", ", keywords);

            return $"Classified as {sentimentDescription} based on {keywordCount} keywords: {keywordList}";
        }
    }
}
