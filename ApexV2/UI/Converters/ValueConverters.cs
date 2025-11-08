#nullable disable
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ApexV2.Charts.Controls
{
    /// <summary>
    /// Converts boolean values to colors for connection status indicators
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public Color TrueColor { get; set; } = Colors.LimeGreen;
        public Color FalseColor { get; set; } = Colors.Red;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueColor : FalseColor;
            }
            return FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean values to foreground brushes for connection status text
    /// </summary>
    public class BoolToForegroundConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = new SolidColorBrush(Colors.LimeGreen);
        public Brush FalseBrush { get; set; } = new SolidColorBrush(Colors.Red);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueBrush : FalseBrush;
            }
            return FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}

namespace ApexV2.Dashboard.Portfolio
{
    /// <summary>
    /// Converts numeric values to colors (green for positive, red for negative)
    /// </summary>
    public class ValueToColorConverter : IValueConverter
    {
        public Color PositiveColor { get; set; } = Colors.LimeGreen;
        public Color NegativeColor { get; set; } = Colors.Red;
        public Color NeutralColor { get; set; } = Colors.White;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                if (decimalValue > 0) return PositiveColor;
                if (decimalValue < 0) return NegativeColor;
                return NeutralColor;
            }

            if (value is double doubleValue)
            {
                if (doubleValue > 0) return PositiveColor;
                if (doubleValue < 0) return NegativeColor;
                return NeutralColor;
            }

            return NeutralColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts numeric values to foreground brushes (green for positive, red for negative)
    /// </summary>
    public class ValueToForegroundConverter : IValueConverter
    {
        public Brush PositiveBrush { get; set; } = new SolidColorBrush(Colors.LimeGreen);
        public Brush NegativeBrush { get; set; } = new SolidColorBrush(Colors.Red);
        public Brush NeutralBrush { get; set; } = new SolidColorBrush(Colors.White);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                if (decimalValue > 0) return PositiveBrush;
                if (decimalValue < 0) return NegativeBrush;
                return NeutralBrush;
            }

            if (value is double doubleValue)
            {
                if (doubleValue > 0) return PositiveBrush;
                if (doubleValue < 0) return NegativeBrush;
                return NeutralBrush;
            }

            return NeutralBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean values to colors for portfolio-specific controls
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public Color TrueColor { get; set; } = Colors.LimeGreen;
        public Color FalseColor { get; set; } = Colors.Red;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueColor : FalseColor;
            }
            return FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converts boolean values to foreground brushes for portfolio-specific controls
    /// </summary>
    public class BoolToForegroundConverter : IValueConverter
    {
        public Brush TrueBrush { get; set; } = new SolidColorBrush(Colors.LimeGreen);
        public Brush FalseBrush { get; set; } = new SolidColorBrush(Colors.Red);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueBrush : FalseBrush;
            }
            return FalseBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
