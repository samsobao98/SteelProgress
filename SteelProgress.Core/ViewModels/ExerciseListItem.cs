using SteelProgress.Core.Services;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Un ejercicio en la biblioteca (Rediseño 4): material, mejor marca y procedencia en una línea
/// («Barra · mejor: 72,5 × 8», «Polea · propio», «de Laura»).
/// </summary>
public sealed class ExerciseListItem
{
    public Exercise Exercise { get; }
    /// <summary>Su progreso en el historial del perfil (o cliente) activo; null si nunca se ha entrenado.</summary>
    public ExerciseProgress? Progress { get; }

    public string Name => Exercise.Name;
    /// <summary>Material y mejor marca: «Barra · mejor: 72,5 × 8».</summary>
    public string DetailText { get; }
    /// <summary>De dónde viene, resaltado: «propio», «de Laura»; vacío en la biblioteca.</summary>
    public string OriginText { get; }
    public bool HasOrigin => OriginText.Length > 0;
    /// <summary>« · » entre el detalle y la procedencia, si están los dos.</summary>
    public string OriginSeparator => HasDetail && HasOrigin ? " · " : "";
    public bool HasDetail => DetailText.Length > 0;
    public bool IsOwn => !Exercise.IsSystem;

    public ExerciseListItem(Exercise exercise, ExerciseProgress? progress)
    {
        Exercise = exercise;
        Progress = progress;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(exercise.Equipment)) parts.Add(exercise.Equipment.Trim());
        if (progress is not null) parts.Add($"mejor: {WorkoutExerciseState.FormatKg(progress.BestWeight)} × {progress.BestReps}");
        else if (IsOwn && exercise.Media.Count == 0) parts.Add("sin vídeo");
        DetailText = string.Join(" · ", parts);

        OriginText = OriginOf(exercise);
    }

    public static string OriginOf(Exercise exercise) =>
        exercise.IsSystem ? ""
        : !string.IsNullOrWhiteSpace(exercise.AuthorName) ? $"de {exercise.AuthorName.Trim()}"
        : "propio";
}

/// <summary>Los ejercicios de un grupo muscular, con su cabecera («PECHO»).</summary>
public sealed class ExerciseGroup : List<ExerciseListItem>
{
    public string Name { get; }
    public string Header => Name.ToUpperInvariant();

    public ExerciseGroup(string name, IEnumerable<ExerciseListItem> items) : base(items) => Name = name;

    /// <summary>Por grupo muscular en el orden del catálogo (los que no están en él, al final y por nombre).</summary>
    public static List<ExerciseGroup> Build(IEnumerable<ExerciseListItem> items)
    {
        var order = ExerciseCatalog.MuscleGroups.ToList();
        return items
            .GroupBy(i => string.IsNullOrWhiteSpace(i.Exercise.MuscleGroup) ? "Otros" : i.Exercise.MuscleGroup)
            .OrderBy(g => order.IndexOf(g.Key) is var k && k >= 0 ? k : int.MaxValue)
            .ThenBy(g => g.Key, StringComparer.CurrentCulture)
            .Select(g => new ExerciseGroup(g.Key, g.OrderBy(i => i.Name, StringComparer.CurrentCulture)))
            .ToList();
    }
}
