using System.Windows.Controls;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.App.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
        DataContext = new HomeViewModel(App.DbContext);
    }
}
