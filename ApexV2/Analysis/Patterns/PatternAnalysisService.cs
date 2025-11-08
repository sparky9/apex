using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ApexV2.Core.Logging;
using ApexV2.Charts.Drawing;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Analysis.Patterns
{
    /// <summary>
    /// Advanced pattern analysis service providing insights, predictions, and strategic recommendations
    /// </summary>
    public class PatternAnalysisService
    {
        private readonly IChartLogger _logger;
        private readonly PatternManagementService _managementService;

        public PatternAnalysisService(IChartLogger logger, PatternManagementService managementService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _managementService = managementService ?? throw new ArgumentNullException(nameof(managementService));
        }

        /// <summary>
        /// Analyze patterns for a symbol and provide trading insights
        /// </summary>
        public async Task<PatternAnalysisResult> AnalyzeSymbolPatternsAsync(
            string symbol, 
            List<CandlestickData> priceData)
        {
            var result = new PatternAnalysisResult { Symbol = symbol };

            try
            {
                var patterns = await _managementService.GetPatternsForSymbolAsync(symbol);
                result.TotalPatterns = patterns.Count;

                if (!patterns.Any())
                {
                    result.Summary = "No patterns detected for this symbol.";
                    result.Recommendation = PatternRecommendation.Hold;
                    return result;
                }

                // Analyze current market context
                result.MarketContext = AnalyzeMarketContext(priceData);
                
                // Categorize patterns by significance
                result.SignificantPatterns = patterns
                    .Where(p => p.ConfidenceScore >= 0.7m && p.IsActive())
                    .OrderByDescending(p => p.ConfidenceScore)
                    .ToList();

                result.RecentPatterns = patterns
                    .Where(p => DateTime.Now - p.DetectedAt <= TimeSpan.FromDays(7))
                    .OrderByDescending(p => p.DetectedAt)
                    .ToList();

                // Analyze pattern conflicts and convergence
                result.PatternConflicts = AnalyzePatternConflicts(patterns);
                result.PatternConvergence = AnalyzePatternConvergence(patterns);

                // Generate insights
                result.BullishSignals = GenerateBullishSignals(patterns, priceData);
                result.BearishSignals = GenerateBearishSignals(patterns, priceData);
                result.NeutralSignals = GenerateNeutralSignals(patterns, priceData);

                // Calculate support and resistance levels
                result.KeyLevels = CalculateKeyLevels(patterns, priceData);

                // Generate overall recommendation
                result.Recommendation = GenerateRecommendation(result);
                result.Confidence = CalculateRecommendationConfidence(result);
                result.Summary = GenerateSummary(result);

                // Risk assessment
                result.RiskFactors = AnalyzeRiskFactors(patterns, priceData);
                result.RiskLevel = CalculateRiskLevel(result.RiskFactors);

                _logger.Info($"Pattern analysis completed for {symbol}: {result.Recommendation} ({result.Confidence:P1})");
            }
            catch (Exception ex)
            {
                _logger.Error($"Error analyzing patterns for {symbol}: {ex.Message}", ex);
                result.Errors.Add($"Analysis failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Get pattern-based price targets and stop levels
        /// </summary>
        public async Task<PatternTradingLevels> GetTradingLevelsAsync(string symbol)
        {
            var levels = new PatternTradingLevels { Symbol = symbol };

            try
            {
                var patterns = await _managementService.GetPatternsForSymbolAsync(symbol);
                var activePatterns = patterns.Where(p => p.IsActive()).ToList();

                foreach (var pattern in activePatterns)
                {
                    switch (pattern)
                    {
                        case TrianglePattern triangle:
                            if (triangle.TargetPrice > 0)
                            {
                                levels.BullishTargets.Add(new PriceTarget
                                {
                                    Price = triangle.TargetPrice,
                                    Confidence = triangle.ConfidenceScore,
                                    Reason = $"{triangle.Type} breakout target",
                                    PatternId = triangle.Id
                                });
                            }
                            break;

                        case SupportResistancePattern sr:
                            if (sr.Type == PatternType.SupportLevel)
                            {
                                levels.SupportLevels.Add(new PriceLevel
                                {
                                    Price = sr.Level,
                                    Strength = sr.Strength,
                                    Description = $"Support level with {sr.TouchCount} touches",
                                    PatternId = sr.Id
                                });
                            }
                            else if (sr.Type == PatternType.ResistanceLevel)
                            {
                                levels.ResistanceLevels.Add(new PriceLevel
                                {
                                    Price = sr.Level,
                                    Strength = sr.Strength,
                                    Description = $"Resistance level with {sr.TouchCount} touches",
                                    PatternId = sr.Id
                                });
                            }
                            break;

                        case HeadAndShouldersPattern hs:
                            if (hs.TargetPrice > 0)
                            {
                                levels.BearishTargets.Add(new PriceTarget
                                {
                                    Price = hs.TargetPrice,
                                    Confidence = hs.ConfidenceScore,
                                    Reason = "Head and shoulders target",
                                    PatternId = hs.Id
                                });
                            }
                            break;
                    }
                }

                // Sort levels by strength/confidence
                levels.SupportLevels = levels.SupportLevels.OrderByDescending(l => l.Strength).ToList();
                levels.ResistanceLevels = levels.ResistanceLevels.OrderByDescending(l => l.Strength).ToList();
                levels.BullishTargets = levels.BullishTargets.OrderByDescending(t => t.Confidence).ToList();
                levels.BearishTargets = levels.BearishTargets.OrderByDescending(t => t.Confidence).ToList();
            }
            catch (Exception ex)
            {
                _logger.Error($"Error calculating trading levels for {symbol}: {ex.Message}", ex);
            }

            return levels;
        }

        /// <summary>
        /// Compare patterns across multiple symbols
        /// </summary>
        public async Task<PatternComparisonResult> CompareSymbolPatternsAsync(List<string> symbols)
        {
            var result = new PatternComparisonResult();

            try
            {
                foreach (var symbol in symbols)
                {
                    var patterns = await _managementService.GetPatternsForSymbolAsync(symbol);
                    result.SymbolPatterns[symbol] = patterns;
                }

                // Find common patterns
                result.CommonPatterns = FindCommonPatterns(result.SymbolPatterns);
                
                // Rank symbols by pattern strength
                result.SymbolRankings = RankSymbolsByPatternStrength(result.SymbolPatterns);

                // Sector/correlation analysis if symbols are related
                result.CorrelationInsights = AnalyzePatternCorrelations(result.SymbolPatterns);
            }
            catch (Exception ex)
            {
                _logger.Error($"Error comparing patterns across symbols: {ex.Message}", ex);
                result.Errors.Add($"Comparison failed: {ex.Message}");
            }

            return result;
        }

        private MarketContext AnalyzeMarketContext(List<CandlestickData> priceData)
        {
            if (priceData.Count < 20) return MarketContext.Insufficient;

            var recentData = priceData.TakeLast(20).ToList();
            var trend = CalculateTrend(recentData);
            var volatility = CalculateVolatility(recentData);

            if (volatility > 0.05m) return MarketContext.HighVolatility;
            if (trend > 0.02m) return MarketContext.BullishTrend;
            if (trend < -0.02m) return MarketContext.BearishTrend;
            
            return MarketContext.Sideways;
        }

        private decimal CalculateTrend(List<CandlestickData> data)
        {
            if (data.Count < 2) return 0;

            var firstPrice = data.First().Close;
            var lastPrice = data.Last().Close;
            
            return (lastPrice - firstPrice) / firstPrice;
        }

        private decimal CalculateVolatility(List<CandlestickData> data)
        {
            if (data.Count < 2) return 0;

            var returns = new List<decimal>();
            for (int i = 1; i < data.Count; i++)
            {
                var ret = (data[i].Close - data[i - 1].Close) / data[i - 1].Close;
                returns.Add(ret);
            }

            var mean = returns.Average();
            var variance = returns.Select(r => (r - mean) * (r - mean)).Average();
            
            return (decimal)Math.Sqrt((double)variance);
        }

        private List<PatternConflict> AnalyzePatternConflicts(List<PatternBase> patterns)
        {
            var conflicts = new List<PatternConflict>();

            for (int i = 0; i < patterns.Count; i++)
            {
                for (int j = i + 1; j < patterns.Count; j++)
                {
                    var pattern1 = patterns[i];
                    var pattern2 = patterns[j];

                    if (pattern1.Direction != pattern2.Direction && 
                        PatternsOverlap(pattern1, pattern2))
                    {
                        conflicts.Add(new PatternConflict
                        {
                            Pattern1 = pattern1,
                            Pattern2 = pattern2,
                            ConflictType = ConflictType.DirectionalConflict,
                            Description = $"{pattern1.Name} suggests {pattern1.Direction} while {pattern2.Name} suggests {pattern2.Direction}"
                        });
                    }
                }
            }

            return conflicts;
        }

        private List<PatternConvergence> AnalyzePatternConvergence(List<PatternBase> patterns)
        {
            var convergences = new List<PatternConvergence>();

            var groupedPatterns = patterns
                .Where(p => p.IsActive())
                .GroupBy(p => p.Direction)
                .Where(g => g.Count() > 1);

            foreach (var group in groupedPatterns)
            {
                var patternList = group.ToList();
                var avgConfidence = patternList.Average(p => p.ConfidenceScore);

                convergences.Add(new PatternConvergence
                {
                    Direction = group.Key,
                    Patterns = patternList,
                    Strength = avgConfidence,
                    Description = $"{patternList.Count} patterns converging on {group.Key} direction"
                });
            }

            return convergences;
        }

        private bool PatternsOverlap(PatternBase pattern1, PatternBase pattern2)
        {
            return pattern1.StartTime <= pattern2.EndTime && pattern2.StartTime <= pattern1.EndTime;
        }

        private List<PatternSignal> GenerateBullishSignals(List<PatternBase> patterns, List<CandlestickData> priceData)
        {
            var signals = new List<PatternSignal>();

            foreach (var pattern in patterns.Where(p => p.Direction == PatternDirection.Bullish && p.IsActive()))
            {
                signals.Add(new PatternSignal
                {
                    Pattern = pattern,
                    SignalType = SignalType.Bullish,
                    Strength = pattern.ConfidenceScore,
                    Description = $"{pattern.Name}: {pattern.Implications}"
                });
            }

            return signals.OrderByDescending(s => s.Strength).ToList();
        }

        private List<PatternSignal> GenerateBearishSignals(List<PatternBase> patterns, List<CandlestickData> priceData)
        {
            var signals = new List<PatternSignal>();

            foreach (var pattern in patterns.Where(p => p.Direction == PatternDirection.Bearish && p.IsActive()))
            {
                signals.Add(new PatternSignal
                {
                    Pattern = pattern,
                    SignalType = SignalType.Bearish,
                    Strength = pattern.ConfidenceScore,
                    Description = $"{pattern.Name}: {pattern.Implications}"
                });
            }

            return signals.OrderByDescending(s => s.Strength).ToList();
        }

        private List<PatternSignal> GenerateNeutralSignals(List<PatternBase> patterns, List<CandlestickData> priceData)
        {
            var signals = new List<PatternSignal>();

            foreach (var pattern in patterns.Where(p => p.Direction == PatternDirection.Neutral && p.IsActive()))
            {
                signals.Add(new PatternSignal
                {
                    Pattern = pattern,
                    SignalType = SignalType.Neutral,
                    Strength = pattern.ConfidenceScore,
                    Description = $"{pattern.Name}: {pattern.Implications}"
                });
            }

            return signals.OrderByDescending(s => s.Strength).ToList();
        }

        private List<KeyLevel> CalculateKeyLevels(List<PatternBase> patterns, List<CandlestickData> priceData)
        {
            var levels = new List<KeyLevel>();

            foreach (var pattern in patterns)
            {
                if (pattern is SupportResistancePattern sr)
                {
                    levels.Add(new KeyLevel
                    {
                        Price = sr.Level,
                        Type = sr.Type == PatternType.SupportLevel ? KeyLevelType.Support : KeyLevelType.Resistance,
                        Strength = sr.Strength,
                        Description = $"{sr.Type} with {sr.TouchCount} touches"
                    });
                }
            }

            return levels.OrderBy(l => l.Price).ToList();
        }

        private PatternRecommendation GenerateRecommendation(PatternAnalysisResult result)
        {
            var bullishScore = result.BullishSignals.Sum(s => s.Strength);
            var bearishScore = result.BearishSignals.Sum(s => s.Strength);
            var neutralScore = result.NeutralSignals.Sum(s => s.Strength);

            if (bullishScore > bearishScore + 0.5m && bullishScore > neutralScore)
                return PatternRecommendation.Buy;
            
            if (bearishScore > bullishScore + 0.5m && bearishScore > neutralScore)
                return PatternRecommendation.Sell;

            return PatternRecommendation.Hold;
        }

        private decimal CalculateRecommendationConfidence(PatternAnalysisResult result)
        {
            if (!result.SignificantPatterns.Any()) return 0;

            var avgConfidence = result.SignificantPatterns.Average(p => p.ConfidenceScore);
            var convergenceBonus = result.PatternConvergence.Any() ? 0.1m : 0;
            var conflictPenalty = result.PatternConflicts.Any() ? 0.2m : 0;

            return Math.Max(0, Math.Min(1, avgConfidence + convergenceBonus - conflictPenalty));
        }

        private string GenerateSummary(PatternAnalysisResult result)
        {
            var summary = $"Analysis of {result.TotalPatterns} patterns detected. ";
            
            if (result.SignificantPatterns.Any())
            {
                summary += $"{result.SignificantPatterns.Count} significant patterns identified. ";
            }

            summary += $"Overall recommendation: {result.Recommendation} with {result.Confidence:P1} confidence.";
            
            if (result.PatternConflicts.Any())
            {
                summary += $" Note: {result.PatternConflicts.Count} pattern conflicts detected.";
            }

            return summary;
        }

        private List<RiskFactor> AnalyzeRiskFactors(List<PatternBase> patterns, List<CandlestickData> priceData)
        {
            var riskFactors = new List<RiskFactor>();

            // Pattern reliability risk
            var lowConfidencePatterns = patterns.Count(p => p.ConfidenceScore < 0.6m);
            if (lowConfidencePatterns > 0)
            {
                riskFactors.Add(new RiskFactor
                {
                    Type = RiskType.PatternReliability,
                    Level = RiskLevel.Medium,
                    Description = $"{lowConfidencePatterns} patterns have low confidence scores"
                });
            }

            // Conflicting signals risk
            var conflictingPatterns = patterns.GroupBy(p => p.Direction).Count();
            if (conflictingPatterns > 1)
            {
                riskFactors.Add(new RiskFactor
                {
                    Type = RiskType.ConflictingSignals,
                    Level = RiskLevel.High,
                    Description = "Multiple patterns suggest different directions"
                });
            }

            return riskFactors;
        }

        private RiskLevel CalculateRiskLevel(List<RiskFactor> riskFactors)
        {
            if (riskFactors.Any(rf => rf.Level == RiskLevel.High))
                return RiskLevel.High;
            
            if (riskFactors.Any(rf => rf.Level == RiskLevel.Medium))
                return RiskLevel.Medium;

            return RiskLevel.Low;
        }

        private Dictionary<PatternType, int> FindCommonPatterns(Dictionary<string, List<PatternBase>> symbolPatterns)
        {
            var patternCounts = new Dictionary<PatternType, int>();

            foreach (var patterns in symbolPatterns.Values)
            {
                var distinctPatterns = patterns.Select(p => p.Type).Distinct();
                foreach (var patternType in distinctPatterns)
                {
                    patternCounts[patternType] = patternCounts.GetValueOrDefault(patternType, 0) + 1;
                }
            }

            return patternCounts.Where(kvp => kvp.Value > 1).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        private List<SymbolRanking> RankSymbolsByPatternStrength(Dictionary<string, List<PatternBase>> symbolPatterns)
        {
            var rankings = new List<SymbolRanking>();

            foreach (var kvp in symbolPatterns)
            {
                var symbol = kvp.Key;
                var patterns = kvp.Value;
                
                var avgConfidence = patterns.Any() ? patterns.Average(p => p.ConfidenceScore) : 0;
                var significantPatterns = patterns.Count(p => p.ConfidenceScore >= 0.7m);

                rankings.Add(new SymbolRanking
                {
                    Symbol = symbol,
                    Score = avgConfidence,
                    SignificantPatternCount = significantPatterns,
                    TotalPatternCount = patterns.Count
                });
            }

            return rankings.OrderByDescending(r => r.Score).ToList();
        }

        private List<string> AnalyzePatternCorrelations(Dictionary<string, List<PatternBase>> symbolPatterns)
        {
            var insights = new List<string>();

            // Find symbols with similar pattern profiles
            var symbols = symbolPatterns.Keys.ToList();
            for (int i = 0; i < symbols.Count; i++)
            {
                for (int j = i + 1; j < symbols.Count; j++)
                {
                    var patterns1 = symbolPatterns[symbols[i]];
                    var patterns2 = symbolPatterns[symbols[j]];
                    
                    var commonTypes = patterns1.Select(p => p.Type)
                        .Intersect(patterns2.Select(p => p.Type))
                        .ToList();

                    if (commonTypes.Count >= 2)
                    {
                        insights.Add($"{symbols[i]} and {symbols[j]} share {commonTypes.Count} common pattern types");
                    }
                }
            }

            return insights;
        }
    }

    // Supporting classes for pattern analysis
    public class PatternAnalysisResult
    {
        public string Symbol { get; set; } = "";
        public int TotalPatterns { get; set; }
        public List<PatternBase> SignificantPatterns { get; set; } = new();
        public List<PatternBase> RecentPatterns { get; set; } = new();
        public List<PatternConflict> PatternConflicts { get; set; } = new();
        public List<PatternConvergence> PatternConvergence { get; set; } = new();
        public List<PatternSignal> BullishSignals { get; set; } = new();
        public List<PatternSignal> BearishSignals { get; set; } = new();
        public List<PatternSignal> NeutralSignals { get; set; } = new();
        public List<KeyLevel> KeyLevels { get; set; } = new();
        public List<RiskFactor> RiskFactors { get; set; } = new();
        public MarketContext MarketContext { get; set; }
        public PatternRecommendation Recommendation { get; set; }
        public decimal Confidence { get; set; }
        public RiskLevel RiskLevel { get; set; }
        public string Summary { get; set; } = "";
        public List<string> Errors { get; set; } = new();
    }

    public class PatternTradingLevels
    {
        public string Symbol { get; set; } = "";
        public List<PriceLevel> SupportLevels { get; set; } = new();
        public List<PriceLevel> ResistanceLevels { get; set; } = new();
        public List<PriceTarget> BullishTargets { get; set; } = new();
        public List<PriceTarget> BearishTargets { get; set; } = new();
    }

    public class PatternComparisonResult
    {
        public Dictionary<string, List<PatternBase>> SymbolPatterns { get; set; } = new();
        public Dictionary<PatternType, int> CommonPatterns { get; set; } = new();
        public List<SymbolRanking> SymbolRankings { get; set; } = new();
        public List<string> CorrelationInsights { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    // Enums and supporting classes
    public enum MarketContext { Insufficient, BullishTrend, BearishTrend, Sideways, HighVolatility }
    public enum PatternRecommendation { Buy, Sell, Hold }
    public enum SignalType { Bullish, Bearish, Neutral }
    public enum KeyLevelType { Support, Resistance, Pivot }
    public enum ConflictType { DirectionalConflict, TimeConflict, PriceConflict }
    public enum RiskType { PatternReliability, ConflictingSignals, MarketVolatility, InsufficientData }
    public enum RiskLevel { Low, Medium, High }

    public class PatternConflict
    {
        public PatternBase Pattern1 { get; set; } = null!;
        public PatternBase Pattern2 { get; set; } = null!;
        public ConflictType ConflictType { get; set; }
        public string Description { get; set; } = "";
    }

    public class PatternConvergence
    {
        public PatternDirection Direction { get; set; }
        public List<PatternBase> Patterns { get; set; } = new();
        public decimal Strength { get; set; }
        public string Description { get; set; } = "";
    }

    public class PatternSignal
    {
        public PatternBase Pattern { get; set; } = null!;
        public SignalType SignalType { get; set; }
        public decimal Strength { get; set; }
        public string Description { get; set; } = "";
    }

    public class KeyLevel
    {
        public decimal Price { get; set; }
        public KeyLevelType Type { get; set; }
        public decimal Strength { get; set; }
        public string Description { get; set; } = "";
    }

    public class PriceLevel
    {
        public decimal Price { get; set; }
        public decimal Strength { get; set; }
        public string Description { get; set; } = "";
        public Guid PatternId { get; set; }
    }

    public class PriceTarget
    {
        public decimal Price { get; set; }
        public decimal Confidence { get; set; }
        public string Reason { get; set; } = "";
        public Guid PatternId { get; set; }
    }

    public class RiskFactor
    {
        public RiskType Type { get; set; }
        public RiskLevel Level { get; set; }
        public string Description { get; set; } = "";
    }

    public class SymbolRanking
    {
        public string Symbol { get; set; } = "";
        public decimal Score { get; set; }
        public int SignificantPatternCount { get; set; }
        public int TotalPatternCount { get; set; }
    }
}
