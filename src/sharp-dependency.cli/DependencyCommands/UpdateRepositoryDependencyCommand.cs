using System.ComponentModel;
using sharp_dependency.cli.Logger;
using sharp_dependency.cli.Repositories;
using sharp_dependency.Logger;
using sharp_dependency.Parsers;
using sharp_dependency.Repositories;
using Spectre.Console;
using Spectre.Console.Cli;

namespace sharp_dependency.cli.DependencyCommands;

internal sealed class UpdateRepositoryDependencyCommand : RepositoryDependencyCommandBase<UpdateRepositoryDependencyCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Name of repository source that should be used. If not given, current context will be used.")]
        [CommandOption("-n|--name")]
        public string? RepositorySourceName { get; init; }
        
        [Description("Name of repository that we should update dependencies in.")]
        [CommandOption("-r|--repository")]
        public string Repository { get; init; } = null!;

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
        
        [Description("Command will look for pre-release versions of packages.")]
        [CommandOption("--prerelease")]
        [DefaultValue(false)]
        public bool IncludePrerelease { get; init; }
        
        [Description("Specifies whether the package should be locked to the current Major or Minor version. Possible values: None (default), Major, Minor")]
        [CommandOption("-v|--version-lock")]
        [DefaultValue(VersionLock.None)]
        public VersionLock VersionLock { get; init; }
        
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

        var bitbucketManager = BitbucketManagerFactory.CreateRepositoryManager(bitbucket.ApiAddress, settings.Workspace, settings.Project, settings.Repository, bitbucket);

        var repositoryPaths = (await bitbucketManager.GetRepositoryFilePaths()).ToList();

        var projectUpdater = new ProjectUpdater(nugetManager, new ProjectDependencyUpdateLogger());
        
        var projectPaths = await GetProjectPaths(repositoryPaths, bitbucketManager);

        //TODO: We should check if anything was actually updated in project before
        //TODO: Refactor loggers/response from project updater
        var updatedProjects = new List<UpdatedProject>(projectPaths.Count);
        foreach (var projectPath in projectPaths)
        {
            var projectContent = await GetProjectContent(bitbucketManager, projectPath);
            var directoryBuildPropsContent = await GetDirectoryBuildPropsContent(bitbucketManager, repositoryPaths, projectPath);
            
            var updatedProject = await projectUpdater.Update(new ProjectUpdater.UpdateProjectRequest(projectPath, projectContent, directoryBuildPropsContent, settings.IncludePrerelease, settings.VersionLock));
         
            if (updatedProject?.UpdatedDependencies is not {Count: > 0}) continue;

            updatedProjects.Add(updatedProject);
        }

        if (settings.DryRun || updatedProjects.Count == 0)
        {
            return 0;
        }

        var branch = settings.BranchName ?? "sharp-dependency";
        var commitMessage = settings.CommitMessage ?? "update dependencies";
        var pullRequestName = $"[{branch}] pull request";
        
        await bitbucketManager.CreatePullRequest(pullRequestName, branch, commitMessage, updatedProjects);

        return 0;
    }
    
    public override ValidationResult Validate(CommandContext context, Settings settings)
    {
        if (string.IsNullOrEmpty(settings.Repository))
        {
            return ValidationResult.Error($"Setting {nameof(settings.Repository)} must have a value.");
        }
        
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
        
        return ValidationResult.Success();
    }
}