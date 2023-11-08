using System.Text.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Spectre.Console.Json;

namespace sharp_dependency.cli.ConfigCommands;

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
        var json = new JsonText(configuration);

        AnsiConsole.Write(
            new Panel(json)
                .Header("Configuration")
                .Collapse()
                .RoundedBorder()
                .BorderColor(Color.Yellow));

        return 0;
    }
}