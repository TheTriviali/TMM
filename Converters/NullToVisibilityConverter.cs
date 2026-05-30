using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TMM
{
    /// <summary>null → Collapsed, any non-null value → Visible.</summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is null ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
