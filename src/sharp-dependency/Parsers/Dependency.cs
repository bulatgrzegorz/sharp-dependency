using System.Diagnostics.CodeAnalysis;
using NuGet.Versioning;

namespace sharp_dependency.Parsers;

//TODO: Some configuration file, where it will be possible to configure which dependencies and how should be updated
public class Dependency
{
    public Dependency(string name, string currentVersion, string[] conditions, Action<string> updateVersionMethod, Action removePackageMethod)
    {
        Name = name;
        CurrentVersion = currentVersion;
        Conditions = conditions;
        UpdateVersionMethod = updateVersionMethod;
        RemovePackageMethod = removePackageMethod;
        CurrentNugetVersion = NuGetVersion.Parse(CurrentVersion);
    }

    public void RemoveDependency()
    {
        RemovePackageMethod();
    }
    
    public bool UpdateVersionIfPossible(IReadOnlyCollection<NuGetVersion> allVersions, VersionRange versionRange, [NotNullWhen(true)] out NuGetVersion? newVersion)
    {
        newVersion = null;

        var versionToUpdate = versionRange.FindBestMatch(allVersions);
        if (versionToUpdate is null)
        {
            return false;
        }

        //if current version satisfies range, and is better then best match (algorithm favor lower versions for example) we will not update it
        if (versionRange.Satisfies(CurrentNugetVersion) && !versionRange.IsBetter(CurrentNugetVersion, versionToUpdate))
        {
            return false;
        }

        newVersion = versionToUpdate;
        UpdateVersionMethod(versionToUpdate.ToNormalizedString());

        return true;
    }
    
    public bool UpdateVersionIfPossible(IReadOnlyCollection<NuGetVersion> allVersions, bool includePrerelease, VersionLock versionLock, [NotNullWhen(true)] out NuGetVersion? newVersion)
    {
        newVersion = null;

        var versionRange = GetVersionRange(includePrerelease, versionLock);
        var versionToUpdate = versionRange.FindBestMatch(allVersions);
        if (versionToUpdate is null)
        {
            return false;
        }

        //here we do not need to check if current version satisfies range, as it was build based on it
        if (!versionRange.IsBetter(CurrentNugetVersion, versionToUpdate))
        {
            return false;
        }

        newVersion = versionToUpdate;
        UpdateVersionMethod(versionToUpdate.ToNormalizedString());

        return true;
    }

    private VersionRange GetVersionRange(bool includePrerelease, VersionLock versionLock)
    {
        if (versionLock != VersionLock.None && includePrerelease)
        {
            var prefix = NuGetVersion.TryParse(CurrentVersion, out var nugetVersion)
                ? nugetVersion.IsPrerelease ? nugetVersion.ReleaseLabels.First() : string.Empty
                : string.Empty;
            return new VersionRange(CurrentNugetVersion, new FloatRange(GetVersionFloatBehavior(includePrerelease, versionLock), CurrentNugetVersion, prefix));
        }

        return new VersionRange(CurrentNugetVersion, new FloatRange(GetVersionFloatBehavior(includePrerelease, versionLock), CurrentNugetVersion));
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
    private Action RemovePackageMethod { get; }
    private NuGetVersion CurrentNugetVersion { get; }
}