using System;
using System.Collections.Generic;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.Engine
{
    /// <summary>
    /// Manages open positions during backtesting
    /// </summary>
    public class PositionManager
    {
        private Position? _currentPosition;

        public bool HasOpenPosition => _currentPosition != null;
        public int OpenPositionCount => HasOpenPosition ? 1 : 0;

        /// <summary>
        /// Open a new position
        /// </summary>
        public void OpenPosition(Position position)
        {
            if (HasOpenPosition)
                throw new InvalidOperationException("Cannot open position - position already open");

            _currentPosition = position ?? throw new ArgumentNullException(nameof(position));
        }

        /// <summary>
        /// Get the current open position
        /// </summary>
        public Position? GetOpenPosition()
        {
            return _currentPosition;
        }

        /// <summary>
        /// Close the current position
        /// </summary>
        public Trade? ClosePosition()
        {
            if (!HasOpenPosition)
                return null;

            var trade = _currentPosition!.Trade;
            _currentPosition = null;
            return trade;
        }

        /// <summary>
        /// Get current position value
        /// </summary>
        public double GetCurrentPositionValue()
        {
            return _currentPosition?.CurrentValue ?? 0;
        }

        /// <summary>
        /// Get unrealized P&L
        /// </summary>
        public double GetUnrealizedPL()
        {
            return _currentPosition?.UnrealizedPL ?? 0;
        }

        /// <summary>
        /// Reset manager (clear all positions)
        /// </summary>
        public void Reset()
        {
            _currentPosition = null;
        }
    }
}
