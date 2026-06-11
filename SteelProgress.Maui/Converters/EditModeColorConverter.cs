using System.Globalization;

namespace SteelProgress.Maui.Converters;

public class EditModeColorConverter : IValueConverter
{
    public static readonly EditModeColorConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Color.FromArgb(value is true ? "#89B4FA" : "#A6E3A1");

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
