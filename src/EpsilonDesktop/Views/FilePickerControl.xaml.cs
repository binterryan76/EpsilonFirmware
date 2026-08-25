using EpsilonDesktop.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace EpsilonDesktop.Views;

/// <summary>
/// Interaction logic for FilePickerControl.xaml
/// </summary>
public partial class FilePickerControl : UserControl
{
    public FilePickerControl()
    {
        InitializeComponent();
        ViewModel = new FilePickerViewModel();
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        RootGrid.DataContext = ViewModel;
    }

    public FilePickerViewModel ViewModel { get; set; }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(FilePickerViewModel.FilePath):
                SetCurrentValue(FilePathProperty, ViewModel.FilePath);
                break;
            case nameof(FilePickerViewModel.FileFilter):
                SetCurrentValue(FileFilterProperty, ViewModel.FileFilter);
                break;
                // LabelText doesn't need this — it's DP -> VM only, never edited by the VM's own UI
        }
    }

    public static readonly DependencyProperty LabelTextProperty =
        DependencyProperty.Register(
            nameof(LabelText), typeof(string), typeof(FilePickerControl),
            new PropertyMetadata(string.Empty, OnLabelTextChanged));

    public string LabelText
    {
        get => (string)GetValue(LabelTextProperty);
        set => SetValue(LabelTextProperty, value);
    }

    private static void OnLabelTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FilePickerControl)d;
        if (control.ViewModel.LabelText != (string)e.NewValue)
            control.ViewModel.LabelText = (string)e.NewValue;
    }

    public static readonly DependencyProperty FilePathProperty =
        DependencyProperty.Register(
            nameof(FilePath), typeof(string), typeof(FilePickerControl),
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
        var control = (FilePickerControl)d;
        if (control.ViewModel.FilePath != (string)e.NewValue)
            control.ViewModel.FilePath = (string)e.NewValue;
    }

    public static readonly DependencyProperty FileFilterProperty =
        DependencyProperty.Register(
            nameof(FileFilter), typeof(string), typeof(FilePickerControl),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnFileFilterChanged));

    public string FileFilter
    {
        get => (string)GetValue(FileFilterProperty);
        set => SetValue(FileFilterProperty, value);
    }

    private static void OnFileFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var control = (FilePickerControl)d;
        if (control.ViewModel.FileFilter != (string)e.NewValue)
            control.ViewModel.FileFilter = (string)e.NewValue;
    }
}
