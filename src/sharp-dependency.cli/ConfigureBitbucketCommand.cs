using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli;

// ReSharper disable once ClassNeverInstantiated.Global
// ReSharper disable once UnusedAutoPropertyAccessor.Global
public class ConfigureBitbucketCommand : AsyncCommand<ConfigureBitbucketCommand.Settings>
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Settings : CommandSettings 
    {
        [CommandArgument(0, "[address]")]
        public string ApiAddress { get; init; }
        
        [CommandOption("-n|--name")]
        public string Name { get; set; }
        
        [CommandOption("-t|--type")]
        public BitbucketType Type { get; set; }
        
        [CommandOption("-u|--username")]
        public string UserName { get; set; }

        [CommandOption("-p|--password")]
        public string AppPassword { get; set; }

        [CommandOption("--token")]
        public string Token { get; set; }
    }
    
    public enum BitbucketType
    {
        Cloud,
        Server
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        Configuration.Bitbucket.BitbucketCredentials bitbucketCredentials = (settings.UserName, settings.AppPassword, settings.Token) switch
        {
            var (_, _, t) when !string.IsNullOrWhiteSpace(t) => new Configuration.Bitbucket.BitbucketCredentials.AccessTokenBitbucketCredentials() { Token = t },
            var (u, p, _) => new Configuration.Bitbucket.BitbucketCredentials.AppPasswordBitbucketCredentials() { UserName = u, AppPassword = p }
        };

        Configuration.Bitbucket bitbucketConfiguration = settings.Type switch
        {
            BitbucketType.Cloud => new Configuration.Bitbucket.CloudBitbucket() { ApiAddress = settings.ApiAddress, Credentials = bitbucketCredentials },
            BitbucketType.Server => new Configuration.Bitbucket.ServerBitbucket() { ApiAddress = settings.ApiAddress, Credentials = bitbucketCredentials },
            _ => throw new ArgumentOutOfRangeException()
        };

        var currentConfiguration = await SettingsManager.GetSettings<Configuration>();
        if (currentConfiguration is null)
        {
            currentConfiguration = new Configuration()
            {
                CurrentConfiguration = null,
                NugetConfiguration = null,
                Bitbuckets = new Dictionary<string, Configuration.Bitbucket>()
                {
                    {settings.Name, bitbucketConfiguration}
                }
            };
        }
        else
        {
            currentConfiguration.Bitbuckets[settings.Name] = bitbucketConfiguration;
        }
        
        await SettingsManager.SaveSettings(currentConfiguration);

        return 0;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Name))
        {
            return ValidationResult.Error($"Setting {nameof(settings.Name)} must have a value.");
        }
        
        if (string.IsNullOrWhiteSpace(settings.ApiAddress))
        {
            return ValidationResult.Error($"Setting {nameof(settings.ApiAddress)} must have a value.");
        }
        
        if ((string.IsNullOrWhiteSpace(settings.UserName) || string.IsNullOrWhiteSpace(settings.AppPassword)) && string.IsNullOrWhiteSpace(settings.Token))
        {
            return ValidationResult.Error($"When using basic authentication both {nameof(settings.UserName)} and {nameof(settings.AppPassword)} must have a value.");
        }
        
        if (!string.IsNullOrWhiteSpace(settings.Token) && (!string.IsNullOrWhiteSpace(settings.UserName) || !string.IsNullOrWhiteSpace(settings.AppPassword)))
        {
            return ValidationResult.Error($"When using token authentication both {nameof(settings.UserName)} and {nameof(settings.AppPassword)} must not have a value.");
        }
        
        return ValidationResult.Success();
    }
}