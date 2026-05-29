using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TMM
{
    /// <summary>Maps IsFavorite (bool) to a Segoe MDL2 Assets glyph: filled star vs hollow star.</summary>
    public class StarGlyphConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? "" : "";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }

    /// <summary>Maps IsFavorite to a brush — gold when on, muted gray when off.</summary>
    public class StarColorConverter : IValueConverter
    {
        private static readonly Brush GoldBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3A));
        private static readonly Brush MutedBrush = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66));

        static StarColorConverter() { GoldBrush.Freeze(); MutedBrush.Freeze(); }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? GoldBrush : MutedBrush;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
