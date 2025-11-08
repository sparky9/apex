using System;
using System.Collections.Generic;
using System.Linq;
using ApexV2.Charts.Models;

namespace ApexV2.Charts.Controls;

/// <summary>
/// Navigation state manager for chart zoom and pan operations
/// </summary>
public class ChartNavigationManager
{
    private readonly List<ChartViewport> _history = new();
    private int _currentIndex = -1;
    private const int MaxHistorySize = 50;

    /// <summary>
    /// Push a new viewport state to the history
    /// </summary>
    public void PushState(ChartViewport viewport)
    {
        // Remove any forward history when adding new state
        if (_currentIndex < _history.Count - 1)
        {
            _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
        }

        _history.Add(viewport.Clone());
        _currentIndex = _history.Count - 1;

        // Limit history size
        if (_history.Count > MaxHistorySize)
        {
            _history.RemoveAt(0);
            _currentIndex--;
        }
    }

    /// <summary>
    /// Go back to the previous viewport state
    /// </summary>
    public ChartViewport? GoBack()
    {
        if (_currentIndex > 0)
        {
            _currentIndex--;
            return _history[_currentIndex].Clone();
        }
        return null;
    }

    /// <summary>
    /// Go forward to the next viewport state
    /// </summary>
    public ChartViewport? GoForward()
    {
        if (_currentIndex < _history.Count - 1)
        {
            _currentIndex++;
            return _history[_currentIndex].Clone();
        }
        return null;
    }

    /// <summary>
    /// Check if can go back
    /// </summary>
    public bool CanGoBack() => _currentIndex > 0;

    /// <summary>
    /// Check if can go forward
    /// </summary>
    public bool CanGoForward() => _currentIndex < _history.Count - 1;

    /// <summary>
    /// Get current viewport from history
    /// </summary>
    public ChartViewport? GetCurrent()
    {
        if (_currentIndex >= 0 && _currentIndex < _history.Count)
            return _history[_currentIndex].Clone();
        return null;
    }

    /// <summary>
    /// Clear navigation history
    /// </summary>
    public void Clear()
    {
        _history.Clear();
        _currentIndex = -1;
    }

    /// <summary>
    /// Apply a predefined zoom preset
    /// </summary>
    public void ApplyZoomPreset(string preset)
    {
        // TODO [REVIEWED]: Implement zoom presets
        // Examples: "1D", "1W", "1M", "3M", "1Y", "ALL"
    }

    /// <summary>
    /// Set timeframe for chart navigation
    /// </summary>
    public void SetTimeframe(string timeframe)
    {
        // TODO [REVIEWED]: Implement timeframe setting
    }
}

/// <summary>
/// Predefined zoom levels for chart navigation
/// </summary>
public static class ZoomPresets
{
    public static readonly Dictionary<string, TimeSpan> TimeFrameZooms = new()
    {
        { "1 Hour", TimeSpan.FromHours(1) },
        { "4 Hours", TimeSpan.FromHours(4) },
        { "1 Day", TimeSpan.FromDays(1) },
        { "3 Days", TimeSpan.FromDays(3) },
        { "1 Week", TimeSpan.FromDays(7) },
        { "2 Weeks", TimeSpan.FromDays(14) },
        { "1 Month", TimeSpan.FromDays(30) },
        { "3 Months", TimeSpan.FromDays(90) },
        { "6 Months", TimeSpan.FromDays(180) },
        { "1 Year", TimeSpan.FromDays(365) },
        { "All", TimeSpan.MaxValue }
    };

    public static readonly double[] ZoomFactors = { 0.1, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0, 4.0, 8.0, 16.0 };
}

/// <summary>
/// Chart interaction modes
/// </summary>
public enum ChartInteractionMode
{
    Pan,        // Drag to pan
    ZoomBox,    // Drag to zoom to selection
    Crosshair,  // Just crosshair, no interaction
    DrawLine,   // Draw trend lines
    DrawRect    // Draw rectangles
}
