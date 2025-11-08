using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ApexV2.Analysis.Alerts
{
    /// <summary>
    /// Base class for all alert types in the APEX system
    /// </summary>
    public abstract class AlertBase : INotifyPropertyChanged
    {
        private bool _isEnabled = true;
        private DateTime _createdAt = DateTime.Now;
        private DateTime? _lastTriggered;
        private int _triggerCount = 0;
        private AlertPriority _priority = AlertPriority.Medium;
        private string _name = string.Empty;
        private string _description = string.Empty;

        public Guid Id { get; set; } = Guid.NewGuid();
        
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        
        public string Description
        {
            get => _description;
            set => SetProperty(ref _description, value);
        }
        
        public string Symbol { get; set; } = string.Empty;
        
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }
        
        public AlertPriority Priority
        {
            get => _priority;
            set => SetProperty(ref _priority, value);
        }
        
        public DateTime CreatedAt
        {
            get => _createdAt;
            set => SetProperty(ref _createdAt, value);
        }
        
        public DateTime? LastTriggered
        {
            get => _lastTriggered;
            set => SetProperty(ref _lastTriggered, value);
        }
        
        public int TriggerCount
        {
            get => _triggerCount;
            set => SetProperty(ref _triggerCount, value);
        }
        
        public DateTime? ExpiresAt { get; set; }
        public string Author { get; set; } = Environment.UserName;
        public List<string> Tags { get; set; } = new();
        public Dictionary<string, object> Metadata { get; set; } = new();

        // Alert evaluation
        public abstract AlertType AlertType { get; }
        public abstract bool ShouldTrigger(AlertContext context);
        public abstract string GetTriggerMessage(AlertContext context);
        
        // Validation
        public virtual List<string> Validate()
        {
            var errors = new List<string>();
            
            if (string.IsNullOrWhiteSpace(Name))
                errors.Add("Alert name is required");
                
            if (string.IsNullOrWhiteSpace(Symbol))
                errors.Add("Symbol is required");
                
            if (ExpiresAt.HasValue && ExpiresAt.Value <= DateTime.Now)
                errors.Add("Expiration date must be in the future");
                
            return errors;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(backingStore, value))
                return false;

            backingStore = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    /// <summary>
    /// Price-based alert for monitoring stock price movements
    /// </summary>
    public class PriceAlert : AlertBase
    {
        private decimal _targetPrice;
        private PriceAlertCondition _condition = PriceAlertCondition.Above;
        private decimal _tolerancePercent = 0.0m;

        public override AlertType AlertType => AlertType.Price;

        public decimal TargetPrice
        {
            get => _targetPrice;
            set => SetProperty(ref _targetPrice, value);
        }

        public PriceAlertCondition Condition
        {
            get => _condition;
            set => SetProperty(ref _condition, value);
        }

        public decimal TolerancePercent
        {
            get => _tolerancePercent;
            set => SetProperty(ref _tolerancePercent, value);
        }

        public override bool ShouldTrigger(AlertContext context)
        {
            if (context.CurrentPrice == null) return false;

            var currentPrice = context.CurrentPrice.Value;
            var tolerance = TargetPrice * (TolerancePercent / 100);

            return Condition switch
            {
                PriceAlertCondition.Above => currentPrice >= (TargetPrice - tolerance),
                PriceAlertCondition.Below => currentPrice <= (TargetPrice + tolerance),
                PriceAlertCondition.Equals => Math.Abs(currentPrice - TargetPrice) <= tolerance,
                _ => false
            };
        }

        public override string GetTriggerMessage(AlertContext context)
        {
            var condition = Condition.ToString().ToLower();
            return $"{Symbol} price {condition} target: {context.CurrentPrice:C} (Target: {TargetPrice:C})";
        }

        public override List<string> Validate()
        {
            var errors = base.Validate();
            
            if (TargetPrice <= 0)
                errors.Add("Target price must be greater than zero");
                
            if (TolerancePercent < 0 || TolerancePercent > 50)
                errors.Add("Tolerance percent must be between 0 and 50");
                
            return errors;
        }
    }

    /// <summary>
    /// Percentage change alert for monitoring price movements
    /// </summary>
    public class PercentageChangeAlert : AlertBase
    {
        private decimal _changePercent;
        private ChangeDirection _direction = ChangeDirection.Either;
        private ChangeTimeframe _timeframe = ChangeTimeframe.Daily;

        public override AlertType AlertType => AlertType.PercentageChange;

        public decimal ChangePercent
        {
            get => _changePercent;
            set => SetProperty(ref _changePercent, value);
        }

        public ChangeDirection Direction
        {
            get => _direction;
            set => SetProperty(ref _direction, value);
        }

        public ChangeTimeframe Timeframe
        {
            get => _timeframe;
            set => SetProperty(ref _timeframe, value);
        }

        public override bool ShouldTrigger(AlertContext context)
        {
            if (context.CurrentPrice == null || context.ReferencePrice == null) return false;

            var currentPrice = context.CurrentPrice.Value;
            var referencePrice = context.ReferencePrice.Value;
            var actualChangePercent = ((currentPrice - referencePrice) / referencePrice) * 100;

            return Direction switch
            {
                ChangeDirection.Up => actualChangePercent >= ChangePercent,
                ChangeDirection.Down => actualChangePercent <= -ChangePercent,
                ChangeDirection.Either => Math.Abs(actualChangePercent) >= ChangePercent,
                _ => false
            };
        }

        public override string GetTriggerMessage(AlertContext context)
        {
            var currentPrice = context.CurrentPrice!.Value;
            var referencePrice = context.ReferencePrice!.Value;
            var actualChangePercent = ((currentPrice - referencePrice) / referencePrice) * 100;
            
            return $"{Symbol} moved {actualChangePercent:F2}% to {currentPrice:C} (from {referencePrice:C})";
        }

        public override List<string> Validate()
        {
            var errors = base.Validate();
            
            if (ChangePercent <= 0)
                errors.Add("Change percentage must be greater than zero");
                
            if (ChangePercent > 100)
                errors.Add("Change percentage should not exceed 100%");
                
            return errors;
        }
    }

    /// <summary>
    /// Volume-based alert for unusual trading activity
    /// </summary>
    public class VolumeAlert : AlertBase
    {
        private long _targetVolume;
        private VolumeCondition _condition = VolumeCondition.Above;
        private decimal _averageVolumeMultiplier = 2.0m;

        public override AlertType AlertType => AlertType.Volume;

        public long TargetVolume
        {
            get => _targetVolume;
            set => SetProperty(ref _targetVolume, value);
        }

        public VolumeCondition Condition
        {
            get => _condition;
            set => SetProperty(ref _condition, value);
        }

        public decimal AverageVolumeMultiplier
        {
            get => _averageVolumeMultiplier;
            set => SetProperty(ref _averageVolumeMultiplier, value);
        }

        public override bool ShouldTrigger(AlertContext context)
        {
            var currentVolume = context.CurrentVolume;
            var averageVolume = context.AverageVolume;

            return Condition switch
            {
                VolumeCondition.Above => currentVolume > TargetVolume,
                VolumeCondition.Below => currentVolume < TargetVolume,
                VolumeCondition.AboveAverage => averageVolume > 0 && currentVolume > (averageVolume * AverageVolumeMultiplier),
                _ => false
            };
        }

        public override string GetTriggerMessage(AlertContext context)
        {
            return Condition switch
            {
                VolumeCondition.Above => $"{Symbol} volume above target: {context.CurrentVolume:N0} (Target: {TargetVolume:N0})",
                VolumeCondition.Below => $"{Symbol} volume below target: {context.CurrentVolume:N0} (Target: {TargetVolume:N0})",
                VolumeCondition.AboveAverage => $"{Symbol} unusual volume: {context.CurrentVolume:N0} ({AverageVolumeMultiplier:F1}x average)",
                _ => $"{Symbol} volume alert triggered"
            };
        }

        public override List<string> Validate()
        {
            var errors = base.Validate();
            
            if (TargetVolume <= 0 && Condition != VolumeCondition.AboveAverage)
                errors.Add("Target volume must be greater than zero");
                
            if (AverageVolumeMultiplier <= 0)
                errors.Add("Average volume multiplier must be greater than zero");
                
            return errors;
        }
    }

    /// <summary>
    /// Indicator-based alert for technical analysis signals
    /// </summary>
    public class IndicatorAlert : AlertBase
    {
        private string _indicatorName = string.Empty;
        private string _indicatorParameter = string.Empty;
        private decimal _targetValue;
        private IndicatorCondition _condition = IndicatorCondition.Above;

        public override AlertType AlertType => AlertType.Indicator;

        public string IndicatorName
        {
            get => _indicatorName;
            set => SetProperty(ref _indicatorName, value);
        }

        public string IndicatorParameter
        {
            get => _indicatorParameter;
            set => SetProperty(ref _indicatorParameter, value);
        }

        public decimal TargetValue
        {
            get => _targetValue;
            set => SetProperty(ref _targetValue, value);
        }

        public IndicatorCondition Condition
        {
            get => _condition;
            set => SetProperty(ref _condition, value);
        }

        public override bool ShouldTrigger(AlertContext context)
        {
            if (context.IndicatorValues == null || !context.IndicatorValues.ContainsKey(IndicatorName))
                return false;

            var indicatorValue = context.IndicatorValues[IndicatorName];

            return Condition switch
            {
                IndicatorCondition.Above => indicatorValue > TargetValue,
                IndicatorCondition.Below => indicatorValue < TargetValue,
                IndicatorCondition.CrossesAbove => context.PreviousIndicatorValues?.ContainsKey(IndicatorName) == true &&
                    context.PreviousIndicatorValues[IndicatorName] <= TargetValue && indicatorValue > TargetValue,
                IndicatorCondition.CrossesBelow => context.PreviousIndicatorValues?.ContainsKey(IndicatorName) == true &&
                    context.PreviousIndicatorValues[IndicatorName] >= TargetValue && indicatorValue < TargetValue,
                _ => false
            };
        }

        public override string GetTriggerMessage(AlertContext context)
        {
            var indicatorValue = context.IndicatorValues![IndicatorName];
            var condition = Condition.ToString().Replace("Crosses", "crossed ").ToLower();
            
            return $"{Symbol} {IndicatorName} {condition} {TargetValue}: Current value {indicatorValue:F2}";
        }

        public override List<string> Validate()
        {
            var errors = base.Validate();
            
            if (string.IsNullOrWhiteSpace(IndicatorName))
                errors.Add("Indicator name is required");
                
            return errors;
        }
    }

    /// <summary>
    /// Time-based alert for market events
    /// </summary>
    public class TimeBasedAlert : AlertBase
    {
        private TimeSpan _targetTime;
        private TimeAlertType _timeAlertType = TimeAlertType.Daily;
        private List<DayOfWeek> _activeDays = new();

        public override AlertType AlertType => AlertType.TimeBased;

        public TimeSpan TargetTime
        {
            get => _targetTime;
            set => SetProperty(ref _targetTime, value);
        }

        public TimeAlertType TimeAlertType
        {
            get => _timeAlertType;
            set => SetProperty(ref _timeAlertType, value);
        }

        public List<DayOfWeek> ActiveDays
        {
            get => _activeDays;
            set => SetProperty(ref _activeDays, value);
        }

        public override bool ShouldTrigger(AlertContext context)
        {
            var now = DateTime.Now;
            
            // Check if today is an active day
            if (ActiveDays.Any() && !ActiveDays.Contains(now.DayOfWeek))
                return false;

            return TimeAlertType switch
            {
                TimeAlertType.Daily => Math.Abs((now.TimeOfDay - TargetTime).TotalMinutes) < 1,
                TimeAlertType.MarketOpen => Math.Abs((now.TimeOfDay - new TimeSpan(9, 30, 0)).TotalMinutes) < 1,
                TimeAlertType.MarketClose => Math.Abs((now.TimeOfDay - new TimeSpan(16, 0, 0)).TotalMinutes) < 1,
                _ => false
            };
        }

        public override string GetTriggerMessage(AlertContext context)
        {
            return TimeAlertType switch
            {
                TimeAlertType.Daily => $"Daily alert for {Symbol} at {TargetTime:hh\\:mm}",
                TimeAlertType.MarketOpen => $"Market open alert for {Symbol}",
                TimeAlertType.MarketClose => $"Market close alert for {Symbol}",
                _ => $"Time-based alert for {Symbol}"
            };
        }
    }

    /// <summary>
    /// Context information provided when evaluating alerts
    /// </summary>
    public class AlertContext
    {
        public string Symbol { get; set; } = string.Empty;
        public decimal? CurrentPrice { get; set; }
        public decimal? ReferencePrice { get; set; } // Previous close, day open, etc.
        public long CurrentVolume { get; set; }
        public long AverageVolume { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public Dictionary<string, decimal>? IndicatorValues { get; set; }
        public Dictionary<string, decimal>? PreviousIndicatorValues { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new();
    }

    /// <summary>
    /// Alert execution result
    /// </summary>
    public class AlertTriggerResult
    {
        public AlertBase Alert { get; set; } = null!;
        public bool WasTriggered { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime TriggeredAt { get; set; } = DateTime.Now;
        public AlertContext Context { get; set; } = null!;
        public Exception? Error { get; set; }
    }

    // Enums
    public enum AlertType
    {
        Price,
        PercentageChange,
        Volume,
        Indicator,
        TimeBased,
        Pattern,
        Custom
    }

    public enum AlertPriority
    {
        Low,
        Medium,
        High,
        Critical
    }

    public enum PriceAlertCondition
    {
        Above,
        Below,
        Equals
    }

    public enum VolumeCondition
    {
        Above,
        Below,
        AboveAverage
    }

    public enum IndicatorCondition
    {
        Above,
        Below,
        CrossesAbove,
        CrossesBelow
    }

    public enum ChangeDirection
    {
        Up,
        Down,
        Either
    }

    public enum ChangeTimeframe
    {
        Intraday,
        Daily,
        Weekly,
        Monthly
    }

    public enum TimeAlertType
    {
        Daily,
        MarketOpen,
        MarketClose
    }
}
