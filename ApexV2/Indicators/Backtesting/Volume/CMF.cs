using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Volume
{
    /// <summary>
    /// Chaikin Money Flow (CMF)
    /// Measures buying/selling pressure over a period
    /// Ranges from -1 to +1
    /// </summary>
    public class CMF : BacktestIndicatorBase
    {
        public override string IndicatorType => "CMF";
        public override string DisplayName => "Chaikin Money Flow";
        public override string Description => "Measures buying/selling pressure (-1 to +1)";

        public CMF(int window = 20)
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
            if (closes == null || highs == null || lows == null || volumes == null)
                throw new ArgumentException("CMF requires close, high, low, and volume");

            int window = GetParameter<int>("window");
            int length = closes.Length;
            var cmfValues = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (i < window - 1)
                {
                    cmfValues[i] = double.NaN;
                    continue;
                }

                double sumMoneyFlowVolume = 0;
                double sumVolume = 0;

                for (int j = 0; j < window; j++)
                {
                    int idx = i - j;
                    double range = highs[idx] - lows[idx];

                    if (range > 0)
                    {
                        double moneyFlowMultiplier = ((closes[idx] - lows[idx]) - (highs[idx] - closes[idx])) / range;
                        double moneyFlowVolume = moneyFlowMultiplier * volumes[idx];

                        sumMoneyFlowVolume += moneyFlowVolume;
                        sumVolume += volumes[idx];
                    }
                }

                cmfValues[i] = sumVolume > 0 ? sumMoneyFlowVolume / sumVolume : 0;
            }

            return new Dictionary<string, double[]>
            {
                { "value", cmfValues }
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
            return new CMF(GetParameter<int>("window"));
        }

        public static bool IsPositive(double cmfValue)
        {
            return !double.IsNaN(cmfValue) && cmfValue > 0;
        }

        public static bool IsNegative(double cmfValue)
        {
            return !double.IsNaN(cmfValue) && cmfValue < 0;
        }
    }
}
