namespace SteelProgress.Core.Services;

/// <summary>
/// Los días de la semana que entrena un cliente (<see cref="Domain.Entities.Client.TrainingDays"/>):
/// un bit por día, lunes el primero.
/// </summary>
public static class TrainingDays
{
    /// <summary>La semana de lunes a domingo, con la letra de cada día.</summary>
    public static readonly IReadOnlyList<(DayOfWeek Day, string Letter)> Week =
    [
        (DayOfWeek.Monday, "L"), (DayOfWeek.Tuesday, "M"), (DayOfWeek.Wednesday, "X"), (DayOfWeek.Thursday, "J"),
        (DayOfWeek.Friday, "V"), (DayOfWeek.Saturday, "S"), (DayOfWeek.Sunday, "D")
    ];

    private static readonly string[] Names = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    public static string Name(DayOfWeek day) => Names[(int)day];

    public static int Bit(DayOfWeek day) => 1 << (((int)day + 6) % 7);

    public static bool Includes(int days, DateTime date) => (days & Bit(date.DayOfWeek)) != 0;

    /// <summary>Cuántos de sus días han pasado entre la última sesión y hoy, sin contar ninguno de los dos.</summary>
    public static int MissedBetween(int days, DateTime lastSession, DateTime today)
    {
        var missed = 0;
        for (var d = lastSession.Date.AddDays(1); d < today.Date; d = d.AddDays(1))
            if (Includes(days, d)) missed++;
        return missed;
    }
}
