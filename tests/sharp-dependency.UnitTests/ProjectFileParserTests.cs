using System.Collections.ObjectModel;
using NuGet.Versioning;
using sharp_dependency.Parsers;

namespace sharp_dependency.UnitTests;

public class ProjectFileParserTests
{
    [Fact]
    public async Task ProjectFileParser_ParsingPackageDependenciesWithVersionsWell()
    {
        var content = """
<Project Sdk="Microsoft.NET.Sdk.Web">

	<PropertyGroup>
		<TargetFramework>net6.0</TargetFramework>
	</PropertyGroup>

	<ItemGroup>
		<PackageReference Include="Lib1" Version="0.0.25" />
		<PackageReference Include="Lib2"><Version>0.1.0-beta.3</Version></PackageReference>
		<PackageReference Include="Lib3" version="1.0.25" />
		<PackageReference Include="Lib4"><version>1.0.0-beta.4</version></PackageReference>
	</ItemGroup>

	<ItemGroup>
	  <ProjectReference Include="..\Data.Core\Core.csproj" />
	  <ProjectReference Include="..\Data.Contracts\Data.Contracts.csproj" />
	</ItemGroup>

</Project>
""";
        
        await using var parser = new ProjectFileParser(content);
        var projectFile = await parser.Parse();
        var dependencies = projectFile.Dependencies.ToArray();
        
        Assert.Equal("Lib1", dependencies[0].Name);
        Assert.Equal("0.0.25", dependencies[0].CurrentVersion);
        
        Assert.Equal("Lib2", dependencies[1].Name);
        Assert.Equal("0.1.0-beta.3", dependencies[1].CurrentVersion);
        
        Assert.Equal("Lib3", dependencies[2].Name);
        Assert.Equal("1.0.25", dependencies[2].CurrentVersion);
        
        Assert.Equal("Lib4", dependencies[3].Name);
        Assert.Equal("1.0.0-beta.4", dependencies[3].CurrentVersion);
    }
    
    [Fact]
    public async Task ProjectFileParser_UpdatePackageVersionWell()
    {
	    var content = """
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<TargetFramework>net6.0</TargetFramework>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="Lib1" Version="0.0.25" />
	</ItemGroup>
</Project>
""";
	    
	    var expectedResultContent = """
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<TargetFramework>net6.0</TargetFramework>
	</PropertyGroup>
	<ItemGroup>
		<PackageReference Include="Lib1" Version="1.0.4" />
	</ItemGroup>
</Project>
""";
        
	    await using var parser = new ProjectFileParser(content);
	    var projectFile = await parser.Parse();

	    var dependency = projectFile.Dependencies.First(x => x.Name == "Lib1");
	    dependency.UpdateVersionIfPossible(new Collection<NuGetVersion>(){NuGetVersion.Parse("1.0.4")}, out _);

	    var resultContent = await parser.Generate();
	    
	    Assert.Equal(expectedResultContent, resultContent);
    }
    
    [Fact]
    public async Task ProjectFileParser_ParseTargetFrameworkWell()
    {
	    var content = """
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<TargetFramework>net6.0</TargetFramework>
	</PropertyGroup>
</Project>
""";
        
	    await using var parser = new ProjectFileParser(content);
	    var projectFile = await parser.Parse();

	    var targetFrameworks = projectFile.TargetFrameworks.ToArray();
	    Assert.Single(targetFrameworks);
	    Assert.Equal("net6.0", targetFrameworks[0]);
    }
    
    [Fact]
    public async Task ProjectFileParser_ParseTargetFrameworksWell()
    {
	    var content = """
<Project Sdk="Microsoft.NET.Sdk.Web">
	<PropertyGroup>
		<TargetFrameworks>net6.0;netstandard2.0</TargetFrameworks>
	</PropertyGroup>
</Project>
""";
        
	    await using var parser = new ProjectFileParser(content);
	    var projectFile = await parser.Parse();

	    var targetFrameworks = projectFile.TargetFrameworks.ToArray();
	    Assert.Equal(2, targetFrameworks.Length);
	    Assert.Equal("net6.0", targetFrameworks[0]);
	    Assert.Equal("netstandard2.0", targetFrameworks[1]);
    }
    
    [Fact]
    public async Task ProjectFileParser_ParseTargetFrameworkWell_WhereNone()
    {
	    var content = """
<Project Sdk="Microsoft.NET.Sdk.Web">
</Project>
""";
        
	    await using var parser = new ProjectFileParser(content);
	    var projectFile = await parser.Parse();

	    Assert.Empty(projectFile.TargetFrameworks);
    }
}