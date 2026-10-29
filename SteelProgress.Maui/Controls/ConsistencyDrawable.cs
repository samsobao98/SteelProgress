using SteelProgress.Core.Services;

namespace SteelProgress.Maui.Controls;

// Calendario de constancia de Progreso: una columna por semana (lunes arriba) y un cuadro por día,
// más claro cuanto más volumen. Hoy lleva contorno; los días que aún no han llegado no se pintan.
public class ConsistencyDrawable : IDrawable
{
    // Nivel 0 = sin entreno (SpCardHi); 1–4 = del acero apagado al SpSteel2
    public static readonly Color[] Levels =
    [
        Color.FromArgb("#262A37"),
        Color.FromArgb("#3A4670"),
        Color.FromArgb("#4F67A8"),
        Color.FromArgb("#6E8FDB"),
        Color.FromArgb("#B3B7EC")
    ];

    public IReadOnlyList<CalendarDay> Days { get; set; } = Array.Empty<CalendarDay>();
    public float Gap { get; set; } = 4;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Days.Count == 0) return;
        var weeks = (Days.Count + 6) / 7;
        var cell = Math.Min((dirtyRect.Width - Gap * (weeks - 1)) / weeks, (dirtyRect.Height - Gap * 6) / 7);
        var left = dirtyRect.X + (dirtyRect.Width - (cell * weeks + Gap * (weeks - 1))) / 2;

        for (var i = 0; i < Days.Count; i++)
        {
            var day = Days[i];
            if (day.IsFuture) continue;
            var x = left + i / 7 * (cell + Gap);
            var y = dirtyRect.Y + i % 7 * (cell + Gap);
            canvas.FillColor = Levels[Math.Clamp(day.Level, 0, 4)];
            canvas.FillRoundedRectangle(x, y, cell, cell, 3);
            if (day.IsToday)
            {
                canvas.StrokeColor = Color.FromArgb("#E8EAF2");
                canvas.StrokeSize = 1.5f;
                canvas.DrawRoundedRectangle(x + 0.75f, y + 0.75f, cell - 1.5f, cell - 1.5f, 3);
            }
        }
    }
}
