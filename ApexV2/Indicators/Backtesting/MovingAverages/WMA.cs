using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.MovingAverages
{
    /// <summary>
    /// Weighted Moving Average (WMA)
    /// More weight to recent prices in linear fashion
    /// More responsive than SMA, less smooth than EMA
    /// </summary>
    public class WMA : BacktestIndicatorBase
    {
        public override string IndicatorType => "WMA";
        public override string DisplayName => "Weighted Moving Average";
        public override string Description => "Moving average with linear weights on recent prices";

        public WMA(int window)
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

            if (length < window)
                throw new ArgumentException($"Not enough data points. Need at least {window}, got {length}");

            var wmaValues = new double[length];

            // Calculate weight denominator (sum of weights 1+2+3+...+n)
            double weightDenominator = (window * (window + 1)) / 2.0;

            // Fill initial values with NaN
            for (int i = 0; i < window - 1; i++)
            {
                wmaValues[i] = double.NaN;
            }

            // Calculate WMA for each period
            for (int i = window - 1; i < length; i++)
            {
                double weightedSum = 0;

                for (int j = 0; j < window; j++)
                {
                    // Weight increases linearly (most recent = highest weight)
                    int weight = window - j;
                    weightedSum += closes[i - j] * weight;
                }

                wmaValues[i] = weightedSum / weightDenominator;
            }

            return new Dictionary<string, double[]>
            {
                { "value", wmaValues }
            };
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("window", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 500);
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window");
        }

        public override IBacktestIndicator Clone()
        {
            return new WMA(GetParameter<int>("window"));
        }
    }
}
