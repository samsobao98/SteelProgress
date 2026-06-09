using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class HomePage : ContentPage
{
    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
