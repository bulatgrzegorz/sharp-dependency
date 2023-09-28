using NuGet.Frameworks;

namespace sharp_dependency.UnitTests;

//Those tests are created rather to understand how nuget API is working then to test their correctness
public class FrameworkReducerTests
{
    [Fact]
    public void NugetGetNearest_WillReturnExactMatch()
    {
        var reducer = new FrameworkReducer();
        var framework = NuGetFramework.Parse("net6.0", new DefaultFrameworkNameProvider());
        var frameworks = new[]
        {
            NuGetFramework.Parse("net6.0", new DefaultFrameworkNameProvider()),
            NuGetFramework.Parse("net7.0", new DefaultFrameworkNameProvider())
        };

        var nearest = reducer.GetNearest(framework, frameworks);
        Assert.Equal(framework, nearest);
    }
    
    [Fact]
    public void NugetGetNearest_WillReturnOlderCoreVersion()
    {
        var reducer = new FrameworkReducer();
        var framework = NuGetFramework.Parse("net6.0", new DefaultFrameworkNameProvider());
        var frameworks = new[]
        {
            NuGetFramework.Parse("netcoreapp3.1", new DefaultFrameworkNameProvider())
        };

        var nearest = reducer.GetNearest(framework, frameworks);
        Assert.NotNull(nearest);
    }
    
    [Fact]
    public void NugetGetNearest_WillReturnStandardVersion()
    {
        var reducer = new FrameworkReducer();
        var framework = NuGetFramework.Parse("net6.0", new DefaultFrameworkNameProvider());
        var frameworks = new[]
        {
            NuGetFramework.Parse("netstandard2.1", new DefaultFrameworkNameProvider())
        };

        var nearest = reducer.GetNearest(framework, frameworks);
        Assert.NotNull(nearest);
    }
    
    [Fact]
    public void NugetGetNearest_WillReturnNullForOnlyNewerFramework()
    {
        var reducer = new FrameworkReducer();
        var framework = NuGetFramework.Parse("net6.0", new DefaultFrameworkNameProvider());
        var frameworks = new[]
        {
            NuGetFramework.Parse("net7.0", new DefaultFrameworkNameProvider())
        };

        var nearest = reducer.GetNearest(framework, frameworks);
        Assert.Null(nearest);
    }
}