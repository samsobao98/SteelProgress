using Microsoft.Maui.Controls.Shapes;
using SteelProgress.Domain.Entities;
using SteelProgress.Maui.Converters;
using SteelProgress.Maui.Services;

namespace SteelProgress.Maui.Controls;

/// <summary>
/// Miniatura de un ejercicio en la biblioteca (Rediseño 4): el fotograma de su primer vídeo con
/// la duración («0:24»), su GIF («GIF») o su foto; sin nada, un hueco punteado con «+».
/// El fotograma se calcula aparte (<see cref="VideoThumbnails"/>) y aparece al estar listo.
/// </summary>
public class ExerciseThumb : ContentView
{
    public static readonly BindableProperty ExerciseProperty = BindableProperty.Create(
        nameof(Exercise), typeof(Exercise), typeof(ExerciseThumb), propertyChanged: (b, _, _) => ((ExerciseThumb)b).Refresh());

    public Exercise? Exercise
    {
        get => (Exercise?)GetValue(ExerciseProperty);
        set => SetValue(ExerciseProperty, value);
    }

    private readonly Border _card, _empty, _badge;
    private readonly Image _image;
    private readonly Label _play, _badgeText;

    public ExerciseThumb()
    {
        _image = new Image { Aspect = Aspect.AspectFill, IsAnimationPlaying = false };
        _play = new Label { Text = "▶", FontSize = 18, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center };
        _badgeText = new Label { FontSize = 9, FontFamily = "OpenSansSemibold" };
        _badge = new Border
        {
            BackgroundColor = Color.FromArgb("#B3161622"),
            Padding = new Thickness(5, 1),
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 5 },
            HorizontalOptions = LayoutOptions.Start,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(5),
            Content = _badgeText
        };
        _card = new Border
        {
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Grid { Children = { _image, _play, _badge } }
        };
        _empty = new Border
        {
            BackgroundColor = Colors.Transparent,
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 3, 3 },
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label { Text = "+", FontSize = 18, HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center }
        };
        _card.SetDynamicResource(BackgroundColorProperty, "SpCardHi");
        _play.SetDynamicResource(Label.TextColorProperty, "SpText");
        _badgeText.SetDynamicResource(Label.TextColorProperty, "SpText");
        _empty.SetDynamicResource(Border.StrokeProperty, "SpText4");
        ((Label)_empty.Content).SetDynamicResource(Label.TextColorProperty, "SpText4");
        Content = new Grid { Children = { _card, _empty } };
        Refresh();
    }

    private async void Refresh()
    {
        var exercise = Exercise;
        var first = exercise?.Media.OrderBy(m => m.Order).FirstOrDefault();
        _card.IsVisible = first is not null;
        _empty.IsVisible = first is null;
        _image.Source = null;
        _play.IsVisible = first?.Kind == MediaKind.Video;
        _badge.IsVisible = first?.Kind == MediaKind.Gif;
        _badgeText.Text = "GIF";
        if (first is null || MediaFiles.PathOf(first) is not string path) return;

        if (first.Kind != MediaKind.Video)
        {
            _image.Source = ImageSource.FromFile(path);
            return;
        }

        var info = VideoThumbnails.Peek(path) ?? await VideoThumbnails.GetAsync(path);
        if (!ReferenceEquals(exercise, Exercise)) return;   // la fila se reutilizó para otro ejercicio
        if (info.ImagePath is not null) _image.Source = ImageSource.FromFile(info.ImagePath);
        if (info.Duration is { } d)
        {
            _badgeText.Text = VideoThumbnails.Format(d);
            _badge.IsVisible = true;
        }
    }
}
