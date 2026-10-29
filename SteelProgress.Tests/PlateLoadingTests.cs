using SteelProgress.Core.Services;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// «Tu gimnasio» y cómo cargar la barra
public class PlateLoadingTests
{
    private static readonly double[] Standard = { 25, 20, 15, 10, 5, 2.5 };

    [Theory]
    [InlineData(100, new[] { 25.0, 15 })]
    [InlineData(72.5, new[] { 25.0, 1.25 })]          // con 1,25 en el gimnasio
    [InlineData(60, new[] { 20.0 })]
    [InlineData(142.5, new[] { 25.0, 25, 10, 1.25 })]
    public void LoadsEachSideWithTheFewestPlatesHeaviestFirst(double total, double[] perSide)
    {
        var load = PlateLoading.For(total, 20, GymSetup.AllPlates);
        Assert.Equal(perSide, load.PerSide);
        Assert.True(load.IsExact);
        Assert.Equal(total, load.Achieved);
    }

    [Fact]
    public void WithoutSmallPlatesGivesTheClosestBelow()
    {
        var load = PlateLoading.For(72.5, 20, Standard);   // 26,25 por lado: sin discos de 1,25 no sale
        Assert.False(load.IsExact);
        Assert.Equal(new[] { 25.0 }, load.PerSide);
        Assert.Equal(70, load.Achieved);
    }

    [Fact]
    public void FindsCombinationsThatGreedyWouldMiss()
    {
        // Solo discos de 20 y 15: 30 por lado son dos de 15, no uno de 20 y nada más
        var load = PlateLoading.For(80, 20, new[] { 20.0, 15 });
        Assert.Equal(new[] { 15.0, 15 }, load.PerSide);
        Assert.True(load.IsExact);
    }

    [Fact]
    public void BarAloneOrLess()
    {
        Assert.Empty(PlateLoading.For(20, 20, Standard).PerSide);
        Assert.True(PlateLoading.For(20, 20, Standard).IsExact);

        var light = PlateLoading.For(15, 20, Standard);
        Assert.True(light.BelowBase);
        Assert.Empty(light.PerSide);
    }

    [Fact]
    public void PlateMachineWithoutCarriageWeightUsesOnlyPlates()
    {
        var load = PlateLoading.For(120, 0, Standard);
        Assert.Equal(new[] { 25.0, 25, 10 }, load.PerSide);
    }

    [Fact]
    public void GymSetupRoundTripsAndFallsBackToDefaults()
    {
        Assert.Equal("25;20;15;10;5;2.5", GymSetup.Default.PlatesText);
        Assert.Equal(20, GymSetup.Default.BarWeight);

        var gym = GymSetup.From("2.5;25; 1.25 ;x;20", 15);
        Assert.Equal(new[] { 25, 20, 2.5, 1.25 }, gym.Plates);
        Assert.Equal(15, gym.BarWeight);

        Assert.Equal(GymSetup.Default, GymSetup.From(null, null));
        Assert.Equal(GymSetup.Default, GymSetup.From("", null));
        // Igualdad por valor: aplicar el mismo perfil dos veces no avisa de cambios
        Assert.Equal(ViewOptions.For(new UserProfile { GymPlates = "20;10" }), ViewOptions.For(new UserProfile { GymPlates = "20;10" }));
    }
}
