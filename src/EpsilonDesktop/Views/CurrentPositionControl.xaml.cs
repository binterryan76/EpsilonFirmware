using EpsilonDesktop.ViewModels;
using System.Windows.Controls;


namespace EpsilonDesktop.Views;

/// <summary>
/// Interaction logic for CurrentPositionControl.xaml
/// </summary>
public partial class CurrentPositionControl : UserControl
{
    public CurrentPositionControl()
    {
        InitializeComponent();
        ViewModel = new CurrentPositionViewModel();
        //ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        RootGrid.DataContext = ViewModel;
    }


    public CurrentPositionViewModel ViewModel { get; set; }
    /*
    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(CurrentPositionViewModel.FilePath):
                SetCurrentValue(FilePathProperty, ViewModel.FilePath);
                break;
                // Add case for each DependencyProperty
        }
    }

    public static readonly DependencyProperty FilePathProperty =
        DependencyProperty.Register(
            nameof(FilePath), typeof(string), typeof(CurrentPositionControl),
                    new FrameworkPropertyMetadata(string.Empty,
                        FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                        OnFilePathChanged));

    public string FilePath
    {
        get => (string)GetValue(FilePathProperty);
        set => SetValue(FilePathProperty, value);
    }

    private static void OnFilePathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (CurrentPositionControl)d;
        if (control.ViewModel.FilePath != (string)e.NewValue)
            control.ViewModel.FilePath = (string)e.NewValue;
    }
    */
}
