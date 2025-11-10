using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ApexV2.Backtesting.Engine;
using ApexV2.Backtesting.Models;
using ApexV2.Backtesting.StrategyGeneration;
using ApexV2.Backtesting.Database;

namespace ApexV2.Backtesting.UI
{
    public partial class BacktestWindow : Window
    {
        private Strategy _currentStrategy;
        private BacktestResults _latestResults;
        private PerformanceMetrics _latestMetrics;
        private Stopwatch _backtestTimer;
        private BacktestingDataService _dataService;
        private int? _savedStrategyId;

        public BacktestWindow()
        {
            InitializeComponent();
            _dataService = new BacktestingDataService();
            InitializeDefaults();
        }

        private void InitializeDefaults()
        {
            // Set default dates
            EndDatePicker.SelectedDate = DateTime.Today;
            StartDatePicker.SelectedDate = DateTime.Today.AddYears(-1);

            // Hide progress bar initially
            ProgressBar.Visibility = Visibility.Collapsed;

            // Set empty state
            TradesEmptyState.Visibility = Visibility.Visible;
            TradesItemsControl.Visibility = Visibility.Collapsed;

            _backtestTimer = new Stopwatch();
        }

        #region Button Event Handlers

        private void RunBacktestButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStrategy == null)
            {
                MessageBox.Show("Please select or build a strategy first.", "No Strategy",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                RunBacktest();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error running backtest: {ex.Message}", "Backtest Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateStatus("Error: " + ex.Message, false);
            }
        }

        private void GenerateStrategiesButton_Click(object sender, RoutedEventArgs e)
        {
            // Open strategy generation window
            var generationWindow = new StrategyGenerationWindow();
            if (generationWindow.ShowDialog() == true)
            {
                _currentStrategy = generationWindow.SelectedStrategy;
                UpdateStrategyDisplay();
            }
        }

        private void LoadStrategyButton_Click(object sender, RoutedEventArgs e)
        {
            // Open strategy library window
            var libraryWindow = new StrategyLibraryWindow();
            if (libraryWindow.ShowDialog() == true)
            {
                _currentStrategy = libraryWindow.SelectedStrategy;
                UpdateStrategyDisplay();
            }
        }

        private void BuildStrategyButton_Click(object sender, RoutedEventArgs e)
        {
            // Open strategy builder window
            var builderWindow = new StrategyBuilderWindow();
            if (builderWindow.ShowDialog() == true)
            {
                _currentStrategy = builderWindow.BuiltStrategy;
                UpdateStrategyDisplay();
            }
        }

        private void SaveStrategyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStrategy == null)
            {
                MessageBox.Show("No strategy to save. Please select or build a strategy first.", "No Strategy",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Simple input dialog for description
                var description = PromptForInput("Save Strategy", "Enter a description for this strategy (optional):");

                // Save strategy to database
                var entity = _dataService.SaveStrategy(_currentStrategy, description);
                _savedStrategyId = entity.Id;

                MessageBox.Show($"Strategy '{_currentStrategy.Name}' saved successfully!", "Strategy Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // If we have backtest results, offer to save them too
                if (_latestResults != null && _latestMetrics != null)
                {
                    var result = MessageBox.Show("Would you like to save the backtest results as well?",
                        "Save Results", MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        SaveBacktestResults();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving strategy: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveBacktestResults()
        {
            if (_savedStrategyId == null)
            {
                MessageBox.Show("Please save the strategy first before saving results.", "Strategy Not Saved",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_latestResults == null || _latestMetrics == null)
            {
                MessageBox.Show("No backtest results to save. Run a backtest first.", "No Results",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var parameters = new BacktestParameters
                {
                    InitialCapital = double.Parse(InitialCapitalTextBox.Text),
                    PositionSize = double.Parse(PositionSizeTextBox.Text),
                    Commission = double.Parse(CommissionTextBox.Text) / 100.0,
                    Slippage = double.Parse(SlippageTextBox.Text) / 100.0
                };

                var notes = PromptForInput("Save Results", "Enter notes for these results (optional):");

                _dataService.SaveBacktestResults(
                    _savedStrategyId.Value,
                    SymbolTextBox.Text,
                    StartDatePicker.SelectedDate.Value,
                    EndDatePicker.SelectedDate.Value,
                    parameters,
                    _latestResults,
                    _latestMetrics,
                    notes);

                MessageBox.Show("Backtest results saved successfully!", "Results Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving results: {ex.Message}", "Save Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string PromptForInput(string title, string message)
        {
            // Simple input dialog
            var dialog = new Window
            {
                Title = title,
                Width = 450,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E")),
                WindowStyle = WindowStyle.ToolWindow
            };

            var grid = new Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(15) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var label = new TextBlock
            {
                Text = message,
                Foreground = Brushes.White,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13
            };
            Grid.SetRow(label, 0);
            grid.Children.Add(label);

            var textBox = new TextBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3C3C3C")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555")),
                Padding = new Thickness(8, 6),
                FontSize = 12
            };
            Grid.SetRow(textBox, 2);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var okButton = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 32,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#007ACC")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Margin = new Thickness(0, 0, 10, 0)
            };
            okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
            buttonPanel.Children.Add(okButton);

            var cancelButton = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 32,
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3C3C3C")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            cancelButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 4);
            grid.Children.Add(buttonPanel);

            dialog.Content = grid;

            if (dialog.ShowDialog() == true)
            {
                return textBox.Text;
            }

            return string.Empty;
        }

        #endregion

        #region Backtest Execution

        private void RunBacktest()
        {
            UpdateStatus("Preparing backtest...", true);
            _backtestTimer.Restart();

            // Validate inputs
            if (!ValidateInputs(out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                UpdateStatus("Ready", false);
                return;
            }

            // Parse parameters
            var parameters = new BacktestParameters
            {
                InitialCapital = double.Parse(InitialCapitalTextBox.Text),
                PositionSize = double.Parse(PositionSizeTextBox.Text),
                Commission = double.Parse(CommissionTextBox.Text) / 100.0,
                Slippage = double.Parse(SlippageTextBox.Text) / 100.0,
                UseStopLoss = UseStopLossCheckBox.IsChecked ?? false,
                UseTakeProfit = UseTakeProfitCheckBox.IsChecked ?? false
            };

            UpdateStatus("Loading market data...", true);

            // TODO: Load actual market data from data provider
            // For now, generate sample data
            var marketData = GenerateSampleData();

            UpdateStatus("Calculating indicators...", true);

            // Calculate indicators
            var indicatorCalculator = new IndicatorCalculator(marketData);
            var indicators = indicatorCalculator.CalculateStrategyIndicators(_currentStrategy);

            UpdateStatus("Running backtest...", true);

            // Create backtest engine
            var backtestEngine = new BacktestEngine(parameters);

            // Generate signals from strategy
            var evaluator = new StrategyEvaluator(backtestEngine, new EvaluationConfig());
            var evaluationResult = evaluator.EvaluateStrategy(_currentStrategy, marketData, indicators);

            // Store results
            _latestResults = evaluationResult.BacktestResults;
            _latestMetrics = evaluationResult.PerformanceMetrics;

            _backtestTimer.Stop();

            UpdateStatus($"Backtest complete in {_backtestTimer.ElapsedMilliseconds}ms", false);
            TimeElapsedText.Text = $"Elapsed: {_backtestTimer.Elapsed.TotalSeconds:F2}s";

            // Display results
            DisplayResults();
        }

        private bool ValidateInputs(out string errorMessage)
        {
            errorMessage = string.Empty;

            // Validate symbol
            if (string.IsNullOrWhiteSpace(SymbolTextBox.Text))
            {
                errorMessage = "Please enter a symbol.";
                return false;
            }

            // Validate dates
            if (!StartDatePicker.SelectedDate.HasValue || !EndDatePicker.SelectedDate.HasValue)
            {
                errorMessage = "Please select start and end dates.";
                return false;
            }

            if (StartDatePicker.SelectedDate >= EndDatePicker.SelectedDate)
            {
                errorMessage = "Start date must be before end date.";
                return false;
            }

            // Validate numeric inputs
            if (!double.TryParse(InitialCapitalTextBox.Text, out double initialCapital) || initialCapital <= 0)
            {
                errorMessage = "Initial capital must be a positive number.";
                return false;
            }

            if (!double.TryParse(PositionSizeTextBox.Text, out double positionSize) || positionSize <= 0)
            {
                errorMessage = "Position size must be a positive number.";
                return false;
            }

            if (!double.TryParse(CommissionTextBox.Text, out double commission) || commission < 0)
            {
                errorMessage = "Commission must be a non-negative number.";
                return false;
            }

            if (!double.TryParse(SlippageTextBox.Text, out double slippage) || slippage < 0)
            {
                errorMessage = "Slippage must be a non-negative number.";
                return false;
            }

            return true;
        }

        private MarketData GenerateSampleData()
        {
            // Generate sample data for testing
            // TODO: Replace with actual data loading from database or API
            var random = new Random(42);
            int bars = 252; // One year of daily data

            var timestamps = new DateTime[bars];
            var open = new double[bars];
            var high = new double[bars];
            var low = new double[bars];
            var close = new double[bars];
            var volume = new double[bars];

            double price = 100.0;
            var startDate = StartDatePicker.SelectedDate.Value;

            for (int i = 0; i < bars; i++)
            {
                timestamps[i] = startDate.AddDays(i);

                // Generate price with trend and noise
                double trend = 0.0005;
                double volatility = 0.02;
                double change = (random.NextDouble() - 0.5) * volatility + trend;

                price *= (1 + change);

                open[i] = price;
                double dayRange = price * 0.015 * random.NextDouble();
                high[i] = price + dayRange * random.NextDouble();
                low[i] = price - dayRange * random.NextDouble();
                close[i] = low[i] + (high[i] - low[i]) * random.NextDouble();

                price = close[i];
                volume[i] = 1000000 + random.Next(500000);
            }

            return new MarketData
            {
                Symbol = SymbolTextBox.Text,
                Timestamps = timestamps,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume
            };
        }

        #endregion

        #region Results Display

        private void DisplayResults()
        {
            if (_latestResults == null || _latestMetrics == null)
                return;

            // Display performance summary cards
            TotalReturnText.Text = _latestMetrics.TotalReturn.ToString("P2");
            TotalReturnText.Foreground = _latestMetrics.TotalReturn >= 0 ?
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0")) :
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F14C4C"));

            TotalReturnDollarText.Text = $"${_latestMetrics.TotalReturn * double.Parse(InitialCapitalTextBox.Text):N2}";

            SharpeRatioText.Text = _latestMetrics.SharpeRatio.ToString("F2");
            SharpeRatioText.Foreground = _latestMetrics.SharpeRatio >= 1.0 ?
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0")) :
                new SolidColorBrush(Colors.White);

            WinRateText.Text = _latestMetrics.WinRate.ToString("P1");
            WinLossText.Text = $"{_latestMetrics.WinningTrades} Wins / {_latestMetrics.LosingTrades} Losses";

            MaxDrawdownText.Text = _latestMetrics.MaxDrawdown.ToString("P2");

            // Display detailed metrics
            TotalTradesText.Text = _latestMetrics.TotalTrades.ToString();
            ProfitFactorText.Text = _latestMetrics.ProfitFactor.ToString("F2");
            SortinoRatioText.Text = _latestMetrics.SortinoRatio.ToString("F2");
            CalmarRatioText.Text = _latestMetrics.CalmarRatio.ToString("F2");
            AverageWinText.Text = $"${_latestMetrics.AverageWin:N2}";
            AnnualizedReturnText.Text = _latestMetrics.AnnualizedReturn.ToString("P2");
            AvgDaysInTradeText.Text = _latestMetrics.AverageHoldingPeriod.ToString("F1");
            MaxConsecutiveWinsText.Text = _latestMetrics.MaxConsecutiveWins.ToString();
            MaxConsecutiveLossesText.Text = _latestMetrics.MaxConsecutiveLosses.ToString();
            AverageLossText.Text = $"${_latestMetrics.AverageLoss:N2}";

            // Display trades
            DisplayTrades();

            // Create and display charts
            DisplayCharts();
        }

        private void DisplayTrades()
        {
            if (_latestResults.Trades.Count == 0)
            {
                TradesEmptyState.Visibility = Visibility.Visible;
                TradesItemsControl.Visibility = Visibility.Collapsed;
                return;
            }

            TradesEmptyState.Visibility = Visibility.Collapsed;
            TradesItemsControl.Visibility = Visibility.Visible;

            var tradeViewModels = _latestResults.Trades.Select((trade, index) => new TradeViewModel
            {
                TradeNumber = index + 1,
                EntryDate = trade.EntryDate,
                ExitDate = trade.ExitDate,
                EntryPrice = trade.EntryPrice,
                ExitPrice = trade.ExitPrice,
                Shares = trade.Shares,
                ProfitLoss = trade.ProfitLoss,
                ReturnPct = trade.ReturnPct,
                IsProfit = trade.ProfitLoss > 0,
                ExitReason = GetExitReasonText(trade.ExitReason)
            }).ToList();

            TradesItemsControl.ItemsSource = tradeViewModels;
        }

        private string GetExitReasonText(ExitReason exitReason)
        {
            return exitReason switch
            {
                ExitReason.Signal => "Exit Signal",
                ExitReason.StopLoss => "Stop Loss",
                ExitReason.TakeProfit => "Take Profit",
                ExitReason.EndOfData => "End of Data",
                _ => "Unknown"
            };
        }

        private void DisplayCharts()
        {
            if (_latestResults == null)
                return;

            try
            {
                // Equity Curve Chart
                EquityCurvePlaceholder.Visibility = Visibility.Collapsed;
                var equityCurveChart = ChartHelper.CreateEquityCurveChart(
                    _latestResults,
                    double.Parse(InitialCapitalTextBox.Text));
                EquityCurveContainer.Child = equityCurveChart;

                // Drawdown Chart
                DrawdownPlaceholder.Visibility = Visibility.Collapsed;
                var drawdownChart = ChartHelper.CreateDrawdownChart(_latestResults);
                DrawdownChartContainer.Child = drawdownChart;

                // Returns Distribution Histogram
                ReturnsPlaceholder.Visibility = Visibility.Collapsed;
                var returnsChart = ChartHelper.CreateReturnsHistogram(_latestResults.Trades);
                ReturnsChartContainer.Child = returnsChart;

                // Trade Scatter Plot
                ScatterPlaceholder.Visibility = Visibility.Collapsed;
                var scatterChart = ChartHelper.CreateTradeScatterPlot(_latestResults.Trades);
                ScatterChartContainer.Child = scatterChart;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating charts: {ex.Message}");
                // If chart creation fails, show placeholders again
                EquityCurvePlaceholder.Visibility = Visibility.Visible;
                DrawdownPlaceholder.Visibility = Visibility.Visible;
                ReturnsPlaceholder.Visibility = Visibility.Visible;
                ScatterPlaceholder.Visibility = Visibility.Visible;
            }
        }

        #endregion

        #region Strategy Display

        private void UpdateStrategyDisplay()
        {
            if (_currentStrategy == null)
            {
                StrategyNameText.Text = "No strategy selected";
                EntryRulesText.Text = "None";
                ExitRulesText.Text = "None";
                return;
            }

            StrategyNameText.Text = _currentStrategy.Name;

            // Format entry rules
            if (_currentStrategy.EntryRules != null && _currentStrategy.EntryRules.Count > 0)
            {
                EntryRulesText.Text = string.Join("\n", _currentStrategy.EntryRules.Select(r =>
                    $"• {r.IndicatorName} {r.Condition}"));
            }
            else
            {
                EntryRulesText.Text = "None";
            }

            // Format exit rules
            if (_currentStrategy.ExitRules != null && _currentStrategy.ExitRules.Count > 0)
            {
                ExitRulesText.Text = string.Join("\n", _currentStrategy.ExitRules.Select(r =>
                    $"• {r.IndicatorName} {r.Condition}"));
            }
            else
            {
                ExitRulesText.Text = "None";
            }
        }

        #endregion

        #region Status Updates

        private void UpdateStatus(string message, bool showProgress)
        {
            StatusText.Text = message;
            ProgressBar.Visibility = showProgress ? Visibility.Visible : Visibility.Collapsed;

            if (!showProgress)
            {
                ProgressBar.IsIndeterminate = false;
            }
            else
            {
                ProgressBar.IsIndeterminate = true;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Load a strategy from external source
        /// </summary>
        public void LoadStrategy(Strategy strategy)
        {
            _currentStrategy = strategy;
            UpdateStrategyDisplay();
        }

        /// <summary>
        /// Set market data parameters
        /// </summary>
        public void SetMarketDataParameters(string symbol, DateTime startDate, DateTime endDate)
        {
            SymbolTextBox.Text = symbol;
            StartDatePicker.SelectedDate = startDate;
            EndDatePicker.SelectedDate = endDate;
        }

        #endregion
    }

    #region View Models

    public class TradeViewModel
    {
        public int TradeNumber { get; set; }
        public DateTime EntryDate { get; set; }
        public DateTime ExitDate { get; set; }
        public double EntryPrice { get; set; }
        public double ExitPrice { get; set; }
        public double Shares { get; set; }
        public double ProfitLoss { get; set; }
        public double ReturnPct { get; set; }
        public bool IsProfit { get; set; }
        public string ExitReason { get; set; }
    }

    #endregion
}
