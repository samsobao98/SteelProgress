using SteelProgress.Core.ViewModels;

namespace SteelProgress.Maui.Pages;

public partial class ExercisePage : ContentPage
{
    public ExercisePage(ExerciseViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}
