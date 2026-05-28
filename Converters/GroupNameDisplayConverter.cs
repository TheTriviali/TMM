using System;
using System.Globalization;
using System.Windows.Data;

namespace TMM
{
    /// <summary>Displays a friendly header for mod groups in the mod list.</summary>
    public sealed class GroupNameDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string? group = value as string;
            return string.IsNullOrWhiteSpace(group) ? "(Ungrouped)" : group;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
