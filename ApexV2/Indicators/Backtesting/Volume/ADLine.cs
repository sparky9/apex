using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Volume
{
    /// <summary>
    /// Accumulation/Distribution Line (A/D Line)
    /// Volume indicator showing flow of money into/out of security
    /// Similar to OBV but considers where price closes within daily range
    /// </summary>
    public class ADLine : BacktestIndicatorBase
    {
        public override string IndicatorType => "ADLINE";
        public override string DisplayName => "Accumulation/Distribution Line";
        public override string Description => "Volume flow indicator based on close location in range";

        public ADLine()
        {
            // A/D Line has no configurable parameters
        }

        public override Dictionary<string, double[]> Calculate(
            double[] closes,
            double[]? highs = null,
            double[]? lows = null,
            double[]? volumes = null)
        {
            if (closes == null || highs == null || lows == null || volumes == null)
                throw new ArgumentException("A/D Line requires close, high, low, and volume");

            int length = closes.Length;
            var adLineValues = new double[length];

            adLineValues[0] = 0;

            for (int i = 0; i < length; i++)
            {
                double range = highs[i] - lows[i];

                double moneyFlowMultiplier;
                if (range > 0)
                {
                    moneyFlowMultiplier = ((closes[i] - lows[i]) - (highs[i] - closes[i])) / range;
                }
                else
                {
                    moneyFlowMultiplier = 0;
                }

                double moneyFlowVolume = moneyFlowMultiplier * volumes[i];

                if (i == 0)
                {
                    adLineValues[i] = moneyFlowVolume;
                }
                else
                {
                    adLineValues[i] = adLineValues[i - 1] + moneyFlowVolume;
                }
            }

            return new Dictionary<string, double[]>
            {
                { "value", adLineValues }
            };
        }

        public override bool ValidateParameters()
        {
            return true; // No parameters
        }

        public override int GetMinimumDataPoints()
        {
            return 1;
        }

        public override IBacktestIndicator Clone()
        {
            return new ADLine();
        }

        public static bool IsRising(double[] adLineValues, int index, int lookback = 1)
        {
            if (index < lookback) return false;
            return adLineValues[index] > adLineValues[index - lookback];
        }

        public static bool IsFalling(double[] adLineValues, int index, int lookback = 1)
        {
            if (index < lookback) return false;
            return adLineValues[index] < adLineValues[index - lookback];
        }
    }
}
