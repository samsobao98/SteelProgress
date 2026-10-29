using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

public partial class ExerciseViewModel : ObservableObject
{
    private readonly ExerciseRepository _repository;
    private List<Exercise> _allExercises = new();
    private readonly ExerciseMediaService? _media;
    private readonly ViewOptionsService? _viewOptions;
    private readonly WorkoutRepository? _workouts;
    // Progreso de cada ejercicio en el historial del perfil (o cliente) activo, por Id
    private Dictionary<int, ExerciseProgress> _progress = new();

    // Vídeos e imágenes del ejercicio que se está creando o editando.
    // Al crear, se copian a la app al elegirlos y se asocian al guardar; si se cancela, se borran.
    public ObservableCollection<ExerciseMedia> Media { get; } = new();

    public bool MediaEnabled => _media is not null;

    [ObservableProperty] private bool _isImportingMedia;

    public ObservableCollection<Exercise> Exercises { get; } = new();

    /// <summary>Lo filtrado, agrupado por grupo muscular (la lista de la pantalla).</summary>
    public ObservableCollection<ExerciseGroup> Groups { get; } = new();

    /// <summary>«111 ejercicios · 3 propios» (todos, sin filtrar).</summary>
    [ObservableProperty] private string _countText = string.Empty;

    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary>Grupo muscular activo en el filtro; null = todos.</summary>
    [ObservableProperty] private string? _selectedGroupFilter;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedGroupFilterChanged(string? value) => ApplyFilter();

    public IReadOnlyList<string> MuscleGroups => ExerciseCatalog.MuscleGroups;

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _muscleGroup;
    [ObservableProperty] private string _notes = string.Empty;
    /// <summary>Con qué se hace (obligatorio): los chips con icono del formulario.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOtherEquipment))]
    private EquipmentType? _selectedEquipment;
    public bool IsOtherEquipment => SelectedEquipment == EquipmentType.Other;
    public IReadOnlyList<EquipmentType> EquipmentTypes => EquipmentCatalog.All;
    /// <summary>Solo con «Otro»: cuál (gomas, TRX…).</summary>
    [ObservableProperty] private string _equipment = string.Empty;
    [ObservableProperty] private string _instructions = string.Empty;
    /// <summary>Etiquetas separadas por comas.</summary>
    [ObservableProperty] private string _tags = string.Empty;

    /// <summary>Instrucciones y etiquetas: solo vista entrenador (si el ejercicio ya las tiene, se ven siempre).</summary>
    public bool ShowDetailFields => (_viewOptions?.Current.EditExerciseDetails ?? false)
        || Instructions.Length > 0 || Tags.Length > 0;

    [RelayCommand]
    private void SelectEquipment(EquipmentType type) => SelectedEquipment = type;

    /// <summary>Grupo muscular elegido con los chips del formulario.</summary>
    [RelayCommand]
    private void SelectMuscleGroup(string? group) => MuscleGroup = group;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAddMode), nameof(OverlayTitle), nameof(SaveButtonText))]
    private bool _isEditMode;

    public bool IsAddMode => !IsEditMode;
    public string OverlayTitle => IsEditMode ? "Editar ejercicio" : "Nuevo ejercicio";
    public string SaveButtonText => IsEditMode ? "Guardar cambios" : "Añadir ejercicio";

    private Exercise? _selectedExercise;
    public Exercise? SelectedExercise
    {
        get => _selectedExercise;
        set
        {
            _selectedExercise = value;
            OnPropertyChanged();
            IsEditMode = value is not null;
            LoadSelectedExerciseIntoForm();
        }
    }

    // media es opcional para que la app de escritorio, que crea el ViewModel a mano, siga compilando
    public ExerciseViewModel(ExerciseRepository repository, ExerciseMediaService? media = null, ViewOptionsService? viewOptions = null,
        WorkoutRepository? workouts = null)
    {
        _repository = repository;
        _media = media;
        _viewOptions = viewOptions;
        _workouts = workouts;
        _ = LoadExercisesAsync();
    }

    public Task RefreshAsync() => LoadExercisesAsync();

    private async Task LoadExercisesAsync()
    {
        _allExercises = await _repository.GetAllAsync();
        if (_workouts is not null)
            _progress = TrainingStats.Exercises((await _workouts.GetTrainingLogAsync()).Sets).ToDictionary(p => p.ExerciseId);
        int own = _allExercises.Count(e => !e.IsSystem);
        CountText = $"{Plural(_allExercises.Count, "ejercicio", "ejercicios")} · {Plural(own, "propio", "propios")}";
        ApplyFilter();
    }

    private static string Plural(int n, string one, string many) => $"{n} {(n == 1 ? one : many)}";

    /// <summary>La fila de un ejercicio (con su mejor marca y procedencia), para la ficha.</summary>
    public ExerciseListItem ItemOf(Exercise exercise) =>
        new(exercise, _progress.GetValueOrDefault(exercise.Id));

    private void ApplyFilter()
    {
        var result = ExerciseCatalog.Filter(_allExercises, SelectedGroupFilter, SearchText);

        Exercises.Clear();
        foreach (var exercise in result)
            Exercises.Add(exercise);

        Groups.Clear();
        foreach (var group in ExerciseGroup.Build(Exercises.Select(ItemOf)))
            Groups.Add(group);
    }

    public void ClearForm()
    {
        DiscardPendingMedia();
        Name = string.Empty;
        Notes = string.Empty;
        Equipment = string.Empty;
        SelectedEquipment = null;
        Instructions = string.Empty;
        Tags = string.Empty;
        MuscleGroup = null;
        SelectedExercise = null;
        OnPropertyChanged(nameof(ShowDetailFields));
    }

    private void LoadSelectedExerciseIntoForm()
    {
        DiscardPendingMedia();

        if (SelectedExercise is null)
        {
            Name = string.Empty;
            MuscleGroup = null;
            Notes = string.Empty;
            Equipment = string.Empty;
            SelectedEquipment = null;
            Instructions = string.Empty;
            Tags = string.Empty;
            OnPropertyChanged(nameof(ShowDetailFields));
            return;
        }

        foreach (var m in SelectedExercise.Media.OrderBy(m => m.Order))
            Media.Add(m);

        Name = SelectedExercise.Name;
        MuscleGroup = SelectedExercise.MuscleGroup;
        Notes = SelectedExercise.Notes ?? string.Empty;
        SelectedEquipment = SelectedExercise.EquipmentType
            ?? EquipmentCatalog.Classify(SelectedExercise.Equipment, SelectedExercise.Name);
        Equipment = SelectedEquipment == EquipmentType.Other ? SelectedExercise.Equipment?.Trim() ?? string.Empty : string.Empty;
        Instructions = SelectedExercise.Instructions ?? string.Empty;
        Tags = SelectedExercise.Tags ?? string.Empty;
        OnPropertyChanged(nameof(ShowDetailFields));
    }

    [RelayCommand]
    private async Task AddExercise()
    {
        string name = Name.Trim();
        string muscleGroup = MuscleGroup?.Trim() ?? string.Empty;
        string notes = Notes.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            NotificationService.Error("El nombre del ejercicio es obligatorio.");
            return;
        }

        if (string.IsNullOrWhiteSpace(muscleGroup))
        {
            NotificationService.Error("El grupo muscular es obligatorio.");
            return;
        }

        if (SelectedEquipment is not { } equipment)
        {
            NotificationService.Error("Elige con qué se hace el ejercicio.");
            return;
        }

        if (await _repository.ExistsByNameAsync(name))
        {
            NotificationService.Error("Ya existe un ejercicio con ese nombre.");
            return;
        }

        var exercise = new Exercise
        {
            Name = name,
            MuscleGroup = muscleGroup,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
            Equipment = EquipmentCatalog.StoredText(equipment, Equipment),
            EquipmentType = equipment,
            Instructions = Clean(Instructions),
            Tags = ExerciseCatalog.JoinTags([Tags])
        };
        for (var i = 0; i < Media.Count; i++)
        {
            Media[i].Order = i;
            exercise.Media.Add(Media[i]);
        }

        await _repository.AddAsync(exercise);
        Media.Clear(); // ya guardados: que ClearForm no los borre
        await LoadExercisesAsync();
        ClearForm();
        NotificationService.Success("Ejercicio añadido correctamente.");
    }

    [RelayCommand]
    private async Task UpdateExercise()
    {
        if (SelectedExercise is null)
        {
            NotificationService.Info("Selecciona un ejercicio para actualizar.");
            return;
        }

        if (SelectedExercise.IsSystem)
        {
            NotificationService.Error("Los ejercicios de la biblioteca no se pueden editar.");
            return;
        }

        string name = Name.Trim();
        string muscleGroup = MuscleGroup?.Trim() ?? string.Empty;
        string notes = Notes.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            NotificationService.Error("El nombre del ejercicio es obligatorio.");
            return;
        }

        if (string.IsNullOrWhiteSpace(muscleGroup))
        {
            NotificationService.Error("El grupo muscular es obligatorio.");
            return;
        }

        if (SelectedEquipment is not { } equipment)
        {
            NotificationService.Error("Elige con qué se hace el ejercicio.");
            return;
        }

        if (await _repository.ExistsByNameExceptIdAsync(name, SelectedExercise.Id))
        {
            NotificationService.Error("Ya existe otro ejercicio con ese nombre.");
            return;
        }

        SelectedExercise.Name = name;
        SelectedExercise.MuscleGroup = muscleGroup;
        SelectedExercise.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes;
        SelectedExercise.Equipment = EquipmentCatalog.StoredText(equipment, Equipment);
        SelectedExercise.EquipmentType = equipment;
        SelectedExercise.Instructions = Clean(Instructions);
        SelectedExercise.Tags = ExerciseCatalog.JoinTags([Tags]);

        await _repository.UpdateAsync(SelectedExercise);
        await LoadExercisesAsync();
        ClearForm();
        NotificationService.Success("Ejercicio actualizado correctamente.");
    }

    [RelayCommand]
    private async Task DeleteExercise()
    {
        if (SelectedExercise is null)
        {
            NotificationService.Info("Selecciona un ejercicio para eliminar.");
            return;
        }

        if (SelectedExercise.IsSystem)
        {
            NotificationService.Error("Los ejercicios de la biblioteca no se pueden eliminar.");
            return;
        }

        var confirmed = await ConfirmDialogService
            .ConfirmAsync("Eliminar ejercicio", $"¿Eliminar «{SelectedExercise.Name}»?", accept: "Eliminar", destructive: true);

        if (!confirmed)
            return;

        var files = _media is null ? new List<string>() : await _repository.GetMediaFileNamesAsync(SelectedExercise.Id);
        await _repository.DeleteAsync(SelectedExercise);
        _media?.DeleteFiles(files);
        Media.Clear();
        await LoadExercisesAsync();
        ClearForm();
    }

    private static string? Clean(string text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    // ── Vídeos e imágenes ─────────────────────────────────────────────────

    // La página abre la galería y pasa aquí el archivo elegido
    public async Task AddMediaAsync(Stream source, string originalFileName)
    {
        if (_media is null) return;

        IsImportingMedia = true;
        try
        {
            var media = await _media.StoreAsync(source, originalFileName);
            if (SelectedExercise is not null)
            {
                await _repository.AddMediaAsync(SelectedExercise.Id, media);
                SelectedExercise.Media.Add(media);
            }
            Media.Add(media);
        }
        catch (MediaImportException ex)
        {
            NotificationService.Error(ex.Message);
        }
        finally
        {
            IsImportingMedia = false;
        }
    }

    [RelayCommand]
    private async Task RemoveMedia(ExerciseMedia? media)
    {
        if (media is null || _media is null) return;

        if (SelectedExercise is not null)
        {
            await _repository.RemoveMediaAsync(media);
            SelectedExercise.Media.Remove(media);
        }
        _media.DeleteFile(media);
        Media.Remove(media);
    }

    public string? GetMediaPath(ExerciseMedia media) => _media?.Storage.GetFullPath(media.FileName);

    // Archivos elegidos para un ejercicio nuevo que no llegó a guardarse
    private void DiscardPendingMedia()
    {
        if (_media is null) return;
        foreach (var m in Media.Where(m => m.Id == 0).ToList())
            _media.DeleteFile(m);
        Media.Clear();
    }
}
