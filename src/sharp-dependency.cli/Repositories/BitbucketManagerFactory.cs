using sharp_dependency.Repositories;
using sharp_dependency.Repositories.Bitbucket;
using AppPasswordCredentials = sharp_dependency.cli.Configuration.Bitbucket.BitbucketCredentials.AppPasswordBitbucketCredentials;
using AccessTokenCredentials = sharp_dependency.cli.Configuration.Bitbucket.BitbucketCredentials.AccessTokenBitbucketCredentials;

namespace sharp_dependency.cli.Repositories;

public static class BitbucketManagerFactory
{
    public static IProjectManager CreateProjectManager(string bitbucketAddress, string? workspace, string? project, Configuration.Bitbucket bitbucket) => (bitbucket, bitbucket.Credentials) switch
    {
        (Configuration.Bitbucket.CloudBitbucket, null) => new BitbucketCloudProjectManager( bitbucketAddress, workspace!),
        (Configuration.Bitbucket.ServerBitbucket, null) => new BitbucketServerProjectManager(bitbucketAddress, project!),
        (Configuration.Bitbucket.CloudBitbucket, AppPasswordCredentials c) => new BitbucketCloudProjectManager( bitbucketAddress, workspace!, (c.UserName, c.AppPassword)),
        (Configuration.Bitbucket.CloudBitbucket, AccessTokenCredentials c) => new BitbucketCloudProjectManager(bitbucketAddress, workspace!, c.Token),
        (Configuration.Bitbucket.ServerBitbucket, AppPasswordCredentials c) => new BitbucketServerProjectManager(bitbucketAddress, project!, (c.UserName, c.AppPassword)),
        (Configuration.Bitbucket.ServerBitbucket, AccessTokenCredentials c) => new BitbucketServerProjectManager(bitbucketAddress, project!, c.Token),
        _ => throw new ArgumentOutOfRangeException()
    };
    
    public static IRepositoryManger CreateRepositoryManager(string bitbucketAddress, string? workspace, string? project, string repository, Configuration.Bitbucket bitbucket) => (bitbucket, bitbucket.Credentials) switch
    {
        (Configuration.Bitbucket.CloudBitbucket, null) => new BitbucketCloudRepositoryManager( bitbucketAddress, workspace!, repository),
        (Configuration.Bitbucket.ServerBitbucket, null) => new BitbucketServerRepositoryManager(bitbucketAddress, repository, project!),
        (Configuration.Bitbucket.CloudBitbucket, AppPasswordCredentials c) => new BitbucketCloudRepositoryManager( bitbucketAddress, workspace!, repository, (c.UserName, c.AppPassword)),
        (Configuration.Bitbucket.CloudBitbucket, AccessTokenCredentials c) => new BitbucketCloudRepositoryManager(bitbucketAddress, workspace!, repository, c.Token),
        (Configuration.Bitbucket.ServerBitbucket, AppPasswordCredentials c) => new BitbucketServerRepositoryManager(bitbucketAddress, repository, project!, (c.UserName, c.AppPassword)),
        (Configuration.Bitbucket.ServerBitbucket, AccessTokenCredentials c) => new BitbucketServerRepositoryManager(bitbucketAddress, repository, project!, c.Token),
        _ => throw new ArgumentOutOfRangeException()
    };
}