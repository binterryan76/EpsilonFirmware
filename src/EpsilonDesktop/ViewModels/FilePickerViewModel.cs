using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace EpsilonDesktop.ViewModels;

public partial class FilePickerViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string FilePath { get; set; } = "";

    [ObservableProperty]
    public partial string FileFilter { get; set; } = "";

    [ObservableProperty]
    public partial string LabelText { get; set; } = "File Path:";

    [RelayCommand]
    public void Browse()
    {
        OpenFileDialog openFileDialog = new() { Filter = FileFilter };

        if (openFileDialog.ShowDialog() == true)
            FilePath = openFileDialog.FileName;
    }
}
