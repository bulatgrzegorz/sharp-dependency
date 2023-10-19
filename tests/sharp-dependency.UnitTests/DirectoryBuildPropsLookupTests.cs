﻿namespace sharp_dependency.UnitTests;

public class DirectoryBuildPropsLookupTests
{
    [Theory]
    [InlineData("tests/testProj/testProj.csproj", "tests/Directory.Build.props")]
    [InlineData("tool/tool.csproj", "Directory.Build.props")]
    [InlineData("src/proj/project.csproj", "src/proj/Directory.Build.props")]
    public void Test(string projectPath, string expectedDirectoryBuildPropsFilePath)
    {
        var paths = new[]
        {
            "project.sln",
            "Directory.Build.props",
            "src/Directory.Build.props",
            "src/proj/project.csproj",
            "src/proj/Directory.Build.props",
            "tool/tool.csproj",
            "tests/Directory.Build.props",
            "tests/testProj/testProj.csproj"
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, projectPath);
        
        Assert.Equal(expectedDirectoryBuildPropsFilePath, directoryBuildProps);
    }
    
    [Theory]
    [InlineData(@"C:\dir\tests\testProj\testProj.csproj", @"C:\dir\tests\Directory.Build.props")]
    [InlineData(@"C:\dir\tool\tool.csproj", @"C:\dir\Directory.Build.props")]
    [InlineData(@"C:\dir\src\proj\project.csproj", @"C:\dir\src\proj\Directory.Build.props")]
    public void Test1(string projectPath, string expectedDirectoryBuildPropsFilePath)
    {
        var paths = new[]
        {
            @"C:\dir\project.sln",
            @"C:\dir\Directory.Build.props",
            @"C:\dir\src\Directory.Build.props",
            @"C:\dir\src\proj\project.csproj",
            @"C:\dir\src\proj\Directory.Build.props",
            @"C:\dir\tool\tool.csproj",
            @"C:\dir\tests\Directory.Build.props",
            @"C:\dir\tests\testProj\testProj.csproj"
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, projectPath, @"C:\dir");
        
        Assert.Equal(expectedDirectoryBuildPropsFilePath, directoryBuildProps);
    }
}