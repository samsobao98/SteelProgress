using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// 1RM estimado: solo con series de 1 a 10 repeticiones (ver docs/INVESTIGACION_DATOS_PERFIL.md)
public class OneRepMaxTests
{
    [Fact]
    public void EstimatesOnlyFromOneToTenReps()
    {
        Assert.Equal(100, WorkoutRepository.EstimatedOneRepMax(100, 1));
        Assert.Equal(100 * (1 + 10 / 30.0), WorkoutRepository.EstimatedOneRepMax(100, 10), 6);
        Assert.Equal(0, WorkoutRepository.EstimatedOneRepMax(100, 11));
        Assert.Equal(0, WorkoutRepository.EstimatedOneRepMax(60, 20));
        Assert.Equal(0, WorkoutRepository.EstimatedOneRepMax(100, 0));
    }

    [Fact]
    public void HighRepSessionsKeepBestSetButLeaveTheChart()
    {
        var today = new DateTime(2026, 10, 30);
        LoggedSet Set(int session, int daysAgo, double kg, int reps) =>
            new(session, today.AddDays(-daysAgo), 1, "Sentadilla", session * 10 + reps, kg, reps);

        // Una sesión solo con series de 20: antes inflaba el 1RM (60 × 20 → 100 kg)
        var sets = new[] { Set(1, 10, 80, 5), Set(2, 5, 60, 20), Set(3, 1, 82.5, 5) };
        var progress = TrainingStats.Exercises(sets).Single();

        Assert.Equal(82.5 * (1 + 5 / 30.0), progress.BestE1rm, 6);
        Assert.Equal(0, progress.Points[1].BestE1rm);
        Assert.Equal(60, progress.Points[1].MaxWeight);
        Assert.Equal(2, ExerciseDetailViewModel.ChartPoints(progress, today, out _).Count);
    }

    [Fact]
    public void OnlyHighRepsShowsBestSetWithoutEstimateOrChart()
    {
        var today = new DateTime(2026, 10, 30);
        var sets = new[]
        {
            new LoggedSet(1, today.AddDays(-3), 1, "Zancadas", 1, 20, 15),
            new LoggedSet(2, today.AddDays(-1), 1, "Zancadas", 2, 22.5, 15)
        };
        var progress = TrainingStats.Exercises(sets).Single();
        var vm = new ExerciseDetailViewModel(new Exercise { Id = 1, Name = "Zancadas", MuscleGroup = "Piernas" }, progress, today);

        Assert.Equal(("22,5×15", "—", "1RM est. (series de hasta 10 reps)"), (vm.BestSetText, vm.E1rmText, vm.E1rmCaption));
        Assert.False(vm.HasChart);
        Assert.Equal("", new ExerciseProgressItem(progress, today).E1rmText);
    }
}
