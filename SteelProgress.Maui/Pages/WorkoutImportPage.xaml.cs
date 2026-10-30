using System.ComponentModel;
using SteelProgress.Core.Services;
using SteelProgress.Core.ViewModels;
using SteelProgress.Domain.Entities;

namespace SteelProgress.Maui.Pages;

// Se abre con la ruta de un .steel de entrenos ya copiado a la caché: GoToAsync("WorkoutImportPage", { ["path"] = ruta })
public partial class WorkoutImportPage : ContentPage, IQueryAttributable
{
    private readonly WorkoutImportViewModel _vm;
    private readonly ClientContextService _clientContext;
    private string? _path;

    public WorkoutImportPage(WorkoutImportViewModel vm, ClientContextService clientContext)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _clientContext = clientContext;
        _vm.Saved += OnSaved;
        _vm.PropertyChanged += OnViewModelChanged;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("path", out var value)) _path = value as string;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_path is null || _vm.IsLoaded) return;
        await using var stream = File.OpenRead(_path);
        await _vm.LoadAsync(stream);
        BuildClientChoice();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Dispose();
    }

    // Guardado: al historial de ese cliente (salvo con un entreno en marcha, que no se cambia de cliente)
    private async void OnSaved(Client client)
    {
        if (!WorkoutStateService.IsWorkoutActive)
        {
            _clientContext.Activate(client);
            ProgressPage.Request(ProgressSection.History);
            await Shell.Current.GoToAsync("//ProgressPage");
        }
        else await Shell.Current.GoToAsync("..");
    }

    private async void OnCloseClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("..");

    // Abierto en un perfil personal: el archivo espera a que se elija otro perfil
    private async void OnOtherProfileClicked(object? sender, EventArgs e)
    {
        if (_path is null) return;
        Services.PendingImport.Hold(_path);
        var launcher = Handler?.MauiContext?.Services.GetService<LauncherPage>();
        if (launcher is not null) await launcher.ReturnToProfilesAsync();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorkoutImportViewModel.Client)) PaintClientChoice();
    }

    // Sin reconocer la rutina: un botón por cliente para elegir de quién es
    private void BuildClientChoice()
    {
        ClientChoice.Children.Clear();
        foreach (var client in _vm.Clients)
        {
            var chip = new Border
            {
                HeightRequest = 40, Padding = new Thickness(14, 0), Margin = new Thickness(0, 0, 8, 8),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 }, StrokeThickness = 1,
                ClassId = client.Id.ToString(),
                Content = new Label { Text = client.Name, FontSize = 14, VerticalOptions = LayoutOptions.Center }
            };
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => _vm.Choose(client);
            chip.GestureRecognizers.Add(tap);
            ClientChoice.Children.Add(chip);
        }
        PaintClientChoice();
    }

    private void PaintClientChoice()
    {
        var res = Application.Current!.Resources;
        foreach (var chip in ClientChoice.Children.OfType<Border>())
        {
            var selected = chip.ClassId == _vm.Client?.Id.ToString();
            chip.BackgroundColor = selected ? (Color)res["SpSteel1"] : (Color)res["SpBg"];
            chip.Stroke = selected ? (Color)res["SpSteel1"] : (Color)res["SpLine"];
            ((Label)chip.Content!).TextColor = selected ? (Color)res["SpOnAccent"] : (Color)res["SpText2"];
        }
    }
}
