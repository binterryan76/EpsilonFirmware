using System.Windows;

namespace EpsilonDesktop;

internal static class Helpers
{
    public static void DisplayErrorMessage(string message, string title)
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
