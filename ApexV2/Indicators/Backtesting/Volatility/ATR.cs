using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Volatility
{
    /// <summary>
    /// Average True Range (ATR) Indicator
    /// Measures market volatility by decomposing the entire range of price for that period
    /// Critical for position sizing and stop loss placement
    /// </summary>
    public class ATR : BacktestIndicatorBase
    {
        public override string IndicatorType => "ATR";
        public override string DisplayName => "Average True Range";
        public override string Description => "Volatility indicator measuring the average range of price movements";

        /// <summary>
        /// Create ATR with specified parameters
        /// </summary>
        /// <param name="window">Period for averaging (typically 14)</param>
        public ATR(int window = 14)
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

            if (highs == null || lows == null)
                throw new ArgumentException("ATR requires high and low prices");

            if (highs.Length != closes.Length || lows.Length != closes.Length)
                throw new ArgumentException("Price arrays must have the same length");

            int window = GetParameter<int>("window");
            int length = closes.Length;

            if (length < window + 1)
                throw new ArgumentException($"Not enough data points. Need at least {window + 1}, got {length}");

            var trueRange = new double[length];
            var atrValues = new double[length];

            // Calculate True Range for each period
            // TR = max(high - low, |high - previous_close|, |low - previous_close|)
            trueRange[0] = highs[0] - lows[0]; // First period has no previous close

            for (int i = 1; i < length; i++)
            {
                double highLow = highs[i] - lows[i];
                double highPrevClose = Math.Abs(highs[i] - closes[i - 1]);
                double lowPrevClose = Math.Abs(lows[i] - closes[i - 1]);

                trueRange[i] = Math.Max(highLow, Math.Max(highPrevClose, lowPrevClose));
            }

            // Fill initial values with NaN
            for (int i = 0; i < window; i++)
            {
                atrValues[i] = double.NaN;
            }

            // Calculate first ATR as simple average of TR
            double sum = 0;
            for (int i = 1; i <= window; i++)
            {
                sum += trueRange[i];
            }
            atrValues[window] = sum / window;

            // Calculate subsequent ATR values using smoothed average
            // ATR = ((Prior ATR × (n-1)) + Current TR) / n
            for (int i = window + 1; i < length; i++)
            {
                atrValues[i] = ((atrValues[i - 1] * (window - 1)) + trueRange[i]) / window;
            }

            return new Dictionary<string, double[]>
            {
                { "atr", atrValues },
                { "true_range", trueRange }
            };
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("window", val =>
            {
                int window = Convert.ToInt32(val);
                return window > 0 && window <= 100;
            });
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window") + 1;
        }

        public override IBacktestIndicator Clone()
        {
            return new ATR(GetParameter<int>("window"));
        }

        /// <summary>
        /// Calculate stop loss price based on ATR
        /// </summary>
        /// <param name="entryPrice">Entry price of the position</param>
        /// <param name="atrValue">Current ATR value</param>
        /// <param name="multiplier">ATR multiplier (typically 2.0 for stop loss)</param>
        /// <param name="isLong">True for long position, false for short</param>
        /// <returns>Stop loss price</returns>
        public static double CalculateStopLoss(double entryPrice, double atrValue, double multiplier, bool isLong)
        {
            if (double.IsNaN(atrValue) || atrValue <= 0)
                throw new ArgumentException("Invalid ATR value");

            double atrDistance = atrValue * multiplier;

            if (isLong)
            {
                return entryPrice - atrDistance; // Stop below entry for long
            }
            else
            {
                return entryPrice + atrDistance; // Stop above entry for short
            }
        }

        /// <summary>
        /// Calculate take profit price based on ATR
        /// </summary>
        /// <param name="entryPrice">Entry price of the position</param>
        /// <param name="atrValue">Current ATR value</param>
        /// <param name="multiplier">ATR multiplier (typically 3.0 for take profit)</param>
        /// <param name="isLong">True for long position, false for short</param>
        /// <returns>Take profit price</returns>
        public static double CalculateTakeProfit(double entryPrice, double atrValue, double multiplier, bool isLong)
        {
            if (double.IsNaN(atrValue) || atrValue <= 0)
                throw new ArgumentException("Invalid ATR value");

            double atrDistance = atrValue * multiplier;

            if (isLong)
            {
                return entryPrice + atrDistance; // Target above entry for long
            }
            else
            {
                return entryPrice - atrDistance; // Target below entry for short
            }
        }

        /// <summary>
        /// Calculate position size based on ATR risk
        /// </summary>
        /// <param name="accountSize">Total account size</param>
        /// <param name="riskPercent">Percentage of account to risk (e.g., 0.02 for 2%)</param>
        /// <param name="entryPrice">Entry price</param>
        /// <param name="atrValue">Current ATR value</param>
        /// <param name="atrMultiplier">ATR multiplier for stop loss</param>
        /// <returns>Number of shares/contracts to buy</returns>
        public static double CalculatePositionSize(
            double accountSize,
            double riskPercent,
            double entryPrice,
            double atrValue,
            double atrMultiplier = 2.0)
        {
            if (accountSize <= 0 || riskPercent <= 0 || entryPrice <= 0)
                throw new ArgumentException("Invalid parameters");

            if (double.IsNaN(atrValue) || atrValue <= 0)
                return 0;

            // Calculate dollar risk per share
            double riskPerShare = atrValue * atrMultiplier;

            // Calculate total dollar risk
            double totalRisk = accountSize * riskPercent;

            // Calculate position size
            double positionSize = totalRisk / riskPerShare;

            return Math.Floor(positionSize); // Round down to whole shares
        }

        /// <summary>
        /// Detect volatility expansion (ATR increasing)
        /// </summary>
        public static bool DetectVolatilityExpansion(double[] atrValues, int index, int lookback = 5)
        {
            if (index < lookback || double.IsNaN(atrValues[index]))
                return false;

            // Check if ATR is consistently increasing
            for (int i = 1; i <= lookback; i++)
            {
                if (atrValues[index - i + 1] <= atrValues[index - i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Detect volatility contraction (ATR decreasing - potential breakout setup)
        /// </summary>
        public static bool DetectVolatilityContraction(double[] atrValues, int index, int lookback = 5)
        {
            if (index < lookback || double.IsNaN(atrValues[index]))
                return false;

            // Check if ATR is consistently decreasing
            for (int i = 1; i <= lookback; i++)
            {
                if (atrValues[index - i + 1] >= atrValues[index - i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Check if volatility is high relative to recent history
        /// </summary>
        public static bool IsHighVolatility(double[] atrValues, int index, int lookbackPeriods = 20, double threshold = 1.5)
        {
            if (index < lookbackPeriods || double.IsNaN(atrValues[index]))
                return false;

            double currentATR = atrValues[index];

            // Calculate average ATR over lookback period
            double sum = 0;
            int count = 0;
            for (int i = index - lookbackPeriods; i < index; i++)
            {
                if (!double.IsNaN(atrValues[i]))
                {
                    sum += atrValues[i];
                    count++;
                }
            }

            if (count == 0)
                return false;

            double avgATR = sum / count;

            // Current ATR is significantly higher than average
            return currentATR > (avgATR * threshold);
        }

        /// <summary>
        /// Check if volatility is low relative to recent history (potential breakout setup)
        /// </summary>
        public static bool IsLowVolatility(double[] atrValues, int index, int lookbackPeriods = 20, double threshold = 0.7)
        {
            if (index < lookbackPeriods || double.IsNaN(atrValues[index]))
                return false;

            double currentATR = atrValues[index];

            // Calculate average ATR over lookback period
            double sum = 0;
            int count = 0;
            for (int i = index - lookbackPeriods; i < index; i++)
            {
                if (!double.IsNaN(atrValues[i]))
                {
                    sum += atrValues[i];
                    count++;
                }
            }

            if (count == 0)
                return false;

            double avgATR = sum / count;

            // Current ATR is significantly lower than average
            return currentATR < (avgATR * threshold);
        }

        /// <summary>
        /// Calculate trailing stop loss that moves with price
        /// </summary>
        public static double CalculateTrailingStop(
            double currentPrice,
            double atrValue,
            double multiplier,
            bool isLong,
            double? previousStop = null)
        {
            if (double.IsNaN(atrValue) || atrValue <= 0)
                throw new ArgumentException("Invalid ATR value");

            double atrDistance = atrValue * multiplier;
            double newStop;

            if (isLong)
            {
                newStop = currentPrice - atrDistance;
                // Trail stop up only, never down
                if (previousStop.HasValue)
                {
                    newStop = Math.Max(newStop, previousStop.Value);
                }
            }
            else
            {
                newStop = currentPrice + atrDistance;
                // Trail stop down only, never up
                if (previousStop.HasValue)
                {
                    newStop = Math.Min(newStop, previousStop.Value);
                }
            }

            return newStop;
        }

        /// <summary>
        /// Normalize ATR as percentage of price (for comparing across different instruments)
        /// </summary>
        public static double CalculateATRPercent(double atrValue, double currentPrice)
        {
            if (double.IsNaN(atrValue) || currentPrice <= 0)
                return double.NaN;

            return (atrValue / currentPrice) * 100;
        }
    }
}
