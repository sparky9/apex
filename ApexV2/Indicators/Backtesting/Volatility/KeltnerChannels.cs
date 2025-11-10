using System;
using System.Collections.Generic;

namespace ApexV2.Indicators.Backtesting.Volatility
{
    /// <summary>
    /// Keltner Channels
    /// Volatility-based bands using ATR instead of standard deviation
    /// Similar to Bollinger Bands but uses ATR for width
    /// </summary>
    public class KeltnerChannels : BacktestIndicatorBase
    {
        public override string IndicatorType => "KELTNER";
        public override string DisplayName => "Keltner Channels";
        public override string Description => "Volatility bands using EMA and ATR";

        public KeltnerChannels(int window = 20, double multiplier = 2.0, int atrWindow = 10)
        {
            if (window <= 0 || atrWindow <= 0)
                throw new ArgumentException("Windows must be greater than 0");

            if (multiplier <= 0)
                throw new ArgumentException("Multiplier must be greater than 0");

            Parameters["window"] = window;
            Parameters["multiplier"] = multiplier;
            Parameters["atr_window"] = atrWindow;
        }

        public override Dictionary<string, double[]> Calculate(
            double[] closes,
            double[]? highs = null,
            double[]? lows = null,
            double[]? volumes = null)
        {
            if (closes == null || highs == null || lows == null)
                throw new ArgumentException("Keltner Channels require close, high, and low prices");

            int window = GetParameter<int>("window");
            int atrWindow = GetParameter<int>("atr_window");
            double multiplier = GetParameter<double>("multiplier");
            int length = closes.Length;

            // Calculate EMA for middle line
            var middleLine = CalculateEMA(closes, window);

            // Calculate ATR
            var atr = CalculateATR(closes, highs, lows, atrWindow);

            // Calculate upper and lower bands
            var upperBand = new double[length];
            var lowerBand = new double[length];

            for (int i = 0; i < length; i++)
            {
                if (double.IsNaN(middleLine[i]) || double.IsNaN(atr[i]))
                {
                    upperBand[i] = double.NaN;
                    lowerBand[i] = double.NaN;
                }
                else
                {
                    upperBand[i] = middleLine[i] + (multiplier * atr[i]);
                    lowerBand[i] = middleLine[i] - (multiplier * atr[i]);
                }
            }

            return new Dictionary<string, double[]>
            {
                { "upper", upperBand },
                { "middle", middleLine },
                { "lower", lowerBand }
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

        private double[] CalculateATR(double[] closes, double[] highs, double[] lows, int period)
        {
            int length = closes.Length;
            var atr = new double[length];
            var tr = new double[length];

            tr[0] = highs[0] - lows[0];

            for (int i = 1; i < length; i++)
            {
                double hl = highs[i] - lows[i];
                double hc = Math.Abs(highs[i] - closes[i - 1]);
                double lc = Math.Abs(lows[i] - closes[i - 1]);
                tr[i] = Math.Max(hl, Math.Max(hc, lc));
            }

            for (int i = 0; i < period; i++)
            {
                atr[i] = double.NaN;
            }

            double sum = 0;
            for (int i = 1; i <= period; i++)
            {
                sum += tr[i];
            }
            atr[period] = sum / period;

            for (int i = period + 1; i < length; i++)
            {
                atr[i] = ((atr[i - 1] * (period - 1)) + tr[i]) / period;
            }

            return atr;
        }

        public override bool ValidateParameters()
        {
            return ValidateParameter("window", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 200) &&
                   ValidateParameter("atr_window", val => Convert.ToInt32(val) > 0 && Convert.ToInt32(val) <= 100) &&
                   ValidateParameter("multiplier", val => Convert.ToDouble(val) > 0 && Convert.ToDouble(val) <= 5.0);
        }

        public override int GetMinimumDataPoints()
        {
            return Math.Max(GetParameter<int>("window"), GetParameter<int>("atr_window")) + 1;
        }

        public override IBacktestIndicator Clone()
        {
            return new KeltnerChannels(
                GetParameter<int>("window"),
                GetParameter<double>("multiplier"),
                GetParameter<int>("atr_window")
            );
        }

        public static bool IsPriceAboveChannel(double price, double upperBand)
        {
            return !double.IsNaN(upperBand) && price > upperBand;
        }

        public static bool IsPriceBelowChannel(double price, double lowerBand)
        {
            return !double.IsNaN(lowerBand) && price < lowerBand;
        }
    }
}
