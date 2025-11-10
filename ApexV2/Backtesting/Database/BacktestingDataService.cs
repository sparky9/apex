using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.Database
{
    /// <summary>
    /// Service for managing backtesting data persistence
    /// </summary>
    public class BacktestingDataService : IDisposable
    {
        private readonly BacktestingDbContext _context;

        public BacktestingDataService()
        {
            _context = new BacktestingDbContext();
            _context.Database.EnsureCreated();
        }

        #region Strategy Management

        /// <summary>
        /// Save a strategy to the database
        /// </summary>
        public SavedStrategyEntity SaveStrategy(Strategy strategy, string description = null, string generationMethod = null)
        {
            var entity = new SavedStrategyEntity
            {
                Name = strategy.Name,
                Description = description ?? strategy.Description ?? string.Empty,
                StrategyType = InferStrategyType(strategy),
                GenerationMethod = generationMethod ?? strategy.GenerationMethod ?? "Manual",
                EntryRulesJson = JsonSerializer.Serialize(strategy.EntryRules),
                ExitRulesJson = JsonSerializer.Serialize(strategy.ExitRules),
                CreatedDate = DateTime.Now,
                IsFavorite = false
            };

            _context.SavedStrategies.Add(entity);
            _context.SaveChanges();

            return entity;
        }

        /// <summary>
        /// Infer strategy type from rules
        /// </summary>
        private string InferStrategyType(Strategy strategy)
        {
            if (strategy.EntryRules == null || strategy.EntryRules.Count == 0)
                return "Unknown";

            // Count rule types
            var trendCount = strategy.EntryRules.Count(r => r.Type == RuleType.TrendFollowing);
            var meanRevCount = strategy.EntryRules.Count(r => r.Type == RuleType.MeanReversion);
            var momentumCount = strategy.EntryRules.Count(r => r.Type == RuleType.Momentum);
            var breakoutCount = strategy.EntryRules.Count(r => r.Type == RuleType.VolatilityBreakout);

            // Return dominant type
            var max = Math.Max(Math.Max(trendCount, meanRevCount), Math.Max(momentumCount, breakoutCount));

            if (max == 0) return "Custom";
            if (trendCount == max) return "Trend Following";
            if (meanRevCount == max) return "Mean Reversion";
            if (momentumCount == max) return "Momentum";
            if (breakoutCount == max) return "Breakout";

            return "Custom";
        }

        /// <summary>
        /// Load all saved strategies
        /// </summary>
        public List<SavedStrategyEntity> LoadAllStrategies()
        {
            return _context.SavedStrategies
                .OrderByDescending(s => s.CreatedDate)
                .ToList();
        }

        /// <summary>
        /// Load strategies by type
        /// </summary>
        public List<SavedStrategyEntity> LoadStrategiesByType(string strategyType)
        {
            return _context.SavedStrategies
                .Where(s => s.StrategyType == strategyType)
                .OrderByDescending(s => s.CreatedDate)
                .ToList();
        }

        /// <summary>
        /// Load favorite strategies
        /// </summary>
        public List<SavedStrategyEntity> LoadFavoriteStrategies()
        {
            return _context.SavedStrategies
                .Where(s => s.IsFavorite)
                .OrderByDescending(s => s.CreatedDate)
                .ToList();
        }

        /// <summary>
        /// Convert entity to Strategy model
        /// </summary>
        public Strategy EntityToStrategy(SavedStrategyEntity entity)
        {
            return new Strategy
            {
                Name = entity.Name,
                StrategyType = entity.StrategyType,
                EntryRules = JsonSerializer.Deserialize<List<Rule>>(entity.EntryRulesJson),
                ExitRules = JsonSerializer.Deserialize<List<Rule>>(entity.ExitRulesJson)
            };
        }

        /// <summary>
        /// Delete a strategy
        /// </summary>
        public void DeleteStrategy(int strategyId)
        {
            var strategy = _context.SavedStrategies.Find(strategyId);
            if (strategy != null)
            {
                _context.SavedStrategies.Remove(strategy);
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Update strategy favorite status
        /// </summary>
        public void UpdateFavoriteStatus(int strategyId, bool isFavorite)
        {
            var strategy = _context.SavedStrategies.Find(strategyId);
            if (strategy != null)
            {
                strategy.IsFavorite = isFavorite;
                strategy.LastModifiedDate = DateTime.Now;
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Search strategies by name or description
        /// </summary>
        public List<SavedStrategyEntity> SearchStrategies(string searchTerm)
        {
            searchTerm = searchTerm.ToLower();
            return _context.SavedStrategies
                .Where(s => s.Name.ToLower().Contains(searchTerm) ||
                           (s.Description != null && s.Description.ToLower().Contains(searchTerm)))
                .OrderByDescending(s => s.CreatedDate)
                .ToList();
        }

        #endregion

        #region Backtest Results Management

        /// <summary>
        /// Save backtest results
        /// </summary>
        public BacktestResultEntity SaveBacktestResults(
            int strategyId,
            string symbol,
            DateTime startDate,
            DateTime endDate,
            BacktestParameters parameters,
            BacktestResults results,
            PerformanceMetrics metrics,
            string notes = null)
        {
            var entity = new BacktestResultEntity
            {
                StrategyId = strategyId,
                Symbol = symbol,
                StartDate = startDate,
                EndDate = endDate,
                RunDate = DateTime.Now,

                // Parameters
                InitialCapital = parameters.InitialCapital,
                PositionSize = parameters.PositionSize,
                Commission = parameters.Commission,
                Slippage = parameters.Slippage,

                // Performance metrics
                TotalReturn = metrics.TotalReturn,
                AnnualizedReturn = metrics.AnnualizedReturn,
                SharpeRatio = metrics.SharpeRatio,
                SortinoRatio = metrics.SortinoRatio,
                CalmarRatio = metrics.CalmarRatio,
                ProfitFactor = metrics.ProfitFactor,
                MaxDrawdown = metrics.MaxDrawdown,
                WinRate = metrics.WinRate,
                TotalTrades = metrics.TotalTrades,
                WinningTrades = metrics.WinningTrades,
                LosingTrades = metrics.LosingTrades,
                AverageWin = metrics.AverageWin,
                AverageLoss = metrics.AverageLoss,
                LargestWin = metrics.LargestWin,
                LargestLoss = metrics.LargestLoss,
                AverageHoldingPeriod = metrics.AverageHoldingPeriod,
                MaxConsecutiveWins = metrics.MaxConsecutiveWins,
                MaxConsecutiveLosses = metrics.MaxConsecutiveLosses,

                // Curves and trades as JSON
                EquityCurveJson = JsonSerializer.Serialize(results.EquityCurve),
                DrawdownCurveJson = JsonSerializer.Serialize(results.DrawdownCurve),
                TradesJson = JsonSerializer.Serialize(results.Trades),

                Notes = notes,
                IsBookmarked = false
            };

            _context.BacktestResults.Add(entity);
            _context.SaveChanges();

            return entity;
        }

        /// <summary>
        /// Load all backtest results for a strategy
        /// </summary>
        public List<BacktestResultEntity> LoadResultsForStrategy(int strategyId)
        {
            return _context.BacktestResults
                .Where(r => r.StrategyId == strategyId)
                .OrderByDescending(r => r.RunDate)
                .ToList();
        }

        /// <summary>
        /// Load recent backtest results
        /// </summary>
        public List<BacktestResultEntity> LoadRecentResults(int count = 20)
        {
            return _context.BacktestResults
                .Include(r => r.Strategy)
                .OrderByDescending(r => r.RunDate)
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Load bookmarked results
        /// </summary>
        public List<BacktestResultEntity> LoadBookmarkedResults()
        {
            return _context.BacktestResults
                .Include(r => r.Strategy)
                .Where(r => r.IsBookmarked)
                .OrderByDescending(r => r.RunDate)
                .ToList();
        }

        /// <summary>
        /// Delete backtest results
        /// </summary>
        public void DeleteBacktestResult(int resultId)
        {
            var result = _context.BacktestResults.Find(resultId);
            if (result != null)
            {
                _context.BacktestResults.Remove(result);
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Update bookmark status
        /// </summary>
        public void UpdateBookmarkStatus(int resultId, bool isBookmarked)
        {
            var result = _context.BacktestResults.Find(resultId);
            if (result != null)
            {
                result.IsBookmarked = isBookmarked;
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Update result notes
        /// </summary>
        public void UpdateResultNotes(int resultId, string notes)
        {
            var result = _context.BacktestResults.Find(resultId);
            if (result != null)
            {
                result.Notes = notes;
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Get top performing strategies
        /// </summary>
        public List<BacktestResultEntity> GetTopPerformingStrategies(int count = 10, string metric = "SharpeRatio")
        {
            var query = _context.BacktestResults.Include(r => r.Strategy).AsQueryable();

            query = metric switch
            {
                "SharpeRatio" => query.OrderByDescending(r => r.SharpeRatio),
                "TotalReturn" => query.OrderByDescending(r => r.TotalReturn),
                "WinRate" => query.OrderByDescending(r => r.WinRate),
                "ProfitFactor" => query.OrderByDescending(r => r.ProfitFactor),
                _ => query.OrderByDescending(r => r.SharpeRatio)
            };

            return query.Take(count).ToList();
        }

        #endregion

        #region Strategy Templates

        /// <summary>
        /// Save a strategy template
        /// </summary>
        public StrategyTemplateEntity SaveTemplate(Strategy strategy, string description, bool isBuiltIn = false)
        {
            var entity = new StrategyTemplateEntity
            {
                Name = strategy.Name,
                Description = description,
                StrategyType = strategy.StrategyType,
                EntryRulesJson = JsonSerializer.Serialize(strategy.EntryRules),
                ExitRulesJson = JsonSerializer.Serialize(strategy.ExitRules),
                IsBuiltIn = isBuiltIn,
                CreatedDate = DateTime.Now,
                TimesUsed = 0
            };

            _context.StrategyTemplates.Add(entity);
            _context.SaveChanges();

            return entity;
        }

        /// <summary>
        /// Load all templates
        /// </summary>
        public List<StrategyTemplateEntity> LoadAllTemplates()
        {
            return _context.StrategyTemplates
                .OrderByDescending(t => t.TimesUsed)
                .ThenBy(t => t.Name)
                .ToList();
        }

        /// <summary>
        /// Increment template usage count
        /// </summary>
        public void IncrementTemplateUsage(int templateId)
        {
            var template = _context.StrategyTemplates.Find(templateId);
            if (template != null)
            {
                template.TimesUsed++;
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Initialize built-in templates
        /// </summary>
        public void InitializeBuiltInTemplates()
        {
            // Check if templates already exist
            if (_context.StrategyTemplates.Any(t => t.IsBuiltIn))
                return;

            // Add some common strategy templates
            var templates = new[]
            {
                new StrategyTemplateEntity
                {
                    Name = "Simple Moving Average Crossover",
                    Description = "Buy when fast SMA crosses above slow SMA, sell when it crosses below",
                    StrategyType = "Trend Following",
                    EntryRulesJson = JsonSerializer.Serialize(new List<Rule>
                    {
                        new Rule { IndicatorName = "SMA_50", Condition = "CrossesAbove", ComparisonIndicator = "SMA_200" }
                    }),
                    ExitRulesJson = JsonSerializer.Serialize(new List<Rule>
                    {
                        new Rule { IndicatorName = "SMA_50", Condition = "CrossesBelow", ComparisonIndicator = "SMA_200" }
                    }),
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    TimesUsed = 0
                },
                new StrategyTemplateEntity
                {
                    Name = "RSI Oversold/Overbought",
                    Description = "Buy when RSI is oversold, sell when overbought",
                    StrategyType = "Mean Reversion",
                    EntryRulesJson = JsonSerializer.Serialize(new List<Rule>
                    {
                        new Rule { IndicatorName = "RSI_14", Condition = "Below", Value = 30 }
                    }),
                    ExitRulesJson = JsonSerializer.Serialize(new List<Rule>
                    {
                        new Rule { IndicatorName = "RSI_14", Condition = "Above", Value = 70 }
                    }),
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    TimesUsed = 0
                },
                new StrategyTemplateEntity
                {
                    Name = "MACD Signal Crossover",
                    Description = "Buy when MACD crosses above signal line, sell when it crosses below",
                    StrategyType = "Momentum",
                    EntryRulesJson = JsonSerializer.Serialize(new List<Rule>
                    {
                        new Rule { IndicatorName = "MACD", Condition = "CrossesAbove", ComparisonIndicator = "MACD_Signal" }
                    }),
                    ExitRulesJson = JsonSerializer.Serialize(new List<Rule>
                    {
                        new Rule { IndicatorName = "MACD", Condition = "CrossesBelow", ComparisonIndicator = "MACD_Signal" }
                    }),
                    IsBuiltIn = true,
                    CreatedDate = DateTime.Now,
                    TimesUsed = 0
                }
            };

            _context.StrategyTemplates.AddRange(templates);
            _context.SaveChanges();
        }

        #endregion

        #region Batch Sessions

        /// <summary>
        /// Create a new batch backtest session
        /// </summary>
        public BatchBacktestSessionEntity CreateBatchSession(string name, string[] symbols, int totalStrategies, object configuration)
        {
            var entity = new BatchBacktestSessionEntity
            {
                Name = name,
                StartTime = DateTime.Now,
                TotalStrategies = totalStrategies,
                CompletedStrategies = 0,
                Status = "Running",
                Symbols = string.Join(",", symbols),
                ConfigurationJson = JsonSerializer.Serialize(configuration)
            };

            _context.BatchBacktestSessions.Add(entity);
            _context.SaveChanges();

            return entity;
        }

        /// <summary>
        /// Update batch session progress
        /// </summary>
        public void UpdateBatchProgress(int sessionId, int completedStrategies)
        {
            var session = _context.BatchBacktestSessions.Find(sessionId);
            if (session != null)
            {
                session.CompletedStrategies = completedStrategies;
                _context.SaveChanges();
            }
        }

        /// <summary>
        /// Complete batch session
        /// </summary>
        public void CompleteBatchSession(int sessionId, string status = "Completed")
        {
            var session = _context.BatchBacktestSessions.Find(sessionId);
            if (session != null)
            {
                session.EndTime = DateTime.Now;
                session.Status = status;
                _context.SaveChanges();
            }
        }

        #endregion

        public void Dispose()
        {
            _context?.Dispose();
        }
    }
}
