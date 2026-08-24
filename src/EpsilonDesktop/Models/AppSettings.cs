using GenericHelpers;
using System.IO;
using System.Text.Json;

namespace EpsilonDesktop.Models;

/// <summary>
/// Stores application settings for EpsilonDesktop. 
/// </summary>
public class AppSettings
{
    private static readonly JsonSerializerOptions jsonSerializerOptions = new()
    {
        WriteIndented = true,
        AllowTrailingCommas = true
    };

    public string? StartupFilePath { get; set; }

    /// <summary>
    /// Saves application settings to <see cref="Constants.SETTINGS_FILE_PATH"/>.
    /// </summary>
    /// <returns></returns>
    public Exception? Save()
    {
        try
        {
            string settingsJson = JsonSerializer.Serialize(this, jsonSerializerOptions);
            File.WriteAllText(Constants.SETTINGS_FILE_PATH, settingsJson);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>
    /// Reads application settings from <see cref="Constants.SETTINGS_FILE_PATH"/>,
    /// parses them, and returns them.
    /// Returns default settings if file doesn't exist.
    /// Returns an error <see cref="Result{T}"/> if the file exists but cannot be read.
    /// </summary>
    /// <returns></returns>
    public static Result<AppSettings> Load()
    {
        if (!File.Exists(Constants.SETTINGS_FILE_PATH))
            return new();

        try
        {
            string settingsJson = File.ReadAllText(Constants.SETTINGS_FILE_PATH);
            AppSettings? appSettings = JsonSerializer.Deserialize<AppSettings>(settingsJson, jsonSerializerOptions);
            return appSettings is not null ? appSettings : new Exception($"Failed to read settings file: {Constants.SETTINGS_FILE_PATH}.");
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
