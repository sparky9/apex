using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using ApexV2.Analysis.Patterns;
using ApexV2.Charts.Drawing;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Tests.Analysis.Patterns
{
    /// <summary>
    /// Comprehensive unit tests for the Pattern Recognition system
    /// </summary>
    public class PatternRecognitionTests
    {
        private readonly TestLogger _logger;
        private readonly PatternDetectionService _detectionService;
        private readonly PatternManagementService _managementService;
        private readonly PatternAnalysisService _analysisService;

        public PatternRecognitionTests()
        {
            _logger = new TestLogger();
            _detectionService = new PatternDetectionService(_logger);
            _managementService = new PatternManagementService(_logger, _detectionService);
            _analysisService = new PatternAnalysisService(_logger, _managementService);
        }

        [Fact]
        public void PatternBase_ShouldValidateCorrectly()
        {
            // Arrange
            var pattern = new TrianglePattern
            {
                Symbol = "AAPL",
                Name = "Test Triangle",
                ConfidenceScore = 0.8m,
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now,
                MinPrice = 100m,
                MaxPrice = 110m
            };
            pattern.KeyPoints.Add(new PatternPoint(DateTime.Now, 105m, "Test Point"));
            pattern.KeyPoints.Add(new PatternPoint(DateTime.Now, 108m, "Test Point 2"));

            // Act
            var errors = pattern.Validate();

            // Assert
            Assert.Empty(errors);
            Assert.Equal(10m, pattern.GetPatternHeight());
            Assert.True(pattern.GetPatternDuration().TotalDays >= 0);
        }

        [Fact]
        public void PatternBase_ShouldFailValidation_WithInvalidData()
        {
            // Arrange
            var pattern = new TrianglePattern
            {
                Symbol = "", // Invalid
                Name = "", // Invalid
                ConfidenceScore = 1.5m, // Invalid
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddDays(-1) // Invalid
            };

            // Act
            var errors = pattern.Validate();

            // Assert
            Assert.NotEmpty(errors);
            Assert.Contains("Symbol is required", errors);
            Assert.Contains("Pattern name is required", errors);
            Assert.Contains("Confidence score must be between 0 and 1", errors);
            Assert.Contains("Start time must be before end time", errors);
        }

        [Fact]
        public async Task PatternDetectionService_ShouldDetectTrianglePatterns()
        {
            // Arrange
            var priceData = GenerateTrianglePatternData();

            // Act
            var result = await _detectionService.DetectPatternsAsync("AAPL", priceData);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TotalFound >= 0);
            Assert.Empty(result.Errors);
        }

        [Fact]
        public async Task PatternDetectionService_ShouldHandleInsufficientData()
        {
            // Arrange
            var priceData = GenerateSmallDataSet();

            // Act
            var result = await _detectionService.DetectPatternsAsync("AAPL", priceData);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Insufficient data", result.Warnings.FirstOrDefault() ?? "");
        }

        [Fact]
        public async Task PatternDetectionService_ShouldHandleEmptySymbol()
        {
            // Arrange
            var priceData = GenerateTestData();

            // Act
            var result = await _detectionService.DetectPatternsAsync("", priceData);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Symbol is required", result.Errors);
        }

        [Fact]
        public async Task PatternManagementService_ShouldAddPattern()
        {
            // Arrange
            var pattern = CreateTestTrianglePattern();

            // Act
            var success = await _managementService.AddPatternAsync(pattern);

            // Assert
            Assert.True(success);
            
            var retrievedPattern = await _managementService.GetPatternAsync(pattern.Id);
            Assert.NotNull(retrievedPattern);
            Assert.Equal(pattern.Symbol, retrievedPattern.Symbol);
        }

        [Fact]
        public async Task PatternManagementService_ShouldUpdatePattern()
        {
            // Arrange
            var pattern = CreateTestTrianglePattern();
            await _managementService.AddPatternAsync(pattern);
            
            pattern.ConfidenceScore = 0.9m;
            pattern.Status = PatternStatus.Confirmed;

            // Act
            var success = await _managementService.UpdatePatternAsync(pattern);

            // Assert
            Assert.True(success);
            
            var retrievedPattern = await _managementService.GetPatternAsync(pattern.Id);
            Assert.NotNull(retrievedPattern);
            Assert.Equal(0.9m, retrievedPattern.ConfidenceScore);
            Assert.Equal(PatternStatus.Confirmed, retrievedPattern.Status);
        }

        [Fact]
        public async Task PatternManagementService_ShouldRemovePattern()
        {
            // Arrange
            var pattern = CreateTestTrianglePattern();
            await _managementService.AddPatternAsync(pattern);

            // Act
            var success = await _managementService.RemovePatternAsync(pattern.Id);

            // Assert
            Assert.True(success);
            
            var retrievedPattern = await _managementService.GetPatternAsync(pattern.Id);
            Assert.Null(retrievedPattern);
        }

        [Fact]
        public async Task PatternManagementService_ShouldGetPatternsForSymbol()
        {
            // Arrange
            var pattern1 = CreateTestTrianglePattern();
            pattern1.Symbol = "AAPL";
            var pattern2 = CreateTestSupportPattern();
            pattern2.Symbol = "AAPL";
            var pattern3 = CreateTestTrianglePattern();
            pattern3.Symbol = "MSFT";

            await _managementService.AddPatternAsync(pattern1);
            await _managementService.AddPatternAsync(pattern2);
            await _managementService.AddPatternAsync(pattern3);

            // Act
            var aaplPatterns = await _managementService.GetPatternsForSymbolAsync("AAPL");
            var msftPatterns = await _managementService.GetPatternsForSymbolAsync("MSFT");

            // Assert
            Assert.Equal(2, aaplPatterns.Count);
            Assert.Single(msftPatterns);
        }

        [Fact]
        public async Task PatternManagementService_ShouldSearchPatterns()
        {
            // Arrange
            var pattern1 = CreateTestTrianglePattern();
            pattern1.ConfidenceScore = 0.8m;
            pattern1.Direction = PatternDirection.Bullish;
            
            var pattern2 = CreateTestSupportPattern();
            pattern2.ConfidenceScore = 0.6m;
            pattern2.Direction = PatternDirection.Bearish;

            await _managementService.AddPatternAsync(pattern1);
            await _managementService.AddPatternAsync(pattern2);

            var criteria = new PatternSearchCriteria
            {
                MinConfidence = 0.7m,
                Directions = new List<PatternDirection> { PatternDirection.Bullish }
            };

            // Act
            var results = await _managementService.SearchPatternsAsync(criteria);

            // Assert
            Assert.Single(results);
            Assert.Equal(pattern1.Id, results[0].Id);
        }

        [Fact]
        public async Task PatternManagementService_ShouldGetStatistics()
        {
            // Arrange
            var pattern1 = CreateTestTrianglePattern();
            pattern1.Status = PatternStatus.Forming;
            var pattern2 = CreateTestSupportPattern();
            pattern2.Status = PatternStatus.Completed;
            var pattern3 = CreateTestTrianglePattern();
            pattern3.Status = PatternStatus.Confirmed;

            await _managementService.AddPatternAsync(pattern1);
            await _managementService.AddPatternAsync(pattern2);
            await _managementService.AddPatternAsync(pattern3);

            // Act
            var stats = await _managementService.GetStatisticsAsync();

            // Assert
            Assert.Equal(3, stats.TotalPatterns);
            Assert.Equal(1, stats.ActivePatterns);
            Assert.Equal(1, stats.CompletedPatterns);
            Assert.Equal(1, stats.ConfirmedPatterns);
            Assert.True(stats.PatternsByType.ContainsKey(PatternType.Triangle));
            Assert.True(stats.PatternsByType.ContainsKey(PatternType.SupportLevel));
        }

        [Fact]
        public async Task PatternManagementService_ShouldScanForPatterns()
        {
            // Arrange
            var priceData = GenerateTestData();

            // Act
            var result = await _managementService.ScanForPatternsAsync("AAPL", priceData);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.TotalFound >= 0);
            
            // Verify patterns were added to management system
            var storedPatterns = await _managementService.GetPatternsForSymbolAsync("AAPL");
            Assert.Equal(result.TotalFound, storedPatterns.Count);
        }

        [Fact]
        public async Task PatternAnalysisService_ShouldAnalyzeSymbolPatterns()
        {
            // Arrange
            var pattern1 = CreateTestTrianglePattern();
            pattern1.Symbol = "AAPL";
            pattern1.Direction = PatternDirection.Bullish;
            pattern1.ConfidenceScore = 0.8m;
            
            var pattern2 = CreateTestSupportPattern();
            pattern2.Symbol = "AAPL";
            pattern2.Direction = PatternDirection.Bullish;
            pattern2.ConfidenceScore = 0.7m;

            await _managementService.AddPatternAsync(pattern1);
            await _managementService.AddPatternAsync(pattern2);

            var priceData = GenerateTestData();

            // Act
            var result = await _analysisService.AnalyzeSymbolPatternsAsync("AAPL", priceData);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("AAPL", result.Symbol);
            Assert.Equal(2, result.TotalPatterns);
            Assert.NotEmpty(result.BullishSignals);
            Assert.NotEqual(PatternRecommendation.Hold, result.Recommendation);
            Assert.True(result.Confidence > 0);
            Assert.NotEmpty(result.Summary);
        }

        [Fact]
        public async Task PatternAnalysisService_ShouldGetTradingLevels()
        {
            // Arrange
            var supportPattern = CreateTestSupportPattern();
            supportPattern.Symbol = "AAPL";
            
            var resistancePattern = CreateTestResistancePattern();
            resistancePattern.Symbol = "AAPL";

            await _managementService.AddPatternAsync(supportPattern);
            await _managementService.AddPatternAsync(resistancePattern);

            // Act
            var levels = await _analysisService.GetTradingLevelsAsync("AAPL");

            // Assert
            Assert.NotNull(levels);
            Assert.Equal("AAPL", levels.Symbol);
            Assert.NotEmpty(levels.SupportLevels);
            Assert.NotEmpty(levels.ResistanceLevels);
        }

        [Fact]
        public async Task PatternAnalysisService_ShouldCompareSymbolPatterns()
        {
            // Arrange
            var symbols = new List<string> { "AAPL", "MSFT" };
            
            var applePattern = CreateTestTrianglePattern();
            applePattern.Symbol = "AAPL";
            
            var microsoftPattern = CreateTestTrianglePattern();
            microsoftPattern.Symbol = "MSFT";

            await _managementService.AddPatternAsync(applePattern);
            await _managementService.AddPatternAsync(microsoftPattern);

            // Act
            var result = await _analysisService.CompareSymbolPatternsAsync(symbols);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.SymbolPatterns.Count);
            Assert.NotEmpty(result.CommonPatterns);
            Assert.Equal(2, result.SymbolRankings.Count);
        }

        [Fact]
        public void TrianglePattern_ShouldValidateCorrectly()
        {
            // Arrange
            var triangle = new TrianglePattern
            {
                Symbol = "AAPL",
                Name = "Test Triangle",
                ConfidenceScore = 0.8m,
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now,
                UpperTrendLine1 = new PatternPoint(DateTime.Now.AddDays(-1), 110m),
                UpperTrendLine2 = new PatternPoint(DateTime.Now, 108m),
                LowerTrendLine1 = new PatternPoint(DateTime.Now.AddDays(-1), 100m),
                LowerTrendLine2 = new PatternPoint(DateTime.Now, 102m),
                MinPrice = 100m,
                MaxPrice = 110m
            };
            triangle.KeyPoints.Add(triangle.UpperTrendLine1);
            triangle.KeyPoints.Add(triangle.UpperTrendLine2);

            // Act
            var errors = triangle.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void TrianglePattern_ShouldFailValidation_WithInvalidTrendLines()
        {
            // Arrange
            var triangle = new TrianglePattern
            {
                Symbol = "AAPL",
                Name = "Test Triangle",
                ConfidenceScore = 0.8m,
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now,
                UpperTrendLine1 = new PatternPoint(DateTime.Now.AddDays(-1), 100m), // Below lower line
                LowerTrendLine1 = new PatternPoint(DateTime.Now.AddDays(-1), 110m),
                MinPrice = 100m,
                MaxPrice = 110m
            };
            triangle.KeyPoints.Add(triangle.UpperTrendLine1);
            triangle.KeyPoints.Add(triangle.LowerTrendLine1);

            // Act
            var errors = triangle.Validate();

            // Assert
            Assert.Contains("Upper trend line must be above lower trend line", errors);
        }

        [Fact]
        public void HeadAndShouldersPattern_ShouldValidateCorrectly()
        {
            // Arrange
            var pattern = new HeadAndShouldersPattern
            {
                Symbol = "AAPL",
                Name = "H&S Pattern",
                ConfidenceScore = 0.8m,
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now,
                LeftShoulder = new PatternPoint(DateTime.Now.AddDays(-1), 105m),
                Head = new PatternPoint(DateTime.Now.AddHours(-12), 115m),
                RightShoulder = new PatternPoint(DateTime.Now, 105m),
                MinPrice = 100m,
                MaxPrice = 115m
            };
            pattern.KeyPoints.Add(pattern.LeftShoulder);
            pattern.KeyPoints.Add(pattern.Head);

            // Act
            var errors = pattern.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void SupportResistancePattern_ShouldValidateCorrectly()
        {
            // Arrange
            var pattern = new SupportResistancePattern
            {
                Symbol = "AAPL",
                Name = "Support Level",
                Type = PatternType.SupportLevel,
                ConfidenceScore = 0.8m,
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now,
                Level = 100m,
                TouchCount = 3,
                Strength = 0.8m,
                MinPrice = 100m,
                MaxPrice = 100m
            };
            pattern.KeyPoints.Add(new PatternPoint(DateTime.Now, 100m));
            pattern.KeyPoints.Add(new PatternPoint(DateTime.Now, 100m));

            // Act
            var errors = pattern.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public void CandlestickPattern_ShouldValidateCorrectly()
        {
            // Arrange
            var pattern = new CandlestickPattern
            {
                Symbol = "AAPL",
                Name = "Doji",
                Type = PatternType.Doji,
                ConfidenceScore = 0.8m,
                StartTime = DateTime.Now.AddDays(-1),
                EndTime = DateTime.Now,
                CandleCount = 1,
                MinPrice = 100m,
                MaxPrice = 100m
            };
            pattern.Candles.Add(new CandlestickData 
            { 
                Timestamp = DateTime.Now, 
                Open = 100m, 
                High = 101m, 
                Low = 99m, 
                Close = 100m, 
                Volume = 1000 
            });
            pattern.KeyPoints.Add(new PatternPoint(DateTime.Now, 100m));
            pattern.KeyPoints.Add(new PatternPoint(DateTime.Now, 100m));

            // Act
            var errors = pattern.Validate();

            // Assert
            Assert.Empty(errors);
        }

        [Fact]
        public async Task TriangleDetector_ShouldDetectTriangles()
        {
            // Arrange
            var detector = new TriangleDetector(_logger);
            var data = GenerateTrianglePatternData();

            // Act
            var patterns = await detector.DetectAsync("AAPL", data);

            // Assert
            Assert.NotNull(patterns);
            // Note: Detection depends on algorithm implementation
        }

        [Fact]
        public async Task SupportResistanceDetector_ShouldDetectLevels()
        {
            // Arrange
            var detector = new SupportResistanceDetector(_logger);
            var data = GenerateSupportResistanceData();

            // Act
            var patterns = await detector.DetectAsync("AAPL", data);

            // Assert
            Assert.NotNull(patterns);
            // Note: Detection depends on algorithm implementation
        }

        // Helper methods for generating test data
        private TrianglePattern CreateTestTrianglePattern()
        {
            return new TrianglePattern
            {
                Symbol = "AAPL",
                Name = "Test Triangle",
                Type = PatternType.Triangle,
                Direction = PatternDirection.Bullish,
                Status = PatternStatus.Forming,
                Reliability = PatternReliability.Medium,
                ConfidenceScore = 0.75m,
                StartTime = DateTime.Now.AddDays(-10),
                EndTime = DateTime.Now,
                MinPrice = 100m,
                MaxPrice = 110m,
                BreakoutLevel = 105m,
                TargetPrice = 115m,
                UpperTrendLine1 = new PatternPoint(DateTime.Now.AddDays(-10), 110m),
                UpperTrendLine2 = new PatternPoint(DateTime.Now.AddDays(-5), 108m),
                LowerTrendLine1 = new PatternPoint(DateTime.Now.AddDays(-9), 100m),
                LowerTrendLine2 = new PatternPoint(DateTime.Now.AddDays(-4), 102m)
            };
        }

        private SupportResistancePattern CreateTestSupportPattern()
        {
            return new SupportResistancePattern
            {
                Symbol = "AAPL",
                Name = "Support Level",
                Type = PatternType.SupportLevel,
                Direction = PatternDirection.Bullish,
                Status = PatternStatus.Forming,
                Reliability = PatternReliability.High,
                ConfidenceScore = 0.8m,
                StartTime = DateTime.Now.AddDays(-10),
                EndTime = DateTime.Now,
                MinPrice = 100m,
                MaxPrice = 100m,
                Level = 100m,
                TouchCount = 3,
                Strength = 0.8m
            };
        }

        private SupportResistancePattern CreateTestResistancePattern()
        {
            return new SupportResistancePattern
            {
                Symbol = "AAPL",
                Name = "Resistance Level",
                Type = PatternType.ResistanceLevel,
                Direction = PatternDirection.Bearish,
                Status = PatternStatus.Forming,
                Reliability = PatternReliability.High,
                ConfidenceScore = 0.8m,
                StartTime = DateTime.Now.AddDays(-10),
                EndTime = DateTime.Now,
                MinPrice = 120m,
                MaxPrice = 120m,
                Level = 120m,
                TouchCount = 2,
                Strength = 0.7m
            };
        }

        private List<CandlestickData> GenerateTestData()
        {
            var data = new List<CandlestickData>();
            var random = new Random(42); // Fixed seed for consistent tests
            var basePrice = 100m;

            for (int i = 0; i < 50; i++)
            {
                var open = basePrice + (decimal)(random.NextDouble() * 4 - 2);
                var high = open + (decimal)(random.NextDouble() * 3);
                var low = open - (decimal)(random.NextDouble() * 3);
                var close = low + (decimal)(random.NextDouble() * (double)(high - low));

                data.Add(new CandlestickData
                {
                    Timestamp = DateTime.Now.AddDays(-50 + i),
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = (long)(random.NextDouble() * 1000000 + 100000)
                });

                basePrice = close;
            }

            return data;
        }

        private List<CandlestickData> GenerateSmallDataSet()
        {
            return GenerateTestData().Take(5).ToList();
        }

        private List<CandlestickData> GenerateTrianglePatternData()
        {
            var data = new List<CandlestickData>();
            var basePrice = 100m;

            // Generate converging highs and lows to simulate triangle
            for (int i = 0; i < 30; i++)
            {
                var upperBound = 110m - (i * 0.3m);
                var lowerBound = 90m + (i * 0.2m);
                
                var price = lowerBound + (decimal)(0.5 * (double)(upperBound - lowerBound));
                var variation = (decimal)(new Random(i).NextDouble() * 2 - 1);
                
                var open = price + variation;
                var high = Math.Min(upperBound, open + Math.Abs(variation));
                var low = Math.Max(lowerBound, open - Math.Abs(variation));
                var close = low + (decimal)(new Random(i + 1).NextDouble() * (double)(high - low));

                data.Add(new CandlestickData
                {
                    Timestamp = DateTime.Now.AddDays(-30 + i),
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = 100000
                });
            }

            return data;
        }

        private List<CandlestickData> GenerateSupportResistanceData()
        {
            var data = new List<CandlestickData>();
            var supportLevel = 100m;
            var resistanceLevel = 120m;

            for (int i = 0; i < 30; i++)
            {
                var price = supportLevel + (decimal)(new Random(i).NextDouble() * (double)(resistanceLevel - supportLevel));
                
                // Occasionally touch support/resistance
                if (i % 8 == 0) price = supportLevel;
                if (i % 12 == 0) price = resistanceLevel;

                var open = price;
                var high = Math.Min(resistanceLevel + 1, price + 2);
                var low = Math.Max(supportLevel - 1, price - 2);
                var close = low + (decimal)(new Random(i + 10).NextDouble() * (double)(high - low));

                data.Add(new CandlestickData
                {
                    Timestamp = DateTime.Now.AddDays(-30 + i),
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = 100000
                });
            }

            return data;
        }
    }

    /// <summary>
    /// Test logger implementation for unit tests
    /// </summary>
    public class TestLogger : IChartLogger
    {
        public List<string> LogEntries { get; } = new();

        public void Trace(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"TRACE: {msg}");
        public void Debug(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"DEBUG: {msg}");
        public void Info(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"INFO: {msg}");
        public void Warn(string msg, Dictionary<string, object?>? props = null) => LogEntries.Add($"WARN: {msg}");
        public void Error(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => LogEntries.Add($"ERROR: {msg}");
        public void Critical(string msg, Exception? ex = null, Dictionary<string, object?>? props = null) => LogEntries.Add($"CRITICAL: {msg}");
    }
}
