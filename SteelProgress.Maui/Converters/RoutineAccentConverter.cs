using System.Globalization;

namespace SteelProgress.Maui.Converters;

// Color de la insignia de una rutina (RoutineListItem.AccentIndex): intensidad, hecho, acero o descanso.
// ConverterParameter=bg devuelve el mismo color muy transparente para el fondo.
public class RoutineAccentConverter : IValueConverter
{
    private static readonly string[] Accents = ["#FAB387", "#A6E3A1", "#89B4FA", "#94E2D5"];

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hex = Accents[value is int i ? Math.Abs(i) % Accents.Length : 0];
        var color = Color.FromArgb(hex);
        return parameter is "bg" ? color.WithAlpha(0.14f) : color;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
