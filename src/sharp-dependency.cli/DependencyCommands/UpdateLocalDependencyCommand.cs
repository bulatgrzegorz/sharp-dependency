﻿using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using sharp_dependency.cli.Logger;
using sharp_dependency.Parsers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli.DependencyCommands;

// ReSharper disable once ClassNeverInstantiated.Global
// ReSharper disable once UnusedAutoPropertyAccessor.Global
internal sealed class UpdateLocalDependencyCommand : LocalDependencyCommandBase<UpdateLocalDependencyCommand.Settings>
{
    // ReSharper disable once ClassNeverInstantiated.Global
    [SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
    public sealed class Settings : CommandSettings 
    {
        [Description("Path to solution/csproj which dependency should be updated")]
        [CommandArgument(0, "[path]")]
        public string? Path { get; init; }

        [Description("Command will determine dependencies to be updated without actually updating them.")]
        [CommandOption("--dry-run")]
        public bool DryRun { get; init; }
        
        [Description("Command will look for pre-release versions of packages.")]
        [CommandOption("--prerelease")]
        [DefaultValue(false)]
        public bool IncludePrerelease { get; init; }
        
        [Description("Specifies whether the package should be locked to the current Major or Minor version. Possible values: None (default), Major, Minor")]
        [CommandOption("-v|--version-lock")]
        [DefaultValue(VersionLock.None)]
        public VersionLock VersionLock { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var currentConfiguration = await SettingsManager.GetSettings<Configuration>();
        if (currentConfiguration is null)
        {
            Console.WriteLine("[ERROR]: There is no configuration created yet. Use -h|--help for more info.");
            return 1;
        }

        var nugetManager = currentConfiguration.GetNugetManager();

        var packages = await nugetManager.GetPackageVersions("System.Text.Json", new[] { "net6.0" }, true);
        var pp = packages.Select(x => x.ToNormalizedString()).ToList();
        var projectUpdater = new ProjectUpdater(nugetManager, new ProjectDependencyUpdateLogger());
        
        var (basePath, projectPaths, directoryBuildPropsPaths) = GetRepositoryFiles(settings.Path);

        foreach (var projectPath in projectPaths)
        {
            var directoryBuildPropsPath = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(directoryBuildPropsPaths, projectPath, basePath);
            var directoryBuildPropsContent = directoryBuildPropsPath is not null ? await File.ReadAllTextAsync(projectPath) : null;
            var projectContent = await File.ReadAllTextAsync(projectPath);

            var updatedProject = await projectUpdater.Update(new ProjectUpdater.UpdateProjectRequest(projectPath, projectContent, directoryBuildPropsContent, settings.IncludePrerelease, settings.VersionLock));

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