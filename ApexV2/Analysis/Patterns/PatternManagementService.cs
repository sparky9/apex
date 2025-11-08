using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ApexV2.Charts.Export;
using ApexV2.Charts.Drawing;
using ApexV2.Charts.Models;

namespace ApexV2.Analysis.Patterns
{
    /// <summary>
    /// Central pattern management service for storing, retrieving, and managing detected patterns
    /// </summary>
    public class PatternManagementService
    {
        private readonly IChartLogger _logger;
        private readonly PatternDetectionService _detectionService;
        private readonly ConcurrentDictionary<string, List<PatternBase>> _symbolPatterns;
        private readonly ConcurrentDictionary<Guid, PatternBase> _allPatterns;
        private readonly SemaphoreSlim _patternLock;
        private readonly Timer _cleanupTimer;

        public event EventHandler<PatternEventArgs>? PatternDetected;
        public event EventHandler<PatternEventArgs>? PatternUpdated;
        public event EventHandler<PatternEventArgs>? PatternExpired;
        public event EventHandler<PatternEventArgs>? PatternBreakout;

        public PatternManagementService(IChartLogger logger, PatternDetectionService detectionService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _detectionService = detectionService ?? throw new ArgumentNullException(nameof(detectionService));
            
            _symbolPatterns = new ConcurrentDictionary<string, List<PatternBase>>();
            _allPatterns = new ConcurrentDictionary<Guid, PatternBase>();
            _patternLock = new SemaphoreSlim(1, 1);
            
            // Set up cleanup timer to run every hour
            _cleanupTimer = new Timer(async _ => await CleanupExpiredPatternsAsync(), 
                null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
        }

        /// <summary>
        /// Add a detected pattern to the management system
        /// </summary>
        public async Task<bool> AddPatternAsync(PatternBase pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

            var validationErrors = pattern.Validate();
            if (validationErrors.Any())
            {
                _logger.Warn($"Pattern validation failed: {string.Join(", ", validationErrors)}");
                return false;
            }

            await _patternLock.WaitAsync();
            try
            {
                // Add to global patterns collection
                _allPatterns[pattern.Id] = pattern;

                // Add to symbol-specific collection
                if (!_symbolPatterns.ContainsKey(pattern.Symbol))
                {
                    _symbolPatterns[pattern.Symbol] = new List<PatternBase>();
                }
                _symbolPatterns[pattern.Symbol].Add(pattern);

                _logger.Info($"Added {pattern.Type} pattern for {pattern.Symbol} with confidence {pattern.ConfidenceScore:P1}");
                
                // Fire event
                PatternDetected?.Invoke(this, new PatternEventArgs(pattern));
                
                return true;
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Update an existing pattern
        /// </summary>
        public async Task<bool> UpdatePatternAsync(PatternBase pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

            await _patternLock.WaitAsync();
            try
            {
                if (_allPatterns.ContainsKey(pattern.Id))
                {
                    _allPatterns[pattern.Id] = pattern;
                    
                    // Update in symbol collection
                    if (_symbolPatterns.TryGetValue(pattern.Symbol, out var symbolPatterns))
                    {
                        var index = symbolPatterns.FindIndex(p => p.Id == pattern.Id);
                        if (index >= 0)
                        {
                            symbolPatterns[index] = pattern;
                        }
                    }

                    _logger.Debug($"Updated {pattern.Type} pattern {pattern.Id} for {pattern.Symbol}");
                    PatternUpdated?.Invoke(this, new PatternEventArgs(pattern));
                    
                    return true;
                }
                
                return false;
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Remove a pattern from the system
        /// </summary>
        public async Task<bool> RemovePatternAsync(Guid patternId)
        {
            await _patternLock.WaitAsync();
            try
            {
                if (_allPatterns.TryRemove(patternId, out var pattern))
                {
                    // Remove from symbol collection
                    if (_symbolPatterns.TryGetValue(pattern.Symbol, out var symbolPatterns))
                    {
                        symbolPatterns.RemoveAll(p => p.Id == patternId);
                    }

                    _logger.Info($"Removed pattern {patternId} for {pattern.Symbol}");
                    return true;
                }
                
                return false;
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Get all patterns for a specific symbol
        /// </summary>
        public async Task<List<PatternBase>> GetPatternsForSymbolAsync(string symbol)
        {
            await _patternLock.WaitAsync();
            try
            {
                if (_symbolPatterns.TryGetValue(symbol, out var patterns))
                {
                    return patterns.ToList(); // Return a copy
                }
                
                return new List<PatternBase>();
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Get a specific pattern by ID
        /// </summary>
        public async Task<PatternBase?> GetPatternAsync(Guid patternId)
        {
            await _patternLock.WaitAsync();
            try
            {
                _allPatterns.TryGetValue(patternId, out var pattern);
                return pattern;
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Search patterns based on criteria
        /// </summary>
        public async Task<List<PatternBase>> SearchPatternsAsync(PatternSearchCriteria criteria)
        {
            await _patternLock.WaitAsync();
            try
            {
                var allPatterns = _allPatterns.Values.ToList();
                
                if (!string.IsNullOrWhiteSpace(criteria.Symbol))
                {
                    allPatterns = allPatterns.Where(p => 
                        p.Symbol.Equals(criteria.Symbol, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (criteria.PatternTypes?.Any() == true)
                {
                    allPatterns = allPatterns.Where(p => criteria.PatternTypes.Contains(p.Type)).ToList();
                }

                if (criteria.Directions?.Any() == true)
                {
                    allPatterns = allPatterns.Where(p => criteria.Directions.Contains(p.Direction)).ToList();
                }

                if (criteria.Statuses?.Any() == true)
                {
                    allPatterns = allPatterns.Where(p => criteria.Statuses.Contains(p.Status)).ToList();
                }

                if (criteria.MinReliability.HasValue)
                {
                    allPatterns = allPatterns.Where(p => p.Reliability >= criteria.MinReliability.Value).ToList();
                }

                if (criteria.MinConfidence.HasValue)
                {
                    allPatterns = allPatterns.Where(p => p.ConfidenceScore >= criteria.MinConfidence.Value).ToList();
                }

                if (criteria.StartDate.HasValue)
                {
                    allPatterns = allPatterns.Where(p => p.StartTime >= criteria.StartDate.Value).ToList();
                }

                if (criteria.EndDate.HasValue)
                {
                    allPatterns = allPatterns.Where(p => p.EndTime <= criteria.EndDate.Value).ToList();
                }

                if (!string.IsNullOrWhiteSpace(criteria.SearchText))
                {
                    var searchText = criteria.SearchText.ToLowerInvariant();
                    allPatterns = allPatterns.Where(p =>
                        p.Name.ToLowerInvariant().Contains(searchText) ||
                        p.Description.ToLowerInvariant().Contains(searchText) ||
                        p.Symbol.ToLowerInvariant().Contains(searchText)).ToList();
                }

                return allPatterns;
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Scan for new patterns in provided data
        /// </summary>
        public async Task<PatternDetectionResult> ScanForPatternsAsync(
            string symbol, 
            List<CandlestickData> data, 
            PatternSearchCriteria? criteria = null)
        {
            try
            {
                var result = await _detectionService.DetectPatternsAsync(symbol, data, criteria);
                
                // Add newly detected patterns to management system
                foreach (var pattern in result.Patterns)
                {
                    await AddPatternAsync(pattern);
                }

                _logger.Info($"Pattern scan completed for {symbol}: {result.TotalFound} patterns found");
                return result;
            }
            catch (Exception ex)
            {
                _logger.Error($"Error scanning patterns for {symbol}: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// Update patterns with new market data
        /// </summary>
        public async Task UpdatePatternsWithNewDataAsync(string symbol, List<CandlestickData> newData)
        {
            try
            {
                var existingPatterns = await GetPatternsForSymbolAsync(symbol);
                var activePatterns = existingPatterns.Where(p => p.IsActive()).ToList();

                if (!activePatterns.Any()) return;

                var updatedPatterns = await _detectionService.UpdateActivePatternsAsync(symbol, newData, activePatterns);

                foreach (var updatedPattern in updatedPatterns)
                {
                    await UpdatePatternAsync(updatedPattern);
                    
                    // Check for breakouts or status changes
                    await CheckPatternBreakouts(updatedPattern, newData.LastOrDefault());
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"Error updating patterns for {symbol}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Get pattern statistics
        /// </summary>
        public async Task<PatternStatistics> GetStatisticsAsync()
        {
            await _patternLock.WaitAsync();
            try
            {
                var allPatterns = _allPatterns.Values.ToList();
                
                return new PatternStatistics
                {
                    TotalPatterns = allPatterns.Count,
                    ActivePatterns = allPatterns.Count(p => p.IsActive()),
                    CompletedPatterns = allPatterns.Count(p => p.Status == PatternStatus.Completed),
                    ConfirmedPatterns = allPatterns.Count(p => p.Status == PatternStatus.Confirmed),
                    PatternsByType = allPatterns.GroupBy(p => p.Type)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    PatternsByDirection = allPatterns.GroupBy(p => p.Direction)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    PatternsByReliability = allPatterns.GroupBy(p => p.Reliability)
                        .ToDictionary(g => g.Key, g => g.Count()),
                    AverageConfidence = allPatterns.Any() ? allPatterns.Average(p => p.ConfidenceScore) : 0,
                    LastDetection = allPatterns.Any() ? allPatterns.Max(p => p.DetectedAt) : DateTime.MinValue
                };
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Get patterns by status
        /// </summary>
        public async Task<List<PatternBase>> GetPatternsByStatusAsync(PatternStatus status)
        {
            await _patternLock.WaitAsync();
            try
            {
                return _allPatterns.Values.Where(p => p.Status == status).ToList();
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Get recently detected patterns
        /// </summary>
        public async Task<List<PatternBase>> GetRecentPatternsAsync(TimeSpan timespan)
        {
            await _patternLock.WaitAsync();
            try
            {
                var cutoffTime = DateTime.Now - timespan;
                return _allPatterns.Values.Where(p => p.DetectedAt >= cutoffTime).ToList();
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Clear all patterns for a symbol
        /// </summary>
        public async Task ClearPatternsForSymbolAsync(string symbol)
        {
            await _patternLock.WaitAsync();
            try
            {
                if (_symbolPatterns.TryRemove(symbol, out var patterns))
                {
                    foreach (var pattern in patterns)
                    {
                        _allPatterns.TryRemove(pattern.Id, out _);
                    }
                    
                    _logger.Info($"Cleared {patterns.Count} patterns for {symbol}");
                }
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Clear all patterns
        /// </summary>
        public async Task ClearAllPatternsAsync()
        {
            await _patternLock.WaitAsync();
            try
            {
                var count = _allPatterns.Count;
                _allPatterns.Clear();
                _symbolPatterns.Clear();
                
                _logger.Info($"Cleared all {count} patterns");
            }
            finally
            {
                _patternLock.Release();
            }
        }

        /// <summary>
        /// Cleanup expired patterns
        /// </summary>
        private async Task CleanupExpiredPatternsAsync()
        {
            await _patternLock.WaitAsync();
            try
            {
                var expiredPatterns = _allPatterns.Values
                    .Where(p => IsPatternExpired(p))
                    .ToList();

                foreach (var pattern in expiredPatterns)
                {
                    _allPatterns.TryRemove(pattern.Id, out _);
                    
                    if (_symbolPatterns.TryGetValue(pattern.Symbol, out var symbolPatterns))
                    {
                        symbolPatterns.RemoveAll(p => p.Id == pattern.Id);
                    }

                    PatternExpired?.Invoke(this, new PatternEventArgs(pattern));
                }

                if (expiredPatterns.Any())
                {
                    _logger.Info($"Cleaned up {expiredPatterns.Count} expired patterns");
                }
            }
            finally
            {
                _patternLock.Release();
            }
        }

        private bool IsPatternExpired(PatternBase pattern)
        {
            // Pattern is expired if it's older than 30 days and not active
            return !pattern.IsActive() && 
                   DateTime.Now - pattern.DetectedAt > TimeSpan.FromDays(30);
        }

        private async Task CheckPatternBreakouts(PatternBase pattern, CandlestickData? latestCandle)
        {
            if (latestCandle == null) return;

            bool breakoutDetected = false;
            
            // Check specific pattern types for breakouts
            switch (pattern)
            {
                case TrianglePattern triangle when !triangle.IsBreakoutConfirmed:
                    if (latestCandle.Close > triangle.BreakoutLevel || latestCandle.Close < triangle.BreakoutLevel * 0.95m)
                    {
                        triangle.IsBreakoutConfirmed = true;
                        breakoutDetected = true;
                    }
                    break;
                    
                case SupportResistancePattern sr:
                    var tolerance = sr.Level * 0.02m; // 2% tolerance
                    if ((sr.Type == PatternType.SupportLevel && latestCandle.Close < sr.Level - tolerance) ||
                        (sr.Type == PatternType.ResistanceLevel && latestCandle.Close > sr.Level + tolerance))
                    {
                        pattern.Status = PatternStatus.Broken;
                        breakoutDetected = true;
                    }
                    break;
            }

            if (breakoutDetected)
            {
                _logger.Info($"Breakout detected for {pattern.Type} pattern on {pattern.Symbol}");
                PatternBreakout?.Invoke(this, new PatternEventArgs(pattern));
            }

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            _cleanupTimer?.Dispose();
            _patternLock?.Dispose();
        }
    }

    /// <summary>
    /// Event arguments for pattern-related events
    /// </summary>
    public class PatternEventArgs : EventArgs
    {
        public PatternBase Pattern { get; }
        public DateTime Timestamp { get; }

        public PatternEventArgs(PatternBase pattern)
        {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            Timestamp = DateTime.Now;
        }
    }
}
