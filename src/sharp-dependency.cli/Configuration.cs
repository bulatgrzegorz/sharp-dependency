using System.Text.Json.Serialization;
using NuGet.Configuration;

namespace sharp_dependency.cli;

// ReSharper disable once WithExpressionModifiesAllMembers
public sealed record Configuration
{
    public required Dictionary<string, Bitbucket> Bitbuckets { get; set; }
    public required Current? CurrentConfiguration { get; set; }
    public required Nuget? NugetConfiguration { get; set; }

    public Bitbucket? GetBitbucket(string? repositorySourceName)
    {
        if (Bitbuckets is { Count: 0 })
        {
            Console.WriteLine("[ERROR]: Given bitbucket configuration has no repository sources.");
            return null;
        }

        var repositoryContext = repositorySourceName ?? CurrentConfiguration?.RepositoryContext;
        if (string.IsNullOrEmpty(repositoryContext))
        {
            Console.WriteLine("[ERROR]: Either repository source name parameter should be used or repository source name as current context.");
            return null;
        }

        if (!Bitbuckets.TryGetValue(repositoryContext, out var bitbucket))
        {
            Console.WriteLine("[ERROR]: There is no bitbucket repository configuration for repository source name {0}.", repositoryContext);
            return null;
        }

        return bitbucket;
    }
    
    public NugetPackageSourceMangerChain GetNugetManager()
    {
        if (NugetConfiguration is null)
        {
            return new NugetPackageSourceMangerChain(new NugetPackageSourceManger());
        }

        var packageSourceProvider = new PackageSourceProvider(new NuGet.Configuration.Settings(NugetConfiguration.ConfigFileDirectory, NugetConfiguration.ConfigFileName));
        var packageSources = packageSourceProvider.LoadPackageSources().ToList();
        if (packageSources is { Count: 0 })
        {
            Console.WriteLine("[WARNING]: Given nuget configuration has no package sources. We are going to use default official nuget.");
            return new NugetPackageSourceMangerChain(new NugetPackageSourceManger());
        }

        return new NugetPackageSourceMangerChain(packageSources.Select(x => new NugetPackageSourceManger(x)).ToArray());
    }

    public Configuration WithoutSensitiveData()
    {
        return this with
        {
            Bitbuckets = Bitbuckets.ToDictionary(x => x.Key, x => x.Value with { Credentials = x.Value.Credentials?.WithoutSensitiveData() })
        };
    }

    public record Current
    {
        public required string? RepositoryContext { get; set; }
    }

    public sealed record Nuget
    {
        public required string ConfigFileDirectory { get; set; }
        public required string ConfigFileName { get; set; }
    }

    [JsonDerivedType(typeof(CloudBitbucket), "Cloud")]
    [JsonDerivedType(typeof(ServerBitbucket), "Server")]
    public abstract record Bitbucket
    {
        public required string ApiAddress { get; set; }
        public required BitbucketCredentials? Credentials { get; set; }

        [JsonDerivedType(typeof(AppPasswordBitbucketCredentials), "AppPassword")]
        [JsonDerivedType(typeof(AccessTokenBitbucketCredentials), "Token")]
        public abstract record BitbucketCredentials
        {
            public sealed record AppPasswordBitbucketCredentials : BitbucketCredentials
            {
                public required string UserName { get; set; }
                
                public required string AppPassword { get; set; }

                public override BitbucketCredentials WithoutSensitiveData()
                {
                    return this with
                    {
                        AppPassword = "****"
                    };
                }
            }
        
            public sealed record AccessTokenBitbucketCredentials : BitbucketCredentials
            {
                public required string Token { get; set; }

                public override BitbucketCredentials WithoutSensitiveData()
                {
                    return this with
                    {
                        Token = "****"
                    };
                }
            }

            public abstract BitbucketCredentials WithoutSensitiveData();
        }

        public sealed record CloudBitbucket : Bitbucket
        {
            public CloudBitbucket()
            {
                ApiAddress = "https://api.bitbucket.org/";
            }
        }
    
        public sealed record ServerBitbucket : Bitbucket
        {
        }
    }
}