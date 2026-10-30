using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Services;

// Qué ve y qué edita el perfil actual. Dos ejes independientes (2026-10-30):
// el tipo de perfil (personal o entrenador) decide si hay clientes; la vista (simple o detallada)
// decide cuánto se edita y cuántos apartados hay.
// Regla de diseño: la vista simple quita opciones de edición, nunca datos. Lo que una rutina
// ya tenga (RPE, tempo, notas...) se muestra siempre; estas banderas solo deciden qué se edita.
public sealed record ViewOptions(ProfileKind Kind, ProfileViewMode Mode, SimpleViewBlocks Blocks)
{
    public static ViewOptions Default { get; } = new(ProfileKind.Personal, ProfileViewMode.Simple, SimpleViewBlocks.None);

    public static ViewOptions For(UserProfile profile) =>
        new(profile.Kind, profile.ViewMode, profile.SimpleViewBlocks)
        {
            Gym = GymSetup.From(profile.GymPlates, profile.BarWeight)
        };

    /// <summary>Discos y barra del gimnasio del perfil.</summary>
    public GymSetup Gym { get; init; } = GymSetup.Default;

    public bool IsCoach => Kind == ProfileKind.Coach;
    public bool IsDetailed => Mode == ProfileViewMode.Detailed;

    public bool EditIntensity => Allows(SimpleViewBlocks.Intensity);
    public bool EditTempoAndRest => Allows(SimpleViewBlocks.TempoAndRest);
    public bool EditAdvancedStructure => Allows(SimpleViewBlocks.AdvancedStructure);
    public bool EditRepRanges => Allows(SimpleViewBlocks.RepRanges);
    public bool EditDetailedNotes => Allows(SimpleViewBlocks.DetailedNotes);

    // Material e instrucciones de los ejercicios, y crear ejercicios propios: vista detallada
    public bool EditExerciseDetails => IsDetailed;

    // Apartados del menú
    public bool ShowClients => IsCoach;
    public bool ShowExercises => IsDetailed;
    // El entrenador en vista simple usa las rutinas de cada cliente desde Clientes y Entreno;
    // crearlas y enviarlas es de la detallada
    public bool ShowRoutines => !IsCoach || IsDetailed;

    // El entrenador no entrena con su perfil: su entreno va en un perfil personal
    public bool ShowOwnTraining => !IsCoach;

    private bool Allows(SimpleViewBlocks block) => IsDetailed || Blocks.HasFlag(block);
}

// Opciones del perfil actual, compartidas por todas las pantallas
public class ViewOptionsService
{
    public ViewOptions Current { get; private set; } = ViewOptions.Default;

    public event Action? Changed;

    public void Apply(UserProfile profile)
    {
        var next = ViewOptions.For(profile);
        if (next == Current) return;
        Current = next;
        Changed?.Invoke();
    }

    public void Reset()
    {
        Current = ViewOptions.Default;
        Changed?.Invoke();
    }
}
