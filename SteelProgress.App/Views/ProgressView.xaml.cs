using System.Windows.Controls;
using SteelProgress.Core.ViewModels;

namespace SteelProgress.App.Views;

public partial class ProgressView : UserControl
{
    public ProgressView(int? exerciseId = null)
    {
        InitializeComponent();
        DataContext = new ProgressViewModel(App.DbContext, exerciseId);
    }
}
