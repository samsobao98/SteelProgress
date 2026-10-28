using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Panel «Añadir ejercicio»: buscador, filtro por grupo y selección múltiple.
/// La selección conserva el orden en que se tocaron los ejercicios.
/// </summary>
public partial class ExercisePickerViewModel : ObservableObject
{
    private readonly List<ExercisePickerItem> _all = new();
    private readonly List<ExercisePickerItem> _selected = new();

    public ObservableCollection<ExercisePickerItem> Items { get; } = new();
    public IReadOnlyList<string> MuscleGroups => ExerciseCatalog.MuscleGroups;

    [ObservableProperty] private string _searchText = string.Empty;

    /// <summary>Grupo muscular activo en el filtro; null = todos.</summary>
    [ObservableProperty] private string? _groupFilter;

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnGroupFilterChanged(string? value) => ApplyFilter();

    public int SelectedCount => _selected.Count;
    public bool HasSelection => _selected.Count > 0;
    public string ConfirmText => _selected.Count switch
    {
        0 => "Elige ejercicios",
        1 => "Añadir 1 ejercicio",
        var n => $"Añadir {n} ejercicios"
    };

    /// <summary>Ejercicios elegidos, en el orden en que se tocaron.</summary>
    public IReadOnlyList<Exercise> Selected => _selected.Select(i => i.Exercise).ToList();

    public void Load(IEnumerable<Exercise> pool)
    {
        _all.Clear();
        _all.AddRange(pool.Select(e => new ExercisePickerItem(e)));
        Reset();
    }

    /// <summary>Deja el panel limpio para volver a abrirlo.</summary>
    public void Reset()
    {
        foreach (var item in _selected) item.IsSelected = false;
        _selected.Clear();
        SearchText = string.Empty;
        GroupFilter = null;
        ApplyFilter();
        NotifySelection();
    }

    [RelayCommand]
    private void Toggle(ExercisePickerItem? item)
    {
        if (item is null) return;
        if (item.IsSelected)
        {
            item.IsSelected = false;
            _selected.Remove(item);
        }
        else
        {
            item.IsSelected = true;
            _selected.Add(item);
        }
        NotifySelection();
    }

    private void ApplyFilter()
    {
        var visible = ExerciseCatalog.Filter(_all.Select(i => i.Exercise), GroupFilter, SearchText).ToHashSet();
        Items.Clear();
        foreach (var item in _all.Where(i => visible.Contains(i.Exercise)))
            Items.Add(item);
    }

    private void NotifySelection()
    {
        OnPropertyChanged(nameof(SelectedCount));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(ConfirmText));
    }
}

public partial class ExercisePickerItem : ObservableObject
{
    public Exercise Exercise { get; }

    [ObservableProperty] private bool _isSelected;

    public ExercisePickerItem(Exercise exercise) => Exercise = exercise;

    public string Name => Exercise.Name;

    /// <summary>"Pecho · con vídeo" / "Pecho · material: barra".</summary>
    public string Detail
    {
        get
        {
            if (Exercise.Media.Any(m => m.Kind == MediaKind.Video)) return $"{Exercise.MuscleGroup} · con vídeo";
            if (!string.IsNullOrWhiteSpace(Exercise.Equipment)) return $"{Exercise.MuscleGroup} · {Exercise.Equipment}";
            return Exercise.MuscleGroup;
        }
    }
}
