using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Data.Repositories;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Tests;

// Las filas de «Hoy con tus clientes» en el Inicio de un perfil de entrenador
public class CoachHomeTests
{
    private static readonly DateTime Today = new(2026, 10, 30);

    private static ClientSummary Summary(string? routine, DateTime? last, int[]? done = null, int? lastDay = null, int trainingDays = 0)
    {
        var days = new List<RoutineDay>
        {
            new() { Id = 1, Name = "Día 1", Order = 1 },
            new() { Id = 2, Name = "Día 2", Order = 2 }
        };
        var week = routine is null ? null : new RoutineWeek(days, done ?? Array.Empty<int>(), lastDay);
        return new ClientSummary(new Client { Name = "Marta", TrainingDays = trainingDays }, 0, last is null ? 0 : 1, last, routine, week);
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

    // Hoy (30/10/2026) es viernes
    private static readonly int LunesMiercolesViernes =
        TrainingDays.Bit(DayOfWeek.Monday) | TrainingDays.Bit(DayOfWeek.Wednesday) | TrainingDays.Bit(DayOfWeek.Friday);

    [Fact]
    public void ConDias_TocaHoySoloEnSusDias()
    {
        // Entrenó ayer jueves, pero hoy viernes es uno de sus días
        Assert.Equal("Toca hoy", CoachHome.Row(Summary("Fuerza", Today.AddDays(-1), new[] { 1 }, 1, LunesMiercolesViernes), Today).Chip);
        // Hace tres días, pero los viernes no entrena
        var martesJueves = TrainingDays.Bit(DayOfWeek.Tuesday) | TrainingDays.Bit(DayOfWeek.Thursday);
        Assert.Null(CoachHome.Row(Summary("Fuerza", Today.AddDays(-3), new[] { 1 }, 1, martesJueves), Today).Chip);
    }

    [Fact]
    public void ConDias_SinNoticiasAlSaltarseDosSeguidos()
    {
        // Último lunes 26: se ha saltado el miércoles; hoy viernes aún puede venir
        Assert.NotEqual("Sin noticias", CoachHome.Row(Summary("Fuerza", Today.AddDays(-4), new[] { 1 }, 1, LunesMiercolesViernes), Today).Chip);
        // Último miércoles 21: lunes 26 y miércoles 28 sin sesión
        var summary = Summary("Fuerza", Today.AddDays(-9), new[] { 1 }, 1, LunesMiercolesViernes);
        Assert.Equal("Sin noticias", CoachHome.Row(summary, Today).Chip);
        Assert.Single(CoachHome.Pending(new[] { summary }, Today));
    }

    [Fact]
    public void UnDiaPorSemana_NoAvisaALosSieteDias()
    {
        var soloLunes = TrainingDays.Bit(DayOfWeek.Monday);
        // Último lunes 19: solo se ha saltado el 26
        var summary = Summary("Fuerza", Today.AddDays(-11), new[] { 1 }, 1, soloLunes);
        Assert.NotEqual("Sin noticias", CoachHome.Row(summary, Today).Chip);
        Assert.Empty(CoachHome.Pending(new[] { summary }, Today));
        Assert.False(ClientsViewModel.ToItem(summary, Today).BadgeIsWarning);
    }

    [Fact]
    public void SemanaHecha_NoTocaHoy()
    {
        var row = CoachHome.Row(Summary("Fuerza", Today.AddDays(-3), new[] { 1, 2 }, 2), Today);
        Assert.Equal("Semana hecha · Fuerza", row.Line);
        Assert.Null(row.Chip);
    }
}
