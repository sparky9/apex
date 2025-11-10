using System;
using System.Collections.Generic;
using ApexV2.Backtesting.Models;
using ApexV2.Indicators.Backtesting;
using ApexV2.Indicators.Backtesting.Momentum;
using ApexV2.Indicators.Backtesting.MovingAverages;
using ApexV2.Indicators.Backtesting.Trend;
using ApexV2.Indicators.Backtesting.Volatility;
using ApexV2.Indicators.Backtesting.Volume;

namespace ApexV2.Backtesting.StrategyGeneration
{
    /// <summary>
    /// Pre-calculates all indicators for efficient strategy evaluation
    /// </summary>
    public class IndicatorCalculator
    {
        private readonly MarketData _marketData;

        public IndicatorCalculator(MarketData marketData)
        {
            _marketData = marketData ?? throw new ArgumentNullException(nameof(marketData));
        }

        /// <summary>
        /// Calculate all common indicators with standard parameters
        /// Returns dictionary: IndicatorName -> { "value" -> values[], "signal" -> values[], etc. }
        /// </summary>
        public Dictionary<string, Dictionary<string, double[]>> CalculateAllIndicators(List<string> enabledIndicators = null)
        {
            var results = new Dictionary<string, Dictionary<string, double[]>>();

            // Use all indicators if none specified
            if (enabledIndicators == null || enabledIndicators.Count == 0)
            {
                enabledIndicators = new List<string>
                {
                    "RSI", "MACD", "BB", "ATR", "STOCH", "WILLIAMSR", "CCI", "ADX",
                    "SMA", "EMA", "CMF", "OBV", "VWAP", "WMA", "DEMA"
                };
            }

            foreach (var indicatorName in enabledIndicators)
            {
                try
                {
                    var indicatorData = CalculateIndicator(indicatorName);
                    if (indicatorData != null)
                    {
                        results[indicatorName] = indicatorData;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error calculating {indicatorName}: {ex.Message}");
                }
            }

            return results;
        }

        /// <summary>
        /// Calculate a specific indicator with default parameters
        /// </summary>
        public Dictionary<string, double[]> CalculateIndicator(string indicatorName)
        {
            IBacktestIndicator indicator = CreateIndicator(indicatorName);
            if (indicator == null)
                return null;

            return indicator.Calculate(
                _marketData.Close,
                _marketData.High,
                _marketData.Low,
                _marketData.Volume
            );
        }

        /// <summary>
        /// Calculate indicator with specific parameters
        /// </summary>
        public Dictionary<string, double[]> CalculateIndicatorWithParameters(
            string indicatorName,
            Dictionary<string, object> parameters)
        {
            IBacktestIndicator indicator = CreateIndicatorWithParameters(indicatorName, parameters);
            if (indicator == null)
                return null;

            return indicator.Calculate(
                _marketData.Close,
                _marketData.High,
                _marketData.Low,
                _marketData.Volume
            );
        }

        /// <summary>
        /// Create indicator instance with default parameters
        /// </summary>
        private IBacktestIndicator CreateIndicator(string indicatorName)
        {
            return indicatorName switch
            {
                "RSI" => new RSI(window: 14, overbought: 70, oversold: 30),
                "MACD" => new MACD(fastPeriod: 12, slowPeriod: 26, signalPeriod: 9),
                "BB" => new BollingerBands(window: 20, stdDev: 2.0),
                "ATR" => new ATR(window: 14),
                "STOCH" => new Stochastic(kWindow: 14, dWindow: 3, smoothK: 3, overbought: 80, oversold: 20),
                "WILLIAMSR" => new WilliamsR(window: 14, overbought: -20, oversold: -80),
                "CCI" => new CCI(window: 20, overbought: 100, oversold: -100),
                "ADX" => new ADX(window: 14, trendThreshold: 25),
                "SMA" => new SMA(window: 50),
                "EMA" => new EMA(window: 20),
                "CMF" => new CMF(window: 20),
                "OBV" => new OBV(),
                "VWAP" => new VWAP(),
                "WMA" => new WMA(window: 20),
                "DEMA" => new DEMA(window: 20),
                _ => null
            };
        }

        /// <summary>
        /// Create indicator with specific parameters
        /// </summary>
        private IBacktestIndicator CreateIndicatorWithParameters(
            string indicatorName,
            Dictionary<string, object> parameters)
        {
            try
            {
                return indicatorName switch
                {
                    "RSI" => new RSI(
                        window: GetIntParameter(parameters, "window", 14),
                        overbought: GetDoubleParameter(parameters, "overbought", 70),
                        oversold: GetDoubleParameter(parameters, "oversold", 30)),

                    "MACD" => new MACD(
                        fastPeriod: GetIntParameter(parameters, "fast_period", 12),
                        slowPeriod: GetIntParameter(parameters, "slow_period", 26),
                        signalPeriod: GetIntParameter(parameters, "signal_period", 9)),

                    "BB" => new BollingerBands(
                        window: GetIntParameter(parameters, "window", 20),
                        stdDev: GetDoubleParameter(parameters, "std_dev", 2.0)),

                    "ATR" => new ATR(
                        window: GetIntParameter(parameters, "window", 14)),

                    "STOCH" => new Stochastic(
                        kWindow: GetIntParameter(parameters, "k_window", 14),
                        dWindow: GetIntParameter(parameters, "d_window", 3),
                        smoothK: GetIntParameter(parameters, "smooth_k", 3),
                        overbought: GetDoubleParameter(parameters, "overbought", 80),
                        oversold: GetDoubleParameter(parameters, "oversold", 20)),

                    "WILLIAMSR" => new WilliamsR(
                        window: GetIntParameter(parameters, "window", 14),
                        overbought: GetDoubleParameter(parameters, "overbought", -20),
                        oversold: GetDoubleParameter(parameters, "oversold", -80)),

                    "CCI" => new CCI(
                        window: GetIntParameter(parameters, "window", 20),
                        overbought: GetDoubleParameter(parameters, "overbought", 100),
                        oversold: GetDoubleParameter(parameters, "oversold", -100)),

                    "ADX" => new ADX(
                        window: GetIntParameter(parameters, "window", 14),
                        trendThreshold: GetDoubleParameter(parameters, "trend_threshold", 25)),

                    "SMA" => new SMA(
                        window: GetIntParameter(parameters, "window", 50)),

                    "EMA" => new EMA(
                        window: GetIntParameter(parameters, "window", 20)),

                    "CMF" => new CMF(
                        window: GetIntParameter(parameters, "window", 20)),

                    "OBV" => new OBV(),

                    "VWAP" => new VWAP(),

                    "WMA" => new WMA(
                        window: GetIntParameter(parameters, "window", 20)),

                    "DEMA" => new DEMA(
                        window: GetIntParameter(parameters, "window", 20)),

                    _ => null
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating indicator {indicatorName} with parameters: {ex.Message}");
                return null;
            }
        }

        private int GetIntParameter(Dictionary<string, object> parameters, string key, int defaultValue)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                return Convert.ToInt32(value);
            }
            return defaultValue;
        }

        private double GetDoubleParameter(Dictionary<string, object> parameters, string key, double defaultValue)
        {
            if (parameters.TryGetValue(key, out var value))
            {
                return Convert.ToDouble(value);
            }
            return defaultValue;
        }

        /// <summary>
        /// Calculate indicators specifically required by a strategy
        /// </summary>
        public Dictionary<string, Dictionary<string, double[]>> CalculateStrategyIndicators(Strategy strategy)
        {
            var results = new Dictionary<string, Dictionary<string, double[]>>();
            var processedIndicators = new HashSet<string>();

            // Collect all indicators from entry and exit rules
            var allRules = new List<StrategyRule>();
            if (strategy.EntryRules != null)
                allRules.AddRange(strategy.EntryRules);
            if (strategy.ExitRules != null)
                allRules.AddRange(strategy.ExitRules);

            foreach (var rule in allRules)
            {
                // Skip if already processed
                if (processedIndicators.Contains(rule.IndicatorName))
                    continue;

                try
                {
                    var indicatorData = CalculateIndicatorWithParameters(rule.IndicatorName, rule.Parameters);
                    if (indicatorData != null)
                    {
                        results[rule.IndicatorName] = indicatorData;
                        processedIndicators.Add(rule.IndicatorName);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error calculating {rule.IndicatorName} for strategy {strategy.Name}: {ex.Message}");
                }
            }

            return results;
        }
    }
}
