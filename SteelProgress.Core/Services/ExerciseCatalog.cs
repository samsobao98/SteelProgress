using System.Globalization;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Services;

// Grupos musculares y búsqueda de ejercicios, compartidos por la pantalla de Ejercicios
// y el panel de añadir ejercicios a una rutina
public static class ExerciseCatalog
{
    public static IReadOnlyList<string> MuscleGroups { get; } =
    [
        "Pecho",
        "Espalda",
        "Hombros",
        "Bíceps",
        "Tríceps",
        "Antebrazo",
        "Abdominales",
        "Lumbares",
        "Cuádriceps",
        "Isquiotibiales",
        "Glúteos",
        "Gemelos",
        "Cardio",
        "Cuerpo completo",
    ];

    /// <summary>Filtro de la biblioteca que deja solo los ejercicios propios o importados (no los de la biblioteca).</summary>
    public const string OwnFilter = "★ Propios";

    /// <summary>
    /// Filtra por grupo (null = todos; <see cref="OwnFilter"/> = solo propios) y por texto en nombre,
    /// grupo o etiquetas, sin distinguir tildes.
    /// </summary>
    public static IEnumerable<Exercise> Filter(IEnumerable<Exercise> exercises, string? group, string? search)
    {
        var result = exercises;

        if (group == OwnFilter)
            result = result.Where(e => !e.IsSystem);
        else if (!string.IsNullOrWhiteSpace(group))
            result = result.Where(e => e.MuscleGroup == group);

        var text = search?.Trim() ?? "";
        if (text.Length > 0)
        {
            // IgnoreNonSpace: "biceps" encuentra "Bíceps"
            var compare = CultureInfo.InvariantCulture.CompareInfo;
            const CompareOptions opts = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            result = result.Where(e =>
                compare.IndexOf(e.Name, text, opts) >= 0 ||
                compare.IndexOf(e.MuscleGroup, text, opts) >= 0 ||
                (e.Tags is not null && compare.IndexOf(e.Tags, text, opts) >= 0));
        }

        return result;
    }

    /// <summary>Etiquetas de un ejercicio («unilateral, casa» → [unilateral, casa]), sin vacías ni repetidas.</summary>
    public static IReadOnlyList<string> TagsOf(string? tags) =>
        (tags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    /// <summary>Lo que se guarda: las etiquetas unidas por «, » (null si no hay).</summary>
    public static string? JoinTags(IEnumerable<string> tags)
    {
        var list = TagsOf(string.Join(",", tags));
        return list.Count == 0 ? null : string.Join(", ", list);
    }
}
