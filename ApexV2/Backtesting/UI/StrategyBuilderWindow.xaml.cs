using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ApexV2.Backtesting.StrategyGeneration;

namespace ApexV2.Backtesting.UI
{
    public partial class StrategyBuilderWindow : Window
    {
        private List<RuleEditorControl> _entryRuleControls = new List<RuleEditorControl>();
        private List<RuleEditorControl> _exitRuleControls = new List<RuleEditorControl>();

        public Strategy BuiltStrategy { get; private set; }

        public StrategyBuilderWindow()
        {
            InitializeComponent();
        }

        private void AddEntryRuleButton_Click(object sender, RoutedEventArgs e)
        {
            AddRuleControl(EntryRulesPanel, _entryRuleControls, true);
        }

        private void AddExitRuleButton_Click(object sender, RoutedEventArgs e)
        {
            AddRuleControl(ExitRulesPanel, _exitRuleControls, false);
        }

        private void AddRuleControl(StackPanel container, List<RuleEditorControl> controlList, bool isEntryRule)
        {
            // Remove placeholder text if this is the first rule
            if (controlList.Count == 0)
            {
                container.Children.Clear();
            }

            var ruleControl = new RuleEditorControl();
            ruleControl.OnRemove += (s, args) =>
            {
                container.Children.Remove(ruleControl);
                controlList.Remove(ruleControl);

                // Show placeholder if no rules left
                if (controlList.Count == 0)
                {
                    var placeholder = new TextBlock
                    {
                        Text = isEntryRule ? "Click 'Add Rule' to define entry conditions" :
                                           "Click 'Add Rule' to define exit conditions",
                        FontSize = 12,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Padding = new Thickness(20)
                    };
                    container.Children.Add(placeholder);
                }
            };

            controlList.Add(ruleControl);
            container.Children.Add(ruleControl);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidateStrategy(out string errorMessage))
            {
                ValidationText.Text = errorMessage;
                return;
            }

            // Build strategy from rules
            BuiltStrategy = new Strategy
            {
                Name = StrategyNameTextBox.Text,
                Description = $"Custom {StrategyTypeComboBox.Text} strategy",
                StrategyType = GetStrategyType(),
                EntryRules = _entryRuleControls.Select(c => c.BuildRule()).ToList(),
                ExitRules = _exitRuleControls.Select(c => c.BuildRule()).ToList(),
                GenerationMethod = "Manual",
                CreatedDate = DateTime.Now
            };

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private bool ValidateStrategy(out string errorMessage)
        {
            errorMessage = string.Empty;

            if (string.IsNullOrWhiteSpace(StrategyNameTextBox.Text))
            {
                errorMessage = "Strategy name is required";
                return false;
            }

            if (_entryRuleControls.Count == 0)
            {
                errorMessage = "At least one entry rule is required";
                return false;
            }

            if (_exitRuleControls.Count == 0)
            {
                errorMessage = "At least one exit rule is required";
                return false;
            }

            return true;
        }

        private StrategyType GetStrategyType()
        {
            var selectedItem = StrategyTypeComboBox.SelectedItem as ComboBoxItem;
            var text = selectedItem?.Content?.ToString() ?? "Custom";

            return text switch
            {
                "Trend Following" => StrategyType.TrendFollowing,
                "Mean Reversion" => StrategyType.MeanReversion,
                "Momentum" => StrategyType.Momentum,
                "Breakout" => StrategyType.Breakout,
                "Swing Trading" => StrategyType.SwingTrading,
                _ => StrategyType.Custom
            };
        }
    }

    /// <summary>
    /// Control for editing a single strategy rule
    /// </summary>
    public class RuleEditorControl : Border
    {
        private ComboBox _indicatorComboBox;
        private ComboBox _conditionComboBox;
        private StackPanel _parametersPanel;
        private Button _removeButton;

        public event EventHandler OnRemove;

        public RuleEditorControl()
        {
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#252526"));
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3F3F46"));
            BorderThickness = new Thickness(1);
            Padding = new Thickness(15);
            Margin = new Thickness(0, 0, 0, 10);
            CornerRadius = new CornerRadius(4);

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Indicator ComboBox
            _indicatorComboBox = new ComboBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3C3C3C")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12
            };

            // Add indicator options
            var indicators = new[] { "RSI", "MACD", "SMA", "EMA", "BB", "ATR", "STOCH", "WILLIAMSR", "CCI", "ADX" };
            foreach (var indicator in indicators)
            {
                _indicatorComboBox.Items.Add(indicator);
            }
            _indicatorComboBox.SelectedIndex = 0;
            _indicatorComboBox.SelectionChanged += IndicatorComboBox_SelectionChanged;

            Grid.SetColumn(_indicatorComboBox, 0);
            grid.Children.Add(_indicatorComboBox);

            // Condition ComboBox
            _conditionComboBox = new ComboBox
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3C3C3C")),
                Foreground = Brushes.White,
                BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#555555")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                FontSize = 12
            };

            UpdateConditionOptions();

            Grid.SetColumn(_conditionComboBox, 2);
            grid.Children.Add(_conditionComboBox);

            // Remove Button
            _removeButton = new Button
            {
                Content = "Remove",
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8B0000")),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(12, 6, 12, 6),
                FontSize = 11
            };
            _removeButton.Click += RemoveButton_Click;

            Grid.SetColumn(_removeButton, 4);
            grid.Children.Add(_removeButton);

            Child = grid;
        }

        private void IndicatorComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateConditionOptions();
        }

        private void UpdateConditionOptions()
        {
            _conditionComboBox.Items.Clear();

            var selectedIndicator = _indicatorComboBox.SelectedItem?.ToString();

            // Add condition options based on indicator
            switch (selectedIndicator)
            {
                case "RSI":
                case "STOCH":
                case "WILLIAMSR":
                case "CCI":
                    _conditionComboBox.Items.Add("Oversold");
                    _conditionComboBox.Items.Add("Overbought");
                    _conditionComboBox.Items.Add("Rising");
                    _conditionComboBox.Items.Add("Falling");
                    break;

                case "MACD":
                    _conditionComboBox.Items.Add("Indicator Cross Above");
                    _conditionComboBox.Items.Add("Indicator Cross Below");
                    _conditionComboBox.Items.Add("Positive");
                    _conditionComboBox.Items.Add("Negative");
                    break;

                case "SMA":
                case "EMA":
                    _conditionComboBox.Items.Add("Price Cross Above");
                    _conditionComboBox.Items.Add("Price Cross Below");
                    _conditionComboBox.Items.Add("Price Above");
                    _conditionComboBox.Items.Add("Price Below");
                    break;

                case "BB":
                    _conditionComboBox.Items.Add("Price Above");
                    _conditionComboBox.Items.Add("Price Below");
                    _conditionComboBox.Items.Add("Price Cross Above");
                    _conditionComboBox.Items.Add("Price Cross Below");
                    break;

                case "ADX":
                    _conditionComboBox.Items.Add("Above");
                    _conditionComboBox.Items.Add("Below");
                    _conditionComboBox.Items.Add("Rising");
                    _conditionComboBox.Items.Add("Falling");
                    break;

                default:
                    _conditionComboBox.Items.Add("Rising");
                    _conditionComboBox.Items.Add("Falling");
                    _conditionComboBox.Items.Add("Positive");
                    _conditionComboBox.Items.Add("Negative");
                    break;
            }

            if (_conditionComboBox.Items.Count > 0)
            {
                _conditionComboBox.SelectedIndex = 0;
            }
        }

        private void RemoveButton_Click(object sender, RoutedEventArgs e)
        {
            OnRemove?.Invoke(this, EventArgs.Empty);
        }

        public StrategyRule BuildRule()
        {
            var indicator = _indicatorComboBox.SelectedItem.ToString();
            var condition = _conditionComboBox.SelectedItem.ToString();

            var rule = new StrategyRule
            {
                IndicatorName = indicator,
                Condition = ParseCondition(condition),
                Parameters = GetDefaultParameters(indicator)
            };

            return rule;
        }

        private RuleCondition ParseCondition(string conditionText)
        {
            return conditionText switch
            {
                "Oversold" => RuleCondition.Oversold,
                "Overbought" => RuleCondition.Overbought,
                "Price Cross Above" => RuleCondition.PriceCrossAbove,
                "Price Cross Below" => RuleCondition.PriceCrossBelow,
                "Indicator Cross Above" => RuleCondition.IndicatorCrossAbove,
                "Indicator Cross Below" => RuleCondition.IndicatorCrossBelow,
                "Price Above" => RuleCondition.PriceAbove,
                "Price Below" => RuleCondition.PriceBelow,
                "Rising" => RuleCondition.Rising,
                "Falling" => RuleCondition.Falling,
                "Positive" => RuleCondition.Positive,
                "Negative" => RuleCondition.Negative,
                "Above" => RuleCondition.Above,
                "Below" => RuleCondition.Below,
                _ => RuleCondition.Oversold
            };
        }

        private Dictionary<string, object> GetDefaultParameters(string indicator)
        {
            return indicator switch
            {
                "RSI" => new Dictionary<string, object>
                {
                    { "window", 14 },
                    { "overbought", 70.0 },
                    { "oversold", 30.0 }
                },
                "MACD" => new Dictionary<string, object>
                {
                    { "fast_period", 12 },
                    { "slow_period", 26 },
                    { "signal_period", 9 }
                },
                "SMA" => new Dictionary<string, object>
                {
                    { "window", 50 }
                },
                "EMA" => new Dictionary<string, object>
                {
                    { "window", 20 }
                },
                "BB" => new Dictionary<string, object>
                {
                    { "window", 20 },
                    { "std_dev", 2.0 }
                },
                "ATR" => new Dictionary<string, object>
                {
                    { "window", 14 }
                },
                "STOCH" => new Dictionary<string, object>
                {
                    { "k_window", 14 },
                    { "d_window", 3 },
                    { "smooth_k", 3 },
                    { "overbought", 80.0 },
                    { "oversold", 20.0 }
                },
                "ADX" => new Dictionary<string, object>
                {
                    { "window", 14 },
                    { "trend_threshold", 25.0 }
                },
                _ => new Dictionary<string, object>()
            };
        }
    }
}
