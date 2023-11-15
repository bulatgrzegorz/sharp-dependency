using System.ComponentModel;
using System.Text.Json;
using NuGet.Versioning;
using sharp_dependency.cli.Logger;
using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli.DependencyCommands;

public class MigrateLocalDependencyCommand : LocalDependencyCommandBase<MigrateLocalDependencyCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [Description("Path to solution/csproj which dependency should be updated")]
        [CommandArgument(0, "[path]")]
        public string? Path { get; init; }
        
        //Arrows might seems little bit odd, but they are inlining fine in console
        [Description("""
Path to migration configuration file. It should contain instructions about dependencies changes, like:
{
    "update": {
        "package.name.with.exact.version": "[[1.2.1]]",
        "package.name.with.range": "(1.0,)"
    }
}

Version should be passed in range format:
1.0           --> 1.0 ≤ x
(,1.0]]        --> x ≤ 1.0
(,1.0)        --> x < 1.0
[[1.0]]         --> x == 1.0
(1.0,)        --> 1.0 < x
(1.0, 2.0)    --> 1.0 < x < 2.0
[[1.0, 2.0]]    --> 1.0 ≤ x ≤ 2.0
""")]
        //TODO: Rename to instructions?
        [CommandOption("--conf")]
        public string MigrationConfigPath { get; init; }
        
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

        if (!File.Exists(settings.MigrationConfigPath))
        {
            throw new ArgumentException($"Could not find migration configuration file with path: {settings.MigrationConfigPath}");
        }

        var migrationConfigurationContent = await File.ReadAllTextAsync(settings.MigrationConfigPath);
        var migrationConfiguration = JsonSerializer.Deserialize<MigrationConfiguration>(migrationConfigurationContent, new JsonSerializerOptions(){PropertyNamingPolicy = JsonNamingPolicy.CamelCase});
        if (migrationConfiguration is not { Update.Count: > 0 })
        {
            return 0;
        }
        
        var nugetManager = currentConfiguration.GetNugetManager();

        var projectMigrator = new ProjectMigrator(nugetManager, new ProjectDependencyUpdateLogger());
        
        var (basePath, projectPaths, directoryBuildPropsPaths) = GetRepositoryFiles(settings.Path);
        
        foreach (var projectPath in projectPaths)
        {
            var directoryBuildPropsPath = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(directoryBuildPropsPaths, projectPath, basePath);
            var directoryBuildPropsContent = directoryBuildPropsPath is not null ? await File.ReadAllTextAsync(projectPath) : null;
            var projectContent = await File.ReadAllTextAsync(projectPath);

            var instructions = Convert(migrationConfiguration).ToList();
            var updatedProject = await projectMigrator.Update(new ProjectMigrator.UpdateProjectRequest(projectPath, projectContent, directoryBuildPropsContent, instructions));

            if (!settings.DryRun && updatedProject.UpdatedContent is not null)
            {
                await File.WriteAllTextAsync(projectPath, updatedProject.UpdatedContent);
            }
        }
        
        return 0;
    }

    private IEnumerable<ProjectMigrator.MigrationInstruction> Convert(MigrationConfiguration configuration)
    {
        foreach (var (packageName, packageVersionRange) in configuration.Update)
        {
            yield return new ProjectMigrator.MigrationInstruction(
                ProjectMigrator.Instruction.Update, 
                packageName,
                VersionRange.Parse(packageVersionRange));
        }

        foreach (var packageName in configuration.Remove)
        {
            //TODO: Fix it
            yield return new ProjectMigrator.MigrationInstruction(
                ProjectMigrator.Instruction.Remove, 
                packageName,
                null!);
        }
    }
    
    private class MigrationConfiguration
    {
        public Dictionary<string, string> Update { get; set; } = new();
        public string[] Remove { get; set; } = Array.Empty<string>();
    }
}