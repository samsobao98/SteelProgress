using System.Globalization;

namespace SteelProgress.Maui.Converters;

// true si el valor es null (p. ej. un ejercicio de rutina sin series indicadas)
public class IsNullConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
