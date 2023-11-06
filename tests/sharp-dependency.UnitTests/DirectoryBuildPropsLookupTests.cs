using System.Runtime.InteropServices;

namespace sharp_dependency.UnitTests;

public class DirectoryBuildPropsLookupTests
{
    [Theory]
    [InlineData("tests/testProj/testProj.csproj", "tests/Directory.Build.props")]
    [InlineData("tool/tool.csproj", "Directory.Build.props")]
    [InlineData("src/proj/project.csproj", "src/proj/Directory.Build.props")]
    public void DirectoryBuildPropsLookup_WillReturnPropsFile_ForRelativePaths_GnuLike(string projectPath, string expectedDirectoryBuildPropsFilePath)
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
            "tests/testProj/testProj.csproj",
        };
        
        var directoryBuildProps = GetDirectoryBuildPropsPath(paths, projectPath);
        
        AssertPathAreEqual(expectedDirectoryBuildPropsFilePath, directoryBuildProps!);
    }
    
    [Theory]
    [InlineData(@"tests\testProj\testProj.csproj", @"tests\Directory.Build.props")]
    [InlineData(@"tool\tool.csproj", @"Directory.Build.props")]
    [InlineData(@"src\proj\project.csproj", @"src\proj\Directory.Build.props")]
    public void DirectoryBuildPropsLookup_WillReturnPropsFile_ForRelativePaths_WindowsLike(string projectPath, string expectedDirectoryBuildPropsFilePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        
        var paths = new[]
        {
            @"project.sln",
            @"Directory.Build.props",
            @"src\Directory.Build.props",
            @"src\proj\project.csproj",
            @"src\proj\Directory.Build.props",
            @"tool\tool.csproj",
            @"tests\Directory.Build.props",
            @"tests\testProj\testProj.csproj",
        };

        var directoryBuildProps = GetDirectoryBuildPropsPath(paths, projectPath);
        
        AssertPathAreEqual(expectedDirectoryBuildPropsFilePath, directoryBuildProps!);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForSingleProjectAtRoot_RelativePath()
    {
        var paths = new[]
        {
            "project.csproj"
        };

        var directoryBuildProps = GetDirectoryBuildPropsPath(paths, "project.csproj");
        
        Assert.Null(directoryBuildProps);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForSingleProjectInNestedDir_RelativePath()
    {
        var paths = new[]
        {
            "project.sln",
            "proj/project.csproj",
        };

        var directoryBuildProps = GetDirectoryBuildPropsPath(paths, "proj/project.csproj");
        
        Assert.Null(directoryBuildProps);
    }
    
    [Theory]
    [InlineData(@"C:\dir\tests\testProj\testProj.csproj", @"C:\dir\tests\Directory.Build.props")]
    [InlineData(@"C:\dir\tool\tool.csproj", @"C:\dir\Directory.Build.props")]
    [InlineData(@"C:\dir\src\proj\project.csproj", @"C:\dir\src\proj\Directory.Build.props")]
    public void DirectoryBuildPropsLookup_WillReturnPropsFile_ForAbsolutePaths_WindowsLike(string projectPath, string expectedDirectoryBuildPropsFilePath)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        
        var paths = new[]
        {
            @"C:\dir\Directory.Build.props",
            @"C:\dir\src\Directory.Build.props",
            @"C:\dir\src\proj\Directory.Build.props",
            @"C:\dir\tests\Directory.Build.props",
        };

        var directoryBuildProps = GetDirectoryBuildPropsPath(paths, projectPath, @"C:\dir");
        
        AssertPathAreEqual(expectedDirectoryBuildPropsFilePath, directoryBuildProps!);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnPropsFile_ForProjectOnBaseLevel()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        
        var paths = new[]
        {
            @"C:\dir\Directory.Build.props",
        };

        var directoryBuildProps = GetDirectoryBuildPropsPath(paths, @"C:\dir\project.csproj", @"C:\dir");
        
        AssertPathAreEqual(paths[0], directoryBuildProps!);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForSingleProjectAtRoot_AbsolutePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        
        var paths = new[]
        {
            @"C:\dir\project.csproj"
        };

        var directoryBuildProps = GetDirectoryBuildPropsPath(paths, @"C:\dir\project.csproj");
        
        Assert.Null(directoryBuildProps);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForSingleProjectInNestedDir_AbsolutePath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return;
        
        var paths = new[]
        {
            @"C:\dir\project.sln",
            @"C:\dir\proj\project.csproj",
        };

        var directoryBuildProps = GetDirectoryBuildPropsPath(paths, @"C:\dir\proj\project.csproj");
        
        Assert.Null(directoryBuildProps);
    }

    [Theory]
    [InlineData("/home/usr/repo/tests/testProj/testProj.csproj", "/home/usr/repo/tests/Directory.Build.props")]
    [InlineData("/home/usr/repo/tool/tool.csproj", "/home/usr/repo/Directory.Build.props")]
    [InlineData("/home/usr/repo/src/proj/project.csproj", "/home/usr/repo/src/proj/Directory.Build.props")]
    public void DirectoryBuildPropsLookup_WillReturnPropsFile_ForAbsolutePaths_GnuLike(string projectPath, string expectedDirectoryBuildPropsFilePath)
    {
        var paths = new[]
        {
            "/home/usr/repo/Directory.Build.props",
            "/home/usr/repo/src/Directory.Build.props",
            "/home/usr/repo/src/proj/Directory.Build.props",
            "/home/usr/repo/tests/Directory.Build.props",
        };

        var directoryBuildProps = GetDirectoryBuildPropsPath(paths, projectPath, "/home/usr/repo");
        
        AssertPathAreEqual(expectedDirectoryBuildPropsFilePath, directoryBuildProps!);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForEmptyDirectoryBuildPropsList()
    {
        var directoryBuildProps = GetDirectoryBuildPropsPath(ArraySegment<string>.Empty, @"C:\dir\project.csproj", @"C:\dir");
        
        Assert.Null(directoryBuildProps);
    }

    private void AssertPathAreEqual(string path1, string path2)
    {
        Assert.Equal(path1, path2);
    }
     
    private static string? GetDirectoryBuildPropsPath(IReadOnlyCollection<string> repositoryPaths, string projectPath)
    {
        return DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(repositoryPaths, projectPath);
    }
    
    private static string? GetDirectoryBuildPropsPath(IReadOnlyCollection<string> directoryBuildPropsFiles, string projectPath, string basePath)
    {
        return DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(directoryBuildPropsFiles, projectPath, basePath);
    }
    
    internal static class PathExtensions
    {
        public static string NormalizePath(string path) =>
            path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}