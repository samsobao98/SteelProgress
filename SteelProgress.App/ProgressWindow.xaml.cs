using System.Windows;
using SteelProgress.App.ViewModels;

namespace SteelProgress.App;

public partial class ProgressWindow : Window
{
    public ProgressWindow()
    {
        InitializeComponent();
        DataContext = new ProgressViewModel();
    }

  
}