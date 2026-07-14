using System.Globalization;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Converters;

/// <summary>
/// Convierte un <see cref="RoutineDayExercise"/> en su texto de objetivo:
/// "4×8", "4 series", "8 reps" o "—" según qué campos estén definidos.
/// </summary>
public class TargetSetsRepsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not RoutineDayExercise rde)
            return "—";

        var sets = rde.TargetSets;
        var reps = rde.TargetReps;

        if (sets is > 0 && reps is > 0)
            return $"{sets}×{reps}";
        if (sets is > 0)
            return $"{sets} {(sets == 1 ? "serie" : "series")}";
        if (reps is > 0)
            return $"{reps} reps";

        return "—";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
