using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting
{
    /// <summary>
    /// Base interface for all backtesting technical indicators
    /// Provides a consistent API for indicator calculation and configuration
    /// </summary>
    public interface IBacktestIndicator
    {
        /// <summary>
        /// Unique identifier for the indicator type (e.g., "SMA", "RSI", "MACD")
        /// </summary>
        string IndicatorType { get; }

        /// <summary>
        /// Human-readable name of the indicator
        /// </summary>
        string DisplayName { get; }

        /// <summary>
        /// Description of what the indicator measures
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Parameters used for this indicator instance
        /// </summary>
        Dictionary<string, object> Parameters { get; }

        /// <summary>
        /// Calculate the indicator values for the given price data
        /// </summary>
        /// <param name="closes">Close prices (required for most indicators)</param>
        /// <param name="highs">High prices (optional, for indicators that need it)</param>
        /// <param name="lows">Low prices (optional, for indicators that need it)</param>
        /// <param name="volumes">Volume data (optional, for volume-based indicators)</param>
        /// <returns>Dictionary of indicator outputs (e.g., {"value": [...], "signal": [...]})</returns>
        Dictionary<string, double[]> Calculate(
            double[] closes,
            double[]? highs = null,
            double[]? lows = null,
            double[]? volumes = null);

        /// <summary>
        /// Validate that the parameters are correct for this indicator
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        bool ValidateParameters();

        /// <summary>
        /// Get the minimum number of data points required to calculate this indicator
        /// </summary>
        int GetMinimumDataPoints();

        /// <summary>
        /// Clone this indicator with the same parameters
        /// </summary>
        IBacktestIndicator Clone();
    }

    /// <summary>
    /// Base abstract class implementing common indicator functionality
    /// </summary>
    public abstract class BacktestIndicatorBase : IBacktestIndicator
    {
        public abstract string IndicatorType { get; }
        public abstract string DisplayName { get; }
        public abstract string Description { get; }
        public Dictionary<string, object> Parameters { get; protected set; }

        protected BacktestIndicatorBase()
        {
            Parameters = new Dictionary<string, object>();
        }

        public abstract Dictionary<string, double[]> Calculate(
            double[] closes,
            double[]? highs = null,
            double[]? lows = null,
            double[]? volumes = null);

        public abstract bool ValidateParameters();
        public abstract int GetMinimumDataPoints();
        public abstract IBacktestIndicator Clone();

        /// <summary>
        /// Helper method to get a parameter value with type safety
        /// </summary>
        protected T GetParameter<T>(string key, T defaultValue = default!)
        {
            if (Parameters.TryGetValue(key, out var value))
            {
                try
                {
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    return defaultValue;
                }
            }
            return defaultValue;
        }

        /// <summary>
        /// Helper method to validate a parameter exists and meets criteria
        /// </summary>
        protected bool ValidateParameter(string key, Func<object, bool> validator)
        {
            if (!Parameters.ContainsKey(key))
                return false;

            return validator(Parameters[key]);
        }

        /// <summary>
        /// Create an array filled with NaN for the warmup period
        /// </summary>
        protected double[] CreateNaNArray(int length)
        {
            var result = new double[length];
            Array.Fill(result, double.NaN);
            return result;
        }
    }
}
