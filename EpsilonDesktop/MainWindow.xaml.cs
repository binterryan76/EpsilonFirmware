using EpsilonDesktop.ViewModels;
using System.Windows;

namespace EpsilonDesktop;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private MainViewModel GetViewModel()
    {
        return (MainViewModel)DataContext;
    }

    private void MainWindow_Load(object sender, EventArgs e)
    {
        //GetViewModel().PlotTests();
        //GetViewModel().Test();
    }
}