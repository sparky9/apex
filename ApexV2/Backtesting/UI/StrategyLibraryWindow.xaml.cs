using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ApexV2.Backtesting.StrategyGeneration;
using ApexV2.Backtesting.Database;
using ApexV2.Backtesting.Models;

namespace ApexV2.Backtesting.UI
{
    public partial class StrategyLibraryWindow : Window
    {
        public Strategy SelectedStrategy { get; private set; }
        private List<Strategy> _allStrategies = new List<Strategy>();
        private List<Strategy> _filteredStrategies = new List<Strategy>();
        private BacktestingDataService _dataService;

        public StrategyLibraryWindow()
        {
            InitializeComponent();
            _dataService = new BacktestingDataService();
            _dataService.InitializeBuiltInTemplates();
            LoadStrategies();
        }

        private void LoadStrategies()
        {
            try
            {
                // Load strategies from database
                var entities = _dataService.LoadAllStrategies();

                if (entities.Count == 0)
                {
                    // If no saved strategies, show sample/template strategies
                    _allStrategies = CreateSampleStrategies();
                }
                else
                {
                    // Convert entities to Strategy models
                    _allStrategies = entities.Select(e => _dataService.EntityToStrategy(e)).ToList();
                }

                _filteredStrategies = new List<Strategy>(_allStrategies);
                UpdateStrategyDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading strategies: {ex.Message}", "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                // Fallback to sample strategies
                _allStrategies = CreateSampleStrategies();
                _filteredStrategies = new List<Strategy>(_allStrategies);
                UpdateStrategyDisplay();
            }
        }

        private List<Strategy> CreateSampleStrategies()
        {
            // Create sample strategies for demonstration
            var strategies = new List<Strategy>();

            // RSI Mean Reversion
            strategies.Add(new Strategy
            {
                Name = "RSI Mean Reversion (14, 30/70)",
                Description = "Buy when RSI crosses below 30, sell when it crosses above 70",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.MeanReversion,
                        IndicatorName = "RSI",
                        Condition = RuleCondition.Oversold,
                        Parameters = new Dictionary<string, object> { { "window", 14 }, { "oversold", 30.0 } }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.MeanReversion,
                        IndicatorName = "RSI",
                        Condition = RuleCondition.Overbought,
                        Parameters = new Dictionary<string, object> { { "window", 14 }, { "overbought", 70.0 } }
                    }
                },
                GenerationMethod = "Template",
                CreatedAt = DateTime.Now.AddDays(-10)
            });

            // MACD Trend Following
            strategies.Add(new Strategy
            {
                Name = "MACD Trend Following (12, 26, 9)",
                Description = "Buy on MACD bullish crossover, sell on bearish crossover",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.TrendFollowing,
                        IndicatorName = "MACD",
                        Condition = RuleCondition.IndicatorCrossAbove,
                        Parameters = new Dictionary<string, object>
                        {
                            { "fast_period", 12 }, { "slow_period", 26 }, { "signal_period", 9 }
                        }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.TrendFollowing,
                        IndicatorName = "MACD",
                        Condition = RuleCondition.IndicatorCrossBelow,
                        Parameters = new Dictionary<string, object>
                        {
                            { "fast_period", 12 }, { "slow_period", 26 }, { "signal_period", 9 }
                        }
                    }
                },
                GenerationMethod = "Template",
                CreatedAt = DateTime.Now.AddDays(-8)
            });

            // Bollinger Band Mean Reversion
            strategies.Add(new Strategy
            {
                Name = "Bollinger Band Mean Reversion (20, 2.0)",
                Description = "Buy at lower band, sell at upper band",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.MeanReversion,
                        IndicatorName = "BB",
                        Condition = RuleCondition.PriceBelow,
                        Parameters = new Dictionary<string, object> { { "window", 20 }, { "std_dev", 2.0 } }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.MeanReversion,
                        IndicatorName = "BB",
                        Condition = RuleCondition.PriceAbove,
                        Parameters = new Dictionary<string, object> { { "window", 20 }, { "std_dev", 2.0 } }
                    }
                },
                GenerationMethod = "Template",
                CreatedAt = DateTime.Now.AddDays(-5)
            });

            // Moving Average Crossover
            strategies.Add(new Strategy
            {
                Name = "Moving Average Crossover (20/50)",
                Description = "Buy when fast MA crosses above slow MA",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.TrendFollowing,
                        IndicatorName = "EMA",
                        Condition = RuleCondition.PriceCrossAbove,
                        Parameters = new Dictionary<string, object> { { "window", 20 } }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.TrendFollowing,
                        IndicatorName = "EMA",
                        Condition = RuleCondition.PriceCrossBelow,
                        Parameters = new Dictionary<string, object> { { "window", 50 } }
                    }
                },
                GenerationMethod = "Template",
                CreatedAt = DateTime.Now.AddDays(-3)
            });

            // Momentum Strategy
            strategies.Add(new Strategy
            {
                Name = "Stochastic Momentum",
                Description = "Buy oversold stochastic with rising momentum",
                EntryRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.Momentum,
                        IndicatorName = "STOCH",
                        Condition = RuleCondition.Oversold,
                        Parameters = new Dictionary<string, object>
                        {
                            { "k_window", 14 }, { "d_window", 3 }, { "oversold", 20.0 }
                        }
                    }
                },
                ExitRules = new List<StrategyRule>
                {
                    new StrategyRule
                    {
                        Type = RuleType.Momentum,
                        IndicatorName = "STOCH",
                        Condition = RuleCondition.Overbought,
                        Parameters = new Dictionary<string, object>
                        {
                            { "k_window", 14 }, { "d_window", 3 }, { "overbought", 80.0 }
                        }
                    }
                },
                GenerationMethod = "Template",
                CreatedAt = DateTime.Now.AddDays(-1)
            });

            return strategies;
        }

        private string GetStrategyType(Strategy strategy)
        {
            if (strategy.EntryRules == null || strategy.EntryRules.Count == 0)
                return "Unknown";

            // Get the most common rule type from entry rules
            var ruleType = strategy.EntryRules
                .GroupBy(r => r.Type)
                .OrderByDescending(g => g.Count())
                .First().Key;

            return ruleType.ToString();
        }

        private void UpdateStrategyDisplay()
        {
            StrategiesPanel.Children.Clear();

            if (_filteredStrategies.Count == 0)
            {
                EmptyState.Visibility = Visibility.Visible;
                StrategiesPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                EmptyState.Visibility = Visibility.Collapsed;
                StrategiesPanel.Visibility = Visibility.Visible;

                foreach (var strategy in _filteredStrategies)
                {
                    var card = CreateStrategyCard(strategy);
                    StrategiesPanel.Children.Add(card);
                }
            }

            StrategyCountText.Text = $"{_filteredStrategies.Count} Strategies";
            StatusText.Text = $"Showing {_filteredStrategies.Count} of {_allStrategies.Count} strategies";
        }

        private Border CreateStrategyCard(Strategy strategy)
        {
            var card = new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30")),
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 15),
                CornerRadius = new CornerRadius(4),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header row
            var headerGrid = new Grid();
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var nameText = new TextBlock
            {
                Text = strategy.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White
            };
            Grid.SetColumn(nameText, 0);
            headerGrid.Children.Add(nameText);

            var strategyType = GetStrategyType(strategy);
            var typeBadge = new Border
            {
                Background = GetTypeColor(strategyType),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(8, 3),
                VerticalAlignment = VerticalAlignment.Center
            };
            var typeText = new TextBlock
            {
                Text = strategyType,
                FontSize = 10,
                Foreground = Brushes.White
            };
            typeBadge.Child = typeText;
            Grid.SetColumn(typeBadge, 1);
            headerGrid.Children.Add(typeBadge);

            Grid.SetRow(headerGrid, 0);
            grid.Children.Add(headerGrid);

            // Description
            var descriptionText = new TextBlock
            {
                Text = strategy.Description ?? "No description",
                FontSize = 12,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCCCCC")),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(descriptionText, 2);
            grid.Children.Add(descriptionText);

            // Rules summary
            var rulesPanel = new StackPanel { Orientation = Orientation.Horizontal };

            var entryLabel = new TextBlock
            {
                Text = $"Entry: {strategy.EntryRules?.Count ?? 0} rules",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                Margin = new Thickness(0, 0, 20, 0)
            };
            rulesPanel.Children.Add(entryLabel);

            var exitLabel = new TextBlock
            {
                Text = $"Exit: {strategy.ExitRules?.Count ?? 0} rules",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#999999")),
                Margin = new Thickness(0, 0, 20, 0)
            };
            rulesPanel.Children.Add(exitLabel);

            var dateLabel = new TextBlock
            {
                Text = strategy.CreatedAt.HasValue ? $"Created: {strategy.CreatedAt.Value:MMM dd, yyyy}" : "Created: Recently",
                FontSize = 11,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"))
            };
            rulesPanel.Children.Add(dateLabel);

            Grid.SetRow(rulesPanel, 4);
            grid.Children.Add(rulesPanel);

            card.Child = grid;

            // Hover effects
            card.MouseEnter += (s, e) =>
            {
                card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3E3E42"));
            };

            card.MouseLeave += (s, e) =>
            {
                card.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D2D30"));
            };

            // Click to select
            card.MouseDown += (s, e) =>
            {
                SelectedStrategy = strategy;
                DialogResult = true;
                Close();
            };

            return card;
        }

        private Brush GetTypeColor(string type)
        {
            return type switch
            {
                "TrendFollowing" or "Trend Following" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0E639C")),
                "MeanReversion" or "Mean Reversion" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B4789")),
                "Momentum" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4EC9B0")),
                "VolatilityBreakout" or "Breakout" => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CE9178")),
                _ => new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666"))
            };
        }

        #region Event Handlers

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadStrategies();
            StatusText.Text = "Strategies refreshed";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void TypeFilterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Text = "";
            TypeFilterComboBox.SelectedIndex = 0;
            ApplyFilters();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        #endregion

        private void ApplyFilters()
        {
            _filteredStrategies = new List<Strategy>(_allStrategies);

            // Apply search filter
            var searchText = SearchTextBox?.Text?.ToLower() ?? "";
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                _filteredStrategies = _filteredStrategies.Where(s =>
                    s.Name.ToLower().Contains(searchText) ||
                    (s.Description?.ToLower().Contains(searchText) ?? false)
                ).ToList();
            }

            // Apply type filter
            if (TypeFilterComboBox?.SelectedIndex > 0)
            {
                var selectedType = (TypeFilterComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString();
                if (!string.IsNullOrEmpty(selectedType))
                {
                    _filteredStrategies = _filteredStrategies.Where(s =>
                        GetStrategyType(s) == selectedType || GetStrategyType(s).Replace(" ", "") == selectedType
                    ).ToList();
                }
            }

            UpdateStrategyDisplay();
        }
    }
}
