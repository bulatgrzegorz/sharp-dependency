using System.Diagnostics.CodeAnalysis;
using NuGet.Versioning;

namespace sharp_dependency.Parsers;

//TODO: Some configuration file, where it will be possible to configure which dependencies and how should be updated
public class Dependency
{
    public Dependency(string name, string currentVersion, string[] conditions, Action<string> updateVersionMethod)
    {
        Name = name;
        CurrentVersion = currentVersion;
        Conditions = conditions;
        UpdateVersionMethod = updateVersionMethod;
        CurrentNugetVersion = NuGetVersion.Parse(CurrentVersion);
    }

    public bool UpdateVersionIfPossible(IReadOnlyCollection<NuGetVersion> allVersions, bool includePrerelease, VersionLock versionLock, [NotNullWhen(true)] out NuGetVersion? newVersion)
    {
        newVersion = null;
        
        var versionRange = new VersionRange(CurrentNugetVersion, new FloatRange(GetVersionFloatBehavior(includePrerelease, versionLock), CurrentNugetVersion));
        var versionToUpdate = versionRange.FindBestMatch(allVersions);
        if (versionToUpdate is null)
        {
            return false;
        }

        if (!versionRange.IsBetter(CurrentNugetVersion, versionToUpdate))
        {
            return false;
        }

        newVersion = versionToUpdate;
        UpdateVersionMethod(versionToUpdate.ToNormalizedString());

        return true;
    }

    private NuGetVersionFloatBehavior GetVersionFloatBehavior(bool includePrerelease, VersionLock versionLock) =>
        (includePrerelease, versionLock) switch
        {
            (true, VersionLock.None) => NuGetVersionFloatBehavior.AbsoluteLatest,
            (true, VersionLock.Major) => NuGetVersionFloatBehavior.PrereleaseMinor,
            (true, VersionLock.Minor) => NuGetVersionFloatBehavior.PrereleasePatch,
            (false, VersionLock.None) => NuGetVersionFloatBehavior.Major,
            (false, VersionLock.Major) => NuGetVersionFloatBehavior.Minor,
            (false, VersionLock.Minor) => NuGetVersionFloatBehavior.Patch,
            _ => throw new ArgumentOutOfRangeException($"Could not create {nameof(NuGetVersionFloatBehavior)} from parameters includePrerelease: {includePrerelease}, versionLock: {versionLock}")
        };

    public string Name { get; }
    public string CurrentVersion { get; }
    public string[] Conditions { get; }
    private Action<string> UpdateVersionMethod { get; }
    private NuGetVersion CurrentNugetVersion { get; }
}