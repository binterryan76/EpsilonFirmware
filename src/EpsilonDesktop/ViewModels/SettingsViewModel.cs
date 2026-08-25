using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EpsilonDesktop.Models;

namespace EpsilonDesktop.ViewModels;

public partial class SettingsViewModel(AppSettings settings) : ObservableObject
{
    [ObservableProperty]
    public partial AppSettings Settings { get; set; } = settings;

    [RelayCommand]
    public void Save()
    {
        Exception? exception = Settings.Save();
        if (exception is not null)
        {
            Helpers.DisplayErrorMessage($"Failed to save settings:\n{Constants.SETTINGS_FILE_PATH}\n{exception.Message}", "Save Error");
            return;
        }
    }
}