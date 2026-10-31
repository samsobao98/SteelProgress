using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Repositories;

namespace SteelProgress.Tests;

// Series por grupo muscular: esta semana y la media de las cuatro anteriores
public class MuscleVolumeTests
{
    // Viernes; la semana empieza el lunes 26/10
    private static readonly DateTime Today = new(2026, 10, 30);
    private int _id;

    private LoggedSet Set(int daysAgo, string? muscle) =>
        new(1, Today.AddDays(-daysAgo), 1, "Ej", ++_id, 50, 8, null, muscle);

    [Fact]
    public void CountsThisWeekAgainstTheFourBefore()
    {
        var sets = new List<LoggedSet>();
        sets.AddRange(Enumerable.Range(0, 6).Select(_ => Set(1, "Pecho")));      // esta semana: 6
        sets.AddRange(Enumerable.Range(0, 8).Select(_ => Set(10, "Pecho")));     // semana anterior: 8
        sets.AddRange(Enumerable.Range(0, 4).Select(_ => Set(20, "Espalda")));   // hace 3 semanas: 4
        sets.Add(Set(60, "Pecho"));                                              // fuera de las 4 semanas
        sets.Add(Set(2, "Cardio"));                                              // no es un músculo
        sets.Add(Set(2, null));

        var v = TrainingStats.SetsByMuscle(sets, Today);

        Assert.Equal(new[] { "Pecho", "Espalda" }, v.Select(m => m.Muscle));
        Assert.Equal((6, 2.0), (v[0].ThisWeek, v[0].PreviousAverage));
        Assert.Equal((0, 1.0), (v[1].ThisWeek, v[1].PreviousAverage));
    }

    [Fact]
    public void RowTextAndBar()
    {
        var item = new MuscleVolumeItem(new MuscleVolume("Pecho", 6, 2.5), top: 12);
        Assert.Equal(("6", "media 2,5", 0.5), (item.ThisWeek, item.AverageText, item.Fill));
    }
}
