using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NuGet.Versioning;
using sharp_dependency.cli.Logger;
using sharp_dependency.cli.Repositories;
using sharp_dependency.Logger;
using sharp_dependency.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli.DependencyCommands;

// ReSharper disable ClassNeverInstantiated.Global
internal sealed class MigrateRepositoryDependencyCommand : RepositoryDependencyCommandBase<MigrateRepositoryDependencyCommand.Settings>
{
    private static readonly JsonSerializerOptions CamelCaseJsonSerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    
    public sealed class Settings : CommandSettings
    {
        [Description("Name of repository source that should be used. If not given, current context will be used.")]
        [CommandOption("-n|--name")]
        public string? RepositorySourceName { get; init; }
        
        [Description("Name of repository that we should update dependencies in.")]
        [CommandOption("-r|--repository")]
        public string? Repository { get; init; }

        [Description("Name of a project within which we should update repositorty. Value is required for Server Bitbucket source type.")]
        [CommandOption("-p|--project")]
        public string? Project { get; init; }
        
        [Description("Name of a workspace within which we should update repositorty. Value is required for Cloud Bitbucket source type.")]
        [CommandOption("-w|--workspace")]
        public string? Workspace { get; init; }
        
        [Description("Name of a branch on which dependencies updates should be commited at.")]
        [CommandOption("-b|--branch")]
        public string? BranchName { get; init; }
        
        [Description("Commit message with which dependencies update commit will going to be done.")]
        [CommandOption("--commitMessage")]
        public string? CommitMessage { get; init; }
        
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
        [CommandOption("--update-path")]
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
            Log.LogError("There is no configuration created yet. Use -h|--help for more info.");
            return 1;
        }
        
        var nugetManager = currentConfiguration.GetNugetManager();
        var bitbucket = currentConfiguration.GetBitbucket(settings.RepositorySourceName);
        if (bitbucket is null)
        {
            return 1;
        }
        
        if (settings.Repository is null)
        {
            var bitbucketProjectManager = BitbucketManagerFactory.CreateProjectManager(bitbucket.ApiAddress, settings.Workspace, settings.Project, bitbucket);

            var repositories = await bitbucketProjectManager.GetRepositories();

            foreach (var repository in repositories)
            {
                await MigrateRepository(settings, repository, bitbucket, nugetManager);
            }
        }
        else
        {
            await MigrateRepository(settings, settings.Repository, bitbucket, nugetManager);
        }

        return 0;
    }

    private async Task MigrateRepository(Settings settings, string repository, Configuration.Bitbucket bitbucket, NugetPackageSourceMangerChain nugetManager)
    {
        var bitbucketManager = BitbucketManagerFactory.CreateRepositoryManager(bitbucket.ApiAddress, settings.Workspace, settings.Project, repository, bitbucket);

        try
        {
            await MigrateRepository(settings, bitbucketManager, nugetManager);
        }
        catch (Exception e)
        {
            Log.LogError("Occured while migrating repository: {0} in project: {1}", repository, (settings.Project ?? settings.Workspace)!);
            Log.LogDebug("Error: {0}", e.Message);
        }
    }

    private async Task MigrateRepository(Settings settings, IRepositoryManger bitbucketManager, NugetPackageSourceMangerChain nugetManager)
    {
        var instructions = await GetMigrationInstructions(settings);
        
        var repositoryPaths = (await bitbucketManager.GetRepositoryFilePaths()).ToList();

        var projectMigrator = new ProjectMigrator(nugetManager, new ProjectDependencyUpdateLogger());

        var projectPaths = await GetProjectPaths(repositoryPaths, bitbucketManager);

        //TODO: We should check if anything was actually updated in project before
        //TODO: Refactor loggers/response from project updater
        var updatedProjects = new List<UpdatedProject>(projectPaths.Count);
        foreach (var projectPath in projectPaths)
        {
            var directoryBuildPropsFile = DirectoryBuildPropsLookup.GetDirectoryBuildPropsPath(repositoryPaths, projectPath);
            var projectContent = await bitbucketManager.GetFileContentRaw(projectPath);
            var directoryBuildPropsContent = directoryBuildPropsFile is not null ? await bitbucketManager.GetFileContentRaw(directoryBuildPropsFile) : null;
            var migratedProject = await projectMigrator.Update(new ProjectMigrator.UpdateProjectRequest(projectPath, projectContent, directoryBuildPropsContent, instructions));

            if (migratedProject?.UpdatedDependencies is not {Count: > 0}) continue;

            updatedProjects.Add(migratedProject);
        }

        if (settings.DryRun || updatedProjects.Count == 0)
        {
            return;
        }

        var branch = settings.BranchName ?? "sharp-dependency";
        var commitMessage = settings.CommitMessage ?? "update dependencies";
        var pullRequestName = $"[{branch}] pull request";
        
        await bitbucketManager.CreatePullRequest(pullRequestName, branch, commitMessage, updatedProjects);
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
        var migrationConfiguration = JsonSerializer.Deserialize<MigrationConfiguration>(migrationConfigurationContent, CamelCaseJsonSerializerOptions);
        return migrationConfiguration is not { Update.Count: > 0 } ? Array.Empty<ProjectMigrator.MigrationInstruction>() : Convert(migrationConfiguration).ToArray();
    }
    
    //TODO: Refactor - same in MigrateLocalDependencyCommand 
    private ProjectMigrator.MigrationInstruction ConvertUpdateStringToInstruction(string value)
    {
        try
        {
            var parts = value.Split(":");
            return new ProjectMigrator.MigrationInstruction(parts[0].Trim(), VersionRange.Parse(parts[1].Trim()));
        }
        catch (Exception)
        {
            Log.LogError("Could not correctly parse parameter value \"{0}\" to instruction. It should be passed in format: \"package.name:[1.2.0,2.0.0)\"", value);
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
        if (string.IsNullOrEmpty(settings.Project) && string.IsNullOrEmpty(settings.Workspace))
        {
            return ValidationResult.Error($"Neither {nameof(settings.Project)} or {nameof(settings.Workspace)} must have a value (depending of bitbucket type you are using).");
        }

        if (!string.IsNullOrEmpty(settings.BranchName) && settings.BranchName.Length > 255)
        {
            return ValidationResult.Error("Branch name has to be shorter then 255 chars.");
        }
        
        if (!string.IsNullOrEmpty(settings.CommitMessage) && settings.CommitMessage.Length > 72)
        {
            return ValidationResult.Error("Commit message has to be shorter then 72 chars.");
        }
        
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