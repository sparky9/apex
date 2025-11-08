using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ApexV2.Dashboard.Calendar
{
    // Economic Event Types
    public enum EventType
    {
        Earnings,
        Dividend,
        Split,
        EconomicIndicator,
        CentralBankMeeting,
        IPO,
        ConferenceCall,
        ProductLaunch,
        Acquisition,
        Other
    }

    public enum EventImportance
    {
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum EventStatus
    {
        Scheduled,
        InProgress,
        Completed,
        Cancelled,
        Delayed
    }

    // Main Economic Event Model
    public class EconomicEvent
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;
        
        [MaxLength(1000)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        public DateTime EventDate { get; set; }
        
        public DateTime? EndDate { get; set; }
        
        [Required]
        public EventType Type { get; set; }
        
        [Required]
        public EventImportance Importance { get; set; }
        
        [Required]
        public EventStatus Status { get; set; }
        
        [MaxLength(20)]
        public string? Symbol { get; set; }
        
        [MaxLength(200)]
        public string? CompanyName { get; set; }
        
        [MaxLength(50)]
        public string? Country { get; set; }
        
        [MaxLength(50)]
        public string? Currency { get; set; }
        
        public decimal? ActualValue { get; set; }
        public decimal? ForecastValue { get; set; }
        public decimal? PreviousValue { get; set; }
        
        [MaxLength(50)]
        public string? Unit { get; set; }
        
        [MaxLength(500)]
        public string? Source { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public List<EventAlert> Alerts { get; set; } = new();
        public List<EventNote> Notes { get; set; } = new();
    }

    // Earnings-specific model
    public class EarningsEvent : EconomicEvent
    {
        public decimal? EPSActual { get; set; }
        public decimal? EPSEstimate { get; set; }
        public decimal? EPSPrevious { get; set; }
        
        public decimal? RevenueActual { get; set; }
        public decimal? RevenueEstimate { get; set; }
        public decimal? RevenuePrevious { get; set; }
        
        [MaxLength(20)]
        public string? Quarter { get; set; }
        
        public int? FiscalYear { get; set; }
        
        public DateTime? ConferenceCallTime { get; set; }
        
        [MaxLength(500)]
        public string? ConferenceCallNumber { get; set; }
        
        [MaxLength(500)]
        public string? WebcastUrl { get; set; }
    }

    // Economic Indicator model
    public class EconomicIndicator : EconomicEvent
    {
        [MaxLength(100)]
        public string? IndicatorName { get; set; }
        
        [MaxLength(50)]
        public string? ReportingAgency { get; set; }
        
        [MaxLength(20)]
        public string? Frequency { get; set; } // Monthly, Quarterly, etc.
        
        public decimal? Impact { get; set; } // Market impact score
        
        [MaxLength(20)]
        public string? MarketSector { get; set; }
    }

    // Event Alert model
    public class EventAlert
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int EventId { get; set; }
        public EconomicEvent Event { get; set; } = null!;
        
        [Required]
        public DateTime AlertTime { get; set; }
        
        [MaxLength(500)]
        public string? Message { get; set; }
        
        public bool IsEnabled { get; set; } = true;
        public bool HasBeenTriggered { get; set; } = false;
        
        public DateTime? TriggeredAt { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    // Event Notes model
    public class EventNote
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int EventId { get; set; }
        public EconomicEvent Event { get; set; } = null!;
        
        [Required]
        [MaxLength(2000)]
        public string Content { get; set; } = string.Empty;
        
        [MaxLength(100)]
        public string? Author { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Calendar View Models
    public class CalendarFilter
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<EventType> EventTypes { get; set; } = new();
        public List<EventImportance> ImportanceLevels { get; set; } = new();
        public List<string> Countries { get; set; } = new();
        public List<string> Symbols { get; set; } = new();
        public bool ShowOnlyWatchlistSymbols { get; set; } = false;
        public string? SearchTerm { get; set; }
    }

    public class CalendarView
    {
        public DateTime Date { get; set; }
        public List<EconomicEvent> Events { get; set; } = new();
        public int TotalEvents { get; set; }
        public int HighImportanceCount { get; set; }
        public int EarningsCount { get; set; }
        public int EconomicIndicatorCount { get; set; }
    }

    public class CalendarSummary
    {
        public DateTime Date { get; set; }
        public int TotalEvents { get; set; }
        public int HighImportanceEvents { get; set; }
        public int EarningsEvents { get; set; }
        public List<string> TopSymbols { get; set; } = new();
        public bool HasWatchlistEvents { get; set; }
    }

    // Market Hours model
    public class MarketHours
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string Market { get; set; } = string.Empty;
        
        [Required]
        [MaxLength(10)]
        public string Timezone { get; set; } = string.Empty;
        
        public TimeSpan? PreMarketOpen { get; set; }
        public TimeSpan? MarketOpen { get; set; }
        public TimeSpan? MarketClose { get; set; }
        public TimeSpan? PostMarketClose { get; set; }
        
        public bool IsOpen { get; set; }
        public bool IsHoliday { get; set; }
        
        [MaxLength(100)]
        public string? HolidayName { get; set; }
        
        public DateTime Date { get; set; }
        
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    // Event Impact Analysis
    public class EventImpactAnalysis
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int EventId { get; set; }
        public EconomicEvent Event { get; set; } = null!;
        
        [MaxLength(20)]
        public string? Symbol { get; set; }
        
        public decimal? PriceChangePct { get; set; }
        public decimal? VolumeChangePct { get; set; }
        
        public decimal? PreEventPrice { get; set; }
        public decimal? PostEventPrice { get; set; }
        
        public long? PreEventVolume { get; set; }
        public long? PostEventVolume { get; set; }
        
        public DateTime? AnalysisDate { get; set; }
        
        [MaxLength(1000)]
        public string? Notes { get; set; }
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
