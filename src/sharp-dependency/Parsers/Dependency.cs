using System.Diagnostics.CodeAnalysis;
using NuGet.Versioning;

namespace sharp_dependency.Parsers;

//TODO: Some configuration file, where it will be possible to configure which dependencies and how should be updated
//TODO: CLI tool - update remote repository (with PR)
//TODO: Support directory.build.props files
//TODO: Support target framework conditions in csproj item groups
public class Dependency
{
    public Dependency(string name, string currentVersion, Action<string> updateVersionMethod)
    {
        Name = name;
        CurrentVersion = currentVersion;
        UpdateVersionMethod = updateVersionMethod;
        CurrentNugetVersion = NuGetVersion.Parse(CurrentVersion);
        VersionRange = new VersionRange(CurrentNugetVersion, new FloatRange(NuGetVersionFloatBehavior.Major, CurrentNugetVersion));
    }

    public bool UpdateVersionIfPossible(IReadOnlyCollection<NuGetVersion> allVersions, [NotNullWhen(true)] out NuGetVersion? newVersion)
    {
        newVersion = null;
        var versionToUpdate = VersionRange.FindBestMatch(allVersions);
        if (versionToUpdate is null)
        {
            return false;
        }

        if (!VersionRange.IsBetter(CurrentNugetVersion, versionToUpdate))
        {
            return false;
        }

        newVersion = versionToUpdate;
        UpdateVersionMethod(versionToUpdate.ToNormalizedString());

        return true;
    }

    public string Name { get; }
    public string CurrentVersion { get; }
    private Action<string> UpdateVersionMethod { get; }
    private VersionRange VersionRange { get; }
    private NuGetVersion CurrentNugetVersion { get; }
}