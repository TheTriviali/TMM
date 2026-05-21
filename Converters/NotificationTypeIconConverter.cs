using System;
using System.Globalization;
using System.Windows.Data;

namespace TGTAMM
{
    public class NotificationTypeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
            {
                return type switch
                {
                    NotificationType.Success => "✓",
                    NotificationType.Warning => "⚠",
                    NotificationType.Error => "✕",
                    _ => "ⓘ"
                };
            }
            return "ⓘ";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
