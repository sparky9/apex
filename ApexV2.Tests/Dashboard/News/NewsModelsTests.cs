using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using ApexV2.Dashboard.News;

namespace ApexV2.Tests.Dashboard.News
{
    public class NewsModelsTests
    {
        [Fact]
        public void NewsArticle_Constructor_SetsDefaultValues()
        {
            // Act
            var article = new NewsArticle();

            // Assert
            article.Id.Should().NotBeEmpty();
            article.PublishedAt.Should().BeCloseTo(DateTime.MinValue, TimeSpan.FromSeconds(1));
            article.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            article.Tags.Should().NotBeNull().And.BeEmpty();
            article.Symbols.Should().NotBeNull().And.BeEmpty();
            article.RelevanceScore.Should().Be(0.0);
        }

        [Fact]
        public void NewsArticle_PropertyChangedEvents_FireCorrectly()
        {
            // Arrange
            var article = new NewsArticle();
            var eventFired = false;
            string? changedProperty = null;

            article.PropertyChanged += (sender, e) =>
            {
                eventFired = true;
                changedProperty = e.PropertyName;
            };

            // Act
            article.Title = "Test Title";

            // Assert
            eventFired.Should().BeTrue();
            changedProperty.Should().Be(nameof(NewsArticle.Title));
        }

        [Theory]
        [InlineData(-0.5, 0.0)]
        [InlineData(1.5, 1.0)]
        [InlineData(0.5, 0.5)]
        public void NewsArticle_RelevanceScore_ClampsToValidRange(double input, double expected)
        {
            // Arrange
            var article = new NewsArticle();

            // Act
            article.RelevanceScore = input;

            // Assert
            article.RelevanceScore.Should().Be(expected);
        }

        [Fact]
        public void NewsSource_Constructor_SetsDefaultValues()
        {
            // Act
            var source = new NewsSource();

            // Assert
            source.Name.Should().BeEmpty();
            source.Domain.Should().BeEmpty();
            source.ReliabilityScore.Should().Be(0.5);
            source.Type.Should().Be(NewsSourceType.Financial);
            source.IsVerified.Should().BeFalse();
        }

        [Fact]
        public void NewsSentiment_Constructor_SetsDefaultValues()
        {
            // Act
            var sentiment = new NewsSentiment();

            // Assert
            sentiment.Type.Should().Be(SentimentType.VeryNegative);
            sentiment.Score.Should().Be(0.0);
            sentiment.Confidence.Should().Be(0.0);
            sentiment.Keywords.Should().NotBeNull().And.BeEmpty();
            sentiment.Explanation.Should().BeEmpty();
        }

        [Fact]
        public void NewsFilter_Constructor_SetsDefaultValues()
        {
            // Act
            var filter = new NewsFilter();

            // Assert
            filter.Symbols.Should().NotBeNull().And.BeEmpty();
            filter.Categories.Should().NotBeNull().And.BeEmpty();
            filter.SourceTypes.Should().NotBeNull().And.BeEmpty();
            filter.Sentiments.Should().NotBeNull().And.BeEmpty();
            filter.FromDate.Should().BeNull();
            filter.ToDate.Should().BeNull();
            filter.SearchText.Should().BeEmpty();
            filter.MinRelevanceScore.Should().Be(0.0);
            filter.ShowBookmarkedOnly.Should().BeFalse();
            filter.ShowUnreadOnly.Should().BeFalse();
            filter.SortBy.Should().Be(NewsSortBy.PublishedDate);
            filter.SortDescending.Should().BeTrue();
            filter.MaxResults.Should().Be(100);
        }

        [Fact]
        public void NewsFeedConfig_Constructor_SetsDefaultValues()
        {
            // Act
            var config = new NewsFeedConfig();

            // Assert
            config.WatchedSymbols.Should().NotBeNull().And.BeEmpty();
            config.EnabledSources.Should().NotBeNull().And.BeEmpty();
            config.RefreshIntervalMinutes.Should().Be(15);
            config.EnableRealTimeUpdates.Should().BeTrue();
            config.EnableSentimentAnalysis.Should().BeTrue();
            config.MinRelevanceThreshold.Should().Be(0.3);
            config.MaxArticlesPerSymbol.Should().Be(50);
            config.RetentionDays.Should().Be(30);
            config.EnablePushNotifications.Should().BeFalse();
            config.NotificationCategories.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public void NewsAnalytics_Constructor_SetsDefaultValues()
        {
            // Act
            var analytics = new NewsAnalytics();

            // Assert
            analytics.TotalArticles.Should().Be(0);
            analytics.TodayArticles.Should().Be(0);
            analytics.UnreadArticles.Should().Be(0);
            analytics.BookmarkedArticles.Should().Be(0);
            analytics.ArticlesBySymbol.Should().NotBeNull().And.BeEmpty();
            analytics.ArticlesBySentiment.Should().NotBeNull().And.BeEmpty();
            analytics.ArticlesByCategory.Should().NotBeNull().And.BeEmpty();
            analytics.ArticlesBySource.Should().NotBeNull().And.BeEmpty();
            analytics.AverageSentimentScore.Should().Be(0.0);
            analytics.AverageRelevanceScore.Should().Be(0.0);
            analytics.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void NewsUpdate_Constructor_SetsDefaultValues()
        {
            // Act
            var update = new NewsUpdate();

            // Assert
            update.Type.Should().Be(NewsUpdateType.NewArticle);
            update.Article.Should().BeNull();
            update.Symbol.Should().BeEmpty();
            update.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
            update.Message.Should().BeEmpty();
        }

        [Fact]
        public void NewsCategory_EnumValues_AreComplete()
        {
            // Arrange
            var expectedCategories = new[]
            {
                NewsCategory.Breaking,
                NewsCategory.Earnings,
                NewsCategory.Mergers,
                NewsCategory.Dividends,
                NewsCategory.Analyst,
                NewsCategory.Economic,
                NewsCategory.Regulatory,
                NewsCategory.Technology,
                NewsCategory.Energy,
                NewsCategory.Healthcare,
                NewsCategory.Financial,
                NewsCategory.General
            };

            // Act
            var actualCategories = Enum.GetValues<NewsCategory>();

            // Assert
            actualCategories.Should().BeEquivalentTo(expectedCategories);
        }

        [Fact]
        public void SentimentType_EnumValues_AreComplete()
        {
            // Arrange
            var expectedSentiments = new[]
            {
                SentimentType.VeryNegative,
                SentimentType.Negative,
                SentimentType.Neutral,
                SentimentType.Positive,
                SentimentType.VeryPositive
            };

            // Act
            var actualSentiments = Enum.GetValues<SentimentType>();

            // Assert
            actualSentiments.Should().BeEquivalentTo(expectedSentiments);
        }
    }
}
