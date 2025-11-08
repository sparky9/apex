using ApexV2.Charts.Models;

namespace ApexV2.Indicators.Engine;

/// <summary>
/// Mathematical helper functions for technical indicators
/// Provides common calculations used across multiple indicators
/// </summary>
public static class IndicatorMath
{
    /// <summary>
    /// Calculate Simple Moving Average (SMA)
    /// </summary>
    /// <param name="values">Values to average</param>
    /// <param name="period">Number of periods</param>
    /// <returns>SMA value</returns>
    public static double SimpleMovingAverage(IEnumerable<double> values, int period)
    {
        var valueArray = values.TakeLast(period).ToArray();
        
        if (valueArray.Length < period)
        {
            return double.NaN;
        }
        
        return valueArray.Average();
    }
    
    /// <summary>
    /// Calculate Exponential Moving Average (EMA)
    /// </summary>
    /// <param name="values">Values to process</param>
    /// <param name="period">EMA period</param>
    /// <param name="previousEma">Previous EMA value (null for first calculation)</param>
    /// <returns>EMA value</returns>
    public static double ExponentialMovingAverage(IEnumerable<double> values, int period, double? previousEma = null)
    {
        var valueArray = values.ToArray();
        
        if (valueArray.Length == 0)
        {
            return double.NaN;
        }
        
        var multiplier = 2.0 / (period + 1);
        var currentValue = valueArray.Last();
        
        if (previousEma.HasValue)
        {
            return (currentValue * multiplier) + (previousEma.Value * (1 - multiplier));
        }
        
        // First EMA calculation - use SMA as seed
        if (valueArray.Length < period)
        {
            return double.NaN;
        }
        
        return SimpleMovingAverage(valueArray.Take(period), period);
    }
    
    /// <summary>
    /// Calculate Typical Price (HLC/3)
    /// </summary>
    /// <param name="candlestick">Candlestick data</param>
    /// <returns>Typical price</returns>
    public static double TypicalPrice(CandlestickData candlestick)
    {
        return ((double)candlestick.High + (double)candlestick.Low + (double)candlestick.Close) / 3.0;
    }
    
    /// <summary>
    /// Calculate Weighted Close (HLCC/4)
    /// </summary>
    /// <param name="candlestick">Candlestick data</param>
    /// <returns>Weighted close price</returns>
    public static double WeightedClose(CandlestickData candlestick)
    {
        return ((double)candlestick.High + (double)candlestick.Low + (2.0 * (double)candlestick.Close)) / 4.0;
    }
    
    /// <summary>
    /// Calculate True Range
    /// </summary>
    /// <param name="current">Current candlestick</param>
    /// <param name="previous">Previous candlestick</param>
    /// <returns>True range value</returns>
    public static double TrueRange(CandlestickData current, CandlestickData? previous)
    {
        if (previous == null)
        {
            return (double)current.High - (double)current.Low;
        }
        
        var highLow = (double)current.High - (double)current.Low;
        var highClose = Math.Abs((double)current.High - (double)previous.Close);
        var lowClose = Math.Abs((double)current.Low - (double)previous.Close);
        
        return Math.Max(highLow, Math.Max(highClose, lowClose));
    }
    
    /// <summary>
    /// Calculate Standard Deviation
    /// </summary>
    /// <param name="values">Values to calculate standard deviation for</param>
    /// <param name="period">Number of periods</param>
    /// <returns>Standard deviation</returns>
    public static double StandardDeviation(IEnumerable<double> values, int period)
    {
        var valueArray = values.TakeLast(period).ToArray();
        
        if (valueArray.Length < period)
        {
            return double.NaN;
        }
        
        var mean = valueArray.Average();
        var squaredDifferences = valueArray.Select(v => Math.Pow(v - mean, 2));
        var variance = squaredDifferences.Average();
        
        return Math.Sqrt(variance);
    }
    
    /// <summary>
    /// Calculate Relative Strength Index (RSI) gain/loss values
    /// </summary>
    /// <param name="prices">Price values</param>
    /// <param name="period">RSI period</param>
    /// <returns>Tuple of (gains, losses)</returns>
    public static (double[] gains, double[] losses) CalculateGainsAndLosses(double[] prices, int period)
    {
        if (prices.Length < 2)
        {
            return (Array.Empty<double>(), Array.Empty<double>());
        }
        
        var gains = new List<double>();
        var losses = new List<double>();
        
        for (int i = 1; i < prices.Length; i++)
        {
            var change = prices[i] - prices[i - 1];
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? Math.Abs(change) : 0);
        }
        
        return (gains.ToArray(), losses.ToArray());
    }
    
    /// <summary>
    /// Calculate MACD values
    /// </summary>
    /// <param name="prices">Price values</param>
    /// <param name="fastPeriod">Fast EMA period</param>
    /// <param name="slowPeriod">Slow EMA period</param>
    /// <param name="signalPeriod">Signal line EMA period</param>
    /// <returns>Tuple of (MACD line, Signal line, Histogram)</returns>
    public static (double[] macdLine, double[] signalLine, double[] histogram) CalculateMACD(
        double[] prices, int fastPeriod, int slowPeriod, int signalPeriod)
    {
        if (prices.Length < slowPeriod)
        {
            return (Array.Empty<double>(), Array.Empty<double>(), Array.Empty<double>());
        }
        
        var fastEma = CalculateEMAArray(prices, fastPeriod);
        var slowEma = CalculateEMAArray(prices, slowPeriod);
        
        var macdLine = new List<double>();
        for (int i = 0; i < Math.Min(fastEma.Length, slowEma.Length); i++)
        {
            if (!double.IsNaN(fastEma[i]) && !double.IsNaN(slowEma[i]))
            {
                macdLine.Add(fastEma[i] - slowEma[i]);
            }
        }
        
        var signalLine = CalculateEMAArray(macdLine.ToArray(), signalPeriod);
        
        var histogram = new List<double>();
        for (int i = 0; i < Math.Min(macdLine.Count, signalLine.Length); i++)
        {
            if (!double.IsNaN(signalLine[i]))
            {
                histogram.Add(macdLine[i] - signalLine[i]);
            }
        }
        
        return (macdLine.ToArray(), signalLine, histogram.ToArray());
    }
    
    /// <summary>
    /// Calculate Bollinger Bands
    /// </summary>
    /// <param name="prices">Price values</param>
    /// <param name="period">Moving average period</param>
    /// <param name="standardDeviations">Number of standard deviations</param>
    /// <returns>Tuple of (Middle band/SMA, Upper band, Lower band)</returns>
    public static (double middleBand, double upperBand, double lowerBand) CalculateBollingerBands(
        IEnumerable<double> prices, int period, double standardDeviations)
    {
        var priceArray = prices.TakeLast(period).ToArray();
        
        if (priceArray.Length < period)
        {
            return (double.NaN, double.NaN, double.NaN);
        }
        
        var sma = SimpleMovingAverage(priceArray, period);
        var stdDev = StandardDeviation(priceArray, period);
        
        var upperBand = sma + (standardDeviations * stdDev);
        var lowerBand = sma - (standardDeviations * stdDev);
        
        return (sma, upperBand, lowerBand);
    }
    
    /// <summary>
    /// Helper method to calculate EMA array
    /// </summary>
    /// <param name="values">Input values</param>
    /// <param name="period">EMA period</param>
    /// <returns>EMA array</returns>
    private static double[] CalculateEMAArray(double[] values, int period)
    {
        if (values.Length < period)
        {
            return Array.Empty<double>();
        }
        
        var result = new double[values.Length - period + 1];
        double? previousEma = null;
        
        for (int i = period - 1; i < values.Length; i++)
        {
            var subset = values.Take(i + 1);
            var ema = ExponentialMovingAverage(subset, period, previousEma);
            result[i - period + 1] = ema;
            previousEma = ema;
        }
        
        return result;
    }
    
    /// <summary>
    /// Validate that a value is not NaN or infinite
    /// </summary>
    /// <param name="value">Value to validate</param>
    /// <returns>True if valid number</returns>
    public static bool IsValidNumber(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value);
    }
    
    /// <summary>
    /// Clamp a value between minimum and maximum bounds
    /// </summary>
    /// <param name="value">Value to clamp</param>
    /// <param name="min">Minimum value</param>
    /// <param name="max">Maximum value</param>
    /// <returns>Clamped value</returns>
    public static double Clamp(double value, double min, double max)
    {
        if (value < min) return min;
        if (value > max) return max;
        return value;
    }
    
    /// <summary>
    /// Calculate percentage change between two values
    /// </summary>
    /// <param name="oldValue">Original value</param>
    /// <param name="newValue">New value</param>
    /// <returns>Percentage change</returns>
    public static double PercentageChange(double oldValue, double newValue)
    {
        if (Math.Abs(oldValue) < double.Epsilon)
        {
            return double.NaN;
        }
        
        return ((newValue - oldValue) / oldValue) * 100.0;
    }
}
