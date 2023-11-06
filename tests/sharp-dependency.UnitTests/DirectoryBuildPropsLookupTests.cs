namespace sharp_dependency.UnitTests;
using static DirectoryBuildPropsLookupTests.PathExtensions;

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
            NormalizePath("project.sln"),
            NormalizePath("Directory.Build.props"),
            NormalizePath("src/Directory.Build.props"),
            NormalizePath("src/proj/project.csproj"),
            NormalizePath("src/proj/Directory.Build.props"),
            NormalizePath("tool/tool.csproj"),
            NormalizePath("tests/Directory.Build.props"),
            NormalizePath("tests/testProj/testProj.csproj")
        };
        
        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, NormalizePath(projectPath));
        
        Assert.Equal(NormalizePath(expectedDirectoryBuildPropsFilePath), directoryBuildProps);
    }
    
    [Theory]
    [InlineData(@"tests\testProj\testProj.csproj", @"tests\Directory.Build.props")]
    [InlineData(@"tool\tool.csproj", @"Directory.Build.props")]
    [InlineData(@"src\proj\project.csproj", @"src\proj\Directory.Build.props")]
    public void DirectoryBuildPropsLookup_WillReturnPropsFile_ForRelativePaths_WindowsLike(string projectPath, string expectedDirectoryBuildPropsFilePath)
    {
        var paths = new[]
        {
            NormalizePath(@"project.sln"),
            NormalizePath(@"Directory.Build.props"),
            NormalizePath(@"src\Directory.Build.props"),
            NormalizePath(@"src\proj\project.csproj"),
            NormalizePath(@"src\proj\Directory.Build.props"),
            NormalizePath(@"tool\tool.csproj"),
            NormalizePath(@"tests\Directory.Build.props"),
            NormalizePath(@"tests\testProj\testProj.csproj")
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, NormalizePath(projectPath));
        
        Assert.Equal(NormalizePath(expectedDirectoryBuildPropsFilePath), directoryBuildProps);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForSingleProjectAtRoot_RelativePath()
    {
        var paths = new[]
        {
            "project.csproj"
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, "project.csproj");
        
        Assert.Null(directoryBuildProps);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForSingleProjectInNestedDir_RelativePath()
    {
        var paths = new[]
        {
            NormalizePath("project.sln"),
            NormalizePath("proj/project.csproj")
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, NormalizePath("proj/project.csproj"));
        
        Assert.Null(directoryBuildProps);
    }
    
    [Theory]
    [InlineData(@"C:\dir\tests\testProj\testProj.csproj", @"C:\dir\tests\Directory.Build.props")]
    [InlineData(@"C:\dir\tool\tool.csproj", @"C:\dir\Directory.Build.props")]
    [InlineData(@"C:\dir\src\proj\project.csproj", @"C:\dir\src\proj\Directory.Build.props")]
    public void DirectoryBuildPropsLookup_WillReturnPropsFile_ForAbsolutePaths_WindowsLike(string projectPath, string expectedDirectoryBuildPropsFilePath)
    {
        var paths = new[]
        {
            NormalizePath(@"C:\dir\Directory.Build.props"),
            NormalizePath(@"C:\dir\src\Directory.Build.props"),
            NormalizePath(@"C:\dir\src\proj\Directory.Build.props"),
            NormalizePath(@"C:\dir\tests\Directory.Build.props"),
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, projectPath, NormalizePath(@"C:\dir"));
        
        Assert.Equal(NormalizePath(expectedDirectoryBuildPropsFilePath), directoryBuildProps);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnPropsFile_ForProjectOnBaseLevel()
    {
        var paths = new[]
        {
            NormalizePath(@"C:\dir\Directory.Build.props"),
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, @"C:\dir\project.csproj", NormalizePath(@"C:\dir"));
        
        Assert.Equal(paths[0], directoryBuildProps);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForSingleProjectAtRoot_AbsolutePath()
    {
        var paths = new[]
        {
            NormalizePath(@"C:\dir\project.csproj")
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, NormalizePath(@"C:\dir\project.csproj"));
        
        Assert.Null(directoryBuildProps);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForSingleProjectInNestedDir_AbsolutePath()
    {
        var paths = new[]
        {
            NormalizePath(@"C:\dir\project.sln"),
            NormalizePath(@"C:\dir\proj\project.csproj")
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, NormalizePath(@"C:\dir\proj\project.csproj"));
        
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
            NormalizePath("/home/usr/repo/Directory.Build.props"),
            NormalizePath("/home/usr/repo/src/Directory.Build.props"),
            NormalizePath("/home/usr/repo/src/proj/Directory.Build.props"),
            NormalizePath("/home/usr/repo/tests/Directory.Build.props"),
        };

        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(paths, projectPath, NormalizePath("/home/usr/repo"));
        
        Assert.Equal(NormalizePath(expectedDirectoryBuildPropsFilePath), directoryBuildProps);
    }
    
    [Fact]
    public void DirectoryBuildPropsLookup_WillReturnNullForEmptyDirectoryBuildPropsList()
    {
        var directoryBuildProps = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(ArraySegment<string>.Empty, @"C:\dir\project.csproj", NormalizePath(@"C:\dir"));
        
        Assert.Null(directoryBuildProps);
    }
    
    internal static class PathExtensions
    {
        public static string NormalizePath(string path) =>
            path.Replace(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}