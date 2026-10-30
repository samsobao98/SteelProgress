using CommunityToolkit.Mvvm.ComponentModel;

namespace SteelProgress.Core.ViewModels;

/// <summary>Un disco en «Tu gimnasio» (Perfil): su peso y si lo hay.</summary>
public partial class PlateOption : ObservableObject
{
    public double Weight { get; }
    public string Label => WorkoutExerciseState.FormatKg(Weight);

    [ObservableProperty] private bool _isAvailable;

    public PlateOption(double weight, bool isAvailable)
    {
        Weight = weight;
        _isAvailable = isAvailable;
    }
}
