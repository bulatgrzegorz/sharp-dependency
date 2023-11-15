using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
Path to migration configuration file. It should contain instructions about dependencies changes, in format:
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
        [CommandOption("-p|--update-path")]
        public string? MigrationConfigPath { get; init; }

        [Description("""
Update instractions for migration. Multiple of them can be passed in format: 
"package.name:[[1.2.0,2.0.0)". 
Version should be passed in range format as explained in update-path parameter.
""")]
        [CommandOption("-u|--update")] 
        public string[] Updates { get; init; } = Array.Empty<string>();
        
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

        var instructions = await GetMigrationInstructions(settings);

        var nugetManager = currentConfiguration.GetNugetManager();

        var projectMigrator = new ProjectMigrator(nugetManager, new ProjectDependencyUpdateLogger());
        
        var (basePath, projectPaths, directoryBuildPropsPaths) = GetRepositoryFiles(settings.Path);
        
        foreach (var projectPath in projectPaths)
        {
            var directoryBuildPropsPath = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(directoryBuildPropsPaths, projectPath, basePath);
            var directoryBuildPropsContent = directoryBuildPropsPath is not null ? await File.ReadAllTextAsync(projectPath) : null;
            var projectContent = await File.ReadAllTextAsync(projectPath);

            
            var updatedProject = await projectMigrator.Update(new ProjectMigrator.UpdateProjectRequest(projectPath, projectContent, directoryBuildPropsContent, instructions));

            if (!settings.DryRun && updatedProject.UpdatedContent is not null)
            {
                await File.WriteAllTextAsync(projectPath, updatedProject.UpdatedContent);
            }
        }
        
        return 0;
    }

    private async Task<ProjectMigrator.MigrationInstruction[]> GetMigrationInstructions(Settings settings)
    {
        if (string.IsNullOrEmpty(settings.MigrationConfigPath))
        {
            return settings.Updates.Select(ConvertUpdateStringToInstruction).ToArray();
        }
        
        if (!File.Exists(settings.MigrationConfigPath))
        {
            throw new ArgumentException($"Could not find migration configuration file with path: {settings.MigrationConfigPath}");
        }
        
        var migrationConfigurationContent = await File.ReadAllTextAsync(settings.MigrationConfigPath!);
        var migrationConfiguration = JsonSerializer.Deserialize<MigrationConfiguration>(migrationConfigurationContent, new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return migrationConfiguration is not { Update.Count: > 0 } ? Array.Empty<ProjectMigrator.MigrationInstruction>() : Convert(migrationConfiguration).ToArray();

    }

    private ProjectMigrator.MigrationInstruction ConvertUpdateStringToInstruction(string value)
    {
        try
        {
            var parts = value.Split(":");
            return new ProjectMigrator.MigrationInstruction(parts[0].Trim(), VersionRange.Parse(parts[1].Trim()));
        }
        catch (Exception)
        {
            Console.WriteLine("[ERROR]: Could not correctly parse parameter value \"{0}\" to instruction. It should be passed in format: \"package.name:[1.2.0,2.0.0)\"", value);
            throw;
        }

    }
    
    private IEnumerable<ProjectMigrator.MigrationInstruction> Convert(MigrationConfiguration configuration)
    {
        foreach (var (packageName, packageVersionRange) in configuration.Update)
        {
            if (!VersionRange.TryParse(packageVersionRange, out var versionRange))
            {
                throw new ArgumentException($"Could not correctly parse parameter value \"{packageVersionRange}\" to instruction. It should be passed in format: \"package.name:[1.2.0,2.0.0)\"");
            }
            
            yield return new ProjectMigrator.MigrationInstruction(packageName, versionRange);
        }
    }
    
    [SuppressMessage("ReSharper", "CollectionNeverUpdated.Local")]
    private class MigrationConfiguration
    {
        public Dictionary<string, string> Update { get; set; } = new();
    }

    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.MigrationConfigPath) && settings.Updates is {Length: 0})
        {
            return ValidationResult.Error("Either updates path or updates parameter need to be provided.");
        }
        
        if (!string.IsNullOrWhiteSpace(settings.MigrationConfigPath) && settings.Updates is {Length: > 0})
        {
            return ValidationResult.Error("Updates path and updates parameter cannot be provided jointly.");
        }
        
        return ValidationResult.Success();
    }
}