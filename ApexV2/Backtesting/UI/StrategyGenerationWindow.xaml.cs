using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ApexV2.Backtesting.StrategyGeneration;

namespace ApexV2.Backtesting.UI
{
    public partial class StrategyGenerationWindow : Window
    {
        public Strategy SelectedStrategy { get; private set; }
        public List<Strategy> GeneratedStrategies { get; private set; }

        public StrategyGenerationWindow()
        {
            InitializeComponent();
        }

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateInputs(out string errorMessage))
            {
                MessageBox.Show(errorMessage, "Invalid Input", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            GenerateButton.IsEnabled = false;
            ProgressBar.Visibility = Visibility.Visible;
            StatusText.Text = "Generating strategies...";

            try
            {
                await Task.Run(() => GenerateStrategies());

                StatusText.Text = $"Generated {GeneratedStrategies.Count} strategies successfully";
                ProgressBar.Visibility = Visibility.Collapsed;

                // Show selection window
                var selectionWindow = new StrategySelectionWindow(GeneratedStrategies);
                if (selectionWindow.ShowDialog() == true)
                {
                    SelectedStrategy = selectionWindow.SelectedStrategy;
                    DialogResult = true;
                    Close();
                }
                else
                {
                    GenerateButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating strategies: {ex.Message}", "Generation Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Error during generation";
                ProgressBar.Visibility = Visibility.Collapsed;
                GenerateButton.IsEnabled = true;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void GenerateStrategies()
        {
            // Get enabled indicators
            var enabledIndicators = GetEnabledIndicators();

            // Create configuration
            var config = new StrategyGenerationConfig
            {
                EnabledIndicators = enabledIndicators,
                MaxEntryRules = int.Parse(MaxEntryRulesTextBox.Text),
                MaxExitRules = int.Parse(MaxExitRulesTextBox.Text),
                ParameterRanges = CreateDefaultParameterRanges()
            };

            // Create generator
            var generator = new StrategyGenerator(config, new Random());

            // Generate strategies
            int totalStrategies = int.Parse(TotalStrategiesTextBox.Text);
            double randomPercentage = double.Parse(RandomPercentageTextBox.Text) / 100.0;
            double templatePercentage = double.Parse(TemplatePercentageTextBox.Text) / 100.0;

            GeneratedStrategies = generator.GenerateStrategies(totalStrategies, randomPercentage, templatePercentage);
        }

        private List<string> GetEnabledIndicators()
        {
            var indicators = new List<string>();

            if (RsiCheckBox.IsChecked == true) indicators.Add("RSI");
            if (MacdCheckBox.IsChecked == true) indicators.Add("MACD");
            if (SmaCheckBox.IsChecked == true) indicators.Add("SMA");
            if (EmaCheckBox.IsChecked == true) indicators.Add("EMA");
            if (BbCheckBox.IsChecked == true) indicators.Add("BB");
            if (AtrCheckBox.IsChecked == true) indicators.Add("ATR");
            if (StochCheckBox.IsChecked == true) indicators.Add("STOCH");
            if (WilliamsRCheckBox.IsChecked == true) indicators.Add("WILLIAMSR");
            if (CciCheckBox.IsChecked == true) indicators.Add("CCI");
            if (AdxCheckBox.IsChecked == true) indicators.Add("ADX");
            if (CmfCheckBox.IsChecked == true) indicators.Add("CMF");
            if (ObvCheckBox.IsChecked == true) indicators.Add("OBV");
            if (VwapCheckBox.IsChecked == true) indicators.Add("VWAP");
            if (WmaCheckBox.IsChecked == true) indicators.Add("WMA");
            if (DemaCheckBox.IsChecked == true) indicators.Add("DEMA");
            if (KeltnerCheckBox.IsChecked == true) indicators.Add("KELTNER");
            if (AdLineCheckBox.IsChecked == true) indicators.Add("ADLINE");

            return indicators;
        }

        private Dictionary<string, ParameterRange> CreateDefaultParameterRanges()
        {
            return new Dictionary<string, ParameterRange>
            {
                { "rsi_window", new ParameterRange(10, 20, 2) },
                { "rsi_overbought", new ParameterRange(65, 75, 5) },
                { "rsi_oversold", new ParameterRange(25, 35, 5) },
                { "macd_fast", new ParameterRange(10, 14, 2) },
                { "macd_slow", new ParameterRange(24, 30, 2) },
                { "macd_signal", new ParameterRange(7, 11, 2) },
                { "bb_window", new ParameterRange(15, 25, 5) },
                { "bb_std", new ParameterRange(1.5, 2.5, 0.5) },
                { "sma_window", new ParameterRange(40, 60, 10) },
                { "ema_window", new ParameterRange(15, 25, 5) },
                { "stoch_k", new ParameterRange(12, 16, 2) },
                { "stoch_d", new ParameterRange(3, 5, 1) },
                { "atr_window", new ParameterRange(12, 16, 2) },
                { "adx_window", new ParameterRange(12, 16, 2) },
                { "adx_threshold", new ParameterRange(20, 30, 5) }
            };
        }

        private bool ValidateInputs(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (!int.TryParse(TotalStrategiesTextBox.Text, out int totalStrategies) || totalStrategies <= 0)
            {
                errorMessage = "Total strategies must be a positive number";
                return false;
            }

            if (!double.TryParse(RandomPercentageTextBox.Text, out double randomPct) || randomPct < 0 || randomPct > 100)
            {
                errorMessage = "Random percentage must be between 0 and 100";
                return false;
            }

            if (!double.TryParse(TemplatePercentageTextBox.Text, out double templatePct) || templatePct < 0 || templatePct > 100)
            {
                errorMessage = "Template percentage must be between 0 and 100";
                return false;
            }

            if (randomPct + templatePct > 100)
            {
                errorMessage = "Random + Template percentages cannot exceed 100%";
                return false;
            }

            if (!int.TryParse(MaxEntryRulesTextBox.Text, out int maxEntry) || maxEntry <= 0)
            {
                errorMessage = "Max entry rules must be a positive number";
                return false;
            }

            if (!int.TryParse(MaxExitRulesTextBox.Text, out int maxExit) || maxExit <= 0)
            {
                errorMessage = "Max exit rules must be a positive number";
                return false;
            }

            var enabledIndicators = GetEnabledIndicators();
            if (enabledIndicators.Count == 0)
            {
                errorMessage = "Please enable at least one indicator";
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Window for selecting a strategy from generated list
    /// </summary>
    public partial class StrategySelectionWindow : Window
    {
        public Strategy SelectedStrategy { get; private set; }
        private List<Strategy> _strategies;

        public StrategySelectionWindow(List<Strategy> strategies)
        {
            _strategies = strategies;
            InitializeComponent();
            LoadStrategies();
        }

        private void InitializeComponent()
        {
            Width = 800;
            Height = 600;
            Title = "Select Strategy";
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#1E1E1E"));

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Header
            var header = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D30")),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3F3F46")),
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(20, 15)
            };

            var headerText = new TextBlock
            {
                Text = $"Select a Strategy ({_strategies.Count} generated)",
                FontSize = 18,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White
            };

            header.Child = headerText;
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            // Strategies List
            var scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _strategiesPanel = new StackPanel { Margin = new Thickness(20) };
            scrollViewer.Content = _strategiesPanel;
            Grid.SetRow(scrollViewer, 1);
            grid.Children.Add(scrollViewer);

            // Buttons
            var buttonPanel = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#252526")),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3F3F46")),
                BorderThickness = new Thickness(0, 1, 0, 0),
                Padding = new Thickness(20, 15)
            };

            var buttonGrid = new Grid();
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            buttonGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var cancelButton = new Button
            {
                Content = "Cancel",
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3C3C3C")),
                Foreground = System.Windows.Media.Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(20, 10),
                FontSize = 13
            };
            cancelButton.Click += (s, e) => { DialogResult = false; Close(); };

            Grid.SetColumn(cancelButton, 1);
            buttonGrid.Children.Add(cancelButton);

            buttonPanel.Child = buttonGrid;
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            Content = grid;
        }

        private StackPanel _strategiesPanel;

        private void LoadStrategies()
        {
            foreach (var strategy in _strategies.Take(50)) // Show first 50 for performance
            {
                var strategyCard = CreateStrategyCard(strategy);
                _strategiesPanel.Children.Add(strategyCard);
            }
        }

        private Border CreateStrategyCard(Strategy strategy)
        {
            var card = new Border
            {
                Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D30")),
                BorderBrush = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3F3F46")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 0, 0, 10),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var stackPanel = new StackPanel();

            var nameText = new TextBlock
            {
                Text = strategy.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = System.Windows.Media.Brushes.White,
                Margin = new Thickness(0, 0, 0, 5)
            };

            var typeText = new TextBlock
            {
                Text = $"Type: {strategy.StrategyType} | Method: {strategy.GenerationMethod}",
                FontSize = 11,
                Foreground = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#CCCCCC"))
            };

            stackPanel.Children.Add(nameText);
            stackPanel.Children.Add(typeText);

            card.Child = stackPanel;

            card.MouseEnter += (s, e) =>
            {
                card.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#3E3E42"));
            };

            card.MouseLeave += (s, e) =>
            {
                card.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2D2D30"));
            };

            card.MouseDown += (s, e) =>
            {
                SelectedStrategy = strategy;
                DialogResult = true;
                Close();
            };

            return card;
        }
    }
}
