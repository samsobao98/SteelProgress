using System.ComponentModel;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

// Se abre con la ruta de un .steel ya copiado a la caché: GoToAsync("ImportPreviewPage", { ["path"] = ruta })
public partial class ImportPreviewPage : ContentPage, IQueryAttributable
{
    private readonly ImportViewModel _vm;
    private string? _path;

    public ImportPreviewPage(ImportViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.PropertyChanged += OnViewModelChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("path", out var value))
            _path = value as string;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_path is null || _vm.IsLoaded || _vm.IsDone) return;

        await _vm.LoadAsync(File.OpenRead(_path));
        UpdateSections();

        Badge.Scale = 0.6;
        Badge.Opacity = 0;
        await Task.WhenAll(Badge.ScaleToAsync(1, 380, Easing.SpringOut), Badge.FadeToAsync(1, 200));
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImportViewModel.IsLoaded) or nameof(ImportViewModel.IsDone) or nameof(ImportViewModel.IsUpdate))
            UpdateSections();
    }

    // «Importar» solo cuando no la tienes; si ya la tienes, «Actualizar» y «Guardar como copia»
    private void UpdateSections()
    {
        PreviewSection.IsVisible = _vm.IsLoaded && !_vm.IsDone;
        ImportButton.IsVisible = !_vm.IsUpdate;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (_vm.IsDone || !_vm.IsLoaded) _vm.Dispose();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        _vm.Dispose();
        await Shell.Current.GoToAsync("..");
    }
}
