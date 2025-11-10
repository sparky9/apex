using System;
using System.Collections.Generic;
using System.Linq;

namespace ApexV2.Indicators.Backtesting.Volatility
{
    /// <summary>
    /// Bollinger Bands Indicator
    /// Volatility bands placed above and below a moving average
    /// Width of bands is based on standard deviation of prices
    /// Used for mean reversion and breakout strategies
    /// </summary>
    public class BollingerBands : BacktestIndicatorBase
    {
        public override string IndicatorType => "BB";
        public override string DisplayName => "Bollinger Bands";
        public override string Description => "Volatility bands based on standard deviation around moving average";

        /// <summary>
        /// Create Bollinger Bands with specified parameters
        /// </summary>
        /// <param name="window">Period for moving average and std dev (typically 20)</param>
        /// <param name="stdDev">Number of standard deviations for bands (typically 2.0)</param>
        public BollingerBands(int window = 20, double stdDev = 2.0)
        {
            if (window <= 0)
                throw new ArgumentException("Window must be greater than 0", nameof(window));

            if (stdDev <= 0)
                throw new ArgumentException("Standard deviation multiplier must be greater than 0", nameof(stdDev));

            Parameters["window"] = window;
            Parameters["std_dev"] = stdDev;
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
            double stdDevMultiplier = GetParameter<double>("std_dev");
            int length = closes.Length;

            if (length < window)
                throw new ArgumentException($"Not enough data points. Need at least {window}, got {length}");

            var middleBand = new double[length];  // SMA
            var upperBand = new double[length];
            var lowerBand = new double[length];
            var bandwidth = new double[length];
            var percentB = new double[length];

            // Fill initial values with NaN
            for (int i = 0; i < window - 1; i++)
            {
                middleBand[i] = double.NaN;
                upperBand[i] = double.NaN;
                lowerBand[i] = double.NaN;
                bandwidth[i] = double.NaN;
                percentB[i] = double.NaN;
            }

            // Calculate Bollinger Bands for each period
            for (int i = window - 1; i < length; i++)
            {
                // Calculate middle band (SMA)
                double sum = 0;
                for (int j = 0; j < window; j++)
                {
                    sum += closes[i - j];
                }
                middleBand[i] = sum / window;

                // Calculate standard deviation
                double sumSquaredDiff = 0;
                for (int j = 0; j < window; j++)
                {
                    double diff = closes[i - j] - middleBand[i];
                    sumSquaredDiff += diff * diff;
                }
                double stdDev = Math.Sqrt(sumSquaredDiff / window);

                // Calculate upper and lower bands
                upperBand[i] = middleBand[i] + (stdDevMultiplier * stdDev);
                lowerBand[i] = middleBand[i] - (stdDevMultiplier * stdDev);

                // Calculate bandwidth (measure of volatility)
                bandwidth[i] = (upperBand[i] - lowerBand[i]) / middleBand[i];

                // Calculate %B (position within bands, 0-1)
                // %B = (Price - Lower Band) / (Upper Band - Lower Band)
                double bandRange = upperBand[i] - lowerBand[i];
                if (bandRange > 0)
                {
                    percentB[i] = (closes[i] - lowerBand[i]) / bandRange;
                }
                else
                {
                    percentB[i] = 0.5; // If bands collapsed, price is in middle
                }
            }

            return new Dictionary<string, double[]>
            {
                { "upper", upperBand },
                { "middle", middleBand },
                { "lower", lowerBand },
                { "bandwidth", bandwidth },
                { "percent_b", percentB }
            };
        }

        public override bool ValidateParameters()
        {
            bool windowValid = ValidateParameter("window", val =>
            {
                int window = Convert.ToInt32(val);
                return window > 0 && window <= 200;
            });

            bool stdDevValid = ValidateParameter("std_dev", val =>
            {
                double stdDev = Convert.ToDouble(val);
                return stdDev > 0 && stdDev <= 5.0;
            });

            return windowValid && stdDevValid;
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window");
        }

        public override IBacktestIndicator Clone()
        {
            return new BollingerBands(
                GetParameter<int>("window"),
                GetParameter<double>("std_dev")
            );
        }

        /// <summary>
        /// Check if price touched or broke below lower band
        /// </summary>
        public static bool TouchedLowerBand(double price, double lowerBand, double tolerance = 0.001)
        {
            if (double.IsNaN(lowerBand))
                return false;

            return price <= lowerBand * (1 + tolerance);
        }

        /// <summary>
        /// Check if price touched or broke above upper band
        /// </summary>
        public static bool TouchedUpperBand(double price, double upperBand, double tolerance = 0.001)
        {
            if (double.IsNaN(upperBand))
                return false;

            return price >= upperBand * (1 - tolerance);
        }

        /// <summary>
        /// Check if price is outside bands (potential reversal)
        /// </summary>
        public static bool IsOutsideBands(double price, double upperBand, double lowerBand)
        {
            if (double.IsNaN(upperBand) || double.IsNaN(lowerBand))
                return false;

            return price > upperBand || price < lowerBand;
        }

        /// <summary>
        /// Check if price is inside bands (normal conditions)
        /// </summary>
        public static bool IsInsideBands(double price, double upperBand, double lowerBand)
        {
            if (double.IsNaN(upperBand) || double.IsNaN(lowerBand))
                return false;

            return price >= lowerBand && price <= upperBand;
        }

        /// <summary>
        /// Detect Bollinger Squeeze (low volatility, potential breakout setup)
        /// Bandwidth is at multi-period low
        /// </summary>
        public static bool DetectSqueeze(double[] bandwidth, int index, int lookbackPeriods = 20)
        {
            if (index < lookbackPeriods || double.IsNaN(bandwidth[index]))
                return false;

            double currentBandwidth = bandwidth[index];

            // Check if current bandwidth is lowest in lookback period
            for (int i = index - lookbackPeriods; i < index; i++)
            {
                if (!double.IsNaN(bandwidth[i]) && bandwidth[i] < currentBandwidth)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Detect Bollinger Band expansion (increasing volatility)
        /// </summary>
        public static bool DetectExpansion(double[] bandwidth, int index, int lookback = 5)
        {
            if (index < lookback || double.IsNaN(bandwidth[index]))
                return false;

            // Check if bandwidth is consistently increasing
            for (int i = 1; i <= lookback; i++)
            {
                if (bandwidth[index - i + 1] <= bandwidth[index - i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Detect "Walking the Band" (strong trend - price consistently near one band)
        /// </summary>
        public static bool IsWalkingUpperBand(double[] percentB, int index, int consecutivePeriods = 3, double threshold = 0.85)
        {
            if (index < consecutivePeriods - 1)
                return false;

            for (int i = 0; i < consecutivePeriods; i++)
            {
                if (double.IsNaN(percentB[index - i]) || percentB[index - i] < threshold)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Detect "Walking the Band" on lower band (strong downtrend)
        /// </summary>
        public static bool IsWalkingLowerBand(double[] percentB, int index, int consecutivePeriods = 3, double threshold = 0.15)
        {
            if (index < consecutivePeriods - 1)
                return false;

            for (int i = 0; i < consecutivePeriods; i++)
            {
                if (double.IsNaN(percentB[index - i]) || percentB[index - i] > threshold)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Detect mean reversion setup (price at band, ready to revert to middle)
        /// </summary>
        public static bool DetectMeanReversionSetup(
            double price,
            double upperBand,
            double middleBand,
            double lowerBand,
            bool bullish)
        {
            if (double.IsNaN(upperBand) || double.IsNaN(middleBand) || double.IsNaN(lowerBand))
                return false;

            if (bullish)
            {
                // Price at or below lower band, ready to bounce back to middle
                return price <= lowerBand && price < middleBand;
            }
            else
            {
                // Price at or above upper band, ready to fall back to middle
                return price >= upperBand && price > middleBand;
            }
        }

        /// <summary>
        /// Calculate Bollinger Band width as percentage
        /// Useful for comparing volatility across different price levels
        /// </summary>
        public static double CalculateBandwidthPercent(double upperBand, double lowerBand, double middleBand)
        {
            if (double.IsNaN(upperBand) || double.IsNaN(lowerBand) || double.IsNaN(middleBand) || middleBand == 0)
                return double.NaN;

            return ((upperBand - lowerBand) / middleBand) * 100;
        }
    }
}
