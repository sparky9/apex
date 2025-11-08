using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ApexV2.Dashboard.News;

namespace ApexV2.Tests.Dashboard.News
{
    public class NewsSentimentAnalyzerTests
    {
        private readonly Mock<ILogger<NewsSentimentAnalyzer>> _mockLogger;
        private readonly NewsSentimentAnalyzer _analyzer;

        public NewsSentimentAnalyzerTests()
        {
            _mockLogger = new Mock<ILogger<NewsSentimentAnalyzer>>();
            _analyzer = new NewsSentimentAnalyzer(_mockLogger.Object);
        }

        [Fact]
        public void Constructor_WithValidLogger_SetsIsEnabledTrue()
        {
            // Assert
            _analyzer.IsEnabled.Should().BeTrue();
        }

        [Theory]
        [InlineData("Stock soars to record highs", "Company announces breakthrough technology", SentimentType.VeryPositive)]
        [InlineData("Earnings beat expectations", "Strong revenue growth reported", SentimentType.Positive)]
        [InlineData("Company updates guidance", "Regular quarterly report", SentimentType.Neutral)]
        [InlineData("Stock declines on concerns", "Analyst downgrades rating", SentimentType.Negative)]
        [InlineData("Massive fraud scandal crashes stock", "Bankruptcy filing imminent", SentimentType.VeryNegative)]
        public async Task AnalyzeSentimentAsync_WithVariousContent_ReturnsExpectedSentiment(
            string title, string content, SentimentType expectedType)
        {
            // Act
            var result = await _analyzer.AnalyzeSentimentAsync(title, content);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(expectedType);
            result.Keywords.Should().NotBeEmpty();
            result.Explanation.Should().NotBeEmpty();
        }

        [Fact]
        public async Task AnalyzeSentimentAsync_WhenDisabled_ReturnsNeutralSentiment()
        {
            // Arrange
            _analyzer.IsEnabled = false;

            // Act
            var result = await _analyzer.AnalyzeSentimentAsync("Test title", "Test content");

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(SentimentType.Neutral);
            result.Score.Should().Be(0.0);
            result.Confidence.Should().Be(0.0);
        }

        [Theory]
        [InlineData("", "")]
        [InlineData("Short", "")]
        [InlineData("", "Short")]
        public async Task AnalyzeSentimentAsync_WithShortText_ReturnsLowConfidence(string title, string content)
        {
            // Act
            var result = await _analyzer.AnalyzeSentimentAsync(title, content);

            // Assert
            result.Should().NotBeNull();
            result.Confidence.Should().BeLessThan(0.5);
        }

        [Fact]
        public async Task AnalyzeSentimentAsync_WithPositiveKeywords_ReturnsPositiveScore()
        {
            // Arrange
            var title = "Stock surge rally boom breakthrough strong growth profit earnings beat";
            var content = "Company reports record revenue and profit margins with strong momentum";

            // Act
            var result = await _analyzer.AnalyzeSentimentAsync(title, content);

            // Assert
            result.Should().NotBeNull();
            result.Score.Should().BeGreaterThan(0.0);
            result.Type.Should().BeOneOf(SentimentType.Positive, SentimentType.VeryPositive);
            result.Keywords.Should().Contain(k => k.StartsWith("+"));
        }

        [Fact]
        public async Task AnalyzeSentimentAsync_WithNegativeKeywords_ReturnsNegativeScore()
        {
            // Arrange
            var title = "Stock crash plunge collapse bankruptcy fraud scandal loss decline";
            var content = "Company faces massive losses and potential bankruptcy filing";

            // Act
            var result = await _analyzer.AnalyzeSentimentAsync(title, content);

            // Assert
            result.Should().NotBeNull();
            result.Score.Should().BeLessThan(0.0);
            result.Type.Should().BeOneOf(SentimentType.Negative, SentimentType.VeryNegative);
            result.Keywords.Should().Contain(k => k.StartsWith("-"));
        }

        [Fact]
        public async Task AnalyzeBatchSentimentAsync_WithMultipleArticles_AnalyzesAll()
        {
            // Arrange
            var articles = new List<NewsArticle>
            {
                new NewsArticle { Title = "Stock soars on earnings beat", Summary = "Record profits reported" },
                new NewsArticle { Title = "Company struggles with debt", Summary = "Financial concerns mount" },
                new NewsArticle { Title = "Regular quarterly update", Summary = "Standard business report" }
            };

            // Act
            var result = await _analyzer.AnalyzeBatchSentimentAsync(articles);

            // Assert
            result.Should().HaveCount(3);
            result.All(a => a.Sentiment != null).Should().BeTrue();
            
            // Should have mix of sentiments
            var sentimentTypes = result.Select(a => a.Sentiment!.Type).Distinct().ToList();
            sentimentTypes.Should().HaveCountGreaterThan(1);
        }

        [Fact]
        public async Task AnalyzeBatchSentimentAsync_WhenDisabled_ReturnsOriginalArticles()
        {
            // Arrange
            _analyzer.IsEnabled = false;
            var articles = new List<NewsArticle>
            {
                new NewsArticle { Title = "Test", Summary = "Test" }
            };

            // Act
            var result = await _analyzer.AnalyzeBatchSentimentAsync(articles);

            // Assert
            result.Should().BeEquivalentTo(articles);
        }

        [Fact]
        public async Task AnalyzeBatchSentimentAsync_WithNullArticles_ReturnsEmptyList()
        {
            // Act
            var result = await _analyzer.AnalyzeBatchSentimentAsync(null!);

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Fact]
        public async Task AnalyzeBatchSentimentAsync_WithEmptyList_ReturnsEmptyList()
        {
            // Arrange
            var articles = new List<NewsArticle>();

            // Act
            var result = await _analyzer.AnalyzeBatchSentimentAsync(articles);

            // Assert
            result.Should().NotBeNull().And.BeEmpty();
        }

        [Theory]
        [InlineData(0.8, SentimentType.VeryPositive)]
        [InlineData(0.3, SentimentType.Positive)]
        [InlineData(0.0, SentimentType.Neutral)]
        [InlineData(-0.3, SentimentType.Negative)]
        [InlineData(-0.8, SentimentType.VeryNegative)]
        public async Task AnalyzeSentimentAsync_ScoreMapping_ReturnsCorrectType(double targetScore, SentimentType expectedType)
        {
            // Arrange - Use specific keywords to get close to target score
            string title, content;
            if (targetScore > 0.6)
            {
                title = "surge soar rally boom breakthrough record strong growth profit earnings beat upgrade bullish optimistic";
                content = "Exceptional performance with record-breaking results and strong momentum across all sectors";
            }
            else if (targetScore > 0.1)
            {
                title = "rise increase improve progress positive success";
                content = "Company shows positive trends and improvement";
            }
            else if (targetScore < -0.6)
            {
                title = "crash plunge collapse bankruptcy fraud scandal loss decline downgrade bearish pessimistic disappoint";
                content = "Devastating performance with massive losses and complete collapse of business fundamentals";
            }
            else if (targetScore < -0.1)
            {
                title = "drop decrease concern worry risk challenge pressure weakness struggle";
                content = "Company faces significant challenges and mounting pressures";
            }
            else
            {
                title = "Company quarterly update";
                content = "Regular business operations continue as expected";
            }

            // Act
            var result = await _analyzer.AnalyzeSentimentAsync(title, content);

            // Assert
            result.Should().NotBeNull();
            result.Type.Should().Be(expectedType);
        }

        [Fact]
        public async Task AnalyzeSentimentAsync_WithException_ReturnsNeutralSentiment()
        {
            // Arrange - Create analyzer that will throw during processing
            var faultyAnalyzer = new NewsSentimentAnalyzer(_mockLogger.Object);

            // Act & Assert
            var result = await faultyAnalyzer.AnalyzeSentimentAsync(null!, null!);
            
            // Should handle gracefully
            result.Should().NotBeNull();
            result.Type.Should().Be(SentimentType.Neutral);
            result.Score.Should().Be(0.0);
            result.Confidence.Should().Be(0.0);
        }
    }
}
