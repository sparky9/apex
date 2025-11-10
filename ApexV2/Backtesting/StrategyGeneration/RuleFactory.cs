using System;
using System.Collections.Generic;
using System.Linq;

namespace ApexV2.Backtesting.StrategyGeneration
{
    /// <summary>
    /// Factory for creating trading rules with random or specific parameters
    /// </summary>
    public class RuleFactory
    {
        private readonly StrategyGenerationConfig _config;
        private readonly Random _random;

        // Available rule conditions per indicator type
        private readonly Dictionary<string, List<RuleCondition>> _indicatorConditions = new Dictionary<string, List<RuleCondition>>
        {
            { "RSI", new List<RuleCondition> { RuleCondition.Oversold, RuleCondition.Overbought, RuleCondition.Rising, RuleCondition.Falling } },
            { "MACD", new List<RuleCondition> { RuleCondition.IndicatorCrossAbove, RuleCondition.IndicatorCrossBelow, RuleCondition.Positive, RuleCondition.Negative } },
            { "BB", new List<RuleCondition> { RuleCondition.PriceAbove, RuleCondition.PriceBelow, RuleCondition.Squeeze, RuleCondition.Expansion } },
            { "ATR", new List<RuleCondition> { RuleCondition.Expansion, RuleCondition.Above, RuleCondition.Below } },
            { "STOCH", new List<RuleCondition> { RuleCondition.Oversold, RuleCondition.Overbought, RuleCondition.IndicatorCrossAbove, RuleCondition.IndicatorCrossBelow } },
            { "WILLIAMSR", new List<RuleCondition> { RuleCondition.Oversold, RuleCondition.Overbought } },
            { "CCI", new List<RuleCondition> { RuleCondition.Oversold, RuleCondition.Overbought } },
            { "ADX", new List<RuleCondition> { RuleCondition.Above, RuleCondition.Below } },
            { "SMA", new List<RuleCondition> { RuleCondition.PriceCrossAbove, RuleCondition.PriceCrossBelow, RuleCondition.PriceAbove, RuleCondition.PriceBelow } },
            { "EMA", new List<RuleCondition> { RuleCondition.PriceCrossAbove, RuleCondition.PriceCrossBelow, RuleCondition.IndicatorCrossAbove, RuleCondition.IndicatorCrossBelow } },
            { "CMF", new List<RuleCondition> { RuleCondition.Positive, RuleCondition.Negative } },
            { "OBV", new List<RuleCondition> { RuleCondition.Rising, RuleCondition.Falling } },
            { "VWAP", new List<RuleCondition> { RuleCondition.PriceAbove, RuleCondition.PriceBelow } }
        };

        public RuleFactory(StrategyGenerationConfig config, Random random)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>
        /// Create a random rule
        /// </summary>
        public StrategyRule? CreateRandomRule(bool isEntry)
        {
            // Pick random indicator
            var enabledIndicators = _config.EnabledIndicators.Where(i => _indicatorConditions.ContainsKey(i)).ToList();
            if (enabledIndicators.Count == 0)
                return null;

            string indicator = enabledIndicators[_random.Next(enabledIndicators.Count)];

            // Pick random condition for this indicator
            var conditions = _indicatorConditions[indicator];
            var condition = conditions[_random.Next(conditions.Count)];

            // Determine rule type
            var ruleType = DetermineRuleType(indicator, condition);

            // Create rule
            var rule = new StrategyRule
            {
                Type = ruleType,
                IndicatorName = indicator,
                Condition = condition,
                Parameters = GenerateRandomParameters(indicator)
            };

            return rule;
        }

        /// <summary>
        /// Generate random parameters for an indicator
        /// </summary>
        private Dictionary<string, object> GenerateRandomParameters(string indicator)
        {
            var parameters = new Dictionary<string, object>();

            switch (indicator)
            {
                case "RSI":
                    parameters["window"] = (int)GetRandomParameter("rsi_window");
                    parameters["overbought"] = GetRandomParameter("rsi_overbought");
                    parameters["oversold"] = GetRandomParameter("rsi_oversold");
                    break;

                case "MACD":
                    parameters["fast_period"] = (int)GetRandomParameter("macd_fast");
                    parameters["slow_period"] = (int)GetRandomParameter("macd_slow");
                    parameters["signal_period"] = (int)GetRandomParameter("macd_signal");
                    break;

                case "BB":
                    parameters["window"] = (int)GetRandomParameter("bb_window");
                    parameters["std_dev"] = GetRandomParameter("bb_std");
                    break;

                case "ATR":
                    parameters["window"] = (int)GetRandomParameter("atr_window");
                    break;

                case "STOCH":
                    parameters["k_window"] = 14;
                    parameters["d_window"] = 3;
                    parameters["smooth_k"] = 3;
                    parameters["overbought"] = 80;
                    parameters["oversold"] = 20;
                    break;

                case "WILLIAMSR":
                    parameters["window"] = 14;
                    parameters["overbought"] = -20;
                    parameters["oversold"] = -80;
                    break;

                case "CCI":
                    parameters["window"] = 20;
                    parameters["overbought"] = 100;
                    parameters["oversold"] = -100;
                    break;

                case "ADX":
                    parameters["window"] = 14;
                    parameters["trend_threshold"] = 25;
                    break;

                case "SMA":
                    parameters["window"] = (int)GetRandomParameter("sma_window");
                    break;

                case "EMA":
                    parameters["window"] = (int)GetRandomParameter("ema_window");
                    parameters["fast_window"] = 12;
                    parameters["slow_window"] = 26;
                    break;

                case "CMF":
                    parameters["window"] = 20;
                    break;

                case "VWAP":
                    parameters["window"] = 20;
                    break;
            }

            return parameters;
        }

        /// <summary>
        /// Get random value from parameter range
        /// </summary>
        private double GetRandomParameter(string parameterName)
        {
            if (_config.ParameterRanges.TryGetValue(parameterName, out var range))
            {
                return range.GetRandomValue(_random);
            }

            // Default values if not in config
            return parameterName switch
            {
                "rsi_window" => 14,
                "rsi_overbought" => 70,
                "rsi_oversold" => 30,
                "sma_window" => 50,
                "ema_window" => 20,
                _ => 14
            };
        }

        /// <summary>
        /// Determine rule type based on indicator and condition
        /// </summary>
        private RuleType DetermineRuleType(string indicator, RuleCondition condition)
        {
            // Mean reversion indicators
            if (indicator == "RSI" && (condition == RuleCondition.Oversold || condition == RuleCondition.Overbought))
                return RuleType.MeanReversion;

            if (indicator == "BB" && (condition == RuleCondition.PriceAbove || condition == RuleCondition.PriceBelow))
                return RuleType.MeanReversion;

            // Trend following
            if (indicator == "MACD" || indicator == "EMA" || indicator == "SMA")
                return RuleType.TrendFollowing;

            if (indicator == "ADX")
                return RuleType.TrendFollowing;

            // Momentum
            if (indicator == "RSI" && (condition == RuleCondition.Rising || condition == RuleCondition.Falling))
                return RuleType.Momentum;

            if (indicator == "STOCH" || indicator == "CCI")
                return RuleType.Momentum;

            // Volatility breakout
            if (indicator == "ATR" || indicator == "BB")
                return RuleType.VolatilityBreakout;

            // Volume confirmation
            if (indicator == "CMF" || indicator == "OBV" || indicator == "VWAP")
                return RuleType.VolumeConfirmation;

            // Default
            return RuleType.TrendFollowing;
        }

        /// <summary>
        /// Create specific rule with given parameters
        /// </summary>
        public StrategyRule CreateRule(
            string indicator,
            RuleCondition condition,
            Dictionary<string, object> parameters)
        {
            return new StrategyRule
            {
                Type = DetermineRuleType(indicator, condition),
                IndicatorName = indicator,
                Condition = condition,
                Parameters = parameters
            };
        }

        /// <summary>
        /// Create rule from template
        /// </summary>
        public StrategyRule CreateRuleFromTemplate(string templateName)
        {
            // Predefined rule templates
            return templateName switch
            {
                "RSI_OVERSOLD" => CreateRule("RSI", RuleCondition.Oversold,
                    new Dictionary<string, object> { { "window", 14 }, { "oversold", 30 } }),

                "RSI_OVERBOUGHT" => CreateRule("RSI", RuleCondition.Overbought,
                    new Dictionary<string, object> { { "window", 14 }, { "overbought", 70 } }),

                "MACD_BULLISH" => CreateRule("MACD", RuleCondition.IndicatorCrossAbove,
                    new Dictionary<string, object> { { "fast_period", 12 }, { "slow_period", 26 }, { "signal_period", 9 } }),

                "MACD_BEARISH" => CreateRule("MACD", RuleCondition.IndicatorCrossBelow,
                    new Dictionary<string, object> { { "fast_period", 12 }, { "slow_period", 26 }, { "signal_period", 9 } }),

                "BB_LOWER" => CreateRule("BB", RuleCondition.PriceBelow,
                    new Dictionary<string, object> { { "window", 20 }, { "std_dev", 2.0 } }),

                "BB_UPPER" => CreateRule("BB", RuleCondition.PriceAbove,
                    new Dictionary<string, object> { { "window", 20 }, { "std_dev", 2.0 } }),

                _ => throw new ArgumentException($"Unknown template: {templateName}")
            };
        }
    }
}
