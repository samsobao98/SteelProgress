using System.Windows.Controls;
using SteelProgress.App.ViewModels;

namespace SteelProgress.App.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        DataContext = new HomeViewModel();
    }
}