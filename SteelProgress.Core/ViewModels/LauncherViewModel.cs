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
    [NotifyPropertyChangedFor(nameof(CanAddProfile))]
    private bool _isCreating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    private bool _isLoaded;

    public bool IsLoading => !IsLoaded;
    public bool IsNotCreating => !IsCreating;
    public bool CanAddProfile => !IsCreating;
    public bool HasProfiles => Profiles.Count > 0;
    public bool IsFirstTimeSetup => !HasProfiles;
    public string SubtitleText => HasProfiles ? "¿Quién entrena hoy?" : "Crea tu primer perfil";

    public IReadOnlyList<string> AvailableColors { get; } = new[]
    {
        "#89B4FA", "#A6E3A1", "#F38BA8", "#FAB387", "#F9E2AF",
        "#CBA6F7", "#94E2D5", "#89DCEB"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DescriptionPreviewText))]
    [NotifyPropertyChangedFor(nameof(HasDescription))]
    [NotifyPropertyChangedFor(nameof(HasNoDescription))]
    private string _newProfileDescription = string.Empty;

    [ObservableProperty] private string _newProfileName = string.Empty;
    [ObservableProperty] private string _selectedColor = "#A6E3A1";

    public bool HasDescription => !string.IsNullOrWhiteSpace(NewProfileDescription);
    public bool HasNoDescription => string.IsNullOrWhiteSpace(NewProfileDescription);
    public string DescriptionPreviewText =>
        string.IsNullOrWhiteSpace(NewProfileDescription)
            ? "Tu frase aquí..."
            : $"\"{NewProfileDescription.Trim()}\"";

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

    public async Task DeleteProfileAsync(UserProfile profile)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.UserProfiles.FindAsync(profile.Id);
        if (entity is null) return;
        db.UserProfiles.Remove(entity);
        await db.SaveChangesAsync();

        Profiles.Remove(profile);
        NotifyProfileDerived();

        if (!HasProfiles)
            IsCreating = true;
    }

    [RelayCommand]
    public void StartCreating()
    {
        NewProfileName = string.Empty;
        NewProfileDescription = string.Empty;
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
            var desc = NewProfileDescription.Trim();
            profile = new UserProfile
            {
                Name = name,
                AvatarColor = SelectedColor,
                Description = desc.Length > 0 ? desc : null
            };
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
        OnPropertyChanged(nameof(CanAddProfile));
    }
}
