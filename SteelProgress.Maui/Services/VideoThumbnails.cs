namespace SteelProgress.Maui.Services;

/// <summary>
/// Fotograma y duración de un vídeo guardado en la app, para su miniatura (Rediseño 4). Se
/// calculan una vez y se guardan junto al vídeo, en <c>thumbs/&lt;archivo&gt;.&lt;ms&gt;.jpg</c> (la
/// duración va en el nombre): sin cambios en la base de datos. Solo Android; en las demás
/// plataformas no hay fotograma y la miniatura se queda con el icono ▶.
/// </summary>
public static class VideoThumbnails
{
    public sealed record Info(string? ImagePath, TimeSpan? Duration);

    private static readonly Dictionary<string, Info> Cache = new();
    private static readonly Info None = new(null, null);

    /// <summary>Lo ya calculado para este vídeo, sin esperar (null si aún no).</summary>
    public static Info? Peek(string videoPath)
    {
        lock (Cache) return Cache.TryGetValue(videoPath, out var info) ? info : null;
    }

    public static async Task<Info> GetAsync(string videoPath)
    {
        if (Peek(videoPath) is { } cached) return cached;
        var info = await Task.Run(() => Load(videoPath));
        lock (Cache) Cache[videoPath] = info;
        return info;
    }

    /// <summary>«0:24».</summary>
    public static string Format(TimeSpan duration) =>
        duration.TotalHours >= 1 ? duration.ToString(@"h\:mm\:ss") : $"{(int)duration.TotalMinutes}:{duration.Seconds:00}";

    private static Info Load(string videoPath)
    {
        try
        {
            if (!File.Exists(videoPath)) return None;
            var dir = Path.Combine(Path.GetDirectoryName(videoPath)!, "thumbs");
            var name = Path.GetFileName(videoPath);
            if (Directory.Exists(dir))
                foreach (var file in Directory.GetFiles(dir, name + ".*.jpg"))
                    if (long.TryParse(Path.GetFileNameWithoutExtension(file)[(name.Length + 1)..], out var ms))
                        return new Info(file, TimeSpan.FromMilliseconds(ms));
            return Create(videoPath, dir, name);
        }
        catch (Exception)
        {
            return None;     // un vídeo que el móvil no sabe leer: se queda con el icono
        }
    }

#if ANDROID
    private const int Width = 256;

    private static Info Create(string videoPath, string dir, string name)
    {
        using var retriever = new Android.Media.MediaMetadataRetriever();
        retriever.SetDataSource(videoPath);
        long.TryParse(retriever.ExtractMetadata(Android.Media.MetadataKey.Duration), out var ms);
        // Un fotograma a un segundo (o a la mitad si es más corto): el primero suele ser negro
        var at = Math.Min(1_000_000L, ms * 1000 / 2);
        using var frame = retriever.GetFrameAtTime(at, Android.Media.Option.ClosestSync);
        if (frame is null) return new Info(null, ms > 0 ? TimeSpan.FromMilliseconds(ms) : null);

        var height = Math.Max(1, frame.Height * Width / Math.Max(1, frame.Width));
        using var small = Android.Graphics.Bitmap.CreateScaledBitmap(frame, Width, height, true);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{name}.{ms}.jpg");
        using (var stream = File.Create(path))
            small.Compress(Android.Graphics.Bitmap.CompressFormat.Jpeg!, 80, stream);
        return new Info(path, TimeSpan.FromMilliseconds(ms));
    }
#else
    private static Info Create(string videoPath, string dir, string name) => None;
#endif
}
