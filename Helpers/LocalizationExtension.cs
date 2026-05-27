using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using TMM.Services;

namespace TMM.Helpers
{
    /// <summary>
    /// XAML markup extension for string localization with live language switching.
    /// Usage: Text="{local:Loc Window_MainTitle}"
    /// </summary>
    public class LocalizationExtension : MarkupExtension
    {
        public string Key { get; set; } = string.Empty;

        public LocalizationExtension()
        {
        }

        public LocalizationExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrEmpty(Key))
                return string.Empty;

            // Create binding to LocalizationConverter with CurrentLanguage as path
            var binding = new Binding(nameof(LocalizationService.CurrentLanguage))
            {
                Source = LocalizationService.Instance,
                Converter = new LocalizationConverter(),
                ConverterParameter = Key,
                Mode = BindingMode.OneWay
            };

            return binding.ProvideValue(serviceProvider);
        }
    }

    /// <summary>
    /// Converter for localization. Returns string for given key whenever language changes.
    /// </summary>
    public class LocalizationConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var key = parameter as string ?? string.Empty;
            return LocalizationService.Instance[key];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
