using System;
using System.Collections.Generic;
using System.Linq;

namespace ApexV2.Backtesting.StrategyGeneration
{
    /// <summary>
    /// Generates trading strategies automatically
    /// Supports random generation, template-based, and genetic algorithms
    /// </summary>
    public class StrategyGenerator
    {
        private readonly StrategyGenerationConfig _config;
        private readonly Random _random;
        private readonly RuleFactory _ruleFactory;
        private int _generatedCount = 0;

        public StrategyGenerator(StrategyGenerationConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = config.RandomSeed.HasValue ? new Random(config.RandomSeed.Value) : new Random();
            _ruleFactory = new RuleFactory(_config, _random);
        }

        /// <summary>
        /// Generate strategies based on configuration
        /// </summary>
        public List<Strategy> GenerateStrategies()
        {
            var strategies = new List<Strategy>();

            switch (_config.Method)
            {
                case GenerationMethod.Random:
                    strategies = GenerateRandomStrategies(_config.MaxStrategies);
                    break;

                case GenerationMethod.TemplateBased:
                    strategies = GenerateTemplateBasedStrategies();
                    break;

                case GenerationMethod.Mixed:
                    int randomCount = (int)(_config.MaxStrategies * 0.7); // 70% random
                    int templateCount = _config.MaxStrategies - randomCount;

                    strategies.AddRange(GenerateRandomStrategies(randomCount));
                    strategies.AddRange(GenerateTemplateBasedStrategies(templateCount));
                    break;

                default:
                    throw new NotImplementedException($"Generation method {_config.Method} not implemented");
            }

            return strategies;
        }

        /// <summary>
        /// Generate completely random strategies
        /// </summary>
        private List<Strategy> GenerateRandomStrategies(int count)
        {
            var strategies = new List<Strategy>();

            for (int i = 0; i < count; i++)
            {
                var strategy = new Strategy
                {
                    Name = $"Random_Strategy_{++_generatedCount}",
                    Description = "Randomly generated strategy",
                    GenerationMethod = "Random",
                    CreatedAt = DateTime.UtcNow
                };

                // Generate entry rules
                int entryRuleCount = _random.Next(_config.MinEntryConditions, _config.MaxEntryConditions + 1);
                for (int j = 0; j < entryRuleCount; j++)
                {
                    var rule = _ruleFactory.CreateRandomRule(isEntry: true);
                    if (rule != null)
                    {
                        strategy.EntryRules.Add(rule);
                    }
                }

                // Generate exit rules
                int exitRuleCount = _random.Next(_config.MinExitConditions, _config.MaxExitConditions + 1);
                for (int j = 0; j < exitRuleCount; j++)
                {
                    var rule = _ruleFactory.CreateRandomRule(isEntry: false);
                    if (rule != null)
                    {
                        strategy.ExitRules.Add(rule);
                    }
                }

                // Random logical operator
                strategy.EntryLogic = _random.Next(2) == 0 ? LogicalOperator.AND : LogicalOperator.OR;
                strategy.ExitLogic = _random.Next(2) == 0 ? LogicalOperator.AND : LogicalOperator.OR;

                // Validate strategy
                if (ValidateStrategy(strategy))
                {
                    strategies.Add(strategy);
                }
                else
                {
                    i--; // Try again
                }
            }

            return strategies;
        }

        /// <summary>
        /// Generate strategies based on proven templates
        /// </summary>
        private List<Strategy> GenerateTemplateBasedStrategies(int maxCount = int.MaxValue)
        {
            var strategies = new List<Strategy>();
            var templates = GetStrategyTemplates();

            foreach (var template in templates.Take(maxCount))
            {
                var strategy = CreateStrategyFromTemplate(template);
                if (strategy != null)
                {
                    strategies.Add(strategy);
                }
            }

            return strategies;
        }

        /// <summary>
        /// Create strategy from template
        /// </summary>
        private Strategy? CreateStrategyFromTemplate(StrategyTemplate template)
        {
            var strategy = new Strategy
            {
                Name = $"{template.Name}_{++_generatedCount}",
                Description = template.Description,
                GenerationMethod = "Template",
                CreatedAt = DateTime.UtcNow
            };

            strategy.EntryRules.AddRange(template.EntryRules);
            strategy.ExitRules.AddRange(template.ExitRules);
            strategy.EntryLogic = template.EntryLogic;
            strategy.ExitLogic = template.ExitLogic;

            return ValidateStrategy(strategy) ? strategy : null;
        }

        /// <summary>
        /// Get predefined strategy templates
        /// Based on proven trading strategies
        /// </summary>
        private List<StrategyTemplate> GetStrategyTemplates()
        {
            var templates = new List<StrategyTemplate>();

            // Template 1: RSI Oversold/Overbought
            templates.Add(new StrategyTemplate
            {
                Name = "RSI_Mean_Reversion",
                Description = "Buy oversold, sell overbought",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.MeanReversion,
                        IndicatorName = "RSI",
                        Condition = RuleCondition.Oversold,
                        Parameters = new Dictionary<string, object>
                        {
                            { "window", 14 },
                            { "oversold", 30 }
                        }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.MeanReversion,
                        IndicatorName = "RSI",
                        Condition = RuleCondition.Overbought,
                        Parameters = new Dictionary<string, object>
                        {
                            { "window", 14 },
                            { "overbought", 70 }
                        }
                    }
                },
                EntryLogic = LogicalOperator.AND,
                ExitLogic = LogicalOperator.OR
            });

            // Template 2: MACD Crossover
            templates.Add(new StrategyTemplate
            {
                Name = "MACD_Trend",
                Description = "MACD crossover with trend confirmation",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.TrendFollowing,
                        IndicatorName = "MACD",
                        Condition = RuleCondition.IndicatorCrossAbove,
                        Parameters = new Dictionary<string, object>
                        {
                            { "fast_period", 12 },
                            { "slow_period", 26 },
                            { "signal_period", 9 }
                        }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.TrendFollowing,
                        IndicatorName = "MACD",
                        Condition = RuleCondition.IndicatorCrossBelow,
                        Parameters = new Dictionary<string, object>
                        {
                            { "fast_period", 12 },
                            { "slow_period", 26 },
                            { "signal_period", 9 }
                        }
                    }
                },
                EntryLogic = LogicalOperator.AND,
                ExitLogic = LogicalOperator.OR
            });

            // Template 3: Bollinger Band Mean Reversion
            templates.Add(new StrategyTemplate
            {
                Name = "BB_Mean_Reversion",
                Description = "Buy at lower band, sell at upper band",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.MeanReversion,
                        IndicatorName = "BB",
                        Condition = RuleCondition.PriceBelow,
                        Parameters = new Dictionary<string, object>
                        {
                            { "window", 20 },
                            { "std_dev", 2.0 }
                        }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.MeanReversion,
                        IndicatorName = "BB",
                        Condition = RuleCondition.PriceAbove,
                        Parameters = new Dictionary<string, object>
                        {
                            { "window", 20 },
                            { "std_dev", 2.0 }
                        }
                    }
                },
                EntryLogic = LogicalOperator.AND,
                ExitLogic = LogicalOperator.OR
            });

            // Template 4: Moving Average Crossover
            templates.Add(new StrategyTemplate
            {
                Name = "MA_Crossover",
                Description = "Fast MA crosses above slow MA",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.TrendFollowing,
                        IndicatorName = "EMA",
                        Condition = RuleCondition.IndicatorCrossAbove,
                        Parameters = new Dictionary<string, object>
                        {
                            { "fast_window", 12 },
                            { "slow_window", 26 }
                        }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.TrendFollowing,
                        IndicatorName = "EMA",
                        Condition = RuleCondition.IndicatorCrossBelow,
                        Parameters = new Dictionary<string, object>
                        {
                            { "fast_window", 12 },
                            { "slow_window", 26 }
                        }
                    }
                },
                EntryLogic = LogicalOperator.AND,
                ExitLogic = LogicalOperator.OR
            });

            // Template 5: RSI + MACD Combo
            templates.Add(new StrategyTemplate
            {
                Name = "RSI_MACD_Combo",
                Description = "RSI oversold + MACD bullish",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.Momentum,
                        IndicatorName = "RSI",
                        Condition = RuleCondition.Oversold,
                        Parameters = new Dictionary<string, object> { { "window", 14 }, { "oversold", 30 } }
                    },
                    new StrategyRule
                    {
                        Type = RuleType.TrendFollowing,
                        IndicatorName = "MACD",
                        Condition = RuleCondition.Positive,
                        Parameters = new Dictionary<string, object>
                        {
                            { "fast_period", 12 },
                            { "slow_period", 26 },
                            { "signal_period", 9 }
                        }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.Momentum,
                        IndicatorName = "RSI",
                        Condition = RuleCondition.Overbought,
                        Parameters = new Dictionary<string, object> { { "window", 14 }, { "overbought", 70 } }
                    }
                },
                EntryLogic = LogicalOperator.AND,
                ExitLogic = LogicalOperator.OR
            });

            return templates;
        }

        /// <summary>
        /// Validate strategy has valid rules and configuration
        /// </summary>
        private bool ValidateStrategy(Strategy strategy)
        {
            if (strategy.EntryRules.Count == 0 || strategy.ExitRules.Count == 0)
                return false;

            // Check for duplicate rules
            var entryRuleHashes = new HashSet<string>();
            foreach (var rule in strategy.EntryRules)
            {
                string hash = $"{rule.IndicatorName}_{rule.Condition}";
                if (entryRuleHashes.Contains(hash))
                    return false;
                entryRuleHashes.Add(hash);
            }

            return true;
        }
    }

    /// <summary>
    /// Strategy template for template-based generation
    /// </summary>
    public class StrategyTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<StrategyRule> EntryRules { get; set; } = new List<StrategyRule>();
        public List<StrategyRule> ExitRules { get; set; } = new List<StrategyRule>();
        public LogicalOperator EntryLogic { get; set; } = LogicalOperator.AND;
        public LogicalOperator ExitLogic { get; set; } = LogicalOperator.OR;
    }
}
