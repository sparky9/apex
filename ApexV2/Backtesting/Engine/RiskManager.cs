using System;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.Engine
{
    /// <summary>
    /// Manages risk and position sizing rules
    /// </summary>
    public class RiskManager
    {
        private readonly BacktestParameters _parameters;

        public RiskManager(BacktestParameters parameters)
        {
            _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        }

        /// <summary>
        /// Check if a trade meets risk management criteria
        /// </summary>
        public bool IsTradeAcceptable(double entryPrice, double stopLossPrice, double positionSize, double accountEquity)
        {
            if (stopLossPrice <= 0 || entryPrice <= 0)
                return false;

            // Calculate risk per share
            double riskPerShare = Math.Abs(entryPrice - stopLossPrice);

            // Calculate total position risk
            double totalRisk = riskPerShare * positionSize;

            // Calculate risk as percentage of equity
            double riskPercent = (totalRisk / accountEquity) * 100;

            // Check if risk is within acceptable limits (e.g., 2% per trade)
            return riskPercent <= 2.0;
        }

        /// <summary>
        /// Calculate maximum position size based on risk parameters
        /// </summary>
        public double CalculateMaxPositionSize(double accountEquity, double entryPrice, double stopLossPrice)
        {
            if (stopLossPrice <= 0 || entryPrice <= 0 || accountEquity <= 0)
                return 0;

            // Maximum risk per trade (2% of equity)
            double maxRiskAmount = accountEquity * 0.02;

            // Risk per share
            double riskPerShare = Math.Abs(entryPrice - stopLossPrice);

            if (riskPerShare == 0)
                return 0;

            // Calculate max shares
            double maxShares = maxRiskAmount / riskPerShare;

            return Math.Floor(maxShares);
        }

        /// <summary>
        /// Validate that position sizing doesn't exceed portfolio limits
        /// </summary>
        public bool ValidatePositionSize(double positionValue, double accountEquity)
        {
            // Position shouldn't exceed 95% of equity
            double positionPercent = (positionValue / accountEquity) * 100;
            return positionPercent <= 95;
        }

        /// <summary>
        /// Check if stop loss is at reasonable distance
        /// </summary>
        public bool IsStopLossReasonable(double entryPrice, double stopLossPrice)
        {
            double stopLossPercent = Math.Abs((stopLossPrice - entryPrice) / entryPrice) * 100;

            // Stop loss should be between 0.5% and 10%
            return stopLossPercent >= 0.5 && stopLossPercent <= 10.0;
        }

        /// <summary>
        /// Check if risk-reward ratio is acceptable
        /// </summary>
        public bool IsRiskRewardAcceptable(double entryPrice, double stopLossPrice, double targetPrice, double minRatio = 2.0)
        {
            double risk = Math.Abs(entryPrice - stopLossPrice);
            double reward = Math.Abs(targetPrice - entryPrice);

            if (risk == 0)
                return false;

            double ratio = reward / risk;
            return ratio >= minRatio;
        }
    }
}
