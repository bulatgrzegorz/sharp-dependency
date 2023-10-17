using NuGet.Frameworks;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace sharp_dependency.UnitTests;

public class NugetPackageSourceManagerTests
{
    [Fact]
    public void PackageSelector_WhenNoTargetFrameworks_PackageWillNotBeSelected()
    {
        var packageContent = """
        {
            "@id": "Lib",
            "version": "7.0.12",
            "listed": true
        }
""";
        
        var packageMetadata = GetPackageMetadata(packageContent);
        Assert.False(PackageSelector.GetVersionIfSelected(packageMetadata, new List<NuGetFramework>(), out _));
    }
    
    [Fact]
    public void PackageSelector_NotListedPackageWillNotBeSelected()
    {
        var packageContent = """
        {
            "id": "Lib",
            "version": "4.1.10331",
            "listed": false
        }
""";

        var packageMetadata = GetPackageMetadata(packageContent);
        Assert.False(PackageSelector.GetVersionIfSelected(packageMetadata, new List<NuGetFramework>(){NuGetFramework.Parse("net8.0")}, out _));
    }

    [Fact]
    public void PackageSelector_PackageWithoutAnyDependencyWillBeSelected()
    {
        var packageContent = """
        {
            "@id": "Lib",
            "version": "7.0.12",
            "listed": true
        }
""";
        
        var packageMetadata = GetPackageMetadata(packageContent);
        Assert.True(PackageSelector.GetVersionIfSelected(packageMetadata, new List<NuGetFramework>(){NuGetFramework.Parse("net8.0")}, out _));
    }
    
    [Fact]
    public void PackageSelector_PackageWithNoCompatibleDependencyFramework_WillNotBeSelected()
    {
        var packageContent = """
        {
            "@id": "Lib",
            "dependencyGroups": [
              {
                "targetFramework": "net8.0"
              }
            ],
            "version": "7.0.12",
            "listed": true
        }
""";
        
        var packageMetadata = GetPackageMetadata(packageContent);
        Assert.False(PackageSelector.GetVersionIfSelected(packageMetadata, new List<NuGetFramework>(){NuGetFramework.Parse("net45")}, out _));
    }
    
    [Fact]
    public void PackageSelector_PackageWithNoCompatibleDependencyFramework_MultipleTargets_WillNotBeSelected()
    {
        var packageContent = """
        {
            "@id": "Lib",
            "dependencyGroups": [
              {
                "targetFramework": "net8.0"
              }
            ],
            "version": "7.0.12",
            "listed": true
        }
""";
        
        var packageMetadata = GetPackageMetadata(packageContent);
        Assert.False(PackageSelector.GetVersionIfSelected(packageMetadata, new List<NuGetFramework>(){NuGetFramework.Parse("net45"), NuGetFramework.Parse("net8.0")}, out _));
    }
    
    [Fact]
    public void PackageSelector_PackageWithCompatibleButNotExactDependency_WillBeSelected()
    {
        var packageContent = """
        {
            "@id": "Lib",
            "dependencyGroups": [
              {
                "targetFramework": "netstandard2.0"
              }
            ],
            "version": "7.0.12",
            "listed": true
        }
""";
        
        var packageMetadata = GetPackageMetadata(packageContent);
        Assert.True(PackageSelector.GetVersionIfSelected(packageMetadata, new List<NuGetFramework>(){NuGetFramework.Parse("net48"), NuGetFramework.Parse("net8.0")}, out _));
    }
    
    private static IPackageSearchMetadata GetPackageMetadata(string content)
    {
        return content.FromJson<PackageSearchMetadata>();
    }
}