using EpsilonDesktop.ViewModels;
using System.Windows;

namespace EpsilonDesktop.Views;

/// <summary>
/// Interaction logic for SettingsWindow.xaml
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ButtonCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void ButtonSave_Click(object sender, RoutedEventArgs e)
    {
        GetViewModel().Save();
        DialogResult = true;
    }

    private SettingsViewModel GetViewModel()
    {
        return (SettingsViewModel)DataContext;
    }
}
