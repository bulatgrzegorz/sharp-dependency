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
	    dependency.UpdateVersion("1.0.4");

	    var resultContent = await parser.Generate();
	    
	    Assert.Equal(expectedResultContent, resultContent);
    }
}