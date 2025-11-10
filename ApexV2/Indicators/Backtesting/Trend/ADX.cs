using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Trend
{
    /// <summary>
    /// Average Directional Index (ADX)
    /// Measures trend strength (0-100), not direction
    /// Includes +DI and -DI for trend direction
    /// </summary>
    public class ADX : BacktestIndicatorBase
    {
        public override string IndicatorType => "ADX";
        public override string DisplayName => "Average Directional Index";
        public override string Description => "Measures trend strength (0-100)";

        public ADX(int window = 14, double trendThreshold = 25)
        {
            if (window <= 0)
                throw new ArgumentException("Window must be greater than 0");

            Parameters["window"] = window;
            Parameters["trend_threshold"] = trendThreshold;
        }

        public override Dictionary<string, double[]> Calculate(
            double[] closes,
            double[]? highs = null,
            double[]? lows = null,
            double[]? volumes = null)
        {
            if (closes == null || highs == null || lows == null)
                throw new ArgumentException("ADX requires close, high, and low prices");

            int window = GetParameter<int>("window");
            int length = closes.Length;

            var plusDM = new double[length];
            var minusDM = new double[length];
            var tr = new double[length];
            var plusDI = new double[length];
            var minusDI = new double[length];
            var dx = new double[length];
            var adx = new double[length];

            // Calculate +DM, -DM, and TR
            for (int i = 1; i < length; i++)
            {
                double highDiff = highs[i] - highs[i - 1];
                double lowDiff = lows[i - 1] - lows[i];

                plusDM[i] = (highDiff > lowDiff && highDiff > 0) ? highDiff : 0;
                minusDM[i] = (lowDiff > highDiff && lowDiff > 0) ? lowDiff : 0;

                double hl = highs[i] - lows[i];
                double hc = Math.Abs(highs[i] - closes[i - 1]);
                double lc = Math.Abs(lows[i] - closes[i - 1]);
                tr[i] = Math.Max(hl, Math.Max(hc, lc));
            }

            // Smooth +DM, -DM, and TR
            var smoothedPlusDM = SmoothWilder(plusDM, window);
            var smoothedMinusDM = SmoothWilder(minusDM, window);
            var smoothedTR = SmoothWilder(tr, window);

            // Calculate +DI and -DI
            for (int i = window; i < length; i++)
            {
                if (smoothedTR[i] > 0)
                {
                    plusDI[i] = (smoothedPlusDM[i] / smoothedTR[i]) * 100;
                    minusDI[i] = (smoothedMinusDM[i] / smoothedTR[i]) * 100;
                }
                else
                {
                    plusDI[i] = 0;
                    minusDI[i] = 0;
                }
            }

            // Calculate DX
            for (int i = window; i < length; i++)
            {
                double sum = plusDI[i] + minusDI[i];
                if (sum > 0)
                {
                    dx[i] = (Math.Abs(plusDI[i] - minusDI[i]) / sum) * 100;
                }
                else
                {
                    dx[i] = 0;
                }
            }

            // Calculate ADX (smoothed DX)
            adx = SmoothWilder(dx, window);

            // Set initial values to NaN
            for (int i = 0; i < window * 2; i++)
            {
                if (i < length)
                {
                    plusDI[i] = double.NaN;
                    minusDI[i] = double.NaN;
                    adx[i] = double.NaN;
                }
            }

            return new Dictionary<string, double[]>
            {
                { "adx", adx },
                { "plus_di", plusDI },
                { "minus_di", minusDI }
            };
        }

        private double[] SmoothWilder(double[] values, int period)
        {
            int length = values.Length;
            var smoothed = new double[length];

            // First value is sum
            double sum = 0;
            for (int i = 1; i <= period && i < length; i++)
            {
                sum += values[i];
            }
            smoothed[period] = sum;

            // Subsequent values use Wilder smoothing
            for (int i = period + 1; i < length; i++)
            {
                smoothed[i] = smoothed[i - 1] - (smoothed[i - 1] / period) + values[i];
            }

            return smoothed;
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("window", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 100);
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("window") * 2;
        }

        public override IBacktestIndicator Clone()
        {
            return new ADX(
                GetParameter<int>("window"),
                GetParameter<double>("trend_threshold")
            );
        }

        public bool IsTrendingStrong(double adxValue)
        {
            return !double.IsNaN(adxValue) && adxValue > GetParameter<double>("trend_threshold");
        }

        public static bool IsBullishTrend(double plusDI, double minusDI)
        {
            return !double.IsNaN(plusDI) && !double.IsNaN(minusDI) && plusDI > minusDI;
        }

        public static bool IsBearishTrend(double plusDI, double minusDI)
        {
            return !double.IsNaN(plusDI) && !double.IsNaN(minusDI) && minusDI > plusDI;
        }
    }
}
