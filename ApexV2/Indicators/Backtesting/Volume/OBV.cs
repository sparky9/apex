using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Volume
{
    /// <summary>
    /// On Balance Volume (OBV)
    /// Cumulative volume indicator based on price direction
    /// Rising OBV = accumulation, Falling OBV = distribution
    /// </summary>
    public class OBV : BacktestIndicatorBase
    {
        public override string IndicatorType => "OBV";
        public override string DisplayName => "On Balance Volume";
        public override string Description => "Cumulative volume based on price direction";

        public OBV()
        {
            // OBV has no configurable parameters
        }

        public override Dictionary<string, double[]> Calculate(
            double[] closes,
            double[]? highs = null,
            double[]? lows = null,
            double[]? volumes = null)
        {
            if (closes == null || volumes == null)
                throw new ArgumentException("OBV requires close prices and volume");

            int length = closes.Length;
            var obvValues = new double[length];

            obvValues[0] = volumes[0];

            for (int i = 1; i < length; i++)
            {
                if (closes[i] > closes[i - 1])
                {
                    obvValues[i] = obvValues[i - 1] + volumes[i];
                }
                else if (closes[i] < closes[i - 1])
                {
                    obvValues[i] = obvValues[i - 1] - volumes[i];
                }
                else
                {
                    obvValues[i] = obvValues[i - 1];
                }
            }

            return new Dictionary<string, double[]>
            {
                { "value", obvValues }
            };
        }

        public override bool ValidateParameters()
        {
            return true; // No parameters to validate
        }

        public override int GetMinimumDataPoints()
        {
            return 2;
        }

        public override IBacktestIndicator Clone()
        {
            return new OBV();
        }

        public static bool IsRising(double[] obvValues, int index, int lookback = 1)
        {
            if (index < lookback) return false;
            return obvValues[index] > obvValues[index - lookback];
        }

        public static bool IsFalling(double[] obvValues, int index, int lookback = 1)
        {
            if (index < lookback) return false;
            return obvValues[index] < obvValues[index - lookback];
        }
    }
}
