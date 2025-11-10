using System;
using System.Collections.Generic;
using System.Linq;

namespace ApexV2.Backtesting.StrategyGeneration
{
    /// <summary>
    /// Complete strategy definition with entry and exit rules
    /// </summary>
    public class Strategy
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public List<StrategyRule> EntryRules { get; set; } = new List<StrategyRule>();
        public List<StrategyRule> ExitRules { get; set; } = new List<StrategyRule>();

        public LogicalOperator EntryLogic { get; set; } = LogicalOperator.AND;
        public LogicalOperator ExitLogic { get; set; } = LogicalOperator.OR;

        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        public DateTime? CreatedAt { get; set; }
        public string? GenerationMethod { get; set; }

        public override string ToString()
        {
            var entryDesc = string.Join($" {EntryLogic} ", EntryRules.Select(r => r.ToString()));
            var exitDesc = string.Join($" {ExitLogic} ", ExitRules.Select(r => r.ToString()));
            return $"{Name}: ENTRY({entryDesc}) EXIT({exitDesc})";
        }
    }

    /// <summary>
    /// Individual trading rule (condition to check)
    /// </summary>
    public class StrategyRule
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public RuleType Type { get; set; }
        public string IndicatorName { get; set; } = string.Empty;
        public RuleCondition Condition { get; set; }
        public Dictionary<string, object> Parameters { get; set; } = new Dictionary<string, object>();

        public override string ToString()
        {
            var paramStr = string.Join(", ", Parameters.Select(kv => $"{kv.Key}={kv.Value}"));
            return $"{IndicatorName}({paramStr}) {Condition}";
        }
    }

    /// <summary>
    /// Rule condition types
    /// </summary>
    public enum RuleCondition
    {
        // Price vs Indicator
        PriceAbove,
        PriceBelow,
        PriceCrossAbove,
        PriceCrossBelow,

        // Indicator vs Threshold
        Above,
        Below,
        CrossAbove,
        CrossBelow,

        // Indicator vs Indicator
        IndicatorAbove,
        IndicatorBelow,
        IndicatorCrossAbove,
        IndicatorCrossBelow,

        // State conditions
        Oversold,
        Overbought,
        Rising,
        Falling,
        Positive,
        Negative,

        // Pattern conditions
        BullishDivergence,
        BearishDivergence,
        Squeeze,
        Expansion,

        // Volume conditions
        VolumeSurge,
        HighVolume,
        LowVolume
    }

    /// <summary>
    /// Rule categories
    /// </summary>
    public enum RuleType
    {
        TrendFollowing,
        MeanReversion,
        Momentum,
        VolatilityBreakout,
        VolumeConfirmation,
        PriceAction
    }

    /// <summary>
    /// Logical operators for combining rules
    /// </summary>
    public enum LogicalOperator
    {
        AND,    // All rules must be true
        OR,     // Any rule must be true
        XOR     // Exactly one rule must be true
    }

    /// <summary>
    /// Rule template for quick strategy creation
    /// </summary>
    public class RuleTemplate
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public RuleType Type { get; set; }
        public List<string> RequiredIndicators { get; set; } = new List<string>();
        public Func<Dictionary<string, double[]>, int, bool> EvaluateEntry { get; set; } = null!;
        public Func<Dictionary<string, double[]>, int, bool> EvaluateExit { get; set; } = null!;
    }

    /// <summary>
    /// Strategy generation configuration
    /// </summary>
    public class StrategyGenerationConfig
    {
        public int MaxStrategies { get; set; } = 1000;
        public GenerationMethod Method { get; set; } = GenerationMethod.Mixed;

        // Entry/Exit complexity
        public int MinEntryConditions { get; set; } = 1;
        public int MaxEntryConditions { get; set; } = 3;
        public int MinExitConditions { get; set; } = 1;
        public int MaxExitConditions { get; set; } = 2;

        // Rule categories to include
        public bool IncludeTrendFollowing { get; set; } = true;
        public bool IncludeMeanReversion { get; set; } = true;
        public bool IncludeMomentum { get; set; } = true;
        public bool IncludeVolatilityBreakout { get; set; } = true;
        public bool IncludeVolumeConfirmation { get; set; } = true;

        // Indicators to use
        public List<string> EnabledIndicators { get; set; } = new List<string>
        {
            "SMA", "EMA", "RSI", "MACD", "BB", "ATR",
            "STOCH", "WILLIAMSR", "CCI", "ADX", "KELTNER",
            "CMF", "OBV", "VWAP", "ADLINE", "DEMA", "WMA"
        };

        // Parameter ranges for indicators
        public Dictionary<string, ParameterRange> ParameterRanges { get; set; } = new Dictionary<string, ParameterRange>
        {
            { "sma_window", new ParameterRange { Min = 10, Max = 200, Step = 10 } },
            { "ema_window", new ParameterRange { Min = 8, Max = 100, Step = 4 } },
            { "rsi_window", new ParameterRange { Min = 9, Max = 21, Step = 3 } },
            { "rsi_overbought", new ParameterRange { Min = 70, Max = 80, Step = 5 } },
            { "rsi_oversold", new ParameterRange { Min = 20, Max = 30, Step = 5 } },
            { "macd_fast", new ParameterRange { Min = 8, Max = 16, Step = 4 } },
            { "macd_slow", new ParameterRange { Min = 21, Max = 34, Step = 5 } },
            { "macd_signal", new ParameterRange { Min = 7, Max = 11, Step = 2 } },
            { "bb_window", new ParameterRange { Min = 15, Max = 25, Step = 5 } },
            { "bb_std", new ParameterRange { Min = 1.5, Max = 2.5, Step = 0.5 } },
            { "atr_window", new ParameterRange { Min = 14, Max = 28, Step = 7 } }
        };

        // Random seed for reproducibility
        public int? RandomSeed { get; set; }
    }

    /// <summary>
    /// Parameter range for indicator configuration
    /// </summary>
    public class ParameterRange
    {
        public double Min { get; set; }
        public double Max { get; set; }
        public double Step { get; set; }

        public List<double> GetValues()
        {
            var values = new List<double>();
            for (double val = Min; val <= Max; val += Step)
            {
                values.Add(val);
            }
            return values;
        }

        public double GetRandomValue(Random random)
        {
            var values = GetValues();
            return values[random.Next(values.Count)];
        }
    }

    /// <summary>
    /// Strategy generation methods
    /// </summary>
    public enum GenerationMethod
    {
        Random,         // Completely random rule combinations
        TemplateBased,  // Use predefined templates
        Genetic,        // Genetic algorithm optimization
        Mixed           // Combination of methods
    }

    /// <summary>
    /// Strategy validation result
    /// </summary>
    public class StrategyValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();

        public void AddError(string error) => Errors.Add(error);
        public void AddWarning(string warning) => Warnings.Add(warning);
    }
}
