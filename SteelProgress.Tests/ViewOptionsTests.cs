using SteelProgress.Core.Services;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

public class ViewOptionsTests
{
    [Fact]
    public void SimpleViewHidesAdvancedEditingByDefault()
    {
        var o = ViewOptions.For(new UserProfile());

        Assert.False(o.IsTrainer);
        Assert.False(o.EditIntensity);
        Assert.False(o.EditTempoAndRest);
        Assert.False(o.EditAdvancedStructure);
        Assert.False(o.EditRepRanges);
        Assert.False(o.EditDetailedNotes);
        Assert.False(o.EditExerciseDetails);
        Assert.False(o.ShowClients);
        Assert.True(o.ShowOwnTraining);
    }

    [Fact]
    public void SimpleViewShowsOnlyActivatedBlocks()
    {
        var o = ViewOptions.For(new UserProfile { SimpleViewBlocks = SimpleViewBlocks.Intensity | SimpleViewBlocks.RepRanges });

        Assert.True(o.EditIntensity);
        Assert.True(o.EditRepRanges);
        Assert.False(o.EditTempoAndRest);
        Assert.False(o.EditAdvancedStructure);
        Assert.False(o.ShowClients);
    }

    [Fact]
    public void TrainerViewShowsEverythingRegardlessOfBlocks()
    {
        var o = ViewOptions.For(new UserProfile { ViewMode = ProfileViewMode.Trainer });

        Assert.True(o.EditIntensity && o.EditTempoAndRest && o.EditAdvancedStructure && o.EditRepRanges && o.EditDetailedNotes);
        Assert.True(o.EditExerciseDetails);
        Assert.True(o.ShowClients);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void TrainerSeesOwnTrainingOnlyIfTrainsSelf(bool trainsSelf, bool expected)
    {
        var o = ViewOptions.For(new UserProfile { ViewMode = ProfileViewMode.Trainer, TrainsSelf = trainsSelf });
        Assert.Equal(expected, o.ShowOwnTraining);
    }

    [Fact]
    public void ServiceRaisesChangedOnlyWhenOptionsChange()
    {
        var service = new ViewOptionsService();
        var raised = 0;
        service.Changed += () => raised++;

        service.Apply(new UserProfile());          // igual que el valor por defecto
        service.Apply(new UserProfile { ViewMode = ProfileViewMode.Trainer });
        service.Apply(new UserProfile { ViewMode = ProfileViewMode.Trainer });

        Assert.Equal(1, raised);
        Assert.True(service.Current.IsTrainer);
    }
}

public class PrescriptionFormatterTests
{
    [Fact]
    public void FormatsFullPrescription()
    {
        var p = new RoutineDayExercise
        {
            TargetSets = 4, TargetReps = 6, TargetRepsMax = 8, TargetWeight = 72.5,
            TargetRpe = 8, Tempo = "3-1-1-0", RestSeconds = 90, WarmupSets = 2
        };
        Assert.Equal("4 × 6–8 · 72,5 kg · RPE 8 · 3-1-1-0 · 1:30 · +2 calent.", PrescriptionFormatter.Format(p));
    }

    [Fact]
    public void FormatsLegacyPrescriptionLikeBefore()
    {
        // Las rutinas antiguas solo tienen series y repeticiones
        Assert.Equal("3 × 10", PrescriptionFormatter.Format(new RoutineDayExercise { TargetSets = 3, TargetReps = 10 }));
    }

    [Fact]
    public void EmptyPrescriptionGivesEmptyText() =>
        Assert.Equal("", PrescriptionFormatter.Format(new RoutineDayExercise()));

    [Theory]
    [InlineData(6, 8, "6–8")]
    [InlineData(8, 8, "8")]
    [InlineData(8, null, "8")]
    [InlineData(null, 12, "12")]
    [InlineData(10, 6, "10")]
    public void FormatsReps(int? min, int? max, string expected) =>
        Assert.Equal(expected, PrescriptionFormatter.FormatReps(min, max));

    [Theory]
    [InlineData(45, "45 s")]
    [InlineData(60, "1 min")]
    [InlineData(90, "1:30")]
    [InlineData(120, "2 min")]
    [InlineData(150, "2:30")]
    public void FormatsRest(int seconds, string expected) =>
        Assert.Equal(expected, PrescriptionFormatter.FormatRest(seconds));

    [Fact]
    public void UsesPercentOrRirWhenThereIsNoWeightOrRpe()
    {
        var p = new RoutineDayExercise { TargetSets = 5, TargetReps = 5, TargetPercent1RM = 80, TargetRir = 2 };
        Assert.Equal("5 × 5 · 80 % 1RM · RIR 2", PrescriptionFormatter.Format(p));
    }
}
