using System.Globalization;

namespace SteelProgress.Maui.Converters;

public class MuscleGroupColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Color.FromArgb(value?.ToString() switch
        {
            "Pecho" or "Hombros"                               => "#9DB4E6",
            "Espalda" or "Bíceps" or "Antebrazo"              => "#A6E3A1",
            "Tríceps"                                           => "#C9B9F2",
            "Abdominales" or "Lumbares"                        => "#F0C46A",
            "Cuádriceps" or "Isquiotibiales" or "Glúteos" or "Gemelos" => "#FAB387",
            "Cardio" or "Cuerpo completo"                      => "#EF8A8A",
            _                                                   => "#6A7088"
        });

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
