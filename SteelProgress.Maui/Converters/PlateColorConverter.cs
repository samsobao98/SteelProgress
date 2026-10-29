using System.Globalization;

namespace SteelProgress.Maui.Converters;

/// <summary>
/// Colores de los discos olímpicos (maquetas «Acero + Discos»): siempre con su significado en kilos.
/// Rojo 25, azul 20, amarillo 15, verde 10, blanco 5 y cromados los pequeños.
/// </summary>
public static class PlateColors
{
    public static Color Of(double kg) => kg switch
    {
        >= 25 => Color.FromArgb("#D9473F"),
        >= 20 => Color.FromArgb("#2E6BD6"),
        >= 15 => Color.FromArgb("#E8B931"),
        >= 10 => Color.FromArgb("#3F9E5D"),
        >= 5 => Color.FromArgb("#E9E9E6"),
        _ => Color.FromArgb("#AEB5C8")
    };

    /// <summary>Alto relativo del disco (1 = el de 25/20) para dibujarlo a escala aproximada.</summary>
    public static float HeightOf(double kg) => kg switch
    {
        >= 20 => 1f,
        >= 15 => 0.9f,
        >= 10 => 0.8f,
        >= 5 => 0.62f,
        >= 2.5 => 0.5f,
        _ => 0.42f
    };
}

/// <summary>Peso de un disco → su color.</summary>
public class PlateColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        PlateColors.Of(value is double kg ? kg : 0);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}

/// <summary>Peso de un disco → alto de su muestra en «Tu gimnasio» (parámetro: alto máximo).</summary>
public class PlateHeightConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var max = double.TryParse(parameter as string, NumberStyles.Float, CultureInfo.InvariantCulture, out var m) ? m : 22;
        return max * PlateColors.HeightOf(value is double kg ? kg : 0);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotImplementedException();
}
