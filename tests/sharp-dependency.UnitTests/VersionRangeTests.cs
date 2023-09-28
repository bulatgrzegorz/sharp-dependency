using NuGet.Versioning;

namespace sharp_dependency.UnitTests;

//Those tests are created rather to understand how nuget API is working then to test their correctness
public class VersionRangeTests
{
    [Fact]
    public void NugetFindBaseMatch_Major()
    {
        var version = "0.1.2";
        var nugetVersion = NuGetVersion.Parse(version);
        var range = new VersionRange(nugetVersion, new FloatRange(NuGetVersionFloatBehavior.Major, nugetVersion));
        var versions = new[]
        {
            NuGetVersion.Parse("0.1.1"), 
            NuGetVersion.Parse("0.1.2"),
            NuGetVersion.Parse("0.1.3-v1"),
            NuGetVersion.Parse("0.1.3"),
            NuGetVersion.Parse("0.2.1"),
            NuGetVersion.Parse("0.2.2-v2"),
            NuGetVersion.Parse("1.0.0"), 
            NuGetVersion.Parse("1.0.1"), 
            NuGetVersion.Parse("2.0.0-test")
        };

        var bestMatch = range.FindBestMatch(versions);
        
        Assert.Equal(versions[^2], bestMatch);
    }

    [Fact]
    public void NugetFindBaseMatch_Minor()
    {
        var version = "0.1.2";
        var nugetVersion = NuGetVersion.Parse(version);
        var range = new VersionRange(nugetVersion, new FloatRange(NuGetVersionFloatBehavior.Minor, nugetVersion));
        var versions = new[]
        {
            NuGetVersion.Parse("0.1.1"), 
            NuGetVersion.Parse("0.1.2"),
            NuGetVersion.Parse("0.1.3-v1"),
            NuGetVersion.Parse("0.1.3"),
            NuGetVersion.Parse("0.2.1"),
            NuGetVersion.Parse("0.2.2-v2"),
            NuGetVersion.Parse("1.0.0"), 
            NuGetVersion.Parse("1.0.1"), 
            NuGetVersion.Parse("2.0.0-test")
        };

        var bestMatch = range.FindBestMatch(versions);
        
        Assert.Equal(versions[4], bestMatch);
    }
    
    [Fact]
    public void NugetFindBaseMatch_Patch()
    {
        var version = "0.1.2";
        var nugetVersion = NuGetVersion.Parse(version);
        var range = new VersionRange(nugetVersion, new FloatRange(NuGetVersionFloatBehavior.Patch, nugetVersion));
        var versions = new[]
        {
            NuGetVersion.Parse("0.1.1"), 
            NuGetVersion.Parse("0.1.2"),
            NuGetVersion.Parse("0.1.3-v1"),
            NuGetVersion.Parse("0.1.3"),
            NuGetVersion.Parse("0.2.1"),
            NuGetVersion.Parse("0.2.2-v2"),
            NuGetVersion.Parse("1.0.0"), 
            NuGetVersion.Parse("1.0.1"), 
            NuGetVersion.Parse("2.0.0-test")
        };

        var bestMatch = range.FindBestMatch(versions);
        
        Assert.Equal(versions[3], bestMatch);
    }
    
    [Fact]
    public void NugetFindBaseMatch_AbsoluteLatest()
    {
        var version = "0.1.2";
        var nugetVersion = NuGetVersion.Parse(version);
        var range = new VersionRange(nugetVersion, new FloatRange(NuGetVersionFloatBehavior.AbsoluteLatest, nugetVersion));
        var versions = new[]
        {
            NuGetVersion.Parse("0.1.1"), 
            NuGetVersion.Parse("0.1.2"),
            NuGetVersion.Parse("0.1.3-v1"),
            NuGetVersion.Parse("0.1.3"),
            NuGetVersion.Parse("0.2.1"),
            NuGetVersion.Parse("0.2.2-v2"),
            NuGetVersion.Parse("1.0.0"), 
            NuGetVersion.Parse("1.0.1"), 
            NuGetVersion.Parse("2.0.0-test")
        };

        var bestMatch = range.FindBestMatch(versions);
        
        Assert.Equal(versions[^1], bestMatch);
    }
}