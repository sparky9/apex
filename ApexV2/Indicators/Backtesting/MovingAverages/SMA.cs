using System;
using System.Collections.Generic;
using System.Linq;

namespace ApexV2.Indicators.Backtesting.MovingAverages
{
    /// <summary>
    /// Simple Moving Average (SMA) Indicator
    /// Calculates the arithmetic mean of prices over a specified period
    /// </summary>
    public class SMA : BacktestIndicatorBase
    {
        public override string IndicatorType => "SMA";
        public override string DisplayName => "Simple Moving Average";
        public override string Description => "Arithmetic mean of prices over a specified period";

        /// <summary>
        /// Create SMA with specified window period
        /// </summary>
        /// <param name="window">Number of periods for averaging (e.g., 20, 50, 200)</param>
        public SMA(int window)
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

            var smaValues = new double[length];

            // Fill initial values with NaN (not enough data)
            for (int i = 0; i < window - 1; i++)
            {
                smaValues[i] = double.NaN;
            }

            // Calculate SMA for each period
            for (int i = window - 1; i < length; i++)
            {
                double sum = 0;
                for (int j = 0; j < window; j++)
                {
                    sum += closes[i - j];
                }
                smaValues[i] = sum / window;
            }

            return new Dictionary<string, double[]>
            {
                { "value", smaValues }
            };
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("window", val =>
            {
                int window = Convert.ToInt32(val);
                return window > 0 && window <= 500; // Reasonable limits
            });
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window");
        }

        public override IBacktestIndicator Clone()
        {
            return new SMA(GetParameter<int>("window"));
        }

        /// <summary>
        /// Helper method to check if price is above SMA
        /// </summary>
        public static bool IsPriceAboveSMA(double price, double smaValue)
        {
            return !double.IsNaN(smaValue) && price > smaValue;
        }

        /// <summary>
        /// Helper method to check if price is below SMA
        /// </summary>
        public static bool IsPriceBelowSMA(double price, double smaValue)
        {
            return !double.IsNaN(smaValue) && price < smaValue;
        }

        /// <summary>
        /// Helper method to detect SMA crossover (fast crosses above slow)
        /// </summary>
        public static bool DetectBullishCrossover(double[] fastSMA, double[] slowSMA, int index)
        {
            if (index < 1 || double.IsNaN(fastSMA[index]) || double.IsNaN(slowSMA[index]))
                return false;

            bool previousBelow = fastSMA[index - 1] <= slowSMA[index - 1];
            bool currentAbove = fastSMA[index] > slowSMA[index];

            return previousBelow && currentAbove;
        }

        /// <summary>
        /// Helper method to detect SMA crossunder (fast crosses below slow)
        /// </summary>
        public static bool DetectBearishCrossover(double[] fastSMA, double[] slowSMA, int index)
        {
            if (index < 1 || double.IsNaN(fastSMA[index]) || double.IsNaN(slowSMA[index]))
                return false;

            bool previousAbove = fastSMA[index - 1] >= slowSMA[index - 1];
            bool currentBelow = fastSMA[index] < slowSMA[index];

            return previousAbove && currentBelow;
        }
    }
}
