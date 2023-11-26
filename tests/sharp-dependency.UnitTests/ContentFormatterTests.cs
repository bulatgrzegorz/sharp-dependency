using sharp_dependency.Repositories;

namespace sharp_dependency.UnitTests;

public class ContentFormatterTests
{
    [Fact]
    public void FormatPullRequestDescription_ReturnEmptyString_WhenNoProjectUpdated()
    {
        var result = ContentFormatter.FormatPullRequestDescription(new Description());

        Assert.Empty(result);
    }
    
    [Fact]
    public void FormatPullRequestDescription_ReturnEmptyString_WhenNoProjectWithDependenciesUpdated()
    {
        var result = ContentFormatter.FormatPullRequestDescription(new Description(){UpdatedProjects = new List<UpdatedProject>()});

        Assert.Empty(result);
    }
    
    [Fact]
    public void FormatPullRequestDescription_ReturnProjectWithDependency()
    {
        var result = ContentFormatter.FormatPullRequestDescription(new Description(){UpdatedProjects = new List<UpdatedProject>()
        {
            new(){Name = "ExampleProject/x.csproj", UpdatedDependencies = new List<Dependency>(){new(){Name = "depName", CurrentVersion = "1.0.0", NewVersion = "1.0.1"}}},
        }});

        var expected = """
* ExampleProject/x.csproj
    * depName 1.0.0 -> 1.0.1
""";
        var expectedWithAdjustNewLine = expected.Replace(Environment.NewLine, "\n");
        Assert.Equal(expectedWithAdjustNewLine, result);
    }
    
    [Fact]
    public void FormatPullRequestDescription_ReturnMultipleProjectWithDependencies()
    {
        var result = ContentFormatter.FormatPullRequestDescription(new Description(){UpdatedProjects = new List<UpdatedProject>()
        {
            new()
            {
                Name = "ExampleProject/x.csproj", 
                UpdatedDependencies = new List<Dependency>() { new() { Name = "depName", CurrentVersion = "1.0.0", NewVersion = "1.0.1" }}
            },
            new()
            {
                Name = "tests/x2.csproj", 
                UpdatedDependencies = new List<Dependency>()
                {
                    new() { Name = "depName", CurrentVersion = "1.0.0", NewVersion = "1.0.1" },
                    new() { Name = "depNameAnother", CurrentVersion = "1.0.0", NewVersion = "1.0.1-test-version" },
                }
            },
        }});

        var expected = """
* ExampleProject/x.csproj
    * depName 1.0.0 -> 1.0.1
* tests/x2.csproj
    * depName 1.0.0 -> 1.0.1
    * depNameAnother 1.0.0 -> 1.0.1-test-version
""";
        var expectedWithAdjustNewLine = expected.Replace(Environment.NewLine, "\n");
        Assert.Equal(expectedWithAdjustNewLine, result);
    }
}