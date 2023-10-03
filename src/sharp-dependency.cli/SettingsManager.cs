using System.Text.Json;

namespace sharp_dependency.cli;

public static class SettingsManager
{
    private const string AppDirectoryName = "sharp_dependency";
    private const string SettingsFileExtension = ".json";

    public static async ValueTask<T?> GetSettings<T>()
    {
        var filePath = PathFor<T>();

        if (!File.Exists(filePath)) return default;

        await using var fileStream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<T>(fileStream);
    }

    public static Task SaveSettings<T>(T settings)
    {
        Directory.CreateDirectory(AppDirectory());

        return File.WriteAllTextAsync(PathFor<T>(), JsonSerializer.Serialize(settings, new JsonSerializerOptions(){WriteIndented = true}));
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