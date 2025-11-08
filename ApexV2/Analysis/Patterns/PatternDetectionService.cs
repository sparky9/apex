using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Charts.Export;
using ApexV2.Charts.Drawing;
using ApexV2.Charts.Models;

namespace ApexV2.Analysis.Patterns
{
    /// <summary>
    /// Core pattern detection service that analyzes price data to identify chart patterns
    /// </summary>
    public class PatternDetectionService
    {
        private readonly IChartLogger _logger;
        private readonly Dictionary<PatternType, IPatternDetector> _detectors;

        public PatternDetectionService(IChartLogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _detectors = new Dictionary<PatternType, IPatternDetector>();
            
            InitializeDetectors();
        }

        /// <summary>
        /// Detect patterns in the provided price data
        /// </summary>
        public async Task<PatternDetectionResult> DetectPatternsAsync(
            string symbol, 
            List<CandlestickData> priceData, 
            PatternSearchCriteria? criteria = null)
        {
            var startTime = DateTime.Now;
            var result = new PatternDetectionResult();
            
            try
            {
                if (string.IsNullOrWhiteSpace(symbol))
                {
                    result.Errors.Add("Symbol is required");
                    return result;
                }

                if (priceData?.Count < 10)
                {
                    result.Warnings.Add("Insufficient data for reliable pattern detection (minimum 10 candles required)");
                    return result;
                }

                // Sort data by time to ensure proper analysis
                var sortedData = priceData.OrderBy(d => d.Timestamp).ToList();
                
                // Detect patterns based on criteria
                var patternsToDetect = GetPatternsToDetect(criteria);
                
                foreach (var patternType in patternsToDetect)
                {
                    if (_detectors.TryGetValue(patternType, out var detector))
                    {
                        try
                        {
                            var detectedPatterns = await detector.DetectAsync(symbol, sortedData);
                            result.Patterns.AddRange(detectedPatterns);
                            result.PatternCounts[patternType] = detectedPatterns.Count;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"Error detecting {patternType} patterns for {symbol}: {ex.Message}", ex);
                            result.Errors.Add($"Failed to detect {patternType}: {ex.Message}");
                        }
                    }
                }

                // Apply post-detection filtering
                result.Patterns = ApplyFiltering(result.Patterns, criteria).ToList();
                result.TotalFound = result.Patterns.Count;
                
                _logger.Info($"Pattern detection completed for {symbol}: {result.TotalFound} patterns found");
            }
            catch (Exception ex)
            {
                _logger.Error($"Pattern detection failed for {symbol}: {ex.Message}", ex);
                result.Errors.Add($"Detection failed: {ex.Message}");
            }
            finally
            {
                result.DetectionTime = DateTime.Now - startTime;
            }

            return result;
        }

        /// <summary>
        /// Detect patterns for multiple symbols
        /// </summary>
        public async Task<Dictionary<string, PatternDetectionResult>> DetectPatternsForSymbolsAsync(
            Dictionary<string, List<CandlestickData>> symbolData,
            PatternSearchCriteria? criteria = null)
        {
            var results = new Dictionary<string, PatternDetectionResult>();
            
            var tasks = symbolData.Select(async kvp =>
            {
                var result = await DetectPatternsAsync(kvp.Key, kvp.Value, criteria);
                return new { Symbol = kvp.Key, Result = result };
            });

            var completedTasks = await Task.WhenAll(tasks);
            
            foreach (var task in completedTasks)
            {
                results[task.Symbol] = task.Result;
            }

            return results;
        }

        /// <summary>
        /// Get real-time pattern updates for active patterns
        /// </summary>
        public async Task<List<PatternBase>> UpdateActivePatternsAsync(
            string symbol,
            List<CandlestickData> newData,
            List<PatternBase> existingPatterns)
        {
            var updatedPatterns = new List<PatternBase>();

            foreach (var pattern in existingPatterns.Where(p => p.IsActive()))
            {
                try
                {
                    if (_detectors.TryGetValue(pattern.Type, out var detector))
                    {
                        var updated = await detector.UpdatePatternAsync(pattern, newData);
                        if (updated != null)
                        {
                            updatedPatterns.Add(updated);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error($"Error updating pattern {pattern.Id} for {symbol}: {ex.Message}", ex);
                }
            }

            return updatedPatterns;
        }

        /// <summary>
        /// Register a custom pattern detector
        /// </summary>
        public void RegisterDetector(PatternType patternType, IPatternDetector detector)
        {
            _detectors[patternType] = detector;
            _logger.Info($"Registered detector for {patternType}");
        }

        /// <summary>
        /// Get available pattern types
        /// </summary>
        public List<PatternType> GetAvailablePatternTypes()
        {
            return _detectors.Keys.ToList();
        }

        private void InitializeDetectors()
        {
            // Register built-in pattern detectors
            _detectors[PatternType.Triangle] = new TriangleDetector(_logger);
            _detectors[PatternType.AscendingTriangle] = new TriangleDetector(_logger);
            _detectors[PatternType.DescendingTriangle] = new TriangleDetector(_logger);
            _detectors[PatternType.HeadAndShoulders] = new HeadAndShouldersDetector(_logger);
            _detectors[PatternType.InverseHeadAndShoulders] = new HeadAndShouldersDetector(_logger);
            _detectors[PatternType.DoubleTop] = new DoubleTopBottomDetector(_logger);
            _detectors[PatternType.DoubleBottom] = new DoubleTopBottomDetector(_logger);
            _detectors[PatternType.SupportLevel] = new SupportResistanceDetector(_logger);
            _detectors[PatternType.ResistanceLevel] = new SupportResistanceDetector(_logger);
            _detectors[PatternType.Flag] = new FlagDetector(_logger);
            _detectors[PatternType.BullishFlag] = new FlagDetector(_logger);
            _detectors[PatternType.BearishFlag] = new FlagDetector(_logger);
            
            // Candlestick patterns
            _detectors[PatternType.Doji] = new CandlestickPatternDetector(_logger);
            _detectors[PatternType.Hammer] = new CandlestickPatternDetector(_logger);
            _detectors[PatternType.ShootingStar] = new CandlestickPatternDetector(_logger);
            _detectors[PatternType.BullishEngulfing] = new CandlestickPatternDetector(_logger);
            _detectors[PatternType.BearishEngulfing] = new CandlestickPatternDetector(_logger);
        }

        private List<PatternType> GetPatternsToDetect(PatternSearchCriteria? criteria)
        {
            if (criteria?.PatternTypes?.Any() == true)
            {
                return criteria.PatternTypes.Where(pt => _detectors.ContainsKey(pt)).ToList();
            }

            return _detectors.Keys.ToList();
        }

        private IEnumerable<PatternBase> ApplyFiltering(List<PatternBase> patterns, PatternSearchCriteria? criteria)
        {
            if (criteria == null) return patterns;

            var filtered = patterns.AsEnumerable();

            if (criteria.Directions?.Any() == true)
                filtered = filtered.Where(p => criteria.Directions.Contains(p.Direction));

            if (criteria.Statuses?.Any() == true)
                filtered = filtered.Where(p => criteria.Statuses.Contains(p.Status));

            if (criteria.MinReliability.HasValue)
                filtered = filtered.Where(p => p.Reliability >= criteria.MinReliability.Value);

            if (criteria.MinConfidence.HasValue)
                filtered = filtered.Where(p => p.ConfidenceScore >= criteria.MinConfidence.Value);

            if (criteria.StartDate.HasValue)
                filtered = filtered.Where(p => p.StartTime >= criteria.StartDate.Value);

            if (criteria.EndDate.HasValue)
                filtered = filtered.Where(p => p.EndTime <= criteria.EndDate.Value);

            if (!string.IsNullOrWhiteSpace(criteria.SearchText))
            {
                var searchText = criteria.SearchText.ToLowerInvariant();
                filtered = filtered.Where(p => 
                    p.Name.ToLowerInvariant().Contains(searchText) ||
                    p.Description.ToLowerInvariant().Contains(searchText) ||
                    p.Symbol.ToLowerInvariant().Contains(searchText));
            }

            return filtered;
        }
    }

    /// <summary>
    /// Interface for pattern detection algorithms
    /// </summary>
    public interface IPatternDetector
    {
        Task<List<PatternBase>> DetectAsync(string symbol, List<CandlestickData> data);
        Task<PatternBase?> UpdatePatternAsync(PatternBase existingPattern, List<CandlestickData> newData);
    }

    /// <summary>
    /// Triangle pattern detector implementation
    /// </summary>
    public class TriangleDetector : IPatternDetector
    {
        private readonly IChartLogger _logger;

        public TriangleDetector(IChartLogger logger)
        {
            _logger = logger;
        }

        public async Task<List<PatternBase>> DetectAsync(string symbol, List<CandlestickData> data)
        {
            var patterns = new List<PatternBase>();
            
            if (data.Count < 20) return patterns; // Need sufficient data for triangle detection

            try
            {
                // Find potential triangle formations
                var highs = FindPivotHighs(data);
                var lows = FindPivotLows(data);

                for (int i = 0; i < highs.Count - 1; i++)
                {
                    for (int j = i + 1; j < highs.Count; j++)
                    {
                        for (int k = 0; k < lows.Count - 1; k++)
                        {
                            for (int l = k + 1; l < lows.Count; l++)
                            {
                                var triangle = AnalyzeTriangleCandidate(symbol, data, highs[i], highs[j], lows[k], lows[l]);
                                if (triangle != null)
                                {
                                    patterns.Add(triangle);
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in triangle detection for {symbol}: {ex.Message}", ex);
            }

            return await Task.FromResult(patterns);
        }

        public async Task<PatternBase?> UpdatePatternAsync(PatternBase existingPattern, List<CandlestickData> newData)
        {
            if (existingPattern is not TrianglePattern triangle) return null;

            // Update triangle pattern with new data
            var lastCandle = newData.LastOrDefault();
            if (lastCandle == null) return triangle;

            // Check for breakout
            if (!triangle.IsBreakoutConfirmed && lastCandle.Close > triangle.BreakoutLevel)
            {
                triangle.IsBreakoutConfirmed = true;
                triangle.Status = PatternStatus.Confirmed;
                triangle.ConfidenceScore = Math.Min(triangle.ConfidenceScore + 0.2m, 1.0m);
            }

            return await Task.FromResult(triangle);
        }

        private List<PatternPoint> FindPivotHighs(List<CandlestickData> data, int lookback = 5)
        {
            var pivots = new List<PatternPoint>();
            
            for (int i = lookback; i < data.Count - lookback; i++)
            {
                var current = data[i];
                bool isPivot = true;
                
                // Check if current high is higher than surrounding highs
                for (int j = i - lookback; j <= i + lookback; j++)
                {
                    if (j != i && data[j].High >= current.High)
                    {
                        isPivot = false;
                        break;
                    }
                }
                
                if (isPivot)
                {
                    pivots.Add(new PatternPoint(current.Timestamp, current.High, "Pivot High"));
                }
            }
            
            return pivots;
        }

        private List<PatternPoint> FindPivotLows(List<CandlestickData> data, int lookback = 5)
        {
            var pivots = new List<PatternPoint>();
            
            for (int i = lookback; i < data.Count - lookback; i++)
            {
                var current = data[i];
                bool isPivot = true;
                
                // Check if current low is lower than surrounding lows
                for (int j = i - lookback; j <= i + lookback; j++)
                {
                    if (j != i && data[j].Low <= current.Low)
                    {
                        isPivot = false;
                        break;
                    }
                }
                
                if (isPivot)
                {
                    pivots.Add(new PatternPoint(current.Timestamp, current.Low, "Pivot Low"));
                }
            }
            
            return pivots;
        }

        private TrianglePattern? AnalyzeTriangleCandidate(
            string symbol, 
            List<CandlestickData> data,
            PatternPoint high1, 
            PatternPoint high2, 
            PatternPoint low1, 
            PatternPoint low2)
        {
            // Validate time sequence
            if (high1.Time >= high2.Time || low1.Time >= low2.Time) return null;

            // Calculate trend line slopes
            var upperSlope = (high2.Price - high1.Price) / (decimal)(high2.Time - high1.Time).TotalDays;
            var lowerSlope = (low2.Price - low1.Price) / (decimal)(low2.Time - low1.Time).TotalDays;

            // Determine triangle type and validate
            PatternType triangleType;
            PatternDirection direction;
            
            if (Math.Abs(upperSlope) < 0.01m && Math.Abs(lowerSlope) < 0.01m)
            {
                // Both lines relatively flat - not a valid triangle
                return null;
            }
            else if (upperSlope < -0.01m && lowerSlope > 0.01m)
            {
                triangleType = PatternType.SymmetricalTriangle;
                direction = PatternDirection.Neutral;
            }
            else if (Math.Abs(upperSlope) < 0.01m && lowerSlope > 0.01m)
            {
                triangleType = PatternType.AscendingTriangle;
                direction = PatternDirection.Bullish;
            }
            else if (upperSlope < -0.01m && Math.Abs(lowerSlope) < 0.01m)
            {
                triangleType = PatternType.DescendingTriangle;
                direction = PatternDirection.Bearish;
            }
            else
            {
                return null; // Not a valid triangle pattern
            }

            // Create triangle pattern
            var triangle = new TrianglePattern
            {
                Symbol = symbol,
                Type = triangleType,
                Direction = direction,
                Status = PatternStatus.Forming,
                Reliability = PatternReliability.Medium,
                ConfidenceScore = 0.7m,
                StartTime = new[] { high1.Time, high2.Time, low1.Time, low2.Time }.Min(),
                EndTime = new[] { high1.Time, high2.Time, low1.Time, low2.Time }.Max(),
                UpperTrendLine1 = high1,
                UpperTrendLine2 = high2,
                LowerTrendLine1 = low1,
                LowerTrendLine2 = low2,
                MinPrice = Math.Min(low1.Price, low2.Price),
                MaxPrice = Math.Max(high1.Price, high2.Price)
            };

            // Calculate breakout level (typically at the convergence point)
            triangle.BreakoutLevel = (triangle.MaxPrice + triangle.MinPrice) / 2;
            triangle.TargetPrice = triangle.BreakoutLevel + (triangle.GetPatternHeight() * 0.75m);

            triangle.KeyPoints = new List<PatternPoint> { high1, high2, low1, low2 };
            triangle.Description = $"{triangleType} pattern with {direction.ToString().ToLower()} bias";
            triangle.Formation = "Converging trend lines forming a triangle shape";
            triangle.Implications = direction == PatternDirection.Bullish ? "Potential upward breakout" :
                                   direction == PatternDirection.Bearish ? "Potential downward breakout" :
                                   "Breakout direction depends on which trend line breaks first";

            return triangle;
        }
    }

    /// <summary>
    /// Support and Resistance level detector
    /// </summary>
    public class SupportResistanceDetector : IPatternDetector
    {
        private readonly IChartLogger _logger;

        public SupportResistanceDetector(IChartLogger logger)
        {
            _logger = logger;
        }

        public async Task<List<PatternBase>> DetectAsync(string symbol, List<CandlestickData> data)
        {
            var patterns = new List<PatternBase>();
            
            if (data.Count < 20) return patterns;

            try
            {
                // Find support levels
                var supportLevels = FindSupportLevels(symbol, data);
                patterns.AddRange(supportLevels);

                // Find resistance levels
                var resistanceLevels = FindResistanceLevels(symbol, data);
                patterns.AddRange(resistanceLevels);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error in support/resistance detection for {symbol}: {ex.Message}", ex);
            }

            return await Task.FromResult(patterns);
        }

        public async Task<PatternBase?> UpdatePatternAsync(PatternBase existingPattern, List<CandlestickData> newData)
        {
            if (existingPattern is not SupportResistancePattern srPattern) return null;

            var lastCandle = newData.LastOrDefault();
            if (lastCandle == null) return srPattern;

            // Check if level was touched again
            var tolerance = srPattern.Level * 0.005m; // 0.5% tolerance
            
            if (srPattern.Type == PatternType.SupportLevel)
            {
                if (lastCandle.Low <= srPattern.Level + tolerance && lastCandle.Low >= srPattern.Level - tolerance)
                {
                    srPattern.TouchCount++;
                    srPattern.TouchPoints.Add(new PatternPoint(lastCandle.Timestamp, lastCandle.Low, "Support Touch"));
                    srPattern.Strength = Math.Min(srPattern.Strength + 0.1m, 1.0m);
                }
            }
            else if (srPattern.Type == PatternType.ResistanceLevel)
            {
                if (lastCandle.High >= srPattern.Level - tolerance && lastCandle.High <= srPattern.Level + tolerance)
                {
                    srPattern.TouchCount++;
                    srPattern.TouchPoints.Add(new PatternPoint(lastCandle.Timestamp, lastCandle.High, "Resistance Touch"));
                    srPattern.Strength = Math.Min(srPattern.Strength + 0.1m, 1.0m);
                }
            }

            return await Task.FromResult(srPattern);
        }

        private List<SupportResistancePattern> FindSupportLevels(string symbol, List<CandlestickData> data)
        {
            var levels = new List<SupportResistancePattern>();
            var lows = data.Select(d => d.Low).ToList();
            
            // Group similar lows together
            var priceGroups = GroupSimilarPrices(lows, 0.01m); // 1% grouping
            
            foreach (var group in priceGroups.Where(g => g.Value.Count >= 2))
            {
                var level = group.Key;
                var touchPoints = new List<PatternPoint>();
                
                foreach (var price in group.Value)
                {
                    var candle = data.FirstOrDefault(d => Math.Abs(d.Low - price) < 0.001m);
                    if (candle != null)
                    {
                        touchPoints.Add(new PatternPoint(candle.Timestamp, candle.Low, "Support Touch"));
                    }
                }

                if (touchPoints.Count >= 2)
                {
                    var support = new SupportResistancePattern
                    {
                        Symbol = symbol,
                        Name = "Support Level",
                        Type = PatternType.SupportLevel,
                        Direction = PatternDirection.Bullish,
                        Status = PatternStatus.Forming,
                        Reliability = touchPoints.Count >= 3 ? PatternReliability.High : PatternReliability.Medium,
                        Level = level,
                        TouchCount = touchPoints.Count,
                        TouchPoints = touchPoints,
                        Strength = Math.Min(touchPoints.Count * 0.2m, 1.0m),
                        StartTime = touchPoints.Min(tp => tp.Time),
                        EndTime = touchPoints.Max(tp => tp.Time),
                        MinPrice = level,
                        MaxPrice = level,
                        ConfidenceScore = Math.Min(0.5m + (touchPoints.Count * 0.1m), 1.0m)
                    };

                    support.KeyPoints = touchPoints;
                    support.Description = $"Support level at {level:C2} with {touchPoints.Count} touches";
                    support.Formation = "Multiple price rejections at similar level forming support";
                    support.Implications = "Price likely to bounce higher from this level";

                    levels.Add(support);
                }
            }

            return levels;
        }

        private List<SupportResistancePattern> FindResistanceLevels(string symbol, List<CandlestickData> data)
        {
            var levels = new List<SupportResistancePattern>();
            var highs = data.Select(d => d.High).ToList();
            
            // Group similar highs together
            var priceGroups = GroupSimilarPrices(highs, 0.01m); // 1% grouping
            
            foreach (var group in priceGroups.Where(g => g.Value.Count >= 2))
            {
                var level = group.Key;
                var touchPoints = new List<PatternPoint>();
                
                foreach (var price in group.Value)
                {
                    var candle = data.FirstOrDefault(d => Math.Abs(d.High - price) < 0.001m);
                    if (candle != null)
                    {
                        touchPoints.Add(new PatternPoint(candle.Timestamp, candle.High, "Resistance Touch"));
                    }
                }

                if (touchPoints.Count >= 2)
                {
                    var resistance = new SupportResistancePattern
                    {
                        Symbol = symbol,
                        Name = "Resistance Level",
                        Type = PatternType.ResistanceLevel,
                        Direction = PatternDirection.Bearish,
                        Status = PatternStatus.Forming,
                        Reliability = touchPoints.Count >= 3 ? PatternReliability.High : PatternReliability.Medium,
                        Level = level,
                        TouchCount = touchPoints.Count,
                        TouchPoints = touchPoints,
                        Strength = Math.Min(touchPoints.Count * 0.2m, 1.0m),
                        StartTime = touchPoints.Min(tp => tp.Time),
                        EndTime = touchPoints.Max(tp => tp.Time),
                        MinPrice = level,
                        MaxPrice = level,
                        ConfidenceScore = Math.Min(0.5m + (touchPoints.Count * 0.1m), 1.0m)
                    };

                    resistance.KeyPoints = touchPoints;
                    resistance.Description = $"Resistance level at {level:C2} with {touchPoints.Count} touches";
                    resistance.Formation = "Multiple price rejections at similar level forming resistance";
                    resistance.Implications = "Price likely to reverse lower from this level";

                    levels.Add(resistance);
                }
            }

            return levels;
        }

        private Dictionary<decimal, List<decimal>> GroupSimilarPrices(List<decimal> prices, decimal tolerance)
        {
            var groups = new Dictionary<decimal, List<decimal>>();
            
            foreach (var price in prices)
            {
                var existingGroup = groups.Keys.FirstOrDefault(k => Math.Abs(k - price) / k <= tolerance);
                
                if (existingGroup != 0)
                {
                    groups[existingGroup].Add(price);
                }
                else
                {
                    groups[price] = new List<decimal> { price };
                }
            }
            
            return groups;
        }
    }

    /// <summary>
    /// Placeholder detectors for other pattern types
    /// </summary>
    public class HeadAndShouldersDetector : IPatternDetector
    {
        private readonly IChartLogger _logger;

        public HeadAndShouldersDetector(IChartLogger logger)
        {
            _logger = logger;
        }

        public async Task<List<PatternBase>> DetectAsync(string symbol, List<CandlestickData> data)
        {
            // TODO [REVIEWED]: Implement head and shoulders detection algorithm
            return await Task.FromResult(new List<PatternBase>());
        }

        public async Task<PatternBase?> UpdatePatternAsync(PatternBase existingPattern, List<CandlestickData> newData)
        {
            return await Task.FromResult(existingPattern);
        }
    }

    public class DoubleTopBottomDetector : IPatternDetector
    {
        private readonly IChartLogger _logger;

        public DoubleTopBottomDetector(IChartLogger logger)
        {
            _logger = logger;
        }

        public async Task<List<PatternBase>> DetectAsync(string symbol, List<CandlestickData> data)
        {
            // TODO [REVIEWED]: Implement double top/bottom detection algorithm
            return await Task.FromResult(new List<PatternBase>());
        }

        public async Task<PatternBase?> UpdatePatternAsync(PatternBase existingPattern, List<CandlestickData> newData)
        {
            return await Task.FromResult(existingPattern);
        }
    }

    public class FlagDetector : IPatternDetector
    {
        private readonly IChartLogger _logger;

        public FlagDetector(IChartLogger logger)
        {
            _logger = logger;
        }

        public async Task<List<PatternBase>> DetectAsync(string symbol, List<CandlestickData> data)
        {
            // TODO [REVIEWED]: Implement flag pattern detection algorithm
            return await Task.FromResult(new List<PatternBase>());
        }

        public async Task<PatternBase?> UpdatePatternAsync(PatternBase existingPattern, List<CandlestickData> newData)
        {
            return await Task.FromResult(existingPattern);
        }
    }

    public class CandlestickPatternDetector : IPatternDetector
    {
        private readonly IChartLogger _logger;

        public CandlestickPatternDetector(IChartLogger logger)
        {
            _logger = logger;
        }

        public async Task<List<PatternBase>> DetectAsync(string symbol, List<CandlestickData> data)
        {
            // TODO [REVIEWED]: Implement candlestick pattern detection algorithms
            return await Task.FromResult(new List<PatternBase>());
        }

        public async Task<PatternBase?> UpdatePatternAsync(PatternBase existingPattern, List<CandlestickData> newData)
        {
            return await Task.FromResult(existingPattern);
        }
    }
}
