using System;
using System.Globalization;
using System.Windows.Data;

namespace TMM
{
    public class NotificationTypeIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is NotificationType type)
            {
                return type switch
                {
                    NotificationType.Success => "âœ“",
                    NotificationType.Warning => "âš ",
                    NotificationType.Error => "x",
                    _ => "â“˜"
                };
            }
            return "â“˜";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
