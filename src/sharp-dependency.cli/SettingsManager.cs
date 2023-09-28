using System.Text.Json;

namespace sharp_dependency.cli;

public static class SettingsManager
{
    private const string AppDirectoryName = "sharp_dependency";
    private const string SettingsFileExtension = ".json";

    public static T? GetSettings<T>()
    {
        var filePath = PathFor<T>();

        if (!File.Exists(filePath)) return default;

        var fileText = File.ReadAllText(filePath);

        return JsonSerializer.Deserialize<T>(fileText);
    }

    public static void SaveSettings<T>(T settings)
    {
        Directory.CreateDirectory(AppDirectory());

        File.WriteAllText(PathFor<T>(), JsonSerializer.Serialize(settings));
    }

    private static string PathFor<T>()
    {
        return Path.Combine(AppDirectory(), $"{typeof(T).Name}{SettingsFileExtension}");
    }

    private static string AppDirectory()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDirectoryName);
    }
}