using System.Text.Json;
using Spectre.Console.Cli;

namespace sharp_dependency.cli;

public class PrintConfigurationCommand : AsyncCommand
{
    public override async Task<int> ExecuteAsync(CommandContext context)
    {
        var currentConfiguration = await SettingsManager.GetSettings<Configuration>();
        if (currentConfiguration is null)
        {
            Console.WriteLine("No configuration yet. Use -h|--help for more info.");
            return 0;
        }
        
        var configuration = JsonSerializer.Serialize(currentConfiguration.WithoutSensitiveData(), new JsonSerializerOptions(){WriteIndented = true});
        Console.WriteLine(configuration);

        return 0;
    }
}