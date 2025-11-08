using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using ApexV2.Charts.Drawing;
using ApexV2.Charts.Models;
using ApexV2.Charts.Export;

namespace ApexV2.Analysis.Patterns
{
    /// <summary>
    /// Enumeration of pattern types supported by the pattern recognition system
    /// </summary>
    public enum PatternType
    {
        // Reversal Patterns
        HeadAndShoulders,
        InverseHeadAndShoulders,
        DoubleTop,
        DoubleBottom,
        TripleTop,
        TripleBottom,
        
        // Continuation Patterns
        Triangle,
        AscendingTriangle,
        DescendingTriangle,
        SymmetricalTriangle,
        Flag,
        BullishFlag,
        BearishFlag,
        Pennant,
        Wedge,
        RisingWedge,
        FallingWedge,
        
        // Support/Resistance
        SupportLevel,
        ResistanceLevel,
        TrendLine,
        Channel,
        
        // Candlestick Patterns
        Doji,
        Hammer,
        ShootingStar,
        Engulfing,
        BullishEngulfing,
        BearishEngulfing,
        Harami,
        MorningStar,
        EveningStar,
        
        // Custom Patterns
        Custom
    }

    /// <summary>
    /// Pattern reliability levels
    /// </summary>
    public enum PatternReliability
    {
        Low,
        Medium,
        High,
        VeryHigh
    }

    /// <summary>
    /// Pattern direction/bias
    /// </summary>
    public enum PatternDirection
    {
        Bullish,
        Bearish,
        Neutral
    }

    /// <summary>
    /// Pattern completion status
    /// </summary>
    public enum PatternStatus
    {
        Forming,
        Completed,
        Confirmed,
        Broken,
        Failed
    }

    /// <summary>
    /// Represents a point in price/time space for pattern analysis
    /// </summary>
    public class PatternPoint
    {
        public DateTime Time { get; set; }
        public decimal Price { get; set; }
        public string Description { get; set; } = "";
        public bool IsSignificant { get; set; }
        
        public PatternPoint() { }
        
        public PatternPoint(DateTime time, decimal price, string description = "")
        {
            Time = time;
            Price = price;
            Description = description;
        }
    }

    /// <summary>
    /// Base class for all detected patterns
    /// </summary>
    public abstract class PatternBase : INotifyPropertyChanged
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Symbol { get; set; } = "";
        public string Name { get; set; } = "";
        public PatternType Type { get; set; }
        public PatternDirection Direction { get; set; }
        public PatternStatus Status { get; set; }
        public PatternReliability Reliability { get; set; }
        
        public DateTime DetectedAt { get; set; } = DateTime.Now;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        
        public decimal ConfidenceScore { get; set; } // 0.0 to 1.0
        public decimal MinPrice { get; set; }
        public decimal MaxPrice { get; set; }
        public decimal EntryPrice { get; set; }
        public decimal? StopLoss { get; set; }
        public decimal? TakeProfit { get; set; }
        
        public List<PatternPoint> KeyPoints { get; set; } = new();
        public Dictionary<string, object> Properties { get; set; } = new();
        
        public string Description { get; set; } = "";
        public string Formation { get; set; } = "";
        public string Implications { get; set; } = "";
        
        /// <summary>
        /// Validate the pattern data
        /// </summary>
        public virtual List<string> Validate()
        {
            var errors = new List<string>();
            
            if (string.IsNullOrWhiteSpace(Symbol))
                errors.Add("Symbol is required");
                
            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("Pattern name is required");
                
            if (ConfidenceScore < 0 || ConfidenceScore > 1)
                errors.Add("Confidence score must be between 0 and 1");
                
            if (StartTime >= EndTime)
                errors.Add("Start time must be before end time");
                
            if (KeyPoints.Count < 2)
                errors.Add("Pattern must have at least 2 key points");
                
            return errors;
        }

        /// <summary>
        /// Calculate pattern height (price range)
        /// </summary>
        public decimal GetPatternHeight()
        {
            return MaxPrice - MinPrice;
        }

        /// <summary>
        /// Calculate pattern duration
        /// </summary>
        public TimeSpan GetPatternDuration()
        {
            return EndTime - StartTime;
        }

        /// <summary>
        /// Check if pattern is currently active
        /// </summary>
        public bool IsActive()
        {
            return Status == PatternStatus.Forming || Status == PatternStatus.Completed;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Triangle pattern implementation
    /// </summary>
    public class TrianglePattern : PatternBase
    {
        public PatternPoint UpperTrendLine1 { get; set; } = new();
        public PatternPoint UpperTrendLine2 { get; set; } = new();
        public PatternPoint LowerTrendLine1 { get; set; } = new();
        public PatternPoint LowerTrendLine2 { get; set; } = new();
        
        public decimal BreakoutLevel { get; set; }
        public decimal TargetPrice { get; set; }
        public bool IsBreakoutConfirmed { get; set; }
        
        public TrianglePattern()
        {
            Type = PatternType.Triangle;
            Name = "Triangle";
        }

        public override List<string> Validate()
        {
            var errors = base.Validate();
            
            if (UpperTrendLine1.Price <= LowerTrendLine1.Price)
                errors.Add("Upper trend line must be above lower trend line");
                
            return errors;
        }
    }

    /// <summary>
    /// Head and Shoulders pattern implementation
    /// </summary>
    public class HeadAndShouldersPattern : PatternBase
    {
        public PatternPoint LeftShoulder { get; set; } = new();
        public PatternPoint Head { get; set; } = new();
        public PatternPoint RightShoulder { get; set; } = new();
        public PatternPoint NecklineLeft { get; set; } = new();
        public PatternPoint NecklineRight { get; set; } = new();
        
        public decimal NecklinePrice { get; set; }
        public decimal TargetPrice { get; set; }
        public bool IsNecklineBroken { get; set; }
        
        public HeadAndShouldersPattern()
        {
            Type = PatternType.HeadAndShoulders;
            Name = "Head and Shoulders";
            Direction = PatternDirection.Bearish;
        }

        public override List<string> Validate()
        {
            var errors = base.Validate();
            
            if (Head.Price <= LeftShoulder.Price || Head.Price <= RightShoulder.Price)
                errors.Add("Head must be higher than both shoulders");
                
            return errors;
        }
    }

    /// <summary>
    /// Double Top/Bottom pattern implementation
    /// </summary>
    public class DoubleTopBottomPattern : PatternBase
    {
        public PatternPoint FirstPeak { get; set; } = new();
        public PatternPoint SecondPeak { get; set; } = new();
        public PatternPoint Valley { get; set; } = new();
        
        public decimal ConfirmationLevel { get; set; }
        public decimal TargetPrice { get; set; }
        public bool IsConfirmed { get; set; }
        public decimal PeakDifference { get; set; }
        
        public DoubleTopBottomPattern()
        {
            Name = "Double Top/Bottom";
        }

        public override List<string> Validate()
        {
            var errors = base.Validate();
            
            if (Type == PatternType.DoubleTop)
            {
                if (FirstPeak.Price <= Valley.Price || SecondPeak.Price <= Valley.Price)
                    errors.Add("Peaks must be above valley for double top");
            }
            else if (Type == PatternType.DoubleBottom)
            {
                if (FirstPeak.Price >= Valley.Price || SecondPeak.Price >= Valley.Price)
                    errors.Add("Valleys must be below peak for double bottom");
            }
                
            return errors;
        }
    }

    /// <summary>
    /// Support/Resistance level pattern
    /// </summary>
    public class SupportResistancePattern : PatternBase
    {
        public decimal Level { get; set; }
        public int TouchCount { get; set; }
        public List<PatternPoint> TouchPoints { get; set; } = new();
        public decimal Strength { get; set; } // 0.0 to 1.0
        public TimeSpan Duration { get; set; }
        
        public SupportResistancePattern()
        {
            Direction = PatternDirection.Neutral;
        }

        public override List<string> Validate()
        {
            var errors = base.Validate();
            
            if (TouchCount < 2)
                errors.Add("Support/Resistance must have at least 2 touch points");
                
            if (Strength < 0 || Strength > 1)
                errors.Add("Strength must be between 0 and 1");
                
            return errors;
        }
    }

    /// <summary>
    /// Flag pattern implementation
    /// </summary>
    public class FlagPattern : PatternBase
    {
        public PatternPoint FlagPole { get; set; } = new();
        public List<PatternPoint> FlagBoundary { get; set; } = new();
        public decimal FlagPoleHeight { get; set; }
        public decimal BreakoutTarget { get; set; }
        public bool IsBreakoutConfirmed { get; set; }
        
        public FlagPattern()
        {
            Type = PatternType.Flag;
            Name = "Flag";
        }
    }

    /// <summary>
    /// Candlestick pattern implementation
    /// </summary>
    public class CandlestickPattern : PatternBase
    {
        public int CandleCount { get; set; }
        public List<CandlestickData> Candles { get; set; } = new();
        public bool IsReversal { get; set; }
        public decimal SignalStrength { get; set; }
        
        public CandlestickPattern()
        {
            Name = "Candlestick Pattern";
        }

        public override List<string> Validate()
        {
            var errors = base.Validate();
            
            if (CandleCount <= 0)
                errors.Add("Candle count must be positive");
                
            if (Candles.Count != CandleCount)
                errors.Add("Candle count must match candles list size");
                
            return errors;
        }
    }

    /// <summary>
    /// Pattern search criteria for filtering and scanning
    /// </summary>
    public class PatternSearchCriteria
    {
        public string? Symbol { get; set; }
        public List<PatternType> PatternTypes { get; set; } = new();
        public List<PatternDirection> Directions { get; set; } = new();
        public List<PatternStatus> Statuses { get; set; } = new();
        public PatternReliability? MinReliability { get; set; }
        public decimal? MinConfidence { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? SearchText { get; set; }
        public bool IncludeBreakouts { get; set; } = true;
        public bool IncludeFormation { get; set; } = true;
    }

    /// <summary>
    /// Pattern detection result with metadata
    /// </summary>
    public class PatternDetectionResult
    {
        public List<PatternBase> Patterns { get; set; } = new();
        public int TotalFound { get; set; }
        public TimeSpan DetectionTime { get; set; }
        public DateTime DetectionTimestamp { get; set; } = DateTime.Now;
        public Dictionary<PatternType, int> PatternCounts { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Pattern statistics and analytics
    /// </summary>
    public class PatternStatistics
    {
        public int TotalPatterns { get; set; }
        public int ActivePatterns { get; set; }
        public int CompletedPatterns { get; set; }
        public int ConfirmedPatterns { get; set; }
        public Dictionary<PatternType, int> PatternsByType { get; set; } = new();
        public Dictionary<PatternDirection, int> PatternsByDirection { get; set; } = new();
        public Dictionary<PatternReliability, int> PatternsByReliability { get; set; } = new();
        public decimal AverageConfidence { get; set; }
        public DateTime LastDetection { get; set; }
        public TimeSpan TotalDetectionTime { get; set; }
    }
}
