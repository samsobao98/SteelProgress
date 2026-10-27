using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

// Panel para editar lo prescrito en un ejercicio de un día de rutina.
// Los campos avanzados solo aparecen si la vista los permite o si ya tienen un valor
// (la vista simple nunca esconde datos que existan).
public partial class PrescriptionEditorViewModel : ObservableObject
{
    private readonly RoutineRepository _repository;
    private readonly ViewOptionsService _viewOptions;
    private RoutineDayExercise? _item;

    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _exerciseName = "";
    [ObservableProperty] private string? _error;

    [ObservableProperty] private string _sets = "";
    [ObservableProperty] private string _reps = "";
    [ObservableProperty] private string _repsMax = "";
    [ObservableProperty] private string _weight = "";
    [ObservableProperty] private string _percent1RM = "";
    [ObservableProperty] private string _rpe = "";
    [ObservableProperty] private string _rir = "";
    [ObservableProperty] private string _tempo = "";
    [ObservableProperty] private string _restSeconds = "";
    [ObservableProperty] private string _warmupSets = "";
    [ObservableProperty] private string _groupLabel = "";
    [ObservableProperty] private string _notes = "";

    [ObservableProperty] private bool _showRepRange;
    [ObservableProperty] private bool _showIntensity;
    [ObservableProperty] private bool _showTempoAndRest;
    [ObservableProperty] private bool _showStructure;

    public event Action? Saved;

    public PrescriptionEditorViewModel(RoutineRepository repository, ViewOptionsService viewOptions)
    {
        _repository = repository;
        _viewOptions = viewOptions;
    }

    public void Open(RoutineDayExercise item)
    {
        _item = item;
        ExerciseName = item.Exercise?.Name ?? "Ejercicio";
        Error = null;

        Sets = Text(item.TargetSets);
        Reps = Text(item.TargetReps);
        RepsMax = Text(item.TargetRepsMax);
        Weight = Text(item.TargetWeight);
        Percent1RM = Text(item.TargetPercent1RM);
        Rpe = Text(item.TargetRpe);
        Rir = Text(item.TargetRir);
        Tempo = item.Tempo ?? "";
        RestSeconds = Text(item.RestSeconds);
        WarmupSets = Text(item.WarmupSets);
        GroupLabel = item.GroupLabel ?? "";
        Notes = item.Notes ?? "";

        var o = _viewOptions.Current;
        ShowRepRange = o.EditRepRanges || item.TargetRepsMax is not null;
        ShowIntensity = o.EditIntensity || item.TargetWeight is not null || item.TargetPercent1RM is not null
                        || item.TargetRpe is not null || item.TargetRir is not null;
        ShowTempoAndRest = o.EditTempoAndRest || !string.IsNullOrWhiteSpace(item.Tempo) || item.RestSeconds is not null;
        ShowStructure = o.EditAdvancedStructure || item.WarmupSets is not null || !string.IsNullOrWhiteSpace(item.GroupLabel);

        IsOpen = true;
    }

    [RelayCommand]
    private void Close() => IsOpen = false;

    [RelayCommand]
    public void Save()
    {
        if (_item is null) return;

        if (!TryInt(Sets, 1, 50, out var sets)) { Error = "Series: un número entre 1 y 50."; return; }
        if (!TryInt(Reps, 1, 999, out var reps)) { Error = "Repeticiones: un número entre 1 y 999."; return; }
        if (!TryInt(RepsMax, 1, 999, out var repsMax)) { Error = "Repeticiones máximas: un número entre 1 y 999."; return; }
        if (reps is not null && repsMax is not null && repsMax < reps) { Error = "El máximo del rango no puede ser menor que el mínimo."; return; }
        if (!TryDouble(Weight, 0, 1000, out var weight)) { Error = "Peso: un número entre 0 y 1000."; return; }
        if (!TryDouble(Percent1RM, 1, 150, out var pct)) { Error = "% de 1RM: un número entre 1 y 150."; return; }
        if (!TryDouble(Rpe, 1, 10, out var rpe)) { Error = "RPE: un número entre 1 y 10."; return; }
        if (!TryInt(Rir, 0, 10, out var rir)) { Error = "RIR: un número entre 0 y 10."; return; }
        if (!TryInt(RestSeconds, 0, 3600, out var rest)) { Error = "Descanso: segundos, entre 0 y 3600."; return; }
        if (!TryInt(WarmupSets, 0, 20, out var warmup)) { Error = "Series de calentamiento: entre 0 y 20."; return; }

        _item.TargetSets = sets;
        _item.TargetReps = reps;
        _item.TargetRepsMax = repsMax;
        _item.TargetWeight = weight;
        _item.TargetPercent1RM = pct;
        _item.TargetRpe = rpe;
        _item.TargetRir = rir;
        _item.Tempo = Clean(Tempo);
        _item.RestSeconds = rest;
        _item.WarmupSets = warmup;
        _item.GroupLabel = Clean(GroupLabel)?.ToUpperInvariant();
        _item.Notes = Clean(Notes);

        _repository.UpdateRoutineDayExercise(_item);

        Error = null;
        IsOpen = false;
        Saved?.Invoke();
    }

    private static string Text(int? v) => v?.ToString(CultureInfo.InvariantCulture) ?? "";
    private static string Text(double? v) => v?.ToString("0.##", CultureInfo.GetCultureInfo("es-ES")) ?? "";
    private static string? Clean(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // Vacío = sin valor. Acepta coma o punto decimal.
    public static bool TryDouble(string text, double min, double max, out double? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return false;
        if (v < min || v > max) return false;
        value = v;
        return true;
    }

    public static bool TryInt(string text, int min, int max, out int? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(text)) return true;
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return false;
        if (v < min || v > max) return false;
        value = v;
        return true;
    }
}
