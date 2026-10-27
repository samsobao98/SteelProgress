using System.Globalization;
using SteelProgress.Core.Services;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Converters;

// Ruta real de un archivo multimedia guardado en la app
internal static class MediaFiles
{
    public static string? PathOf(ExerciseMedia media) =>
        IPlatformApplication.Current?.Services.GetService<IMediaStorage>()?.GetFullPath(media.FileName);
}

// ExerciseMedia (imagen o GIF) → imagen para mostrar. Los vídeos no tienen miniatura: devuelve null.
public class MediaImageSourceConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ExerciseMedia { Kind: not MediaKind.Video } m && MediaFiles.PathOf(m) is string path
            ? ImageSource.FromFile(path)
            : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// ExerciseMedia → ¿es vídeo? (con ConverterParameter=gif → ¿es GIF?)
public class MediaIsVideoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ExerciseMedia m && m.Kind == (parameter is "gif" ? MediaKind.Gif : MediaKind.Video);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

// Exercise → miniatura de su primera imagen o GIF (null si no tiene).
// Con ConverterParameter=video → si su primer archivo es un vídeo; con =any → si tiene alguno.
public class ExerciseThumbnailConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var first = (value as Exercise)?.Media.OrderBy(m => m.Order).FirstOrDefault();
        return parameter switch
        {
            "video" => first?.Kind == MediaKind.Video,
            "any" => first is not null,
            _ => first is { Kind: not MediaKind.Video } && MediaFiles.PathOf(first) is string path
                ? ImageSource.FromFile(path)
                : null
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
