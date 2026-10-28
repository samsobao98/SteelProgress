using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Pantalla de lista de rutinas: la activa (la última entrenada) en una placa con su
/// semana, el resto en tarjetas. Orden automático o elegido a mano, crear y eliminar
/// con deshacer. La gestión de días y ejercicios vive en <see cref="RoutineDetailViewModel"/>.
/// </summary>
public partial class RoutineViewModel : ObservableObject
{
    private readonly RoutineRepository _repository;
    private readonly TimeProvider _clock;

    // En la app todos los repositorios comparten un DbContext: una operación cada vez
    private readonly SemaphoreSlim _dbLock = new(1, 1);

    public ObservableCollection<RoutineListItem> Items { get; } = new();

    [ObservableProperty] private bool _isLoaded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotReordering))]
    private bool _isReordering;

    [ObservableProperty] private bool _isManualOrder;

    // ── Overlay de creación ────────────────────────────────────────────────
    [ObservableProperty] private string _newName = string.Empty;
    [ObservableProperty] private string _newNotes = string.Empty;

    public bool HasRoutines => Items.Count > 0;
    public bool IsNotReordering => !IsReordering;

    public string HeaderText
    {
        get
        {
            var n = Items.Count == 1 ? "1 rutina" : $"{Items.Count} rutinas";
            return Items.Any(i => i.IsActive) ? $"{n} · 1 activa" : n;
        }
    }

    /// <summary>Se dispara tras crear una rutina con éxito (para cerrar el sheet).</summary>
    public event Action? RoutineCreated;

    public RoutineViewModel(RoutineRepository repository, TimeProvider? clock = null)
    {
        _repository = repository;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task RefreshAsync()
    {
        await _dbLock.WaitAsync();
        try { await LoadAsync(); }
        finally { _dbLock.Release(); }
    }

    private async Task LoadAsync()
    {
        var today = _clock.GetLocalNow().DateTime;
        var summaries = await _repository.GetRoutineSummariesAsync();

        // Una rutina pendiente de borrar no vuelve a aparecer mientras se puede deshacer
        if (PendingDelete is { } pending)
            summaries.RemoveAll(s => s.Routine.Id == pending.Routine.Id);

        var active = summaries.Where(s => s.LastTrained is not null).MaxBy(s => s.LastTrained);

        RoutineListItem? activeItem = null;
        if (active is not null)
        {
            var week = await _repository.GetRoutineWeekAsync(active.Routine.Id, RoutineSchedule.WeekStart(today));
            var marks = week.Days.Select(d => new WeekDayMark(d.Name, week.DoneThisWeek.Contains(d.Id))).ToList();
            activeItem = new RoutineListItem(active, today, true, marks, RoutineSchedule.NextDay(week)?.Name ?? "");
        }

        IsManualOrder = summaries.Any(s => s.Routine.SortOrder is not null);

        Items.Clear();
        foreach (var s in Sort(summaries, active?.Routine.Id))
            Items.Add(ReferenceEquals(s, active) ? activeItem! : new RoutineListItem(s, today));

        IsLoaded = true;
        OnPropertyChanged(nameof(HasRoutines));
        OnPropertyChanged(nameof(HeaderText));
    }

    /// <summary>
    /// Orden de la lista. Sin orden manual: la activa primero, luego por última vez entrenada
    /// y al final las nunca entrenadas por nombre. Con orden manual: el elegido, y las rutinas
    /// sin posición (nuevas o importadas) al final por nombre.
    /// </summary>
    public static IReadOnlyList<RoutineSummary> Sort(IEnumerable<RoutineSummary> summaries, int? activeId)
    {
        var list = summaries.ToList();
        if (list.Any(s => s.Routine.SortOrder is not null))
        {
            return list
                .OrderBy(s => s.Routine.SortOrder is null)
                .ThenBy(s => s.Routine.SortOrder)
                .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
        }

        return list
            .OrderByDescending(s => s.Routine.Id == activeId)
            .ThenByDescending(s => s.LastTrained ?? DateTime.MinValue)
            .ThenBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
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

        await _dbLock.WaitAsync();
        try
        {
            // Con orden manual, la nueva va al final
            if (IsManualOrder)
                routine.SortOrder = Items.Select(i => i.Routine.SortOrder ?? 0).DefaultIfEmpty(0).Max() + 1;

            await _repository.AddAsync(routine);
            ClearForm();
            await LoadAsync();
        }
        finally { _dbLock.Release(); }

        RoutineCreated?.Invoke();
    }

    // ── Ordenar ───────────────────────────────────────────────────────────

    [RelayCommand]
    private void StartReorder() => IsReordering = true;

    /// <summary>Guarda el orden actual de <see cref="Items"/> (tras arrastrar) como orden manual.</summary>
    [RelayCommand]
    private async Task FinishReorder()
    {
        IsReordering = false;
        await _dbLock.WaitAsync();
        try
        {
            await _repository.SetSortOrderAsync(Items.Select(i => i.Routine).ToList(), manual: true);
            await LoadAsync();
        }
        finally { _dbLock.Release(); }
    }

    [RelayCommand]
    private async Task ResetOrder()
    {
        IsReordering = false;
        await _dbLock.WaitAsync();
        try
        {
            await _repository.SetSortOrderAsync(Items.Select(i => i.Routine).ToList(), manual: false);
            await LoadAsync();
        }
        finally { _dbLock.Release(); }
        NotificationService.Info("Orden automático: la activa primero y el resto por última vez entrenada.");
    }

    // ── Eliminar con deshacer ─────────────────────────────────────────────

    // Borrar una rutina borra en cascada sus días y su historial de entrenos: el borrado real
    // se retrasa hasta que caduca el aviso de deshacer o se sale de la pantalla
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPendingDelete), nameof(PendingDeleteText))]
    private RoutineListItem? _pendingDelete;

    private int _pendingIndex;

    public bool HasPendingDelete => PendingDelete is not null;
    public string PendingDeleteText => PendingDelete is null ? "" : $"«{PendingDelete.Name}» eliminada";

    /// <summary>Entrenos que se perderían al borrar la rutina (para avisar antes).</summary>
    public async Task<int> CountSessionsAsync(RoutineListItem item)
    {
        await _dbLock.WaitAsync();
        try { return await _repository.CountSessionsOfRoutineAsync(item.Routine.Id); }
        finally { _dbLock.Release(); }
    }

    public async Task DeleteWithUndoAsync(RoutineListItem item)
    {
        await CommitPendingDeleteAsync(); // solo una pendiente a la vez

        _pendingIndex = Items.IndexOf(item);
        if (_pendingIndex < 0) return;
        Items.RemoveAt(_pendingIndex);
        PendingDelete = item;
        OnPropertyChanged(nameof(HasRoutines));
        OnPropertyChanged(nameof(HeaderText));
    }

    [RelayCommand]
    private void UndoDelete()
    {
        if (PendingDelete is not { } item) return;
        Items.Insert(Math.Min(_pendingIndex, Items.Count), item);
        PendingDelete = null;
        OnPropertyChanged(nameof(HasRoutines));
        OnPropertyChanged(nameof(HeaderText));
    }

    public async Task CommitPendingDeleteAsync()
    {
        if (PendingDelete is not { } item) return;
        PendingDelete = null;

        await _dbLock.WaitAsync();
        try { await _repository.DeleteAsync(item.Routine); }
        finally { _dbLock.Release(); }
    }
}
