using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Trend
{
    /// <summary>
    /// Moving Average Convergence Divergence (MACD) Indicator
    /// Shows relationship between two moving averages of prices
    /// Consists of MACD line, signal line, and histogram
    /// </summary>
    public class MACD : BacktestIndicatorBase
    {
        public override string IndicatorType => "MACD";
        public override string DisplayName => "Moving Average Convergence Divergence";
        public override string Description => "Trend-following momentum indicator showing relationship between two EMAs";

        /// <summary>
        /// Create MACD with specified parameters
        /// </summary>
        /// <param name="fastPeriod">Fast EMA period (typically 12)</param>
        /// <param name="slowPeriod">Slow EMA period (typically 26)</param>
        /// <param name="signalPeriod">Signal line EMA period (typically 9)</param>
        public MACD(int fastPeriod = 12, int slowPeriod = 26, int signalPeriod = 9)
        {
            if (fastPeriod <= 0 || slowPeriod <= 0 || signalPeriod <= 0)
                throw new ArgumentException("All periods must be greater than 0");

            if (fastPeriod >= slowPeriod)
                throw new ArgumentException("Fast period must be less than slow period");

            Parameters["fast_period"] = fastPeriod;
            Parameters["slow_period"] = slowPeriod;
            Parameters["signal_period"] = signalPeriod;
        }

        public override Dictionary<string, double[]> Calculate(
            double[] closes,
            double[]? highs = null,
            double[]? lows = null,
            double[]? volumes = null)
        {
            if (closes == null || closes.Length == 0)
                throw new ArgumentException("Close prices cannot be null or empty");

            int fastPeriod = GetParameter<int>("fast_period");
            int slowPeriod = GetParameter<int>("slow_period");
            int signalPeriod = GetParameter<int>("signal_period");
            int length = closes.Length;

            if (length < slowPeriod + signalPeriod)
                throw new ArgumentException($"Not enough data points. Need at least {slowPeriod + signalPeriod}, got {length}");

            // Calculate fast and slow EMAs
            var fastEMA = CalculateEMA(closes, fastPeriod);
            var slowEMA = CalculateEMA(closes, slowPeriod);

            // Calculate MACD line (fast EMA - slow EMA)
            var macdLine = new double[length];
            for (int i = 0; i < length; i++)
            {
                if (double.IsNaN(fastEMA[i]) || double.IsNaN(slowEMA[i]))
                {
                    macdLine[i] = double.NaN;
                }
                else
                {
                    macdLine[i] = fastEMA[i] - slowEMA[i];
                }
            }

            // Calculate signal line (EMA of MACD line)
            var signalLine = CalculateEMAFromValues(macdLine, signalPeriod, slowPeriod - 1);

            // Calculate histogram (MACD line - signal line)
            var histogram = new double[length];
            for (int i = 0; i < length; i++)
            {
                if (double.IsNaN(macdLine[i]) || double.IsNaN(signalLine[i]))
                {
                    histogram[i] = double.NaN;
                }
                else
                {
                    histogram[i] = macdLine[i] - signalLine[i];
                }
            }

            return new Dictionary<string, double[]>
            {
                { "macd", macdLine },
                { "signal", signalLine },
                { "histogram", histogram }
            };
        }

        /// <summary>
        /// Calculate EMA for price data
        /// </summary>
        private double[] CalculateEMA(double[] prices, int period)
        {
            int length = prices.Length;
            var ema = new double[length];
            double multiplier = 2.0 / (period + 1);

            // Fill initial values with NaN
            for (int i = 0; i < period - 1; i++)
            {
                ema[i] = double.NaN;
            }

            // Use SMA for first EMA value
            double sum = 0;
            for (int i = 0; i < period; i++)
            {
                sum += prices[i];
            }
            ema[period - 1] = sum / period;

            // Calculate EMA for subsequent periods
            for (int i = period; i < length; i++)
            {
                ema[i] = (prices[i] - ema[i - 1]) * multiplier + ema[i - 1];
            }

            return ema;
        }

        /// <summary>
        /// Calculate EMA from already calculated values (for signal line)
        /// </summary>
        private double[] CalculateEMAFromValues(double[] values, int period, int startIndex)
        {
            int length = values.Length;
            var ema = new double[length];
            double multiplier = 2.0 / (period + 1);

            // Fill initial values with NaN
            for (int i = 0; i <= startIndex; i++)
            {
                ema[i] = double.NaN;
            }

            // Find first valid starting point
            int firstValid = startIndex + 1;
            while (firstValid < length && double.IsNaN(values[firstValid]))
            {
                ema[firstValid] = double.NaN;
                firstValid++;
            }

            if (firstValid >= length || firstValid + period > length)
            {
                return ema;
            }

            // Use SMA for first EMA value
            double sum = 0;
            int count = 0;
            for (int i = firstValid; i < firstValid + period && i < length; i++)
            {
                if (!double.IsNaN(values[i]))
                {
                    sum += values[i];
                    count++;
                }
            }

            if (count == 0)
            {
                return ema;
            }

            int emaStartIndex = firstValid + period - 1;
            if (emaStartIndex < length)
            {
                ema[emaStartIndex] = sum / count;

                // Calculate EMA for subsequent periods
                for (int i = emaStartIndex + 1; i < length; i++)
                {
                    if (!double.IsNaN(values[i]))
                    {
                        ema[i] = (values[i] - ema[i - 1]) * multiplier + ema[i - 1];
                    }
                    else
                    {
                        ema[i] = ema[i - 1];
                    }
                }
            }

            return ema;
        }

        public override bool ValidateParameters()
        {
            bool fastValid = ValidateParameter("fast_period", val =>
            {
                int fast = Convert.ToInt32(val);
                return fast > 0 && fast <= 50;
            });

            bool slowValid = ValidateParameter("slow_period", val =>
            {
                int slow = Convert.ToInt32(val);
                int fast = GetParameter<int>("fast_period");
                return slow > fast && slow <= 200;
            });

            bool signalValid = ValidateParameter("signal_period", val =>
            {
                int signal = Convert.ToInt32(val);
                return signal > 0 && signal <= 50;
            });

            return fastValid && slowValid && signalValid;
        }

        public override int GetMinimumDataPoints()
        {
            int slowPeriod = GetParameter<int>("slow_period");
            int signalPeriod = GetParameter<int>("signal_period");
            return slowPeriod + signalPeriod;
        }

        public override IBacktestIndicator Clone()
        {
            return new MACD(
                GetParameter<int>("fast_period"),
                GetParameter<int>("slow_period"),
                GetParameter<int>("signal_period")
            );
        }

        /// <summary>
        /// Detect bullish crossover (MACD crosses above signal)
        /// </summary>
        public static bool DetectBullishCrossover(double[] macdLine, double[] signalLine, int index)
        {
            if (index < 1 || double.IsNaN(macdLine[index]) || double.IsNaN(signalLine[index]))
                return false;

            bool previousBelow = macdLine[index - 1] <= signalLine[index - 1];
            bool currentAbove = macdLine[index] > signalLine[index];

            return previousBelow && currentAbove;
        }

        /// <summary>
        /// Detect bearish crossover (MACD crosses below signal)
        /// </summary>
        public static bool DetectBearishCrossover(double[] macdLine, double[] signalLine, int index)
        {
            if (index < 1 || double.IsNaN(macdLine[index]) || double.IsNaN(signalLine[index]))
                return false;

            bool previousAbove = macdLine[index - 1] >= signalLine[index - 1];
            bool currentBelow = macdLine[index] < signalLine[index];

            return previousAbove && currentBelow;
        }

        /// <summary>
        /// Check if MACD is positive (above zero line - bullish momentum)
        /// </summary>
        public static bool IsPositive(double macdValue)
        {
            return !double.IsNaN(macdValue) && macdValue > 0;
        }

        /// <summary>
        /// Check if MACD is negative (below zero line - bearish momentum)
        /// </summary>
        public static bool IsNegative(double macdValue)
        {
            return !double.IsNaN(macdValue) && macdValue < 0;
        }

        /// <summary>
        /// Detect bullish divergence (price makes lower low but MACD makes higher low)
        /// </summary>
        public static bool DetectBullishDivergence(
            double[] prices,
            double[] macdLine,
            int currentIndex,
            int lookbackPeriods = 20)
        {
            if (currentIndex < lookbackPeriods + 1)
                return false;

            // Find previous low in price
            int prevPriceLowIndex = currentIndex - 1;
            double prevPriceLow = prices[prevPriceLowIndex];

            for (int i = currentIndex - 2; i >= currentIndex - lookbackPeriods; i--)
            {
                if (prices[i] < prevPriceLow)
                {
                    prevPriceLow = prices[i];
                    prevPriceLowIndex = i;
                }
            }

            // Check if current price is lower than previous low
            if (prices[currentIndex] <= prevPriceLow)
            {
                // Check if MACD made a higher low
                if (macdLine[currentIndex] > macdLine[prevPriceLowIndex])
                {
                    return true; // Bullish divergence
                }
            }

            return false;
        }

        /// <summary>
        /// Detect bearish divergence (price makes higher high but MACD makes lower high)
        /// </summary>
        public static bool DetectBearishDivergence(
            double[] prices,
            double[] macdLine,
            int currentIndex,
            int lookbackPeriods = 20)
        {
            if (currentIndex < lookbackPeriods + 1)
                return false;

            // Find previous high in price
            int prevPriceHighIndex = currentIndex - 1;
            double prevPriceHigh = prices[prevPriceHighIndex];

            for (int i = currentIndex - 2; i >= currentIndex - lookbackPeriods; i--)
            {
                if (prices[i] > prevPriceHigh)
                {
                    prevPriceHigh = prices[i];
                    prevPriceHighIndex = i;
                }
            }

            // Check if current price is higher than previous high
            if (prices[currentIndex] >= prevPriceHigh)
            {
                // Check if MACD made a lower high
                if (macdLine[currentIndex] < macdLine[prevPriceHighIndex])
                {
                    return true; // Bearish divergence
                }
            }

            return false;
        }

        /// <summary>
        /// Check if histogram is increasing (momentum strengthening)
        /// </summary>
        public static bool IsHistogramIncreasing(double[] histogram, int index, int lookback = 1)
        {
            if (index < lookback || double.IsNaN(histogram[index]))
                return false;

            return histogram[index] > histogram[index - lookback];
        }

        /// <summary>
        /// Check if histogram is decreasing (momentum weakening)
        /// </summary>
        public static bool IsHistogramDecreasing(double[] histogram, int index, int lookback = 1)
        {
            if (index < lookback || double.IsNaN(histogram[index]))
                return false;

            return histogram[index] < histogram[index - lookback];
        }
    }
}
