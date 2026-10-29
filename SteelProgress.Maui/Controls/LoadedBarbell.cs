using SteelProgress.Core.Services;
using SteelProgress.Maui.Converters;

namespace SteelProgress.Maui.Controls;

/// <summary>
/// La barra cargada de Entreno (maqueta «Acero + Discos»): barra, manguitos y los discos de cada lado
/// con su color, del más pesado (dentro) al más ligero (fuera). En la máquina de discos, el carro
/// en el centro en vez de los topes. Se dibuja en una caja de 340 × 96 escalada al ancho del control.
/// </summary>
public class LoadedBarbell : GraphicsView
{
    public static readonly BindableProperty LoadProperty = BindableProperty.Create(
        nameof(Load), typeof(PlateLoad), typeof(LoadedBarbell), null, propertyChanged: Redraw);

    public static readonly BindableProperty IsMachineProperty = BindableProperty.Create(
        nameof(IsMachine), typeof(bool), typeof(LoadedBarbell), false, propertyChanged: Redraw);

    public PlateLoad? Load
    {
        get => (PlateLoad?)GetValue(LoadProperty);
        set => SetValue(LoadProperty, value);
    }

    public bool IsMachine
    {
        get => (bool)GetValue(IsMachineProperty);
        set => SetValue(IsMachineProperty, value);
    }

    private readonly LoadedBarbellDrawable _drawable = new();

    public LoadedBarbell()
    {
        Drawable = _drawable;
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
        HeightRequest = 84;
    }

    private static void Redraw(BindableObject b, object? _, object? __)
    {
        var view = (LoadedBarbell)b;
        view._drawable.Load = view.Load;
        view._drawable.IsMachine = view.IsMachine;
        view.Invalidate();
    }
}

public class LoadedBarbellDrawable : IDrawable
{
    public PlateLoad? Load { get; set; }
    public bool IsMachine { get; set; }

    private static readonly Color Bar = Color.FromArgb("#7D8396");
    private static readonly Color Sleeve = Color.FromArgb("#5C6276");
    private static readonly Color Collar = Color.FromArgb("#AEB5C8");

    private const float W = 340, H = 96, Mid = H / 2;
    // Hueco de cada lado para los discos: del tope (x = 242) al final de la barra (x = 336)
    private const float SideStart = 242, SideRoom = 94, Gap = 2;

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Load is not { } load) return;

        var scale = Math.Min(dirtyRect.Width / W, dirtyRect.Height / H);
        canvas.SaveState();
        canvas.Translate(dirtyRect.X + (dirtyRect.Width - W * scale) / 2, dirtyRect.Y + (dirtyRect.Height - H * scale) / 2);
        canvas.Scale(scale, scale);

        Fill(canvas, 4, 44, 332, 8, 3, Bar);
        if (IsMachine)
        {
            // Carro de la máquina en el centro
            canvas.StrokeColor = Sleeve;
            canvas.StrokeSize = 3;
            canvas.DrawRoundedRectangle(112, 14, 116, 68, 8);
        }
        else
        {
            Fill(canvas, 118, 40, 104, 16, 3, Sleeve);
            Fill(canvas, 102, 36, 10, 24, 2, Collar);
            Fill(canvas, 228, 36, 10, 24, 2, Collar);
        }

        // Anchos a escala aproximada; si no caben, se estrechan todos por igual
        var widths = load.PerSide.Select(WidthOf).ToList();
        var total = widths.Sum() + Gap * Math.Max(0, widths.Count - 1);
        var squeeze = total > SideRoom ? SideRoom / total : 1f;

        var offset = 0f;
        for (var i = 0; i < load.PerSide.Count; i++)
        {
            var kg = load.PerSide[i];
            var w = widths[i] * squeeze;
            var h = 88 * PlateColors.HeightOf(kg);
            var color = PlateColors.Of(kg);
            var radius = Math.Min(6, w / 3);
            Fill(canvas, SideStart + offset, Mid - h / 2, w, h, radius, color);
            Fill(canvas, W - SideStart - offset - w, Mid - h / 2, w, h, radius, color);
            offset += w + Gap * squeeze;
        }

        canvas.RestoreState();
    }

    private static float WidthOf(double kg) => kg switch
    {
        >= 20 => 36,
        >= 15 => 30,
        >= 10 => 24,
        >= 5 => 14,
        >= 2.5 => 10,
        _ => 8
    };

    private static void Fill(ICanvas canvas, float x, float y, float w, float h, float r, Color color)
    {
        canvas.FillColor = color;
        canvas.FillRoundedRectangle(x, y, w, h, r);
    }
}
