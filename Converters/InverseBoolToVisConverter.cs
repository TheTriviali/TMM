using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TMM
{
    /// <summary>Converts bool to Visibility, inverted: true → Collapsed, false → Visible.</summary>
    public class InverseBoolToVisConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is Visibility.Collapsed;
    }
}
