using MeetingMind.ViewModels;
using System.Windows;

namespace MeetingMind.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}
