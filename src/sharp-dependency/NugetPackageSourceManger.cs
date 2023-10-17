using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace sharp_dependency;

//TODO: Should check if updated package has other packages references that should be taken into consideration
public class NugetPackageSourceMangerChain : IPackageMangerService
{
    private readonly List<NugetPackageSourceManger> _chain;

    public NugetPackageSourceMangerChain(params NugetPackageSourceManger[] managers)
    {
        _chain = managers.ToList();
    }
    
    public async Task<IReadOnlyCollection<NuGetVersion>> GetPackageVersions(string packageId, IEnumerable<string> targetFrameworks, bool includePrerelease = false)
    {
        if(_chain is {Count: 0}) return ArraySegment<NuGetVersion>.Empty;
        
        var frameworks = targetFrameworks as string[] ?? targetFrameworks.ToArray();
        
        foreach (var nugetPackageSourceManger in _chain)
        {
            var allVersions = await nugetPackageSourceManger.GetPackageVersions(packageId, frameworks, includePrerelease);
            if (allVersions.Count == 0)
            {
                continue;
            }

            return allVersions;
        }
        
        return ArraySegment<NuGetVersion>.Empty;
    }
}

public class NugetPackageSourceManger
{
    public enum ApiVersion { V3, V2 }

    private readonly SourceRepository _sourceRepository;
    private static readonly SourceCacheContext SourceCacheContext = new();
    private static readonly FrameworkReducer FrameworkReducer = new();
    private static readonly ConcurrentDictionary<string, NuGetFramework> ParsedTargetFrameworks = new();
    private readonly ConcurrentDictionary<string, Lazy<Task<IReadOnlyCollection<NuGetVersion>>>> _packageVersionCache = new();

    public NugetPackageSourceManger()
    {
        _sourceRepository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
    }
    
    public NugetPackageSourceManger(string address, ApiVersion apiVersion, (string username, string password, bool isPasswordClearText)? credentials)
    {
        var packageSource = new PackageSource(address);
        if(credentials.HasValue)
        {
            packageSource.Credentials = new PackageSourceCredential(address, credentials.Value.username, credentials.Value.password, credentials.Value.isPasswordClearText, string.Empty);
        }
        
        _sourceRepository = apiVersion == ApiVersion.V3 ? Repository.Factory.GetCoreV3(packageSource) : Repository.Factory.GetCoreV2(packageSource);
    }
    
    public NugetPackageSourceManger(PackageSource packageSource)
    {
        _sourceRepository = Repository.Factory.GetCoreV3(packageSource);
        //TODO: Some onpremise sources work much faster on protocolVersion = 2 when using v3. Does all sources with protocolVersion = 2 will work correctly on v3?
        // _sourceRepository = packageSource.ProtocolVersion == 3 ? Repository.Factory.GetCoreV3(packageSource) : Repository.Factory.GetCoreV2(packageSource);
    }

    public async Task<IReadOnlyCollection<NuGetVersion>> GetPackageVersions(string packageId, IEnumerable<string> targetFrameworks, bool includePrerelease = false)
    {
        var frameworks = targetFrameworks as string[] ?? targetFrameworks.ToArray();
        
        var cacheKey = GetCacheKey(packageId, includePrerelease, frameworks);
        var getAllVersionsTask = new Lazy<Task<IReadOnlyCollection<NuGetVersion>>>(() => GetPackageVersionsInternal(packageId, frameworks, includePrerelease));
        return await _packageVersionCache.GetOrAdd(cacheKey, getAllVersionsTask).Value;
    }
    
    private async Task<IReadOnlyCollection<NuGetVersion>> GetPackageVersionsInternal(string packageId, IEnumerable<string> targetFrameworks, bool includePrerelease = false)
    {
        var versions = new List<NuGetVersion>();
        var packagesMetadata = await GetPackageMetadata(packageId, includePrerelease);
        
        var parsedFrameworks = targetFrameworks.Select(x => ParsedTargetFrameworks.GetOrAdd(x, NuGetFramework.Parse)).ToList();
        foreach (var packageMetadata in packagesMetadata)
        {
            if (PackageSelector.GetVersionIfSelected(packageMetadata, parsedFrameworks, out var version))
            {
                versions.Add(version);
            }
        }

        return versions;
    }

    private static string GetCacheKey(string packageId, bool includePrerelease, IEnumerable<string> targetFrameworks) => $"{packageId.ToLowerInvariant()}-{includePrerelease.ToString()}-{string.Join(";", targetFrameworks.Order())}";

    private async Task<IEnumerable<IPackageSearchMetadata>> GetPackageMetadata(string packageId, bool includePrerelease)
    {
        var packageMetadataResource = await _sourceRepository.GetResourceAsync<PackageMetadataResource>();
        return await packageMetadataResource.GetMetadataAsync(packageId, includePrerelease, false, SourceCacheContext, new NullLogger(), CancellationToken.None);
    }
}

internal static class PackageSelector
{
    private static readonly FrameworkReducer FrameworkReducer = new();
    
    public static bool GetVersionIfSelected(IPackageSearchMetadata package, List<NuGetFramework> frameworks, [NotNullWhen(true)] out NuGetVersion? version)
    {
        //if we do not have any target frameworks, we should not update anything (it could be filter out at condition evaluation step)
        if(frameworks.Count == 0)
        {
            version = default;
            return false;
        }
        
        if (!package.IsListed)
        {
            version = default;
            return false;
        }

        //if package has no dependencies, we can simply add it
        if (!package.DependencySets.Any())
        {
            version = package.GetVersion();
            return true;
        }

        //if we have multiple targets for some package, all must to be compatible
        if (frameworks.TrueForAll(x => FrameworkReducer.GetNearest(x, package.DependencySets.Select(y => y.TargetFramework)) is not null))
        {
            version = package.GetVersion();
            return true;
        }
        
        version = default;
        return false;
    }
    
    private static NuGetVersion GetVersion(this IPackageSearchMetadata packageSearchMetadata) 
        => packageSearchMetadata switch
        {
            PackageSearchMetadata value => value.Version,
            PackageSearchMetadataV2Feed value => value.Version,
            LocalPackageSearchMetadata value => value.Identity.Version,
            var version => version.Identity.Version
        };
}