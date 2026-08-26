using System.IO;

namespace EpsilonDesktop;

public static class Constants
{
    public const string APP_NAME = "Epsilon Desktop";
    public static string APP_DATA_DIRECTORY { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EpsilonDesktop");

    public static string SETTINGS_FILE_PATH { get; } = Path.Combine(APP_DATA_DIRECTORY, "settings.json");

}
