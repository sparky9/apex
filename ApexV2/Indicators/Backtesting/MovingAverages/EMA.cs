using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.MovingAverages
{
    /// <summary>
    /// Exponential Moving Average (EMA) Indicator
    /// Gives more weight to recent prices, more responsive than SMA
    /// </summary>
    public class EMA : BacktestIndicatorBase
    {
        public override string IndicatorType => "EMA";
        public override string DisplayName => "Exponential Moving Average";
        public override string Description => "Moving average that gives more weight to recent prices";

        /// <summary>
        /// Create EMA with specified window period
        /// </summary>
        /// <param name="window">Number of periods for averaging (e.g., 12, 26, 50)</param>
        public EMA(int window)
        {
            if (window <= 0)
                throw new ArgumentException("Window must be greater than 0", nameof(window));

            Parameters["window"] = window;
        }

        public override Dictionary<string, double[]> Calculate(
            double[] closes,
            double[]? highs = null,
            double[]? lows = null,
            double[]? volumes = null)
        {
            if (closes == null || closes.Length == 0)
                throw new ArgumentException("Close prices cannot be null or empty");

            int window = GetParameter<int>("window");
            int length = closes.Length;

            if (length < window)
                throw new ArgumentException($"Not enough data points. Need at least {window}, got {length}");

            var emaValues = new double[length];

            // Calculate multiplier (smoothing factor)
            double multiplier = 2.0 / (window + 1);

            // Fill initial values with NaN
            for (int i = 0; i < window - 1; i++)
            {
                emaValues[i] = double.NaN;
            }

            // Use SMA for the first EMA value
            double sum = 0;
            for (int i = 0; i < window; i++)
            {
                sum += closes[i];
            }
            emaValues[window - 1] = sum / window;

            // Calculate EMA for subsequent periods
            // EMA = (Close - EMA(previous)) × multiplier + EMA(previous)
            for (int i = window; i < length; i++)
            {
                emaValues[i] = (closes[i] - emaValues[i - 1]) * multiplier + emaValues[i - 1];
            }

            return new Dictionary<string, double[]>
            {
                { "value", emaValues }
            };
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("window", val =>
            {
                int window = Convert.ToInt32(val);
                return window > 0 && window <= 500;
            });
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window");
        }

        public override IBacktestIndicator Clone()
        {
            return new EMA(GetParameter<int>("window"));
        }

        /// <summary>
        /// Helper method to detect EMA crossover (fast crosses above slow)
        /// </summary>
        public static bool DetectBullishCrossover(double[] fastEMA, double[] slowEMA, int index)
        {
            if (index < 1 || double.IsNaN(fastEMA[index]) || double.IsNaN(slowEMA[index]))
                return false;

            bool previousBelow = fastEMA[index - 1] <= slowEMA[index - 1];
            bool currentAbove = fastEMA[index] > slowEMA[index];

            return previousBelow && currentAbove;
        }

        /// <summary>
        /// Helper method to detect EMA crossunder (fast crosses below slow)
        /// </summary>
        public static bool DetectBearishCrossover(double[] fastEMA, double[] slowEMA, int index)
        {
            if (index < 1 || double.IsNaN(fastEMA[index]) || double.IsNaN(slowEMA[index]))
                return false;

            bool previousAbove = fastEMA[index - 1] >= slowEMA[index - 1];
            bool currentBelow = fastEMA[index] < slowEMA[index];

            return previousAbove && currentBelow;
        }

        /// <summary>
        /// Check if fast EMA is above slow EMA (bullish alignment)
        /// </summary>
        public static bool IsBullishAlignment(double fastEMA, double slowEMA)
        {
            return !double.IsNaN(fastEMA) && !double.IsNaN(slowEMA) && fastEMA > slowEMA;
        }

        /// <summary>
        /// Check if fast EMA is below slow EMA (bearish alignment)
        /// </summary>
        public static bool IsBearishAlignment(double fastEMA, double slowEMA)
        {
            return !double.IsNaN(fastEMA) && !double.IsNaN(slowEMA) && fastEMA < slowEMA;
        }
    }
}
