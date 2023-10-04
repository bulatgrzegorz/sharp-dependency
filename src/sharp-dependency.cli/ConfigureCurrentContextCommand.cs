using System.ComponentModel;
using Spectre.Console.Cli;

namespace sharp_dependency.cli;

// ReSharper disable once ClassNeverInstantiated.Global
// ReSharper disable once UnusedAutoPropertyAccessor.Global
public class ConfigureCurrentContextCommand : AsyncCommand<ConfigureCurrentContextCommand.Settings>
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Settings : CommandSettings
    {
        [Description("Repository source name to setup as current context.")]
        [CommandOption("-r|--repository")]
        public string? CurrentRepositoryName { get; set; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var currentConfiguration = await SettingsManager.GetSettings<Configuration>();
        if (currentConfiguration is null)
        {
            currentConfiguration = new Configuration()
            {
                CurrentConfiguration = new Configuration.Current(){ RepositoryContext = settings.CurrentRepositoryName },
                NugetConfiguration = null,
                Bitbuckets = new Dictionary<string, Configuration.Bitbucket>()
            };
        }
        else
        {
            if (currentConfiguration.CurrentConfiguration is null)
            {
                currentConfiguration.CurrentConfiguration = new Configuration.Current() { RepositoryContext = settings.CurrentRepositoryName };
            }
            else
            {
                currentConfiguration.CurrentConfiguration.RepositoryContext = settings.CurrentRepositoryName ?? currentConfiguration.CurrentConfiguration.RepositoryContext;
            }
        }
        
        await SettingsManager.SaveSettings(currentConfiguration);

        return 0;
    }
}