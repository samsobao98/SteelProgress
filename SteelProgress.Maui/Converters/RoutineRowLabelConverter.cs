using System.Globalization;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Converters;

// Etiqueta de un ejercicio en el día: la de su superserie ("A1") o su posición ("3")
public class RoutineRowLabelConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RoutineDayExercise rde
            ? (string.IsNullOrWhiteSpace(rde.GroupLabel) ? rde.Order.ToString(culture) : rde.GroupLabel)
            : "";

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
