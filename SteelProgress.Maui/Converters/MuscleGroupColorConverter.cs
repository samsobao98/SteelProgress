using System.Globalization;

namespace SteelProgress.Maui.Converters;

public class MuscleGroupColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Color.FromArgb(value?.ToString() switch
        {
            "Pecho" or "Hombros"                               => "#89B4FA",
            "Espalda" or "Bíceps" or "Antebrazo"              => "#A6E3A1",
            "Tríceps"                                           => "#CBA6F7",
            "Abdominales" or "Lumbares"                        => "#F9E2AF",
            "Cuádriceps" or "Isquiotibiales" or "Glúteos" or "Gemelos" => "#FAB387",
            "Cardio" or "Cuerpo completo"                      => "#F38BA8",
            _                                                   => "#585B70"
        });

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
