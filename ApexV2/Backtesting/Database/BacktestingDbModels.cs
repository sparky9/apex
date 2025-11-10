using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ApexV2.Backtesting.Database
{
    /// <summary>
    /// Database entity for saved strategies
    /// </summary>
    [Table("SavedStrategies")]
    public class SavedStrategyEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string StrategyType { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string GenerationMethod { get; set; } = string.Empty;

        [Required]
        public string EntryRulesJson { get; set; } = string.Empty;

        [Required]
        public string ExitRulesJson { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; }

        public DateTime? LastModifiedDate { get; set; }

        public bool IsFavorite { get; set; }

        [MaxLength(500)]
        public string? Tags { get; set; }

        // Navigation property
        public virtual ICollection<BacktestResultEntity> BacktestResults { get; set; } = new List<BacktestResultEntity>();
    }

    /// <summary>
    /// Database entity for backtest results
    /// </summary>
    [Table("BacktestResults")]
    public class BacktestResultEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Foreign key to strategy
        public int StrategyId { get; set; }

        [ForeignKey("StrategyId")]
        public virtual SavedStrategyEntity? Strategy { get; set; }

        [Required]
        [MaxLength(20)]
        public string Symbol { get; set; } = string.Empty;

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public DateTime RunDate { get; set; }

        // Backtest parameters
        public double InitialCapital { get; set; }

        public double PositionSize { get; set; }

        public double Commission { get; set; }

        public double Slippage { get; set; }

        // Performance metrics
        public double TotalReturn { get; set; }

        public double AnnualizedReturn { get; set; }

        public double SharpeRatio { get; set; }

        public double SortinoRatio { get; set; }

        public double CalmarRatio { get; set; }

        public double ProfitFactor { get; set; }

        public double MaxDrawdown { get; set; }

        public double WinRate { get; set; }

        public int TotalTrades { get; set; }

        public int WinningTrades { get; set; }

        public int LosingTrades { get; set; }

        public double AverageWin { get; set; }

        public double AverageLoss { get; set; }

        public double LargestWin { get; set; }

        public double LargestLoss { get; set; }

        public double AverageHoldingPeriod { get; set; }

        public int MaxConsecutiveWins { get; set; }

        public int MaxConsecutiveLosses { get; set; }

        // Robustness metrics (stored as JSON)
        public string? MonteCarloResultsJson { get; set; }

        public string? WalkForwardResultsJson { get; set; }

        // Equity and drawdown curves (stored as JSON)
        public string EquityCurveJson { get; set; } = string.Empty;

        public string DrawdownCurveJson { get; set; } = string.Empty;

        // All trades (stored as JSON)
        public string TradesJson { get; set; } = string.Empty;

        // User notes
        [MaxLength(2000)]
        public string? Notes { get; set; }

        public bool IsBookmarked { get; set; }
    }

    /// <summary>
    /// Database entity for strategy templates
    /// </summary>
    [Table("StrategyTemplates")]
    public class StrategyTemplateEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        [MaxLength(50)]
        public string StrategyType { get; set; } = string.Empty;

        [Required]
        public string EntryRulesJson { get; set; } = string.Empty;

        [Required]
        public string ExitRulesJson { get; set; } = string.Empty;

        public bool IsBuiltIn { get; set; }

        public DateTime CreatedDate { get; set; }

        public int TimesUsed { get; set; }
    }

    /// <summary>
    /// Database entity for batch backtest sessions
    /// </summary>
    [Table("BatchBacktestSessions")]
    public class BatchBacktestSessionEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        public DateTime StartTime { get; set; }

        public DateTime? EndTime { get; set; }

        public int TotalStrategies { get; set; }

        public int CompletedStrategies { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } = "Running";

        [MaxLength(500)]
        public string? Symbols { get; set; }

        public string ConfigurationJson { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Notes { get; set; }
    }
}
