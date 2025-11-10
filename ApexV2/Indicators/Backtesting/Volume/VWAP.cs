using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Volume
{
    /// <summary>
    /// Volume Weighted Average Price (VWAP)
    /// Average price weighted by volume
    /// Often used as a benchmark for institutional trading
    /// </summary>
    public class VWAP : BacktestIndicatorBase
    {
        public override string IndicatorType => "VWAP";
        public override string DisplayName => "Volume Weighted Average Price";
        public override string Description => "Average price weighted by volume";

        public VWAP(int window = 20)
        {
            Parameters["window"] = window;
        }

        public override Dictionary<string, double[]> Calculate(
            double[] closes,
            double[]? highs = null,
            double[]? lows = null,
            double[]? volumes = null)
        {
            if (closes == null || highs == null || lows == null || volumes == null)
                throw new ArgumentException("VWAP requires close, high, low, and volume");

            int window = GetParameter<int>("window");
            int length = closes.Length;
            var vwapValues = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (i < window - 1)
                {
                    vwapValues[i] = double.NaN;
                    continue;
                }

                double sumTypicalPriceVolume = 0;
                double sumVolume = 0;

                for (int j = 0; j < window; j++)
                {
                    int idx = i - j;
                    double typicalPrice = (highs[idx] + lows[idx] + closes[idx]) / 3.0;
                    sumTypicalPriceVolume += typicalPrice * volumes[idx];
                    sumVolume += volumes[idx];
                }

                vwapValues[i] = sumVolume > 0 ? sumTypicalPriceVolume / sumVolume : closes[i];
            }

            return new Dictionary<string, double[]>
            {
                { "value", vwapValues }
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
            return new VWAP(GetParameter<int>("window"));
        }

        public static bool IsPriceAboveVWAP(double price, double vwap)
        {
            return !double.IsNaN(vwap) && price > vwap;
        }

        public static bool IsPriceBelowVWAP(double price, double vwap)
        {
            return !double.IsNaN(vwap) && price < vwap;
        }
    }
}
