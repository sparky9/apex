using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Momentum
{
    /// <summary>
    /// Stochastic Oscillator Indicator
    /// Compares closing price to price range over a period
    /// %K (fast) and %D (slow) lines ranging from 0-100
    /// </summary>
    public class Stochastic : BacktestIndicatorBase
    {
        public override string IndicatorType => "STOCH";
        public override string DisplayName => "Stochastic Oscillator";
        public override string Description => "Momentum indicator comparing close to price range (0-100)";

        public Stochastic(int kWindow = 14, int dWindow = 3, int smoothK = 3, double overbought = 80, double oversold = 20)
        {
            if (kWindow <= 0 || dWindow <= 0 || smoothK <= 0)
                throw new ArgumentException("All windows must be greater than 0");

            Parameters["k_window"] = kWindow;
            Parameters["d_window"] = dWindow;
            Parameters["smooth_k"] = smoothK;
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
                throw new ArgumentException("Stochastic requires close, high, and low prices");

            int kWindow = GetParameter<int>("k_window");
            int dWindow = GetParameter<int>("d_window");
            int smoothK = GetParameter<int>("smooth_k");
            int length = closes.Length;

            var rawK = new double[length];
            var smoothedK = new double[length];
            var percentD = new double[length];

            // Calculate raw %K
            for (int i = 0; i < length; i++)
            {
                if (i < kWindow - 1)
                {
                    rawK[i] = double.NaN;
                    continue;
                }

                // Find highest high and lowest low over K period
                double highestHigh = highs[i];
                double lowestLow = lows[i];

                for (int j = 0; j < kWindow; j++)
                {
                    highestHigh = Math.Max(highestHigh, highs[i - j]);
                    lowestLow = Math.Min(lowestLow, lows[i - j]);
                }

                // Calculate raw %K
                double range = highestHigh - lowestLow;
                if (range > 0)
                {
                    rawK[i] = ((closes[i] - lowestLow) / range) * 100;
                }
                else
                {
                    rawK[i] = 50; // Neutral if no range
                }
            }

            // Smooth %K (if smoothK > 1)
            if (smoothK == 1)
            {
                Array.Copy(rawK, smoothedK, length);
            }
            else
            {
                for (int i = 0; i < length; i++)
                {
                    if (i < kWindow + smoothK - 2)
                    {
                        smoothedK[i] = double.NaN;
                        continue;
                    }

                    double sum = 0;
                    int count = 0;
                    for (int j = 0; j < smoothK; j++)
                    {
                        if (!double.IsNaN(rawK[i - j]))
                        {
                            sum += rawK[i - j];
                            count++;
                        }
                    }

                    smoothedK[i] = count > 0 ? sum / count : double.NaN;
                }
            }

            // Calculate %D (SMA of smoothed %K)
            for (int i = 0; i < length; i++)
            {
                if (i < kWindow + smoothK + dWindow - 3)
                {
                    percentD[i] = double.NaN;
                    continue;
                }

                double sum = 0;
                int count = 0;
                for (int j = 0; j < dWindow; j++)
                {
                    if (!double.IsNaN(smoothedK[i - j]))
                    {
                        sum += smoothedK[i - j];
                        count++;
                    }
                }

                percentD[i] = count > 0 ? sum / count : double.NaN;
            }

            return new Dictionary<string, double[]>
            {
                { "k", smoothedK },
                { "d", percentD }
            };
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("k_window", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 100) &&
                   ValidateParameter("d_window", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 50) &&
                   ValidateParameter("smooth_k", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 10);
        }

        public override int GetMinimumDataPoints()
        {
            return GetParameter<int>("k_window") + GetParameter<int>("smooth_k") + GetParameter<int>("d_window");
        }

        public override IBacktestIndicator Clone()
        {
            return new Stochastic(
                GetParameter<int>("k_window"),
                GetParameter<int>("d_window"),
                GetParameter<int>("smooth_k"),
                GetParameter<double>("overbought"),
                GetParameter<double>("oversold")
            );
        }

        public bool IsOversold(double kValue)
        {
            return !double.IsNaN(kValue) && kValue < GetParameter<double>("oversold");
        }

        public bool IsOverbought(double kValue)
        {
            return !double.IsNaN(kValue) && kValue > GetParameter<double>("overbought");
        }

        public static bool DetectBullishCrossover(double[] kValues, double[] dValues, int index)
        {
            if (index < 1) return false;
            return kValues[index - 1] <= dValues[index - 1] && kValues[index] > dValues[index];
        }

        public static bool DetectBearishCrossover(double[] kValues, double[] dValues, int index)
        {
            if (index < 1) return false;
            return kValues[index - 1] >= dValues[index - 1] && kValues[index] < dValues[index];
        }
    }
}
