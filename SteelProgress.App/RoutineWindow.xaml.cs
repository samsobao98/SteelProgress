using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SteelProgress.Data.Repositories;
using SteelProgress.App.ViewModels;

namespace SteelProgress.App
{
    /// <summary>
    /// Lógica de interacción para RoutineWindow.xaml
    /// </summary>
    public partial class RoutineWindow : Window
    {
        public RoutineWindow()
        {
            InitializeComponent();

            var repository= new RoutineRepository(App.DbContext);
            DataContext= new RoutineViewModel(repository);
        }
    }
}
