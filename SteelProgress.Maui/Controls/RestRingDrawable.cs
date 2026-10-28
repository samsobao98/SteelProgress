namespace SteelProgress.Maui.Controls;

// Anillo del descanso: pista apagada y arco que se vacía desde arriba en sentido horario
public class RestRingDrawable : IDrawable
{
    /// <summary>Fracción que queda (1 = lleno, 0 = vacío).</summary>
    public double Progress { get; set; } = 1;

    public Color TrackColor { get; set; } = Color.FromArgb("#2C2D42");
    public Color ArcColor { get; set; } = Color.FromArgb("#94E2D5");
    public float Thickness { get; set; } = 16;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var size = Math.Min(dirtyRect.Width, dirtyRect.Height) - Thickness;
        var x = dirtyRect.Center.X - size / 2;
        var y = dirtyRect.Center.Y - size / 2;

        canvas.StrokeSize = Thickness;
        canvas.StrokeLineCap = LineCap.Round;

        canvas.StrokeColor = TrackColor;
        canvas.DrawEllipse(x, y, size, size);

        var p = (float)Math.Clamp(Progress, 0, 1);
        if (p <= 0) return;

        canvas.StrokeColor = ArcColor;
        if (p >= 0.999f)
        {
            canvas.DrawEllipse(x, y, size, size);
            return;
        }
        // Ángulos en grados, 0 = derecha, positivos en sentido antihorario: 90 = arriba
        canvas.DrawArc(x, y, size, size, 90, 90 - 360 * p, true, false);
    }
}
