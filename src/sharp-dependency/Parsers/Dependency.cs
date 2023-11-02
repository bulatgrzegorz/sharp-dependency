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
    public string[] Conditions { get; }
    private Action<string> UpdateVersionMethod { get; }
    private VersionRange VersionRange { get; }
    private NuGetVersion CurrentNugetVersion { get; }
}