using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.MovingAverages
{
    /// <summary>
    /// Double Exponential Moving Average (DEMA)
    /// More responsive than EMA, reduces lag
    /// DEMA = 2*EMA - EMA(EMA)
    /// </summary>
    public class DEMA : BacktestIndicatorBase
    {
        public override string IndicatorType => "DEMA";
        public override string DisplayName => "Double Exponential Moving Average";
        public override string Description => "Faster moving average with reduced lag";

        public DEMA(int window)
        {
            if (window <= 0)
                throw new ArgumentException("Window must be greater than 0");

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

            // Calculate first EMA
            var ema1 = CalculateEMA(closes, window);

            // Calculate EMA of EMA
            var ema2 = CalculateEMAFromArray(ema1, window);

            // Calculate DEMA = 2*EMA - EMA(EMA)
            var demaValues = new double[length];
            for (int i = 0; i < length; i++)
            {
                if (double.IsNaN(ema1[i]) || double.IsNaN(ema2[i]))
                {
                    demaValues[i] = double.NaN;
                }
                else
                {
                    demaValues[i] = (2 * ema1[i]) - ema2[i];
                }
            }

            return new Dictionary<string, double[]>
            {
                { "value", demaValues }
            };
        }

        private double[] CalculateEMA(double[] prices, int period)
        {
            int length = prices.Length;
            var ema = new double[length];
            double multiplier = 2.0 / (period + 1);

            for (int i = 0; i < period - 1; i++)
            {
                ema[i] = double.NaN;
            }

            double sum = 0;
            for (int i = 0; i < period; i++)
            {
                sum += prices[i];
            }
            ema[period - 1] = sum / period;

            for (int i = period; i < length; i++)
            {
                ema[i] = (prices[i] - ema[i - 1]) * multiplier + ema[i - 1];
            }

            return ema;
        }

        private double[] CalculateEMAFromArray(double[] values, int period)
        {
            int length = values.Length;
            var ema = new double[length];
            double multiplier = 2.0 / (period + 1);

            // Find first non-NaN value
            int firstValid = 0;
            while (firstValid < length && double.IsNaN(values[firstValid]))
            {
                ema[firstValid] = double.NaN;
                firstValid++;
            }

            if (firstValid + period > length)
            {
                for (int i = firstValid; i < length; i++)
                {
                    ema[i] = double.NaN;
                }
                return ema;
            }

            // Fill up to period
            for (int i = firstValid; i < firstValid + period - 1; i++)
            {
                ema[i] = double.NaN;
            }

            // Calculate first EMA value
            double sum = 0;
            for (int i = firstValid; i < firstValid + period; i++)
            {
                sum += values[i];
            }
            ema[firstValid + period - 1] = sum / period;

            // Calculate subsequent EMA values
            for (int i = firstValid + period; i < length; i++)
            {
                ema[i] = (values[i] - ema[i - 1]) * multiplier + ema[i - 1];
            }

            return ema;
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("window", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 500);
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window") * 2;
        }

        public override IBacktestIndicator Clone()
        {
            return new DEMA(GetParameter<int>("window"));
        }
    }
}
