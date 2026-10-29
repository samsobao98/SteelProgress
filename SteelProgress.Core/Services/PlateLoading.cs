using System.Globalization;

namespace SteelProgress.Core.Services;

/// <summary>
/// «Tu gimnasio»: los discos que hay y lo que pesa la barra. Se guarda en el perfil como texto
/// («25;20;15;10;5;2.5») para no depender de la cultura del móvil; null en el perfil = lo de siempre.
/// </summary>
public sealed record GymSetup(string PlatesText, double BarWeight)
{
    /// <summary>Todos los discos que se pueden marcar, de mayor a menor.</summary>
    public static readonly IReadOnlyList<double> AllPlates = new[] { 25, 20, 15, 10, 5, 2.5, 1.25 };
    public static readonly IReadOnlyList<double> DefaultPlates = new[] { 25, 20, 15, 10, 5, 2.5 };
    public const double DefaultBarWeight = 20;

    public static GymSetup Default { get; } = new(Format(DefaultPlates), DefaultBarWeight);

    public static GymSetup From(string? platesText, double? barWeight) =>
        new(Format(Parse(platesText) is { Count: > 0 } p ? p : DefaultPlates), barWeight ?? DefaultBarWeight);

    public IReadOnlyList<double> Plates => Parse(PlatesText);

    public static List<double> Parse(string? text) =>
        (text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0)
            .Where(v => v > 0)
            .Distinct()
            .OrderByDescending(v => v)
            .ToList();

    public static string Format(IEnumerable<double> plates) =>
        string.Join(";", plates.Where(p => p > 0).Distinct().OrderByDescending(p => p).Select(p => p.ToString(CultureInfo.InvariantCulture)));
}

/// <summary>Cómo cargar un peso: discos de un lado (de dentro afuera, el más pesado primero) y lo que sale de verdad.</summary>
public sealed record PlateLoad(double Target, double Base, IReadOnlyList<double> PerSide, double Achieved)
{
    /// <summary>Con estos discos sale justo el peso pedido.</summary>
    public bool IsExact => Math.Abs(Achieved - Target) < 0.001;
    /// <summary>El peso pedido es menor que la barra (o el carro) sola.</summary>
    public bool BelowBase => Target < Base - 0.001;
}

public static class PlateLoading
{
    // Se calcula en cuartos de kilo para no arrastrar errores de coma flotante (1,25 = 5 cuartos)
    private const int Units = 4;

    /// <summary>
    /// Discos por lado para llegar a <paramref name="target"/> con una barra (o carro) de <paramref name="baseWeight"/>,
    /// suponiendo que de cada disco hay los que hagan falta. Si no sale justo, lo más cercano por debajo.
    /// Con varias formas, la de menos discos y, a igualdad, los más pesados primero.
    /// </summary>
    public static PlateLoad For(double target, double baseWeight, IEnumerable<double> plates)
    {
        var available = plates.Where(p => p > 0).Distinct().OrderByDescending(p => p).ToList();
        var perSideTarget = (int)Math.Floor(Math.Max(0, target - baseWeight) * Units / 2 + 0.0001);
        if (perSideTarget == 0 || available.Count == 0)
            return new PlateLoad(target, baseWeight, Array.Empty<double>(), baseWeight);

        var sizes = available.Select(p => (int)Math.Round(p * Units)).ToList();
        // fewest[s] = mínimo de discos para sumar exactamente s cuartos por lado (-1 si no se puede)
        var fewest = new int[perSideTarget + 1];
        Array.Fill(fewest, -1);
        fewest[0] = 0;
        for (var s = 1; s <= perSideTarget; s++)
            foreach (var size in sizes)
                if (size <= s && fewest[s - size] >= 0 && (fewest[s] < 0 || fewest[s - size] + 1 < fewest[s]))
                    fewest[s] = fewest[s - size] + 1;

        var reach = perSideTarget;
        while (fewest[reach] < 0) reach--;

        // Reconstruye quitando siempre el disco más pesado que mantiene el mínimo
        var side = new List<double>();
        for (var left = reach; left > 0;)
            for (var i = 0; i < sizes.Count; i++)
                if (sizes[i] <= left && fewest[left - sizes[i]] == fewest[left] - 1)
                {
                    side.Add(available[i]);
                    left -= sizes[i];
                    break;
                }

        return new PlateLoad(target, baseWeight, side, baseWeight + 2.0 * reach / Units);
    }
}
