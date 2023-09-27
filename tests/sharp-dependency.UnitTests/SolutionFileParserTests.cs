namespace sharp_dependency.UnitTests;

public class SolutionFileParserTests
{
	[Fact]
    public void GetProjectPaths_ReturnsProjectPathsCorrectly()
    {
        var solutionContent = """
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.3.32929.385
MinimumVisualStudioVersion = 15.0.26124.0
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Market.Data", "Market.Data\Market.Data.csproj", "{6A72DC0A-65C5-4CA7-AA6C-8954E6A4FD13}"
EndProject
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "Market.Data.UnitTests", "tests\Market.Data.UnitTests\Market.Data.UnitTests.csproj", "{717EBBD6-C354-47F9-9F9C-417AD86BEB14}"
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Release|Any CPU = Release|Any CPU
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{6A72DC0A-65C5-4CA7-AA6C-8954E6A4FD13}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{6A72DC0A-65C5-4CA7-AA6C-8954E6A4FD13}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{6A72DC0A-65C5-4CA7-AA6C-8954E6A4FD13}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{6A72DC0A-65C5-4CA7-AA6C-8954E6A4FD13}.Release|Any CPU.Build.0 = Release|Any CPU
		{717EBBD6-C354-47F9-9F9C-417AD86BEB14}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{717EBBD6-C354-47F9-9F9C-417AD86BEB14}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{717EBBD6-C354-47F9-9F9C-417AD86BEB14}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{717EBBD6-C354-47F9-9F9C-417AD86BEB14}.Release|Any CPU.Build.0 = Release|Any CPU
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
	GlobalSection(ExtensibilityGlobals) = postSolution
		SolutionGuid = {2C47ADA4-AA6A-4389-B03C-3E302EDF5B98}
	EndGlobalSection
EndGlobal
""";
        
        var parser = new SolutionFileParser();
        var projects = parser.GetProjectPaths(new FileContent(solutionContent.Split("\n"), "")).ToArray();
     
        Assert.Equal(@"Market.Data\Market.Data.csproj", projects[0]);
        Assert.Equal(@"tests\Market.Data.UnitTests\Market.Data.UnitTests.csproj", projects[1]);
    }
}