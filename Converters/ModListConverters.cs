using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using TMM.Services;

namespace TMM
{
    /// <summary>
    /// Converts a mod category string to the 4 px left-spine brush.
    /// Null/empty → neutral uncategorized colour. Stateless; safe as a shared singleton.
    /// </summary>
    public sealed class CategorySpineBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            ModCategories.BrushFor(value as string);

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }

    /// <summary>
    /// Converts a <see cref="ModConflictSummary"/> to <see cref="Visibility"/>:
    /// Visible when the summary is non-null and has at least one clash; Collapsed otherwise.
    /// </summary>
    public sealed class ConflictBadgeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is ModConflictSummary s && (s.OverwritesCount > 0 || s.OverwrittenByCount > 0)
                ? Visibility.Visible
                : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }

    /// <summary>True → Visible; False → Collapsed.</summary>
    public sealed class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            Binding.DoNothing;
    }
}
