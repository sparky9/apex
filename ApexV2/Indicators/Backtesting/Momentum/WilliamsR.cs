using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Momentum
{
    /// <summary>
    /// Williams %R Indicator
    /// Momentum indicator measuring overbought/oversold levels
    /// Ranges from -100 to 0 (inverted scale)
    /// </summary>
    public class WilliamsR : BacktestIndicatorBase
    {
        public override string IndicatorType => "WILLIAMSR";
        public override string DisplayName => "Williams %R";
        public override string Description => "Momentum indicator showing overbought/oversold (-100 to 0)";

        public WilliamsR(int window = 14, double overbought = -20, double oversold = -80)
        {
            if (window <= 0)
                throw new ArgumentException("Window must be greater than 0");

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
            if (closes == null || highs == null || lows == null)
                throw new ArgumentException("Williams %R requires close, high, and low prices");

            int window = GetParameter<int>("window");
            int length = closes.Length;
            var wrValues = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (i < window - 1)
                {
                    wrValues[i] = double.NaN;
                    continue;
                }

                // Find highest high and lowest low
                double highestHigh = highs[i];
                double lowestLow = lows[i];

                for (int j = 0; j < window; j++)
                {
                    highestHigh = Math.Max(highestHigh, highs[i - j]);
                    lowestLow = Math.Min(lowestLow, lows[i - j]);
                }

                // Calculate Williams %R
                double range = highestHigh - lowestLow;
                if (range > 0)
                {
                    wrValues[i] = ((highestHigh - closes[i]) / range) * -100;
                }
                else
                {
                    wrValues[i] = -50;
                }
            }

            return new Dictionary<string, double[]>
            {
                { "value", wrValues }
            };
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("window", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 100);
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window");
        }

        public override IBacktestIndicator Clone()
        {
            return new WilliamsR(
                GetParameter<int>("window"),
                GetParameter<double>("overbought"),
                GetParameter<double>("oversold")
            );
        }

        public bool IsOversold(double wrValue)
        {
            return !double.IsNaN(wrValue) && wrValue < GetParameter<double>("oversold");
        }

        public bool IsOverbought(double wrValue)
        {
            return !double.IsNaN(wrValue) && wrValue > GetParameter<double>("overbought");
        }
    }
}
