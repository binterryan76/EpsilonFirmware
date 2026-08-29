using System.IO;

namespace EpsilonDesktop;

public static class Constants
{
    public const string APP_NAME = "Epsilon Desktop";
    public static string APP_DATA_DIRECTORY { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EpsilonDesktop");

    public static string SETTINGS_FILE_PATH { get; } = Path.Combine(APP_DATA_DIRECTORY, "settings.json");

    /// <summary>
    /// U+1F818 Heavy Leftwards Arrow with Equilateral Arrowhead
    /// </summary>
    public const string ARROW_LEFT = "\uD83E\uDC18";

    /// <summary>
    /// U+1F81A Heavy Rightwards Arrow with Equilateral Arrowhead
    /// </summary>
    public const string ARROW_RIGHT = "\uD83E\uDC1A";

    /// <summary>
    /// U+1F81B Heavy Downwards Arrow with Equilateral Arrowhead
    /// </summary>
    public const string ARROW_DOWN = "\uD83E\uDC1B";

    /// <summary>
    /// U+1F819 Heavy Upwards Arrow with Equilateral Arrowhead
    /// </summary>
    public const string ARROW_UP = "\uD83E\uDC19";

    /// <summary>
    /// U+2B6E Clockwise Triangle-Headed Open Circle Arrow
    /// </summary>
    public const string ARROW_CLOCKWISE = "\u2B6E";

    /// <summary>
    /// U+21B6 Anticlockwise Top Semicircle Arrow
    /// </summary>
    public const string ARROW_COUNTERCLOCKWISE = "\u21B6";
}
