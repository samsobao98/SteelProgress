using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Controls;

/// <summary>
/// Icono del tipo de material (maquetas «Acero + Discos»): barra con discos, mancuernas, máquina de placas
/// (pila con el pasador dorado), máquina de discos, polea, peso corporal, kettlebell y «otro» (una goma).
/// Se dibuja en una caja de 34 × 22 escalada al tamaño del control. Sin tipo no dibuja nada.
/// </summary>
public class EquipmentIcon : GraphicsView
{
    public static readonly BindableProperty TypeProperty = BindableProperty.Create(
        nameof(Type), typeof(EquipmentType?), typeof(EquipmentIcon), null,
        propertyChanged: (b, _, n) => { var icon = (EquipmentIcon)b; icon._drawable.Type = (EquipmentType?)n; icon.Invalidate(); });

    public EquipmentType? Type
    {
        get => (EquipmentType?)GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    private readonly EquipmentIconDrawable _drawable = new();

    public EquipmentIcon()
    {
        Drawable = _drawable;
        WidthRequest = 34;
        HeightRequest = 22;
        BackgroundColor = Colors.Transparent;
        InputTransparent = true;
    }
}

public class EquipmentIconDrawable : IDrawable
{
    public EquipmentType? Type { get; set; }

    // Colores de las maquetas: los de los discos (rojo 25, azul 20, blanco 5) tienen significado en kilos
    private static readonly Color Bar = Color.FromArgb("#7D8396");
    private static readonly Color Metal = Color.FromArgb("#AEB5C8");
    private static readonly Color Frame = Color.FromArgb("#5C6276");
    private static readonly Color Steel = Color.FromArgb("#9DB4E6");
    private static readonly Color Unused = Color.FromArgb("#3A3F52");
    private static readonly Color Gold = Color.FromArgb("#F0C46A");
    private static readonly Color Red = Color.FromArgb("#D9473F");
    private static readonly Color Blue = Color.FromArgb("#2E6BD6");
    private static readonly Color White = Color.FromArgb("#E9E9E6");

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Type is not { } type) return;

        // Caja de 34 × 22 centrada y escalada sin deformar
        var scale = Math.Min(dirtyRect.Width / 34f, dirtyRect.Height / 22f);
        canvas.SaveState();
        canvas.Translate(dirtyRect.X + (dirtyRect.Width - 34 * scale) / 2, dirtyRect.Y + (dirtyRect.Height - 22 * scale) / 2);
        canvas.Scale(scale, scale);
        canvas.StrokeLineCap = LineCap.Round;

        switch (type)
        {
            case EquipmentType.Barbell:
                Rect(canvas, 0, 10, 34, 2, 0, Bar);
                Rect(canvas, 7, 0, 6, 22, 1.5f, Red);
                Rect(canvas, 14, 3, 4, 16, 1, Blue);
                break;

            case EquipmentType.Dumbbells:
                Rect(canvas, 2, 4, 7, 14, 2, Metal);
                Rect(canvas, 25, 4, 7, 14, 2, Metal);
                Rect(canvas, 9, 9.5f, 16, 3, 1, Bar);
                break;

            case EquipmentType.StackMachine:
                canvas.StrokeColor = Frame;
                canvas.StrokeSize = 1.5f;
                canvas.DrawRoundedRectangle(9, 0.75f, 16, 20.5f, 3);
                Rect(canvas, 12, 3, 10, 3, 1, Steel);
                Rect(canvas, 12, 7.5f, 10, 3, 1, Steel);
                Rect(canvas, 12, 12, 10, 3, 1, Unused);
                Rect(canvas, 12, 16.5f, 10, 3, 1, Unused);
                canvas.FillColor = Gold;
                canvas.FillCircle(5, 10.5f, 2.2f);
                break;

            case EquipmentType.PlateMachine:
                canvas.StrokeColor = Frame;
                canvas.StrokeSize = 1.5f;
                canvas.DrawRoundedRectangle(1, 3, 12, 16, 2.5f);
                Rect(canvas, 13, 9.5f, 20, 3, 1, Bar);
                Rect(canvas, 16, 1, 5, 20, 1.5f, Red);
                Rect(canvas, 22, 1, 5, 20, 1.5f, Red);
                Rect(canvas, 28, 6, 3, 10, 1, White);
                break;

            case EquipmentType.Cable:
                canvas.StrokeColor = Metal;
                canvas.StrokeSize = 1.8f;
                canvas.DrawCircle(9, 5, 4);
                canvas.StrokeColor = Bar;
                canvas.StrokeSize = 1.5f;
                var cable = new PathF();
                cable.MoveTo(9, 1); cable.LineTo(28, 1); cable.LineTo(28, 7);
                canvas.DrawPath(cable);
                canvas.DrawLine(5, 5, 5, 17);
                canvas.StrokeColor = Metal;
                canvas.StrokeSize = 2.6f;
                canvas.DrawLine(1, 18, 11, 18);
                Rect(canvas, 24, 8, 8, 3, 1, Steel);
                Rect(canvas, 24, 12, 8, 3, 1, Steel);
                Rect(canvas, 24, 16, 8, 3, 1, Unused);
                break;

            case EquipmentType.Bodyweight:
                canvas.FillColor = Metal;
                canvas.FillCircle(17, 4, 3.2f);
                canvas.StrokeColor = Metal;
                canvas.StrokeSize = 2.2f;
                canvas.DrawLine(17, 8, 17, 15);
                canvas.DrawLine(17, 15, 12, 21);
                canvas.DrawLine(17, 15, 22, 21);
                canvas.DrawLine(10, 10, 24, 10);
                break;

            case EquipmentType.Kettlebell:
                canvas.StrokeColor = Metal;
                canvas.StrokeSize = 2.2f;
                var handle = new PathF();
                handle.MoveTo(12, 8); handle.CurveTo(12, 1, 22, 1, 22, 8);
                canvas.DrawPath(handle);
                canvas.FillColor = Metal;
                canvas.FillCircle(17, 14, 7.5f);
                break;

            default:
                canvas.StrokeColor = Metal;
                canvas.StrokeSize = 2.4f;
                var band = new PathF();
                band.MoveTo(4, 11); band.CurveTo(10, 3, 24, 19, 30, 11);
                canvas.DrawPath(band);
                break;
        }

        canvas.RestoreState();
    }

    private static void Rect(ICanvas canvas, float x, float y, float w, float h, float r, Color color)
    {
        canvas.FillColor = color;
        canvas.FillRoundedRectangle(x, y, w, h, r);
    }
}
