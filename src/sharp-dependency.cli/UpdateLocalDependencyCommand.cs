using System.ComponentModel;
using NuGet.Configuration;
using sharp_dependency.cli.Logger;
using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli;

// ReSharper disable once ClassNeverInstantiated.Global
// ReSharper disable once UnusedAutoPropertyAccessor.Global
internal sealed class UpdateLocalDependencyCommand : LocalDependencyCommandBase<UpdateLocalDependencyCommand.Settings>
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Settings : CommandSettings 
    {
        [Description("Path to solution/csproj which dependency should be updated")]
        [CommandArgument(0, "[path]")]
        public string? Path { get; init; }

        [Description("Command will determine dependencies to be updated without actually updating them.")]
        [CommandOption("--dry-run")]
        public bool DryRun { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var currentConfiguration = await SettingsManager.GetSettings<Configuration>();
        if (currentConfiguration is null)
        {
            Console.WriteLine("[ERROR]: There is no configuration created yet. Use -h|--help for more info.");
            return 1;
        }

        if (currentConfiguration.NugetConfiguration is null)
        {
            Console.WriteLine("[ERROR]: There is no nuget configuration created yet. Use -h|--help for more info.");
            return 1;
        }
        
        var packageSourceProvider = new PackageSourceProvider(new NuGet.Configuration.Settings(currentConfiguration.NugetConfiguration.ConfigFileDirectory, currentConfiguration.NugetConfiguration.ConfigFileName));
        var packageSources = packageSourceProvider.LoadPackageSources().ToList();
        if (packageSources is { Count: 0 })
        {
            Console.WriteLine("[ERROR]: Given nuget configuration has no package sources. We cannot determine any package version using it.");
            return 1;
        }

        var nugetManager = new NugetPackageSourceMangerChain(packageSources.Select(x => new NugetPackageSourceManger(x)).ToArray());
        var projectUpdater = new ProjectUpdater(nugetManager, new ProjectDependencyUpdateLogger());
        
        var (basePath, projectPaths, directoryBuildPropsPaths) = GetRepositoryFiles(settings.Path);

        foreach (var projectPath in projectPaths)
        {
            var directoryBuildPropsPath = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(directoryBuildPropsPaths, projectPath, basePath);
            var directoryBuildPropsContent = directoryBuildPropsPath is not null ? await File.ReadAllTextAsync(projectPath) : null;
            var projectContent = await File.ReadAllTextAsync(projectPath);

            var updatedProject = await projectUpdater.Update(new ProjectUpdater.UpdateProjectRequest(projectPath, projectContent, directoryBuildPropsContent));

            if (!settings.DryRun && updatedProject.UpdatedContent is not null)
            {
                await File.WriteAllTextAsync(projectPath, updatedProject.UpdatedContent);
            }
        }

        return 0;
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.Path))
        {
            if (!settings.Path.EndsWith(".sln", StringComparison.InvariantCultureIgnoreCase) && !settings.Path.EndsWith(".csproj", StringComparison.InvariantCultureIgnoreCase))
            {
                return ValidationResult.Error($"Setting {nameof(settings.Path)} must be either solution (sln) or project (csproj) file.");
            }
        }
        
        return ValidationResult.Success();
    }
}