using System.Globalization;
using SteelProgress.Core.Services;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Converters;

// Ejercicio de un día → "4 × 6–8 · RPE 8 · 1:30".
// Con ConverterParameter=visible devuelve si hay algo que mostrar.
public class PrescriptionTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value is RoutineDayExercise item ? PrescriptionFormatter.Format(item) : string.Empty;
        return parameter is "visible" ? text.Length > 0 : text;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
