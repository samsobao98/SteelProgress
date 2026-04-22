using System.Windows.Controls;
using SteelProgress.App.ViewModels;

namespace SteelProgress.App.Views;

public partial class ProgressView : UserControl
{
    public ProgressView()
    {
        InitializeComponent();
        DataContext = new ProgressViewModel();
    }
}