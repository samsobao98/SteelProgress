using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Pantalla de lista de rutinas: muestra cada rutina con su resumen de
/// días/ejercicios y permite crear nuevas. La gestión de días y ejercicios
/// vive en <see cref="RoutineDetailViewModel"/>.
/// </summary>
public partial class RoutineViewModel : ObservableObject
{
    private readonly RoutineRepository _repository;

    public ObservableCollection<RoutineSummary> Routines { get; } = new();

    [ObservableProperty] private bool _isLoaded;

    // ── Overlay de creación ────────────────────────────────────────────────
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newNotes = string.Empty;

    public bool HasRoutines => Routines.Count > 0;

    /// <summary>Se dispara tras crear una rutina con éxito (para cerrar el sheet).</summary>
    public event Action? RoutineCreated;

    public RoutineViewModel(RoutineRepository repository)
    {
        _repository = repository;
    }

    public async Task RefreshAsync()
    {
        var summaries = await _repository.GetRoutineSummariesAsync();
        Routines.Clear();
        foreach (var s in summaries)
            Routines.Add(s);

        IsLoaded = true;
        OnPropertyChanged(nameof(HasRoutines));
    }

    public void ClearForm()
    {
        NewName = string.Empty;
        NewNotes = string.Empty;
    }

    [RelayCommand]
    private async Task CreateRoutine()
    {
        var name = NewName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            NotificationService.Error("El nombre es obligatorio");
            return;
        }

        var routine = new Routine
        {
            Name = name,
            Notes = string.IsNullOrWhiteSpace(NewNotes) ? null : NewNotes.Trim()
        };

        await _repository.AddAsync(routine);
        ClearForm();
        await RefreshAsync();
        RoutineCreated?.Invoke();
    }
}
