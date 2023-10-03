using System.Text.Json.Serialization;

namespace sharp_dependency.cli;

public sealed class Configuration
{
    public required Dictionary<string, Bitbucket> Bitbuckets { get; set; }
    public required Current? CurrentConfiguration { get; set; }
    public required Nuget? NugetConfiguration { get; set; }

    public class Current
    {
        public required string? RepositoryContext { get; set; }
    }

    public sealed class Nuget
    {
        public required string ConfigFileDirectory { get; set; }
        public required string ConfigFileName { get; set; }
    }

    [JsonDerivedType(typeof(CloudBitbucket), "Cloud")]
    [JsonDerivedType(typeof(ServerBitbucket), "Server")]
    public abstract class Bitbucket
    {
        public required string ApiAddress { get; set; }
        public required BitbucketCredentials Credentials { get; set; }

        [JsonDerivedType(typeof(AppPasswordBitbucketCredentials), "AppPassword")]
        [JsonDerivedType(typeof(AccessTokenBitbucketCredentials), "Token")]
        public class BitbucketCredentials
        {
            public sealed class AppPasswordBitbucketCredentials : BitbucketCredentials
            {
                public required string UserName { get; set; }
                public required string AppPassword { get; set; }
            }
        
            public sealed class AccessTokenBitbucketCredentials : BitbucketCredentials
            {
                public required string Token { get; set; }
            }
        }

        public sealed class CloudBitbucket : Bitbucket
        {
            public CloudBitbucket()
            {
                ApiAddress = "https://api.bitbucket.org/";
            }
        }
    
        public sealed class ServerBitbucket : Bitbucket
        {
        }
    }
}