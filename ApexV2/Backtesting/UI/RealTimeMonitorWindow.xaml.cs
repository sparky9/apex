using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ApexV2.Backtesting.StrategyGeneration;
using ApexV2.Backtesting.RealTime;

namespace ApexV2.Backtesting.UI
{
    public partial class RealTimeMonitorWindow : Window
    {
        private RealTimeStrategyMonitor _monitor;
        private MockDataFeed _dataFeed;
        private Strategy _strategy;

        public RealTimeMonitorWindow()
        {
            InitializeComponent();
        }

        public void SetStrategy(Strategy strategy)
        {
            _strategy = strategy;
            StrategyNameText.Text = strategy.Name;
            StrategyNameText.Foreground = Brushes.White;
        }

        private async void StartButton_Click(object sender, RoutedEventArgs e)
        {
            if (_strategy == null)
            {
                MessageBox.Show("Please select a strategy first.", "No Strategy",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(SymbolTextBox.Text))
            {
                MessageBox.Show("Please enter a symbol to monitor.", "No Symbol",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!double.TryParse(InitialCapitalTextBox.Text, out double capital) || capital <= 0)
            {
                MessageBox.Show("Please enter a valid initial capital amount.", "Invalid Capital",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Create data feed and monitor
                _dataFeed = new MockDataFeed();
                _monitor = new RealTimeStrategyMonitor(_dataFeed, _strategy, capital);

                // Subscribe to events
                _monitor.OnSignalGenerated += Monitor_OnSignalGenerated;
                _monitor.OnStatusChanged += Monitor_OnStatusChanged;
                _monitor.TradingEngine.OnTradeExecuted += TradingEngine_OnTradeExecuted;
                _monitor.TradingEngine.OnEquityUpdated += TradingEngine_OnEquityUpdated;

                // Start monitoring
                var symbols = SymbolTextBox.Text.Split(',', StringSplitOptions.RemoveEmptyEntries);
                var started = await _monitor.StartMonitoringAsync(
                    Array.ConvertAll(symbols, s => s.Trim()),
                    TimeFrame.Minute_1);

                if (started)
                {
                    StartButton.IsEnabled = false;
                    StopButton.IsEnabled = true;
                    StatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"));
                    StatusText.Text = "Monitoring";

                    AddActivityMessage("Monitoring started", "#4EC9B0");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error starting monitor: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (_monitor != null)
            {
                await _monitor.StopMonitoringAsync();

                StartButton.IsEnabled = true;
                StopButton.IsEnabled = false;
                StatusIndicator.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"));
                StatusText.Text = "Stopped";

                AddActivityMessage("Monitoring stopped", "#F14C4C");
            }
        }

        private void Monitor_OnSignalGenerated(object sender, SignalGeneratedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string color = e.SignalType == SignalType.Buy ? "#4EC9B0" : "#F14C4C";
                string message = $"{e.SignalType} signal for {e.Symbol} at ${e.Price:F2}";

                if (e.OrderResult != null && e.OrderResult.Success)
                {
                    message += $" - {e.OrderResult.Message}";
                }

                AddActivityMessage(message, color);
            });
        }

        private void Monitor_OnStatusChanged(object sender, MonitoringStatusEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                FooterStatusText.Text = e.Message;
            });
        }

        private void TradingEngine_OnTradeExecuted(object sender, TradeExecutedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                string color = e.Side == OrderSide.Buy ? "#4EC9B0" : "#F14C4C";
                string message = $"{e.Side} {e.Shares} {e.Symbol} @ ${e.Price:F2} (Commission: ${e.Commission:F2})";
                AddActivityMessage(message, color);
            });
        }

        private void TradingEngine_OnEquityUpdated(object sender, EquityUpdatedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                UpdatePerformanceDisplay(e);
            });
        }

        private void AddActivityMessage(string message, string hexColor)
        {
            // Clear placeholder text if this is the first message
            if (ActivityPanel.Children.Count == 1 &&
                ActivityPanel.Children[0] is TextBlock tb &&
                tb.Text.StartsWith("No activity"))
            {
                ActivityPanel.Children.Clear();
            }

            var border = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hexColor)),
                BorderThickness = new Thickness(2, 0, 0, 0),
                Padding = new Thickness(12, 10),
                Margin = new Thickness(0, 0, 0, 8),
                CornerRadius = new CornerRadius(3)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var messageText = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(messageText, 0);
            grid.Children.Add(messageText);

            var timeText = new TextBlock
            {
                Text = DateTime.Now.ToString("HH:mm:ss"),
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                FontSize = 10,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(timeText, 1);
            grid.Children.Add(timeText);

            border.Child = grid;

            ActivityPanel.Children.Insert(0, border);

            // Limit to 100 messages
            if (ActivityPanel.Children.Count > 100)
            {
                ActivityPanel.Children.RemoveAt(100);
            }

            // Scroll to top
            ActivityScrollViewer.ScrollToTop();
        }

        private void UpdatePerformanceDisplay(EquityUpdatedEventArgs e)
        {
            var metrics = _monitor.TradingEngine.GetMetrics();

            TotalEquityText.Text = $"${e.Equity:N2}";
            CashText.Text = $"${e.Cash:N2}";

            TotalPnLText.Text = $"${metrics.TotalPnL:N2}";
            TotalPnLText.Foreground = metrics.TotalPnL >= 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F14C4C"));

            TotalReturnText.Text = metrics.TotalReturn.ToString("P2");
            TotalReturnText.Foreground = TotalPnLText.Foreground;

            RealizedPnLText.Text = $"${metrics.RealizedPnL:N2}";
            RealizedPnLText.Foreground = metrics.RealizedPnL >= 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F14C4C"));

            UnrealizedPnLText.Text = $"${metrics.UnrealizedPnL:N2}";
            UnrealizedPnLText.Foreground = metrics.UnrealizedPnL >= 0
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F14C4C"));

            CompletedTradesText.Text = metrics.CompletedTrades.ToString();
            WinRateText.Text = metrics.WinRate.ToString("P1");
            ActivePositionsText.Text = metrics.ActivePositions.ToString();

            UpdatePositionsDisplay();
        }

        private void UpdatePositionsDisplay()
        {
            PositionsPanel.Children.Clear();

            if (_monitor.TradingEngine.Positions.Count == 0)
            {
                PositionsPanel.Children.Add(new TextBlock
                {
                    Text = "No active positions",
                    FontSize = 11,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(0, 10, 0, 0)
                });
                return;
            }

            foreach (var position in _monitor.TradingEngine.Positions.Values)
            {
                var border = new Border
                {
                    Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")),
                    CornerRadius = new CornerRadius(3),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 8)
                };

                var grid = new Grid();
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(5) });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var symbolText = new TextBlock
                {
                    Text = $"{position.Symbol} - {position.Shares} shares",
                    FontSize = 12,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White
                };
                Grid.SetRow(symbolText, 0);
                grid.Children.Add(symbolText);

                var priceGrid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                priceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                priceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var priceLabel = new TextBlock
                {
                    Text = $"Avg: ${position.AveragePrice:F2} | Now: ${position.CurrentPrice:F2}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999"))
                };
                Grid.SetColumn(priceLabel, 0);
                priceGrid.Children.Add(priceLabel);

                Grid.SetRow(priceGrid, 2);
                grid.Children.Add(priceGrid);

                var pnlText = new TextBlock
                {
                    Text = $"P&L: ${position.UnrealizedPnL:F2}",
                    FontSize = 10,
                    Foreground = position.UnrealizedPnL >= 0
                        ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0"))
                        : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F14C4C"))
                };
                Grid.SetRow(pnlText, 3);
                grid.Children.Add(pnlText);

                border.Child = grid;
                PositionsPanel.Children.Add(border);
            }
        }
    }
}
