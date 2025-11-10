using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Momentum
{
    /// <summary>
    /// Relative Strength Index (RSI) Indicator
    /// Momentum oscillator that measures the speed and magnitude of price changes
    /// Values range from 0 to 100, with 70+ indicating overbought and 30- indicating oversold
    /// </summary>
    public class RSI : BacktestIndicatorBase
    {
        public override string IndicatorType => "RSI";
        public override string DisplayName => "Relative Strength Index";
        public override string Description => "Momentum oscillator measuring speed and magnitude of price changes (0-100)";

        /// <summary>
        /// Create RSI with specified parameters
        /// </summary>
        /// <param name="window">Number of periods for RSI calculation (typically 14)</param>
        /// <param name="overbought">Overbought threshold (typically 70)</param>
        /// <param name="oversold">Oversold threshold (typically 30)</param>
        public RSI(int window = 14, double overbought = 70, double oversold = 30)
        {
            if (window <= 0)
                throw new ArgumentException("Window must be greater than 0", nameof(window));

            if (overbought <= oversold)
                throw new ArgumentException("Overbought must be greater than oversold");

            Parameters["window"] = window;
            Parameters["overbought"] = overbought;
            Parameters["oversold"] = oversold;
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

            if (length < window + 1)
                throw new ArgumentException($"Not enough data points. Need at least {window + 1}, got {length}");

            var rsiValues = new double[length];
            var gains = new double[length];
            var losses = new double[length];

            // Calculate price changes
            for (int i = 1; i < length; i++)
            {
                double change = closes[i] - closes[i - 1];
                gains[i] = change > 0 ? change : 0;
                losses[i] = change < 0 ? -change : 0;
            }

            // Fill initial values with NaN
            for (int i = 0; i < window; i++)
            {
                rsiValues[i] = double.NaN;
            }

            // Calculate initial average gain and loss
            double avgGain = 0;
            double avgLoss = 0;
            for (int i = 1; i <= window; i++)
            {
                avgGain += gains[i];
                avgLoss += losses[i];
            }
            avgGain /= window;
            avgLoss /= window;

            // Calculate first RSI
            double rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
            rsiValues[window] = 100 - (100 / (1 + rs));

            // Calculate RSI for subsequent periods using smoothed averages
            for (int i = window + 1; i < length; i++)
            {
                avgGain = ((avgGain * (window - 1)) + gains[i]) / window;
                avgLoss = ((avgLoss * (window - 1)) + losses[i]) / window;

                rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
                rsiValues[i] = 100 - (100 / (1 + rs));
            }

            return new Dictionary<string, double[]>
            {
                { "value", rsiValues }
            };
        }

        public override bool ValidateParameters()
        {
            bool windowValid = ValidateParameter("window", val =>
            {
                int window = Convert.ToInt32(val);
                return window > 0 && window <= 100;
            });

            bool overboughtValid = ValidateParameter("overbought", val =>
            {
                double overbought = Convert.ToDouble(val);
                return overbought > 50 && overbought <= 100;
            });

            bool oversoldValid = ValidateParameter("oversold", val =>
            {
                double oversold = Convert.ToDouble(val);
                return oversold >= 0 && oversold < 50;
            });

            return windowValid && overboughtValid && oversoldValid;
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window") + 1;
        }

        public override IBacktestIndicator Clone()
        {
            return new RSI(
                GetParameter<int>("window"),
                GetParameter<double>("overbought"),
                GetParameter<double>("oversold")
            );
        }

        /// <summary>
        /// Check if RSI indicates oversold condition
        /// </summary>
        public bool IsOversold(double rsiValue)
        {
            double oversoldLevel = GetParameter<double>("oversold");
            return !double.IsNaN(rsiValue) && rsiValue < oversoldLevel;
        }

        /// <summary>
        /// Check if RSI indicates overbought condition
        /// </summary>
        public bool IsOverbought(double rsiValue)
        {
            double overboughtLevel = GetParameter<double>("overbought");
            return !double.IsNaN(rsiValue) && rsiValue > overboughtLevel;
        }

        /// <summary>
        /// Check if RSI is rising (momentum building)
        /// </summary>
        public static bool IsRising(double[] rsiValues, int index, int lookback = 1)
        {
            if (index < lookback || double.IsNaN(rsiValues[index]))
                return false;

            return rsiValues[index] > rsiValues[index - lookback];
        }

        /// <summary>
        /// Check if RSI is falling (momentum declining)
        /// </summary>
        public static bool IsFalling(double[] rsiValues, int index, int lookback = 1)
        {
            if (index < lookback || double.IsNaN(rsiValues[index]))
                return false;

            return rsiValues[index] < rsiValues[index - lookback];
        }

        /// <summary>
        /// Detect bullish divergence (price makes lower low but RSI makes higher low)
        /// </summary>
        public static bool DetectBullishDivergence(
            double[] prices,
            double[] rsiValues,
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
                // Check if RSI made a higher low
                if (rsiValues[currentIndex] > rsiValues[prevPriceLowIndex])
                {
                    return true; // Bullish divergence
                }
            }

            return false;
        }

        /// <summary>
        /// Detect bearish divergence (price makes higher high but RSI makes lower high)
        /// </summary>
        public static bool DetectBearishDivergence(
            double[] prices,
            double[] rsiValues,
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
                // Check if RSI made a lower high
                if (rsiValues[currentIndex] < rsiValues[prevPriceHighIndex])
                {
                    return true; // Bearish divergence
                }
            }

            return false;
        }
    }
}
