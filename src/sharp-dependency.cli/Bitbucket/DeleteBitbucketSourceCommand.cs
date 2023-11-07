using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli.Bitbucket;

// ReSharper disable once ClassNeverInstantiated.Global
// ReSharper disable once UnusedAutoPropertyAccessor.Global
public class DeleteBitbucketSourceCommand : AsyncCommand<DeleteBitbucketSourceCommand.Settings>
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public class Settings : CommandSettings 
    {
        [Description("Name of repository source.")]
        [CommandArgument(0, "[name]")]
        public string Name { get; set; } = null!;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var currentConfiguration = await SettingsManager.GetSettings<Configuration>();
        if (currentConfiguration is null)
        {
            return 0;
        }

        _ = currentConfiguration.Bitbuckets.Remove(settings.Name);

        await SettingsManager.SaveSettings(currentConfiguration);
        
        return 0;
    }
    
    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ValidationResult.Error($"Setting {nameof(settings.Name)} must have a value.");
        }

        return ValidationResult.Success();
    }
}
