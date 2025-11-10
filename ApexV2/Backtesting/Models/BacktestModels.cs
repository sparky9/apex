using System;
using System.Collections.Generic;

namespace ApexV2.Backtesting.Models
{
    /// <summary>
    /// Configuration parameters for a backtest
    /// </summary>
    public class BacktestParameters
    {
        public string Symbol { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double InitialCapital { get; set; } = 10000;
        public double Commission { get; set; } = 0.001; // 0.1%
        public double Slippage { get; set; } = 0.001; // 0.1%

        // Position sizing
        public PositionSizingMethod SizingMethod { get; set; } = PositionSizingMethod.FixedPercentage;
        public double PositionSizePercent { get; set; } = 0.95; // Use 95% of capital
        public double MaxPositionSize { get; set; } = 1000000; // Maximum dollars per position

        // Risk management
        public bool UseStopLoss { get; set; } = true;
        public double StopLossATRMultiple { get; set; } = 2.0;
        public bool UseTakeProfit { get; set; } = false;
        public double TakeProfitATRMultiple { get; set; } = 3.0;
        public bool UseTrailingStop { get; set; } = false;
        public double TrailingStopATRMultiple { get; set; } = 1.5;

        // Performance filters
        public int MinimumTrades { get; set; } = 5;
        public double MinimumWinRate { get; set; } = 0.35;
        public double MinimumProfitFactor { get; set; } = 1.1;
        public double MaximumDrawdown { get; set; } = 0.25;
    }

    /// <summary>
    /// Represents a single trade
    /// </summary>
    public class Trade
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Symbol { get; set; } = string.Empty;

        // Entry
        public DateTime EntryDate { get; set; }
        public double EntryPrice { get; set; }
        public double Shares { get; set; }
        public double EntryValue { get; set; }
        public double EntryCommission { get; set; }
        public string EntryReason { get; set; } = string.Empty;

        // Exit
        public DateTime? ExitDate { get; set; }
        public double? ExitPrice { get; set; }
        public double? ExitValue { get; set; }
        public double? ExitCommission { get; set; }
        public string? ExitReason { get; set; }

        // Risk management
        public double? StopLossPrice { get; set; }
        public double? TakeProfitPrice { get; set; }
        public double? TrailingStopPrice { get; set; }

        // Performance
        public double? GrossProfit { get; set; }
        public double? NetProfit { get; set; }
        public double? ReturnPercent { get; set; }
        public TimeSpan? HoldingPeriod { get; set; }
        public double? MAE { get; set; } // Maximum Adverse Excursion
        public double? MFE { get; set; } // Maximum Favorable Excursion

        // Status
        public bool IsOpen => !ExitDate.HasValue;
        public bool IsClosed => ExitDate.HasValue;
        public bool IsWinner => NetProfit.HasValue && NetProfit.Value > 0;
        public bool IsLoser => NetProfit.HasValue && NetProfit.Value < 0;

        /// <summary>
        /// Close the trade and calculate performance
        /// </summary>
        public void Close(DateTime exitDate, double exitPrice, double exitCommission, string exitReason)
        {
            ExitDate = exitDate;
            ExitPrice = exitPrice;
            ExitValue = exitPrice * Shares;
            ExitCommission = exitCommission;
            ExitReason = exitReason;

            GrossProfit = ExitValue - EntryValue;
            NetProfit = GrossProfit - EntryCommission - ExitCommission;
            ReturnPercent = (NetProfit / EntryValue) * 100;
            HoldingPeriod = exitDate - EntryDate;
        }

        /// <summary>
        /// Update MAE and MFE based on current price
        /// </summary>
        public void UpdateExcursions(double currentPrice)
        {
            double currentValue = currentPrice * Shares;
            double currentProfit = currentValue - EntryValue;

            // Update MAE (most we were down)
            if (!MAE.HasValue || currentProfit < MAE.Value)
            {
                MAE = currentProfit;
            }

            // Update MFE (most we were up)
            if (!MFE.HasValue || currentProfit > MFE.Value)
            {
                MFE = currentProfit;
            }
        }
    }

    /// <summary>
    /// Represents an open position
    /// </summary>
    public class Position
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Symbol { get; set; } = string.Empty;
        public Trade Trade { get; set; } = new Trade();
        public double CurrentPrice { get; set; }
        public double CurrentValue => CurrentPrice * Trade.Shares;
        public double UnrealizedPL => CurrentValue - Trade.EntryValue;
        public double UnrealizedPLPercent => (UnrealizedPL / Trade.EntryValue) * 100;

        // Risk management levels
        public double? CurrentStopLoss { get; set; }
        public double? CurrentTakeProfit { get; set; }
        public double? CurrentTrailingStop { get; set; }

        /// <summary>
        /// Update position with current market price
        /// </summary>
        public void UpdatePrice(double price)
        {
            CurrentPrice = price;
            Trade.UpdateExcursions(price);
        }

        /// <summary>
        /// Check if stop loss is hit
        /// </summary>
        public bool IsStopLossHit()
        {
            if (!CurrentStopLoss.HasValue)
                return false;

            return CurrentPrice <= CurrentStopLoss.Value;
        }

        /// <summary>
        /// Check if take profit is hit
        /// </summary>
        public bool IsTakeProfitHit()
        {
            if (!CurrentTakeProfit.HasValue)
                return false;

            return CurrentPrice >= CurrentTakeProfit.Value;
        }

        /// <summary>
        /// Check if trailing stop is hit
        /// </summary>
        public bool IsTrailingStopHit()
        {
            if (!CurrentTrailingStop.HasValue)
                return false;

            return CurrentPrice <= CurrentTrailingStop.Value;
        }
    }

    /// <summary>
    /// Complete results of a backtest
    /// </summary>
    public class BacktestResults
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Symbol { get; set; } = string.Empty;
        public string StrategyName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public TimeSpan Duration => EndDate - StartDate;

        // Capital
        public double InitialCapital { get; set; }
        public double FinalCapital { get; set; }
        public double PeakCapital { get; set; }

        // Trades
        public List<Trade> Trades { get; set; } = new List<Trade>();
        public int TotalTrades => Trades.Count;
        public int WinningTrades => Trades.FindAll(t => t.IsWinner).Count;
        public int LosingTrades => Trades.FindAll(t => t.IsLoser).Count;

        // Performance metrics (calculated)
        public PerformanceMetrics Metrics { get; set; } = new PerformanceMetrics();

        // Equity curve
        public List<EquityPoint> EquityCurve { get; set; } = new List<EquityPoint>();

        // Drawdown curve
        public List<DrawdownPoint> DrawdownCurve { get; set; } = new List<DrawdownPoint>();

        // Monthly returns
        public Dictionary<string, double> MonthlyReturns { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Comprehensive performance metrics
    /// </summary>
    public class PerformanceMetrics
    {
        // Returns
        public double TotalReturn { get; set; }
        public double TotalReturnPercent { get; set; }
        public double CAGR { get; set; } // Compound Annual Growth Rate
        public double AverageReturn { get; set; }
        public double MedianReturn { get; set; }

        // Win/Loss
        public double WinRate { get; set; }
        public double LossRate { get; set; }
        public double AverageWin { get; set; }
        public double AverageLoss { get; set; }
        public double LargestWin { get; set; }
        public double LargestLoss { get; set; }

        // Risk metrics
        public double MaxDrawdown { get; set; }
        public double MaxDrawdownPercent { get; set; }
        public DateTime? MaxDrawdownDate { get; set; }
        public double AverageDrawdown { get; set; }

        // Risk-adjusted returns
        public double SharpeRatio { get; set; }
        public double SortinoRatio { get; set; }
        public double CalmarRatio { get; set; }
        public double ProfitFactor { get; set; }

        // Trade statistics
        public double AverageTradeDuration { get; set; } // In days
        public double AverageWinDuration { get; set; }
        public double AverageLossDuration { get; set; }
        public int LongestWinStreak { get; set; }
        public int LongestLossStreak { get; set; }

        // Expectancy
        public double Expectancy { get; set; } // Average $ per trade
        public double ExpectancyPercent { get; set; } // Average % per trade

        // Exposure
        public double TimeInMarket { get; set; } // Percentage of time with open position
        public double AverageExposure { get; set; } // Average capital deployed
    }

    /// <summary>
    /// Point on equity curve
    /// </summary>
    public class EquityPoint
    {
        public DateTime Date { get; set; }
        public double Equity { get; set; }
        public double Cash { get; set; }
        public double PositionValue { get; set; }
        public double TotalValue => Cash + PositionValue;
    }

    /// <summary>
    /// Point on drawdown curve
    /// </summary>
    public class DrawdownPoint
    {
        public DateTime Date { get; set; }
        public double Drawdown { get; set; }
        public double DrawdownPercent { get; set; }
        public bool IsNewPeak { get; set; }
    }

    /// <summary>
    /// Position sizing methods
    /// </summary>
    public enum PositionSizingMethod
    {
        FixedShares,        // Buy fixed number of shares
        FixedDollar,        // Buy fixed dollar amount
        FixedPercentage,    // Use fixed % of capital
        ATRBased,           // Size based on ATR risk
        KellyCreterion      // Optimal bet sizing (advanced)
    }

    /// <summary>
    /// Reason for trade exit
    /// </summary>
    public enum ExitReason
    {
        Signal,             // Strategy exit signal
        StopLoss,           // Stop loss hit
        TakeProfit,         // Take profit hit
        TrailingStop,       // Trailing stop hit
        EndOfBacktest,      // Backtest ended
        TimeLimit           // Maximum holding period
    }
}
