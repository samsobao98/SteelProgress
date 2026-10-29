namespace SteelProgress.Maui.Controls;

// Anillo del objetivo semanal de Progreso: pista apagada y arco con el degradado acero
// (SpSteel1 → SpSteel3) que se llena desde arriba en sentido horario
public class GoalRingDrawable : IDrawable
{
    private static readonly Color Start = Color.FromArgb("#9DB4E6");
    private static readonly Color End = Color.FromArgb("#C9B9F2");

    /// <summary>Fracción cumplida (0 a 1).</summary>
    public double Progress { get; set; }

    public Color TrackColor { get; set; } = Color.FromArgb("#262A37");
    public float Thickness { get; set; } = 12;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var size = Math.Min(dirtyRect.Width, dirtyRect.Height) - Thickness;
        var x = dirtyRect.Center.X - size / 2;
        var y = dirtyRect.Center.Y - size / 2;

        canvas.StrokeSize = Thickness;
        canvas.StrokeColor = TrackColor;
        canvas.DrawEllipse(x, y, size, size);

        var p = (float)Math.Clamp(Progress, 0, 1);
        if (p <= 0) return;

        // ICanvas no admite degradados en el trazo: el arco se dibuja en tramos de color interpolado
        const int segments = 48;
        var count = Math.Max(1, (int)Math.Ceiling(segments * p));
        var sweep = 360f * p / count;
        canvas.StrokeLineCap = LineCap.Butt;
        for (var i = 0; i < count; i++)
        {
            var t = count == 1 ? 0 : (float)i / (count - 1);
            canvas.StrokeColor = Start.Mix(End, t);
            // Ángulos en grados, 0 = derecha, positivos en sentido antihorario: 90 = arriba.
            // Cada tramo se solapa un poco con el siguiente para que no se vean juntas
            var from = 90 - sweep * i;
            canvas.DrawArc(x, y, size, size, from, from - sweep - (i < count - 1 ? 1 : 0), true, false);
        }

        // Extremos redondeados
        canvas.FillColor = Start;
        var r = Thickness / 2;
        canvas.FillCircle(dirtyRect.Center.X, y, r);
        var end = (90 - 360 * p) * MathF.PI / 180;
        canvas.FillColor = p >= 0.999f ? Start : End;
        canvas.FillCircle(dirtyRect.Center.X + size / 2 * MathF.Cos(end), dirtyRect.Center.Y - size / 2 * MathF.Sin(end), r);
    }
}

internal static class ColorLerp
{
    public static Color Mix(this Color a, Color b, float t) =>
        new(a.Red + (b.Red - a.Red) * t, a.Green + (b.Green - a.Green) * t, a.Blue + (b.Blue - a.Blue) * t);
}
