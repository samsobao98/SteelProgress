using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.Services;

// Qué opciones de edición ve el perfil actual.
// Regla de diseño: la vista simple quita opciones de edición, nunca datos. Lo que una rutina
// ya tenga (RPE, tempo, notas...) se muestra siempre; estas banderas solo deciden qué se edita.
public sealed record ViewOptions(ProfileViewMode Mode, SimpleViewBlocks Blocks, bool TrainsSelf)
{
    public static ViewOptions Default { get; } = new(ProfileViewMode.Simple, SimpleViewBlocks.None, false);

    public static ViewOptions For(UserProfile profile) =>
        new(profile.ViewMode, profile.SimpleViewBlocks, profile.TrainsSelf)
        {
            Gym = GymSetup.From(profile.GymPlates, profile.BarWeight)
        };

    /// <summary>Discos y barra del gimnasio del perfil.</summary>
    public GymSetup Gym { get; init; } = GymSetup.Default;

    public bool IsTrainer => Mode == ProfileViewMode.Trainer;

    public bool EditIntensity => Allows(SimpleViewBlocks.Intensity);
    public bool EditTempoAndRest => Allows(SimpleViewBlocks.TempoAndRest);
    public bool EditAdvancedStructure => Allows(SimpleViewBlocks.AdvancedStructure);
    public bool EditRepRanges => Allows(SimpleViewBlocks.RepRanges);
    public bool EditDetailedNotes => Allows(SimpleViewBlocks.DetailedNotes);

    // Material, instrucciones y clientes: solo vista entrenador
    public bool EditExerciseDetails => IsTrainer;
    public bool ShowClients => IsTrainer;

    // En vista entrenador, "Yo" solo aparece en Entreno si el perfil también entrena
    public bool ShowOwnTraining => !IsTrainer || TrainsSelf;

    private bool Allows(SimpleViewBlocks block) => IsTrainer || Blocks.HasFlag(block);
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
