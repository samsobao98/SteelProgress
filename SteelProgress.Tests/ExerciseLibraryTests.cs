using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Rediseño 4 — biblioteca de ejercicios: filas, grupos, filtro de propios y etiquetas
public class ExerciseLibraryTests
{
    private static ExerciseProgress Best(int id, double kg, int reps) =>
        new(id, "x", kg, reps, kg * (1 + reps / 30.0), DateTime.Today, new[] { new ExercisePoint(DateTime.Today, kg, kg) });

    [Fact]
    public void RowShowsEquipmentBestAndOrigin()
    {
        var library = new ExerciseListItem(new Exercise { Id = 1, Name = "Press banca", Equipment = "Barra", IsSystem = true }, Best(1, 72.5, 8));
        Assert.Equal(("Barra · mejor: 72,5 × 8", ""), (library.DetailText, library.OriginText));

        var own = new ExerciseListItem(new Exercise { Id = 2, Name = "Aperturas", Equipment = "Polea" }, null);
        Assert.Equal(("Polea · sin vídeo", "propio"), (own.DetailText, own.OriginText));

        var imported = new ExerciseListItem(new Exercise { Id = 3, Name = "Remo", AuthorName = "Laura" }, null);
        Assert.Equal("de Laura", imported.OriginText);
    }

    [Fact]
    public void OwnExerciseWithMediaDoesNotSayWithoutVideo()
    {
        var e = new Exercise { Name = "Fondos", Equipment = "Peso corporal" };
        e.Media.Add(new ExerciseMedia { Kind = MediaKind.Video, FileName = "a.mp4" });
        Assert.Equal("Peso corporal", new ExerciseListItem(e, null).DetailText);
    }

    [Fact]
    public void GroupsFollowCatalogOrderAndSortByName()
    {
        var items = new[]
        {
            new Exercise { Name = "Remo", MuscleGroup = "Espalda" },
            new Exercise { Name = "Press banca", MuscleGroup = "Pecho" },
            new Exercise { Name = "Aperturas", MuscleGroup = "Pecho" },
            new Exercise { Name = "Raro", MuscleGroup = "" },
        }.Select(e => new ExerciseListItem(e, null));

        var groups = ExerciseGroup.Build(items);

        Assert.Equal(new[] { "PECHO", "ESPALDA", "OTROS" }, groups.Select(g => g.Header));
        Assert.Equal(new[] { "Aperturas", "Press banca" }, groups[0].Select(i => i.Name));
    }

    [Fact]
    public void OwnFilterLeavesOutTheLibrary()
    {
        var all = new[]
        {
            new Exercise { Name = "Press banca", MuscleGroup = "Pecho", IsSystem = true },
            new Exercise { Name = "Mío", MuscleGroup = "Pecho" },
            new Exercise { Name = "De Laura", MuscleGroup = "Espalda", AuthorName = "Laura" },
        };
        Assert.Equal(new[] { "Mío", "De Laura" }, ExerciseCatalog.Filter(all, ExerciseCatalog.OwnFilter, null).Select(e => e.Name));
    }

    [Fact]
    public void SearchFindsTagsWithoutAccents()
    {
        var all = new[] { new Exercise { Name = "Zancada", MuscleGroup = "Cuádriceps", Tags = "unilateral, Casa" } };
        Assert.Single(ExerciseCatalog.Filter(all, null, "casa"));
        Assert.Single(ExerciseCatalog.Filter(all, null, "cuadriceps"));
        Assert.Empty(ExerciseCatalog.Filter(all, null, "barra"));
    }

    [Fact]
    public void TagsAreCleanedWhenSaved()
    {
        Assert.Equal("unilateral, casa", ExerciseCatalog.JoinTags(new[] { " unilateral ,, casa, Casa " }));
        Assert.Null(ExerciseCatalog.JoinTags(new[] { " , " }));
        Assert.Equal(new[] { "a", "b" }, ExerciseCatalog.TagsOf("a, b,"));
    }

    [Fact]
    public void StepsAreNumberedOncePerLineOrSentence()
    {
        Assert.Equal(new[] { "1. Escápulas atrás.", "2. Baja controlado." },
            ExerciseDetailViewModel.StepsOf("1. Escápulas atrás.\n- Baja controlado."));
        Assert.Equal(new[] { "1. Uno.", "2. Dos." }, ExerciseDetailViewModel.StepsOf("Uno. Dos."));
        Assert.Empty(ExerciseDetailViewModel.StepsOf("  "));
    }

    [Fact]
    public void DetailShowsBestE1rmSessionsAndChips()
    {
        var today = new DateTime(2026, 10, 29);
        var points = new[] { new ExercisePoint(today.AddDays(-200), 60, 70), new ExercisePoint(today.AddDays(-20), 70, 85.3), new ExercisePoint(today.AddDays(-2), 72.5, 91.2) };
        var progress = new ExerciseProgress(1, "Press banca", 72.5, 8, 91.2, today.AddDays(-2), points);
        var e = new Exercise { Id = 1, Name = "Press banca", MuscleGroup = "Pecho", SecondaryMuscles = "Tríceps", Equipment = "Barra", Tags = "básico, pecho", IsSystem = true };

        var vm = new ExerciseDetailViewModel(e, progress, today);

        Assert.Equal(("72,5×8", "91 kg", "3", "Sesiones"), (vm.BestSetText, vm.E1rmText, vm.SessionsText, vm.SessionsCaption));
        Assert.Equal(new[] { "Pecho", "Tríceps", "Barra", "básico" }, vm.Chips);
        Assert.False(vm.CanEdit);
        Assert.True(vm.HasChart);
        Assert.Equal(2, ExerciseDetailViewModel.ChartPoints(progress, today, out var caption).Count);
        Assert.Equal("1RM est. · 3 meses", caption);
    }

    [Fact]
    public void NeverTrainedHasNoChartAndOwnCanBeEdited()
    {
        var vm = new ExerciseDetailViewModel(new Exercise { Name = "Mío", MuscleGroup = "Pecho" }, null);
        Assert.False(vm.HasProgress);
        Assert.False(vm.HasChart);
        Assert.True(vm.CanEdit);
        Assert.Equal("propio", vm.OriginText);
    }

    [Fact]
    public void MediaCyclesAndSlowMotionHalvesSpeed()
    {
        var e = new Exercise { Name = "x", MuscleGroup = "Pecho" };
        e.Media.Add(new ExerciseMedia { Kind = MediaKind.Image, Order = 1, FileName = "b.jpg" });
        e.Media.Add(new ExerciseMedia { Kind = MediaKind.Video, Order = 0, FileName = "a.mp4" });
        var vm = new ExerciseDetailViewModel(e, null);

        Assert.Equal(("1 / 2", true), (vm.IndexText, vm.IsVideo));
        vm.NextMediaCommand.Execute(null);
        Assert.Equal(("2 / 2", false), (vm.IndexText, vm.IsVideo));
        vm.NextMediaCommand.Execute(null);
        Assert.Equal(0, vm.MediaIndex);
        vm.ToggleSlowMotionCommand.Execute(null);
        Assert.Equal((0.5, "0,5×"), (vm.Speed, vm.SpeedText));
    }
}
