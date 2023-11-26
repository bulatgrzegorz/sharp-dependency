using System.Reflection;
using System.Text.Json;
using Moq;
using NuGet.Versioning;
using sharp_dependency.Parsers;

namespace sharp_dependency.IntegrationTests;

public class ProjectUpdaterTests
{
    [Fact]
    public async Task ProjectUpdater_WillUpdateEfCore_WhenNewVersionAvailable()
    {
        await VerifyProjectPackageUpdate("Microsoft.EntityFrameworkCore", "simple.csproj", "7.0.12");
    }
    
    [Fact]
    public async Task ProjectUpdater_WillUpdateEfCore_WithConditionOnItemGroup_WhenNewVersionAvailable()
    {
        await VerifyProjectPackageUpdate("Microsoft.EntityFrameworkCore", "simpleWithItemGroupCondition.csproj", "7.0.12");
    }
    
    [Fact]
    public async Task ProjectUpdater_WillUpdateEfCore_WithConditionOnPackage_WhenNewVersionAvailable()
    {
        await VerifyProjectPackageUpdate("Microsoft.EntityFrameworkCore", "simpleWithPackageCondition.csproj", "7.0.12");
    }
    
    [Fact]
    public async Task ProjectUpdater_WillNotUpdateEfCore_WhenNewVersionNotAvailable()
    {
        await VerifyProjectPackageUpdate("Microsoft.EntityFrameworkCore", "simple_newest.csproj", "7.0.12");
    }
    
    [Fact]
    public async Task ProjectUpdater_WillNotUpdateEfCore_WithFalseConditionOnItemGroup_WhenNewVersionAvailable()
    {
        await VerifyProjectPackageUpdate("Microsoft.EntityFrameworkCore", "simpleWithFalseItemGroupCondition.csproj", "7.0.11");
    }
    
    [Fact]
    public async Task ProjectUpdater_WillNotUpdateEfCore_WithFalseConditionOnPackage_WhenNewVersionAvailable()
    {
        await VerifyProjectPackageUpdate("Microsoft.EntityFrameworkCore", "simpleWithFalsePackageCondition.csproj", "7.0.11");
    }
    
    [Fact]
    public async Task ProjectUpdater_WillUpdateEfCore_WhenNewVersionAvailable_AndTargetFrameworkOnDirectoryBuildPropsOnly()
    {
        await VerifyProjectPackageUpdate("Microsoft.EntityFrameworkCore", "simple_withoutTargetFramework.csproj", "Net8TargetFramework.Directory.Build.props", "7.0.12");
    }
    
    [Theory]
    [InlineData(false, VersionLock.Minor, "7.0.3")]
    [InlineData(true, VersionLock.Minor, "7.0.3")]
    [InlineData(false, VersionLock.Major, "7.2.0")]
    [InlineData(true, VersionLock.Major, "7.2.0")]
    [InlineData(false, VersionLock.None, "7.2.0")]
    [InlineData(true, VersionLock.None, "8.0.0-rc.2.23479.6")]
    public async Task ProjectUpdater_WillUpgradeInRespectOf_IncludePrereleaseFlag_AndVersionLock(bool includePrerelease, VersionLock versionLock, string expectedVersion)
    {
        await VerifyProjectPackageUpdate("System.Text.Json", "versionLockPrereleaseFlag.csproj", expectedVersion, includePrerelease, versionLock);
    }
    
    [Theory]
    [InlineData(false, VersionLock.Minor, "7.0.3")]
    [InlineData(true, VersionLock.Minor, "7.0.3")]
    [InlineData(false, VersionLock.Major, "7.2.0")]
    [InlineData(true, VersionLock.Major, "7.2.0")]
    [InlineData(false, VersionLock.None, "7.2.0")]
    [InlineData(true, VersionLock.None, "8.0.0-rc.2.23479.6")]
    public async Task ProjectUpdater_WillUpgradeInRespectOf_IncludePrereleaseFlag_AndVersionLock_WhenCurrentPrerelease(bool includePrerelease, VersionLock versionLock, string expectedVersion)
    {
        await VerifyProjectPackageUpdate("System.Text.Json", "versionLockPrereleaseFlag_Prerelease.csproj", expectedVersion, includePrerelease, versionLock);
    }
    
    private async Task VerifyProjectPackageUpdate(string package, string projectName, string directoryBuildProps, string expectedVersion)
    {
        var packageManager = new Mock<IPackageMangerService>();

        MockPackageManagerWithTestFile(packageManager, package);

        var projectUpdater = new ProjectUpdater(packageManager.Object, new DummyLogger());

        var projectContent = GetProjectContent(projectName);
        var directoryBuildPropsContent = GetProjectContent(directoryBuildProps);
        var updateProjectResult = await projectUpdater.Update(new ProjectUpdater.UpdateProjectRequest(projectName, projectContent, directoryBuildPropsContent, false, VersionLock.None));

        var efCore = await GetProjectDependency(updateProjectResult!.UpdatedContent!, package);

        Assert.Equal(expectedVersion, efCore.CurrentVersion);
    }
    
    private async Task VerifyProjectPackageUpdate(string package, string projectName, string expectedVersion, bool includePrerelease = false, VersionLock versionLock = VersionLock.None)
    {
        var packageManager = new Mock<IPackageMangerService>();

        MockPackageManagerWithTestFile(packageManager, package);

        var projectUpdater = new ProjectUpdater(packageManager.Object, new DummyLogger());

        var projectContent = GetProjectContent(projectName);
        var updateProjectResult = await projectUpdater.Update(new ProjectUpdater.UpdateProjectRequest(projectName, projectContent, default, includePrerelease, versionLock));

        var efCore = await GetProjectDependency(updateProjectResult!.UpdatedContent!, package);

        Assert.Equal(expectedVersion, efCore.CurrentVersion);
    }
    
    private static async Task<Dependency> GetProjectDependency(string projectContent, string packageName)
    {
        var projectFileParser = new ProjectFileParser(projectContent);
        var updatedProjectFile = await projectFileParser.Parse();
        return updatedProjectFile.Dependencies.Single(x => x.Name == packageName);
    }
    
    private void MockPackageManagerWithTestFile(Mock<IPackageMangerService> packageManager, string packageName)
    {
        packageManager
            .Setup(x => x.GetPackageVersions(packageName, It.Is<IEnumerable<string>>(y => y.Contains("net8.0")), It.IsAny<bool>()))
            .ReturnsAsync(() => GetTestNugetVersions(packageName));
        
        packageManager
            .Setup(x => x.GetPackageVersions(packageName, It.Is<IEnumerable<string>>(y => !y.Contains("net8.0")), It.IsAny<bool>()))
            .ReturnsAsync(() => ArraySegment<NuGetVersion>.Empty);
    }
    
    private static string GetProjectContent(string relativeProjectPath)
    {
        var executionAssemblyLocation = Assembly.GetExecutingAssembly().Location;
        var projectFilePath = Path.Combine(Directory.GetParent(executionAssemblyLocation)!.FullName, "testProjects", relativeProjectPath);
        
        return File.ReadAllText(projectFilePath);
    }
    
    private static string GetTestDataFile(string fileName)
    {
        var executionAssemblyLocation = Assembly.GetExecutingAssembly().Location;
        var projectFilePath = Path.Combine(Directory.GetParent(executionAssemblyLocation)!.FullName, "testData", fileName);
        
        return File.ReadAllText(projectFilePath);
    }

    private static IReadOnlyCollection<NuGetVersion> GetTestNugetVersions(string packageName)
    {
        var testData = GetTestDataFile($"{packageName}Versions.json");
        var versions = JsonSerializer.Deserialize<List<string>>(testData)!;

        return versions.Select(NuGetVersion.Parse).ToList();
    }
}