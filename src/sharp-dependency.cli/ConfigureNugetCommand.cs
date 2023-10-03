using System.ComponentModel;
using NuGet.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli;

// ReSharper disable once ClassNeverInstantiated.Global
// ReSharper disable once UnusedAutoPropertyAccessor.Global
public class ConfigureNugetCommand : AsyncCommand<ConfigureNugetCommand.Settings>
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Settings : CommandSettings 
    {
        [Description("The NuGet configuration file")]
        [CommandArgument(0, "[path]")]
        public string ConfigPath { get; init; } = null!;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        if (!File.Exists(settings.ConfigPath))
        {
            Console.WriteLine("[ERROR]: Could not find given configuration file {0}", settings.ConfigPath);

            return 1;
        }

        var configFileDirectory = Path.GetDirectoryName(settings.ConfigPath);
        var configFileName = Path.GetFileName(settings.ConfigPath);
        
        EnsureConfiguration(configFileDirectory, configFileName);

        var currentConfiguration = await SettingsManager.GetSettings<Configuration>();
        if (currentConfiguration is null)
        {
            currentConfiguration = new Configuration()
            {
                Bitbuckets = new Dictionary<string, Configuration.Bitbucket>(),
                CurrentConfiguration = null,
                NugetConfiguration = new Configuration.Nuget() { ConfigFileDirectory = configFileDirectory!, ConfigFileName = configFileName}
            };
        }
        else
        {
            if (currentConfiguration.NugetConfiguration is null)
            {
                currentConfiguration.NugetConfiguration = new Configuration.Nuget() { ConfigFileDirectory = configFileDirectory!, ConfigFileName = configFileName };
            }
            else
            {
                currentConfiguration.NugetConfiguration.ConfigFileName = configFileName;
                currentConfiguration.NugetConfiguration.ConfigFileDirectory = configFileDirectory!;                
            }
        }

        await SettingsManager.SaveSettings(currentConfiguration);

        return 0;
    }

    private static void EnsureConfiguration(string? configFileDirectory, string configFileName)
    {
        var packageSourceProvider =
            new PackageSourceProvider(new NuGet.Configuration.Settings(configFileDirectory, configFileName));
        var _ = packageSourceProvider.LoadPackageSources().ToList();
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.ConfigPath))
        {
            return ValidationResult.Error($"Setting {nameof(settings.ConfigPath)} must have a value.");
        }
        
        return ValidationResult.Success();
    }
}