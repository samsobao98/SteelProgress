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

    /// <summary>Filtra por grupo (null = todos) y por texto en nombre o grupo, sin distinguir tildes.</summary>
    public static IEnumerable<Exercise> Filter(IEnumerable<Exercise> exercises, string? group, string? search)
    {
        var result = exercises;

        if (!string.IsNullOrWhiteSpace(group))
            result = result.Where(e => e.MuscleGroup == group);

        var text = search?.Trim() ?? "";
        if (text.Length > 0)
        {
            // IgnoreNonSpace: "biceps" encuentra "Bíceps"
            var compare = CultureInfo.InvariantCulture.CompareInfo;
            const CompareOptions opts = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;
            result = result.Where(e =>
                compare.IndexOf(e.Name, text, opts) >= 0 ||
                compare.IndexOf(e.MuscleGroup, text, opts) >= 0);
        }

        return result;
    }
}
