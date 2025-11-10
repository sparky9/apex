using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Momentum
{
    /// <summary>
    /// Commodity Channel Index (CCI)
    /// Measures deviation from average price
    /// Typically ranges from -100 to +100, but can exceed
    /// </summary>
    public class CCI : BacktestIndicatorBase
    {
        public override string IndicatorType => "CCI";
        public override string DisplayName => "Commodity Channel Index";
        public override string Description => "Measures deviation from average price";

        public CCI(int window = 20, double overbought = 100, double oversold = -100)
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
                throw new ArgumentException("CCI requires close, high, and low prices");

            int window = GetParameter<int>("window");
            int length = closes.Length;
            var cciValues = new double[length];

            // Calculate typical price
            var typicalPrice = new double[length];
            for (int i = 0; i < length; i++)
            {
                typicalPrice[i] = (highs[i] + lows[i] + closes[i]) / 3.0;
            }

            for (int i = 0; i < length; i++)
            {
                if (i < window - 1)
                {
                    cciValues[i] = double.NaN;
                    continue;
                }

                // Calculate SMA of typical price
                double sum = 0;
                for (int j = 0; j < window; j++)
                {
                    sum += typicalPrice[i - j];
                }
                double sma = sum / window;

                // Calculate mean deviation
                double meanDeviation = 0;
                for (int j = 0; j < window; j++)
                {
                    meanDeviation += Math.Abs(typicalPrice[i - j] - sma);
                }
                meanDeviation /= window;

                // Calculate CCI
                if (meanDeviation > 0)
                {
                    cciValues[i] = (typicalPrice[i] - sma) / (0.015 * meanDeviation);
                }
                else
                {
                    cciValues[i] = 0;
                }
            }

            return new Dictionary<string, double[]>
            {
                { "value", cciValues }
            };
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("window", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 200);
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window");
        }

        public override IBacktestIndicator Clone()
        {
            return new CCI(
                GetParameter<int>("window"),
                GetParameter<double>("overbought"),
                GetParameter<double>("oversold")
            );
        }

        public bool IsOversold(double cciValue)
        {
            return !double.IsNaN(cciValue) && cciValue < GetParameter<double>("oversold");
        }

        public bool IsOverbought(double cciValue)
        {
            return !double.IsNaN(cciValue) && cciValue > GetParameter<double>("overbought");
        }
    }
}
