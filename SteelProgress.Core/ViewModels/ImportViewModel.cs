using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SteelProgress.Core.Services;
using SteelProgress.Core.Sharing;

namespace SteelProgress.Core.ViewModels;

// Vista previa de un .steel antes de importarlo (maqueta 6, «Cliente · abre lo que le envían»)
public partial class ImportViewModel : ObservableObject, IDisposable
{
    private readonly PackageImporter _importer;
    private SteelPackage? _package;

    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _error;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private string _doneMessage = "";

    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _kindLabel = "";
    [ObservableProperty] private string? _authorName;
    [ObservableProperty] private string? _message;
    [ObservableProperty] private string? _versionBadge;
    [ObservableProperty] private bool _isUpdate;
    [ObservableProperty] private bool _isOlderOrSameVersion;
    [ObservableProperty] private string _daysText = "";
    [ObservableProperty] private string _newExercisesText = "";
    [ObservableProperty] private string _mediaText = "";
    [ObservableProperty] private bool _hasDays;

    public bool HasAuthor => !string.IsNullOrWhiteSpace(AuthorName);
    public bool HasMessage => !string.IsNullOrWhiteSpace(Message);

    public event Action? Finished;

    public ImportViewModel(PackageImporter importer)
    {
        _importer = importer;
    }

    public async Task LoadAsync(Stream stream)
    {
        Error = null;
        try
        {
            _package?.Dispose();
            _package = SteelPackage.Open(stream);
            var p = await _importer.PreviewAsync(_package);

            Title = p.Title;
            KindLabel = p.Kind == PackageKind.Routine ? "Rutina" : "Ejercicios";
            AuthorName = p.AuthorName;
            Message = p.Message;
            OnPropertyChanged(nameof(HasAuthor));
            OnPropertyChanged(nameof(HasMessage));

            IsUpdate = p.IsUpdate;
            IsOlderOrSameVersion = p.IsUpdate && !p.IsNewerVersion;
            VersionBadge = p.IsUpdate ? $"Actualización v{p.ExistingVersion} → v{p.IncomingVersion}" : null;

            HasDays = p.Kind == PackageKind.Routine;
            DaysText = p.DayCount == 1 ? "1 día" : $"{p.DayCount} días";
            NewExercisesText = p.NewExerciseCount == 0 ? "Ninguno (ya los tienes)" : p.NewExerciseCount.ToString();
            MediaText = p.MediaCount == 0 ? "Sin vídeos" : $"{p.MediaCount} · {MediaKinds.FormatSize(p.MediaBytes)}";

            IsLoaded = true;
        }
        catch (SteelPackageException ex)
        {
            Error = ex.Message;
            IsLoaded = false;
        }
    }

    [RelayCommand]
    private Task Update() => ImportAsync(ImportMode.UpdateExisting);

    [RelayCommand]
    private Task SaveCopy() => ImportAsync(ImportMode.SaveAsCopy);

    [RelayCommand]
    private void Cancel()
    {
        Dispose();
        Finished?.Invoke();
    }

    private async Task ImportAsync(ImportMode mode)
    {
        if (_package is null || IsBusy) return;
        IsBusy = true;
        Error = null;
        try
        {
            var r = await _importer.ImportAsync(_package, mode);
            DoneMessage = r.RoutinesUpdated > 0 ? "Rutina actualizada. Tu historial se conserva."
                : r.RoutinesCreated > 0 ? "Rutina importada."
                : r.ExercisesCreated == 1 ? "1 ejercicio importado."
                : $"{r.ExercisesCreated} ejercicios importados.";
            IsDone = true;
            Dispose();
        }
        catch (Exception ex) when (ex is SteelPackageException or MediaImportException)
        {
            Error = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        _package?.Dispose();
        _package = null;
    }
}
