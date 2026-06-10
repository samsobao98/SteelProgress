using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class WorkoutPage : ContentPage
{
    public WorkoutPage(WorkoutViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
