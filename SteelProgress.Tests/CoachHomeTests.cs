using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Las filas de «Hoy con tus clientes» en el Inicio de un perfil de entrenador
public class CoachHomeTests
{
    private static readonly DateTime Today = new(2026, 10, 30);

    private static ClientSummary Summary(string? routine, DateTime? last, int[]? done = null, int? lastDay = null)
    {
        var days = new List<RoutineDay>
        {
            new() { Id = 1, Name = "Día 1", Order = 1 },
            new() { Id = 2, Name = "Día 2", Order = 2 }
        };
        var week = routine is null ? null : new RoutineWeek(days, done ?? Array.Empty<int>(), lastDay);
        return new ClientSummary(new Client { Name = "Marta" }, 0, last is null ? 0 : 1, last, routine, week);
    }

    private static (string, string?) LineAndChip(CoachClientRow row) => (row.Line, row.Chip);

    [Fact]
    public void SinRutina_PideCrearla() =>
        Assert.Equal(("Sin rutina activa", "Crear rutina"), LineAndChip(CoachHome.Row(Summary(null, null), Today)));

    [Fact]
    public void SinSesiones_PorEmpezar() =>
        Assert.Equal(("Le toca Día 1 · Fuerza", "Por empezar"), LineAndChip(CoachHome.Row(Summary("Fuerza", null), Today)));

    [Fact]
    public void EntrenoHoy_HechoHoy() =>
        Assert.Equal("Hecho hoy", CoachHome.Row(Summary("Fuerza", Today.AddHours(9), new[] { 1 }, 1), Today).Chip);

    [Fact]
    public void HaceTresDiasYQuedaDia_TocaHoy()
    {
        var row = CoachHome.Row(Summary("Fuerza", Today.AddDays(-3), new[] { 1 }, 1), Today);
        Assert.Equal(("Le toca Día 2 · Fuerza", "Toca hoy"), LineAndChip(row));
        Assert.True(row.ChipIsGo);
    }

    [Fact]
    public void AyerEntreno_SinEtiqueta() =>
        Assert.Null(CoachHome.Row(Summary("Fuerza", Today.AddDays(-1), new[] { 1 }, 1), Today).Chip);

    [Fact]
    public void SieteDiasSinSesiones_SinNoticias()
    {
        var summary = Summary("Fuerza", Today.AddDays(-7), lastDay: 1);
        var row = CoachHome.Row(summary, Today);
        Assert.Equal("Sin noticias", row.Chip);
        Assert.True(row.ChipIsWarning);
        Assert.Equal("Marta: 7 días sin sesiones", Assert.Single(CoachHome.Pending(new[] { summary }, Today)).Title);
    }

    [Fact]
    public void SemanaHecha_NoTocaHoy()
    {
        var row = CoachHome.Row(Summary("Fuerza", Today.AddDays(-3), new[] { 1, 2 }, 2), Today);
        Assert.Equal("Semana hecha · Fuerza", row.Line);
        Assert.Null(row.Chip);
    }
}
