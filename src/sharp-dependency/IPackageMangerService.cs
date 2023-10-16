using NuGet.Versioning;

namespace sharp_dependency;

public interface IPackageMangerService
{
    Task<IReadOnlyCollection<NuGetVersion>> GetPackageVersions(string packageId, IEnumerable<string> targetFrameworks, bool includePrerelease = false);
}