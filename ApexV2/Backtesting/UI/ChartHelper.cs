using System;
using System.Collections.Generic;
using System.Linq;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.UI
{
    /// <summary>
    /// Helper class for creating charts for backtesting results
    /// </summary>
    public static class ChartHelper
    {
        /// <summary>
        /// Create equity curve chart
        /// </summary>
        public static CartesianChart CreateEquityCurveChart(BacktestResults results, double initialCapital)
        {
            var chart = new CartesianChart
            {
                Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = results.EquityCurve,
                        Name = "Equity",
                        Fill = null,
                        Stroke = new SolidColorPaint(SKColors.LightGreen) { StrokeThickness = 2 },
                        GeometrySize = 0
                    }
                },
                XAxes = new[]
                {
                    new Axis
                    {
                        Name = "Time",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f }
                    }
                },
                YAxes = new[]
                {
                    new Axis
                    {
                        Name = "Equity ($)",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f },
                        Labeler = value => $"${value:N0}"
                    }
                },
                TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                TooltipBackgroundPaint = new SolidColorPaint(SKColors.DarkSlateGray),
                TooltipTextPaint = new SolidColorPaint(SKColors.White)
            };

            return chart;
        }

        /// <summary>
        /// Create drawdown chart
        /// </summary>
        public static CartesianChart CreateDrawdownChart(BacktestResults results)
        {
            var chart = new CartesianChart
            {
                Series = new ISeries[]
                {
                    new AreaSeries<double>
                    {
                        Values = results.DrawdownCurve,
                        Name = "Drawdown",
                        Fill = new SolidColorPaint(SKColors.Red.WithAlpha(50)),
                        Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                        GeometrySize = 0
                    }
                },
                XAxes = new[]
                {
                    new Axis
                    {
                        Name = "Time",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f }
                    }
                },
                YAxes = new[]
                {
                    new Axis
                    {
                        Name = "Drawdown (%)",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f },
                        Labeler = value => $"{value:P1}"
                    }
                },
                TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                TooltipBackgroundPaint = new SolidColorPaint(SKColors.DarkSlateGray),
                TooltipTextPaint = new SolidColorPaint(SKColors.White)
            };

            return chart;
        }

        /// <summary>
        /// Create returns distribution histogram
        /// </summary>
        public static CartesianChart CreateReturnsHistogram(List<Trade> trades)
        {
            // Calculate bins for histogram
            var returns = trades.Select(t => t.ReturnPct).ToList();
            if (returns.Count == 0)
            {
                return CreateEmptyChart("No trades to display");
            }

            var min = returns.Min();
            var max = returns.Max();
            var binCount = Math.Min(20, trades.Count / 5); // Dynamic bin count
            var binWidth = (max - min) / binCount;

            var histogram = new int[binCount];
            foreach (var ret in returns)
            {
                int binIndex = (int)((ret - min) / binWidth);
                if (binIndex >= binCount) binIndex = binCount - 1;
                if (binIndex < 0) binIndex = 0;
                histogram[binIndex]++;
            }

            var chart = new CartesianChart
            {
                Series = new ISeries[]
                {
                    new ColumnSeries<int>
                    {
                        Values = histogram,
                        Name = "Trade Count",
                        Fill = new SolidColorPaint(SKColors.CornflowerBlue),
                        Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 1 },
                        MaxBarWidth = 30
                    }
                },
                XAxes = new[]
                {
                    new Axis
                    {
                        Name = "Return %",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f }
                    }
                },
                YAxes = new[]
                {
                    new Axis
                    {
                        Name = "Frequency",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f }
                    }
                },
                TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                TooltipBackgroundPaint = new SolidColorPaint(SKColors.DarkSlateGray),
                TooltipTextPaint = new SolidColorPaint(SKColors.White)
            };

            return chart;
        }

        /// <summary>
        /// Create trade scatter plot (return vs duration)
        /// </summary>
        public static CartesianChart CreateTradeScatterPlot(List<Trade> trades)
        {
            if (trades.Count == 0)
            {
                return CreateEmptyChart("No trades to display");
            }

            var scatterData = trades.Select(t => new
            {
                Duration = (t.ExitDate - t.EntryDate).TotalDays,
                Return = t.ReturnPct * 100
            }).ToList();

            var winningTrades = scatterData.Where(t => t.Return > 0)
                .Select(t => new ObservablePoint(t.Duration, t.Return)).ToList();

            var losingTrades = scatterData.Where(t => t.Return <= 0)
                .Select(t => new ObservablePoint(t.Duration, t.Return)).ToList();

            var series = new List<ISeries>();

            if (winningTrades.Any())
            {
                series.Add(new ScatterSeries<ObservablePoint>
                {
                    Values = winningTrades,
                    Name = "Winning Trades",
                    Fill = new SolidColorPaint(SKColors.LightGreen),
                    Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 1 },
                    GeometrySize = 8
                });
            }

            if (losingTrades.Any())
            {
                series.Add(new ScatterSeries<ObservablePoint>
                {
                    Values = losingTrades,
                    Name = "Losing Trades",
                    Fill = new SolidColorPaint(SKColors.LightCoral),
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 1 },
                    GeometrySize = 8
                });
            }

            var chart = new CartesianChart
            {
                Series = series,
                XAxes = new[]
                {
                    new Axis
                    {
                        Name = "Duration (days)",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f }
                    }
                },
                YAxes = new[]
                {
                    new Axis
                    {
                        Name = "Return %",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f },
                        Labeler = value => $"{value:F1}%"
                    }
                },
                TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                TooltipBackgroundPaint = new SolidColorPaint(SKColors.DarkSlateGray),
                TooltipTextPaint = new SolidColorPaint(SKColors.White)
            };

            return chart;
        }

        /// <summary>
        /// Create equity vs buy-and-hold comparison chart
        /// </summary>
        public static CartesianChart CreateComparisonChart(BacktestResults strategyResults, double[] buyAndHoldEquity)
        {
            var chart = new CartesianChart
            {
                Series = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = strategyResults.EquityCurve,
                        Name = "Strategy",
                        Fill = null,
                        Stroke = new SolidColorPaint(SKColors.LightGreen) { StrokeThickness = 2 },
                        GeometrySize = 0
                    },
                    new LineSeries<double>
                    {
                        Values = buyAndHoldEquity,
                        Name = "Buy & Hold",
                        Fill = null,
                        Stroke = new SolidColorPaint(SKColors.LightBlue) { StrokeThickness = 2 },
                        GeometrySize = 0
                    }
                },
                XAxes = new[]
                {
                    new Axis
                    {
                        Name = "Time",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f }
                    }
                },
                YAxes = new[]
                {
                    new Axis
                    {
                        Name = "Equity ($)",
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
                        SeparatorsPaint = new SolidColorPaint(SKColors.Gray) { StrokeThickness = 0.5f },
                        Labeler = value => $"${value:N0}"
                    }
                },
                TooltipPosition = LiveChartsCore.Measure.TooltipPosition.Top,
                TooltipBackgroundPaint = new SolidColorPaint(SKColors.DarkSlateGray),
                TooltipTextPaint = new SolidColorPaint(SKColors.White)
            };

            return chart;
        }

        /// <summary>
        /// Create empty chart with message
        /// </summary>
        private static CartesianChart CreateEmptyChart(string message)
        {
            return new CartesianChart
            {
                Series = Array.Empty<ISeries>(),
                XAxes = new[]
                {
                    new Axis
                    {
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray)
                    }
                },
                YAxes = new[]
                {
                    new Axis
                    {
                        NamePaint = new SolidColorPaint(SKColors.White),
                        LabelsPaint = new SolidColorPaint(SKColors.LightGray)
                    }
                }
            };
        }
    }

    /// <summary>
    /// Observable point for scatter plots
    /// </summary>
    public class ObservablePoint
    {
        public double X { get; set; }
        public double Y { get; set; }

        public ObservablePoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
