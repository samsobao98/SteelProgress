using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SteelProgress.Data.Context;
using SteelProgress.Domain.Entities;
using SteelProgress.Domain.Services;

namespace SteelProgress.Core.ViewModels;

public partial class LauncherViewModel : ObservableObject
{
    private readonly IServiceProvider _sp;
    private readonly ICurrentUserService _currentUserService;

    public ObservableCollection<UserProfile> Profiles { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotCreating))]
    private bool _isCreating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    private bool _isLoaded;

    public bool IsLoading => !IsLoaded;
    public bool IsNotCreating => !IsCreating;
    public bool HasProfiles => Profiles.Count > 0;
    public bool IsFirstTimeSetup => !HasProfiles;
    public string SubtitleText => HasProfiles ? "¿Quién entrena hoy?" : "Crea tu primer perfil";

    public IReadOnlyList<string> AvailableColors { get; } = new[]
    {
        "#89B4FA", "#A6E3A1", "#F38BA8", "#FAB387", "#F9E2AF",
        "#CBA6F7", "#94E2D5", "#89DCEB"
    };

    [ObservableProperty] private string _newProfileName = string.Empty;
    [ObservableProperty] private string _selectedColor = "#A6E3A1";

    public event Action? ProfileChosen;

    public LauncherViewModel(IServiceProvider sp, ICurrentUserService currentUserService)
    {
        _sp = sp;
        _currentUserService = currentUserService;
    }

    public async Task LoadAsync(Task migrationTask)
    {
        await migrationTask;

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var list = await db.UserProfiles.IgnoreQueryFilters().OrderBy(p => p.Id).ToListAsync();
        Profiles.Clear();
        foreach (var p in list)
            Profiles.Add(p);

        IsLoaded = true;
        NotifyProfileDerived();

        if (!HasProfiles)
            IsCreating = true;
    }

    [RelayCommand]
    public void SelectProfile(UserProfile profile)
    {
        _currentUserService.SetCurrentUser(profile.Id);
        ProfileChosen?.Invoke();
    }

    [RelayCommand]
    public void StartCreating()
    {
        NewProfileName = string.Empty;
        SelectedColor = "#A6E3A1";
        IsCreating = true;
    }

    [RelayCommand]
    public void SetColor(string color) => SelectedColor = color;

    [RelayCommand]
    public void CancelCreating()
    {
        if (HasProfiles)
            IsCreating = false;
    }

    [RelayCommand]
    public async Task CreateProfileAsync()
    {
        var name = NewProfileName.Trim();
        if (string.IsNullOrEmpty(name)) return;

        UserProfile profile;
        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            profile = new UserProfile { Name = name, AvatarColor = SelectedColor };
            db.UserProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        Profiles.Add(profile);
        _currentUserService.SetCurrentUser(profile.Id);
        IsCreating = false;
        NotifyProfileDerived();
        ProfileChosen?.Invoke();
    }

    private void NotifyProfileDerived()
    {
        OnPropertyChanged(nameof(HasProfiles));
        OnPropertyChanged(nameof(IsFirstTimeSetup));
        OnPropertyChanged(nameof(SubtitleText));
    }
}
