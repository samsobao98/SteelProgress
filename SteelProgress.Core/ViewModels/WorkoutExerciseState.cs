using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using SteelProgress.Core.Services;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Core.ViewModels;

/// <summary>
/// Un ejercicio dentro del entreno en curso: lo que pide la rutina, lo que se hizo
/// la última vez y las series ya registradas hoy.
/// </summary>
public partial class WorkoutExerciseState : ObservableObject
{
    internal static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    public WorkoutExercise Entry { get; }
    public RoutineDayExercise? Prescription { get; }
    public ExerciseHistory History { get; }

    /// <summary>Posición en el día, empezando por 1.</summary>
    public int Number { get; }

    public string Name => Entry.Exercise?.Name ?? "Ejercicio";
    public Exercise? Exercise => Entry.Exercise;

    public ObservableCollection<DoneSet> Sets { get; } = new();

    [ObservableProperty] private bool _isCurrent;

    public WorkoutExerciseState(WorkoutExercise entry, RoutineDayExercise? prescription, ExerciseHistory history, int number)
    {
        Entry = entry;
        Prescription = prescription;
        History = history;
        Number = number;
    }

    public int? TargetSets => Prescription?.TargetSets;
    public int DoneCount => Sets.Count;
    public bool IsDone => TargetSets is int target && DoneCount >= target;
    public int MissingSets => TargetSets is int target ? Math.Max(0, target - DoneCount) : 0;

    public string PrescriptionText => Prescription is null ? "" : PrescriptionFormatter.Format(Prescription);
    public string PrescriptionNotes => Prescription?.Notes ?? "";

    /// <summary>Objetivo de series y reps para el chip: "Objetivo 4 × 6–8".</summary>
    public string TargetText
    {
        get
        {
            if (Prescription is null) return "";
            var reps = PrescriptionFormatter.FormatReps(Prescription.TargetReps, Prescription.TargetRepsMax);
            if (TargetSets is int sets && reps is not null) return $"Objetivo {sets} × {reps}";
            if (TargetSets is int onlySets) return $"Objetivo {onlySets} series";
            if (reps is not null) return $"Objetivo {reps} reps";
            return "";
        }
    }

    /// <summary>Intensidad para el chip: "RPE 8", "RIR 2", "75 % 1RM".</summary>
    public string IntensityText
    {
        get
        {
            if (Prescription?.TargetRpe is double rpe) return $"RPE {rpe.ToString("0.##", Es)}";
            if (Prescription?.TargetRir is int rir) return $"RIR {rir}";
            if (Prescription?.TargetPercent1RM is double pct) return $"{pct.ToString("0.##", Es)} % 1RM";
            return "";
        }
    }

    /// <summary>"La última vez: 70 kg × 7" (la mejor serie de la última sesión).</summary>
    public string LastTimeText
    {
        get
        {
            var best = History.LastSessionSets
                .OrderByDescending(s => WorkoutRepository.EstimatedOneRepMax(s.Weight, s.Reps))
                .FirstOrDefault();
            return best is null ? "" : $"La última vez: {FormatSet(best.Weight, best.Reps)}";
        }
    }

    /// <summary>Peso y reps con los que empezar la serie <paramref name="setNumber"/>.</summary>
    public (double weight, int reps) Suggestion(int setNumber)
    {
        // Lo mismo que en la serie equivalente de la última vez; si no, lo prescrito
        var last = History.LastSessionSets;
        if (last.Count > 0)
        {
            var same = last[Math.Min(setNumber - 1, last.Count - 1)];
            return (same.Weight, same.Reps);
        }
        return (Prescription?.TargetWeight ?? 0, Prescription?.TargetReps ?? 0);
    }

    internal void NotifyProgress()
    {
        OnPropertyChanged(nameof(DoneCount));
        OnPropertyChanged(nameof(IsDone));
        OnPropertyChanged(nameof(MissingSets));
    }

    public static string FormatSet(double weight, int reps) =>
        weight > 0 ? $"{FormatKg(weight)} kg × {reps}" : $"{reps} reps";

    public static string FormatKg(double weight) => weight.ToString("0.##", Es);
}

/// <summary>Serie registrada hoy, con su insignia de récord.</summary>
public partial class DoneSet : ObservableObject
{
    public WorkoutSet Set { get; }
    public int Number { get; }

    [ObservableProperty] private bool _isRecord;

    public DoneSet(WorkoutSet set, int number, bool isRecord)
    {
        Set = set;
        Number = number;
        _isRecord = isRecord;
    }

    public string Text => WorkoutExerciseState.FormatSet(Set.Weight, Set.Reps);
    public string RpeText => Set.Rpe is double rpe ? $"RPE {rpe.ToString("0.##", WorkoutExerciseState.Es)}" : "";
    public double EstimatedOneRepMax => WorkoutRepository.EstimatedOneRepMax(Set.Weight, Set.Reps);
}
